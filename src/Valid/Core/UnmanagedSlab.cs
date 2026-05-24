using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Valid;

/// <summary>
/// Unmanaged memory slab for high-performance data processing.
/// </summary>
public unsafe class UnmanagedSlab<T> : IDisposable where T : unmanaged
{
    private readonly T* _ptr;
    private readonly int _length;
    private readonly bool _isArenaAllocated;
    private bool _disposed;

    public int Length => _length;

    public UnmanagedSlab(int length)
    {
        _length = length;
        _isArenaAllocated = false;
        _ptr = (T*)NativeMemory.Alloc((nuint)length, (nuint)sizeof(T));
        NativeMemory.Clear(_ptr, (nuint)length * (nuint)sizeof(T));
    }

    /// <summary>
    /// Creates slab with pre-allocated pointer.
    /// </summary>
    public UnmanagedSlab(T* ptr, int length)
    {
        _length = length;
        _isArenaAllocated = true;
        _ptr = ptr;
    }

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_length) throw new IndexOutOfRangeException();
            return ref _ptr[index];
        }
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

    public void Dispose()
    {
        if (!_disposed)
        {
            NativeMemory.Clear(_ptr, (nuint)_length * (nuint)sizeof(T));
            
            if (!_isArenaAllocated)
            {
                NativeMemory.Free(_ptr);
            }
            
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~UnmanagedSlab()
    {
        Dispose();
    }

    public ref struct Enumerator
    {
        private readonly UnmanagedSlab<T> _slab;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(UnmanagedSlab<T> slab)
        {
            _slab = slab;
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int index = _index + 1;
            if (index < _slab.Length)
            {
                _index = index;
                return true;
            }
            return false;
        }

        public ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _slab[_index];
        }
    }

    /// <summary>
    /// Returns direct pointer to slab memory.
    /// </summary>
    public T* GetUnsafePointer() => _ptr;
}
