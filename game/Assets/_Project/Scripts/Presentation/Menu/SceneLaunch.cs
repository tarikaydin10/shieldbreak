using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Starts a scene catalog entry (main menu, test mode): duel variants through DuelLaunch, a replacement scenario
    /// through SimRunner.PendingScenario, then loads the scene. Remembers the entry so the briefing and the pause
    /// menu can tell the duel, the tutorial and spectating apart (they share one scene).
    /// </summary>
    public static class SceneLaunch
    {
        static SceneCatalogDefinition catalog;
        static bool catalogLoaded;

        /// <summary>Id of the entry started last, or null.</summary>
        public static string CurrentId { get; private set; }

        public static SceneCatalogDefinition Catalog
        {
            get
            {
                if (!catalogLoaded)
                {
                    catalog = SceneCatalogDefinition.Load();
                    catalogLoaded = true;
                    if (catalog == null) Debug.LogError($"Resources/{SceneCatalogDefinition.ResourcePath}.asset missing.");
                }
                return catalog;
            }
        }

        public static SceneCatalogDefinition.Entry Find(string id) => Catalog != null ? Catalog.Find(id) : null;

        /// <summary>The entry describing the running scene (see SceneCatalogDefinition.Resolve), or null.</summary>
        public static SceneCatalogDefinition.Entry Running()
        {
            if (Catalog == null) return null;
            var runner = Object.FindAnyObjectByType<SimRunner>();
            return Catalog.Resolve(SceneManager.GetActiveScene().name, runner != null ? runner.scenario : null, CurrentId);
        }

        public static void Start(SceneCatalogDefinition.Entry entry)
        {
            if (entry == null) return;
            CurrentId = entry.id;
            if (entry.launch != SceneCatalogDefinition.LaunchKind.Scene)
            {
                DuelLaunch.Tutorial = entry.launch == SceneCatalogDefinition.LaunchKind.DuelTutorial;
                DuelLaunch.Spectator = entry.launch == SceneCatalogDefinition.LaunchKind.DuelSpectator;
                DuelLaunch.Pending = true;
            }
            SimRunner.PendingScenario = entry.scenario;
            GamePause.Set(false);
            SceneManager.LoadScene(entry.scene);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload may be disabled in the editor; start every play session from a clean state
            catalog = null;
            catalogLoaded = false;
            CurrentId = null;
        }
    }
}
