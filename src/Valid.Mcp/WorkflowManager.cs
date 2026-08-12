using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Valid.Mcp;

/// <summary>
/// Manages workflow recording, playback, and E2E test generation.
/// Integrates with MCP-Lite for browser automation and stores workflows locally.
/// </summary>
public static class WorkflowManager
{
    private static readonly string WorkflowsDirectory = Path.Combine(
        Directory.GetCurrentDirectory(), "workflows");

    private static readonly string SitemapsDirectory = Path.Combine(
        Directory.GetCurrentDirectory(), "sitemaps");

    private static bool _isRecording;
    private static string? _currentWorkflowName;
    private static readonly List<WorkflowStep> _recordedSteps = new();

    /// <summary>
    /// Represents a single step in a recorded workflow.
    /// </summary>
    public class WorkflowStep
    {
        public string Action { get; set; } = "";
        public string? Url { get; set; }
        public string? NodeId { get; set; }
        public string? Text { get; set; }
        public string? Direction { get; set; }
        public int? Amount { get; set; }
        public int? WaitMs { get; set; }
        public string? Key { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? X2 { get; set; }
        public double? Y2 { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a complete workflow definition.
    /// </summary>
    public class WorkflowDefinition
    {
        /// <summary>Schema version for forward compatibility. Current: 1.</summary>
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string BaseUrl { get; set; } = "";
        public List<WorkflowStep> Steps { get; set; } = new();
    }

    /// <summary>
    /// Represents a node in the sitemap graph.
    /// </summary>
    public class SitemapNode
    {
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
        public string Hash { get; set; } = "";
        public int Depth { get; set; }
        public List<string> ChildUrls { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Represents the complete sitemap.
    /// </summary>
    public class Sitemap
    {
        public string RootUrl { get; set; } = "";
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
        public int TotalPages { get; set; }
        public List<SitemapNode> Nodes { get; set; } = new();
    }

    /// <summary>
    /// Starts recording a workflow.
    /// </summary>
    public static string StartRecording(string workflowName, string? description = null)
    {
        if (_isRecording)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Already recording workflow '{_currentWorkflowName}'. Stop it first."
            });
        }

        _isRecording = true;
        _currentWorkflowName = workflowName;
        _recordedSteps.Clear();

        return JsonSerializer.Serialize(new
        {
            success = true,
            workflowName = workflowName,
            message = $"Recording started. Use valid_record_workflow_step to add steps."
        });
    }

    /// <summary>
    /// Records a single step in the current workflow.
    /// </summary>
    public static string RecordStep(string action, string? url = null, string? nodeId = null,
        string? text = null, string? direction = null, int? amount = null, int? waitMs = null,
        string? key = null, double? x = null, double? y = null, double? x2 = null, double? y2 = null)
    {
        if (!_isRecording)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Not recording. Call valid_start_workflow_recording first."
            });
        }

        _recordedSteps.Add(new WorkflowStep
        {
            Action = action,
            Url = url,
            NodeId = nodeId,
            Text = text,
            Direction = direction,
            Amount = amount,
            WaitMs = waitMs,
            Key = key,
            X = x,
            Y = y,
            X2 = x2,
            Y2 = y2
        });

        return JsonSerializer.Serialize(new
        {
            success = true,
            stepCount = _recordedSteps.Count,
            lastAction = action
        });
    }

    /// <summary>
    /// Stops recording and saves the workflow to disk.
    /// </summary>
    public static string StopRecording(string? description = null, string? baseUrl = null)
    {
        if (!_isRecording || _currentWorkflowName == null)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Not recording. Call valid_start_workflow_recording first."
            });
        }

        Directory.CreateDirectory(WorkflowsDirectory);
        var workflowPath = Path.Combine(WorkflowsDirectory, $"{_currentWorkflowName}.json");

        var workflow = new WorkflowDefinition
        {
            Name = _currentWorkflowName,
            Description = description ?? $"Recorded workflow '{_currentWorkflowName}'",
            BaseUrl = baseUrl ?? "https://localhost:5001",
            Steps = new List<WorkflowStep>(_recordedSteps)
        };

        var json = JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(workflowPath, json);

        _isRecording = false;
        _currentWorkflowName = null;
        _recordedSteps.Clear();

        return JsonSerializer.Serialize(new
        {
            success = true,
            workflowName = workflow.Name,
            stepCount = workflow.Steps.Count,
            path = workflowPath,
            message = $"Workflow saved to {workflowPath}"
        });
    }

    /// <summary>
    /// Lists all saved workflows.
    /// </summary>
    public static string ListWorkflows()
    {
        if (!Directory.Exists(WorkflowsDirectory))
        {
            return JsonSerializer.Serialize(new { workflows = Array.Empty<string>() });
        }

        var files = Directory.GetFiles(WorkflowsDirectory, "*.json");
        var workflows = new List<object>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var workflow = JsonSerializer.Deserialize<WorkflowDefinition>(json);
                if (workflow != null)
                {
                    workflows.Add(new
                    {
                        name = workflow.Name,
                        description = workflow.Description,
                        stepCount = workflow.Steps.Count,
                        createdAt = workflow.CreatedAt,
                        path = file
                    });
                }
            }
            catch { /* Skip invalid files */ }
        }

        return JsonSerializer.Serialize(new { workflows }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Loads a workflow for playback.
    /// </summary>
    public static string LoadWorkflow(string workflowName)
    {
        var workflowPath = Path.Combine(WorkflowsDirectory, $"{workflowName}.json");
        if (!File.Exists(workflowPath))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Workflow '{workflowName}' not found at {workflowPath}"
            });
        }

        var json = File.ReadAllText(workflowPath);
        return JsonSerializer.Serialize(new
        {
            success = true,
            workflow = JsonSerializer.Deserialize<WorkflowDefinition>(json)
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Deletes a saved workflow.
    /// </summary>
    public static string DeleteWorkflow(string workflowName)
    {
        var workflowPath = Path.Combine(WorkflowsDirectory, $"{workflowName}.json");
        if (!File.Exists(workflowPath))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Workflow '{workflowName}' not found"
            });
        }

        File.Delete(workflowPath);
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Workflow '{workflowName}' deleted"
        });
    }

    /// <summary>
    /// Saves a sitemap to disk.
    /// </summary>
    public static string SaveSitemap(Sitemap sitemap)
    {
        Directory.CreateDirectory(SitemapsDirectory);
        var sitemapPath = Path.Combine(SitemapsDirectory, $"sitemap_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        var json = JsonSerializer.Serialize(sitemap, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(sitemapPath, json);

        return JsonSerializer.Serialize(new
        {
            success = true,
            path = sitemapPath,
            totalPages = sitemap.TotalPages,
            message = $"Sitemap saved to {sitemapPath}"
        });
    }

    /// <summary>
    /// Generates E2E test code from a recorded workflow.
    /// </summary>
    public static string GenerateE2ETest(string workflowName, string? testFramework = "bunit")
    {
        var workflowPath = Path.Combine(WorkflowsDirectory, $"{workflowName}.json");
        if (!File.Exists(workflowPath))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Workflow '{workflowName}' not found"
            });
        }

        var json = File.ReadAllText(workflowPath);
        var workflow = JsonSerializer.Deserialize<WorkflowDefinition>(json);
        if (workflow == null)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Failed to parse workflow"
            });
        }

        var testCode = testFramework?.ToLower() switch
        {
            "playwright" => GeneratePlaywrightTest(workflow),
            "selenium" => GenerateSeleniumTest(workflow),
            _ => GenerateBUnitStyleTest(workflow)
        };

        // Save the generated test
        var testsDir = Path.Combine(Directory.GetCurrentDirectory(), "tests", "E2E");
        Directory.CreateDirectory(testsDir);
        var testPath = Path.Combine(testsDir, $"{workflowName}Tests.cs");
        File.WriteAllText(testPath, testCode);

        return JsonSerializer.Serialize(new
        {
            success = true,
            testPath = testPath,
            testFramework = testFramework ?? "bunit",
            stepCount = workflow.Steps.Count,
            message = $"E2E test generated at {testPath}"
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GenerateBUnitStyleTest(WorkflowDefinition workflow)
    {
        var steps = string.Join("\n            ", workflow.Steps.Select(GenerateStepCode));

        return $@"using Bunit;
using Xunit;

namespace Valid.Testing.E2E;

/// <summary>
/// E2E test generated from workflow '{workflow.Name}'.
/// {workflow.Description}
/// </summary>
public class {SanitizeName(workflow.Name)}Tests : TestContext
{{
    [Fact]
    public void {SanitizeName(workflow.Name)}_Workflow()
    {{
        // Arrange
        var baseUrl = ""{workflow.BaseUrl}"";

        // Act - Execute workflow steps
        {steps}

        // Assert
        // Add assertions based on expected outcomes
    }}
}}
";
    }

    private static string GeneratePlaywrightTest(WorkflowDefinition workflow)
    {
        var steps = string.Join("\n            ", workflow.Steps.Select(s => s.Action switch
        {
            "navigate" => $"await page.GotoAsync(\"{s.Url}\");",
            "click" => $"await page.ClickAsync(\"[data-node-id='{s.NodeId}']\");",
            "type_text" => $"await page.FillAsync(\"[data-node-id='{s.NodeId}']\", \"{EscapeString(s.Text ?? "")}\");",
            "wait" => $"await page.WaitForTimeoutAsync({s.WaitMs ?? 1000});",
            "press_key" => $"await page.PressAsync(\"body\", \"{s.Key}\");",
            "scroll" => $"await page.MouseWheelAsync(0, {(s.Direction == "down" ? s.Amount ?? 300 : -(s.Amount ?? 300))});",
            _ => $"// Unknown action: {s.Action}"
        }));

        return $@"using Microsoft.Playwright;
using System.Threading.Tasks;

namespace Valid.E2E;

/// <summary>
/// Playwright E2E test from workflow '{workflow.Name}'.
/// {workflow.Description}
/// </summary>
public class {SanitizeName(workflow.Name)}Tests
{{
    public static async Task Run()
    {{
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() {{ Headless = true }});
        var page = await browser.NewPageAsync();

        // Execute workflow steps
        {steps}

        await page.CloseAsync();
    }}
}}
";
    }

    private static string GenerateSeleniumTest(WorkflowDefinition workflow)
    {
        var steps = string.Join("\n            ", workflow.Steps.Select(s => s.Action switch
        {
            "navigate" => $"driver.Navigate().GoToUrl(\"{s.Url}\");",
            "click" => $"driver.FindElement(By.CssSelector(\"[data-node-id='{s.NodeId}']\")).Click();",
            "type_text" => $"driver.FindElement(By.CssSelector(\"[data-node-id='{s.NodeId}']\")).SendKeys(\"{EscapeString(s.Text ?? "")}\");",
            "wait" => $"System.Threading.Thread.Sleep({s.WaitMs ?? 1000});",
            "press_key" => $"driver.FindElement(By.TagName(\"body\")).SendKeys(OpenQA.Selenium.Keys.{s.Key ?? "Enter"});",
            _ => $"// Unknown action: {s.Action}"
        }));

        return $@"using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Valid.E2E;

/// <summary>
/// Selenium E2E test from workflow '{workflow.Name}'.
/// {workflow.Description}
/// </summary>
public class {SanitizeName(workflow.Name)}Tests
{{
    public void Run()
    {{
        using var driver = new ChromeDriver();

        // Execute workflow steps
        {steps}

        driver.Quit();
    }}
}}
";
    }

    private static string GenerateStepCode(WorkflowStep step)
    {
        return step.Action switch
        {
            "navigate" => $"// Navigate to {step.Url}",
            "click" => $"// Click node {step.NodeId}",
            "type_text" => $"// Type '{step.Text}' into node {step.NodeId}",
            "wait" => $"// Wait {step.WaitMs}ms",
            "scroll" => $"// Scroll {step.Direction} by {step.Amount}",
            "press_key" => $"// Press key: {step.Key}",
            _ => $"// {step.Action}"
        };
    }

    private static string SanitizeName(string name)
    {
        var sanitized = string.Concat(name.Split(System.IO.Path.GetInvalidFileNameChars()));
        return string.IsNullOrEmpty(sanitized) ? "Unnamed" : sanitized;
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
