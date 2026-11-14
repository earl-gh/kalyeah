using System;
using System.Reflection;

namespace Kalyeah.Net
{
    /// <summary>
    /// Raises a C# event of another class (a field-like event's backing delegate), the way game_client.gd re-emits the server's signals
    /// on its mirror sim with `emit_signal`. The sim does not expose a raise method for every event (e.g. `MatchSim.Exhausted`), so the
    /// client finds the delegate by name. Keep Kalyeah.Sim from being stripped in IL2CPP builds (its README's link.xml).
    /// </summary>
    public static class EventRaiser
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>True when `target` declares an event called `eventName`.</summary>
        public static bool Has(object target, string eventName) => target != null && target.GetType().GetEvent(eventName, Flags) != null;

        /// <summary>The event's parameter types (its delegate's Invoke), or null when there is no such event.</summary>
        public static Type[] ParameterTypes(object target, string eventName)
        {
            EventInfo info = target?.GetType().GetEvent(eventName, Flags);
            if (info == null) return null;
            ParameterInfo[] ps = info.EventHandlerType.GetMethod("Invoke").GetParameters();
            var types = new Type[ps.Length];
            for (int i = 0; i < ps.Length; i++) types[i] = ps[i].ParameterType;
            return types;
        }

        /// <summary>Invokes the event's handlers with `args` (already of the parameter types). False when the event does not exist.</summary>
        public static bool Raise(object target, string eventName, params object[] args)
        {
            if (!Has(target, eventName)) return false;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(eventName, Flags | BindingFlags.DeclaredOnly);
                if (field == null) continue;
                if (field.GetValue(target) is Delegate handlers) handlers.DynamicInvoke(args);
                break;
            }
            return true;
        }

        /// <summary>snake_case signal name to the PascalCase event name the port uses (`player_tagged` is `PlayerTagged`).</summary>
        public static string Pascal(string snake)
        {
            var parts = snake.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Concat(parts);
        }
    }
}
