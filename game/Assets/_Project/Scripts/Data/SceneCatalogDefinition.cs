using System;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// Everything playable, in one list (CLAUDE.md 8: every content is reachable and explained): per entry the scene,
    /// how to start it, an optional scenario that replaces the scene's own, the objective, which input maps it uses
    /// and what to check by hand. The main menu's test mode lists it, every scene shows its entry as a briefing (F2),
    /// the pause menu shows its checklist. Loaded from Resources so bootstrapped overlays find it in any scene.
    /// SceneCatalogTests fail when a build scene or a scenario asset has no entry.
    /// </summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Scene Catalog", fileName = "SceneCatalog")]
    public class SceneCatalogDefinition : ScriptableObject
    {
        public const string ResourcePath = "SceneCatalog";

        public enum LaunchKind
        {
            /// <summary>Load the scene as it is (with the entry's scenario, if set).</summary>
            Scene,
            /// <summary>Duel with the settings last chosen in the main menu.</summary>
            Duel,
            DuelTutorial,
            DuelSpectator,
        }

        [Serializable]
        public class Entry
        {
            [Tooltip("Stable key (checklist progress is stored under it).")]
            public string id;
            public string title;
            [Tooltip("Heading the entry is listed under in the test mode.")]
            public string section;
            [Tooltip("Scene name as in the build (BuildTools).")]
            public string scene;
            public LaunchKind launch;
            [Tooltip("Optional ISimScenario that replaces the scene's own (e.g. the stress test in the sandbox scene).")]
            public ScriptableObject scenario;
            [Tooltip("Show the briefing on its own when the scene starts (off where the scene has its own start banner).")]
            public bool briefingOnStart = true;
            [TextArea(2, 8)] public string objective;
            [Tooltip("Input action maps listed as the controls of this entry (names in Shieldbreak.inputactions).")]
            public string[] controlMaps = Array.Empty<string>();
            [Tooltip("What to check by hand; each line is ticked off in the test mode or the pause menu.")]
            [TextArea(1, 4)] public string[] checks = Array.Empty<string>();
        }

        [Tooltip("How long the briefing stays when a scene starts (s); F2 shows it again.")]
        [Min(0f)] public float briefingSeconds = 12f;
        public Entry[] entries = Array.Empty<Entry>();

        public static SceneCatalogDefinition Load() => Resources.Load<SceneCatalogDefinition>(ResourcePath);

        public Entry Find(string id)
        {
            foreach (var e in entries) if (e.id == id) return e;
            return null;
        }

        /// <summary>
        /// The entry describing what runs: an entry for this scene whose scenario is the running one, else the
        /// preferred entry if it belongs to the scene and has no scenario, else the scene's first entry without one.
        /// </summary>
        public Entry Resolve(string scene, ScriptableObject runningScenario, string preferredId)
        {
            Entry first = null, preferred = null;
            foreach (var e in entries)
            {
                if (e.scene != scene) continue;
                if (e.scenario != null)
                {
                    if (runningScenario != null && e.scenario == runningScenario) return e;
                    continue;
                }
                if (first == null) first = e;
                if (e.id == preferredId) preferred = e;
            }
            return preferred ?? first;
        }
    }
}
