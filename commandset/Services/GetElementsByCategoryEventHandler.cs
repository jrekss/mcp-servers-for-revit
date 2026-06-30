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
    public class GetElementsByCategoryEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public List<ElementInfo> ResultElements { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public List<string> CategoryNamesOrIds { get; set; }

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
                var resolvedCategories = ResolveCategories(doc, CategoryNamesOrIds);

                if (resolvedCategories.Count == 0)
                {
                    ResultElements = new List<ElementInfo>();
                    return;
                }

                var collector = new FilteredElementCollector(doc);
                var categoryFilters = new List<ElementFilter>();

                foreach (var cat in resolvedCategories)
                {
                    categoryFilters.Add(new ElementCategoryFilter(cat.Id));
                }

                ElementFilter finalFilter = null;
                if (categoryFilters.Count == 1)
                {
                    finalFilter = categoryFilters[0];
                }
                else if (categoryFilters.Count > 1)
                {
                    finalFilter = new LogicalOrFilter(categoryFilters);
                }

                if (finalFilter != null)
                {
                    collector.WherePasses(finalFilter).WhereElementIsNotElementType();
                }

                ResultElements = collector.Select(element => new ElementInfo
                {
#if REVIT2024_OR_GREATER
                    Id = element.Id.Value,
#else
                    Id = element.Id.IntegerValue,
#endif
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    Category = element.Category?.Name
                }).ToList();
            }
            catch (Exception)
            {
                ResultElements = new List<ElementInfo>();
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        private List<Category> ResolveCategories(Document doc, List<string> namesOrIds)
        {
            var resolved = new List<Category>();
            foreach (var entry in namesOrIds)
            {
                if (int.TryParse(entry, out int idVal))
                {
                    try
                    {
#if REVIT2024_OR_GREATER
                        var idObj = new ElementId(idVal);
#else
                        var idObj = new ElementId(idVal);
#endif
                        var cat = Category.GetCategory(doc, idObj);
                        if (cat != null)
                        {
                            resolved.Add(cat);
                            continue;
                        }
                    }
                    catch { }
                }

                foreach (Category c in doc.Settings.Categories)
                {
                    if (string.Equals(c.Name, entry, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.BuiltInCategory.ToString(), entry, StringComparison.OrdinalIgnoreCase))
                    {
                        resolved.Add(c);
                        break;
                    }
                }
            }
            return resolved;
        }

        public string GetName()
        {
            return "Get Elements by Category";
        }
    }
}
