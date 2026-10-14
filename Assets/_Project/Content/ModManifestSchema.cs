using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PG.Content
{
    // Validator for the JSON Schema keywords used by mod_manifest.schema.json; no additional package is required.
    // It intentionally fails closed if the bundled schema adds an unsupported keyword.
    public static class ModManifestSchema
    {
        static readonly HashSet<string> ValidationKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "type", "required", "properties", "additionalProperties", "pattern", "minLength",
            "const", "items", "uniqueItems"
        };

        static readonly HashSet<string> AnnotationKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "$schema", "$id", "title", "description", "default"
        };

        public static bool IsSchemaSupported(string schemaJson, out string error)
        {
            error = null;
            try
            {
                var schema = JToken.Parse(schemaJson);
                if (!(schema is JObject)) { error = "schema root must be an object"; return false; }
                return CheckSchemaNode(schema, "$", out error);
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException)
            {
                error = "schema JSON is invalid: " + e.Message;
                return false;
            }
        }

        public static bool TryValidate(string instanceJson, string schemaJson, out string[] errors)
        {
            var issues = new List<string>();
            JToken instance;
            JToken schema;
            try { instance = JToken.Parse(instanceJson); }
            catch (Exception e) when (e is JsonException || e is ArgumentException)
            {
                errors = new[] { "manifest JSON is invalid: " + e.Message };
                return false;
            }
            try { schema = JToken.Parse(schemaJson); }
            catch (Exception e) when (e is JsonException || e is ArgumentException)
            {
                errors = new[] { "schema JSON is invalid: " + e.Message };
                return false;
            }

            if (!CheckSchemaNode(schema, "$", out string schemaError))
                issues.Add("unsupported or invalid bundled schema: " + schemaError);
            else
                ValidateNode(instance, (JObject)schema, "$", issues);

            errors = issues.ToArray();
            return errors.Length == 0;
        }

        static bool CheckSchemaNode(JToken token, string path, out string error)
        {
            error = null;
            if (!(token is JObject obj)) { error = path + " must be an object"; return false; }
            foreach (var property in obj.Properties())
            {
                if (!ValidationKeywords.Contains(property.Name) && !AnnotationKeywords.Contains(property.Name))
                {
                    error = path + " uses unsupported keyword '" + property.Name + "'";
                    return false;
                }
                if (property.Name == "type" && (property.Value.Type != JTokenType.String || !IsKnownType((string)property.Value)))
                {
                    error = path + ".type must be a supported string type";
                    return false;
                }
                if (property.Name == "required")
                {
                    if (!(property.Value is JArray required)) { error = path + ".required must be an array"; return false; }
                    foreach (var name in required)
                        if (name.Type != JTokenType.String) { error = path + ".required entries must be strings"; return false; }
                }
                if (property.Name == "additionalProperties" && property.Value.Type != JTokenType.Boolean)
                {
                    error = path + ".additionalProperties must be boolean";
                    return false;
                }
                if (property.Name == "uniqueItems" && property.Value.Type != JTokenType.Boolean)
                {
                    error = path + ".uniqueItems must be boolean";
                    return false;
                }
                if (property.Name == "minLength" && (property.Value.Type != JTokenType.Integer || (long)property.Value < 0))
                {
                    error = path + ".minLength must be a non-negative integer";
                    return false;
                }
                if (property.Name == "pattern")
                {
                    if (property.Value.Type != JTokenType.String) { error = path + ".pattern must be a string"; return false; }
                    try { _ = new Regex((string)property.Value, RegexOptions.CultureInvariant); }
                    catch (ArgumentException) { error = path + ".pattern is not a valid regex"; return false; }
                }
                if (property.Name == "properties")
                {
                    if (!(property.Value is JObject properties)) { error = path + ".properties must be an object"; return false; }
                    foreach (var child in properties.Properties())
                        if (!CheckSchemaNode(child.Value, path + ".properties." + child.Name, out error)) return false;
                }
                else if (property.Name == "items" && !CheckSchemaNode(property.Value, path + ".items", out error)) return false;
            }
            return true;
        }

        static void ValidateNode(JToken value, JObject schema, string path, List<string> errors)
        {
            if (schema.TryGetValue("type", out var typeToken) && typeToken.Type == JTokenType.String && !MatchesType(value, (string)typeToken))
            {
                errors.Add(path + " must be " + (string)typeToken);
                return;
            }

            if (schema.TryGetValue("const", out var expected) && !JToken.DeepEquals(value, expected))
                errors.Add(path + " must equal " + expected.ToString(Formatting.None));

            if (value is JObject obj)
            {
                if (schema.TryGetValue("required", out var required) && required is JArray names)
                    foreach (var name in names)
                        if (name.Type == JTokenType.String && !obj.ContainsKey((string)name)) errors.Add(path + " is missing required property '" + (string)name + "'");

                schema.TryGetValue("properties", out var propertySchemas);
                foreach (var property in obj.Properties())
                {
                    if (propertySchemas is JObject known && known.TryGetValue(property.Name, out var childSchema))
                    {
                        if (childSchema is JObject child) ValidateNode(property.Value, child, path + "." + property.Name, errors);
                    }
                    else if (schema.TryGetValue("additionalProperties", out var additional) && additional.Type == JTokenType.Boolean && !(bool)additional)
                        errors.Add(path + " contains unknown property '" + property.Name + "'");
                }
            }

            if (value is JArray array)
            {
                if (schema.TryGetValue("items", out var itemSchema) && itemSchema is JObject itemObject)
                    for (int i = 0; i < array.Count; i++) ValidateNode(array[i], itemObject, path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", errors);
                if (schema.TryGetValue("uniqueItems", out var unique) && unique.Type == JTokenType.Boolean && (bool)unique)
                    for (int i = 0; i < array.Count; i++)
                        for (int j = i + 1; j < array.Count; j++)
                            if (JToken.DeepEquals(array[i], array[j])) { errors.Add(path + " items must be unique"); i = array.Count; break; }
            }

            if (value.Type == JTokenType.String)
            {
                string text = (string)value;
                if (schema.TryGetValue("minLength", out var minLength) && minLength.Type == JTokenType.Integer && UnicodeLength(text) < (int)minLength)
                    errors.Add(path + " must contain at least " + minLength + " character(s)");
                if (schema.TryGetValue("pattern", out var pattern) && pattern.Type == JTokenType.String)
                {
                    try
                    {
                        if (!Regex.IsMatch(text, (string)pattern, RegexOptions.CultureInvariant)) errors.Add(path + " does not match required pattern");
                    }
                    catch (ArgumentException) { errors.Add("bundled schema has an invalid regex at " + path); }
                }
            }
        }

        static bool MatchesType(JToken value, string type) => type switch
        {
            "object" => value.Type == JTokenType.Object,
            "array" => value.Type == JTokenType.Array,
            "string" => value.Type == JTokenType.String,
            "integer" => value.Type == JTokenType.Integer,
            "number" => value.Type == JTokenType.Integer || value.Type == JTokenType.Float,
            "boolean" => value.Type == JTokenType.Boolean,
            "null" => value.Type == JTokenType.Null,
            _ => false,
        };

        static bool IsKnownType(string type) => type == "object" || type == "array" || type == "string" ||
            type == "integer" || type == "number" || type == "boolean" || type == "null";

        static int UnicodeLength(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++, count++)
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
            return count;
        }
    }
}
