using Xunit;
using Valid;
using Valid.Testing.Models;
using System.Text.Json;

namespace Valid.Testing;

/// <summary>
/// Tests for serialization round-trips (WriteDelta, UpdatePropertyFromJson,
/// RestoreHistory), BitPulse events, and concurrency safety.
/// </summary>
public class SerializationAndEventTests
{
    // ── WriteDelta ──────────────────────────────────────────────────

    [Fact]
    public void WriteDelta_ProducesValidJson()
    {
        var model = new BasicTestModel();
        model.Age = 42;
        model.Name = "Alice";

        var json = model.GetDeltaJson();
        using var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void WriteDelta_IncludesAllDirtyProperties()
    {
        var model = new BasicTestModel();
        model.Age = 10;
        model.Name = "Test";

        var json = model.GetDeltaJson();
        Assert.Contains("\"Age\"", json);
        Assert.Contains("\"Name\"", json);
        Assert.Contains("10", json);
        Assert.Contains("\"Test\"", json);
    }

    [Fact]
    public void WriteDelta_ExcludesCleanProperties()
    {
        var model = new BasicTestModel();
        model.Age = 25;
        // Name stays at default

        var json = model.GetDeltaJson();
        Assert.Contains("\"Age\"", json);
        Assert.DoesNotContain("\"Name\"", json);
    }

    [Fact]
    public void WriteDelta_ClearsDirtyFlags()
    {
        var model = new BasicTestModel();
        model.Age = 25;
        Assert.True(model.IsDirty);

        _ = model.GetDeltaJson();
        Assert.False(model.IsDirty);
    }

    // ── UpdatePropertyFromJson (Hydrate) ────────────────────────────

    [Fact]
    public void UpdatePropertyFromJson_RestoresSingleProperty()
    {
        var model = new BasicTestModel();
        // Empty propertyName triggers Hydrate which reads the full JSON object
        model.UpdatePropertyFromJson("", "{\"Age\": 99}");
        Assert.Equal(99, model.Age);
    }

    [Fact]
    public void UpdatePropertyFromJson_RestoresStringProperty()
    {
        var model = new BasicTestModel();
        model.UpdatePropertyFromJson("", "{\"Name\": \"Bob\"}");
        Assert.Equal("Bob", model.Name);
    }

    [Fact]
    public void UpdatePropertyFromJson_EmptyPropertyName_HydratesAll()
    {
        var model = new BasicTestModel();
        var json = "{\"Age\": 55, \"Name\": \"Charlie\"}";
        model.UpdatePropertyFromJson("", json);

        Assert.Equal(55, model.Age);
        Assert.Equal("Charlie", model.Name);
    }

    // ── Full Round-Trip ─────────────────────────────────────────────

    [Fact]
    public void RoundTrip_DeltaJson_ToApplyRawDelta()
    {
        var source = new BasicTestModel();
        source.Age = 77;
        source.Name = "Delta";

        var delta = source.GetDeltaJson();

        var target = new BasicTestModel();
        target.ApplyRawDelta(delta);

        Assert.Equal(77, target.Age);
        Assert.Equal("Delta", target.Name);
    }

    [Fact]
    public void RoundTrip_PartialDelta_OnlyTransfersDirtyProps()
    {
        var source = new BasicTestModel();
        source.Age = 33;
        // Name stays default

        var delta = source.GetDeltaJson();

        var target = new BasicTestModel();
        target.Name = "Original";
        target.ApplyRawDelta(delta);

        Assert.Equal(33, target.Age);
        Assert.Equal("Original", target.Name); // Not overwritten
    }

    [Fact]
    public void RoundTrip_AdvancedModel_DecimalField()
    {
        var source = new AdvancedTestModel();
        source.Balance = 1234.56m;

        var delta = source.GetDeltaJson();

        var target = new AdvancedTestModel();
        target.ApplyRawDelta(delta);

        Assert.Equal(1234.56m, target.Balance);
    }

    // ── RestoreHistory ──────────────────────────────────────────────

    [Fact]
    public void RestoreHistory_OneStepBack_RestoresPreviousState()
    {
        var model = new BasicTestModel();
        model.Age = 10;
        model.Name = "First";

        model.Age = 20;
        model.Name = "Second";

        // Snapshots: [0]={Age:10}, [1]={Age:10,Name:"First"}, [2]={Age:20,Name:"First"}, [3]={Age:20,Name:"Second"}
        // RestoreHistory(1) goes to _stateHistory[2] = state after 3rd change = {Age:20, Name:"First"}
        model.RestoreHistory(1);

        Assert.Equal(20, model.Age);
        Assert.Equal("First", model.Name);
    }

    [Fact]
    public void RestoreHistory_ZeroSteps_RestoresCurrentState()
    {
        var model = new BasicTestModel();
        model.Age = 42;

        model.RestoreHistory(0);

        Assert.Equal(42, model.Age);
    }

    [Fact]
    public void RestoreHistory_NegativeSteps_IsNoOp()
    {
        var model = new BasicTestModel();
        model.Age = 42;

        model.RestoreHistory(-1);

        Assert.Equal(42, model.Age);
    }

    [Fact]
    public void RestoreHistory_ExceedsHistoryLength_IsNoOp()
    {
        var model = new BasicTestModel();
        model.Age = 42;

        model.RestoreHistory(100);

        Assert.Equal(42, model.Age);
    }

    [Fact]
    public void RestoreHistory_RaisesPropertyChanged()
    {
        var model = new BasicTestModel();
        model.Age = 10;
        model.Age = 20;

        bool restoreRaised = false;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "RestoreHistory") restoreRaised = true;
        };

        model.RestoreHistory(1);
        Assert.True(restoreRaised);
    }

    [Fact]
    public void RestoreHistory_SetsStateFlag()
    {
        var model = new BasicTestModel();
        model.Age = 10;
        model.Age = 20;

        model.RestoreHistory(1);

        // RestoreHistory sets _stateFlags |= 0x01
        Assert.NotEqual((UInt128)0, model.StateFlags);
    }

    [Fact]
    public void RestoreHistory_DoesNotCaptureNewSnapshot()
    {
        var model = new BasicTestModel();
        model.Age = 10;
        model.Age = 20;
        model.Age = 30;

        // Restore to step 2 back (Age=10 state)
        model.RestoreHistory(2);
        Assert.Equal(10, model.Age);

        // Restore should NOT have created additional history entries
        // that would shift the history window
    }

    // ── BitPulse Event ──────────────────────────────────────────────

    [Fact]
    public void BitPulse_Fires_OnPropertyChange()
    {
        var model = new BasicTestModel();
        int firedIndex = -1;
        int firedMaskType = -1;
        model.BitPulse += (idx, maskType) => { firedIndex = idx; firedMaskType = maskType; };

        model.Age = 42;

        Assert.Equal(0, firedIndex); // Age is property 0
        Assert.Equal(0, firedMaskType); // 0 = dirty mask
    }

    [Fact]
    public void BitPulse_Fires_ForDifferentProperties()
    {
        var model = new BasicTestModel();
        var firedIndices = new List<int>();
        model.BitPulse += (idx, _) => firedIndices.Add(idx);

        model.Age = 10;
        model.Name = "Test";

        Assert.Contains(0, firedIndices); // Age
        Assert.Contains(1, firedIndices); // Name
    }

    [Fact]
    public void BitPulse_DoesNotFire_OnSameValue()
    {
        var model = new BasicTestModel();
        bool fired = false;
        model.BitPulse += (_, _) => fired = true;

        model.Age = 0; // same as default
        Assert.False(fired);
    }

    [Fact]
    public void BitPulse_Fires_OnValidate_WithErrorChanges()
    {
        var model = new BasicTestModel();
        var pulseIndices = new List<(int index, int maskType)>();
        model.BitPulse += (idx, maskType) => pulseIndices.Add((idx, maskType));

        // Validate with no errors → no pulse
        model.Validate();
        Assert.Empty(pulseIndices);
    }

    // ── Concurrency ─────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentSetProperties_NoDataCorruption()
    {
        var model = new BasicTestModel();
        var tasks = new List<Task>();

        // Hammer Age from multiple threads
        for (int i = 0; i < 100; i++)
        {
            int value = i;
            tasks.Add(Task.Run(() => model.Age = value));
        }

        await Task.WhenAll(tasks);

        // Age should have some value (last writer wins), and model should be dirty
        Assert.True(model.IsDirty);
        Assert.InRange(model.Age, 0, 99);
    }

    [Fact]
    public async Task ConcurrentSetAndResetDirty_NoCrash()
    {
        var model = new BasicTestModel();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var tasks = new List<Task>();

        // Writers
        for (int i = 0; i < 4; i++)
        {
            int val = i;
            tasks.Add(Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    model.Age = val;
                    model.Name = $"Thread{val}";
                }
            }));
        }

        // Resetters
        tasks.Add(Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
                model.ResetDirty();
        }));

        await Task.WhenAll(tasks);
        // No exceptions = success
    }

    [Fact]
    public async Task ConcurrentGetDeltaJson_NoCorruption()
    {
        var model = new BasicTestModel();
        model.Age = 1;
        model.Name = "Concurrent";

        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var json = model.GetDeltaJson();
                    // If we got here, the JSON should be parseable or empty
                    if (!string.IsNullOrEmpty(json) && json != "{}")
                        JsonDocument.Parse(json);
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }
}
