using System.IO;
using System.Text.Json;
using NUnit.Framework;
using Kalyeah.Sim;

namespace Kalyeah.Sim.Tests
{
    /// <summary>The port's random numbers are Godot's, for every seed and call recorded in Golden/rng.json.</summary>
    public class GodotRngTests
    {
        public static string Golden(string name) => Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../Golden", name);

        [Test]
        public void EveryRecordedDrawMatchesGodot()
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Golden("rng.json")));
            int checkedDraws = 0;
            foreach (var seed in doc.RootElement.EnumerateObject())
            {
                var rng = new GodotRng(long.Parse(seed.Name));
                foreach (var draw in seed.Value.EnumerateArray())
                {
                    string op = draw.GetProperty("op").GetString();
                    var want = draw.GetProperty("v");
                    switch (op)
                    {
                        case "randi": Assert.AreEqual(want.GetInt64(), rng.Randi(), $"seed {seed.Name} draw {checkedDraws}"); break;
                        case "randf": Assert.AreEqual(want.GetDouble(), (double)rng.Randf(), 1e-7, $"seed {seed.Name} draw {checkedDraws}"); break;
                        case "randi_range": Assert.AreEqual(want.GetInt32(), rng.RandiRange(draw.GetProperty("a").GetInt32(), draw.GetProperty("b").GetInt32())); break;
                        case "randf_range": Assert.AreEqual(want.GetDouble(), (double)rng.RandfRange((float)draw.GetProperty("a").GetDouble(), (float)draw.GetProperty("b").GetDouble()), 1e-6); break;
                    }
                    checkedDraws++;
                }
            }
            Assert.AreEqual(240, checkedDraws);
        }
    }
}
