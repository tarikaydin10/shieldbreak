using Shieldbreak.Data;
using UnityEngine;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// What the main menu chose for the next duel. The DuelDirector applies it to its scenario copy when the scene
    /// loads, then clears it. Without a pending launch (scene opened directly in the editor) the asset's values apply.
    /// </summary>
    public static class DuelLaunch
    {
        public static bool Pending;
        public static int Preset = 1;
        public static int BotLevel = 3;
        public static int Opponents = 1;
        /// <summary>Bot weapon id, -1 = mixed.</summary>
        public static int BotWeapon;
        /// <summary>Weapon drawn by the hero at the start; the others stay in the loadout.</summary>
        public static int HeroWeapon;
        public static bool Spectator;
        public static bool Tutorial;

        public static void Apply(DuelScenarioDefinition scenario)
        {
            if (!Pending) return;
            Pending = false;
            scenario.activePreset = Mathf.Clamp(Preset, 0, Mathf.Max(0, scenario.presets.Length - 1));
            scenario.botLevel = Mathf.Clamp(BotLevel, 1, 5);
            scenario.opponents = Mathf.Clamp(Opponents, 1, 6);
            scenario.botWeapon = Mathf.Clamp(BotWeapon, -1, scenario.weapons.Length - 1);
            scenario.spectator = Spectator;
            var slots = new System.Collections.Generic.List<int> { Mathf.Clamp(HeroWeapon, 0, scenario.weapons.Length - 1) };
            for (int w = 0; w < scenario.weapons.Length; w++) if (w != slots[0]) slots.Add(w);
            scenario.heroLoadout = slots.ToArray();
            if (Tutorial)
            {
                // Guided duel: one patient opponent, no spectator
                scenario.spectator = false;
                scenario.opponents = 1;
                scenario.botLevel = 2;
                scenario.botWeapon = 0;
            }
        }
    }
}
