using Shieldbreak.Presentation.Admin;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Player options (PlayerPrefs). Components read these when they enable; the options page writes them and calls
    /// Apply so running components pick them up. Sim values (tuning, presets) are not here: those are data assets.
    /// </summary>
    public static class GameSettings
    {
        const string KeyMouse = "opt.mouse", KeyVolume = "opt.volume", KeySlowMo = "opt.slowmo", KeyCommandSlowMo = "opt.commandSlowmo",
            KeyOverrides = "input.overrides";

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat(KeyMouse, 0.12f);
            set => PlayerPrefs.SetFloat(KeyMouse, Mathf.Clamp(value, 0.02f, 0.5f));
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(KeyVolume, 0.8f);
            set => PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(value));
        }

        /// <summary>Singleplayer slow motion factor (Z); the sim runs fewer ticks per second, DT never changes.</summary>
        public static float SlowMotionScale
        {
            get => PlayerPrefs.GetFloat(KeySlowMo, 0.3f);
            set => PlayerPrefs.SetFloat(KeySlowMo, Mathf.Clamp(value, 0.05f, 1f));
        }

        /// <summary>
        /// Singleplayer time scale while the command view is open (CLAUDE.md 3.9: optional, default 0.3×, 1 = off);
        /// fewer ticks per second, DT never changes. Multiplayer (M6) ignores it.
        /// </summary>
        public static float CommandSlowMotionScale
        {
            get => PlayerPrefs.GetFloat(KeyCommandSlowMo, 0.3f);
            set => PlayerPrefs.SetFloat(KeyCommandSlowMo, Mathf.Clamp(value, 0.1f, 1f));
        }

        /// <summary>Pushes the current values into every live component that uses them.</summary>
        public static void Apply()
        {
            foreach (var p in Object.FindObjectsByType<Player.PlayerCombatInput>()) p.mouseSensitivity = MouseSensitivity;
            foreach (var a in Object.FindObjectsByType<Feedback.CombatAudio>()) a.volume = Volume;
            foreach (var r in Object.FindObjectsByType<SimRunner>()) r.slowMotionScale = SlowMotionScale;
            PlayerPrefs.Save();
        }

        // ---- Key bindings ----

        public static void SaveBindingOverrides()
        {
            PlayerPrefs.SetString(KeyOverrides, GameInput.Asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        /// <summary>Loads saved overrides into the input asset (called once when the asset is first used).</summary>
        public static void LoadBindingOverrides()
        {
            string json = PlayerPrefs.GetString(KeyOverrides, "");
            if (!string.IsNullOrEmpty(json)) GameInput.Asset.LoadBindingOverridesFromJson(json);
        }

        public static void ResetBindings()
        {
            GameInput.Asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(KeyOverrides);
            PlayerPrefs.Save();
        }
    }
}
