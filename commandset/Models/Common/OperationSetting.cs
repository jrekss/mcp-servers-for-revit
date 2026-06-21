using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// Define the types of operations that can be performed on elements.
    /// </summary>
    public enum ElementOperationType
    {
        /// <summary>
        /// Select element
        /// </summary>
        Select,

        /// <summary>
        /// 选择框
        /// </summary>
        SelectionBox,

        /// <summary>
        /// Set element color and fill
        /// </summary>
        SetColor,

        /// <summary>
        /// 设置图元透明度
        /// </summary>
        SetTransparency,

        /// <summary>
        /// 删除图元
        /// </summary>
        Delete,

        /// <summary>
        /// Hide elements
        /// </summary>
        Hide,

        /// <summary>
        /// Temporarily hide elements
        /// </summary>
        TempHide,

        /// <summary>
        /// Isolate elements (display individually)
        /// </summary>
        Isolate,

        /// <summary>
        /// Unhide elements
        /// </summary>
        Unhide,

        /// <summary>
        /// Reset isolation (show all elements).
        /// </summary>
        ResetIsolate,
    }


    /// <summary>
    /// Operate on element的设置
    /// </summary>
    public class OperationSetting
    {
        /// <summary>
        /// 需要操作的元素ID列表
        /// </summary>
        [JsonProperty("elementIds")]
        public List<int> ElementIds = new List<int>();

        /// <summary>
        /// Required action to execute, storedElementOperationType枚举的string类型的值
        /// </summary>
        [JsonProperty("action")]
        public string Action { get; set; } = "Select";

        /// <summary>
        /// 透明度值(0-100), the larger the value, the higher the transparency
        /// </summary>
        [JsonProperty("transparencyValue")]
        public int TransparencyValue { get; set; } = 50;

        /// <summary>
        /// 设置图元颜色（RGBformat), default is red
        /// </summary>
        [JsonProperty("colorValue")]
        public int[] ColorValue { get; set; } = new int[] { 255, 0, 0 }; // Default red
    }
}
