namespace LECG.Views.Base
{
    /// <summary>
    /// Static API for showing themed LECG dialogs.
    /// Replaces all TaskDialog and MessageBox usage.
    /// </summary>
    public static class LecgDialog
    {
        /// <summary>
        /// Show an informational message with an OK button.
        /// </summary>
        public static void Show(string title, string message)
        {
            var dlg = new LecgDialogWindow();
            dlg.SetTitle(title);
            dlg.SetMessage(message);
            dlg.ShowDialog();
        }

        /// <summary>
        /// Show a confirmation dialog with OK/Cancel buttons.
        /// Returns true if the user clicked OK.
        /// </summary>
        public static bool Confirm(string title, string message, string? detail = null)
        {
            var dlg = new LecgDialogWindow();
            dlg.SetTitle(title);
            dlg.SetMessage(message);
            dlg.SetDetail(detail);
            dlg.ShowCancelButton();
            dlg.ShowDialog();
            return dlg.Result == LecgDialogResult.Ok;
        }

        /// <summary>
        /// Show a dialog with selectable command-link options.
        /// Returns the zero-based index of the selected option, or -1 if cancelled.
        /// </summary>
        public static int ShowOptions(string title, string message, params string[] options)
        {
            var dlg = new LecgDialogWindow();
            dlg.SetTitle(title);
            dlg.SetMessage(message);
            dlg.ShowCancelButton();
            dlg.SetOptions(options);
            dlg.ShowDialog();
            return dlg.SelectedOptionIndex;
        }
    }

    public enum LecgDialogButtons
    {
        Ok,
        OkCancel
    }

    public enum LecgDialogResult
    {
        Ok,
        Cancel
    }
}
