using System.Windows;
using System.Windows.Input;

namespace revit_mcp_plugin.UI
{
    /// <summary>
    /// Interaction logic for PortConflictWindow.xaml
    /// </summary>
    public partial class PortConflictWindow : Window
    {
        public bool UserSelectedForceSwitch { get; private set; } = false;

        public PortConflictWindow()
        {
            InitializeComponent();
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void ForceSwitchButton_Click(object sender, RoutedEventArgs e)
        {
            UserSelectedForceSwitch = true;
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            UserSelectedForceSwitch = false;
            this.DialogResult = false;
            this.Close();
        }
    }
}
