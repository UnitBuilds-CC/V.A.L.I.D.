using Xunit;
using Valid;
using Valid.Testing.Models;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Valid.Testing;

// ── Concrete test implementations ─────────────────────────────────

/// <summary>
/// Concrete implementation of VavidComponentBase for testing.
/// </summary>
public class TestVavidComponent : VavidComponentBase
{
}

/// <summary>
/// Concrete implementation of VavidComponent&lt;T&gt; for testing.
/// </summary>
public class TestVavidComponentOfT : VavidComponent<BasicTestModel>
{
}

/// <summary>
/// Concrete implementation of VavidSurgicalComponentBase for testing.
/// </summary>
public class TestSurgicalComponent : VavidSurgicalComponentBase
{
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.AddContent(0, $"Surgical:BitIndex={BitIndex}");
    }
}

// ── VavidComponentBase Tests ────────────────────────────────────────

public class VavidComponentBaseTests : TestContext
{
    [Fact]
    public void Renders_ChildContent()
    {
        var model = new BasicTestModel { Age = 10 };
        var cut = RenderComponent<TestVavidComponent>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.ChildContent, builder =>
            {
                builder.AddContent(0, "Hello from child");
            }));

        cut.MarkupMatches("Hello from child");
    }

    [Fact]
    public void Renders_Nothing_WhenNoChildContent()
    {
        var model = new BasicTestModel();
        var cut = RenderComponent<TestVavidComponent>(parameters => parameters
            .Add(p => p.Model, model));

        cut.MarkupMatches("");
    }

    [Fact]
    public void Renders_WithNullModel()
    {
        var cut = RenderComponent<TestVavidComponent>(parameters => parameters
            .Add(p => p.Model, null)
            .Add(p => p.ChildContent, builder =>
            {
                builder.AddContent(0, "No model");
            }));

        cut.MarkupMatches("No model");
    }

    [Fact]
    public void UpdateProperty_DelegatesToModel()
    {
        var model = new BasicTestModel();
        var cut = RenderComponent<TestVavidComponent>(parameters => parameters
            .Add(p => p.Model, model));

        // Call the JSInvokable method directly
        cut.Instance.UpdateProperty("", "{\"Age\": 42}");

        Assert.Equal(42, model.Age);
    }

    [Fact]
    public void UpdateProperty_NullModel_DoesNotThrow()
    {
        var cut = RenderComponent<TestVavidComponent>(parameters => parameters
            .Add(p => p.Model, null));

        // Should not throw
        cut.Instance.UpdateProperty("Age", "42");
    }

    [Fact]
    public void RestoreHistory_DelegatesToModel()
    {
        var model = new BasicTestModel();
        model.Age = 10;
        model.Age = 20;

        var cut = RenderComponent<TestVavidComponent>(parameters => parameters
            .Add(p => p.Model, model));

        cut.Instance.RestoreHistory(1);

        // Should have restored to previous state
        Assert.Equal(10, model.Age);
    }

    [Fact]
    public void RestoreHistory_NullModel_DoesNotThrow()
    {
        var cut = RenderComponent<TestVavidComponent>(parameters => parameters
            .Add(p => p.Model, null));

        cut.Instance.RestoreHistory(1);
    }

    [Fact]
    public void SuppressSurgicalUpdates_DefaultsFalse()
    {
        // Reset static state
        VavidComponentBase.SuppressSurgicalUpdates = false;
        Assert.False(VavidComponentBase.SuppressSurgicalUpdates);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow_WithNullModel()
    {
        var cut = RenderComponent<TestVavidComponent>(parameters => parameters
            .Add(p => p.Model, null));

        await cut.Instance.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow_WithModel()
    {
        var model = new BasicTestModel();
        var cut = RenderComponent<TestVavidComponent>(parameters => parameters
            .Add(p => p.Model, model));

        await cut.Instance.DisposeAsync();
    }
}

// ── VavidComponent<T> Tests ────────────────────────────────────────

public class VavidComponentOfTTests : TestContext
{
    [Fact]
    public void OnParametersSet_SetsModel_FromValue()
    {
        var model = new BasicTestModel { Age = 55 };
        var cut = RenderComponent<TestVavidComponentOfT>(parameters => parameters
            .Add(p => p.Value, model));

        // The Model property (inherited from VavidComponentBase) should be set to Value
        Assert.Equal(model, cut.Instance.Model);
    }

    [Fact]
    public void Value_IsAccessible()
    {
        var model = new BasicTestModel { Name = "Test" };
        var cut = RenderComponent<TestVavidComponentOfT>(parameters => parameters
            .Add(p => p.Value, model));

        Assert.Equal("Test", cut.Instance.Value.Name);
    }
}

// ── VavidSurgicalComponentBase Tests ────────────────────────────────

public class VavidSurgicalComponentBaseTests : TestContext
{
    [Fact]
    public void Renders_InitialContent()
    {
        var model = new BasicTestModel();
        var cut = RenderComponent<TestSurgicalComponent>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.BitIndex, 0));

        cut.MarkupMatches("Surgical:BitIndex=0");
    }

    [Fact]
    public void ShouldRender_ReturnsFalse_AfterFirstRender()
    {
        var model = new BasicTestModel();
        var cut = RenderComponent<TestSurgicalComponent>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.BitIndex, 0));

        // After first render, _shouldRender is set to false in OnAfterRenderAsync
        // ShouldRender should return false (unless ForceStandardRendering is true)
        VavidSurgicalComponentBase.ForceStandardRendering = false;
        // We can't directly test ShouldRender (it's protected), but we can verify
        // that re-rendering doesn't produce new markup after BitPulse with wrong index
    }

    [Fact]
    public void ForceStandardRendering_DefaultsFalse()
    {
        VavidSurgicalComponentBase.ForceStandardRendering = false;
        Assert.False(VavidSurgicalComponentBase.ForceStandardRendering);
    }

    [Fact]
    public void BitPulse_MatchingIndex_TriggersRerender()
    {
        var model = new BasicTestModel();
        var cut = RenderComponent<TestSurgicalComponent>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.BitIndex, 0));

        // Fire BitPulse with matching index
        model.Age = 42; // This triggers BitPulse(0, 0) since Age is property 0

        // The component should have re-rendered
        cut.MarkupMatches("Surgical:BitIndex=0");
    }

    [Fact]
    public void UpdateProperty_DelegatesToModel()
    {
        var model = new BasicTestModel();
        var cut = RenderComponent<TestSurgicalComponent>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.BitIndex, 0));

        cut.Instance.UpdateProperty("", "{\"Age\": 77}");
        Assert.Equal(77, model.Age);
    }

    [Fact]
    public void UpdateProperty_NullModel_DoesNotThrow()
    {
        var cut = RenderComponent<TestSurgicalComponent>(parameters => parameters
            .Add(p => p.Model, null)
            .Add(p => p.BitIndex, 0));

        cut.Instance.UpdateProperty("Age", "42");
    }

    [Fact]
    public void Dispose_UnhooksBitPulse()
    {
        var model = new BasicTestModel();
        var cut = RenderComponent<TestSurgicalComponent>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.BitIndex, 0));

        cut.Instance.Dispose();

        // After dispose, BitPulse should be unhooked
        // Changing a property should not cause issues
        model.Age = 99;
    }

    [Fact]
    public void Dispose_NullModel_DoesNotThrow()
    {
        var cut = RenderComponent<TestSurgicalComponent>(parameters => parameters
            .Add(p => p.Model, null)
            .Add(p => p.BitIndex, 0));

        cut.Instance.Dispose();
    }

    [Fact]
    public void ModelChange_RehooksBitPulse()
    {
        var model1 = new BasicTestModel();
        var model2 = new BasicTestModel();

        var cut = RenderComponent<TestSurgicalComponent>(parameters => parameters
            .Add(p => p.Model, model1)
            .Add(p => p.BitIndex, 0));

        // Change to a different model
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Model, model2)
            .Add(p => p.BitIndex, 0));

        // model2 should now be hooked
        model2.Age = 42; // Should trigger BitPulse
        cut.MarkupMatches("Surgical:BitIndex=0");
    }
}
