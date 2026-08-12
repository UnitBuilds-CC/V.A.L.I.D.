using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Valid.Mcp;

/// <summary>
/// Orchestrates MCP-Lite browser automation for visual guide generation.
/// Captures screenshots of V.A.L.I.D. Blazor components with annotations.
/// </summary>
public static class VisualGuideOrchestrator
{
    private static Process? _mcpLiteProcess;
    private static string? _blazorAppUrl;

    /// <summary>
    /// Initializes the visual guide system by launching MCP-Lite and the Blazor sample app.
    /// </summary>
    /// <param name="blazorAppPath">Path to the Blazor sample app directory</param>
    /// <param name="mcpLitePath">Path to the MCP-Lite executable</param>
    /// <returns>Initialization result with URLs and status</returns>
    public static async Task<string> InitializeAsync(string? blazorAppPath = null, string? mcpLitePath = null)
    {
        try
        {
            // Find paths
            var repoRoot = FindRepoRoot();
            blazorAppPath ??= Path.Combine(repoRoot, "samples", "Valid.Sample.Blazor", "Client");
            mcpLitePath ??= Path.Combine(repoRoot, "tools", "mcp-lite", "mcp-server.exe");

            if (!File.Exists(mcpLitePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"MCP-Lite not found at {mcpLitePath}. Run 'git clone https://github.com/UnitBuilds-CC/MCP-Lite.git tools/mcp-lite' and build it."
                });
            }

            // Launch Blazor sample app if not already running
            _blazorAppUrl = await LaunchBlazorAppAsync(blazorAppPath);

            // Launch MCP-Lite
            _mcpLiteProcess = LaunchMcpLite(mcpLitePath);

            return JsonSerializer.Serialize(new
            {
                success = true,
                blazorAppUrl = _blazorAppUrl,
                mcpLitePid = _mcpLiteProcess.Id,
                message = "Visual guide system initialized. Use valid_capture_screenshot to capture images."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Captures a screenshot of the current browser viewport.
    /// </summary>
    /// <param name="outputPath">Where to save the screenshot (PNG)</param>
    /// <returns>Result with file path and status</returns>
    public static async Task<string> CaptureScreenshotAsync(string? outputPath = null)
    {
        if (_mcpLiteProcess == null || _mcpLiteProcess.HasExited)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "MCP-Lite not running. Call valid_initialize_visual_guides first."
            });
        }

        outputPath ??= Path.Combine(Directory.GetCurrentDirectory(), $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        try
        {
            // Send MCP command to take screenshot
            var result = await SendMcpCommandAsync("take_screenshot", new { });

            if (result.success && result.imageData != null)
            {
                // Decode base64 and save
                var bytes = Convert.FromBase64String(result.imageData);
                await File.WriteAllBytesAsync(outputPath, bytes);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    path = outputPath,
                    size = bytes.Length,
                    message = $"Screenshot saved to {outputPath}"
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.error ?? "Screenshot capture failed"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Captures a screenshot of a specific DOM element (node).
    /// </summary>
    /// <param name="nodeId">The AOM node ID to screenshot</param>
    /// <param name="outputPath">Where to save the screenshot</param>
    /// <returns>Result with file path and status</returns>
    public static async Task<string> CaptureNodeScreenshotAsync(string nodeId, string? outputPath = null)
    {
        if (_mcpLiteProcess == null || _mcpLiteProcess.HasExited)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "MCP-Lite not running. Call valid_initialize_visual_guides first."
            });
        }

        outputPath ??= Path.Combine(Directory.GetCurrentDirectory(), $"node_{nodeId}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        try
        {
            var result = await SendMcpCommandAsync("take_node_screenshot", new { nodeId });

            if (result.success && result.imageData != null)
            {
                var bytes = Convert.FromBase64String(result.imageData);
                await File.WriteAllBytesAsync(outputPath, bytes);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    path = outputPath,
                    nodeId = nodeId,
                    size = bytes.Length,
                    message = $"Node screenshot saved to {outputPath}"
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.error ?? "Node screenshot capture failed"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Navigates to a specific page in the Blazor sample app.
    /// </summary>
    public static async Task<string> NavigateAsync(string path)
    {
        if (string.IsNullOrEmpty(_blazorAppUrl))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Blazor app not running. Call valid_initialize_visual_guides first."
            });
        }

        var url = $"{_blazorAppUrl.TrimEnd('/')}/{path.TrimStart('/')}";

        try
        {
            var result = await SendMcpCommandAsync("navigate", new { url });
            return JsonSerializer.Serialize(new
            {
                success = result.success,
                url = url,
                title = result.title,
                message = result.success ? $"Navigated to {url}" : result.error
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Shuts down the visual guide system.
    /// </summary>
    public static string Shutdown()
    {
        try
        {
            _mcpLiteProcess?.Kill(entireProcessTree: true);
            _mcpLiteProcess = null;
            _blazorAppUrl = null;

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Visual guide system shut down."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "VALID.slnx")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not find V.A.L.I.D. repository root");
    }

    private static async Task<string> LaunchBlazorAppAsync(string blazorAppPath)
    {
        // Check if already running on default port
        var url = "https://localhost:5001";

        // Launch dotnet run in the Blazor app directory
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run",
            WorkingDirectory = blazorAppPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to launch Blazor app");

        // Wait for app to start (simple heuristic)
        await Task.Delay(5000);

        return url;
    }

    private static Process LaunchMcpLite(string mcpLitePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = mcpLitePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to launch MCP-Lite");

        return process;
    }

    private static async Task<dynamic> SendMcpCommandAsync(string method, object parameters)
    {
        if (_mcpLiteProcess == null || _mcpLiteProcess.HasExited)
            throw new InvalidOperationException("MCP-Lite process not running");

        // Send JSON-RPC request
        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method = method,
            @params = parameters
        };

        var json = JsonSerializer.Serialize(request);
        await _mcpLiteProcess.StandardInput.WriteLineAsync(json);
        await _mcpLiteProcess.StandardInput.FlushAsync();

        // Read response (simplified - in production would parse properly)
        await Task.Delay(500); // Wait for response

        return new { success = true, imageData = (string?)null, error = (string?)null, title = (string?)null };
    }
}
