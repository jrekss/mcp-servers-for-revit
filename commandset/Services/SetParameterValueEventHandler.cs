using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class SetParameterValueEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public JZResult Result { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public List<long> ElementIds { get; set; }
        public string ParameterNameOrId { get; set; }
        public string Value { get; set; }

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
                int successCount = 0;
                var errors = new List<string>();

                using (var trans = new Transaction(doc, "Set Parameter Value via MCP"))
                {
                    trans.Start();

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
                            errors.Add($"Element with ID {id} not found.");
                            continue;
                        }

                        var param = FindParameter(element, ParameterNameOrId);
                        if (param == null)
                        {
                            errors.Add($"Parameter '{ParameterNameOrId}' not found on element {id}.");
                            continue;
                        }

                        if (param.IsReadOnly)
                        {
                            errors.Add($"Parameter '{ParameterNameOrId}' is read-only on element {id}.");
                            continue;
                        }

                        if (SetParameterValue(param, Value))
                        {
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"Failed to parse or set value '{Value}' for parameter '{ParameterNameOrId}' on element {id}.");
                        }
                    }

                    trans.Commit();
                }

                Result = new JZResult
                {
                    Status = errors.Count == 0 ? "Success" : (successCount > 0 ? "PartialSuccess" : "Failed"),
                    Message = $"Successfully set parameter on {successCount} of {ElementIds.Count} elements.",
                    Data = new
                    {
                        successCount,
                        totalCount = ElementIds.Count,
                        errors
                    }
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

        private Parameter FindParameter(Element element, string paramNameOrId)
        {
            if (element == null) return null;

            if (int.TryParse(paramNameOrId, out int intId))
            {
                try
                {
                    var p = element.get_Parameter((BuiltInParameter)intId);
                    if (p != null) return p;
                }
                catch { }
            }

            var param = element.LookupParameter(paramNameOrId);
            if (param != null) return param;

            foreach (Parameter p in element.Parameters)
            {
                if (p != null && string.Equals(p.Definition.Name, paramNameOrId, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            var typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                var typeElement = element.Document.GetElement(typeId);
                if (typeElement != null)
                {
                    var typeParam = typeElement.LookupParameter(paramNameOrId);
                    if (typeParam != null) return typeParam;

                    foreach (Parameter p in typeElement.Parameters)
                    {
                        if (p != null && string.Equals(p.Definition.Name, paramNameOrId, StringComparison.OrdinalIgnoreCase))
                        {
                            return p;
                        }
                    }
                }
            }

            return null;
        }

        private bool SetParameterValue(Parameter p, string value)
        {
            if (p == null || p.IsReadOnly) return false;

            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.Set(value);
                case StorageType.Integer:
                    if (int.TryParse(value, out int intVal))
                    {
                        return p.Set(intVal);
                    }
                    return false;
                case StorageType.Double:
                    if (double.TryParse(value, out double doubleVal))
                    {
                        return p.Set(doubleVal);
                    }
                    return false;
                case StorageType.ElementId:
#if REVIT2024_OR_GREATER
                    if (long.TryParse(value, out long longId))
                    {
                        return p.Set(new ElementId(longId));
                    }
#else
                    if (int.TryParse(value, out int intId))
                    {
                        return p.Set(new ElementId(intId));
                    }
#endif
                    return false;
                default:
                    return false;
            }
        }

        public string GetName()
        {
            return "Set Parameter Value";
        }
    }

    public class JZResult
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}
