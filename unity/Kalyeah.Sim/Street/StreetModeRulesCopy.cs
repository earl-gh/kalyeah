using System.Collections.Generic;
using System.Reflection;

namespace Kalyeah.Sim
{
    /// <summary>GDScript's `StreetModeRules.duplicate()` (StreetModeRules.cs has no copy of its own): tests and tools change a copy, never the
    /// shared rules.</summary>
    public static class StreetModeRulesCopy
    {
        /// <summary>A copy of every number; the platform list is a new list of the same rects.</summary>
        public static StreetModeRules Duplicate(this StreetModeRules rules)
        {
            var copy = new StreetModeRules();
            foreach (FieldInfo field in typeof(StreetModeRules).GetFields(BindingFlags.Public | BindingFlags.Instance))
                field.SetValue(copy, field.GetValue(rules));
            copy.LangitSpots = new List<Rect2>(rules.LangitSpots);
            return copy;
        }
    }
}
