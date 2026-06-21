using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Services
{
    public class OperateElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
        public OperationSetting OperationData { get; private set; }
        /// <summary>
        /// Execution result (outgoing data)
        /// </summary>
        public AIResult<string> Result { get; private set; }

        /// <summary>
        /// 设置创建的参数
        /// </summary>
        public void SetParameters(OperationSetting data)
        {
            OperationData = data;
            _resetEvent.Reset();
        }
        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                bool result = ExecuteElementOperation(uiDoc, OperationData);

                Result = new AIResult<string>
                {
                    Success = true,
                    Message = $"Operation executed successfully",
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<string>
                {
                    Success = false,
                    Message = $"Operate on element时出错: {ex.Message}",
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
            return "Operate on element";
        }

        /// <summary>
        /// Execute the corresponding element operation according to the operation settings.
        /// </summary>
        /// <param name="uidoc">当前UI文档</param>
        /// <param name="setting">操作设置</param>
        /// <returns>操作Is successful</returns>
        public static bool ExecuteElementOperation(UIDocument uidoc, OperationSetting setting)
        {
            // 检查参数有效性
            if (uidoc == null || uidoc.Document == null || setting == null || setting.ElementIds == null ||
                (setting.ElementIds.Count == 0 && setting.Action.ToLower() != "resetisolate"))
                throw new Exception("Invalid parameter: the document is null or no elements to operate on are specified.");

            Document doc = uidoc.Document;

            // 将intelements of typeID转换为ElementId类型
            ICollection<ElementId> elementIds = setting.ElementIds.Select(id => new ElementId(id)).ToList();

            // 解析操作类型
            ElementOperationType action;
            if (!Enum.TryParse(setting.Action, true, out action))
            {
                throw new Exception($"Unsupported operation type:{setting.Action}");
            }

            // Execute different operations based on operation type.
            switch (action)
            {
                case ElementOperationType.Select:
                    // Select element
                    uidoc.Selection.SetElementIds(elementIds);
                    return true;

                case ElementOperationType.SelectionBox:
                    // 在3DCreate section box in view

                    // Check if current view is3D视图
                    View3D targetView;

                    if (doc.ActiveView is View3D)
                    {
                        // 如果当前视图是3Dview, create a section box in the current view.
                        targetView = doc.ActiveView as View3D;
                    }
                    else
                    {
                        // If current view is not3D视图，寻找默认3D视图
                        FilteredElementCollector collector = new FilteredElementCollector(doc);
                        collector.OfClass(typeof(View3D));

                        // 尝试找到默认3Dview or any other available3D视图
                        targetView = collector
                            .Cast<View3D>()
                            .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked && (v.Name.Contains("{3D}") || v.Name.Contains("Default 3D")));

                        if (targetView == null)
                        {
                            // If no suitable found3D视图，抛出异常
                            throw new Exception("无法找到合适的3DView used to create section box");
                        }

                        // 激活该3D视图
                        uidoc.ActiveView = targetView;
                    }

                    // Calculate bounding box of selected elements
                    BoundingBoxXYZ boundingBox = null;

                    foreach (ElementId id in elementIds)
                    {
                        Element elem = doc.GetElement(id);
                        BoundingBoxXYZ elemBox = elem.get_BoundingBox(null);

                        if (elemBox != null)
                        {
                            if (boundingBox == null)
                            {
                                boundingBox = new BoundingBoxXYZ
                                {
                                    Min = new XYZ(elemBox.Min.X, elemBox.Min.Y, elemBox.Min.Z),
                                    Max = new XYZ(elemBox.Max.X, elemBox.Max.Y, elemBox.Max.Z)
                                };
                            }
                            else
                            {
                                // Extend bounding box to contain current element
                                boundingBox.Min = new XYZ(
                                    Math.Min(boundingBox.Min.X, elemBox.Min.X),
                                    Math.Min(boundingBox.Min.Y, elemBox.Min.Y),
                                    Math.Min(boundingBox.Min.Z, elemBox.Min.Z));

                                boundingBox.Max = new XYZ(
                                    Math.Max(boundingBox.Max.X, elemBox.Max.X),
                                    Math.Max(boundingBox.Max.Y, elemBox.Max.Y),
                                    Math.Max(boundingBox.Max.Z, elemBox.Max.Z));
                            }
                        }
                    }

                    if (boundingBox == null)
                    {
                        throw new Exception("Unable to create bounding box for the selected elements.");
                    }

                    // Increase the bounding box size to make it slightly larger than the element.
                    double offset = 1.0; // 1offset in feet
                    boundingBox.Min = new XYZ(boundingBox.Min.X - offset, boundingBox.Min.Y - offset, boundingBox.Min.Z - offset);
                    boundingBox.Max = new XYZ(boundingBox.Max.X + offset, boundingBox.Max.Y + offset, boundingBox.Max.Z + offset);

                    // 在3DEnable and set section box in view
                    using (Transaction trans = new Transaction(doc, "Create section box"))
                    {
                        trans.Start();
                        targetView.IsSectionBoxActive = true;
                        targetView.SetSectionBox(boundingBox);
                        trans.Commit();
                    }

                    // 移动到视图中心
                    uidoc.ShowElements(elementIds);
                    return true;

                case ElementOperationType.SetColor:
                    // Set element to specified color
                    using (Transaction trans = new Transaction(doc, "Set element color"))
                    {
                        trans.Start();
                        SetElementsColor(doc, elementIds, setting.ColorValue);
                        trans.Commit();
                    }
                    // Scroll to these elements to make them visible
                    uidoc.ShowElements(elementIds);
                    return true;


                case ElementOperationType.SetTransparency:
                    // Set the transparency of elements in the current view.
                    using (Transaction trans = new Transaction(doc, "设置元素透明度"))
                    {
                        trans.Start();

                        // Create graphic overrides settings object
                        OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();

                        // Set transparency(Ensure value is in0-100范围内)
                        int transparencyValue = Math.Max(0, Math.Min(100, setting.TransparencyValue));

                        // 设置表面透明度
                        overrideSettings.SetSurfaceTransparency(transparencyValue);

                        // Apply transparency settings to each element.
                        foreach (ElementId id in elementIds)
                        {
                            doc.ActiveView.SetElementOverrides(id, overrideSettings);
                        }

                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Delete:
                    // Delete elements (transaction required)
                    using (Transaction trans = new Transaction(doc, "删除元素"))
                    {
                        trans.Start();
                        doc.Delete(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Hide:
                    // Hide elements (requires active view and transaction).
                    using (Transaction trans = new Transaction(doc, "Hide elements"))
                    {
                        trans.Start();
                        doc.ActiveView.HideElements(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.TempHide:
                    // Temporarily hide elements (requires active view and transaction).
                    using (Transaction trans = new Transaction(doc, "临时Hide elements"))
                    {
                        trans.Start();
                        doc.ActiveView.HideElementsTemporary(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Isolate:
                    // Isolate elements (requires active view and transaction).
                    using (Transaction trans = new Transaction(doc, "Isolate elements"))
                    {
                        trans.Start();
                        doc.ActiveView.IsolateElementsTemporary(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Unhide:
                    // Unhide elements (requires active view and transaction).
                    using (Transaction trans = new Transaction(doc, "取消Hide elements"))
                    {
                        trans.Start();
                        doc.ActiveView.UnhideElements(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.ResetIsolate:
                    // Reset isolation (requires active view and transaction).
                    using (Transaction trans = new Transaction(doc, "Reset isolation"))
                    {
                        trans.Start();
                        doc.ActiveView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                        trans.Commit();
                    }
                    return true;

                default:
                    throw new Exception($"Unsupported operation type:{setting.Action}");
            }
        }

        /// <summary>
        /// Set the specified elements to a specified color in the view.
        /// </summary>
        /// <param name="doc">文档</param>
        /// <param name="elementIds">Elements to set color forID集合</param>
        /// <param name="elementColor">颜色值（RGB格式）</param>
        private static void SetElementsColor(Document doc, ICollection<ElementId> elementIds, int[] elementColor)
        {
            // Check if color array is valid
            if (elementColor == null || elementColor.Length < 3)
            {
                elementColor = new int[] { 255, 0, 0 }; // Default red
            }
            // 确保RGB值在0-255范围内
            int r = Math.Max(0, Math.Min(255, elementColor[0]));
            int g = Math.Max(0, Math.Min(255, elementColor[1]));
            int b = Math.Max(0, Math.Min(255, elementColor[2]));
            // 创建RevitColor object - 使用byteType conversion
            Color color = new Color((byte)r, (byte)g, (byte)b);
            // 创建图形覆盖设置
            OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();
            // 设置指定颜色
            overrideSettings.SetProjectionLineColor(color);
            overrideSettings.SetCutLineColor(color);
            overrideSettings.SetSurfaceForegroundPatternColor(color);
            overrideSettings.SetSurfaceBackgroundPatternColor(color);

            // 尝试设置填充图案
            try
            {
                // Try to get default fill pattern
                FilteredElementCollector patternCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement));

                // First try to find solid fill pattern
                FillPatternElement solidPattern = patternCollector
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

                if (solidPattern != null)
                {
                    overrideSettings.SetSurfaceForegroundPatternId(solidPattern.Id);
                    overrideSettings.SetSurfaceForegroundPatternVisible(true);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set fill pattern: {ex.Message}");
            }

            // Apply override settings to each element
            foreach (ElementId id in elementIds)
            {
                doc.ActiveView.SetElementOverrides(id, overrideSettings);
            }
        }

    }
}
