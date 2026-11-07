using Shieldbreak.Presentation.Menu;
using Shieldbreak.Sim.AI;
using Shieldbreak.Sim.Core;
using UnityEngine;

namespace Shieldbreak.Presentation
{
    /// <summary>
    /// Debug view of the AI commanders (CLAUDE.md 3.7: utility scores always visible): tactic, what it reads off the
    /// field and how the tactics scored at its last decision, so a defeat can be understood.
    /// </summary>
    public static class CommanderPanel
    {
        public static readonly string[] TacticNames = { "Halten", "Frontalangriff", "Flanke links", "Flanke rechts", "Rückzug" };

        static GUIStyle line;

        public static string TacticName(CommanderTactic tactic) => TacticNames[Mathf.Clamp((int)tactic, 0, TacticNames.Length - 1)];

        /// <summary>The first commander's tactic, or null without one.</summary>
        public static string Summary(SimWorld world) => world.Commanders.Length > 0 ? TacticName(world.Commanders[0].Tactic) : null;

        public static void Draw(SimWorld world, Texture2D pixel)
        {
            if (world.Commanders.Length == 0) return;
            Vector2 screen = MenuStyle.BeginScaled();
            if (line == null) line = new GUIStyle(MenuStyle.Text) { fontSize = 15, wordWrap = false };
            float y = 110f;
            for (int c = 0; c < world.Commanders.Length; c++)
            {
                var state = world.Commanders[c];
                var f = state.Features;
                var box = new Rect(16f, y, 340f, 262f);
                Fill(box, new Color(0f, 0f, 0f, 0.6f), pixel);
                float row = y + 8f;
                Row($"KOMMANDANT · {TacticName(state.Tactic)} seit {(world.Tick - state.TacticTick) * SimConstants.DT:0} s", ref row, MenuStyle.Accent);
                float best = 0.001f;
                for (int t = 0; t < CommanderUtility.TacticCount; t++) best = Mathf.Max(best, state.Scores[(CommanderTactic)t]);
                for (int t = 0; t < CommanderUtility.TacticCount; t++)
                {
                    var tactic = (CommanderTactic)t;
                    float score = state.Scores[tactic];
                    Fill(new Rect(150f, row + 5f, 190f * Mathf.Clamp01(score / best), 12f), tactic == state.Tactic ? MenuStyle.Accent : MenuStyle.Dim, pixel);
                    Row($"{TacticName(tactic)}  {score:0.00}", ref row, tactic == state.Tactic ? Color.white : MenuStyle.Dim);
                }
                row += 4f;
                Row($"Stärke {f.OwnFighters} : {f.EnemyFighters} ({f.Ratio:0.00})", ref row, MenuStyle.Fg);
                Row($"Moral {f.OwnMorale * 100f:0} % : {f.EnemyMorale * 100f:0} %", ref row, MenuStyle.Fg);
                Row($"Front {f.OwnFront:0} m : {f.EnemyFront:0} m · Abstand {f.Distance:0} m", ref row, MenuStyle.Fg);
                Row($"Feind offen links {f.OpenLeft * 100f:0} %, rechts {f.OpenRight * 100f:0} %", ref row, MenuStyle.Fg);
                y += box.height + 8f;
            }
            GUI.matrix = Matrix4x4.identity;
        }

        static void Row(string text, ref float y, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.Label(new Rect(26f, y, 330f, 22f), text, line);
            GUI.color = old;
            y += 22f;
        }

        static void Fill(Rect r, Color color, Texture2D pixel)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, pixel);
            GUI.color = old;
        }
    }
}
