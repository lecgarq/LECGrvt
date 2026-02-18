using LECG.ViewModels;

namespace LECG.Views
{
    public partial class PurgeView : Base.LecgWindow
    {
        public PurgeView(PurgeViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);

            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = () =>
            {
                DialogResult = true;
                Close();
            };
        }

        private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Run_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            (DataContext as PurgeViewModel)?.ApplyCommand.Execute(null);
        }
    }
}
