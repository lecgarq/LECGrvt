using System.Windows;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class AlignDashboardView : LecgWindow
    {
        public AlignDashboardView()
        {
            InitializeComponent();
        }

        private void SetTagAndClose(string tag)
        {
            Tag = tag;
            DialogResult = true;
            Close();
        }

        private void AlignLeft_Click(object sender, RoutedEventArgs e) => SetTagAndClose("AlignLeft");
        private void AlignCenter_Click(object sender, RoutedEventArgs e) => SetTagAndClose("AlignCenter");
        private void AlignRight_Click(object sender, RoutedEventArgs e) => SetTagAndClose("AlignRight");
        private void AlignTop_Click(object sender, RoutedEventArgs e) => SetTagAndClose("AlignTop");
        private void AlignMiddle_Click(object sender, RoutedEventArgs e) => SetTagAndClose("AlignMiddle");
        private void AlignBottom_Click(object sender, RoutedEventArgs e) => SetTagAndClose("AlignBottom");
        private void DistributeH_Click(object sender, RoutedEventArgs e) => SetTagAndClose("DistributeH");
        private void DistributeV_Click(object sender, RoutedEventArgs e) => SetTagAndClose("DistributeV");
    }
}
