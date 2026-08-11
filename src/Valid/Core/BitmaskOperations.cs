using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Valid;

/// <summary>
/// Low-level SIMD-accelerated bitmask operations for bulk UInt128 blocks.
/// These are advanced utilities for custom slab processing.
/// Most users should use <see cref="ValidObjectBase.IsDirty"/> and related APIs instead.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static unsafe class BitmaskOperations
{
    /// <summary>
    /// Checks if any bit is set in block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AnyBitSet(System.UInt128* ptr, int count)
    {
        ulong* uPtr = (ulong*)ptr;
        int uCount = count * 2;

        int i = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<ulong> vZero = Vector256<ulong>.Zero;
            for (; i <= uCount - 4; i += 4)
            {
                var vMask = Vector256.Load(uPtr + i);
                if (vMask != vZero)
                {
                    return true;
                }
            }
        }

        for (; i < uCount; i++)
        {
            if (uPtr[i] != 0) return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if all bits are zero in block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AllZero(System.UInt128* ptr, int count) => !AnyBitSet(ptr, count);
}
