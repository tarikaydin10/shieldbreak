using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Ticked checks of the scene catalog, kept in PlayerPrefs per entry and check text: a reworded check counts as
    /// new and starts unticked. Also draws an entry's objective, controls and checklist for the test mode, the pause
    /// menu and the briefing, so all three read the same.
    /// </summary>
    public static class TestChecklist
    {
        const string Prefix = "test.check.";

        public static bool IsChecked(SceneCatalogDefinition.Entry entry, int index) =>
            PlayerPrefs.GetInt(Key(entry, index), 0) == 1;

        public static void Set(SceneCatalogDefinition.Entry entry, int index, bool value)
        {
            if (value) PlayerPrefs.SetInt(Key(entry, index), 1);
            else PlayerPrefs.DeleteKey(Key(entry, index));
        }

        public static int CheckedCount(SceneCatalogDefinition.Entry entry)
        {
            int n = 0;
            for (int i = 0; i < entry.checks.Length; i++) if (IsChecked(entry, i)) n++;
            return n;
        }

        public static void Reset(SceneCatalogDefinition.Entry entry)
        {
            for (int i = 0; i < entry.checks.Length; i++) Set(entry, i, false);
        }

        public static string Progress(SceneCatalogDefinition.Entry entry) => $"{CheckedCount(entry)}/{entry.checks.Length}";

        // FNV-1a: string.GetHashCode is not guaranteed to be stable between runs
        static string Key(SceneCatalogDefinition.Entry entry, int index)
        {
            uint hash = 2166136261;
            foreach (char c in entry.checks[index])
            {
                hash ^= c;
                hash *= 16777619;
            }
            return $"{Prefix}{entry.id}.{hash:x8}";
        }

        /// <summary>Objective as a paragraph (inside an open layout group).</summary>
        public static void DrawObjective(SceneCatalogDefinition.Entry entry)
        {
            GUILayout.Label("ZIEL", MenuStyle.Heading);
            GUILayout.Label(entry.objective, MenuStyle.Text);
        }

        /// <summary>Every action of the entry's input maps with its current binding and description.</summary>
        public static void DrawControls(SceneCatalogDefinition.Entry entry, float keyWidth = 150f)
        {
            GUILayout.Label("STEUERUNG", MenuStyle.Heading);
            foreach (var mapName in entry.controlMaps)
            {
                var map = GameInput.Asset.FindActionMap(mapName);
                if (map == null) continue;
                GUILayout.Label(GameInput.MapTitleOf(map), MenuStyle.Muted);
                foreach (var action in map.actions)
                {
                    string binding = action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);
                    MenuStyle.Row(string.IsNullOrEmpty(binding) ? "–" : binding, GameInput.Describe(action), keyWidth);
                }
            }
            GUILayout.Label($"Immer: {Binding("Pause")} Pause (mit Prüfliste) · {Binding("ToggleAdminMenu")} Status und Admin · {Binding("Help")} Hilfe ein/aus", MenuStyle.Muted);
        }

        /// <summary>Keyboard binding of an always-on action (Global map), as shown to the player.</summary>
        public static string Binding(string globalAction) => GameInput.KeyboardBinding(GameInput.Action("Global", globalAction));

        /// <summary>Checklist; with <paramref name="editable"/> each line is a toggle, else a read-only mark.</summary>
        public static void DrawChecks(SceneCatalogDefinition.Entry entry, bool editable)
        {
            GUILayout.Label($"PRÜFLISTE {Progress(entry)}", MenuStyle.Heading);
            for (int i = 0; i < entry.checks.Length; i++)
            {
                bool done = IsChecked(entry, i);
                if (editable)
                {
                    bool next = GUILayout.Toggle(done, " " + entry.checks[i], MenuStyle.Toggle);
                    if (next != done) Set(entry, i, next);
                }
                else GUILayout.Label((done ? "[x] " : "[  ] ") + entry.checks[i], done ? MenuStyle.Muted : MenuStyle.Text);
            }
        }
    }
}
