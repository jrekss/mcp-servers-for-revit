using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class SetParameterValueCommand : ExternalEventCommandBase
    {
        private SetParameterValueEventHandler _handler => (SetParameterValueEventHandler)Handler;

        public override string CommandName => "set_parameter_value_for_elements";

        public SetParameterValueCommand(UIApplication uiApp)
            : base(new SetParameterValueEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                var elementIds = parameters?["elementIds"]?.ToObject<List<long>>() ?? throw new ArgumentException("elementIds is required");
                string parameterNameOrId = parameters?["parameterNameOrId"]?.Value<string>() ?? throw new ArgumentException("parameterNameOrId is required");
                string value = parameters?["value"]?.Value<string>() ?? throw new ArgumentException("value is required");

                _handler.ElementIds = elementIds;
                _handler.ParameterNameOrId = parameterNameOrId;
                _handler.Value = value;

                if (RaiseAndWaitForCompletion(30000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Set parameter value timeout");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Set parameter value failed: {ex.Message}");
            }
        }
    }
}
