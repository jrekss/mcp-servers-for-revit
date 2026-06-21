using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace revit_mcp_plugin.Core
{
    [Transaction(TransactionMode.Manual)]
    public class MCPServiceConnection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // 获取socket服务
                // Obtain socket service.
                SocketService service = SocketService.Instance;

                if (service.IsRunning)
                {
                    service.Stop();
                    Application.UpdateToggleButtonState(false);
                }
                else
                {
                    bool forceSwitchSelected = false;
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

                                bool force = false;
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var window = new revit_mcp_plugin.UI.PortConflictWindow();
                                    var helper = new System.Windows.Interop.WindowInteropHelper(window);
                                    helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                                    window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
                                    if (window.ShowDialog() == true)
                                    {
                                        force = window.UserSelectedForceSwitch;
                                    }
                                });

                                if (force)
                                {
                                    forceSwitchSelected = true;
                                    using (var stream = client.GetStream())
                                    {
                                        string req = "{\"jsonrpc\":\"2.0\",\"method\":\"force_release_port\",\"id\":\"force\"}";
                                        byte[] reqBytes = System.Text.Encoding.UTF8.GetBytes(req);
                                        stream.Write(reqBytes, 0, reqBytes.Length);

                                        byte[] respBuffer = new byte[1024];
                                        stream.ReadTimeout = 200;
                                        try
                                        {
                                            stream.Read(respBuffer, 0, respBuffer.Length);
                                        }
                                        catch { }
                                    }
                                    System.Threading.Thread.Sleep(250);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Port is free or connection failed
                    }

                    if (portWasOccupied && !forceSwitchSelected)
                    {
                        Application.UpdateToggleButtonState(false);
                        return Result.Succeeded;
                    }

                    service.Initialize(commandData.Application);
                    service.Start();
                    
                    if (service.IsRunning)
                    {
                        Application.UpdateToggleButtonState(true);
                    }
                    else
                    {
                        Application.UpdateToggleButtonState(false);
                        TaskDialog.Show("revitMCP", "Failed to start server.\nPort 8080 might be in use by another application.");
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Application.UpdateToggleButtonState(false);
                return Result.Failed;
            }
        }
    }
}
