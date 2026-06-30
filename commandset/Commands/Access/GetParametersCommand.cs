using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Access
{
    public class GetParametersCommand : ExternalEventCommandBase
    {
        private GetParametersEventHandler _handler => (GetParametersEventHandler)Handler;

        public override string CommandName => "get_parameters_from_elementid";

        public GetParametersCommand(UIApplication uiApp)
            : base(new GetParametersEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long elementId = parameters?["elementId"]?.Value<long>() ?? throw new ArgumentException("elementId is required");

                _handler.ElementId = elementId;

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.ResultParameters;
                }
                else
                {
                    throw new TimeoutException("Get parameters timeout");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Get parameters failed: {ex.Message}");
            }
        }
    }
}
