using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace ShopkeepingSuite
{
    internal static class HandsShapes
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        internal static Dictionary<string, MemberInfo> Members()
        {
            // Keep each declaring type literal: the game's scanner rejects computed types.
            var members = new Dictionary<string, MemberInfo>
            {
                ["GameSlotInventory.childItem"] = typeof(GameSlotInventory).GetProperty("childItem", Any),
                ["EmporiumEntry.Instance"] = (MemberInfo)typeof(EmporiumEntry).GetField("Instance", Any) ?? typeof(EmporiumEntry).GetProperty("Instance", Any),
                ["MapUIManager.Instance"] = (MemberInfo)typeof(MapUIManager).GetField("Instance", Any) ?? typeof(MapUIManager).GetProperty("Instance", Any),
                ["PlayerStore.HasInstance"] = (MemberInfo)typeof(PlayerStore).GetField("HasInstance", Any) ?? typeof(PlayerStore).GetProperty("HasInstance", Any),
                ["ReputationUIManager.Instance"] = (MemberInfo)typeof(ReputationUIManager).GetField("Instance", Any) ?? typeof(ReputationUIManager).GetProperty("Instance", Any),
                ["MapUIManager.IsOutside"] = typeof(MapUIManager).GetProperty("IsOutside", Any),
                ["GameItem.unitCount"] = typeof(GameItem).GetProperty("unitCount", Any),
                ["EmporiumEntry.afterhourPocketSlotInvLeft"] = typeof(EmporiumEntry).GetField("afterhourPocketSlotInvLeft", Any),
                ["EmporiumEntry.afterhourPocketSlotInvRight"] = typeof(EmporiumEntry).GetField("afterhourPocketSlotInvRight", Any),
                ["EmporiumEntry.afterhourPocketSlotInvBackpack"] = typeof(EmporiumEntry).GetField("afterhourPocketSlotInvBackpack", Any),
                ["EmporiumEntry.backInvinvElement"] = typeof(EmporiumEntry).GetProperty("backInvinvElement", Any),
                ["EmporiumEntry.invElement"] = typeof(EmporiumEntry).GetProperty("invElement", Any),
                ["EmporiumEntry.showcaseElement"] = typeof(EmporiumEntry).GetProperty("showcaseElement", Any)
            };
            members.Add("GameItem.GetDisplayName", typeof(GameItem).GetMethod("GetDisplayName", Any, null, new[] { typeof(bool) }, null));
            members.Add("GraphUtils.TryAcceptAll", typeof(GraphUtils).GetMethod("TryAcceptAll", Any, null, new[] { typeof(GraphNodeStorage), typeof(GameItem), typeof(int) }, null));
            members.Add("ReputationUIManager.Notify", typeof(ReputationUIManager).GetMethod("Notify", Any, null, new[] { typeof(string), typeof(string) }, null));
            return members;
        }
        internal static string Missing(string name, MemberInfo member)
        {
            if (member == null) return "missing";
            bool slot = name.StartsWith("EmporiumEntry.afterhourPocketSlotInv", StringComparison.Ordinal);
            if (slot)
                return member is FieldInfo field && field.IsPublic && !field.IsStatic && field.FieldType == typeof(GameSlotInventory)
                    ? null : "public instance GameSlotInventory field";
            if (name == "GraphUtils.TryAcceptAll" || name == "GameItem.GetDisplayName" || name == "ReputationUIManager.Notify")
            {
                Type result = name == "GraphUtils.TryAcceptAll" ? typeof(int) : name == "GameItem.GetDisplayName" ? typeof(string) : typeof(void);
                Type[] args = name == "GraphUtils.TryAcceptAll" ? new[] { typeof(GraphNodeStorage), typeof(GameItem), typeof(int) }
                    : name == "GameItem.GetDisplayName" ? new[] { typeof(bool) } : new[] { typeof(string), typeof(string) };
                return member is MethodInfo method && method.IsPublic && !method.IsGenericMethod && !method.ContainsGenericParameters
                    && method.IsStatic == (name == "GraphUtils.TryAcceptAll") && method.ReturnType == result
                    && method.GetParameters().Select(p => p.ParameterType).SequenceEqual(args) ? null : "public method signature";
            }
            bool singleton = name.EndsWith(".Instance", StringComparison.Ordinal) || name == "PlayerStore.HasInstance";
            Type expected = name switch
            {
                "EmporiumEntry.Instance" => typeof(EmporiumEntry),
                "MapUIManager.Instance" => typeof(MapUIManager),
                "ReputationUIManager.Instance" => typeof(ReputationUIManager),
                "PlayerStore.HasInstance" => typeof(bool),
                "MapUIManager.IsOutside" => typeof(bool),
                "GameItem.unitCount" => typeof(int),
                "GameSlotInventory.childItem" => typeof(GameItem),
                _ => typeof(GraphNodeStorage)
            };
            if (singleton && member is FieldInfo instance)
                return instance.IsPublic && instance.IsStatic && instance.FieldType == expected ? null : "public static singleton field";
            if (!(member is PropertyInfo property)) return "readable property missing";
            MethodInfo getter = property.GetGetMethod();
            if (getter == null || getter.IsStatic != singleton || property.GetIndexParameters().Length != 0) return "public getter/static/indexer";
            bool validType = expected == typeof(GraphNodeStorage) ? expected.IsAssignableFrom(property.PropertyType) : property.PropertyType == expected;
            return validType ? null : "property type";
        }
        internal static string MissingAll()
        {
            foreach (var member in Members())
            {
                string difference = Missing(member.Key, Shapes.HandsMember(member.Key, member.Value));
                if (difference != null) return member.Key + " " + difference;
            }
            return null;
        }
    }
}
