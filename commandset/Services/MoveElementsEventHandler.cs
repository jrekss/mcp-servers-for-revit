using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class MoveElementsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public JZResult Result { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public List<long> ElementIds { get; set; }
        public double dX { get; set; }
        public double dY { get; set; }
        public double dZ { get; set; }

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
                var idList = new List<ElementId>();

                foreach (var id in ElementIds)
                {
#if REVIT2024_OR_GREATER
                    var idObj = new ElementId(id);
#else
                    var idObj = new ElementId((int)id);
#endif
                    if (doc.GetElement(idObj) != null)
                    {
                        idList.Add(idObj);
                    }
                }

                if (idList.Count == 0)
                {
                    Result = new JZResult
                    {
                        Status = "Failed",
                        Message = "No valid elements found to move."
                    };
                    return;
                }

                using (var trans = new Transaction(doc, "Move Elements via MCP"))
                {
                    trans.Start();

                    var vector = new XYZ(dX, dY, dZ);
                    ElementTransformUtils.MoveElements(doc, idList, vector);

                    trans.Commit();
                }

                Result = new JZResult
                {
                    Status = "Success",
                    Message = $"Successfully moved {idList.Count} elements by vector ({dX}, {dY}, {dZ})."
                };
            }
            catch (Exception ex)
            {
                Result = new JZResult
                {
                    Status = "Error",
                    Message = ex.Message
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "Move Elements";
        }
    }
}
