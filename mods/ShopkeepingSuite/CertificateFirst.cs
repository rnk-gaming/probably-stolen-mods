using System;
using System.Collections.Generic;

namespace ShopkeepingSuite
{
    internal static class CertificateFirst
    {
        internal static Action<List<(string, int, string)>> Move = entries => CertificateRules.MoveFirst(entries);
        internal static void Postfix(List<(string, int, string)> __result)
        {
            try
            {
                if (!Settings.On("enabled") || !Settings.On("certificate_first") || __result == null) return;
                Move(__result);
            }
            catch (Exception error) { SuiteMod.Fault("certificate first", error); }
        }
    }
}
