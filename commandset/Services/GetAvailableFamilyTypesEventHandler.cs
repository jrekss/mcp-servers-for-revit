using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class GetAvailableFamilyTypesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        // Execution result
        public List<FamilyTypeInfo> ResultFamilyTypes { get; private set; }

        // 状态同步对象
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        // Filter conditions
        public List<string> CategoryList { get; set; }
        public string FamilyNameFilter { get; set; }
        public int? Limit { get; set; }

        // Execution time, slightly shorter than the invocation timeout.
        public bool WaitForCompletion(int timeoutMilliseconds = 12500)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;

                // 可载入族
                var familySymbols = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>();
                // System family type (wall, floor, etc.)
                var systemTypes = new List<ElementType>();
                systemTypes.AddRange(new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<ElementType>());
                systemTypes.AddRange(new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<ElementType>());
                systemTypes.AddRange(new FilteredElementCollector(doc).OfClass(typeof(RoofType)).Cast<ElementType>());
                systemTypes.AddRange(new FilteredElementCollector(doc).OfClass(typeof(CeilingType)).Cast<ElementType>());
                systemTypes.AddRange(new FilteredElementCollector(doc).OfClass(typeof(CurtainSystemType)).Cast<ElementType>());
                // 合并结果
                var allElements = familySymbols
                    .Cast<ElementType>()
                    .Concat(systemTypes)
                    .ToList();

                IEnumerable<ElementType> filteredElements = allElements;

                // Category filtering
                if (CategoryList != null && CategoryList.Any())
                {
                    var validCategoryIds = new List<int>();
                    foreach (var categoryName in CategoryList)
                    {
                        if (Enum.TryParse(categoryName, out BuiltInCategory bic))
                        {
                            validCategoryIds.Add((int)bic);
                        }
                    }

                    if (validCategoryIds.Any())
                    {
                        filteredElements = filteredElements.Where(et =>
                        {
#if REVIT2024_OR_GREATER
                            var categoryId = et.Category?.Id.Value;
#else
                            var categoryId = et.Category?.Id.IntegerValue;
#endif
                            return categoryId != null && validCategoryIds.Contains((int)categoryId.Value);
                        });
                    }
                }

                // Fuzzy name matching (matching both family name and type name)
                if (!string.IsNullOrEmpty(FamilyNameFilter))
                {
                    filteredElements = filteredElements.Where(et =>
                    {
                        string familyName = et is FamilySymbol fs ? fs.FamilyName : et.get_Parameter(
                            BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? "";

                        return familyName?.IndexOf(FamilyNameFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                               et.Name.IndexOf(FamilyNameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                    });
                }

                // 限制返回数量
                if (Limit.HasValue && Limit.Value > 0)
                {
                    filteredElements = filteredElements.Take(Limit.Value);
                }

                // 转换为FamilyTypeInfo列表
                ResultFamilyTypes = filteredElements.Select(et =>
                {
                    string familyName;
                    if (et is FamilySymbol fs)
                    {
                        familyName = fs.FamilyName;
                    }
                    else
                    {
                        Parameter param = et.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM);
                        familyName = param?.AsString() ?? et.GetType().Name.Replace("Type", "");
                    }
                    return new FamilyTypeInfo
                    {
#if REVIT2024_OR_GREATER
                        FamilyTypeId = et.Id.Value,
#else
                        FamilyTypeId = et.Id.IntegerValue,
#endif
                        UniqueId = et.UniqueId,
                        FamilyName = familyName,
                        TypeName = et.Name,
                        Category = et.Category?.Name
                    };
                }).ToList();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "Get family type失败: " + ex.Message);
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "GetAvailableFamilyTypes";
        }
    }
}
