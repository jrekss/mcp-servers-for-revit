using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

/// <summary>
///     三维面
/// </summary>
public class JZFace
{
    /// <summary>
    ///     Constructor
    /// </summary>
    public JZFace()
    {
        InnerLoops = new List<List<JZLine>>();
        OuterLoop = new List<JZLine>();
    }

    /// <summary>
    ///     外环（List<List<JZLine>> 类型）
    /// </summary>
    [JsonProperty("outerLoop")]
    public List<JZLine> OuterLoop { get; set; }

    /// <summary>
    ///     内环（List<JZLine> type, representing one or more inner loops)
    /// </summary>
    [JsonProperty("innerLoops")]
    public List<List<JZLine>> InnerLoops { get; set; }
}