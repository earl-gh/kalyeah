using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Status effects on one player (port of status_effects.gd). Only ONE status is active at a time; each accepted application replaces
    /// the old status, timer and magnitude.
    /// </summary>
    public sealed class StatusEffects
    {
        public enum Type { None, Stun, Slow, Airborne, Polymorph, Bounce, Knockout, Tripped }

        public static readonly IReadOnlyDictionary<Type, string> DisplayNames = new Dictionary<Type, string>
        {
            { Type.Stun, "Stunned" }, { Type.Slow, "Slowed" }, { Type.Airborne, "Airborne" }, { Type.Polymorph, "Morphed" },
            { Type.Bounce, "Bounced" }, { Type.Knockout, "Knocked out" }, { Type.Tripped, "Tripped" },
        };

        /// <summary>Effects that stop movement and casting.</summary>
        public static readonly Type[] HardCc = { Type.Stun, Type.Airborne, Type.Bounce, Type.Knockout, Type.Tripped };

        /// <summary>What `cc_immunity` guards against: everything that stops moving or casting, polymorph included.</summary>
        public static readonly Type[] ImmunityTypes = { Type.Stun, Type.Airborne, Type.Bounce, Type.Knockout, Type.Polymorph };

        /// <summary>Seconds of immunity to a new hard crowd control once one has run out (0 = none).</summary>
        public double ImmunityLength;

        readonly OrderedDict<int, double> _timeLeft = new OrderedDict<int, double>();
        double _slow;
        double _immuneLeft;

        /// <summary>Returns false when nothing was applied (no effect, no time, or still immune to a new hard CC).</summary>
        public bool Apply(Type type, double duration, double magnitude = 0.0)
        {
            if (type == Type.None || duration <= 0.0) return false;
            if (Array.IndexOf(ImmunityTypes, type) >= 0 && _immuneLeft > 0.0 && !_timeLeft.ContainsKey((int)type)) return false;
            _timeLeft.Clear();
            _slow = 0.0;
            _timeLeft[(int)type] = duration;
            if (type == Type.Slow) _slow = magnitude;
            return true;
        }

        public void Step(double dt)
        {
            _immuneLeft = GMath.Maxf(0.0, _immuneLeft - dt);
            foreach (int type in _timeLeft.KeyList())
            {
                _timeLeft[type] = _timeLeft[type] - dt;
                if (_timeLeft[type] <= 0.0)
                {
                    _timeLeft.Remove(type);
                    if (type == (int)Type.Slow) _slow = 0.0;
                    else if (Array.IndexOf(ImmunityTypes, (Type)type) >= 0) _immuneLeft = ImmunityLength;
                }
            }
        }

        public void Clear()
        {
            _timeLeft.Clear();
            _slow = 0.0;
            _immuneLeft = 0.0;
        }

        public void ClearType(Type type)
        {
            if (_timeLeft.ContainsKey((int)type))
            {
                _timeLeft.Remove((int)type);
                if (type == Type.Slow) _slow = 0.0;
            }
        }

        public bool Has(Type type) => _timeLeft.ContainsKey((int)type);

        public double TimeLeft(Type type) => _timeLeft.TryGetValue((int)type, out double t) ? t : 0.0;

        /// <summary>Seconds of hard-CC immunity left.</summary>
        public double ImmuneLeft => _immuneLeft;

        public bool IsHardCc(Type type) => Array.IndexOf(HardCc, type) >= 0;

        public bool CanMove()
        {
            foreach (Type type in HardCc)
                if (_timeLeft.ContainsKey((int)type)) return false;
            return true;
        }

        /// <summary>Polymorph still lets you walk, but not cast.</summary>
        public bool CanCast() => CanMove() && !_timeLeft.ContainsKey((int)Type.Polymorph);

        public double SpeedMultiplier(double polymorphSpeedScale)
        {
            double multiplier = 1.0;
            if (_timeLeft.ContainsKey((int)Type.Slow)) multiplier *= 1.0 - _slow;
            if (_timeLeft.ContainsKey((int)Type.Polymorph)) multiplier *= polymorphSpeedScale;
            return multiplier;
        }

        /// <summary>Player-facing words for the active effects ("Stunned", "Slowed", ...).</summary>
        public List<string> DisplayNameList()
        {
            var words = new List<string>();
            foreach (var pair in _timeLeft) words.Add(DisplayNames.TryGetValue((Type)pair.Key, out string w) ? w : "");
            return words;
        }

        /// <summary>Enum names of the active effects ("STUN", ...).</summary>
        public List<string> ActiveNames()
        {
            var names = new List<string>();
            foreach (var pair in _timeLeft) names.Add(((Type)pair.Key).ToString().ToUpperInvariant());
            return names;
        }

        /// <summary>Active effects (type to seconds left) in order, plus the slow magnitude (for network snapshots).</summary>
        public IEnumerable<KeyValuePair<int, double>> Active => _timeLeft;
        public double SlowMagnitude => _slow;

        /// <summary>Plain data for network snapshots: {type: time_left, "slow": magnitude}.</summary>
        public OrderedDict<string, double> Snapshot()
        {
            var data = new OrderedDict<string, double>();
            foreach (var pair in _timeLeft) data[pair.Key.ToString()] = pair.Value;
            data["slow"] = _slow;
            return data;
        }

        public void Restore(OrderedDict<string, double> data)
        {
            _timeLeft.Clear();
            _slow = data.TryGetValue("slow", out double s) ? s : 0.0;
            foreach (var pair in data)
                if (int.TryParse(pair.Key, out int type)) _timeLeft[type] = pair.Value;
        }
    }
}
