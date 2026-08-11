using Xunit;
using Valid;
using Valid.Testing.Models;
using System.Security;

namespace Valid.Testing;

/// <summary>
/// Tests for ValidSecurity shadow validation assertions.
/// </summary>
public class ValidSecurityTests
{
    [Fact]
    public void AssertShadowValidation_Passes_WhenErrorFlagsMatch()
    {
        var model = new BasicTestModel();
        // Fresh model: ErrorFlags = 0, CalculateValidationState() = 0
        ValidSecurity.AssertShadowValidation(model);
    }

    [Fact]
    public void AssertShadowValidation_Passes_WithNoErrors()
    {
        var model = new BasicTestModel();
        model.Age = 42;
        model.Validate();
        // CalculateValidationState returns 0 (no validation rules)
        ValidSecurity.AssertShadowValidation(model);
    }
}
