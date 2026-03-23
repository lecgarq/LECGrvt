using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Core
{
    public class SelectionCoordinator : ISelectionCoordinator
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        public IList<Reference> PickObjects(Window owner, UIDocument uiDoc, ObjectType objectType, ISelectionFilter? filter, string prompt, bool restoreModalState = true)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

            IntPtr revitHandle = GetRevitHandle(uiDoc, owner);
            SafeHide(owner, revitHandle);
            try
            {
                return filter == null
                    ? uiDoc.Selection.PickObjects(objectType, prompt)
                    : uiDoc.Selection.PickObjects(objectType, filter, prompt);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return new List<Reference>();
            }
            finally
            {
                SafeShow(owner, revitHandle, restoreModalState);
            }
        }

        public Reference? PickObject(Window owner, UIDocument uiDoc, ObjectType objectType, ISelectionFilter? filter, string prompt, bool restoreModalState = true)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

            IntPtr revitHandle = GetRevitHandle(uiDoc, owner);
            SafeHide(owner, revitHandle);
            try
            {
                return filter == null
                    ? uiDoc.Selection.PickObject(objectType, prompt)
                    : uiDoc.Selection.PickObject(objectType, filter, prompt);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
            finally
            {
                SafeShow(owner, revitHandle, restoreModalState);
            }
        }

        /// <summary>
        /// Get the real Revit main window handle from the API, falling back to
        /// the WPF owner handle. Process.MainWindowHandle can return the wrong
        /// handle (splash screen, etc.), so the API handle is authoritative.
        /// </summary>
        private static IntPtr GetRevitHandle(UIDocument uiDoc, Window owner)
        {
            try
            {
                IntPtr apiHandle = uiDoc.Application.MainWindowHandle;
                if (apiHandle != IntPtr.Zero)
                {
                    return apiHandle;
                }
            }
            catch
            {
                // Fall through to WPF owner
            }

            try
            {
                var helper = new WindowInteropHelper(owner);
                return helper.Owner;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static void SafeHide(Window owner, IntPtr revitHandle)
        {
            try
            {
                var helper = new WindowInteropHelper(owner);
                if (helper.Handle != IntPtr.Zero)
                {
                    ShowWindow(helper.Handle, SW_HIDE);
                }
            }
            catch
            {
                try { owner.Visibility = System.Windows.Visibility.Collapsed; } catch { }
            }

            // Re-enable Revit so PickObjects can receive input.
            // ShowDialog() disables the owner via Win32 EnableWindow(owner, FALSE),
            // but our SW_HIDE bypasses WPF so it never re-enables automatically.
            if (revitHandle != IntPtr.Zero)
            {
                EnableWindow(revitHandle, true);
                SetForegroundWindow(revitHandle);
            }
        }

        private static void SafeShow(Window owner, IntPtr revitHandle, bool restoreModalState)
        {
            // Only modal WPF dialogs need Revit re-disabled before the owner is restored.
            if (restoreModalState && revitHandle != IntPtr.Zero)
            {
                EnableWindow(revitHandle, false);
            }

            try
            {
                var helper = new WindowInteropHelper(owner);
                if (helper.Handle != IntPtr.Zero)
                {
                    ShowWindow(helper.Handle, SW_SHOW);
                    SetForegroundWindow(helper.Handle);
                }
                owner.Activate();
            }
            catch
            {
                try { owner.Visibility = System.Windows.Visibility.Visible; owner.Activate(); } catch { }
            }
        }
    }
}
