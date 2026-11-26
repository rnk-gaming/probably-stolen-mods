using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ShopkeepingSuite
{
    internal sealed class Target
    {
        internal string Feature, Name;
        internal MethodInfo Method, Patch;
        internal Type Returns;
        internal bool Static;
        internal bool Prefix;
        internal bool SearchTraversal;
        internal Type[] Arguments;
    }
    internal sealed class PriceField
    {
        internal string Name;
        internal FieldInfo Field;
        internal Type Type;
    }
    internal static class Shapes
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        internal const BindingFlags DragFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal const string Owner = "mod.rnk.shopkeeping-suite";
        internal static readonly List<string> Unavailable = new List<string>();
        internal static Func<Target, MethodInfo> Lookup = target => target.Method;
        internal static Func<string, MemberInfo, MemberInfo> HandsMember = (name, member) => member;
        internal static Func<FieldInfo> ReputationListField = () => typeof(PlayerStore).GetField("storeReputations", BindingFlags.Public | BindingFlags.Instance);
        internal static Func<FieldInfo> ReputationIdField = () => typeof(StoreReputation).GetField("factionId", BindingFlags.Public | BindingFlags.Instance);
        internal static Func<PropertyInfo> TabletInstance = () => typeof(EmporiumEntry).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        internal static Func<FieldInfo> TabletInteractables = () => typeof(EmporiumEntry).GetField("storeInteractables", BindingFlags.Public | BindingFlags.Instance);
        internal static Func<FieldInfo> TabletPhysical = () => typeof(EmporiumEntry).GetField("storePhysical", BindingFlags.Public | BindingFlags.Instance);
        internal static Func<FieldInfo> TabletDayText = () => typeof(StoreCalendar).GetField("dayTMP", BindingFlags.Public | BindingFlags.Instance);
        internal static Func<Type> TabletType = () => typeof(StoreTablet);
        internal static Func<FieldInfo> InspectionPanel = () => typeof(InspectionUIManager).GetField("panel", BindingFlags.Public | BindingFlags.Instance);
        internal static Func<FieldInfo> InspectionItem = () => typeof(InspectionUIManager).GetField("currentInspectingItem", BindingFlags.Public | BindingFlags.Instance);
        internal static Func<PropertyInfo> PriceInstance = () => typeof(StoreStation).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        internal static List<PriceField> PriceFields()
        {
            PriceField Field(string name, FieldInfo field, Type type) => new PriceField { Name = name, Field = field, Type = type };
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            return new List<PriceField>
            {
                Field("StoreStation.storeEventManager", typeof(StoreStation).GetField("storeEventManager", flags), typeof(StoreEventManager)),
                Field("StoreEvent.displayName", typeof(StoreEvent).GetField("displayName", flags), typeof(string)),
                Field("StoreEvent.isHidden", typeof(StoreEvent).GetField("isHidden", flags), typeof(bool)),
                Field("StoreEvent.negociationDatas", typeof(StoreEvent).GetField("negociationDatas", flags), typeof(List<NegociationData>)),
                Field("NegociationData.itemType", typeof(NegociationData).GetField("itemType", flags), typeof(string)),
                Field("NegociationData.value", typeof(NegociationData).GetField("value", flags), typeof(int)),
                Field("NegociationData.reason", typeof(NegociationData).GetField("reason", flags), typeof(string))
            };
        }
        internal static string MissingPriceFields(List<PriceField> fields)
        {
            foreach (PriceField field in fields)
            {
                string missing = MissingPublicTabletField(field.Field, field.Type);
                if (missing != null) return field.Name + " " + missing;
            }
            return null;
        }
        internal static string MissingPrice()
        {
            PropertyInfo instance = PriceInstance(); MethodInfo getter = instance?.GetGetMethod();
            if (instance == null || instance.PropertyType != typeof(StoreStation) || getter == null || !getter.IsStatic || instance.GetIndexParameters().Length != 0)
                return "StoreStation.Instance property type/public getter/static";
            return MissingPriceFields(PriceFields());
        }
        internal static MethodInfo ServiceFind = typeof(StoreService).GetMethod("FindServiceByID", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
        internal static FieldInfo ServiceDisplayName = typeof(StoreService).GetField("displayName", BindingFlags.Public | BindingFlags.Instance);
        internal static Action<Target> Install = target => new Harmony(Owner).Patch(target.Method,
            prefix: target.Prefix ? new HarmonyMethod(target.Patch) : null,
            postfix: target.Prefix ? null : new HarmonyMethod(target.Patch));
        internal static void Unpatch() => new Harmony(Owner).UnpatchAll(Owner);
        private static Target M(string feature, string name, MethodInfo method, Type returns, bool isStatic, MethodInfo patch, params Type[] args)
            => new Target { Feature = feature, Name = name, Method = method, Returns = returns, Static = isStatic, Patch = patch, Arguments = args };
        internal static string Missing(Target target)
        {
            MethodInfo method = Lookup(target);
            if (method == null) return target.Name + " missing";
            if (target.SearchTraversal)
            {
                if (!method.IsPublic) return target.Name + " visibility";
                if (target.Name == "GraphUtils.FindChildrenType")
                {
                    if (!method.IsGenericMethod || method.IsGenericMethodDefinition || method.GetGenericArguments().Length != 1
                        || method.GetGenericArguments()[0] != typeof(GameItem)) return target.Name + " generic shape";
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 3 || parameters[0].IsOptional || !parameters[1].IsOptional || !parameters[2].IsOptional
                        || parameters[1].DefaultValue != null || parameters[2].DefaultValue != null) return target.Name + " optional filters";
                }
            }
            if ((target.Feature == "certificate_first" || target.Feature == "price_data" || target.Feature == "counterfeit_check" || target.Feature == "appraisal_fill" || target.Feature == "reputation_feed" || target.Feature == "standing_data" || target.Feature == "service_names") && !method.IsPublic) return target.Name + " visibility";
            if (method.ContainsGenericParameters) return target.Name + " generic parameters";
            if (method.ReturnType != target.Returns) return target.Name + " return type";
            if (method.IsStatic != target.Static) return target.Name + " static/instance";
            if (!method.GetParameters().Select(p => p.ParameterType).SequenceEqual(target.Arguments)) return target.Name + " parameters";
            return null;
        }
        internal static string MissingField(FieldInfo field, Type expected, bool isStatic)
            => field == null ? "field missing" : field.FieldType != expected ? "field type" : field.IsStatic != isStatic ? "field static/instance" : null;
        private static void Reject(string feature, string difference)
        {
            if (difference == null || Settings.Disabled.Contains(feature)) return;
            Settings.Disabled.Add(feature); Unavailable.Add(feature + ": " + difference);
            try { SuiteMod.Log("[Shopkeeping Suite] " + feature + " unavailable: " + difference); } catch (Exception) { }
            string[] affected = feature switch
            {
                "marks_data" => new[] { "mark_same_kind", "mark_risk", "mark_inspection", "mark_wanted", "mark_wanted_inside", "mark_price" },
                "price_data" => new[] { "mark_price", "tooltip_price" },
                "risk_data" => new[] { "mark_risk", "tooltip_risk", "mark_inspection" },
                "ownership_data" => new[] { "mark_same_kind", "mark_risk", "mark_inspection", "mark_wanted", "mark_wanted_inside", "tooltip_wanted", "tooltip_risk", "tooltip_appraisal", "appraisal_fill", "wanted_data" },
                "runtime" => new[] { "marks_data", "mark_same_kind", "mark_risk", "mark_inspection", "mark_wanted", "mark_wanted_inside", "mark_price", "tooltip_price", "window", "reputation_feed", "tablet_feed" },
                "location_data" => new[] { "marks_data", "mark_same_kind", "mark_risk", "mark_inspection", "mark_wanted", "mark_wanted_inside", "mark_price", "tooltip_price", "tooltip_wanted", "tooltip_risk", "tooltip_appraisal", "appraisal_fill", "customer_data" },
                "customer_data" => new[] { "mark_wanted", "mark_wanted_inside", "tooltip_wanted", "mark_inspection", "tooltip_appraisal", "appraisal_fill" },
                "wanted_data" => new[] { "mark_wanted", "mark_wanted_inside", "tooltip_wanted" },
                "shop_data" => new[] { "mark_same_kind", "mark_wanted", "mark_wanted_inside", "mark_inspection", "mark_price", "wanted_data" },
                "ledger_data" => new[] { "ledger" },
                _ => Array.Empty<string>()
            };
            foreach (string key in affected) Settings.Disabled.Add(key);
        }
        private static void F(string feature, string name, FieldInfo field, Type expected, bool isStatic = false)
        {
            string missing = MissingField(field, expected, isStatic); if (missing != null) Reject(feature, name + " " + missing);
        }
        private static void P(string feature, string name, PropertyInfo property, Type expected, bool isStatic = false)
        {
            if (property == null || property.PropertyType != expected || property.GetGetMethod(true) == null || property.GetGetMethod(true).IsStatic != isStatic) Reject(feature, name + " property type/getter/static");
        }
        private static void S(string feature, string name, FieldInfo field, PropertyInfo property, Type expected)
        {
            if (field != null) F(feature, name, field, expected, true);
            else P(feature, name, property, expected, true);
        }
        internal static List<Target> Targets() => new List<Target>
        {
            M("certificate_first", "ShowcaseHelper.GetDisplayCasePrediction", AccessTools.Method(typeof(ShowcaseHelper), "GetDisplayCasePrediction", Type.EmptyTypes), typeof(List<ValueTuple<string, int, string>>), true, typeof(CertificateFirst).GetMethod("Postfix", Any)),
            new Target { Feature = "appraisal_fill", Name = "InspectionUIManager.UpdateAppraisalPanel",
                Method = AccessTools.Method(typeof(InspectionUIManager), "UpdateAppraisalPanel", Type.EmptyTypes),
                Returns = typeof(void), Static = false, Prefix = true, Arguments = Type.EmptyTypes,
                Patch = typeof(AppraisalFill).GetMethod("Prefix", Any) },
            M("counterfeit_check", "InspectionUIManager.OpenInjectorConfrontPanel", AccessTools.Method(typeof(InspectionUIManager), "OpenInjectorConfrontPanel", new[] { typeof(GameItem) }), typeof(void), false, typeof(Counterfeit).GetMethod("Postfix", Any), typeof(GameItem)),
            M("counterfeit_check", "InspectionUIManager.OpenCigaretteConfrontPanel", AccessTools.Method(typeof(InspectionUIManager), "OpenCigaretteConfrontPanel", new[] { typeof(GameItem) }), typeof(void), false, typeof(Counterfeit).GetMethod("Postfix", Any), typeof(GameItem)),
            M("counterfeit_check", "InspectionUIManager.OpenStampConfrontPanel", AccessTools.Method(typeof(InspectionUIManager), "OpenStampConfrontPanel", new[] { typeof(GameItem) }), typeof(void), false, typeof(Counterfeit).GetMethod("Postfix", Any), typeof(GameItem)),
            M("marks_data", "AnimalMarkHelper.GetMarkColor", AccessTools.Method(typeof(AnimalMarkHelper), "GetMarkColor", new[] { typeof(GameItem) }), typeof(Color?), true, typeof(Marks).GetMethod("ColourPostfix", Any), typeof(GameItem)),
            M("marks_data", "GameItemElement.ValidateSelect", AccessTools.Method(typeof(GameItemElement), "ValidateSelect", new[] { typeof(bool) }), typeof(void), false, typeof(Marks).GetMethod("Paint", Any), typeof(bool)),
            M("mark_same_kind", "ItemMouseDragHandler.StartDrag", AccessTools.Method(typeof(ItemMouseDragHandler), "StartDrag", Type.EmptyTypes), typeof(void), false, typeof(Patches).GetMethod("DragStart", Any)),
            M("mark_same_kind", "ItemMouseDragHandler.EndDrag", AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", new[] { typeof(bool) }), typeof(void), false, typeof(Patches).GetMethod("DragEnd", Any), typeof(bool)),
            M("mark_same_kind", "ItemMouseDragHandler.AbortDrag", AccessTools.Method(typeof(ItemMouseDragHandler), "AbortDrag", Type.EmptyTypes), typeof(void), false, typeof(Patches).GetMethod("DragEnd", Any)),
            M("counter_patch", "StoreClientInstance.CreateClientInstance", AccessTools.Method(typeof(StoreClientInstance), "CreateClientInstance", new[] { typeof(StoreClient) }), typeof(StoreClientInstance), true, typeof(Patches).GetMethod("Arrived", Any), typeof(StoreClient)),
            M("wanted_data", "PlayerStore.SellItem", AccessTools.Method(typeof(PlayerStore), "SellItem", new[] { typeof(GameItem) }), typeof(void), false, typeof(Patches).GetMethod("Sale", Any), typeof(GameItem)),
            M("wanted_data", "PlayerStore.BuyItem", AccessTools.Method(typeof(PlayerStore), "BuyItem", new[] { typeof(GameItem) }), typeof(void), false, typeof(Patches).GetMethod("Sale", Any), typeof(GameItem)),
            M("event.OnItemSold", "RunStats.OnItemSold", AccessTools.Method(typeof(RunStats), "OnItemSold", new[] { typeof(GameItem), typeof(int), typeof(int), typeof(StoreClient) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(GameItem), typeof(int), typeof(int), typeof(StoreClient)),
            M("event.OnItemBought", "RunStats.OnItemBought", AccessTools.Method(typeof(RunStats), "OnItemBought", new[] { typeof(GameItem), typeof(int), typeof(StoreClient) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(GameItem), typeof(int), typeof(StoreClient)),
            M("event.OnServicePaid", "RunStats.OnServicePaid", AccessTools.Method(typeof(RunStats), "OnServicePaid", new[] { typeof(string), typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(string), typeof(int)),
            M("event.OnRentPaid", "RunStats.OnRentPaid", AccessTools.Method(typeof(RunStats), "OnRentPaid", new[] { typeof(int), typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(int), typeof(int)),
            M("event.OnMortgagePayment", "RunStats.OnMortgagePayment", AccessTools.Method(typeof(RunStats), "OnMortgagePayment", new[] { typeof(int), typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(int), typeof(int)),
            M("event.OnPermitBought", "RunStats.OnPermitBought", AccessTools.Method(typeof(RunStats), "OnPermitBought", new[] { typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(int)),
            M("event.OnPermitBribe", "RunStats.OnPermitBribe", AccessTools.Method(typeof(RunStats), "OnPermitBribe", new[] { typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(int)),
            M("event.OnInspectorBribe", "RunStats.OnInspectorBribe", AccessTools.Method(typeof(RunStats), "OnInspectorBribe", new[] { typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(int)),
            M("event.OnInspection", "RunStats.OnInspection", AccessTools.Method(typeof(RunStats), "OnInspection", new[] { typeof(bool), typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(bool), typeof(int)),
            M("event.OnBurglary", "RunStats.OnBurglary", AccessTools.Method(typeof(RunStats), "OnBurglary", new[] { typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(int)),
            M("event.OnVendingSale", "RunStats.OnVendingSale", AccessTools.Method(typeof(RunStats), "OnVendingSale", new[] { typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(int)),
            M("event.OnFountainSale", "RunStats.OnFountainSale", AccessTools.Method(typeof(RunStats), "OnFountainSale", new[] { typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(int)),
            M("event.OnWildUpgradeBought", "RunStats.OnWildUpgradeBought", AccessTools.Method(typeof(RunStats), "OnWildUpgradeBought", new[] { typeof(int) }), typeof(void), true, typeof(Patches).GetMethod("Money", Any), typeof(int))
        };
        internal static List<Target> Helpers() => new List<Target>
        {
            Search("GraphUtils.FindChildrenType", SearchChildren(), typeof(List<GameItem>), true, typeof(GraphNodeStorage), typeof(Func<GameItem, bool>), typeof(Func<GameItem, bool>)),
            M("mark_wanted_inside", "GraphNodeStorage.children", typeof(GraphNodeStorage).GetProperty("children", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod(), typeof(List<GraphNodeStorage>), false, null),
            Search("GameItem.parentInventory", typeof(GameItem).GetProperty("parentInventory", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod(), typeof(GameInventory), false),
            Search("GameInventory.GetParentItem", typeof(GameInventory).GetMethod("GetParentItem", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null), typeof(GameItem), false),
            Search("GameItem.identifier", typeof(GameItem).GetProperty("identifier", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod(), typeof(string), false),
            M("price_data", "StoreEventManager.GetActiveEvents", AccessTools.Method(typeof(StoreEventManager), "GetActiveEvents", Type.EmptyTypes), typeof(List<StoreEvent>), false, null),
            M("price_data", "GameItem.IsTag", AccessTools.Method(typeof(GameItem), "IsTag", new[] { typeof(string) }), typeof(bool), false, null, typeof(string)),
            M("price_data", "GameItem.identifier", typeof(GameItem).GetProperty("identifier", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod(), typeof(string), false, null),
            M("price_data", "StoreStation.GetDayCounter", AccessTools.Method(typeof(StoreStation), "GetDayCounter", Type.EmptyTypes), typeof(int), true, null),
            M("service_names", "StoreService.FindServiceByID", ServiceFind, typeof(StoreService), true, null, typeof(string)),
            M("reputation_feed", "StoreReputation.GetReputationExact", AccessTools.Method(typeof(StoreReputation), "GetReputationExact", Type.EmptyTypes), typeof(double), false, null),
            M("reputation_feed", "StoreReputation.GetFactionNameByID", AccessTools.Method(typeof(StoreReputation), "GetFactionNameByID", new[] { typeof(string) }), typeof(string), true, null, typeof(string)),
            M("reputation_feed", "ReputationUIManager.Notify", AccessTools.Method(typeof(ReputationUIManager), "Notify", new[] { typeof(string), typeof(string) }), typeof(void), false, null, typeof(string), typeof(string)),
            M("appraisal_fill", "GameItem.IsTag", AccessTools.Method(typeof(GameItem), "IsTag", new[] { typeof(string) }), typeof(bool), false, null, typeof(string)),
            M("appraisal_fill", "AppraisalHelper.GetCorrectBaseValue", AccessTools.Method(typeof(AppraisalHelper), "GetCorrectBaseValue", new[] { typeof(GameItem) }), typeof(int), true, null, typeof(GameItem)),
            M("appraisal_fill", "GameItem.GetTagReadonly", AccessTools.Method(typeof(GameItem), "GetTagReadonly", new[] { typeof(string) }), typeof(TagState), false, null, typeof(string)),
            M("appraisal_fill", "TagState.GetInt", AccessTools.Method(typeof(TagState), "GetInt", Type.EmptyTypes), typeof(int), false, null),
            M("appraisal_fill", "GameItem.ModifyTag", AccessTools.Method(typeof(GameItem), "ModifyTag", new[] { typeof(string), typeof(Action<TagState>), typeof(bool) }), typeof(GameItem), false, null, typeof(string), typeof(Action<TagState>), typeof(bool)),
            M("appraisal_fill", "TagState.Enable", AccessTools.Method(typeof(TagState), "Enable", Type.EmptyTypes), typeof(TagState), false, null),
            M("appraisal_fill", "TagState.SetInt", AccessTools.Method(typeof(TagState), "SetInt", new[] { typeof(int) }), typeof(TagState), false, null, typeof(int)),
            M("counterfeit_check", "GameItem.IsTag", AccessTools.Method(typeof(GameItem), "IsTag", new[] { typeof(string) }), typeof(bool), false, null, typeof(string)),
            M("counterfeit_check", "RenderHandler.GetColorFromPalette", AccessTools.Method(typeof(RenderHandler), "GetColorFromPalette", new[] { typeof(RenderHandler.ColorPalette) }), typeof(Color32), true, null, typeof(RenderHandler.ColorPalette)),
            M("marks_data", "GameItemElement.ValidateSelect", AccessTools.Method(typeof(GameItemElement), "ValidateSelect", new[] { typeof(bool) }), typeof(void), false, null, typeof(bool)),
            M("marks_data", "GameItemElement.IsDestroyed", AccessTools.Method(typeof(GameItemElement), "IsDestroyed", Type.EmptyTypes), typeof(bool), false, null),
            M("marks_data", "RenderHandler.GetColorFromPalette", AccessTools.Method(typeof(RenderHandler), "GetColorFromPalette", new[] { typeof(RenderHandler.ColorPalette) }), typeof(Color32), true, null, typeof(RenderHandler.ColorPalette)),
            M("risk_data", "ContrabandHelper.IsFlaggableItem", AccessTools.Method(typeof(ContrabandHelper), "IsFlaggableItem", new[] { typeof(GameItem) }), typeof(bool), true, null, typeof(GameItem)),
            M("ownership_data", "GeneralHelper.IsItemOwned", AccessTools.Method(typeof(GeneralHelper), "IsItemOwned", new[] { typeof(GameItem) }), typeof(bool), true, null, typeof(GameItem)),
            M("mark_risk", "GameItem.IsTag", AccessTools.Method(typeof(GameItem), "IsTag", new[] { typeof(string) }), typeof(bool), false, null, typeof(string)),
            M("mark_inspection", "GameItem.IsTag", AccessTools.Method(typeof(GameItem), "IsTag", new[] { typeof(string) }), typeof(bool), false, null, typeof(string)),
            M("tooltip_risk", "GameItem.IsTag", AccessTools.Method(typeof(GameItem), "IsTag", new[] { typeof(string) }), typeof(bool), false, null, typeof(string)),
            M("tooltip_appraisal", "GameItem.IsTag", AccessTools.Method(typeof(GameItem), "IsTag", new[] { typeof(string) }), typeof(bool), false, null, typeof(string)),
            M("shop_data", "PlayerStore.FindAllItem", AccessTools.Method(typeof(PlayerStore), "FindAllItem", new[] { typeof(bool) }), typeof(List<GameItem>), false, null, typeof(bool)),
            M("shop_data", "GameItemElement.IsDestroyed", AccessTools.Method(typeof(GameItemElement), "IsDestroyed", Type.EmptyTypes), typeof(bool), false, null),
            M("risk_data", "GameItem.GetTagReadonly", AccessTools.Method(typeof(GameItem), "GetTagReadonly", new[] { typeof(string) }), typeof(TagState), false, null, typeof(string)),
            M("risk_data", "TagState.GetInt", AccessTools.Method(typeof(TagState), "GetInt", Type.EmptyTypes), typeof(int), false, null),
            M("risk_data", "TagState.GetString", AccessTools.Method(typeof(TagState), "GetString", Type.EmptyTypes), typeof(string), false, null),
            M("wanted_data", "StoreClient.IsClientBuyingThisItem", AccessTools.Method(typeof(StoreClient), "IsClientBuyingThisItem", new[] { typeof(GameItem) }), typeof(bool), false, null, typeof(GameItem)),
            M("wanted_data", "GameItem.GetNegociatedValue", AccessTools.Method(typeof(GameItem), "GetNegociatedValue", Type.EmptyTypes), typeof(long), false, null),
            M("customer_data", "StoreClient.GetBudget", AccessTools.Method(typeof(StoreClient), "GetBudget", Type.EmptyTypes), typeof(int), false, null),
            M("customer_data", "StoreClientInstance.GetClientBlueprint", AccessTools.Method(typeof(StoreClientInstance), "GetClientBlueprint", Type.EmptyTypes), typeof(StoreClient), false, null),
            M("tooltip_appraisal", "AppraisalHelper.GetCorrectBaseValue", AccessTools.Method(typeof(AppraisalHelper), "GetCorrectBaseValue", new[] { typeof(GameItem) }), typeof(int), true, null, typeof(GameItem)),
            M("ledger_data", "StoreStation.GetDayCounter", AccessTools.Method(typeof(StoreStation), "GetDayCounter", Type.EmptyTypes), typeof(int), true, null),
            M("ledger_data", "ModHelper.GetModData", AccessTools.Method(typeof(ModHelper), "GetModData", new[] { typeof(string), typeof(string), typeof(string) }), typeof(string), true, null, typeof(string), typeof(string), typeof(string)),
            M("ledger_data", "ModHelper.SetModData", AccessTools.Method(typeof(ModHelper), "SetModData", new[] { typeof(string), typeof(string), typeof(string) }), typeof(void), true, null, typeof(string), typeof(string), typeof(string)),
            M("standing_data", "StoreReputation.GetReputationExact", AccessTools.Method(typeof(StoreReputation), "GetReputationExact", Type.EmptyTypes), typeof(double), false, null),
            M("standing_data", "StoreReputation.GetReputation", AccessTools.Method(typeof(StoreReputation), "GetReputation", Type.EmptyTypes), typeof(int), false, null),
            M("standing_data", "StoreReputation.GetReputationStatus", AccessTools.Method(typeof(StoreReputation), "GetReputationStatus", new[] { typeof(int) }), typeof(string), true, null, typeof(int)),
            M("window", "CustomUIManager.Window", AccessTools.Method(typeof(CustomUIManager), "Window", new[] { typeof(string), typeof(string), typeof(string) }), typeof(CustomUIBuilder), true, null, typeof(string), typeof(string), typeof(string)),
            M("window", "CustomUIManager.GetWindow", AccessTools.Method(typeof(CustomUIManager), "GetWindow", new[] { typeof(string) }), typeof(CustomUIWindow), false, null, typeof(string)),
            M("window", "CustomUIManager.CloseWindow", AccessTools.Method(typeof(CustomUIManager), "CloseWindow", new[] { typeof(string) }), typeof(void), false, null, typeof(string)),
            M("window", "CustomUIBuilder.SetSize", AccessTools.Method(typeof(CustomUIBuilder), "SetSize", new[] { typeof(float), typeof(float) }), typeof(CustomUIBuilder), false, null, typeof(float), typeof(float)),
            M("window", "CustomUIBuilder.Center", AccessTools.Method(typeof(CustomUIBuilder), "Center", Type.EmptyTypes), typeof(CustomUIBuilder), false, null),
            M("window", "CustomUIBuilder.SetDraggable", AccessTools.Method(typeof(CustomUIBuilder), "SetDraggable", new[] { typeof(bool) }), typeof(CustomUIBuilder), false, null, typeof(bool)),
            M("window", "CustomUIBuilder.BeginRow", AccessTools.Method(typeof(CustomUIBuilder), "BeginRow", new[] { typeof(float), typeof(TextAnchor) }), typeof(CustomUIBuilder), false, null, typeof(float), typeof(TextAnchor)),
            M("window", "CustomUIBuilder.AddButton", AccessTools.Method(typeof(CustomUIBuilder), "AddButton", new[] { typeof(string), typeof(Action), typeof(string) }), typeof(CustomUIBuilder), false, null, typeof(string), typeof(Action), typeof(string)),
            M("window", "CustomUIBuilder.BeginScroll", AccessTools.Method(typeof(CustomUIBuilder), "BeginScroll", new[] { typeof(float) }), typeof(CustomUIBuilder), false, null, typeof(float)),
            M("window", "CustomUIBuilder.AddLabel", AccessTools.Method(typeof(CustomUIBuilder), "AddLabel", new[] { typeof(string), typeof(string) }), typeof(CustomUIBuilder), false, null, typeof(string), typeof(string)),
            M("window", "CustomUIBuilder.End", AccessTools.Method(typeof(CustomUIBuilder), "End", Type.EmptyTypes), typeof(CustomUIBuilder), false, null),
            M("window", "CustomUIBuilder.AddCloseButton", AccessTools.Method(typeof(CustomUIBuilder), "AddCloseButton", new[] { typeof(Action), typeof(string) }), typeof(CustomUIBuilder), false, null, typeof(Action), typeof(string)),
            M("window", "CustomUIBuilder.Show", AccessTools.Method(typeof(CustomUIBuilder), "Show", Type.EmptyTypes), typeof(CustomUIWindow), false, null),
            M("localisation", "ModHelper.GetLocalized", AccessTools.Method(typeof(ModHelper), "GetLocalized", new[] { typeof(string), typeof(string), typeof(object[]) }), typeof(string), true, null, typeof(string), typeof(string), typeof(object[])),
            M("key_data", "ModHelper.AddGameOptionKeyBind", AccessTools.Method(typeof(ModHelper), "AddGameOptionKeyBind", new[] { typeof(string), typeof(string), typeof(string), typeof(Func<string>), typeof(Func<KeyCode>), typeof(Action<KeyCode>), typeof(Func<string>) }), typeof(void), true, null, typeof(string), typeof(string), typeof(string), typeof(Func<string>), typeof(Func<KeyCode>), typeof(Action<KeyCode>), typeof(Func<string>)),
            M("runtime", "ModHelper.StartCoroutine", AccessTools.Method(typeof(ModHelper), "StartCoroutine", new[] { typeof(string), typeof(System.Collections.IEnumerator) }), typeof(Coroutine), true, null, typeof(string), typeof(System.Collections.IEnumerator)),
            M("runtime", "ModHelper.StopAllCoroutines", AccessTools.Method(typeof(ModHelper), "StopAllCoroutines", new[] { typeof(string) }), typeof(void), true, null, typeof(string))
        };
        private static Target Search(string name, MethodInfo method, Type returns, bool isStatic, params Type[] args)
        {
            Target target = M("mark_inspection", name, method, returns, isStatic, null, args);
            target.SearchTraversal = true; return target;
        }
        private static MethodInfo SearchChildren()
        {
            // Resolve safely before any traversal. Ambiguous overloads also mean fallback.
            MethodInfo[] methods = typeof(GraphUtils).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "FindChildrenType" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1).ToArray();
            if (methods.Length != 1) return null;
            try { return methods[0].MakeGenericMethod(typeof(GameItem)); }
            catch (Exception) { return null; }
        }
        private static void CheckSearch(Target target, string difference)
        {
            if (difference == null) return;
            InspectionSearch.Available = false;
            if (target.Name == "GraphUtils.FindChildrenType") Reject("mark_wanted_inside", difference);
            // This optional extension must leave the old own-tags aid available.
            SuiteMod.Fault("inspection traversal unavailable; using the item's own tags only", new InvalidOperationException(difference));
        }
        internal static void VerifyFields()
        {
            try { Reject("hands_home", HandsShapes.MissingAll()); }
            catch (Exception error) { Reject("hands_home", error.Message); }
            try { Reject("price_data", MissingPrice()); }
            catch (Exception error) { Reject("price_data", error.Message); }
            Reject("service_names", MissingPublicTabletField(ServiceDisplayName, typeof(string)));
            try { Reject("tablet_feed", MissingTablet()); }
            catch (Exception error) { Reject("tablet_feed", error.Message); }
            F("reputation_feed", "PlayerStore.storeReputations", ReputationListField(), typeof(List<StoreReputation>));
            F("reputation_feed", "StoreReputation.factionId", ReputationIdField(), typeof(string));
            const BindingFlags singleton = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            S("reputation_feed", "PlayerStore.HasInstance", typeof(PlayerStore).GetField("HasInstance", singleton), typeof(PlayerStore).GetProperty("HasInstance", singleton), typeof(bool));
            S("reputation_feed", "PlayerStore.Instance", typeof(PlayerStore).GetField("Instance", singleton), typeof(PlayerStore).GetProperty("Instance", singleton), typeof(PlayerStore));
            S("reputation_feed", "ReputationUIManager.Instance", typeof(ReputationUIManager).GetField("Instance", singleton), typeof(ReputationUIManager).GetProperty("Instance", singleton), typeof(ReputationUIManager));
            S("appraisal_fill", "InspectionUIManager.Instance", typeof(InspectionUIManager).GetField("Instance", Any), typeof(InspectionUIManager).GetProperty("Instance", Any), typeof(InspectionUIManager));
            F("appraisal_fill", "InspectionUIManager.appraisalPanel", typeof(InspectionUIManager).GetField("appraisalPanel", BindingFlags.Public | BindingFlags.Instance), typeof(GameObject));
            F("appraisal_fill", "InspectionUIManager.currentInspectingItem", typeof(InspectionUIManager).GetField("currentInspectingItem", BindingFlags.Public | BindingFlags.Instance), typeof(GameItem));
            F("appraisal_fill", "InspectionUIManager.isEditingAppraisalField", typeof(InspectionUIManager).GetField("isEditingAppraisalField", BindingFlags.Public | BindingFlags.Instance), typeof(bool));
            F("counterfeit_check", "InspectionUIManager.confrontPanel", typeof(InspectionUIManager).GetField("confrontPanel", BindingFlags.Public | BindingFlags.Instance), typeof(GameObject));
            S("counterfeit_check", "InspectionUIManager.Instance", typeof(InspectionUIManager).GetField("Instance", singleton), typeof(InspectionUIManager).GetProperty("Instance", singleton), typeof(InspectionUIManager));
            F("counterfeit_check", "InspectionUIManager.panel", InspectionPanel(), typeof(GameObject));
            F("counterfeit_check", "InspectionUIManager.currentInspectingItem", InspectionItem(), typeof(GameItem));
            F("counterfeit_check", "InspectionUIManager.confrontTitleTMP", typeof(InspectionUIManager).GetField("confrontTitleTMP", BindingFlags.Public | BindingFlags.Instance), typeof(TMPro.TextMeshProUGUI));
            FieldInfo background = AccessTools.Field(typeof(GameItemElement), "background");
            if (background != null) F("marks_data", "GameItemElement.background", background, typeof(TreeNodeRender));
            else P("marks_data", "GameItemElement.background", AccessTools.Property(typeof(GameItemElement), "background"), typeof(TreeNodeRender));
            FieldInfo image = AccessTools.Field(typeof(TreeNodeRender), "image");
            if (image != null) F("marks_data", "TreeNodeRender.image", image, typeof(UnityEngine.UI.Image));
            else P("marks_data", "TreeNodeRender.image", AccessTools.Property(typeof(TreeNodeRender), "image"), typeof(UnityEngine.UI.Image));
            F("marks_data", "PlayerStore.storeState", typeof(PlayerStore).GetField("storeState", BindingFlags.Public | BindingFlags.Instance), typeof(PlayerStore.StoreState));
            foreach (string feature in new[] { "runtime", "customer_data", "shop_data", "standing_data", "marks_data" })
            {
                S(feature, "PlayerStore.HasInstance", typeof(PlayerStore).GetField("HasInstance", Any), typeof(PlayerStore).GetProperty("HasInstance", Any), typeof(bool));
                if (feature != "runtime") S(feature, "PlayerStore.Instance", typeof(PlayerStore).GetField("Instance", Any), typeof(PlayerStore).GetProperty("Instance", Any), typeof(PlayerStore));
            }
            S("location_data", "MapUIManager.Instance", typeof(MapUIManager).GetField("Instance", Any), typeof(MapUIManager).GetProperty("Instance", Any), typeof(MapUIManager));
            S("window", "CustomUIManager.Instance", typeof(CustomUIManager).GetField("Instance", Any), typeof(CustomUIManager).GetProperty("Instance", Any), typeof(CustomUIManager));
            F("window", "CustomUIManager.LAYER_PAPER", typeof(CustomUIManager).GetField("LAYER_PAPER", Any), typeof(string), true);
            P("wanted_data", "GameItem.unitCount", typeof(GameItem).GetProperty("unitCount", Any), typeof(int));
            P("wanted_data", "GameItem.unitValue", typeof(GameItem).GetProperty("unitValue", Any), typeof(long));
            F("mark_same_kind", "ItemMouseDragHandler.currentItem", typeof(ItemMouseDragHandler).GetField("currentItem", DragFlags), typeof(GameItemElement));
            P("mark_same_kind", "GameItem.parentInventory", typeof(GameItem).GetProperty("parentInventory", Any), typeof(GameInventory));
            F("mark_same_kind", "ItemMouseDragHandler.current", typeof(ItemMouseDragHandler).GetField("current", Any), typeof(ItemMouseDragHandler), true);
            F("customer_data", "PlayerStore.currentClientInstance", typeof(PlayerStore).GetField("currentClientInstance", Any), typeof(StoreClientInstance));
            F("customer_data", "StoreClient.isSecurity", typeof(StoreClient).GetField("isSecurity", Any), typeof(bool));
            F("customer_data", "StoreClient.clientIntent", typeof(StoreClient).GetField("clientIntent", Any), typeof(StoreClient.ClientIntent));
            F("customer_data", "StoreClient.useClientBudget", typeof(StoreClient).GetField("useClientBudget", Any), typeof(bool));
            F("wanted_data", "StoreClient.useClientBudget", typeof(StoreClient).GetField("useClientBudget", Any), typeof(bool));
            F("customer_data", "StoreClient.displayName", typeof(StoreClient).GetField("displayName", Any), typeof(string));
            F("customer_data", "StoreClient.identifier", typeof(StoreClient).GetField("identifier", Any), typeof(string));
            F("customer_data", "StoreClient.clientFaction", typeof(StoreClient).GetField("clientFaction", Any), typeof(string));
            F("standing_data", "PlayerStore.storeReputations", typeof(PlayerStore).GetField("storeReputations", BindingFlags.Public | BindingFlags.Instance), typeof(List<StoreReputation>));
            F("standing_data", "StoreReputation.factionId", typeof(StoreReputation).GetField("factionId", BindingFlags.Public | BindingFlags.Instance), typeof(string));
            F("standing_data", "StoreReputation.factionDisplay", typeof(StoreReputation).GetField("factionDisplay", BindingFlags.Public | BindingFlags.Instance), typeof(string));
            P("location_data", "MapUIManager.isOutside", typeof(MapUIManager).GetProperty("isOutside", Any), typeof(bool));
            S("sale_data", "EmporiumEntry.Instance", typeof(EmporiumEntry).GetField("Instance", Any), typeof(EmporiumEntry).GetProperty("Instance", Any), typeof(EmporiumEntry));
            P("sale_data", "EmporiumEntry.invElement", typeof(EmporiumEntry).GetProperty("invElement", Any), typeof(GameGridInventory));
            P("sale_data", "EmporiumEntry.backInvinvElement", typeof(EmporiumEntry).GetProperty("backInvinvElement", Any), typeof(GameGridInventory));
            P("sale_data", "EmporiumEntry.backInvinvElementCounter", typeof(EmporiumEntry).GetProperty("backInvinvElementCounter", Any), typeof(GameGridInventory));
            P("sale_data", "EmporiumEntry.showcaseElement", typeof(EmporiumEntry).GetProperty("showcaseElement", Any), typeof(GameGridInventory));
            P("sale_data", "GraphNodeStorage.children", typeof(GraphNodeStorage).GetProperty("children", Any), typeof(List<GraphNodeStorage>));
            P("shop_data", "GraphNodeStorage.children", typeof(GraphNodeStorage).GetProperty("children", Any), typeof(List<GraphNodeStorage>));
            P("shop_data", "GameItem.identifier", typeof(GameItem).GetProperty("identifier", Any), typeof(string));
            P("window", "CustomUIWindow.IsAlive", typeof(CustomUIWindow).GetProperty("IsAlive", Any), typeof(bool));
            P("window", "CustomUIWindow.IsVisible", typeof(CustomUIWindow).GetProperty("IsVisible", Any), typeof(bool));
            P("key_data", "KeyBindHelper.SuppressHotkeys", typeof(KeyBindHelper).GetProperty("SuppressHotkeys", Any), typeof(bool), true);
        }
        internal static string MissingTablet()
        {
            PropertyInfo instance = TabletInstance();
            MethodInfo getter = instance?.GetGetMethod();
            if (instance == null || instance.PropertyType != typeof(EmporiumEntry) || getter == null || !getter.IsStatic || instance.GetIndexParameters().Length != 0)
                return "EmporiumEntry.Instance property type/public getter/static";
            string missing = MissingPublicTabletField(TabletInteractables(), typeof(GameObject));
            if (missing != null) return "EmporiumEntry.storeInteractables " + missing;
            missing = MissingPublicTabletField(TabletPhysical(), typeof(GameObject));
            if (missing != null) return "EmporiumEntry.storePhysical " + missing;
            missing = MissingPublicTabletField(TabletDayText(), typeof(TMPro.TextMeshProUGUI));
            if (missing != null) return "StoreCalendar.dayTMP " + missing;
            Type tablet = TabletType();
            if (tablet == null || tablet.IsAbstract || !typeof(MonoBehaviour).IsAssignableFrom(tablet)) return "StoreTablet component type";
            return null;
        }
        private static string MissingPublicTabletField(FieldInfo field, Type expected)
            => MissingField(field, expected, false) ?? (!field.IsPublic ? "field visibility" : null);
        internal static void Apply()
        {
            InspectionSearch.Available = true; InspectionSearch.Reset();
            WantedInside.Available = true; WantedInside.Reset();
            Settings.Disabled.Clear(); Unavailable.Clear(); VerifyFields();
            List<Target> targets = Targets();
            foreach (Target target in Helpers().Concat(targets))
            {
                try { if (target.SearchTraversal) CheckSearch(target, Missing(target)); else Reject(target.Feature, Missing(target)); }
                catch (Exception error) { if (target.SearchTraversal) CheckSearch(target, target.Name + ": " + error.Message); else Reject(target.Feature, target.Name + ": " + error.Message); }
            }
            foreach (Target target in targets)
            {
                if (Settings.Disabled.Contains(target.Feature)) continue;
                try { Install(target); }
                catch (Exception error) { Reject(target.Feature, target.Name + " patch failed: " + error.Message); }
            }
        }
    }
    internal static class Patches
    {
        internal static void DragStart() { try { Marks.BeginDrag(); } catch (Exception error) { SuiteMod.Fault("drag start patch", error); } }
        internal static void DragEnd() { try { Marks.EndDrag(); } catch (Exception error) { SuiteMod.Fault("drag end patch", error); } }
        internal static void Arrived() { try { SuiteRuntime.Arrived(); } catch (Exception error) { SuiteMod.Fault("arrival patch", error); } }
        internal static void Sale() { try { SuiteRuntime.Sale(); } catch (Exception error) { SuiteMod.Fault("sale patch", error); } }
        internal static void Money(MethodBase __originalMethod, object[] __args)
        {
            string place = "ledger event";
            try
            {
                if (!Settings.On("enabled")) return;
                string name = __originalMethod.Name;
                place = "ledger event " + name;
                if (!Settings.On("event." + name)) return;
                int index = name == "OnItemSold" || name == "OnItemBought" || name == "OnServicePaid" || name == "OnInspection" ? 1 : 0;
                int amount = (int)__args[index];
                int bought = name == "OnItemSold" ? (int)__args[2] : 0;
                string service = name == "OnServicePaid" ? (string)__args[0] : null;
                Ledger.Record(name, amount, bought, service);
                SuiteWindow.Dirty = true;
                if (name == "OnItemSold" || name == "OnItemBought") Marks.RequestAll();
            }
            catch (Exception error) { SuiteMod.Fault(place, error); }
        }
    }
}
