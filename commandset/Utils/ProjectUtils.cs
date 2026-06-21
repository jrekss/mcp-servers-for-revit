using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Commands;
using RevitMCPCommandSet.Models.Common;
using System.IO;
using System.Reflection;

namespace RevitMCPCommandSet.Utils
{
    public static class ProjectUtils
    {
        /// <summary>
        /// Generic method to create family instance
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="familySymbol">族类型</param>
        /// <param name="locationPoint">位置点</param>
        /// <param name="locationLine">基准线</param>
        /// <param name="baseLevel">Bottom level</param>
        /// <param name="topLevel">Second level(用于TwoLevelsBased)</param>
        /// <param name="baseOffset">Bottom Offset (ft）</param>
        /// <param name="topOffset">Top Offset (ft）</param>
        /// <param name="faceDirection">Reference direction</param>
        /// <param name="handDirection">Reference direction</param>
        /// <param name="view">视图</param>
        /// <returns>Created family instance, returns null on failurenull</returns>
        public static FamilyInstance CreateInstance(
            this Document doc,
            FamilySymbol familySymbol,
            XYZ locationPoint = null,
            Line locationLine = null,
            Level baseLevel = null,
            Level topLevel = null,
            double baseOffset = -1,
            double topOffset = -1,
            XYZ faceDirection = null,
            XYZ handDirection = null,
            View view = null,
            Element explicitHost = null,
            bool snapToHostCenter = true)
        {
            // Basic parameter check
            if (doc == null)
                throw new ArgumentNullException($"Required parameters{typeof(Document)} {nameof(doc)}缺失！");
            if (familySymbol == null)
                throw new ArgumentNullException($"Required parameters{typeof(FamilySymbol)} {nameof(familySymbol)}缺失！");

            // Activate family model
            if (!familySymbol.IsActive)
                familySymbol.Activate();

            FamilyInstance instance = null;

            // Select the creation method based on the placement type of the family.
            switch (familySymbol.Family.FamilyPlacementType)
            {
                // Single-level-based families (e.g., metric generic models)
                case FamilyPlacementType.OneLevelBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException($"Required parameters{typeof(XYZ)} {nameof(locationPoint)}缺失！");
                    // with level information
                    if (baseLevel != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            locationPoint,                  // Physical location where the instance will be placed
                            familySymbol,                   // representing the instance type to be inserted FamilySymbol 对象
                            baseLevel,                      // used as base level of object Level 对象
                            StructuralType.NonStructural);  // If it is a structural component, specify the type of the component.
                    }
                    // 不with level information
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(
                            locationPoint,                  // Physical location where the instance will be placed
                            familySymbol,                   // representing the instance type to be inserted FamilySymbol 对象
                            StructuralType.NonStructural);  // If it is a structural component, specify the type of the component.
                    }
                    break;

                // Single-level and host-based families (e.g., doors, windows)
                case FamilyPlacementType.OneLevelBasedHosted:
                    if (locationPoint == null)
                        throw new ArgumentNullException($"Required parameters{typeof(XYZ)} {nameof(locationPoint)}缺失！");

                    Element host = explicitHost;
                    XYZ placementPoint = locationPoint;

                    // If explicit host provided and it's a wall, snap to its centerline
                    if (host != null && snapToHostCenter && host is Wall explicitWall)
                    {
                        LocationCurve eLoc = explicitWall.Location as LocationCurve;
                        if (eLoc != null)
                        {
                            IntersectionResult eIr = eLoc.Curve.Project(locationPoint);
                            if (eIr != null)
                                placementPoint = new XYZ(eIr.XYZPoint.X, eIr.XYZPoint.Y, locationPoint.Z);
                        }
                    }

                    // Auto-detect host wall if not explicitly provided
                    if (host == null)
                    {
                        // Try geometric wall-centerline proximity first
                        var wallResult = doc.GetNearestWallByLocationLine(locationPoint, baseLevel);
                        if (wallResult.HasValue)
                        {
                            host = wallResult.Value.wall;
                            if (snapToHostCenter)
                                placementPoint = wallResult.Value.projectedPoint;
                        }
                        else
                        {
                            // Fall back to original ray-casting method
                            host = doc.GetNearestHostElement(locationPoint, familySymbol);
                        }
                    }

                    if (host == null)
                        throw new ArgumentNullException($"Compliant host information not found!");

                    if (baseLevel != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            placementPoint,
                            familySymbol,
                            host,
                            baseLevel,
                            StructuralType.NonStructural);
                    }
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(
                            placementPoint,
                            familySymbol,
                            host,
                            StructuralType.NonStructural);
                    }

                    // Set sill height for windows (baseOffset maps to sill height for hosted elements)
                    if (instance != null && baseOffset != -1)
                    {
                        Parameter sillParam = instance.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                        if (sillParam != null && !sillParam.IsReadOnly)
                        {
                            sillParam.Set(baseOffset);
                        }
                    }
                    break;

                // Two-level-based families (e.g., columns)
                case FamilyPlacementType.TwoLevelsBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException($"Required parameters{typeof(XYZ)} {nameof(locationPoint)}缺失！");
                    if (baseLevel == null)
                        throw new ArgumentNullException($"Required parameters{typeof(Level)} {nameof(baseLevel)}缺失！");
                    // Determine whether it is a structural column or architectural column
                    StructuralType structuralType = StructuralType.NonStructural;
                    if (familySymbol.Category.Id.GetIntValue() == (int)BuiltInCategory.OST_StructuralColumns)
                        structuralType = StructuralType.Column;
                    instance = doc.Create.NewFamilyInstance(
                        locationPoint,              // Physical location where the instance will be placed
                        familySymbol,               // representing the instance type to be inserted FamilySymbol 对象
                        baseLevel,                  // used as base level of object Level 对象
                        structuralType);            // If it is a structural component, specify the type of the component.
                    // Set the bottom level, top level, bottom offset, and top offset.
                    if (instance != null)
                    {
                        // Set the base level and top level of the column.
                        if (baseLevel != null)
                        {
                            Parameter baseLevelParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                            if (baseLevelParam != null)
                                baseLevelParam.Set(baseLevel.Id);
                        }
                        if (topLevel != null)
                        {
                            Parameter topLevelParam = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (topLevelParam != null)
                                topLevelParam.Set(topLevel.Id);
                        }
                        // 获取Bottom offset参数
                        if (baseOffset != -1)
                        {
                            Parameter baseOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                            if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                            {
                                // 将毫米转换为RevitInternal units
                                double baseOffsetInternal = baseOffset;
                                baseOffsetParam.Set(baseOffsetInternal);
                            }
                        }
                        // 获取顶部偏移参数
                        if (topOffset != -1)
                        {
                            Parameter topOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                            if (topOffsetParam != null && topOffsetParam.StorageType == StorageType.Double)
                            {
                                // 将毫米转换为RevitInternal units
                                double topOffsetInternal = topOffset;
                                topOffsetParam.Set(topOffsetInternal);
                            }
                        }
                    }
                    break;

                // The family is view-specific (e.g., detail annotations).
                case FamilyPlacementType.ViewBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException($"Required parameters{typeof(XYZ)} {nameof(locationPoint)}缺失！");
                    instance = doc.Create.NewFamilyInstance(
                        locationPoint,  // The origin of the family instance. If created in a plan view (ViewPlan), the origin will be projected onto the plan view.
                        familySymbol,   // A FamilySymbol object representing the instance type to be inserted.
                        view);          // 放置族实例的2D视图
                    break;

                // Work-plane-based families (e.g., face-based metric generic models, including face-based, wall-based, etc.)
                case FamilyPlacementType.WorkPlaneBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException($"Required parameters{typeof(XYZ)} {nameof(locationPoint)}缺失！");
                    // Get nearest host face
                    Reference hostFace = doc.GetNearestFaceReference(locationPoint, 1000 / 304.8);
                    if (hostFace == null)
                        throw new ArgumentNullException($"Compliant host information not found!");
                    if (faceDirection == null || faceDirection == XYZ.Zero)
                    {
                        var result = doc.GenerateDefaultOrientation(hostFace);
                        faceDirection = result.FacingOrientation;
                    }
                    // Create a family instance on a face using a point and direction.
                    instance = doc.Create.NewFamilyInstance(
                        hostFace,               // Reference to face  
                        locationPoint,          // Point on face where the instance will be placed
                        faceDirection,          // Defines the vector for the family instance direction. Note that this direction defines the rotation of the instance on the face, and therefore cannot be parallel to the face normal.
                        familySymbol);          // representing the instance type to be inserted FamilySymbol 对象。请注意，此FamilySymbolmust represent FamilyPlacementType 为 WorkPlaneBased 的族
                    break;

                // Line-based and work-plane-based families (e.g., line-based metric generic models)
                case FamilyPlacementType.CurveBased:
                    if (locationLine == null)
                        throw new ArgumentNullException($"Required parameters{typeof(Line)} {nameof(locationLine)}缺失！");

                    // Get the nearest host face (no tolerance allowed).
                    Reference lineHostFace = doc.GetNearestFaceReference(locationLine.Evaluate(0.5, true), 1e-5);
                    if (lineHostFace != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            lineHostFace,   // Reference to face 
                            locationLine,   // 族实例基于的曲线
                            familySymbol);  // 一个FamilySymbolobject, representing the type of the instance to be inserted. Note that thisSymbolmust represent its FamilyPlacementType 为 WorkPlaneBased 或 CurveBased 的族
                    }
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(
                            locationLine,                   // 族实例基于的曲线
                            familySymbol,                   // 一个FamilySymbolobject, representing the type of the instance to be inserted. Note that thisSymbolmust represent its FamilyPlacementType 为 WorkPlaneBased 或 CurveBased 的族
                            baseLevel,                      // 一个Levelobject, used as the base level of the object.
                            StructuralType.NonStructural);  // If it is a structural component, specify the type of the component.
                    }
                    if (instance != null)
                    {
                        // 获取Bottom offset参数
                        if (baseOffset != -1)
                        {
                            Parameter baseOffsetParam = instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                            if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                            {
                                // 将毫米转换为RevitInternal units
                                double baseOffsetInternal = baseOffset;
                                baseOffsetParam.Set(baseOffsetInternal);
                            }
                        }
                    }
                    break;

                // Line-based and view-specific families (e.g., detail components)
                case FamilyPlacementType.CurveBasedDetail:
                    if (locationLine == null)
                        throw new ArgumentNullException($"Required parameters{typeof(Line)} {nameof(locationLine)}缺失！");
                    if (view == null)
                        throw new ArgumentNullException($"Required parameters{typeof(View)} {nameof(view)}缺失！");
                    instance = doc.Create.NewFamilyInstance(
                        locationLine,   // The line location of the family instance. The line must lie within the view plane.
                        familySymbol,   // A FamilySymbol object representing the instance type to be inserted.
                        view);          // 放置族实例的2D视图
                    break;

                // Structural curve-driven families (e.g., beams, braces, or slanted columns)
                case FamilyPlacementType.CurveDrivenStructural:
                    if (locationLine == null)
                        throw new ArgumentNullException($"Required parameters{typeof(Line)} {nameof(locationLine)}缺失！");
                    if (baseLevel == null)
                        throw new ArgumentNullException($"Required parameters{typeof(Level)} {nameof(baseLevel)}缺失！");
                    instance = doc.Create.NewFamilyInstance(
                        locationLine,                   // 族实例基于的曲线
                        familySymbol,                   // 一个FamilySymbolobject, representing the type of the instance to be inserted. Note that thisSymbolmust represent its FamilyPlacementType 为 WorkPlaneBased 或 CurveBased 的族
                        baseLevel,                      // 一个Levelobject, used as the base level of the object.
                        StructuralType.Beam);           // If it is a structural component, specify the type of the component.
                    break;

                // Adaptive families (e.g., adaptive metric generic models, curtain wall panels)
                case FamilyPlacementType.Adaptive:
                    throw new NotImplementedException("未实现FamilyPlacementType.Adaptivecreation method!");

                default:
                    break;
            }
            return instance;
        }

        /// <summary>
        /// Generate default orientation and hand orientation (default long side isHandOrientation, short side isFacingOrientation）
        /// </summary>
        /// <param name="hostFace"></param>
        /// <returns></returns>
        public static (XYZ FacingOrientation, XYZ HandOrientation) GenerateDefaultOrientation(this Document doc, Reference hostFace)
        {
            var facingOrientation = new XYZ();  // 朝向方向：族内YOrientation of the positive axis direction after loading
            var handOrientation = new XYZ();    // 手向方向：族内XOrientation of the positive axis direction after loading

            // Step1 从Referenceget face object from
            Face face = doc.GetElement(hostFace.ElementId).GetGeometryObjectFromReference(hostFace) as Face;

            // Step2 Get face profile
            List<Curve> profile = null;
            // A collection of profile loops, where each sub-list represents a complete closed profile, and the first one is usually the outer profile.
            List<List<Curve>> profiles = new List<List<Curve>>();
            // Get all profile loops (outer profiles and potential inner holes).
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            // 遍历每个轮廓循环
            foreach (EdgeArray loop in edgeLoops)
            {
                List<Curve> currentLoop = new List<Curve>();
                // Get each edge in the loop
                foreach (Edge edge in loop)
                {
                    Curve curve = edge.AsCurve();
                    currentLoop.Add(curve);
                }
                // If the current loop has edges, add to the results collection.
                if (currentLoop.Count > 0)
                {
                    profiles.Add(currentLoop);
                }
            }
            // The first one is usually the outer contour
            if (profiles != null && profiles.Any())
                profile = profiles.FirstOrDefault();

            // Step3 获取面法向量
            XYZ faceNormal = null;
            // If it is a plane, the normal vector property can be obtained directly.
            if (face is PlanarFace planarFace)
                faceNormal = planarFace.FaceNormal;

            // Step4 Get two compliant (conforming to the right-hand rule) primary directions of the face.
            var result = face.GetMainDirections();
            var primaryDirection = result.PrimaryDirection;
            var secondaryDirection = result.SecondaryDirection;

            // Default long edge direction isHandOrientation，短边方向就是FacingOrientation
            facingOrientation = primaryDirection;
            handOrientation = secondaryDirection;

            // Determine if it conforms to the right-hand rule (thumb:HandOrientation, Index finger: FacingOrientation, Middle finger: FaceNormal）
            if (!facingOrientation.IsRightHandRuleCompliant(handOrientation, faceNormal))
            {
                var newHandOrientation = facingOrientation.GenerateIndexFinger(faceNormal);
                if (newHandOrientation != null)
                {
                    handOrientation = newHandOrientation;
                }
            }

            return (facingOrientation, handOrientation);
        }

        /// <summary>
        /// Get nearest face to pointReference
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="location">Target point location</param>
        /// <param name="radius">Search radius (internal units)</param>
        /// <returns>nearest face'sReference，未找到返回null</returns>
        public static Reference GetNearestFaceReference(this Document doc, XYZ location, double radius = 1000 / 304.8)
        {
            try
            {
                // 误差处理
                location = new XYZ(location.X, location.Y, location.Z + 0.1 / 304.8);

                // Create or get3D视图
                View3D view3D = null;
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D));

                foreach (View3D v in collector)
                {
                    if (!v.IsTemplate)
                    {
                        view3D = v;
                        break;
                    }
                }

                if (view3D == null)
                {
                    using (Transaction trans = new Transaction(doc, "Create 3D View"))
                    {
                        trans.Start();
                        ViewFamilyType vft = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                        if (vft != null)
                        {
                            view3D = View3D.CreateIsometric(doc, vft.Id);
                        }
                        trans.Commit();
                    }
                }

                if (view3D == null)
                {
                    TaskDialog.Show("错误", "无法Create or get3D视图");
                    return null;
                }

                // 设置6个方向的射线
                XYZ[] directions = new XYZ[]
                {
                  XYZ.BasisX,    // X正向
                  -XYZ.BasisX,   // X负向
                  XYZ.BasisY,    // Y正向
                  -XYZ.BasisY,   // Y负向
                  XYZ.BasisZ,    // Z正向
                  -XYZ.BasisZ    // Z负向
                };

                // Create filter
                ElementClassFilter wallFilter = new ElementClassFilter(typeof(Wall));
                ElementClassFilter floorFilter = new ElementClassFilter(typeof(Floor));
                ElementClassFilter ceilingFilter = new ElementClassFilter(typeof(Ceiling));
                ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));

                // Combined filter
                LogicalOrFilter categoryFilter = new LogicalOrFilter(
                    new ElementFilter[] { wallFilter, floorFilter, ceilingFilter, instanceFilter });


                // 1. Simplest: filter for all instantiated elements.
                //ElementFilter filter = new ElementIsElementTypeFilter(true);

                // 创建射线追踪器
                ReferenceIntersector refIntersector = new ReferenceIntersector(categoryFilter,
                    FindReferenceTarget.Face, view3D);
                refIntersector.FindReferencesInRevitLinks = true; // If it is necessary to find faces in linked files.

                double minDistance = double.MaxValue;
                Reference nearestFace = null;

                foreach (XYZ direction in directions)
                {
                    // Cast ray from current position
                    IList<ReferenceWithContext> references = refIntersector.Find(location, direction);

                    foreach (ReferenceWithContext rwc in references)
                    {
                        double distance = rwc.Proximity; // 获取到面的距离

                        // If within search range and closer.
                        if (distance <= radius && distance < minDistance)
                        {
                            minDistance = distance;
                            nearestFace = rwc.GetReference();
                        }
                    }
                }

                return nearestFace;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", $"Error occurred while getting nearest face:{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the closest hostable element to the point.
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="location">Target point location</param>
        /// <param name="familySymbol">family type, used to determine the host type.</param>
        /// <param name="radius">Search radius (internal units)</param>
        /// <returns>The closest host element, returns null if not found.null</returns>
        public static Element GetNearestHostElement(this Document doc, XYZ location, FamilySymbol familySymbol, double radius = 5.0)
        {
            try
            {
                // Basic parameter check
                if (doc == null || location == null || familySymbol == null)
                    return null;

                // Get host behavior parameters of family
                Parameter hostParam = familySymbol.Family.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR);
                int hostingBehavior = hostParam?.AsInteger() ?? 0;

                // Create or get3D视图
                View3D view3D = null;
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D));
                foreach (View3D v in viewCollector)
                {
                    if (!v.IsTemplate)
                    {
                        view3D = v;
                        break;
                    }
                }

                if (view3D == null)
                {
                    using (Transaction trans = new Transaction(doc, "Create 3D View"))
                    {
                        trans.Start();
                        ViewFamilyType vft = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                        if (vft != null)
                        {
                            view3D = View3D.CreateIsometric(doc, vft.Id);
                        }
                        trans.Commit();
                    }
                }

                if (view3D == null)
                {
                    TaskDialog.Show("错误", "无法Create or get3D视图");
                    return null;
                }

                // Create a type filter based on host behavior.
                ElementFilter classFilter;
                switch (hostingBehavior)
                {
                    case 1: // Wall based
                        classFilter = new ElementClassFilter(typeof(Wall));
                        break;
                    case 2: // Floor based
                        classFilter = new ElementClassFilter(typeof(Floor));
                        break;
                    case 3: // Ceiling based
                        classFilter = new ElementClassFilter(typeof(Ceiling));
                        break;
                    case 4: // Roof based
                        classFilter = new ElementClassFilter(typeof(RoofBase));
                        break;
                    default:
                        return null; // 不支持的宿主类型
                }

                // 设置6个方向的射线
                XYZ[] directions = new XYZ[]
                {
                    XYZ.BasisX,    // X正向
                    -XYZ.BasisX,   // X负向
                    XYZ.BasisY,    // Y正向
                    -XYZ.BasisY,   // Y负向
                    XYZ.BasisZ,    // Z正向
                    -XYZ.BasisZ    // Z负向
                };

                // 创建射线追踪器
                ReferenceIntersector refIntersector = new ReferenceIntersector(classFilter,
                    FindReferenceTarget.Element, view3D);
                refIntersector.FindReferencesInRevitLinks = true; // If it is necessary to find elements in linked files.

                double minDistance = double.MaxValue;
                Element nearestHost = null;

                foreach (XYZ direction in directions)
                {
                    // Cast ray from current position
                    IList<ReferenceWithContext> references = refIntersector.Find(location, direction);

                    foreach (ReferenceWithContext rwc in references)
                    {
                        double distance = rwc.Proximity; // Get distance to element

                        // If within search range and closer.
                        if (distance <= radius && distance < minDistance)
                        {
                            minDistance = distance;
                            nearestHost = doc.GetElement(rwc.GetReference().ElementId);
                        }
                    }
                }

                return nearestHost;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", $"An error occurred while getting the nearest host element:{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds the nearest wall to a point using wall location-line distance calculation.
        /// More reliable than ray-casting for door/window placement.
        /// </summary>
        /// <param name="doc">Current Revit document</param>
        /// <param name="point">Target point (internal units, feet)</param>
        /// <param name="level">Level to filter walls on</param>
        /// <param name="tolerance">Extra tolerance beyond half wall width (feet). Default ~5mm.</param>
        /// <returns>Tuple of (wall, projectedPoint, wallDirection, distance) or null</returns>
        public static (Wall wall, XYZ projectedPoint, XYZ wallDirection, double distance)?
            GetNearestWallByLocationLine(
                this Document doc,
                XYZ point,
                Level level,
                double tolerance = 5.0 / 304.8)
        {
            if (doc == null || point == null || level == null)
                return null;

            // Collect all walls on the given level
            var walls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w =>
                {
                    Parameter baseLevelParam = w.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                    return baseLevelParam != null && baseLevelParam.AsElementId() == level.Id;
                })
                .ToList();

            Wall bestWall = null;
            XYZ bestProjection = null;
            XYZ bestDirection = null;
            double bestDistance = double.MaxValue;

            foreach (Wall wall in walls)
            {
                LocationCurve locCurve = wall.Location as LocationCurve;
                if (locCurve == null) continue;

                Curve curve = locCurve.Curve;
                if (curve == null) continue;

                // Use Curve.Project() which handles both lines and arcs
                IntersectionResult ir = curve.Project(new XYZ(point.X, point.Y, curve.GetEndPoint(0).Z));
                if (ir == null) continue;

                XYZ projectedPt = ir.XYZPoint;
                double distance = new XYZ(point.X - projectedPt.X, point.Y - projectedPt.Y, 0).GetLength();

                // Check if point is within half the wall width + tolerance
                double halfWidth = wall.Width / 2.0;
                if (distance <= halfWidth + tolerance && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestWall = wall;
                    bestProjection = new XYZ(projectedPt.X, projectedPt.Y, point.Z);

                    // Compute wall direction from curve tangent at projected parameter
                    XYZ p0 = curve.GetEndPoint(0);
                    XYZ p1 = curve.GetEndPoint(1);
                    bestDirection = new XYZ(p1.X - p0.X, p1.Y - p0.Y, 0).Normalize();
                }
            }

            if (bestWall == null)
                return null;

            return (bestWall, bestProjection, bestDirection, bestDistance);
        }

        /// <summary>
        /// Highlight指定的面
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="faceRef">要Highlight的面Reference</param>
        /// <param name="duration">Highlight duration(毫秒)，默认3000毫秒</param>
        public static void HighlightFace(this Document doc, Reference faceRef)
        {
            if (faceRef == null) return;

            // Get solid fill pattern
            FillPatternElement solidFill = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(x => x.GetFillPattern().IsSolidFill);

            if (solidFill == null)
            {
                TaskDialog.Show("错误", "Solid fill pattern not found");
                return;
            }

            // Create highlight settings
            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceForegroundPatternColor(new Color(255, 0, 0)); // 红色
            ogs.SetSurfaceForegroundPatternId(solidFill.Id);
            ogs.SetSurfaceTransparency(0); // 不透明

            // Highlight
            doc.ActiveView.SetElementOverrides(faceRef.ElementId, ogs);
        }

        /// <summary>
        /// Extract two primary direction vectors of the face.
        /// </summary>
        /// <param name="face">输入面</param>
        /// <returns>Tuple containing primary and secondary directions.</returns>
        /// <exception cref="ArgumentNullException">当面为空时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when the profile of the face is insufficient to form a valid shape.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a valid direction cannot be extracted</exception>
        public static (XYZ PrimaryDirection, XYZ SecondaryDirection) GetMainDirections(this Face face)
        {
            // 1. 参数验证
            if (face == null)
                throw new ArgumentNullException(nameof(face), "Face cannot be null");

            // 2. Get the normal vector of the face, used for subsequent perpendicular vector calculations that may be needed.
            XYZ faceNormal = face.ComputeNormal(new UV(0.5, 0.5));

            // 3. 获取面的外轮廓
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            if (edgeLoops.Size == 0)
                throw new ArgumentException("Face has no valid edge loop", nameof(face));

            // Usually the first loop is outer contour
            EdgeArray outerLoop = edgeLoops.get_Item(0);

            // 4. Calculate the direction vector and length of each edge.
            List<XYZ> edgeDirections = new List<XYZ>();  // Store unit vector direction of each edge.
            List<double> edgeLengths = new List<double>(); // Store length of each edge

            foreach (Edge edge in outerLoop)
            {
                Curve curve = edge.AsCurve();
                XYZ startPoint = curve.GetEndPoint(0);
                XYZ endPoint = curve.GetEndPoint(1);

                // Calculate vector from start point to end point
                XYZ direction = endPoint - startPoint;
                double length = direction.GetLength();

                // Ignore edges that are too short (possibly due to coincident vertices or numerical precision issues)
                if (length > 1e-10)
                {
                    edgeDirections.Add(direction.Normalize());  // Store normalized direction vector
                    edgeLengths.Add(length);                    // Store edge length
                }
            }

            if (edgeDirections.Count < 4) // Ensure there is at least4条边
            {
                throw new ArgumentException("The provided face does not have enough edges to form a valid shape.", nameof(face));
            }

            // 5. Group edges with similar directions
            List<List<int>> directionGroups = new List<List<int>>();  // Store direction groups, with each group containing indices of edges.

            for (int i = 0; i < edgeDirections.Count; i++)
            {
                bool foundGroup = false;
                XYZ currentDirection = edgeDirections[i];

                // Try to add the current edge to an existing direction group.
                for (int j = 0; j < directionGroups.Count; j++)
                {
                    var group = directionGroups[j];
                    // Calculate the weighted average direction of the current group.
                    XYZ groupAvgDir = CalculateWeightedAverageDirection(group, edgeDirections, edgeLengths);

                    // Check if the current direction is similar to the average direction of the group (including positive and negative directions)
                    double dotProduct = Math.Abs(groupAvgDir.DotProduct(currentDirection));
                    if (dotProduct > 0.8) // 约30deviations within degrees are considered similar directions
                    {
                        group.Add(i);  // Add the index of the current edge to the direction group.
                        foundGroup = true;
                        break;
                    }
                }

                // If the current edge is not similar to any existing group, create a new group.
                if (!foundGroup)
                {
                    List<int> newGroup = new List<int> { i };
                    directionGroups.Add(newGroup);
                }
            }

            // 6. Calculate the total weight (sum of edge lengths) and average direction of each direction group.
            List<double> groupWeights = new List<double>();
            List<XYZ> groupDirections = new List<XYZ>();

            foreach (var group in directionGroups)
            {
                // Calculate the sum of lengths of all edges in the group.
                double totalLength = 0;
                foreach (int edgeIndex in group)
                {
                    totalLength += edgeLengths[edgeIndex];
                }
                groupWeights.Add(totalLength);

                // Calculate the weighted average direction of the group
                groupDirections.Add(CalculateWeightedAverageDirection(group, edgeDirections, edgeLengths));
            }

            // 7. Sort by weight and extract the primary direction.
            int[] sortedIndices = Enumerable.Range(0, groupDirections.Count)
                .OrderByDescending(i => groupWeights[i])
                .ToArray();

            // 8. 构造结果
            if (groupDirections.Count >= 2)
            {
                // At least two direction groups are present, and the two groups with the largest weights are taken as the primary and secondary directions.
                int primaryIndex = sortedIndices[0];
                int secondaryIndex = sortedIndices[1];

                return (
                    PrimaryDirection: groupDirections[primaryIndex],      // 主方向
                    SecondaryDirection: groupDirections[secondaryIndex]   // 次方向
                );
            }
            else if (groupDirections.Count == 1)
            {
                // Only one direction group exists, manually create a secondary direction perpendicular to the primary direction.
                XYZ primaryDirection = groupDirections[0];
                // Create a perpendicular vector using the cross product of the face normal vector and the primary direction.
                XYZ secondaryDirection = faceNormal.CrossProduct(primaryDirection).Normalize();

                return (
                    PrimaryDirection: primaryDirection,         // 主方向 
                    SecondaryDirection: secondaryDirection      // Manually constructed vertical secondary direction
                );
            }
            else
            {
                // Unable to extract a valid direction (rarely occurs).
                throw new InvalidOperationException("Unable to extract a valid direction from the face.");
            }
        }

        /// <summary>
        /// Calculate the weighted average direction of a group of edges based on edge length.
        /// </summary>
        /// <param name="edgeIndices">边的索引列表</param>
        /// <param name="directions">所有边的方向向量</param>
        /// <param name="lengths">Length of all edges</param>
        /// <returns>Normalized weighted average direction vector.</returns>
        public static XYZ CalculateWeightedAverageDirection(List<int> edgeIndices, List<XYZ> directions, List<double> lengths)
        {
            if (edgeIndices.Count == 0)
                return null;

            double sumX = 0, sumY = 0, sumZ = 0;
            XYZ referenceDir = directions[edgeIndices[0]];  // Use the first direction in the group as a reference.

            foreach (int i in edgeIndices)
            {
                XYZ currentDir = directions[i];

                // Calculate the dot product of the current direction and the reference direction to determine if a reversal is needed.
                double dot = referenceDir.DotProduct(currentDir);

                // If the directions are opposite (negative dot product), reverse the vector before calculating the contribution.
                // This ensures that vectors within the same group point in the same direction, avoiding mutual cancellation.
                double factor = (dot >= 0) ? lengths[i] : -lengths[i];

                // Accumulate vector components (with weights)
                sumX += currentDir.X * factor;
                sumY += currentDir.Y * factor;
                sumZ += currentDir.Z * factor;
            }

            // Create composite vector and normalize
            XYZ avgDir = new XYZ(sumX, sumY, sumZ);
            double magnitude = avgDir.GetLength();

            // Prevent zero vector
            if (magnitude < 1e-10)
                return referenceDir;  // 回退至Reference direction

            return avgDir.Normalize();  // Return normalized direction vector
        }

        /// <summary>
        /// Determine whether the three vectors conform to the right-hand rule and are strictly perpendicular to each other.
        /// </summary>
        /// <param name="thumb">拇指方向向量</param>
        /// <param name="indexFinger">Index finger direction vector</param>
        /// <param name="middleFinger">中指方向向量</param>
        /// <param name="tolerance">Tolerance for judgment, defaults to1e-6</param>
        /// <returns>Returns if the three vectors conform to the right-hand rule and are perpendicular to each other.true, otherwise returnfalse</returns>
        public static bool IsRightHandRuleCompliant(this XYZ thumb, XYZ indexFinger, XYZ middleFinger, double tolerance = 1e-6)
        {
            // Check if three vectors are mutually perpendicular (all dot products are close to0）
            double dotThumbIndex = Math.Abs(thumb.DotProduct(indexFinger));
            double dotThumbMiddle = Math.Abs(thumb.DotProduct(middleFinger));
            double dotIndexMiddle = Math.Abs(indexFinger.DotProduct(middleFinger));

            bool areOrthogonal = (dotThumbIndex <= tolerance) &&
                                  (dotThumbMiddle <= tolerance) &&
                                  (dotIndexMiddle <= tolerance);

            // Only check the right-hand rule when the three vectors are mutually perpendicular.
            if (!areOrthogonal)
                return false;

            // Calculate the dot product of the cross product vector and the thumb to determine if it conforms to the right-hand rule.
            XYZ crossProduct = indexFinger.CrossProduct(middleFinger);
            double rightHandTest = crossProduct.DotProduct(thumb);

            // A positive dot product indicates conformance to the right-hand rule.
            return rightHandTest > tolerance;
        }

        /// <summary>
        /// Generate the index finger direction that conforms to the right-hand rule based on the thumb and middle finger directions.
        /// </summary>
        /// <param name="thumb">拇指方向向量</param>
        /// <param name="middleFinger">中指方向向量</param>
        /// <param name="tolerance">Tolerance for perpendicularity judgment, defaults to1e-6</param>
        /// <returns>The generated index finger direction vector, or returns if the input vectors are not perpendicular.null</returns>
        public static XYZ GenerateIndexFinger(this XYZ thumb, XYZ middleFinger, double tolerance = 1e-6)
        {
            // First normalize input vector
            XYZ normalizedThumb = thumb.Normalize();
            XYZ normalizedMiddleFinger = middleFinger.Normalize();

            // Check if two vectors are perpendicular (dot product is close to0）
            double dotProduct = normalizedThumb.DotProduct(normalizedMiddleFinger);

            // If the absolute value of the dot product is greater than the tolerance, the vectors are not perpendicular.
            if (Math.Abs(dotProduct) > tolerance)
            {
                return null;
            }

            // Calculate the index finger direction via cross product and invert it.
            XYZ indexFinger = normalizedMiddleFinger.CrossProduct(normalizedThumb).Negate();

            // Return the normalized index finger direction vector.
            return indexFinger.Normalize();
        }

        /// <summary>
        /// Create or get a level with the specified height.
        /// </summary>
        /// <param name="doc">revit文档</param>
        /// <param name="elevation">Level height (ft）</param>
        /// <param name="levelName">Level name</param>
        /// <returns></returns>
        public static Level CreateOrGetLevel(this Document doc, double elevation, string levelName)
        {
            // First search for the existence of a level with the specified height.
            Level existingLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => Math.Abs(l.Elevation - elevation) < 0.1 / 304.8);

            if (existingLevel != null)
                return existingLevel;

            // Create new level
            Level newLevel = Level.Create(doc, elevation);
            // 设置Level name
            Level namesakeLevel = new FilteredElementCollector(doc)
                 .OfClass(typeof(Level))
                 .Cast<Level>()
                 .FirstOrDefault(l => l.Name == levelName);
            if (namesakeLevel != null)
            {
                levelName = $"{levelName}_{newLevel.Id.GetValue()}";
            }
            newLevel.Name = levelName;

            return newLevel;
        }

        /// <summary>
        /// Find the level closest to the given height.
        /// </summary>
        /// <param name="doc">当前Revit文档</param>
        /// <param name="height">Target height (Revitinternal units)</param>
        /// <returns>The level closest to the target height, or returns if there are no levels in the document.null</returns>
        public static Level FindNearestLevel(this Document doc, double height)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "文档不能为空");

            // Use directlyLINQQuery to get the nearest level
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => Math.Abs(level.Elevation - height))
                .FirstOrDefault();
        }

        ///// <summary>
        ///// Refresh view and add delay
        ///// </summary>
        //public static void Refresh(this Document doc, int waitingTime = 0, bool allowOperation = true)
        //{
        //    UIApplication uiApp = new UIApplication(doc.Application);
        //    UIDocument uiDoc = uiApp.ActiveUIDocument;

        //    // Check if document can be modified
        //    if (uiDoc.Document.IsModifiable)
        //    {
        //        // 更新模型
        //        uiDoc.Document.Regenerate();
        //    }
        //    // Update UI
        //    uiDoc.RefreshActiveView();

        //    // Delay wait
        //    if (waitingTime != 0)
        //    {
        //        System.Threading.Thread.Sleep(waitingTime);
        //    }

        //    // Allow user to perform unsafe operations
        //    if (allowOperation)
        //    {
        //        System.Windows.Forms.Application.DoEvents();
        //    }
        //}

        /// <summary>
        /// Save the specified message to the specified file on the desktop (overwrites the file by default)
        /// </summary>
        /// <param name="message">Message content to save</param>
        /// <param name="fileName">Target filename</param>
        public static void SaveToDesktop(this string message, string fileName = "temp.json", bool isAppend = false)
        {
            // 确保 logName Contains suffix
            if (!Path.HasExtension(fileName))
            {
                fileName += ".txt"; // 默认添加 .txt 后缀
            }

            // 获取桌面路径
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            // Combine full file path
            string filePath = Path.Combine(desktopPath, fileName);

            // Write to file (overwrite mode)
            using (StreamWriter sw = new StreamWriter(filePath, isAppend))
            {
                sw.WriteLine($"{message}");
            }
        }

    }
}
