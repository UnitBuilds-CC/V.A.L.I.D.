using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Valid;

/// <summary>
/// Bump allocator for UnmanagedSlab.
/// </summary>
public unsafe class ArenaAllocator : IDisposable
{
    private readonly nint _startPtr;
    private readonly long _totalCapacityBytes;
    private long _offset;
    private bool _isDisposed;

    /// <summary>
    /// Initializes arena.
    /// </summary>
    public ArenaAllocator(long capacityBytes)
    {
        if (capacityBytes <= 0) throw new ArgumentOutOfRangeException(nameof(capacityBytes));
        
        _startPtr = (nint)NativeMemory.Alloc((nuint)capacityBytes);
        _totalCapacityBytes = capacityBytes;
        _offset = 0;
        _isDisposed = false;
        
        new Span<byte>((void*)_startPtr, (int)_totalCapacityBytes).Clear();
    }

    /// <summary>
    /// Allocates memory block from arena.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* Allocate<T>(int count) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        
        long size = (long)count * sizeof(T);
        long alignment = 8;
        
        while (true)
        {
            long currentOffset = Volatile.Read(ref _offset);
            long padding = (currentOffset % alignment == 0) ? 0 : alignment - (currentOffset % alignment);
            long absoluteOffset = currentOffset + padding;
            long newOffset = absoluteOffset + size;
            
            if (newOffset > _totalCapacityBytes)
            {
                throw new OutOfMemoryException($"Arena exhausted. Capacity: {_totalCapacityBytes}, Requested: {size}");
            }
            
            if (Interlocked.CompareExchange(ref _offset, newOffset, currentOffset) == currentOffset)
            {
                return (T*)(_startPtr + absoluteOffset);
            }
        }
    }

    /// <summary>
    /// Resets the arena.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        long currentOffset = Interlocked.Exchange(ref _offset, 0);
        new Span<byte>((void*)_startPtr, (int)currentOffset).Clear();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Reset();
            NativeMemory.Free((void*)_startPtr);
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~ArenaAllocator()
    {
        Dispose();
    }
}
