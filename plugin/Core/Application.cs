using System;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.ApplicationServices;



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

            // Subscribe to application initialized event to auto-start the server on startup
            application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;

            return Result.Succeeded;
        }

        private void OnApplicationInitialized(object sender, ApplicationInitializedEventArgs e)
        {
            try
            {
                var app = sender as Autodesk.Revit.ApplicationServices.Application;
                if (app != null)
                {
                    var uiApp = new UIApplication(app);
                    StartMcpServer(uiApp);
                }
            }
            catch (Exception)
            {
                UpdateToggleButtonState(false);
            }
        }

        private void StartMcpServer(UIApplication uiApp)
        {
            try
            {
                bool portWasOccupied = false;
                try
                {
                    using (var client = new System.Net.Sockets.TcpClient())
                    {
                        var result = client.BeginConnect("127.0.0.1", 8080, null, null);
                        bool connected = result.AsyncWaitHandle.WaitOne(150);
                        if (connected)
                        {
                            client.EndConnect(result);
                            portWasOccupied = true;
                        }
                    }
                }
                catch
                {
                    // Port is free or connection failed
                }

                if (portWasOccupied)
                {
                    // Port is occupied. Fail silently without blocking Revit startup.
                    UpdateToggleButtonState(false);
                    return;
                }

                SocketService service = SocketService.Instance;
                service.Initialize(uiApp);
                service.Start();

                if (service.IsRunning)
                {
                    UpdateToggleButtonState(true);
                }
                else
                {
                    UpdateToggleButtonState(false);
                }
            }
            catch (Exception)
            {
                UpdateToggleButtonState(false);
            }
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
