﻿using Bonsai.Dag;
using Bonsai.Design;
using Bonsai.Expressions;
using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Bonsai.Editor.Scripting
{
    class ScriptComponentEditor : WorkflowComponentEditor
    {
        const string ScriptFilter = "C# Files (*.cs)|*.cs|All Files (*.*)|*.*";
        static readonly string[] IgnoreAssemblyReferences = new[] { "mscorlib.dll", "System.dll", "System.Core.dll", "System.Reactive.Linq.dll" };

        static CodeTypeReference CreateTypeReference(Type type)
        {
            var typeName = (type.IsPrimitive || type == typeof(string)) ? type.FullName : type.Name;
            var reference = new CodeTypeReference(typeName);
            if (type.IsArray)
            {
                reference.ArrayElementType = CreateTypeReference(type.GetElementType());
                reference.ArrayRank = type.GetArrayRank();
            }

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    reference.TypeArguments.Add(CreateTypeReference(argument));
                }
            }
            return reference;
        }

        string GetTypeName(Type type)
        {
            using (var provider = new CSharpCodeProvider())
            {
                var typeReference = CreateTypeReference(type);
                return provider.GetTypeOutput(typeReference);
            }
        }

        void CollectNamespaces(Type type, HashSet<string> namespaces)
        {
            namespaces.Add(type.Namespace);
            if (type.IsGenericType)
            {
                var genericArguments = type.GetGenericArguments();
                for (int i = 0; i < genericArguments.Length; i++)
                {
                    CollectNamespaces(genericArguments[i], namespaces);
                }
            }
        }

        void CollectAssemblyReferences(Type type, HashSet<string> assemblyReferences)
        {
            var assemblyName = type.Assembly.GetName().Name;
            assemblyReferences.Add(assemblyName);
            if (type.IsGenericType)
            {
                var genericArguments = type.GetGenericArguments();
                for (int i = 0; i < genericArguments.Length; i++)
                {
                    CollectAssemblyReferences(genericArguments[i], assemblyReferences);
                }
            }
        }

        public override bool EditComponent(ITypeDescriptorContext context, object component, IServiceProvider provider, IWin32Window owner)
        {
            var scriptComponent = (CSharpScript)component;
            if (scriptComponent == null || provider == null || string.IsNullOrWhiteSpace(scriptComponent.Name)) return false;

            var workflowBuilder = (WorkflowBuilder)provider.GetService(typeof(WorkflowBuilder));
            var commandExecutor = (CommandExecutor)provider.GetService(typeof(CommandExecutor));
            var selectionModel = (WorkflowSelectionModel)provider.GetService(typeof(WorkflowSelectionModel));
            var scriptEnvironment = (IScriptEnvironment)provider.GetService(typeof(IScriptEnvironment));
            if (workflowBuilder == null || commandExecutor == null || selectionModel == null || scriptEnvironment == null)
            {
                return false;
            }

            var selectedView = selectionModel.SelectedView;
            if (selectedView == null) return false;

            var selectedNode = selectionModel.SelectedNodes.SingleOrDefault();
            if (selectedNode == null) return false;

            if (scriptEnvironment.AssemblyName != null)
            {
                var existingType = Type.GetType(scriptComponent.Name + ", " + scriptEnvironment.AssemblyName.FullName);
                if (existingType != null)
                {
                    throw new InvalidOperationException("An extension type with the name " + scriptComponent.Name + " already exists.");
                }
            }

            Type inputType;
            var builderNode = (Node<ExpressionBuilder, ExpressionBuilderArgument>)selectedNode.Tag;
            var predecessor = selectedView.Workflow.Predecessors(builderNode).FirstOrDefault();
            if (predecessor != null)
            {
                var expression = workflowBuilder.Workflow.Build(predecessor.Value);
                inputType = expression.Type;
            }
            else inputType = typeof(IObservable<int>);

            var scriptFile = scriptComponent.Name;
            using (var dialog = new SaveFileDialog { FileName = scriptFile, Filter = ScriptFilter })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return false;
                scriptFile = dialog.FileName;

                var namespaces = new HashSet<string>();
                var assemblyReferences = new HashSet<string>();
                var hasDescription = !string.IsNullOrWhiteSpace(scriptComponent.Description);
                assemblyReferences.Add("Bonsai.Core");
                namespaces.Add("Bonsai");
                namespaces.Add("System");
                if (hasDescription) namespaces.Add("System.ComponentModel");
                namespaces.Add("System.Collections.Generic");
                namespaces.Add("System.Linq");
                namespaces.Add("System.Reactive.Linq");
                CollectNamespaces(inputType, namespaces);
                CollectAssemblyReferences(inputType, assemblyReferences);
                assemblyReferences.ExceptWith(IgnoreAssemblyReferences);
                scriptEnvironment.AddAssemblyReferences(assemblyReferences);

                var typeName = GetTypeName(inputType);
                var scriptBuilder = new StringBuilder();
                foreach (var ns in namespaces) scriptBuilder.AppendLine("using " + ns + ";");
                scriptBuilder.AppendLine();
                scriptBuilder.AppendLine("[Combinator]");
                if (hasDescription) scriptBuilder.AppendLine("[Description(\"" + scriptComponent.Description + "\")]");
                scriptBuilder.AppendLine("[WorkflowElementCategory(ElementCategory." + scriptComponent.Category + ")]");
                scriptBuilder.AppendLine("public class " + scriptComponent.Name);
                scriptBuilder.AppendLine("{");
                scriptBuilder.AppendLine("    public " + typeName + " Process(" + (predecessor != null ? typeName + " source)" : ")"));
                scriptBuilder.AppendLine("    {");
                string template;
                switch (scriptComponent.Category)
                {
                    case ElementCategory.Source: template = "Observable.Return(0)"; break;
                    case ElementCategory.Condition: template = "source.Where(value => true)"; break;
                    case ElementCategory.Transform: template = "source.Select(value => value)"; break;
                    case ElementCategory.Sink: template = "source.Do(value => Console.WriteLine(value))"; break;
                    case ElementCategory.Combinator: template = "source"; break;
                    default: throw new InvalidOperationException("The specified element category is not allowed for automatic script generation.");
                }
                scriptBuilder.AppendLine("        return " + template + ";");
                scriptBuilder.AppendLine("    }");
                scriptBuilder.AppendLine("}");

                using (var writer = new StreamWriter(scriptFile))
                {
                    writer.Write(scriptBuilder);
                }
            }

            var assemblyName = new AssemblyName("@DynamicExtensions");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.FullName);
            var typeBuilder = moduleBuilder.DefineType(
                scriptComponent.Name,
                TypeAttributes.Public | TypeAttributes.Class,
                predecessor == null ? typeof(ZeroArgumentExpressionBuilder) : typeof(SingleArgumentExpressionBuilder));
            var descriptionAttributeBuilder = new CustomAttributeBuilder(
                typeof(DescriptionAttribute).GetConstructor(new[] { typeof(string) }),
                new object[] { scriptComponent.Description });
            var categoryAttributeBuilder = new CustomAttributeBuilder(
                typeof(WorkflowElementCategoryAttribute).GetConstructor(new[] { typeof(ElementCategory) }),
                new object[] { scriptComponent.Category });
            typeBuilder.SetCustomAttribute(descriptionAttributeBuilder);
            typeBuilder.SetCustomAttribute(categoryAttributeBuilder);
            var buildMethod = typeBuilder.DefineMethod("Build",
                MethodAttributes.Public |
                MethodAttributes.ReuseSlot |
                MethodAttributes.Virtual |
                MethodAttributes.HideBySig,
                typeof(Expression),
                new[] { typeof(IEnumerable<Expression>) });
            var generator = buildMethod.GetILGenerator();
            var fieldInfo = typeof(ExpressionBuilder).GetField("EmptyExpression");
            generator.Emit(OpCodes.Ldsfld, fieldInfo);
            generator.Emit(OpCodes.Ret);

            var type = typeBuilder.CreateType();
            var builder = new CombinatorBuilder();
            builder.Combinator = (object)Activator.CreateInstance(type);
            selectedView.CreateGraphNode(builder, selectedNode, CreateGraphNodeType.Successor, branch: false, validate: false);
            selectedView.DeleteGraphNodes(selectionModel.SelectedNodes);
            commandExecutor.Execute(() => { }, null);

            ScriptEditorLauncher.Launch(owner, scriptEnvironment.ProjectFileName, scriptFile);
            return true;
        }
    }
}
