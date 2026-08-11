using Xunit;
using Valid;

namespace Valid.Testing;

/// <summary>
/// Tests for CrdtMerger: MergeField resolution semantics, MergeMask union, and Tick().
/// </summary>
public class CrdtMergerTests
{
    // ── MergeField ───────────────────────────────────────────────────

    [Fact]
    public void MergeField_HigherRemoteVersion_RemoteWins()
    {
        string localValue = "local";
        var localClock = new VectorClock(1, 5);
        string remoteValue = "remote";
        var remoteClock = new VectorClock(2, 10);

        CrdtMerger.MergeField(ref localValue, ref localClock, remoteValue, remoteClock);

        Assert.Equal("remote", localValue);
        Assert.Equal(remoteClock, localClock);
    }

    [Fact]
    public void MergeField_HigherLocalVersion_LocalWins()
    {
        string localValue = "local";
        var localClock = new VectorClock(1, 10);
        string remoteValue = "remote";
        var remoteClock = new VectorClock(2, 5);

        CrdtMerger.MergeField(ref localValue, ref localClock, remoteValue, remoteClock);

        Assert.Equal("local", localValue);
        Assert.Equal(new VectorClock(1, 10), localClock);
    }

    [Fact]
    public void MergeField_EqualVersion_HigherNodeId_RemoteWins()
    {
        string localValue = "local";
        var localClock = new VectorClock(1, 5);
        string remoteValue = "remote";
        var remoteClock = new VectorClock(3, 5); // same version, higher NodeId

        CrdtMerger.MergeField(ref localValue, ref localClock, remoteValue, remoteClock);

        Assert.Equal("remote", localValue);
        Assert.Equal(remoteClock, localClock);
    }

    [Fact]
    public void MergeField_EqualVersion_LowerNodeId_LocalWins()
    {
        string localValue = "local";
        var localClock = new VectorClock(5, 5);
        string remoteValue = "remote";
        var remoteClock = new VectorClock(3, 5); // same version, lower NodeId

        CrdtMerger.MergeField(ref localValue, ref localClock, remoteValue, remoteClock);

        Assert.Equal("local", localValue);
    }

    [Fact]
    public void MergeField_EqualVersionAndNodeId_Converged_NoChange()
    {
        string localValue = "local";
        var localClock = new VectorClock(1, 5);
        string remoteValue = "remote";
        var remoteClock = new VectorClock(1, 5); // identical

        CrdtMerger.MergeField(ref localValue, ref localClock, remoteValue, remoteClock);

        // Values converged — no update
        Assert.Equal("local", localValue);
    }

    [Fact]
    public void MergeField_WorksWithIntValues()
    {
        int localValue = 10;
        var localClock = new VectorClock(1, 1);
        int remoteValue = 20;
        var remoteClock = new VectorClock(2, 2);

        CrdtMerger.MergeField(ref localValue, ref localClock, remoteValue, remoteClock);

        Assert.Equal(20, localValue);
    }

    [Fact]
    public void MergeField_WorksWithNullableValues()
    {
        string? localValue = null;
        var localClock = new VectorClock(1, 1);
        string? remoteValue = "hello";
        var remoteClock = new VectorClock(2, 2);

        CrdtMerger.MergeField(ref localValue, ref localClock, remoteValue, remoteClock);

        Assert.Equal("hello", localValue);
    }

    // ── MergeMask ────────────────────────────────────────────────────

    [Fact]
    public void MergeMask_UnionOfBothMasks()
    {
        UInt128 local = (UInt128)1 << 0 | (UInt128)1 << 3;
        UInt128 remote = (UInt128)1 << 1 | (UInt128)1 << 5;

        var merged = CrdtMerger.MergeMask(local, remote);

        Assert.Equal((UInt128)1 << 0 | (UInt128)1 << 1 | (UInt128)1 << 3 | (UInt128)1 << 5, merged);
    }

    [Fact]
    public void MergeMask_BothZero_ReturnsZero()
    {
        var merged = CrdtMerger.MergeMask(0, 0);
        Assert.Equal((UInt128)0, merged);
    }

    [Fact]
    public void MergeMask_LocalOnly_PreservesLocal()
    {
        UInt128 local = (UInt128)1 << 10;
        var merged = CrdtMerger.MergeMask(local, 0);
        Assert.Equal(local, merged);
    }

    [Fact]
    public void MergeMask_RemoteOnly_PreservesRemote()
    {
        UInt128 remote = (UInt128)1 << 42;
        var merged = CrdtMerger.MergeMask(0, remote);
        Assert.Equal(remote, merged);
    }

    [Fact]
    public void MergeMask_OverlappingBits_NoDuplication()
    {
        UInt128 both = (UInt128)1 << 7;
        var merged = CrdtMerger.MergeMask(both, both);
        Assert.Equal(both, merged); // OR of same bits = same bits
    }

    // ── Tick ─────────────────────────────────────────────────────────

    [Fact]
    public void Tick_IncrementsVersion()
    {
        var clock = new VectorClock(1, 5);
        var ticked = CrdtMerger.Tick(clock);
        Assert.Equal(6, ticked.Version);
        Assert.Equal(1, ticked.NodeId); // NodeId unchanged
    }

    [Fact]
    public void Tick_FromZero_BecomesOne()
    {
        var clock = new VectorClock(42, 0);
        var ticked = CrdtMerger.Tick(clock);
        Assert.Equal(1, ticked.Version);
    }

    [Fact]
    public void Tick_DoesNotMutateOriginal()
    {
        var clock = new VectorClock(1, 5);
        CrdtMerger.Tick(clock);
        Assert.Equal(5, clock.Version); // original unchanged (record struct)
    }

    // ── VectorClock ──────────────────────────────────────────────────

    [Fact]
    public void VectorClock_Zero_HasZeroVersionAndNodeId()
    {
        var zero = VectorClock.Zero;
        Assert.Equal(0, zero.Version);
        Assert.Equal(0, zero.NodeId);
    }

    [Fact]
    public void VectorClock_IsNewerThan_HigherVersion()
    {
        var a = new VectorClock(1, 10);
        var b = new VectorClock(1, 5);
        Assert.True(a.IsNewerThan(b));
        Assert.False(b.IsNewerThan(a));
    }

    [Fact]
    public void VectorClock_IsNewerThan_EqualVersion_ReturnsFalse()
    {
        var a = new VectorClock(1, 5);
        var b = new VectorClock(2, 5);
        Assert.False(a.IsNewerThan(b));
    }
}
