using System;

namespace Perks
{
    internal static class AugBounty
    {
        internal static Func<bool> Ready = () => PlayerStore.Instance.isAugIntroduced && !PlayerStore.Instance.isAugEnded;
        internal static Action<StoreClientManager> AddAug = manager => typeof(StoreClientManager).GetMethod("AddAugClient", Bindings.Any, null, Type.EmptyTypes, null).Invoke(manager, null);
        internal static void Generate(StoreClientManager manager)
        {
            Runtime.Guard("Aug generation", () =>
            {
                if (!Runtime.Active("aug_bounty") || !Ready() || manager == null || manager.clientStack == null) return;
                int count = 0;
                foreach (StoreClient client in manager.clientStack) if (client != null && client.isAug) count++;
                if (count == 0) return;
                int day = Runtime.Today();
                AugState state = AugState.Parse(Runtime.Read("aug"), day);
                if (state.ExtraTried) return;
                // Record the attempt before rolling or adding: a fault must not allow a reroll.
                state.ExtraTried = true;
                Runtime.Write("aug", state.Text());
                if (Runtime.Roll(Rules.AugChance) && count < Rules.AugLimit) AddAug(manager);
            });
        }
    }
}
