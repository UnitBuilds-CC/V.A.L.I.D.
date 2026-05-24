using System.Text.Json;
using System.Reflection;
using System.Runtime.Intrinsics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using ModelContextProtocol.Server;

namespace Valid.Mcp;

/// <summary>
/// MCP Tools for inspecting and manipulating VALID objects.
/// These tools are automatically discovered by the MCP server host.
/// </summary>
[McpServerToolType]
public sealed class ValidTools
{
    /// <summary>
    /// Lists all live ValidObject instances currently registered in the application.
    /// Returns a manifest of instance IDs and their class names.
    /// </summary>
    [McpServerTool, Description("Lists all live ValidObject instances registered in the application.")]
    public static string valid_list_instances()
    {
        var manifest = ValidObjectRegistry.GetManifest();
        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Inspects the full bitmask state of a live ValidObject instance.
    /// Returns DirtyFlags, ErrorFlags, BusyFlags, StateFlags, property values, and diagnostics.
    /// </summary>
    [McpServerTool, Description("Inspects the full bitmask state and property values of a live ValidObject instance by its ID.")]
    public static string valid_inspect_state(string instanceId)
    {
        var obj = ValidObjectRegistry.Get(instanceId);
        if (obj == null)
            return JsonSerializer.Serialize(new { error = $"Instance '{instanceId}' not found." });

        var result = new
        {
            InstanceId = instanceId,
            TypeName = obj.GetType().Name,
            IsDirty = obj.IsDirty,
            IsValid = obj.IsValid,
            DirtyFlags = obj.DirtyFlags.ToString("X"),
            ErrorFlags = obj.ErrorFlags.ToString("X"),
            BusyFlags = obj.BusyFlags.ToString("X"),
            StateFlags = obj.StateFlags.ToString("X"),
            Metadata = obj.GetValidMetadata(),
            Diagnostics = obj.GetDiagnostics().Select(d => new
            {
                d.Property,
                d.Message,
                d.Code,
                d.FixSuggestion
            }).ToArray()
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Reads the JSON schema and metadata of a ValidObject instance.
    /// Returns property names, types, bit indexes, and validation rules.
    /// </summary>
    [McpServerTool, Description("Returns the JSON schema metadata (properties, types, bit indexes, rules) of a ValidObject instance.")]
    public static string valid_read_schema(string instanceId)
    {
        var obj = ValidObjectRegistry.Get(instanceId);
        if (obj == null)
            return JsonSerializer.Serialize(new { error = $"Instance '{instanceId}' not found." });

        return obj.GetValidMetadata();
    }

    /// <summary>
    /// Mutates a property on a live ValidObject and returns the resulting state change.
    /// The AI can use this to test edge cases and verify bitmask behavior.
    /// </summary>
    [McpServerTool, Description("Sets a property value on a live ValidObject instance and returns the resulting bitmask state change.")]
    public static string valid_mutate_property(string instanceId, string propertyName, string jsonValue)
    {
        var obj = ValidObjectRegistry.Get(instanceId);
        if (obj == null)
            return JsonSerializer.Serialize(new { error = $"Instance '{instanceId}' not found." });

        var dirtyBefore = obj.DirtyFlags;
        var errorBefore = obj.ErrorFlags;

        try
        {
            obj.UpdatePropertyFromJson(propertyName, jsonValue);

            return JsonSerializer.Serialize(new
            {
                Success = true,
                PropertyName = propertyName,
                NewValue = obj.GetPropertyValue(propertyName)?.ToString(),
                DirtyBefore = dirtyBefore.ToString("X"),
                DirtyAfter = obj.DirtyFlags.ToString("X"),
                ErrorBefore = errorBefore.ToString("X"),
                ErrorAfter = obj.ErrorFlags.ToString("X"),
                BitIndex = obj.GetBitIndex(propertyName),
                IsNowDirty = obj.IsDirtyAt(obj.GetBitIndex(propertyName)),
                IsNowError = obj.HasErrorAt(obj.GetBitIndex(propertyName))
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                Success = false,
                Error = ex.Message,
                ExceptionType = ex.GetType().Name
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Forces a bitmask update pulse for a property without changing its value.
    /// Useful for testing VAVID HUD visibility and surgical UI updates.
    /// </summary>
    [McpServerTool, Description("Forces a surgical UI pulse for a property by temporarily flipping its dirty bit.")]
    public static string valid_pulse_property(string instanceId, string propertyName)
    {
        var obj = ValidObjectRegistry.Get(instanceId);
        if (obj == null)
            return JsonSerializer.Serialize(new { error = $"Instance '{instanceId}' not found." });

        try
        {
            var bitIndex = obj.GetBitIndex(propertyName);
            if (bitIndex < 0) return JsonSerializer.Serialize(new { error = $"Property '{propertyName}' not found." });

            // We use reflection to call OnPropertyChanged to trigger the surgical update via ValidComponentBase
            var method = typeof(ValidObjectBase).GetMethod("OnPropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(obj, new object[] { propertyName });

            return JsonSerializer.Serialize(new { Success = true, PropertyName = propertyName, Action = "PULSE" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Scans a live object for "Reference Hygiene" and "Performance" violations.
    /// Detects non-serializable types and potential serialization bottlenecks.
    /// </summary>
    [McpServerTool, Description("Scans an object for non-serializable properties (DataCloneErrors) and potential performance bottlenecks.")]
    public static string valid_check_hygiene(string instanceId)
    {
        var obj = ValidObjectRegistry.Get(instanceId);
        if (obj == null)
            return JsonSerializer.Serialize(new { error = $"Instance '{instanceId}' not found." });
 
        var issues = new List<string>();
        var performanceWarnings = new List<string>();
        var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var p in props)
        {
            var type = p.PropertyType;
            if (typeof(Delegate).IsAssignableFrom(type) || type.Name.Contains("DotNetObjectReference"))
            {
                issues.Add($"[HYGIENE] Property '{p.Name}' uses non-serializable type '{type.Name}'. This will break the VAVID bridge.");
            }

            if (type == typeof(string))
            {
                var val = p.GetValue(obj) as string;
                if (val?.Length > 10000)
                {
                    performanceWarnings.Add($"[PERFORMANCE] Property '{p.Name}' contains a very large string ({val.Length} chars). Consider using surgical WriteDelta to avoid string allocation overhead.");
                }
            }
        }
 
        return JsonSerializer.Serialize(new 
        { 
            InstanceId = instanceId,
            IsHygienic = issues.Count == 0,
            Issues = issues,
            PerformanceWarnings = performanceWarnings,
            Recommendation = issues.Count > 0 ? "Remove [ValidField] from non-serializable properties." : "Hygienic and ready for bridging."
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Verifies if the object respects framework limits (e.g., 128 property limit).
    /// </summary>
    [McpServerTool, Description("Verifies if the object respects framework property limits (Max 128).")]
    public static string valid_check_limits(string instanceId)
    {
        var obj = ValidObjectRegistry.Get(instanceId);
        if (obj == null)
            return JsonSerializer.Serialize(new { error = $"Instance '{instanceId}' not found." });

        var metadataJson = obj.GetValidMetadata();
        var metadata = JsonDocument.Parse(metadataJson);
        var propCount = metadata.RootElement.GetProperty("Properties").GetArrayLength();

        return JsonSerializer.Serialize(new
        {
            InstanceId = instanceId,
            PropertyCount = propCount,
            Limit = 128,
            Status = propCount <= 128 ? "OK" : "CRITICAL_OVERFLOW",
            Verdict = propCount <= 128 ? "Object is within safety limits." : "VALID001: Object exceeds 128-property limit. Bitmask overflow imminent!"
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Reports hardware acceleration status for the current runtime.
    /// </summary>
    [McpServerTool, Description("Reports if SIMD (Vector256) is hardware accelerated on the current host.")]
    public static string valid_get_hardware_info()
    {
        return JsonSerializer.Serialize(new
        {
            OS = Environment.OSVersion.ToString(),
            Is64Bit = Environment.Is64BitProcess,
            IsWasm = RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")),
            Vector256Accelerated = Vector256.IsHardwareAccelerated,
            Vector512Accelerated = Vector512.IsHardwareAccelerated,
            OptimizationLevel = Vector256.IsHardwareAccelerated ? "SIMD_AVX2" : "SCALAR_FALLBACK"
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Runs the Chaos Monkey fuzzer against a live ValidObject instance.
    /// Throws 1000 random boundary values at every property and reports any crashes.
    /// </summary>
    [McpServerTool, Description("Runs 1000 random boundary-value mutations against a live ValidObject to detect unhandled exceptions.")]
    public static string valid_run_fuzzer(string instanceId, int iterations = 1000)
    {
        var obj = ValidObjectRegistry.Get(instanceId);
        if (obj == null)
            return JsonSerializer.Serialize(new { error = $"Instance '{instanceId}' not found." });

        var metadataJson = obj.GetValidMetadata();
        var metadata = JsonDocument.Parse(metadataJson);
        var properties = metadata.RootElement.GetProperty("Properties").EnumerateArray().ToList();

        var random = new Random(42);
        var crashes = new List<object>();
        int survived = 0;

        for (int i = 0; i < iterations; i++)
        {
            foreach (var prop in properties)
            {
                var propName = prop.GetProperty("Name").GetString()!;
                var propType = prop.GetProperty("Type").GetString()!;
                var fuzzValue = GetFuzzValue(random, propType);

                try
                {
                    obj.UpdatePropertyFromJson(propName, fuzzValue);
                    survived++;
                }
                catch (Exception ex)
                {
                    crashes.Add(new
                    {
                        Iteration = i,
                        Property = propName,
                        FuzzValue = fuzzValue,
                        Error = ex.Message,
                        ExceptionType = ex.GetType().Name
                    });
                }
            }
        }

        return JsonSerializer.Serialize(new
        {
            InstanceId = instanceId,
            TotalMutations = survived + crashes.Count,
            Survived = survived,
            Crashed = crashes.Count,
            Crashes = crashes.Take(20) // Limit output
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Returns the delta JSON (only dirty properties) from a ValidObject.
    /// </summary>
    [McpServerTool, Description("Returns the JSON delta of all currently dirty properties on a ValidObject instance.")]
    public static string valid_get_delta(string instanceId)
    {
        var obj = ValidObjectRegistry.Get(instanceId);
        if (obj == null)
            return JsonSerializer.Serialize(new { error = $"Instance '{instanceId}' not found." });

        return obj.GetDeltaJson();
    }

    /// <summary>
    /// Returns the time-travel state history buffer for a ValidObject.
    /// </summary>
    [McpServerTool, Description("Returns the circular time-travel state history of a ValidObject instance (up to 16 snapshots).")]
    public static string valid_get_history(string instanceId)
    {
        var obj = ValidObjectRegistry.Get(instanceId);
        if (obj == null)
            return JsonSerializer.Serialize(new { error = $"Instance '{instanceId}' not found." });

        var history = obj.GetStateHistory()
            .Where(h => !string.IsNullOrEmpty(h))
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            InstanceId = instanceId,
            SnapshotCount = history.Length,
            Snapshots = history
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Performs zero-trust shadow validation on an object.
    /// Compares client-reported ErrorFlags against server-recalculated state.
    /// </summary>
    [McpServerTool, Description("Performs zero-trust shadow validation: re-calculates ErrorFlags server-side and compares to the client-reported state.")]
    public static string valid_shadow_validate(string instanceId)
    {
        var obj = ValidObjectRegistry.Get(instanceId);
        if (obj == null)
            return JsonSerializer.Serialize(new { error = $"Instance '{instanceId}' not found." });

        var trueState = obj.CalculateValidationState();
        var clientState = obj.ErrorFlags;
        var isTrusted = clientState == trueState;

        return JsonSerializer.Serialize(new
        {
            InstanceId = instanceId,
            ClientErrorFlags = clientState.ToString("X"),
            ServerErrorFlags = trueState.ToString("X"),
            IsTrusted = isTrusted,
            Verdict = isTrusted ? "PASS: Client state matches server recalculation." : "FAIL: Potential tampering detected!"
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GetFuzzValue(Random rnd, string typeName)
    {
        return typeName switch
        {
            "string" => rnd.Next(3) switch
            {
                0 => "null",
                1 => "\"\"",
                _ => $"\"{new string('X', rnd.Next(0, 50))}\""
            },
            "int" => rnd.Next(4) switch
            {
                0 => int.MinValue.ToString(),
                1 => int.MaxValue.ToString(),
                2 => "0",
                _ => rnd.Next().ToString()
            },
            "double" => rnd.Next(4) switch
            {
                0 => double.MinValue.ToString(),
                1 => double.MaxValue.ToString(),
                2 => "0.0",
                _ => rnd.NextDouble().ToString()
            },
            "decimal" => rnd.Next(3) switch
            {
                0 => "0",
                1 => "99999999.99",
                _ => ((decimal)rnd.NextDouble() * 1000).ToString()
            },
            "bool" => rnd.Next(2) == 0 ? "true" : "false",
            _ => "null"
        };
    }
}
