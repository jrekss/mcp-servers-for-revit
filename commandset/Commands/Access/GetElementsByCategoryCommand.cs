using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Commands.Access
{
    public class GetElementsByCategoryCommand : ExternalEventCommandBase
    {
        private GetElementsByCategoryEventHandler _handler => (GetElementsByCategoryEventHandler)Handler;

        public override string CommandName => "get_elements_by_category";

        public GetElementsByCategoryCommand(UIApplication uiApp)
            : base(new GetElementsByCategoryEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                var categoryNamesOrIds = parameters?["categoryNamesOrIds"]?.ToObject<List<string>>() ?? throw new ArgumentException("categoryNamesOrIds is required");

                _handler.CategoryNamesOrIds = categoryNamesOrIds;

                if (RaiseAndWaitForCompletion(30000))
                {
                    return _handler.ResultElements;
                }
                else
                {
                    throw new TimeoutException("Get elements by category timeout");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Get elements by category failed: {ex.Message}");
            }
        }
    }
}
