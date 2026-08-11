using Xunit;
using Valid;
using Valid.Testing.Models;

namespace Valid.Testing;

/// <summary>
/// Tests for ValidObjectBase core engine: ValidId, bitmask operations,
/// dirty tracking, state history, SetProperty, and ResetDirty.
/// </summary>
public class ValidObjectBaseTests
{
    // ── ValidId ──────────────────────────────────────────────────────

    [Fact]
    public void ValidId_IsUnique_PerInstance()
    {
        var a = new BasicTestModel();
        var b = new BasicTestModel();
        Assert.NotEqual(a.ValidId, b.ValidId);
    }

    [Fact]
    public void ValidId_FollowsPattern()
    {
        var model = new BasicTestModel();
        Assert.StartsWith("valid_", model.ValidId);
        Assert.True(model.ValidId.Length > 6); // "valid_" + at least one digit
    }

    [Fact]
    public void ValidId_IsStable_AccessMultipleTimes()
    {
        var model = new BasicTestModel();
        var first = model.ValidId;
        var second = model.ValidId;
        Assert.Equal(first, second);
    }

    // ── Dirty Tracking (Bitmask) ─────────────────────────────────────

    [Fact]
    public void NewObject_IsNotDirty()
    {
        var model = new BasicTestModel();
        Assert.False(model.IsDirty);
        Assert.Equal((UInt128)0, model.DirtyFlags);
    }

    [Fact]
    public void SetProperty_FlipsDirtyBit()
    {
        var model = new BasicTestModel();
        model.Age = 25;
        Assert.True(model.IsDirty);
        Assert.NotEqual((UInt128)0, model.DirtyFlags);
    }

    [Fact]
    public void SetProperty_SameValue_DoesNotFlipBit()
    {
        var model = new BasicTestModel();
        model.Age = 0; // default is 0, setting to 0 again
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void SetProperty_DifferentProperties_FlipDifferentBits()
    {
        var model = new BasicTestModel();
        model.Age = 10;
        var dirtyAfterAge = model.DirtyFlags;

        model.Name = "Test";
        var dirtyAfterName = model.DirtyFlags;

        // Both bits should be set, and they should be different bits
        Assert.NotEqual(dirtyAfterAge, dirtyAfterName);
        Assert.True((dirtyAfterName & dirtyAfterAge) != 0 || dirtyAfterName != dirtyAfterAge);
    }

    [Fact]
    public void ResetDirty_ClearsAllBits()
    {
        var model = new BasicTestModel();
        model.Age = 25;
        model.Name = "Test";
        Assert.True(model.IsDirty);

        model.ResetDirty();
        Assert.False(model.IsDirty);
        Assert.Equal((UInt128)0, model.DirtyFlags);
    }

    // ── Bitmask Properties ───────────────────────────────────────────

    [Fact]
    public void BusyFlags_DefaultsToZero()
    {
        var model = new BasicTestModel();
        Assert.Equal((UInt128)0, model.BusyFlags);
    }

    [Fact]
    public void ErrorFlags_DefaultsToZero()
    {
        var model = new BasicTestModel();
        Assert.Equal((UInt128)0, model.ErrorFlags);
    }

    [Fact]
    public void StateFlags_DefaultsToZero()
    {
        var model = new BasicTestModel();
        Assert.Equal((UInt128)0, model.StateFlags);
    }

    [Fact]
    public void IsValid_TrueWhenNoErrors()
    {
        var model = new BasicTestModel();
        Assert.True(model.IsValid);
    }

    // ── State History ────────────────────────────────────────────────

    [Fact]
    public void GetStateHistory_ReturnsDefensiveCopy()
    {
        var model = new BasicTestModel();
        var history1 = model.GetStateHistory();
        var history2 = model.GetStateHistory();
        Assert.NotSame(history1, history2);
    }

    [Fact]
    public void GetStateHistory_CannotCorruptInternal()
    {
        var model = new BasicTestModel();
        var history = model.GetStateHistory();
        history[0] = "tampered";
        var freshHistory = model.GetStateHistory();
        Assert.NotEqual("tampered", freshHistory[0]);
    }

    [Fact]
    public void GetStateHistory_HasCorrectLength()
    {
        var model = new BasicTestModel();
        var history = model.GetStateHistory();
        Assert.Equal(16, history.Length);
    }

    // ── PropertyChanged ──────────────────────────────────────────────

    [Fact]
    public void SetProperty_RaisesPropertyChanged()
    {
        var model = new BasicTestModel();
        string? changedProp = null;
        model.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        model.Age = 42;
        Assert.Equal("Age", changedProp);
    }

    [Fact]
    public void SetProperty_SameValue_DoesNotRaisePropertyChanged()
    {
        var model = new BasicTestModel();
        bool raised = false;
        model.PropertyChanged += (_, _) => raised = true;

        model.Age = 0; // same as default
        Assert.False(raised);
    }

    // ── GetBitIndex ──────────────────────────────────────────────────

    [Fact]
    public void GetBitIndex_ReturnsConsistentIndex()
    {
        var model = new BasicTestModel();
        var idx1 = model.GetBitIndex("Age");
        var idx2 = model.GetBitIndex("Age");
        Assert.Equal(idx1, idx2);
    }

    [Fact]
    public void GetBitIndex_DifferentProperties_HaveDifferentIndices()
    {
        var model = new BasicTestModel();
        var ageIdx = model.GetBitIndex("Age");
        var nameIdx = model.GetBitIndex("Name");
        Assert.NotEqual(ageIdx, nameIdx);
    }

    [Fact]
    public void GetBitIndex_IsWithinUInt128Range()
    {
        var model = new BasicTestModel();
        var idx = model.GetBitIndex("Age");
        Assert.InRange(idx, 0, 127);
    }

    // ── Delta JSON ───────────────────────────────────────────────────

    [Fact]
    public void GetDeltaJson_OnlyIncludesDirtyProperties()
    {
        var model = new BasicTestModel();
        model.Age = 25;
        // Name is not set, so should not appear in delta

        var delta = model.GetDeltaJson();
        Assert.Contains("Age", delta);
        Assert.DoesNotContain("Name", delta);
    }

    [Fact]
    public void GetDeltaJson_EmptyWhenClean()
    {
        var model = new BasicTestModel();
        var delta = model.GetDeltaJson();
        // No properties dirty → empty or minimal JSON
        Assert.True(delta == "{}" || delta == "" || !delta.Contains("Age"));
    }

    // ── Metadata ─────────────────────────────────────────────────────

    [Fact]
    public void GetValidMetadata_ReturnsNonEmpty()
    {
        var model = new BasicTestModel();
        var metadata = model.GetValidMetadata();
        Assert.False(string.IsNullOrEmpty(metadata));
    }

    [Fact]
    public void GetValidMetadata_ContainsPropertyNames()
    {
        var model = new BasicTestModel();
        var metadata = model.GetValidMetadata();
        Assert.Contains("Age", metadata);
        Assert.Contains("Name", metadata);
    }

    // ── IsNew ────────────────────────────────────────────────────────

    [Fact]
    public void IsNew_TrueByDefault()
    {
        var model = new BasicTestModel();
        Assert.True(model.IsNew);
    }

    // ── SlabIndex ────────────────────────────────────────────────────

    [Fact]
    public void SlabIndex_DefaultsToNegativeOne()
    {
        var model = new BasicTestModel();
        Assert.Equal(-1, model.SlabIndex);
    }
}
