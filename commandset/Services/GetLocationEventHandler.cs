using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class GetLocationEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public List<ElementLocationInfo> ResultLocations { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public List<long> ElementIds { get; set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                var locations = new List<ElementLocationInfo>();

                foreach (var id in ElementIds)
                {
#if REVIT2024_OR_GREATER
                    var idObj = new ElementId(id);
#else
                    var idObj = new ElementId((int)id);
#endif
                    var element = doc.GetElement(idObj);
                    if (element == null)
                    {
                        locations.Add(new ElementLocationInfo { ElementId = id, LocationType = "NotFound" });
                        continue;
                    }

                    var loc = element.Location;
                    if (loc == null)
                    {
                        locations.Add(new ElementLocationInfo { ElementId = id, LocationType = "None" });
                        continue;
                    }

                    if (loc is LocationPoint locPoint)
                    {
                        locations.Add(new ElementLocationInfo
                        {
                            ElementId = id,
                            LocationType = "Point",
                            Point = new LocationPointInfo
                            {
                                X = locPoint.Point.X,
                                Y = locPoint.Point.Y,
                                Z = locPoint.Point.Z
                            }
                        });
                    }
                    else if (loc is LocationCurve locCurve)
                    {
                        var curve = locCurve.Curve;
                        locations.Add(new ElementLocationInfo
                        {
                            ElementId = id,
                            LocationType = "Curve",
                            Curve = new LocationCurveInfo
                            {
                                Start = new LocationPointInfo
                                {
                                    X = curve.GetEndPoint(0).X,
                                    Y = curve.GetEndPoint(0).Y,
                                    Z = curve.GetEndPoint(0).Z
                                },
                                End = new LocationPointInfo
                                {
                                    X = curve.GetEndPoint(1).X,
                                    Y = curve.GetEndPoint(1).Y,
                                    Z = curve.GetEndPoint(1).Z
                                }
                            }
                        });
                    }
                    else
                    {
                        locations.Add(new ElementLocationInfo { ElementId = id, LocationType = "Unknown" });
                    }
                }

                ResultLocations = locations;
            }
            catch (Exception)
            {
                ResultLocations = new List<ElementLocationInfo>();
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "Get Location for Element IDs";
        }
    }

    public class ElementLocationInfo
    {
        public long ElementId { get; set; }
        public string LocationType { get; set; }
        public LocationPointInfo Point { get; set; }
        public LocationCurveInfo Curve { get; set; }
    }

    public class LocationPointInfo
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class LocationCurveInfo
    {
        public LocationPointInfo Start { get; set; }
        public LocationPointInfo End { get; set; }
    }
}
