using System;
using System.Collections.Generic;

namespace ShopkeepingSuite
{
    internal static class CertificateRules
    {
        // The optional callback lets offline tests fault after any assignment.
        internal static void MoveFirst(List<(string, int, string)> entries, Action<int> afterWrite = null)
        {
            if (entries == null) return;
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].Item1 != "scavAppraisalRandom") continue;
                if (index == 0) return;
                // Allocate and read before mutation. The shape check guarantees a
                // concrete List: restoration only assigns existing slots, cannot
                // grow the list, and does not call the fault-injection callback.
                var original = new (string, int, string)[index + 1];
                entries.CopyTo(0, original, 0, original.Length);
                try
                {
                    for (int destination = index; destination > 0; destination--)
                    {
                        entries[destination] = entries[destination - 1];
                        afterWrite?.Invoke(destination);
                    }
                    entries[0] = original[index];
                    afterWrite?.Invoke(0);
                }
                catch
                {
                    for (int restore = 0; restore < original.Length; restore++)
                        entries[restore] = original[restore];
                    throw;
                }
                return;
            }
        }
    }
}
