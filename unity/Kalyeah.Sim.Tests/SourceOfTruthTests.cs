using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// Keeps the owner's rule alive (CLAUDE.md "The five sources of truth"): the five design files exist, every agent entry file
    /// names them, and no other design doc grows beside them without the owner's say.
    /// </summary>
    public sealed class SourceOfTruthTests
    {
        static readonly string[] Five = { "docs/SPEC.md", "docs/KITS.md", "docs/MAPS.md", "docs/LORE.md", "docs/GRAPHICS.md" };

        /// <summary>The only Markdown files allowed directly in docs/. Add one only when the owner agrees to it.</summary>
        static readonly string[] AllowedDocs =
        {
            "SPEC.md", "KITS.md", "MAPS.md", "LORE.md", "GRAPHICS.md",   // the five
            "GDD.md", "ART.md", "ROADMAP.md", "DOS_AND_DONTS.md",         // what is built, the order, the mistake log
        };

        static readonly string[] AllowedDocFolders = { "art-briefs" };

        static string Root()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md"))) dir = Path.GetDirectoryName(dir);
            Assert.IsNotNull(dir, "CLAUDE.md not found above the test directory");
            return dir;
        }

        [Test]
        public void TheFiveSourcesOfTruthExist()
        {
            foreach (string path in Five)
                Assert.IsTrue(File.Exists(Path.Combine(Root(), path)), path + " is a source of truth and must exist");
        }

        [TestCase("CLAUDE.md")]
        [TestCase("AGENTS.md")]
        [TestCase("GEMINI.md")]
        public void EveryAgentEntryFileNamesAllFive(string entry)
        {
            string text = File.ReadAllText(Path.Combine(Root(), entry));
            foreach (string path in Five)
                StringAssert.Contains(path, text, entry + " must name " + path + " (the owner's source-of-truth rule)");
        }

        [Test]
        public void NoCompetingDesignDocAppears()
        {
            string docs = Path.Combine(Root(), "docs");
            string[] files = Directory.GetFiles(docs, "*.md").Select(Path.GetFileName).ToArray();
            string[] extra = files.Except(AllowedDocs).ToArray();
            Assert.IsEmpty(extra, "design docs outside the allowed list: " + string.Join(", ", extra)
                + ". Record the answer in SPEC, KITS, MAPS, LORE or GRAPHICS, or get the owner's OK and list the file in this test.");

            string[] folders = Directory.GetDirectories(docs).Select(Path.GetFileName).Except(AllowedDocFolders).ToArray();
            Assert.IsEmpty(folders, "doc folders outside the allowed list: " + string.Join(", ", folders));
        }

        [Test]
        public void NoDocOrSkillPointsAtADeletedDoc()
        {
            string root = Root();
            string[] gone = { "BARANGAY_AAA_MODEL_QUALITY", "QA_AUDIT_SCORECARD", "docs/audits" };
            foreach (string dir in new[] { "docs", ".claude", ".github" })
                foreach (string file in Directory.GetFiles(Path.Combine(root, dir), "*.md", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    foreach (string name in gone)
                        Assert.IsFalse(text.Contains(name), Path.GetRelativePath(root, file) + " still points at the deleted doc " + name);
                }
        }
    }
}
