using System.Windows;
using System.Windows.Controls;

namespace revit_mcp_plugin.UI
{
    /// <summary>
    /// Settings.xaml interaction logic of
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private CommandSetSettingsPage commandSetPage;
        private bool isInitialized = false;

        public SettingsWindow()
        {
            InitializeComponent();

            // Initialize page
            commandSetPage = new CommandSetSettingsPage();

            // 加载默认页面
            ContentFrame.Navigate(commandSetPage);

            isInitialized = true;
        }

        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInitialized) return;

            if (NavListBox.SelectedItem == CommandSetItem)
            {
                ContentFrame.Navigate(commandSetPage);
            }
        }
    }
}
