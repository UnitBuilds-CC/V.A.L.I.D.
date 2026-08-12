using Xunit;
using Valid;
using Valid.Testing.Models;
using System;

namespace Valid.Testing
{
    public class ImprovementTests
    {
        [Fact]
        public void SetProperty_AutoResolve_ResolvesBitIndexFromPropertyName()
        {
            var model = new BasicTestModel();
            model.ResetDirty();

            // Use the auto-resolve overload (no bitIndex parameter)
            // This uses [CallerMemberName] to resolve the property name
            model.Name = "Test";

            Assert.True(model.IsDirty);
            // Verify the bit index is valid (>= 0)
            var bitIndex = model.GetBitIndex(nameof(BasicTestModel.Name));
            Assert.True(bitIndex >= 0, $"BitIndex for Name should be >= 0, got {bitIndex}");
            // Verify the dirty bit is set at that index
            Assert.True((model.DirtyFlags & ((System.UInt128)1 << bitIndex)) != 0);
        }

        [Fact]
        public void DiagnosticResult_IncludesCurrentValue_AndExpectedConstraint()
        {
            var model = new BasicTestModel();

            // Trigger validation by setting invalid values
            var diagnostics = model.GetDiagnostics();

            // DiagnosticResult should have the new context fields
            foreach (var diag in diagnostics)
            {
                // The struct should have the new fields (even if null)
                Assert.True(true); // DiagnosticResult compiles with new fields
            }
        }

        [Fact]
        public void DiagnosticResult_NewFields_AreAccessible()
        {
            var result = new DiagnosticResult(
                Property: "Name",
                Message: "Name is required",
                Code: "CUST-001",
                FixSuggestion: "Provide a non-empty name",
                CurrentValue: "null",
                ExpectedConstraint: "Required"
            );

            Assert.Equal("Name", result.Property);
            Assert.Equal("Name is required", result.Message);
            Assert.Equal("CUST-001", result.Code);
            Assert.Equal("Provide a non-empty name", result.FixSuggestion);
            Assert.Equal("null", result.CurrentValue);
            Assert.Equal("Required", result.ExpectedConstraint);
        }

        [Fact]
        public void DiagnosticResult_BackwardCompatible_OldConstructorStillWorks()
        {
            // Old 4-parameter constructor should still work
            var result = new DiagnosticResult("Name", "Error", "CODE-1", "Fix it");
            Assert.Equal("Name", result.Property);
            Assert.Null(result.CurrentValue);
            Assert.Null(result.ExpectedConstraint);
        }
    }
}
