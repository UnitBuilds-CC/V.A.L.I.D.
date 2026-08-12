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

    /// <summary>
    /// Returns comprehensive API documentation for the V.A.L.I.D. framework.
    /// LLMs can use this to understand the framework's capabilities and write user guides.
    /// </summary>
    [McpServerTool, Description("Returns API documentation for V.A.L.I.D. framework types, attributes, and usage patterns. LLMs use this to write user guides.")]
    public static string valid_get_api_docs(string? typeName = null)
    {
        var docs = new
        {
            Framework = "V.A.L.I.D. (Vectorized Asynchronous Logic & Intelligent Diagnostics)",
            Version = "3.1.0",
            Description = "A .NET 8 bitmask-driven business logic framework using UInt128 for state tracking.",
            
            CoreTypes = new object[]
            {
                new
                {
                    Name = "ValidObjectBase",
                    Description = "Abstract base class for all VALID objects. Inherits UInt128-backed dirty/busy/error tracking.",
                    Usage = "Inherit from this class and mark with [ValidObject].",
                    Example = "[ValidObject]\npublic partial class Customer : ValidObjectBase {\n    [ValidProperty] public string Name { get; set; } = \"\";\n    [ValidProperty] public int Age { get; set; }\n}",
                    Limits = "Maximum 128 properties per object (UInt128 bitmask limit)."
                },
                new
                {
                    Name = "IValidObject",
                    Description = "Core interface defining the bitmask state management contract.",
                    KeyProperties = "ValidId, DirtyFlags, BusyFlags, ErrorFlags, StateFlags",
                    KeyMethods = "GetDeltaJson(), UpdatePropertyFromJson(), GetValidMetadata(), CalculateValidationState()"
                }
            },
            
            Attributes = new[]
            {
                new
                {
                    Name = "[ValidObject]",
                    Target = "Class",
                    Description = "Marks a class for source generator processing. Class must be partial and inherit ValidObjectBase.",
                    Example = "[ValidObject]\npublic partial class Product : ValidObjectBase { }"
                },
                new
                {
                    Name = "[ValidProperty]",
                    Target = "Property",
                    Description = "Includes a property in bitmask state tracking. Each property gets a unique bit index (0-127).",
                    Example = "[ValidProperty] public string Name { get; set; } = \"\";"
                },
                new
                {
                    Name = "[ValidField]",
                    Target = "Private Field",
                    Description = "Marks a private field for auto-generated proxy property. Eliminates DTO boilerplate.",
                    Example = "[ValidField] private string _name = \"\"; // Generates: public string Name { get; set; }"
                },
                new
                {
                    Name = "[Required]",
                    Target = "Property",
                    Description = "Validates that a string property is non-null and non-whitespace.",
                    Example = "[ValidProperty, Required(\"Name is required\", \"EMP-001\")]\npublic string Name { get; set; } = \"\";"
                },
                new
                {
                    Name = "[Range]",
                    Target = "Property",
                    Description = "Validates that a numeric value is between Min and Max (inclusive).",
                    Example = "[ValidProperty, Range(18, 65, \"Age must be 18-65\", \"EMP-002\")]\npublic int Age { get; set; }"
                },
                new
                {
                    Name = "[StringLength]",
                    Target = "Property",
                    Description = "Constrains string length between MinimumLength and MaximumLength.",
                    Example = "[ValidProperty, StringLength(50, MinimumLength = 3)]\npublic string Name { get; set; } = \"\";"
                }
            },
            
            Capabilities = new[]
            {
                "Dirty tracking — which properties changed since last sync",
                "Busy tracking — which properties are in async operations",
                "Error tracking — which properties have validation errors",
                "State tracking — object lifecycle (New, Deleted, etc.)",
                "Time-travel debugging — circular history buffer (16 snapshots)",
                "Shadow validation — server-side verification of client state",
                "CRDT merging — conflict-free replicated data types for offline-first",
                "SQLite outbox — encrypted, hash-chained event persistence",
                "Blazor integration — surgical UI updates via BitPulse events",
                "Web Worker bridge — JavaScript interop via unmanaged memory slab"
            },
            
            QuickStart = "1. Add [ValidObject] to your class\n2. Inherit from ValidObjectBase\n3. Add [ValidProperty] to each tracked property\n4. Optionally add validation attributes ([Required], [Range], [StringLength])\n5. Changes are automatically tracked in DirtyFlags/BusyFlags/ErrorFlags"
        };

        return JsonSerializer.Serialize(docs, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Initializes the visual guide system by launching MCP-Lite browser automation and the Blazor sample app.
    /// LLMs use this to capture screenshots and create annotated visual guides.
    /// </summary>
    [McpServerTool, Description("Initializes the visual guide system (MCP-Lite browser + Blazor sample app) for capturing screenshots and creating annotated guides.")]
    public static async Task<string> valid_initialize_visual_guides()
    {
        return await VisualGuideOrchestrator.InitializeAsync();
    }

    /// <summary>
    /// Captures a screenshot of the current browser viewport showing the Blazor sample app.
    /// Use after navigating to a page with valid_navigate_visual_guide.
    /// </summary>
    [McpServerTool, Description("Captures a screenshot of the current browser viewport. Returns the file path to the saved PNG image.")]
    public static async Task<string> valid_capture_screenshot(string? outputPath = null)
    {
        return await VisualGuideOrchestrator.CaptureScreenshotAsync(outputPath);
    }

    /// <summary>
    /// Captures a screenshot of a specific DOM element (identified by AOM node ID).
    /// Use valid_get_aom to find node IDs for specific fields/properties.
    /// </summary>
    [McpServerTool, Description("Captures a cropped screenshot of a specific DOM element by its AOM node ID.")]
    public static async Task<string> valid_capture_node_screenshot(string nodeId, string? outputPath = null)
    {
        return await VisualGuideOrchestrator.CaptureNodeScreenshotAsync(nodeId, outputPath);
    }

    /// <summary>
    /// Navigates to a specific page in the Blazor sample app for visual guide capture.
    /// </summary>
    [McpServerTool, Description("Navigates to a page in the Blazor sample app (e.g., 'customer', 'product', 'order').")]
    public static async Task<string> valid_navigate_visual_guide(string path)
    {
        return await VisualGuideOrchestrator.NavigateAsync(path);
    }

    /// <summary>
    /// Shuts down the visual guide system (MCP-Lite and Blazor app).
    /// </summary>
    [McpServerTool, Description("Shuts down the visual guide system after capturing all needed screenshots.")]
    public static string valid_shutdown_visual_guides()
    {
        return VisualGuideOrchestrator.Shutdown();
    }

    // ==================== Workflow Management ====================

    /// <summary>
    /// Starts recording a workflow for later playback and E2E test generation.
    /// </summary>
    [McpServerTool, Description("Starts recording a workflow. Use valid_record_workflow_step to add steps, then valid_stop_workflow_recording to save.")]
    public static string valid_start_workflow_recording(string workflowName, string? description = null)
    {
        return WorkflowManager.StartRecording(workflowName, description);
    }

    /// <summary>
    /// Records a single step in the current workflow.
    /// </summary>
    [McpServerTool, Description("Records a step in the current workflow (navigate, click, type_text, wait, scroll, press_key).")]
    public static string valid_record_workflow_step(string action, string? url = null, string? nodeId = null,
        string? text = null, string? direction = null, int? amount = null, int? waitMs = null,
        string? key = null, double? x = null, double? y = null)
    {
        return WorkflowManager.RecordStep(action, url, nodeId, text, direction, amount, waitMs, key, x, y);
    }

    /// <summary>
    /// Stops recording and saves the workflow to disk.
    /// </summary>
    [McpServerTool, Description("Stops recording and saves the workflow to disk for later playback or E2E test generation.")]
    public static string valid_stop_workflow_recording(string? description = null, string? baseUrl = null)
    {
        return WorkflowManager.StopRecording(description, baseUrl);
    }

    /// <summary>
    /// Lists all saved workflows.
    /// </summary>
    [McpServerTool, Description("Lists all saved workflows with their names, descriptions, and step counts.")]
    public static string valid_list_workflows()
    {
        return WorkflowManager.ListWorkflows();
    }

    /// <summary>
    /// Loads a workflow for inspection or playback.
    /// </summary>
    [McpServerTool, Description("Loads a saved workflow by name for inspection or playback.")]
    public static string valid_load_workflow(string workflowName)
    {
        return WorkflowManager.LoadWorkflow(workflowName);
    }

    /// <summary>
    /// Deletes a saved workflow.
    /// </summary>
    [McpServerTool, Description("Deletes a saved workflow by name.")]
    public static string valid_delete_workflow(string workflowName)
    {
        return WorkflowManager.DeleteWorkflow(workflowName);
    }

    /// <summary>
    /// Generates E2E test code from a recorded workflow.
    /// </summary>
    [McpServerTool, Description("Generates E2E test code (bUnit, Playwright, or Selenium) from a recorded workflow.")]
    public static string valid_generate_e2e_test(string workflowName, string? testFramework = "bunit")
    {
        return WorkflowManager.GenerateE2ETest(workflowName, testFramework);
    }

    /// <summary>
    /// Saves a sitemap captured from browser crawling.
    /// </summary>
    [McpServerTool, Description("Saves a sitemap (captured via MCP-Lite graph crawling) to a local JSON file.")]
    public static string valid_save_sitemap(string rootUrl, string nodesJson)
    {
        try
        {
            var nodes = JsonSerializer.Deserialize<List<WorkflowManager.SitemapNode>>(nodesJson);
            var sitemap = new WorkflowManager.Sitemap
            {
                RootUrl = rootUrl,
                TotalPages = nodes?.Count ?? 0,
                Nodes = nodes ?? new()
            };
            return WorkflowManager.SaveSitemap(sitemap);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Checks the health of the MCP-Lite browser automation process.
    /// Returns process status, uptime, and connectivity information.
    /// </summary>
    [McpServerTool, Description("Checks the health status of the MCP-Lite browser automation process.")]
    public static string valid_check_mcp_health()
    {
        return VisualGuideOrchestrator.GetHealthStatus();
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
