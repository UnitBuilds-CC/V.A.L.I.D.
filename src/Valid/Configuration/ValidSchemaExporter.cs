using System.Reflection;
using System.Text.Json;

namespace Valid.Configuration;

/// <summary>
/// Exports V.A.L.I.D. configuration options as JSON Schema for LLM consumption.
/// </summary>
public static class ValidSchemaExporter
{
    /// <summary>
    /// Exports all VALID configuration options as a JSON Schema document.
    /// LLMs can use this to understand the configuration structure.
    /// </summary>
    /// <returns>JSON Schema string describing all configuration options.</returns>
    public static string ExportJsonSchema()
    {
        var schema = new Dictionary<string, object>
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["title"] = "V.A.L.I.D. Configuration",
            ["description"] = "Configuration options for the V.A.L.I.D. framework",
            ["type"] = "object",
            ["properties"] = new
            {
                Outbox = ExportTypeSchema<ValidOutboxOptions>("SQLite outbox configuration"),
                Registry = ExportTypeSchema<ValidRegistryOptions>("Object registry configuration")
            },
            ["examples"] = new[]
            {
                new
                {
                    Outbox = new
                    {
                        ConnectionString = "Data Source=valid_outbox.db",
                        EncryptionKey = "your-32-byte-key-here-1234567890",
                        DefaultBatchSize = 10
                    },
                    Registry = new
                    {
                        MaxCapacity = 10000
                    }
                }
            }
        };

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object ExportTypeSchema<T>(string description) where T : class
    {
        var type = typeof(T);
        var properties = new Dictionary<string, object>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propSchema = new Dictionary<string, object>
            {
                ["type"] = GetJsonSchemaType(prop.PropertyType),
                ["description"] = GetPropertyDescription(prop)
            };

            if (prop.PropertyType == typeof(string))
            {
                propSchema["default"] = "";
            }
            else if (prop.PropertyType == typeof(int))
            {
                propSchema["default"] = 0;
            }

            properties[prop.Name] = propSchema;
        }

        return new
        {
            type = "object",
            description,
            properties
        };
    }

    private static string GetJsonSchemaType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "integer";
        if (type == typeof(long)) return "integer";
        if (type == typeof(double)) return "number";
        if (type == typeof(decimal)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(DateTime)) return "string";
        return "string";
    }

    private static string GetPropertyDescription(PropertyInfo prop)
    {
        return prop.Name switch
        {
            "ConnectionString" => "SQLite connection string for the outbox database",
            "EncryptionKey" => "AES-GCM encryption key (32 bytes). Leave null to disable encryption.",
            "DefaultBatchSize" => "Default number of messages to dequeue in a single batch",
            "MaxCapacity" => "Maximum number of objects the registry can hold before cleanup",
            _ => prop.Name
        };
    }
}
