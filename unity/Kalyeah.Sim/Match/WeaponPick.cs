using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// The weapon pick (port of weapon_pick.gd): `Slots` different weapons within `GameRules.WeaponPickTime`. Tapping one more replaces the
    /// oldest pick; on timeout the missing picks are filled at random.
    /// </summary>
    public sealed class WeaponPick
    {
        public const int DefaultSlots = 2;

        public int Slots = DefaultSlots;
        public double TimeLeft;
        public double TimeTotal;
        public List<string> Picks = new List<string>();
        public bool Done;

        readonly List<string> _available;

        public WeaponPick(IList<string> available, double time, IList<string> current = null, int slots = DefaultSlots)
        {
            _available = new List<string>(available);
            Slots = GMath.Maxi(slots, 1);
            TimeLeft = time;
            TimeTotal = time;
            if (current != null)
                foreach (string id in current)
                    if (_available.Contains(id) && !Picks.Contains(id) && Picks.Count < Slots) Picks.Add(id);
        }

        /// <summary>An attack that aims itself and the first block (else the first crowd control); empty if there is no such pair.</summary>
        public static List<string> Suggestion(IList<WeaponDef> defs)
        {
            WeaponDef attack = null, support = null, fallback = null;
            foreach (WeaponDef def in defs)
            {
                if (attack == null && def.Kind == WeaponDef.WeaponKind.Attack && def.Shape == WeaponDef.WeaponShape.Targeted) attack = def;
                else if (support == null && def.Kind == WeaponDef.WeaponKind.Block) support = def;
                else if (fallback == null && def.Kind == WeaponDef.WeaponKind.CrowdControl) fallback = def;
            }
            if (support == null) support = fallback;
            var found = new List<string>();
            if (attack != null && support != null)
            {
                found.Add(attack.Id);
                found.Add(support.Id);
            }
            return found;
        }

        /// <summary>`count` unique suggestions: Attack + Block + Crowd Control, filled with the rest in order.</summary>
        public static List<string> SuggestionFor(IList<WeaponDef> defs, int count)
        {
            if (count <= DefaultSlots) return Suggestion(defs);
            WeaponDef attack = null, fallbackAttack = null, blockDef = null, crowdDef = null;
            foreach (WeaponDef def in defs)
            {
                if (attack == null && def.Kind == WeaponDef.WeaponKind.Attack && def.Shape == WeaponDef.WeaponShape.Targeted) attack = def;
                else if (fallbackAttack == null && def.Kind == WeaponDef.WeaponKind.Attack) fallbackAttack = def;
                else if (blockDef == null && def.Kind == WeaponDef.WeaponKind.Block) blockDef = def;
                else if (crowdDef == null && def.Kind == WeaponDef.WeaponKind.CrowdControl) crowdDef = def;
            }
            if (attack == null) attack = fallbackAttack;
            var found = new List<string>();
            if (attack == null) return found;
            found.Add(attack.Id);
            if (blockDef != null && !found.Contains(blockDef.Id) && found.Count < count) found.Add(blockDef.Id);
            if (crowdDef != null && !found.Contains(crowdDef.Id) && found.Count < count) found.Add(crowdDef.Id);
            foreach (WeaponDef def in defs)
            {
                if (found.Count >= count) break;
                if (!found.Contains(def.Id)) found.Add(def.Id);
            }
            if (found.Count < count) found.Clear();
            return found;
        }

        /// <summary>True when none of `ids` can hurt anyone.</summary>
        public static bool DealsNoDamage(IList<string> ids, IReadOnlyDictionary<string, WeaponDef> defs)
        {
            if (ids.Count == 0) return false;
            foreach (string id in ids)
            {
                if (!defs.TryGetValue(id, out WeaponDef def) || def == null || def.Damage > 0) return false;
            }
            return true;
        }

        public void Toggle(string id)
        {
            if (Done || !_available.Contains(id)) return;
            if (Picks.Contains(id))
            {
                Picks.Remove(id);
                return;
            }
            if (Picks.Count >= Slots) Picks.RemoveAt(0);
            Picks.Add(id);
        }

        public bool CanConfirm() => !Done && Picks.Count == Slots;

        public bool Confirm()
        {
            if (!CanConfirm()) return false;
            Done = true;
            return true;
        }

        /// <summary>Counts down; on timeout fills the empty slots and finishes. Returns `Done`.</summary>
        public bool Step(double dt, GodotRng rng)
        {
            if (Done) return true;
            TimeLeft = GMath.Maxf(TimeLeft - dt, 0.0);
            if (TimeLeft <= 0.0)
            {
                Fill(rng);
                Done = true;
            }
            return Done;
        }

        public void Fill(GodotRng rng)
        {
            var left = new List<string>();
            foreach (string id in _available)
                if (!Picks.Contains(id)) left.Add(id);
            while (Picks.Count < Slots && left.Count > 0)
            {
                int at = rng.RandiRange(0, left.Count - 1);
                Picks.Add(left[at]);
                left.RemoveAt(at);
            }
        }

        /// <summary>Replaces a selected slot; the available/unique constraints still apply.</summary>
        public bool ReplaceSlot(int slot, string id)
        {
            if (Done || slot < 0 || slot >= Picks.Count || !_available.Contains(id) || Picks.Contains(id)) return false;
            Picks[slot] = id;
            return true;
        }
    }
}
