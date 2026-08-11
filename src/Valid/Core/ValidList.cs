namespace Valid;

/// <summary>
/// Slab-backed fixed-capacity collection for unmanaged types.
/// Named ValidSlab to accurately reflect its slab semantics (no dynamic resize).
/// </summary>
public sealed class ValidSlab<T> : IDisposable where T : unmanaged
{
    private readonly UnmanagedSlab<T> _slab;

    public ValidSlab(int capacity = 1024)
    {
        _slab = new UnmanagedSlab<T>(capacity);
    }

    public int Length => _slab.Length;

    public ref T this[int index] => ref _slab[index];

    public UnmanagedSlab<T>.Enumerator GetEnumerator() => _slab.GetEnumerator();

    public void Dispose() => _slab.Dispose();
}

/// <summary>
/// Backward-compatible alias. Prefer <see cref="ValidSlab{T}"/> for new code.
/// </summary>
[System.Obsolete("Use ValidSlab<T> instead. ValidList will be removed in a future version.")]
public sealed class ValidList<T> : IDisposable where T : unmanaged
{
    private readonly UnmanagedSlab<T> _slab;

    public ValidList(int capacity = 1024)
    {
        _slab = new UnmanagedSlab<T>(capacity);
    }

    public int Length => _slab.Length;

    public ref T this[int index] => ref _slab[index];

    public UnmanagedSlab<T>.Enumerator GetEnumerator() => _slab.GetEnumerator();

    public void Dispose() => _slab.Dispose();
}
