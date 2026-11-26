using System;
using System.Runtime.CompilerServices;

namespace ShopkeepingSuite
{
    internal static class AppraisalFill
    {
        internal static Func<InspectionUIManager, GameItem> Item = panel => panel.currentInspectingItem;
        internal static Func<InspectionUIManager, bool> Editing = panel => panel.isEditingAppraisalField;
        internal static Action<GameItem, int> Write = (item, value) => item.ModifyTag("APPRAISAL_DEFAULT_VALUE", state => state.Enable().SetInt(value));
        private sealed class Filled { internal bool Done; }
        private static readonly ConditionalWeakTable<GameItem, Filled> filled = new ConditionalWeakTable<GameItem, Filled>();
        // A void prefix always lets the game update the panel, including after a fault.
        internal static void Prefix(InspectionUIManager __instance)
        {
            try
            {
                if (!Settings.On("enabled") || !Settings.On("appraisal_fill") || Editing(__instance)) return;
                GameItem item = Item(__instance);
                if (item == null || !Marks.Tag(item, "APPRAISAL_ITEM")) return;
                StoreClient client = Marks.Current();
                if (client == null || client.clientIntent != StoreClient.ClientIntent.APPRAISAL_SERVICE) return;
                Filled state = filled.GetOrCreateValue(item);
                if (state.Done) return;
                int value = Marks.Appraise(item);
                if (value <= 0) return;
                state.Done = true;
                Write(item, value);
            }
            catch (Exception error) { SuiteMod.Fault("appraisal fill", error); }
        }
    }
}
