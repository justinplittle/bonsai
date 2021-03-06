﻿using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace Bonsai.Resources.Design
{
    public partial class CollectionEditorDialog : Form
    {
        int initialHeight;
        int initialCollectionEditorHeight;
        EditorSite editorSite;

        internal CollectionEditorDialog(CollectionEditor editor)
        {
            InitializeComponent();
            collectionEditorControl.Editor = editor;
            editorSite = new EditorSite(this);
            propertyGrid.Site = editorSite;
        }

        internal IServiceProvider ServiceProvider { get; set; }

        internal CollectionEditorControl EditorControl
        {
            get { return collectionEditorControl; }
        }

        public IEnumerable Items
        {
            get { return collectionEditorControl.Items; }
            set { collectionEditorControl.Items = value; }
        }

        protected override void OnLoad(EventArgs e)
        {
            initialHeight = Height;
            initialCollectionEditorHeight = propertyGrid.Height;
            OnResize(EventArgs.Empty);
            base.OnLoad(e);
        }

        protected override void OnResize(EventArgs e)
        {
            if (initialHeight > 0)
            {
                var expansion = Height - initialHeight;
                collectionEditorControl.Height = initialCollectionEditorHeight + expansion;
            }
            base.OnResize(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            propertyGrid.SelectedObject = null;
            base.OnFormClosed(e);
        }

        void collectionEditorControl_SelectedItemChanged(object sender, EventArgs e)
        {
            var collectionEditor = (CollectionEditorControl)sender;
            propertyGrid.SelectedObjects = collectionEditor.SelectedItems.Cast<object>().ToArray();
        }

        void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            collectionEditorControl.Refresh();
        }

        class EditorSite : ISite
        {
            CollectionEditorDialog siteDialog;

            public EditorSite(CollectionEditorDialog dialog)
            {
                siteDialog = dialog;
            }

            public IComponent Component
            {
                get { return null; }
            }

            public IContainer Container
            {
                get { return null; }
            }

            public bool DesignMode
            {
                get { return false; }
            }

            public string Name { get; set; }

            public object GetService(Type serviceType)
            {
                if (siteDialog.ServiceProvider != null)
                {
                    return siteDialog.ServiceProvider.GetService(serviceType);
                }

                return null;
            }
        }
    }
}
