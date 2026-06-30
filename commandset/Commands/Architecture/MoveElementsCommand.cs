using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class MoveElementsCommand : ExternalEventCommandBase
    {
        private MoveElementsEventHandler _handler => (MoveElementsEventHandler)Handler;

        public override string CommandName => "set_movement_for_elements";

        public MoveElementsCommand(UIApplication uiApp)
            : base(new MoveElementsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                var elementIds = parameters?["elementIds"]?.ToObject<List<long>>() ?? throw new ArgumentException("elementIds is required");
                double dX = parameters?["dX"]?.Value<double>() ?? 0.0;
                double dY = parameters?["dY"]?.Value<double>() ?? 0.0;
                double dZ = parameters?["dZ"]?.Value<double>() ?? 0.0;

                _handler.ElementIds = elementIds;
                _handler.dX = dX;
                _handler.dY = dY;
                _handler.dZ = dZ;

                if (RaiseAndWaitForCompletion(30000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Move elements timeout");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Move elements failed: {ex.Message}");
            }
        }
    }
}
