using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace RevitMCPCommandSet.Services
{
    public class AIElementFilterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;
        /// <summary>
        /// Event wait object
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        /// <summary>
        /// Create data (incoming data)
        /// </summary>
        public FilterSetting FilterSetting { get; private set; }
        /// <summary>
        /// Execution result (outgoing data)
        /// </summary>
        public AIResult<List<object>> Result { get; private set; }

        /// <summary>
        /// 设置创建的参数
        /// </summary>
        public void SetParameters(FilterSetting data)
        {
            FilterSetting = data;
            _resetEvent.Reset();
        }
        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementInfoList = new List<object>();
                // Check if filter settings are valid
                if (!FilterSetting.Validate(out string errorMessage))
                    throw new Exception(errorMessage);
                // Get elements matching specified conditionsId
                var elementList = GetFilteredElements(doc, FilterSetting);
                if (elementList == null || !elementList.Any())
                    throw new Exception("The specified element was not found in the project. Please check if the filter settings are correct.");
                // Maximum filter limit
                string message = "";
                if (FilterSetting.MaxElements > 0)
                {
                    if (elementList.Count > FilterSetting.MaxElements)
                    {
                        elementList = elementList.Take(FilterSetting.MaxElements).ToList();
                        message = $". In addition, there are a total of matching the filter conditions {elementList.Count} elements, only displaying the first {FilterSetting.MaxElements} 个";
                    }
                }

                // Get specifiedIdElement information
                elementInfoList = GetElementFullInfo(doc, elementList);

                Result = new AIResult<List<object>>
                {
                    Success = true,
                    Message = $"成功获取{elementInfoList.Count}elements of information, detailed information stored inResponse属性中"+ message,
                    Response = elementInfoList,
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<object>>
                {
                    Success = false,
                    Message = $"Error getting element information: {ex.Message}",
                };
            }
            finally
            {
                _resetEvent.Set(); // Notify waiting thread that operation is completed
            }
        }

        /// <summary>
        /// 等待创建完成
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout (milliseconds)</param>
        /// <returns>Whether operation completed before timeout</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// IExternalEventHandler.GetName 实现
        /// </summary>
        public string GetName()
        {
            return "获取元素信息";
        }

        /// <summary>
        /// Get based on filter settingsRevitElements in the document matching the conditions, supporting multi-condition combination filtering.
        /// </summary>
        /// <param name="doc">Revit文档</param>
        /// <param name="settings">Filter settings</param>
        /// <returns>Collection of elements matching all filter conditions.</returns>
        public static IList<Element> GetFilteredElements(Document doc, FilterSetting settings)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            // 验证Filter settings
            if (!settings.Validate(out string errorMessage))
            {
                System.Diagnostics.Trace.WriteLine($"Filter settings无效: {errorMessage}");
                return new List<Element>();
            }
            // Record application of filter conditions
            List<string> appliedFilters = new List<string>();
            List<Element> result = new List<Element>();
            // If both types and instances are included, they need to be filtered separately and then the results merged.
            if (settings.IncludeTypes && settings.IncludeInstances)
            {
                // 收集类型元素
                result.AddRange(GetElementsByKind(doc, settings, true, appliedFilters));

                // Collect instance elements
                result.AddRange(GetElementsByKind(doc, settings, false, appliedFilters));
            }
            else if (settings.IncludeInstances)
            {
                // 仅Collect instance elements
                result = GetElementsByKind(doc, settings, false, appliedFilters);
            }
            else if (settings.IncludeTypes)
            {
                // 仅收集类型元素
                result = GetElementsByKind(doc, settings, true, appliedFilters);
            }

            // Output applied filter information
            if (appliedFilters.Count > 0)
            {
                System.Diagnostics.Trace.WriteLine($"已应用 {appliedFilters.Count}  filter conditions: {string.Join(", ", appliedFilters)}");
                System.Diagnostics.Trace.WriteLine($"最终筛选结果: 共找到 {result.Count} 个元素");
            }
            return result;

        }

        /// <summary>
        /// 根据元素种类(Type or instance)Get elements meeting filter conditions
        /// </summary>
        private static List<Element> GetElementsByKind(Document doc, FilterSetting settings, bool isElementType, List<string> appliedFilters)
        {
            // Create basicFilteredElementCollector
            FilteredElementCollector collector;
            // Check if it is necessary to filter elements visible in the current view. (仅适用于实例元素)
            if (!isElementType && settings.FilterVisibleInCurrentView && doc.ActiveView != null)
            {
                collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                appliedFilters.Add("Elements visible in current view");
            }
            else
            {
                collector = new FilteredElementCollector(doc);
            }
            // Filter by element category
            if (isElementType)
            {
                collector = collector.WhereElementIsElementType();
                appliedFilters.Add("Element types only");
            }
            else
            {
                collector = collector.WhereElementIsNotElementType();
                appliedFilters.Add("Element instances only");
            }
            // Create filter列表
            List<ElementFilter> filters = new List<ElementFilter>();
            // 1. Category filter
            if (!string.IsNullOrWhiteSpace(settings.FilterCategory))
            {
                BuiltInCategory category;
                if (!Enum.TryParse(settings.FilterCategory, true, out category))
                {
                    throw new ArgumentException($"无法将 '{settings.FilterCategory}' 转换为有效的Revit类别。");
                }
                ElementCategoryFilter categoryFilter = new ElementCategoryFilter(category);
                filters.Add(categoryFilter);
                appliedFilters.Add($"类别：{settings.FilterCategory}");
            }
            // 2. 元素类型过滤器
            if (!string.IsNullOrWhiteSpace(settings.FilterElementType))
            {

                Type elementType = null;
                // Try to parse various possible forms of the type name.
                string[] possibleTypeNames = new string[]
                {
                    settings.FilterElementType,                                    // 原始输入
                    $"Autodesk.Revit.DB.{settings.FilterElementType}, RevitAPI",  // Revit API命名空间
                    $"{settings.FilterElementType}, RevitAPI"                      // 完整限定带程序集
                };
                foreach (string typeName in possibleTypeNames)
                {
                    elementType = Type.GetType(typeName);
                    if (elementType != null)
                        break;
                }
                if (elementType != null)
                {
                    ElementClassFilter classFilter = new ElementClassFilter(elementType);
                    filters.Add(classFilter);
                    appliedFilters.Add($"Element type: {elementType.Name}");
                }
                else
                {
                    throw new Exception($"Warning: Unable to find type '{settings.FilterElementType}'");
                }
            }
            // 3. Family symbol filter (仅适用于元素实例)
            if (!isElementType && settings.FilterFamilySymbolId > 0)
            {
                ElementId symbolId = new ElementId(settings.FilterFamilySymbolId);
                // Check if the element exists and is a family type.
                Element symbolElement = doc.GetElement(symbolId);
                if (symbolElement != null && symbolElement is FamilySymbol)
                {
                    FamilyInstanceFilter familyFilter = new FamilyInstanceFilter(doc, symbolId);
                    filters.Add(familyFilter);
                    // Add more detailed family information logs
                    FamilySymbol symbol = symbolElement as FamilySymbol;
                    string familyName = symbol.Family?.Name ?? "未知族";
                    string symbolName = symbol.Name ?? "Unknown type";
                    appliedFilters.Add($"Family type: {familyName} - {symbolName} (ID: {settings.FilterFamilySymbolId})");
                }
                else
                {
                    string elementType = symbolElement != null ? symbolElement.GetType().Name : "不存在";
                    System.Diagnostics.Trace.WriteLine($"警告：ID为 {settings.FilterFamilySymbolId} 的元素{(symbolElement == null ? "不存在" : "is not validFamilySymbol")} (Actual type: {elementType})");
                }
            }
            // 4. 空间范围过滤器
            if (settings.BoundingBoxMin != null && settings.BoundingBoxMax != null)
            {
                // 转换为Revit的XYZ坐标 (毫米转Internal units)
                XYZ minXYZ = JZPoint.ToXYZ(settings.BoundingBoxMin);
                XYZ maxXYZ = JZPoint.ToXYZ(settings.BoundingBoxMax);
                // 创建空间范围Outline对象
                Outline outline = new Outline(minXYZ, maxXYZ);
                // 创建相交过滤器
                BoundingBoxIntersectsFilter boundingBoxFilter = new BoundingBoxIntersectsFilter(outline);
                filters.Add(boundingBoxFilter);
                appliedFilters.Add($"空间范围过滤：Min({settings.BoundingBoxMin.X:F2}, {settings.BoundingBoxMin.Y:F2}, {settings.BoundingBoxMin.Z:F2}), " +
                                  $"Max({settings.BoundingBoxMax.X:F2}, {settings.BoundingBoxMax.Y:F2}, {settings.BoundingBoxMax.Z:F2}) mm");
            }
            // 应用Combined filter
            if (filters.Count > 0)
            {
                ElementFilter combinedFilter = filters.Count == 1
                    ? filters[0]
                    : new LogicalAndFilter(filters);
                collector = collector.WherePasses(combinedFilter);
                if (filters.Count > 1)
                {
                    System.Diagnostics.Trace.WriteLine($"应用了{filters.Count}combination filter of filter conditions (逻辑AND关系)");
                }
            }
            return collector.ToElements().ToList();
        }

        /// <summary>
        /// 获取模型元素信息
        /// </summary>
        public static List<object> GetElementFullInfo(Document doc, IList<Element> elementCollector)
        {
            List<object> infoList = new List<object>();

            // 获取并处理元素
            foreach (var element in elementCollector)
            {
                // Determine if it is a solid model element
                // Get element instance information
                if (element?.Category?.HasMaterialQuantities ?? false)
                {
                    var info = CreateElementFullInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 获取元素类型信息
                else if (element is ElementType elementType)
                {
                    var info = CreateTypeFullInfo(doc, elementType);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 3. 空间定位元素 (高频)
                else if (element is Level || element is Grid)
                {
                    var info = CreatePositioningElementInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 4. 空间元素 (中高频)
                else if (element is SpatialElement) // Room, Area等
                {
                    var info = CreateSpatialElementInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 5. View element (高频)
                else if (element is View)
                {
                    var info = CreateViewInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 6. Annotation element (中频)
                else if (element is TextNote || element is Dimension ||
                         element is IndependentTag || element is AnnotationSymbol ||
                         element is SpotDimension)
                {
                    var info = CreateAnnotationInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 7. 处理组和链接
                else if (element is Group || element is RevitLinkInstance)
                {
                    var info = CreateGroupOrLinkInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 8. 获取元素基本信息(Fallback handling)
                else
                {
                    var info = CreateElementBasicInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
            }

            return infoList;
        }

        /// <summary>
        /// Create complete information for a single elementElementInfo对象
        /// </summary>
        public static ElementInstanceInfo CreateElementFullInfo(Document doc, Element element)
        {
            try
            {
                if (element?.Category == null)
                    return null;

                ElementInstanceInfo elementInfo = new ElementInstanceInfo();        //Create a custom class storing complete element information.
                // ID
                elementInfo.Id = element.Id.GetIntValue();
                // UniqueId
                elementInfo.UniqueId = element.UniqueId;
                // Type name
                elementInfo.Name = element.Name;
                // 族名称
                elementInfo.FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString();
                // 类别
                elementInfo.Category = element.Category.Name;
                // 内置类别
                elementInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue());
                // 类型Id
                elementInfo.TypeId = element.GetTypeId().GetIntValue();
                //Owner roomId  
                if (element is FamilyInstance instance)
                    elementInfo.RoomId = instance.Room?.Id.GetIntValue() ?? -1;
                // 标高
                elementInfo.Level = GetElementLevel(doc, element);
                // Maximum bounding box
                BoundingBoxInfo boundingBoxInfo = new BoundingBoxInfo();
                elementInfo.BoundingBox = GetBoundingBoxInfo(element);
                // 参数
                //elementInfo.Parameters = GetDimensionParameters(element);
                ParameterInfo thicknessParam = GetThicknessInfo(element);      //厚度参数
                if (thicknessParam != null)
                {
                    elementInfo.Parameters.Add(thicknessParam);
                }
                ParameterInfo heightParam = GetBoundingBoxHeight(elementInfo.BoundingBox);      //Height parameter
                if (heightParam != null)
                {
                    elementInfo.Parameters.Add(heightParam);
                }

                return elementInfo;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create complete information for a single typeTypeFullInfo对象
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="elementType"></param>
        /// <returns></returns>
        public static ElementTypeInfo CreateTypeFullInfo(Document doc, ElementType elementType)
        {
            ElementTypeInfo typeInfo = new ElementTypeInfo();
            // Id
            typeInfo.Id = elementType.Id.GetIntValue();
            // UniqueId
            typeInfo.UniqueId = elementType.UniqueId;
            // Type name
            typeInfo.Name = elementType.Name;
            // 族名称
            typeInfo.FamilyName = elementType.FamilyName;
            // 类别
            typeInfo.Category = elementType.Category.Name;
            // 内置类别
            typeInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), elementType.Category.Id.GetIntValue());
            // Parameter dictionary
            typeInfo.Parameters = GetDimensionParameters(elementType);
            ParameterInfo thicknessParam = GetThicknessInfo(elementType);      //厚度参数
            if (thicknessParam != null)
            {
                typeInfo.Parameters.Add(thicknessParam);
            }
            return typeInfo;
        }

        /// <summary>
        /// Create information for spatial positioning elements
        /// </summary>
        public static PositioningElementInfo CreatePositioningElementInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                PositioningElementInfo info = new PositioningElementInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 处理标高
                if (element is Level level)
                {
                    // 转换为mm
                    info.Elevation = level.Elevation * 304.8;
                }
                // Handle grids
                else if (element is Grid grid)
                {
                    Curve curve = grid.Curve;
                    if (curve != null)
                    {
                        XYZ start = curve.GetEndPoint(0);
                        XYZ end = curve.GetEndPoint(1);
                        // 创建JZLine(converted tomm）
                        info.GridLine = new JZLine(
                            start.X * 304.8, start.Y * 304.8, start.Z * 304.8,
                            end.X * 304.8, end.Y * 304.8, end.Z * 304.8);
                    }
                }

                // Get level信息
                info.Level = GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating spatial location element information.: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for spatial element
        /// </summary>
        public static SpatialElementInfo CreateSpatialElementInfo(Document doc, Element element)
        {
            try
            {
                if (element == null || !(element is SpatialElement))
                    return null;
                SpatialElement spatialElement = element as SpatialElement;
                SpatialElementInfo info = new SpatialElementInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Get room or area number
                if (element is Room room)
                {
                    info.Number = room.Number;
                    // 转换为mm³
                    info.Volume = room.Volume * Math.Pow(304.8, 3);
                }
                else if (element is Area area)
                {
                    info.Number = area.Number;
                }

                // Get area
                Parameter areaParam = element.get_Parameter(BuiltInParameter.ROOM_AREA);
                if (areaParam != null && areaParam.HasValue)
                {
                    // 转换为mm²
                    info.Area = areaParam.AsDouble() * Math.Pow(304.8, 2);
                }

                // Get perimeter
                Parameter perimeterParam = element.get_Parameter(BuiltInParameter.ROOM_PERIMETER);
                if (perimeterParam != null && perimeterParam.HasValue)
                {
                    // 转换为mm
                    info.Perimeter = perimeterParam.AsDouble() * 304.8;
                }

                // Get level
                info.Level = GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating spatial element information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for view element
        /// </summary>
        public static ViewInfo CreateViewInfo(Document doc, Element element)
        {
            try
            {
                if (element == null || !(element is View))
                    return null;
                View view = element as View;

                ViewInfo info = new ViewInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    ViewType = view.ViewType.ToString(),
                    Scale = view.Scale,
                    IsTemplate = view.IsTemplate,
                    DetailLevel = view.DetailLevel.ToString(),
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Get level associated with view
                if (view is ViewPlan viewPlan && viewPlan.GenLevel != null)
                {
                    Level level = viewPlan.GenLevel;
                    info.AssociatedLevel = new LevelInfo
                    {
                        Id = level.Id.GetIntValue(),
                        Name = level.Name,
                        Height = level.Elevation * 304.8 // 转换为mm
                    };
                }

                // Determine if the view is open and active
                UIDocument uidoc = new UIDocument(doc);

                // Get all open views
                IList<UIView> openViews = uidoc.GetOpenUIViews();

                foreach (UIView uiView in openViews)
                {
                    // 检查视图是否打开
                    if (uiView.ViewId.GetValue() == view.Id.GetValue())
                    {
                        info.IsOpen = true;

                        // Check if the view is the currently active view.
                        if (uidoc.ActiveView.Id.GetValue() == view.Id.GetValue())
                        {
                            info.IsActive = true;
                        }
                        break;
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating view element information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for annotation element
        /// </summary>
        public static AnnotationInfo CreateAnnotationInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                AnnotationInfo info = new AnnotationInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Get owner view
                Parameter viewParam = element.get_Parameter(BuiltInParameter.VIEW_NAME);
                if (viewParam != null && viewParam.HasValue)
                {
                    info.OwnerView = viewParam.AsString();
                }
                else if (element.OwnerViewId != ElementId.InvalidElementId)
                {
                    View ownerView = doc.GetElement(element.OwnerViewId) as View;
                    info.OwnerView = ownerView?.Name;
                }

                // 处理文字标注
                if (element is TextNote textNote)
                {
                    info.TextContent = textNote.Text;
                    XYZ position = textNote.Coord;
                    // 转换为mm
                    info.Position = new JZPoint(
                        position.X * 304.8,
                        position.Y * 304.8,
                        position.Z * 304.8);
                }
                // Process dimensions
                else if (element is Dimension dimension)
                {
                    info.DimensionValue = dimension.Value.ToString();
                    XYZ origin = dimension.Origin;
                    // 转换为mm
                    info.Position = new JZPoint(
                        origin.X * 304.8,
                        origin.Y * 304.8,
                        origin.Z * 304.8);
                }
                // Process other annotation elements
                else if (element is AnnotationSymbol annotationSymbol)
                {
                    if (annotationSymbol.Location is LocationPoint locationPoint)
                    {
                        XYZ position = locationPoint.Point;
                        // 转换为mm
                        info.Position = new JZPoint(
                            position.X * 304.8,
                            position.Y * 304.8,
                            position.Z * 304.8);
                    }
                }
                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating annotation element information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create group or link information
        /// </summary>
        public static GroupOrLinkInfo CreateGroupOrLinkInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                GroupOrLinkInfo info = new GroupOrLinkInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 处理组
                if (element is Group group)
                {
                    ICollection<ElementId> memberIds = group.GetMemberIds();
                    info.MemberCount = memberIds?.Count;
                    info.GroupType = group.GroupType?.Name;
                }
                // 处理链接
                else if (element is RevitLinkInstance linkInstance)
                {
                    RevitLinkType linkType = doc.GetElement(linkInstance.GetTypeId()) as RevitLinkType;
                    if (linkType != null)
                    {
                        ExternalFileReference extFileRef = linkType.GetExternalFileReference();
                        // 获取绝对路径
                        string absPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extFileRef.GetAbsolutePath());
                        info.LinkPath = absPath;

                        // 使用GetLinkedFileStatusGet link status
                        LinkedFileStatus linkStatus = linkType.GetLinkedFileStatus();
                        info.LinkStatus = linkStatus.ToString();
                    }
                    else
                    {
                        info.LinkStatus = LinkedFileStatus.Invalid.ToString();
                    }

                    // Get position
                    LocationPoint location = linkInstance.Location as LocationPoint;
                    if (location != null)
                    {
                        XYZ point = location.Point;
                        // 转换为mm
                        info.Position = new JZPoint(
                            point.X * 304.8,
                            point.Y * 304.8,
                            point.Z * 304.8);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating group and link information: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create enhanced basic information of element
        /// </summary>
        public static ElementBasicInfo CreateElementBasicInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                ElementBasicInfo basicInfo = new ElementBasicInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    BoundingBox = GetBoundingBoxInfo(element)
                };
                return basicInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating basic element information: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get thickness parameter information of system family components.
        /// </summary>
        /// <param name="element">System family components (walls, floors, doors, etc.)</param>
        /// <returns>Parameter information object, returns null on invalidnull</returns>
        public static ParameterInfo GetThicknessInfo(Element element)
        {
            if (element == null)
            {
                return null;
            }

            // 获取构件类型
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
            if (elementType == null)
            {
                return null;
            }

            // Get the corresponding built-in thickness parameters based on different component types.
            Parameter thicknessParam = null;

            if (elementType is WallType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
            }
            else if (elementType is FloorType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
            }
            else if (elementType is FamilySymbol familySymbol)
            {
                switch (familySymbol.Category?.Id.GetIntValue())
                {
                    case (int)BuiltInCategory.OST_Doors:
                    case (int)BuiltInCategory.OST_Windows:
                        thicknessParam = elementType.get_Parameter(BuiltInParameter.FAMILY_THICKNESS_PARAM);
                        break;
                }
            }
            else if (elementType is CeilingType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.CEILING_THICKNESS);
            }

            if (thicknessParam != null && thicknessParam.HasValue)
            {
                return new ParameterInfo
                {
                    Name = "厚度",
                    Value = $"{thicknessParam.AsDouble() * 304.8}"
                };
            }
            return null;
        }

        /// <summary>
        /// Get the level information to which the element belongs
        /// </summary>
        public static LevelInfo GetElementLevel(Document doc, Element element)
        {
            try
            {
                Level level = null;

                // Handle level retrieval for different types of elements.
                if (element is Wall wall) // 墙体
                {
                    level = doc.GetElement(wall.LevelId) as Level;
                }
                else if (element is Floor floor) // 楼板
                {
                    Parameter levelParam = floor.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }
                else if (element is FamilyInstance familyInstance) // Family instances (including generic models, etc.)
                {
                    // Try to get the level parameter of the family instance.
                    Parameter levelParam = familyInstance.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                    // If the above method fails to retrieve it, try usingSCHEDULE_LEVEL_PARAM
                    if (level == null)
                    {
                        levelParam = familyInstance.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                        if (levelParam != null && levelParam.HasValue)
                        {
                            level = doc.GetElement(levelParam.AsElementId()) as Level;
                        }
                    }
                }
                else // Other elements
                {
                    // Try to get common level parameters
                    Parameter levelParam = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }

                if (level != null)
                {
                    LevelInfo levelInfo = new LevelInfo
                    {
                        Id = level.Id.GetIntValue(),
                        Name = level.Name,
                        Height = level.Elevation * 304.8
                    };
                    return levelInfo;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get bounding box information of element
        /// </summary>
        public static BoundingBoxInfo GetBoundingBoxInfo(Element element)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox == null)
                    return null;
                return new BoundingBoxInfo
                {
                    Min = new JZPoint(
                        bbox.Min.X * 304.8,
                        bbox.Min.Y * 304.8,
                        bbox.Min.Z * 304.8),
                    Max = new JZPoint(
                        bbox.Max.X * 304.8,
                        bbox.Max.Y * 304.8,
                        bbox.Max.Z * 304.8)
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get height parameter information of the bounding box.
        /// </summary>
        /// <param name="boundingBoxInfo">Bounding box info</param>
        /// <returns>Parameter information object, returns null on invalidnull</returns>
        public static ParameterInfo GetBoundingBoxHeight(BoundingBoxInfo boundingBoxInfo)
        {
            try
            {
                // Parameter check
                if (boundingBoxInfo?.Min == null || boundingBoxInfo?.Max == null)
                {
                    return null;
                }

                // ZDifference in axis direction is height
                double height = Math.Abs(boundingBoxInfo.Max.Z - boundingBoxInfo.Min.Z);

                return new ParameterInfo
                {
                    Name = "高度",
                    Value = $"{height}"
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the names and values of all non-empty parameters in the element.
        /// </summary>
        /// <param name="element">Revit元素</param>
        /// <returns>Parameter information list</returns>
        public static List<ParameterInfo> GetDimensionParameters(Element element)
        {
            // Check if element is null or empty
            if (element == null)
            {
                return new List<ParameterInfo>();
            }

            var parameters = new List<ParameterInfo>();

            // Get all parameters of element
            foreach (Parameter param in element.Parameters)
            {
                try
                {
                    // Skip invalid parameters
                    if (!param.HasValue || param.IsReadOnly)
                    {
                        continue;
                    }

                    // If the current parameter is a dimension-related parameter.
                    if (IsDimensionParameter(param))
                    {
                        // Get string representation of parameter value
                        string value = param.AsValueString();

                        // If the value is non-empty, add it to the list.
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            parameters.Add(new ParameterInfo
                            {
                                Name = param.Definition.Name,
                                Value = value
                            });
                        }
                    }
                }
                catch
                {
                    // If an error occurs while getting a parameter value, continue processing the next one.
                    continue;
                }
            }

            // Sort by parameter name and return
            return parameters.OrderBy(p => p.Name).ToList();
        }

        /// <summary>
        /// Determine if the parameter is a writable dimension parameter.
        /// </summary>
        public static bool IsDimensionParameter(Parameter param)
        {

#if REVIT2023_OR_GREATER
            // 在Revit 2023中使用Definition的GetDataType()方法获取参数类型
            ForgeTypeId paramTypeId = param.Definition.GetDataType();

            // Determine if the parameter is a dimension-related type.
            bool isDimensionType = paramTypeId.Equals(SpecTypeId.Length) ||
                                   paramTypeId.Equals(SpecTypeId.Angle) ||
                                   paramTypeId.Equals(SpecTypeId.Area) ||
                                   paramTypeId.Equals(SpecTypeId.Volume);
            // Only store dimension type parameters
            return isDimensionType;
#else
            // Determine if the parameter is a dimension-related type.
            bool isDimensionType = param.Definition.ParameterType == ParameterType.Length ||
                                   param.Definition.ParameterType == ParameterType.Angle ||
                                   param.Definition.ParameterType == ParameterType.Area ||
                                   param.Definition.ParameterType == ParameterType.Volume;

            // Only store dimension type parameters
            return isDimensionType;
#endif
        }

    }

    /// <summary>
    /// Custom class storing complete element information.
    /// </summary>
    public class ElementInstanceInfo
    {
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Id
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 类型Id
        /// </summary>
        public int TypeId { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// 类别
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 内置类别
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Owner roomId
        /// </summary>
        public int RoomId { get; set; }
        /// <summary>
        /// 所属Level name
        /// </summary>
        public LevelInfo Level { get; set; }
        /// <summary>
        /// Location info
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Instance parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

    }

    /// <summary>
    /// Custom class storing complete element type information.
    /// </summary>
    public class ElementTypeInfo
    {
        /// <summary>
        /// ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Id
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 内置类别ID
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Type parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

    }

    /// <summary>
    /// 空间定位元素(标高、轴网等)基础信息的类
    /// </summary>
    public class PositioningElementInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique elementID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 元素的.NET类名称
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// 高程值 (Applicable to levels, unitmm)
        /// </summary>
        public double? Elevation { get; set; }
        /// <summary>
        /// 所属标高
        /// </summary>
        public LevelInfo Level { get; set; }
        /// <summary>
        /// Location info
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// 轴网线(Applicable to grids)
        /// </summary>
        public JZLine GridLine { get; set; }
    }
    /// <summary>
    /// 存储空间元素(房间、区域等)基础信息的类
    /// </summary>
    public class SpatialElementInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique elementID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// 编号
        /// </summary>
        public string Number { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 元素的.NET类名称
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// 面积(单位mm²)
        /// </summary>
        public double? Area { get; set; }
        /// <summary>
        /// 体积(单位mm³)
        /// </summary>
        public double? Volume { get; set; }
        /// <summary>
        /// 周长(单位mm)
        /// </summary>
        public double? Perimeter { get; set; }
        /// <summary>
        /// Level
        /// </summary>
        public LevelInfo Level { get; set; }

        /// <summary>
        /// Location info
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }
    /// <summary>
    /// Class for storing basic information of view elements
    /// </summary>
    public class ViewInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique elementID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 元素的.NET类名称
        /// </summary>
        public string ElementClass { get; set; }

        /// <summary>
        /// View type
        /// </summary>
        public string ViewType { get; set; }

        /// <summary>
        /// View scale
        /// </summary>
        public int? Scale { get; set; }

        /// <summary>
        /// 是否为模板视图
        /// </summary>
        public bool IsTemplate { get; set; }

        /// <summary>
        /// Detail level
        /// </summary>
        public string DetailLevel { get; set; }

        /// <summary>
        /// Associated level
        /// </summary>
        public LevelInfo AssociatedLevel { get; set; }

        /// <summary>
        /// Location info
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }

        /// <summary>
        /// 视图是否已打开
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// Whether it is the currently active view
        /// </summary>
        public bool IsActive { get; set; }
    }
    /// <summary>
    /// Class storing basic information of annotation elements.
    /// </summary>
    public class AnnotationInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique elementID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 元素的.NET类名称
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// 所在视图
        /// </summary>
        public string OwnerView { get; set; }
        /// <summary>
        /// Text content (适用于文字标注)
        /// </summary>
        public string TextContent { get; set; }
        /// <summary>
        /// Location info(单位mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// Location info
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// 尺寸值 (适用于尺寸标注)
        /// </summary>
        public string DimensionValue { get; set; }
    }
    /// <summary>
    /// Class storing group and link basic information.
    /// </summary>
    public class GroupOrLinkInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique elementID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 元素的.NET类名称
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Group member count
        /// </summary>
        public int? MemberCount { get; set; }
        /// <summary>
        /// 组类型
        /// </summary>
        public string GroupType { get; set; }
        /// <summary>
        /// Link status
        /// </summary>
        public string LinkStatus { get; set; }
        /// <summary>
        /// Link path
        /// </summary>
        public string LinkPath { get; set; }
        /// <summary>
        /// Location info(单位mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// Location info
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }
    /// <summary>
    /// Enhanced class storing basic element information.
    /// </summary>
    public class ElementBasicInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique elementID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// Location info
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }



    /// <summary>
    /// Custom class storing complete parameter information.
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// Custom class storing bounding box information.
    /// </summary>
    public class BoundingBoxInfo
    {
        public JZPoint Min { get; set; }
        public JZPoint Max { get; set; }
    }

    /// <summary>
    /// Custom class for storing level information
    /// </summary>
    public class LevelInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Height { get; set; }
    }



}
