namespace Kalyeah.Sim
{
    /// <summary>
    /// Godot 4's RandomNumberGenerator (core/math/random_pcg.h: PCG32 with Godot's increment), bit for bit, so the port draws the very
    /// numbers the Godot game drew for the same seed (checked against unity/Golden/rng.json).
    /// </summary>
    public sealed class GodotRng
    {
        const ulong DefaultInc = 1442695040888963407UL;
        const ulong DefaultSeed = 12047754176567800795UL;
        ulong _state;
        ulong _inc;
        long _seed;

        public GodotRng() { Seed = unchecked((long)DefaultSeed); }

        public GodotRng(long seed) { Seed = seed; }

        /// <summary>Godot's `rng.seed = value` (pcg32_srandom_r with Godot's default increment).</summary>
        public long Seed
        {
            get => _seed;
            set
            {
                _seed = value;
                _state = 0UL;
                _inc = (DefaultInc << 1) | 1UL;
                Next();
                _state = unchecked(_state + (ulong)value);
                Next();
            }
        }

        /// <summary>The generator's state (Godot's `rng.state`), to save and restore a sequence.</summary>
        public ulong State { get => _state; set => _state = value; }

        uint Next()
        {
            ulong old = _state;
            _state = unchecked(old * 6364136223846793005UL + _inc);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        uint Bounded(uint bound)
        {
            uint threshold = unchecked((uint)(-bound)) % bound;
            while (true)
            {
                uint r = Next();
                if (r >= threshold) return r % bound;
            }
        }

        /// <summary>Godot's `randi()`: 0 to 2^32 - 1.</summary>
        public long Randi() => Next();

        /// <summary>Godot's `randf()`: [0, 1] as a 32-bit float (two draws, exponent from the first).</summary>
        public float Randf()
        {
            uint protoExpOffset = Next();
            if (protoExpOffset == 0) return 0f;
            int clz = Clz32(protoExpOffset);
            return LdexpF((float)(Next() | 0x80000001u), -32 - clz);
        }

        /// <summary>Godot's `randf_range(from, to)`.</summary>
        public float RandfRange(float from, float to) => Randf() * (to - from) + from;

        /// <summary>Godot's `randi_range(from, to)` (inclusive, either order).</summary>
        public int RandiRange(int from, int to)
        {
            if (from == to) return from;
            long min = System.Math.Min(from, to), max = System.Math.Max(from, to);
            uint diff = (uint)(max - min);
            if (diff == uint.MaxValue) return unchecked((int)(Next() + min));
            return (int)(Bounded(diff + 1) + min);
        }

        static int Clz32(uint x)
        {
            int n = 0;
            if (x == 0) return 32;
            while ((x & 0x80000000u) == 0) { n++; x <<= 1; }
            return n;
        }

        static float LdexpF(float value, int exp) => (float)(value * System.Math.Pow(2.0, exp));
    }
}
