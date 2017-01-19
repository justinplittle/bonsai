﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Bonsai.Shaders.Configuration
{
    [DisplayName(XmlTypeName)]
    [XmlType(TypeName = XmlTypeName)]
    public class ViewportEffectConfiguration : ShaderConfiguration
    {
        const string XmlTypeName = "ViewportEffect";
        readonly TexturedQuad texturedQuad = new TexturedQuad();

        [Category("Shaders")]
        [Description("Specifies the path to the fragment shader.")]
        [FileNameFilter("Fragment Shader Files (*.frag)|*.frag|All Files (*.*)|*.*")]
        [Editor("Bonsai.Design.OpenFileNameEditor, Bonsai.Design", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        public string FragmentShader { get; set; }

        [Category("State")]
        [Description("Optional quad geometry transformation effects.")]
        public QuadEffects QuadEffects
        {
            get { return texturedQuad.QuadEffects; }
            set { texturedQuad.QuadEffects = value; }
        }

        public override Shader CreateShader(ShaderWindow window)
        {
            var fragmentSource = File.ReadAllText(FragmentShader);
            var effect = new ViewportEffect(
                Name, window,
                fragmentSource,
                RenderState,
                ShaderUniforms,
                BufferBindings,
                Framebuffer,
                texturedQuad);
            effect.Enabled = Enabled;
            return effect;
        }

        public override string ToString()
        {
            var name = Name;
            if (string.IsNullOrEmpty(name)) return XmlTypeName;
            else return string.Format("{0} [{1}]", name, XmlTypeName);
        }
    }
}