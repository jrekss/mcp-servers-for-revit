using System;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media.Imaging;



namespace revit_mcp_plugin.Core
{
    public class Application : IExternalApplication
    {
        public static PushButton McpToggleButton { get; private set; }

        public static void UpdateToggleButtonState(bool isRunning)
        {
            if (McpToggleButton == null) return;

            Action updateAction = () =>
            {
                if (isRunning)
                {
                    McpToggleButton.ItemText = "MCP Server";
                    McpToggleButton.ToolTip = "MCP Server is running on port 8080. Click to stop.";
                    McpToggleButton.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/mcp_switch_on.png", UriKind.RelativeOrAbsolute));
                    McpToggleButton.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/mcp_switch_on.png", UriKind.RelativeOrAbsolute));
                }
                else
                {
                    McpToggleButton.ItemText = "MCP Server";
                    McpToggleButton.ToolTip = "MCP Server is offline. Click to start.";
                    McpToggleButton.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/mcp_switch_off.png", UriKind.RelativeOrAbsolute));
                    McpToggleButton.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/mcp_switch_off.png", UriKind.RelativeOrAbsolute));
                }
            };

            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(updateAction);
            }
            else
            {
                updateAction();
            }
        }

        public Result OnStartup(UIControlledApplication application)
        {
            RibbonPanel mcpPanel = application.CreateRibbonPanel("Revit MCP Plugin");

            PushButtonData pushButtonData = new PushButtonData("ID_EXCMD_TOGGLE_REVIT_MCP", "MCP Server",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.MCPServiceConnection");
            pushButtonData.ToolTip = "MCP Server is offline. Click to start.";
            pushButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/mcp_switch_off.png", UriKind.RelativeOrAbsolute));
            pushButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/mcp_switch_off.png", UriKind.RelativeOrAbsolute));
            McpToggleButton = mcpPanel.AddItem(pushButtonData) as PushButton;

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (SocketService.Instance.IsRunning)
                {
                    SocketService.Instance.Stop();
                }
            }
            catch { }

            return Result.Succeeded;
        }
    }
}
