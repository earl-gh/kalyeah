using System.IO;
using NUnit.Framework;

namespace Kalyeah.Game.Tests
{
    /// <summary>The shipped data (unity/Data) as the game reads it.</summary>
    public static class Kit
    {
        static string Root => Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..");

        public static DataSource Data => new DataSource(name =>
        {
            string path = Path.Combine(Root, "Data", name + ".json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        });

        /// <summary>Steps `bridge` `ticks` times, a frame per tick.</summary>
        public static void Run(SimBridge bridge, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                bridge.Frame(bridge.TickDt);
                bridge.Step();
            }
        }
    }
}
