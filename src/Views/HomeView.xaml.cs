using System.Windows;
using Autodesk.Revit.UI;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class HomeView : LecgWindow
    {
        public HomeView()
        {
            InitializeComponent();
        }

        private void SetTagAndClose(string tag)
        {
            Tag = tag;
            DialogResult = true;
            Close();
        }

        private void AlignMaster_Click(object sender, RoutedEventArgs e) => SetTagAndClose("AlignMaster");
        private void AssignMaterial_Click(object sender, RoutedEventArgs e) => SetTagAndClose("AssignMaterial");
        private void SexyRevit_Click(object sender, RoutedEventArgs e) => SetTagAndClose("SexyRevit");
        private void Purge_Click(object sender, RoutedEventArgs e) => SetTagAndClose("Purge");
        private void Offsets_Click(object sender, RoutedEventArgs e) => SetTagAndClose("Offsets");
        private void ResetSlabs_Click(object sender, RoutedEventArgs e) => SetTagAndClose("ResetSlabs");
        private void SimplifyPoints_Click(object sender, RoutedEventArgs e) => SetTagAndClose("SimplifyPoints");
        private void AlignEdges_Click(object sender, RoutedEventArgs e) => SetTagAndClose("AlignEdges");
        private void UpdateContours_Click(object sender, RoutedEventArgs e) => SetTagAndClose("UpdateContours");
        private void ChangeLevel_Click(object sender, RoutedEventArgs e) => SetTagAndClose("ChangeLevel");
        private void CleanSchemas_Click(object sender, RoutedEventArgs e) => SetTagAndClose("CleanSchemas");
        private void RenderMatch_Click(object sender, RoutedEventArgs e) => SetTagAndClose("RenderMatch");
        private void ConvertFamily_Click(object sender, RoutedEventArgs e) => SetTagAndClose("ConvertFamily");
        private void BatchRename_Click(object sender, RoutedEventArgs e) => SetTagAndClose("BatchRename");
    }
}