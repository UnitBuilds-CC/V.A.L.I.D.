using Xunit;
using Valid.Mcp;
using System.Text.Json;
using System.IO;
using System;

namespace Valid.Testing
{
    public class WorkflowManagerTests
    {
        private readonly string _testWorkflowsDir;
        private readonly string _testSitemapsDir;

        public WorkflowManagerTests()
        {
            // Use temp directories for testing
            _testWorkflowsDir = Path.Combine(Path.GetTempPath(), "valid_test_workflows");
            _testSitemapsDir = Path.Combine(Path.GetTempPath(), "valid_test_sitemaps");
            Directory.CreateDirectory(_testWorkflowsDir);
            Directory.CreateDirectory(_testSitemapsDir);
        }

        [Fact]
        public void StartRecording_ReturnsSuccess()
        {
            var result = WorkflowManager.StartRecording("test_workflow", "Test description");
            var json = JsonDocument.Parse(result);

            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("test_workflow", json.RootElement.GetProperty("workflowName").GetString());

            // Clean up recording state
            WorkflowManager.StopRecording();
        }

        [Fact]
        public void StartRecording_WhenAlreadyRecording_ReturnsError()
        {
            WorkflowManager.StartRecording("first_workflow");

            var result = WorkflowManager.StartRecording("second_workflow");
            var json = JsonDocument.Parse(result);

            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("Already recording", json.RootElement.GetProperty("error").GetString());

            // Clean up
            WorkflowManager.StopRecording();
        }

        [Fact]
        public void RecordStep_WhenNotRecording_ReturnsError()
        {
            var result = WorkflowManager.RecordStep("navigate", url: "https://example.com");
            var json = JsonDocument.Parse(result);

            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("Not recording", json.RootElement.GetProperty("error").GetString());
        }

        [Fact]
        public void RecordStep_WhenRecording_ReturnsSuccess()
        {
            WorkflowManager.StartRecording("test_steps");

            var result = WorkflowManager.RecordStep("navigate", url: "https://example.com");
            var json = JsonDocument.Parse(result);

            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(1, json.RootElement.GetProperty("stepCount").GetInt32());

            // Clean up
            WorkflowManager.StopRecording();
        }

        [Fact]
        public void StopRecording_WhenNotRecording_ReturnsError()
        {
            var result = WorkflowManager.StopRecording();
            var json = JsonDocument.Parse(result);

            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        }

        [Fact]
        public void RecordAndStopWorkflow_SavesToFile()
        {
            var workflowName = $"test_save_{Guid.NewGuid():N}";
            WorkflowManager.StartRecording(workflowName, "Test save workflow");
            WorkflowManager.RecordStep("navigate", url: "https://localhost:5001");
            WorkflowManager.RecordStep("click", nodeId: "123");
            WorkflowManager.RecordStep("type_text", nodeId: "456", text: "Hello");

            var result = WorkflowManager.StopRecording("Test description", "https://localhost:5001");
            var json = JsonDocument.Parse(result);

            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(3, json.RootElement.GetProperty("stepCount").GetInt32());

            var path = json.RootElement.GetProperty("path").GetString();
            Assert.True(File.Exists(path), $"Workflow file should exist at {path}");

            // Verify file content
            var fileContent = File.ReadAllText(path);
            var workflow = JsonDocument.Parse(fileContent);
            Assert.Equal(workflowName, workflow.RootElement.GetProperty("Name").GetString());
            Assert.Equal(3, workflow.RootElement.GetProperty("Steps").GetArrayLength());

            // Clean up
            File.Delete(path);
        }

        [Fact]
        public void ListWorkflows_ReturnsWorkflowsArray()
        {
            // Create a workflow first
            var workflowName = $"test_list_{Guid.NewGuid():N}";
            WorkflowManager.StartRecording(workflowName);
            WorkflowManager.RecordStep("navigate", url: "https://example.com");
            WorkflowManager.StopRecording();

            var result = WorkflowManager.ListWorkflows();
            var json = JsonDocument.Parse(result);

            Assert.True(json.RootElement.TryGetProperty("workflows", out var workflows));
            Assert.True(workflows.GetArrayLength() >= 1, "Should have at least 1 workflow");

            // Clean up - find and delete the workflow we created
            foreach (var wf in workflows.EnumerateArray())
            {
                if (wf.GetProperty("name").GetString() == workflowName)
                {
                    var path = wf.GetProperty("path").GetString();
                    if (path != null && File.Exists(path))
                        File.Delete(path);
                }
            }
        }

        [Fact]
        public void LoadWorkflow_ReturnsError_WhenNotFound()
        {
            var result = WorkflowManager.LoadWorkflow("nonexistent_workflow");
            var json = JsonDocument.Parse(result);

            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("not found", json.RootElement.GetProperty("error").GetString());
        }

        [Fact]
        public void DeleteWorkflow_ReturnsError_WhenNotFound()
        {
            var result = WorkflowManager.DeleteWorkflow("nonexistent_workflow");
            var json = JsonDocument.Parse(result);

            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("not found", json.RootElement.GetProperty("error").GetString());
        }

        [Fact]
        public void GenerateE2ETest_ReturnsError_WhenWorkflowNotFound()
        {
            var result = WorkflowManager.GenerateE2ETest("nonexistent");
            var json = JsonDocument.Parse(result);

            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("not found", json.RootElement.GetProperty("error").GetString());
        }

        [Fact]
        public void SaveSitemap_CreatesFile()
        {
            var sitemap = new WorkflowManager.Sitemap
            {
                RootUrl = "https://localhost:5001",
                TotalPages = 2,
                Nodes = new()
                {
                    new WorkflowManager.SitemapNode { Url = "/", Title = "Home", Depth = 0 },
                    new WorkflowManager.SitemapNode { Url = "/about", Title = "About", Depth = 1 }
                }
            };

            var result = WorkflowManager.SaveSitemap(sitemap);
            var json = JsonDocument.Parse(result);

            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            var path = json.RootElement.GetProperty("path").GetString();
            Assert.True(File.Exists(path));

            // Clean up
            File.Delete(path);
        }
    }
}
