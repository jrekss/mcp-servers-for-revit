using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// Filter settings - 支持组合条件过滤
    /// </summary>
    public class FilterSetting
    {
        /// <summary>
        /// Get or set [elements] to be filtered Revit 内置Category name（如"OST_Walls"）。
        /// 如果为 null or null, then category filtering is not performed.
        /// </summary>
        [JsonProperty("filterCategory")]
        public string FilterCategory { get; set; } = null;
        /// <summary>
        /// Get or set [elements] to be filtered Revit 元素Type name（如"Wall"或"Autodesk.Revit.DB.Wall"）。
        /// 如果为 null or empty, no type filtering is performed.
        /// </summary>
        [JsonProperty("filterElementType")]
        public string FilterElementType { get; set; } = null;
        /// <summary>
        /// Gets or sets the family type to be filteredElementId值（FamilySymbol）。
        /// 如果为0or negative number, no family filtering is performed.
        /// Note: This filter only applies to element instances, not to type elements.
        /// </summary>
        [JsonProperty("filterFamilySymbolId")]
        public int FilterFamilySymbolId { get; set; } = -1;
        /// <summary>
        /// Gets or sets whether to include element types (e.g., wall types, door types, etc.)
        /// </summary>
        [JsonProperty("includeTypes")]
        public bool IncludeTypes { get; set; } = false;
        /// <summary>
        /// Gets or sets whether to include element instances (e.g., placed walls, doors, etc.)
        /// </summary>
        [JsonProperty("includeInstances")]
        public bool IncludeInstances { get; set; } = true;
        /// <summary>
        /// Gets or sets whether to return only elements that are visible in the current view.
        /// Note: This filter only applies to element instances, not to type elements.
        /// </summary>
        [JsonProperty("filterVisibleInCurrentView")]
        public bool FilterVisibleInCurrentView { get; set; }
        /// <summary>
        /// Gets or sets the minimum point coordinate for spatial range filtering. (单位：mm)
        /// If this value and [other value] are setBoundingBoxMax, elements intersecting with this bounding box will be filtered out.
        /// </summary>
        [JsonProperty("boundingBoxMin")]
        public JZPoint BoundingBoxMin { get; set; } = null;
        /// <summary>
        /// Gets or sets the maximum point coordinate for spatial range filtering. (单位：mm)
        /// If this value and [other value] are setBoundingBoxMin, elements intersecting with this bounding box will be filtered out.
        /// </summary>
        [JsonProperty("boundingBoxMax")]
        public JZPoint BoundingBoxMax { get; set; } = null;
        /// <summary>
        /// Maximum element count limit
        /// </summary>
        [JsonProperty("maxElements")]
        public int MaxElements { get; set; } = 50; 
        /// <summary>
        /// Validate the validity of the filter settings and check for potential conflicts.
        /// </summary>
        /// <returns>Returns if setting is validtrue, otherwise returnfalse</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = null;

            // Check if at least one element category is selected.
            if (!IncludeTypes && !IncludeInstances)
            {
                errorMessage = "过滤设置无效: Must contain at least one of element type or element instance.";
                return false;
            }

            // Check if at least one filter condition is specified.
            if (string.IsNullOrWhiteSpace(FilterCategory) &&
                string.IsNullOrWhiteSpace(FilterElementType) &&
                FilterFamilySymbolId <= 0)
            {
                errorMessage = "过滤设置无效: At least one filter condition must be specified(Category, element type, or family type)";
                return false;
            }

            // Check for conflicts between type elements and certain filters.
            if (IncludeTypes && !IncludeInstances)
            {
                List<string> invalidFilters = new List<string>();
                if (FilterFamilySymbolId > 0)
                    invalidFilters.Add("Family instance filter");
                if (FilterVisibleInCurrentView)
                    invalidFilters.Add("视图可见性过滤");
                if (invalidFilters.Count > 0)
                {
                    errorMessage = $"When filtering only type elements, the following filters do not apply:: {string.Join(", ", invalidFilters)}";
                    return false;
                }
            }
            // Check the validity of the spatial range filter.
            if (BoundingBoxMin != null && BoundingBoxMax != null)
            {
                // Ensure that the minimum point is less than or equal to the maximum point.
                if (BoundingBoxMin.X > BoundingBoxMax.X ||
                    BoundingBoxMin.Y > BoundingBoxMax.Y ||
                    BoundingBoxMin.Z > BoundingBoxMax.Z)
                {
                    errorMessage = "Invalid spatial boundary filter settings: The minimum point coordinate must be less than or equal to the maximum point coordinate.";
                    return false;
                }
            }
            else if (BoundingBoxMin != null || BoundingBoxMax != null)
            {
                errorMessage = "Invalid spatial boundary filter settings: Both minimum and maximum point coordinates must be set.";
                return false;
            }
            return true;
        }
    }
}
