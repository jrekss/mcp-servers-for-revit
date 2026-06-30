using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Commands.Access
{
    public class GetLocationCommand : ExternalEventCommandBase
    {
        private GetLocationEventHandler _handler => (GetLocationEventHandler)Handler;

        public override string CommandName => "get_location_for_element_ids";

        public GetLocationCommand(UIApplication uiApp)
            : base(new GetLocationEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                var elementIds = parameters?["elementIds"]?.ToObject<List<long>>() ?? throw new ArgumentException("elementIds is required");

                _handler.ElementIds = elementIds;

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.ResultLocations;
                }
                else
                {
                    throw new TimeoutException("Get location timeout");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Get location failed: {ex.Message}");
            }
        }
    }
}
