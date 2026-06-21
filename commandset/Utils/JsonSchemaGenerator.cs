using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Utils
{
    public static class JsonSchemaGenerator
    {
        /// <summary>
        /// Generate and convert specified type of JSON Schema
        /// </summary>
        /// <typeparam name="T">要生成 Schema 的类型</typeparam>
        /// <param name="mainPropertyName">转换后 Schema 中的主要属性名称</param>
        /// <returns>Converted JSON Schema 字符串</returns>
        public static string GenerateTransformedSchema<T>(string mainPropertyName)
        {
            return GenerateTransformedSchema<T>(mainPropertyName, false);
        }

        /// <summary>
        /// Generate and convert specified type of JSON Schema，支持 ThinkingProcess 属性
        /// </summary>
        /// <typeparam name="T">要生成 Schema 的类型</typeparam>
        /// <param name="mainPropertyName">转换后 Schema 中的主要属性名称</param>
        /// <param name="includeThinkingProcess">Whether to add ThinkingProcess 属性</param>
        /// <returns>Converted JSON Schema 字符串</returns>
        public static string GenerateTransformedSchema<T>(string mainPropertyName, bool includeThinkingProcess)
        {
            if (string.IsNullOrWhiteSpace(mainPropertyName))
                throw new ArgumentException("Main property name cannot be null or empty.", nameof(mainPropertyName));

            // 创建根 Schema
            JObject rootSchema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject(),
                ["required"] = new JArray(),
                ["additionalProperties"] = false
            };

            // 如果需要添加 ThinkingProcess 属性
            if (includeThinkingProcess)
            {
                AddProperty(rootSchema, "ThinkingProcess", new JObject { ["type"] = "string" }, true);
            }

            // 生成目标属性的 Schema
            JObject mainPropertySchema = GenerateSchema(typeof(T));
            AddProperty(rootSchema, mainPropertyName, mainPropertySchema, true);

            // Recursively add for all objects "additionalProperties": false
            AddAdditionalPropertiesFalse(rootSchema);

            // 返回格式化后的 JSON Schema
            return JsonConvert.SerializeObject(rootSchema, Formatting.Indented);
        }

        /// <summary>
        /// Recursively generate specified type of JSON Schema
        /// </summary>
        private static JObject GenerateSchema(Type type)
        {
            if (type == typeof(string)) return new JObject { ["type"] = "string" };
            if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return new JObject { ["type"] = "integer" };
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return new JObject { ["type"] = "number" };
            if (type == typeof(bool)) return new JObject { ["type"] = "boolean" };

            // Priority processing Dictionary 类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return HandleDictionary(type);

            // Handle array or collection types
            if (type.IsArray || (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType))
            {
                Type itemType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = GenerateSchema(itemType)
                };
            }

            // Process class type
            if (type.IsClass)
            {
                var schema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject(),
                    ["required"] = new JArray(),
                    ["additionalProperties"] = false
                };

                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    AddProperty(schema, prop.Name, GenerateSchema(prop.PropertyType), isRequired: true);
                }
                return schema;
            }

            // Handled as string by default
            return new JObject { ["type"] = "string" };
        }

        /// <summary>
        /// Specifically handle Dictionary<string, TValue> 类型，确保键是 string type, and handle value type correctly
        /// </summary>
        private static JObject HandleDictionary(Type type)
        {
            Type keyType = type.GetGenericArguments()[0];
            Type valueType = type.GetGenericArguments()[1];

            if (keyType != typeof(string))
            {
                throw new NotSupportedException("JSON Schema only supports dictionaries with string keys.");
            }

            return new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = GenerateSchema(valueType)
            };
        }

        /// <summary>
        /// 为 Schema Add attribute
        /// </summary>
        private static void AddProperty(JObject schema, string propertyName, JToken propertySchema, bool isRequired)
        {
            ((JObject)schema["properties"]).Add(propertyName, propertySchema);

            if (isRequired)
            {
                ((JArray)schema["required"]).Add(propertyName);
            }
        }

        /// <summary>
        /// 为包含 "required" Recursively add objects with properties "additionalProperties": false
        /// </summary>
        private static void AddAdditionalPropertiesFalse(JToken token)
        {
            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                if (obj["required"] != null && obj["additionalProperties"] == null)
                {
                    obj["additionalProperties"] = false;
                }

                foreach (var property in obj.Properties())
                {
                    AddAdditionalPropertiesFalse(property.Value);
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in (JArray)token)
                {
                    AddAdditionalPropertiesFalse(item);
                }
            }
        }
    }
}
