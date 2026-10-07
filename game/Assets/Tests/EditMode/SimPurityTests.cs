using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// Enforces CLAUDE.md 3.3 on the source level. The Sim assembly needs engine references for NativeArray and
    /// Unity.Mathematics (see DECISIONS.md 2026-10-07), so the compiler alone cannot keep UnityEngine out.
    /// </summary>
    public class SimPurityTests
    {
        static readonly (Regex pattern, string reason)[] Forbidden =
        {
            (new Regex(@"\bUnityEngine\b"), "UnityEngine is not allowed in Sim/"),
            (new Regex(@"\bMathf\."), "use Unity.Mathematics math.*"),
            (new Regex(@"\bTime\."), "use SimWorld.Tick and SimConstants.DT"),
            (new Regex(@"\bSystem\.Random\b|\bnew\s+Random\s*\("), "use SimRandom"),
            (new Regex(@"\bDateTime\b|\bStopwatch\b"), "wall-clock time is not deterministic"),
            (new Regex(@"\bPhysics\w*\."), "no PhysX queries in Sim/"),
        };

        [Test]
        public void SimSources_DoNotUseForbiddenApis()
        {
            string simRoot = Path.Combine(Application.dataPath, "_Project", "Scripts", "Sim");
            Assert.That(Directory.Exists(simRoot), simRoot);

            var violations = new List<string>();
            foreach (string file in Directory.GetFiles(simRoot, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    foreach (var (pattern, reason) in Forbidden)
                    {
                        if (pattern.IsMatch(code))
                            violations.Add($"{Path.GetFileName(file)}:{i + 1}: {reason}: {lines[i].Trim()}");
                    }
                }
            }
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        static string StripComment(string line)
        {
            int index = line.IndexOf("//", System.StringComparison.Ordinal);
            return index >= 0 ? line.Substring(0, index) : line;
        }
    }
}
