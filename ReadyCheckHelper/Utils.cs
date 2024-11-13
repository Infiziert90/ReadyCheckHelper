﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lumina.Text.ReadOnly;

namespace ReadyCheckHelper;

public static class Utils
{
    public static unsafe ReadOnlySeStringSpan NameToSeString(Span<byte> name) => new((byte*)Unsafe.AsPointer(ref name[0]));

    /// <summary> Return the first object fulfilling the predicate or null for structs. </summary>
    /// <param name="values"> The enumerable. </param>
    /// <param name="predicate"> The predicate. </param>
    /// <returns> The first object fulfilling the predicate, or a null-optional. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T? FirstOrNull<T>(this IEnumerable<T> values, Func<T, bool> predicate) where T : struct
    {
        foreach(var val in values)
            if (predicate(val))
                return val;

        return null;
    }
}