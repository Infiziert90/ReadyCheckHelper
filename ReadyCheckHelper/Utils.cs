using System;
using System.Runtime.CompilerServices;
using Lumina.Text.ReadOnly;

namespace ReadyCheckHelper;

public static class Utils
{
    public static unsafe ReadOnlySeStringSpan NameToSeString(Span<byte> name) => new((byte*)Unsafe.AsPointer(ref name[0]));
}