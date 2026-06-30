using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class GetParametersEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public List<RevitParameterInfo> ResultParameters { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public long ElementId { get; set; }

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

#if REVIT2024_OR_GREATER
                var idObj = new ElementId(ElementId);
#else
                var idObj = new ElementId((int)ElementId);
#endif

                var element = doc.GetElement(idObj);
                if (element == null)
                {
                    ResultParameters = new List<RevitParameterInfo>();
                    return;
                }

                var parameters = new List<RevitParameterInfo>();
                foreach (Parameter p in element.Parameters)
                {
                    if (p == null || !p.HasValue) continue;

                    string valStr = p.AsValueString();
                    if (string.IsNullOrEmpty(valStr))
                    {
                        switch (p.StorageType)
                        {
                            case StorageType.Integer:
                                valStr = p.AsInteger().ToString();
                                break;
                            case StorageType.Double:
                                valStr = p.AsDouble().ToString();
                                break;
                            case StorageType.String:
                                valStr = p.AsString();
                                break;
                            case StorageType.ElementId:
                                var id = p.AsElementId();
#if REVIT2024_OR_GREATER
                                valStr = id.Value.ToString();
#else
                                valStr = id.IntegerValue.ToString();
#endif
                                break;
                        }
                    }

                    string groupStr = GetParameterGroupName(p);

                    parameters.Add(new RevitParameterInfo
                    {
#if REVIT2024_OR_GREATER
                        Id = p.Id.Value,
#else
                        Id = p.Id.IntegerValue,
#endif
                        Name = p.Definition.Name,
                        Value = valStr ?? string.Empty,
                        StorageType = p.StorageType.ToString(),
                        IsReadOnly = p.IsReadOnly,
                        Group = groupStr
                    });
                }

                ResultParameters = parameters.OrderBy(p => p.Name).ToList();
            }
            catch (Exception)
            {
                ResultParameters = new List<RevitParameterInfo>();
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        private string GetParameterGroupName(Parameter p)
        {
            if (p == null || p.Definition == null) return string.Empty;

            try
            {
                var getGroupMethod = p.Definition.GetType().GetMethod("GetGroup");
                if (getGroupMethod != null)
                {
                    var forgeTypeId = getGroupMethod.Invoke(p.Definition, null);
                    if (forgeTypeId != null)
                    {
                        var typeIdProperty = forgeTypeId.GetType().GetProperty("TypeId");
                        if (typeIdProperty != null)
                        {
                            return typeIdProperty.GetValue(forgeTypeId) as string ?? string.Empty;
                        }
                    }
                }
            }
            catch { }

            try
            {
                if (p.Definition is InternalDefinition internalDef)
                {
                    var getGroupMethod = typeof(InternalDefinition).GetMethod("GetGroup");
                    if (getGroupMethod != null)
                    {
                        var forgeTypeId = getGroupMethod.Invoke(internalDef, null);
                        if (forgeTypeId != null)
                        {
                            var typeIdProperty = forgeTypeId.GetType().GetProperty("TypeId");
                            if (typeIdProperty != null)
                            {
                                return typeIdProperty.GetValue(forgeTypeId) as string ?? string.Empty;
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                var parameterGroupProperty = p.Definition.GetType().GetProperty("ParameterGroup");
                if (parameterGroupProperty != null)
                {
                    var group = parameterGroupProperty.GetValue(p.Definition);
                    if (group != null)
                    {
                        return group.ToString();
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        public string GetName()
        {
            return "Get Parameters by Element ID";
        }
    }
}
