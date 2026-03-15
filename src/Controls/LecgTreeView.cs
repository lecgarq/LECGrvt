using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

namespace LECG.Controls
{
    /// <summary>
    /// Specialized TreeView for LECG with batch expansion/collapse capabilities.
    /// Supports professional Revit-style hierarchical navigation.
    /// </summary>
    public class LecgTreeView : TreeView
    {
        static LecgTreeView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LecgTreeView), new FrameworkPropertyMetadata(typeof(LecgTreeView)));
        }

        public void ExpandAll()
        {
            SetExpansionState(this, true);
        }

        public void CollapseAll()
        {
            SetExpansionState(this, false);
        }

        private void SetExpansionState(ItemsControl container, bool isExpanded)
        {
            if (container == null) return;

            foreach (object item in container.Items)
            {
                TreeViewItem tvi = container.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (tvi != null)
                {
                    tvi.IsExpanded = isExpanded;
                    SetExpansionState(tvi, isExpanded);
                }
            }
        }
    }
}
