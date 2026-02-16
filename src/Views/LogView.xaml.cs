using System.Windows;
using LECG.ViewModels;
using LECG.Services.Logging;

namespace LECG.Views
{
    public partial class LogView : Base.LecgWindow
    {
        public LogView(LogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        // Keep a parameterless constructor for XAML designer if needed, or default usage
        public LogView()
        {
            InitializeComponent();
            DataContext = new LogViewModel(Logger.Instance);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
