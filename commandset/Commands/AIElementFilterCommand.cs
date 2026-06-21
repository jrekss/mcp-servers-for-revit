using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Commands
{
    public class AIElementFilterCommand : ExternalEventCommandBase
    {
        private AIElementFilterEventHandler _handler => (AIElementFilterEventHandler)Handler;

        /// <summary>
        /// 命令名称
        /// </summary>
        public override string CommandName => "ai_element_filter";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public AIElementFilterCommand(UIApplication uiApp)
            : base(new AIElementFilterEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                FilterSetting data = new FilterSetting();
                // Parse parameters
                data = parameters["data"].ToObject<FilterSetting>();
                if (data == null)
                    throw new ArgumentNullException(nameof(data), "AI传入数据为空");

                // 设置AIFilter parameter
                _handler.SetParameters(data);

                // Trigger external event and wait for completion
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Timeout getting element information");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取元素信息失败: {ex.Message}");
            }
        }
    }
}
