using LECG.ViewModels;

namespace LECG.Views
{
    public partial class SexyRevitView : Base.LecgWindow
    {
        public SexyRevitView(SexyRevitViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = () =>
            {
                DialogResult = true;
                Close();
            };
        }

        // Keep event handlers for buttons if strictly necessary or map them to commands in XAML
        // For now, simpler to map clicks to commands in code-behind if XAML isn't changed yet
        // Bur for pure MVVM, we should use Command binding.
        // Let's assume we update XAML to bind buttons, BUT since I can't easily change all XAML bindings at once safely
        // without risk of breaking, I will rig the buttons here.

        private void Apply_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            (DataContext as SexyRevitViewModel)?.ApplyCommand.Execute(null);
        }

        private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            (DataContext as SexyRevitViewModel)?.CancelCommand.Execute(null);
        }
    }
}
