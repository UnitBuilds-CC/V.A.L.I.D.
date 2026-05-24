namespace Valid;

/// <summary>
/// Slab-backed list.
/// </summary>
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
