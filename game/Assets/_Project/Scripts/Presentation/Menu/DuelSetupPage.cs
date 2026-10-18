using Shieldbreak.Data;
using UnityEngine;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Duel setup (feel preset, bot level, opponents, weapons) on DuelLaunch, shared by the main menu and the result
    /// screen after a fight. Draw returns true when "Kampf starten" was pressed; the caller launches.
    /// </summary>
    public static class DuelSetupPage
    {
        static readonly string[] LevelNames = { "L1 · hält nur den Block", "L2 · blockt langsam", "L3 · blockt, feintet selten", "L4 · nutzt Techniken", "L5 · Perfect Blocks, Specials" };
        static readonly string[] OpponentNames = { "1 Gegner", "2 Gegner", "3 Gegner" };

        public static bool Draw(DuelScenarioDefinition duel, string startLabel = "Kampf starten")
        {
            string[] presets = PresetNames(duel);
            DuelLaunch.Preset = MenuStyle.Chooser("Kampfgefühl", DuelLaunch.Preset, presets);
            DuelLaunch.BotLevel = MenuStyle.Chooser("Gegner-Stufe", DuelLaunch.BotLevel - 1, LevelNames) + 1;
            DuelLaunch.Opponents = MenuStyle.Chooser("Gegner", Mathf.Clamp(DuelLaunch.Opponents - 1, 0, 2), OpponentNames) + 1;
            string[] weapons = WeaponNames(duel);
            var botWeapons = new string[weapons.Length + 1];
            weapons.CopyTo(botWeapons, 0);
            botWeapons[weapons.Length] = "gemischt";
            int bw = DuelLaunch.BotWeapon < 0 ? weapons.Length : Mathf.Min(DuelLaunch.BotWeapon, weapons.Length - 1);
            bw = MenuStyle.Chooser("Gegner-Waffe", bw, botWeapons);
            DuelLaunch.BotWeapon = bw >= weapons.Length ? -1 : bw;
            DuelLaunch.HeroWeapon = MenuStyle.Chooser("Deine Waffe zuerst", Mathf.Min(DuelLaunch.HeroWeapon, weapons.Length - 1), weapons);
            GUILayout.Space(6);
            GUILayout.Label("Die anderen Waffen sind im Loadout (Tasten 1–4). Höchstens zwei Gegner greifen gleichzeitig an.", MenuStyle.Muted);
            GUILayout.Space(12);
            return MenuStyle.Big(startLabel);
        }

        /// <summary>One line that says what is running, e.g. "Mittel · L3 · 1 Gegner · Schwert und Schild".</summary>
        public static string Describe(DuelScenarioDefinition s)
        {
            string preset = s.ActivePreset != null ? s.ActivePreset.name.Replace("CombatTuning_", "") : "?";
            string weapon = s.botWeapon < 0 ? "gemischte Waffen" : s.weapons.Length > s.botWeapon && s.weapons[s.botWeapon] != null ? s.weapons[s.botWeapon].displayName : "?";
            string who = s.spectator ? $"Zuschauen: L{s.spectatorHeroLevel} gegen L{s.botLevel}" : $"Gegner L{s.botLevel}";
            return $"{preset} · {who} · {s.opponents} Gegner · {weapon}";
        }

        public static string[] PresetNames(DuelScenarioDefinition duel)
        {
            if (duel == null || duel.presets.Length == 0) return new[] { "Mittel" };
            var names = new string[duel.presets.Length];
            for (int i = 0; i < names.Length; i++) names[i] = duel.presets[i] != null ? duel.presets[i].name.Replace("CombatTuning_", "") : $"Preset {i}";
            return names;
        }

        public static string[] WeaponNames(DuelScenarioDefinition duel)
        {
            if (duel == null || duel.weapons.Length == 0) return new[] { "Schwert und Schild" };
            var names = new string[duel.weapons.Length];
            for (int i = 0; i < names.Length; i++) names[i] = duel.weapons[i] != null ? duel.weapons[i].displayName : $"Waffe {i}";
            return names;
        }
    }
}
