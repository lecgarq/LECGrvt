using System.Windows;
using System.Windows.Controls;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class HomeView : LecgWindow
    {
        public HomeView()
        {
            InitializeComponent();
        }

        // Every tool card carries its HomeCommand key in Tag; the dashboard only reports the choice.
        private void Tool_Click(object sender, RoutedEventArgs e)
        {
            Tag = (sender as Button)?.Tag as string ?? "";
            DialogResult = true;
            Close();
        }
    }
}
