using System;
using System.Collections.Generic;

namespace ShopkeepingSuite
{
    // Cache what we assigned, rather than TMP's auto-sized result. Created afresh with
    // each owned visual; no engine calls, and no write when the source is unchanged.
    internal sealed class VisualValue<T>
    {
        private bool assigned;
        private T previous;
        internal void Set(T value, Action<T> write, Func<T, T, bool> same = null)
        {
            if (assigned && (same == null ? EqualityComparer<T>.Default.Equals(previous, value) : same(previous, value))) return;
            write(value); previous = value; assigned = true;
        }
    }
    internal static class VisualTolerance
    {
        internal static bool Near(float a, float b) => Math.Abs(a - b) <= .0001f;
    }
}
