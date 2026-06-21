using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services;

namespace RevitMCPCommandSet.Commands
{
    public class CreateSurfaceElementCommand : ExternalEventCommandBase
    {
        private CreateSurfaceElementEventHandler _handler => (CreateSurfaceElementEventHandler)Handler;

        /// <summary>
        /// 命令名称
        /// </summary>
        public override string CommandName => "create_surface_based_element";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public CreateSurfaceElementCommand(UIApplication uiApp)
            : base(new CreateSurfaceElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                List<SurfaceElement> data = new List<SurfaceElement>();
                // Parse parameters
                data = parameters["data"].ToObject<List<SurfaceElement>>();
                if (data == null)
                    throw new ArgumentNullException(nameof(data), "AI传入数据为空");

                // Set face-based component parameters
                _handler.SetParameters(data);

                // Trigger external event and wait for completion
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Timeout creating face-based component");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create face-based component: {ex.Message}");
            }
        }
    }
}
