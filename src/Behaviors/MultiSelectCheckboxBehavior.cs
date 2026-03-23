using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LECG.Behaviors
{
    public static class MultiSelectCheckboxBehavior
    {
        public static readonly DependencyProperty EnableRangeSelectionProperty =
            DependencyProperty.RegisterAttached(
                "EnableRangeSelection",
                typeof(bool),
                typeof(MultiSelectCheckboxBehavior),
                new PropertyMetadata(false, OnEnableRangeSelectionChanged));

        private static readonly DependencyProperty AnchorItemProperty =
            DependencyProperty.RegisterAttached(
                "AnchorItem",
                typeof(object),
                typeof(MultiSelectCheckboxBehavior),
                new PropertyMetadata(null));

        public static bool GetEnableRangeSelection(DependencyObject obj) =>
            obj == null ? throw new ArgumentNullException(nameof(obj)) :
            (bool)obj.GetValue(EnableRangeSelectionProperty);

        public static void SetEnableRangeSelection(DependencyObject obj, bool value) =>
            (obj ?? throw new ArgumentNullException(nameof(obj)))
            .SetValue(EnableRangeSelectionProperty, value);

        private static object? GetAnchorItem(DependencyObject obj) =>
            (obj ?? throw new ArgumentNullException(nameof(obj)))
            .GetValue(AnchorItemProperty);

        private static void SetAnchorItem(DependencyObject obj, object? value) =>
            (obj ?? throw new ArgumentNullException(nameof(obj)))
            .SetValue(AnchorItemProperty, value);

        private static void OnEnableRangeSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CheckBox checkBox)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                checkBox.PreviewMouseLeftButtonDown += CheckBoxOnPreviewMouseLeftButtonDown;
            }
            else
            {
                checkBox.PreviewMouseLeftButtonDown -= CheckBoxOnPreviewMouseLeftButtonDown;
            }
        }

        private static void CheckBoxOnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not CheckBox checkBox || checkBox.DataContext == null)
            {
                return;
            }

            if (!TryGetOwningItemsControl(checkBox, out ItemsControl? owner) || owner == null)
            {
                return;
            }

            ItemsControl ownerControl = owner;

            object currentItem = checkBox.DataContext;
            ModifierKeys modifiers = Keyboard.Modifiers;

            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                bool newState = !(checkBox.IsChecked ?? false);
                SetIsSelected(currentItem, newState);
                e.Handled = true;
                return;
            }

            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                List<object> items = GetItems(ownerControl);
                if (items.Count == 0)
                {
                    return;
                }

                object anchorItem = GetAnchorItem(ownerControl) ?? currentItem;
                int anchorIndex = items.IndexOf(anchorItem);
                int currentIndex = items.IndexOf(currentItem);
                if (anchorIndex < 0 || currentIndex < 0)
                {
                    SetAnchorItem(ownerControl, currentItem);
                    return;
                }

                bool newSelectionState = !(checkBox.IsChecked ?? false);
                int start = Math.Min(anchorIndex, currentIndex);
                int end = Math.Max(anchorIndex, currentIndex);

                for (int i = start; i <= end; i++)
                {
                    SetIsSelected(items[i], newSelectionState);
                }

                SetAnchorItem(ownerControl, currentItem);
                e.Handled = true;
                return;
            }

            SetAnchorItem(ownerControl, currentItem);
        }

        private static bool TryGetOwningItemsControl(DependencyObject start, out ItemsControl? owner)
        {
            owner = null;
            DependencyObject? current = start;

            while (current != null)
            {
                ItemsControl? parentItemsControl = ItemsControl.ItemsControlFromItemContainer(current);
                if (parentItemsControl != null)
                {
                    owner = parentItemsControl;
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static List<object> GetItems(ItemsControl owner)
        {
            var items = new List<object>(owner.Items.Count);
            foreach (object item in owner.Items)
            {
                items.Add(item);
            }

            return items;
        }

        private static void SetIsSelected(object item, bool value)
        {
            PropertyInfo? property = item.GetType().GetProperty("IsSelected", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(bool) || !property.CanWrite)
            {
                return;
            }

            property.SetValue(item, value);
        }
    }
}
