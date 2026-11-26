using System;
using System.Collections.Generic;
using System.Reflection;

namespace ShelfCompat
{
    internal static class Shape
    {
        internal static string Water(Type water, MethodInfo method, Type item)
        {
            if (water == null || !water.IsVisible || !water.IsAbstract || !water.IsSealed)
                return "WaterRules is not a public static class";
            if (method == null || method.DeclaringType != water || method.Name != "IsInStoreGrid" || !method.IsPublic
                || !method.IsStatic || method.IsGenericMethod || method.ReturnType != typeof(bool))
                return "WaterRules.IsInStoreGrid is not a public static bool method";
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType == item ? null
                : "WaterRules.IsInStoreGrid does not take exactly one GameItem";
        }

        internal static string Shelves(Type shelf, PropertyInfo grids, PropertyInfo ready, Type grid)
        {
            if (shelf == null) return "ShelfManager type is missing";
            if (grids?.DeclaringType != shelf || grids.Name != "Grids" || !Readable(grids, grid.MakeArrayType(), true)) return "ShelfManager.Grids differs";
            if (ready?.DeclaringType != shelf || ready.Name != "IsReady" || !Readable(ready, typeof(bool), true)) return "ShelfManager.IsReady differs";
            return null;
        }

        private static bool Readable(PropertyInfo property, Type result, bool isStatic)
        {
            MethodInfo getter = property?.GetGetMethod();
            return property != null && property.PropertyType == result && getter != null && getter.IsStatic == isStatic
                && !getter.ContainsGenericParameters && property.GetIndexParameters().Length == 0 && getter.GetParameters().Length == 0;
        }

        internal static string BackRoom(PropertyInfo instance, PropertyInfo window, Type emporium, Type pixelWindow)
        {
            if (instance?.Name != "Instance" || !Readable(instance, emporium, true)) return "EmporiumEntry.Instance differs";
            if (window?.Name != "backInvWindow" || window.DeclaringType != emporium || !Readable(window, pixelWindow, false)) return "EmporiumEntry.backInvWindow differs";
            return null;
        }

        internal static string Game(MethodInfo method, Type node)
        {
            if (method != null && method.Name == "FindParentsType" && method.IsPublic && method.IsStatic && method.IsGenericMethodDefinition)
            {
                Type[] arguments = method.GetGenericArguments();
                if (arguments.Length != 1 || method.ReturnType != typeof(List<>).MakeGenericType(arguments[0])) return "GraphUtils.FindParentsType differs";
                ParameterInfo[] parameters = method.GetParameters();
                Type filter = typeof(Func<,>).MakeGenericType(arguments[0], typeof(bool));
                if (parameters.Length != 3 || parameters[0].ParameterType != node || parameters[1].ParameterType != filter
                    || parameters[2].ParameterType != filter || !parameters[1].IsOptional || !parameters[2].IsOptional
                    || parameters[1].DefaultValue != null || parameters[2].DefaultValue != null) return "GraphUtils.FindParentsType differs";
                try { method.MakeGenericMethod(node); return null; }
                catch (ArgumentException) { }
            }
            return "GraphUtils.FindParentsType differs";
        }
    }
}
