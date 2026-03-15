using System.Windows;

namespace LECG.Views.Base
{
    public partial class LecgDialogWindow : LecgWindow
    {
        public LecgDialogResult Result { get; private set; } = LecgDialogResult.Cancel;
        public int SelectedOptionIndex { get; private set; } = -1;

        public LecgDialogWindow()
        {
            InitializeComponent();
        }

        public void SetTitle(string title)
        {
            TitleText.Text = title;
            Title = title;
        }

        public void SetMessage(string message)
        {
            MessageText.Text = message;
        }

        public void SetDetail(string? detail)
        {
            if (!string.IsNullOrEmpty(detail))
            {
                DetailText.Text = detail;
                DetailText.Visibility = Visibility.Visible;
            }
        }

        public void ShowCancelButton()
        {
            CancelButton.Visibility = Visibility.Visible;
        }

        public void SetOptions(string[] options)
        {
            OptionsPanel.Visibility = Visibility.Visible;

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var btn = new System.Windows.Controls.Button
                {
                    Content = options[i],
                    Margin = new Thickness(0, 0, 0, 6),
                    Style = (Style)FindResource("OptionButtonStyle")
                };
                btn.Click += (_, __) =>
                {
                    SelectedOptionIndex = index;
                    Result = LecgDialogResult.Ok;
                    Close();
                };
                OptionsPanel.Items.Add(btn);
            }

            // Hide OK button when showing options (options act as selection)
            OkButton.Visibility = Visibility.Collapsed;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Result = LecgDialogResult.Ok;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = LecgDialogResult.Cancel;
            Close();
        }
    }
}
