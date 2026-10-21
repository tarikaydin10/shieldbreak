using Shieldbreak.Presentation.Player;
using UnityEngine;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>Options page of the menus: mouse sensitivity, volume, singleplayer slow motion (Z and command view), attack control scheme.</summary>
    public class OptionsPage
    {
        static readonly string[] SchemeNames = { "Einfach: tippen = leicht, halten = schwer", "Richtungen: aus der Mausbewegung" };

        /// <summary>Draws the page. Returns true when "Zurück" was pressed.</summary>
        public bool Draw()
        {
            GUILayout.Label("OPTIONEN", MenuStyle.Heading);
            float mouse = MenuStyle.Slider("Mausempfindlichkeit", GameSettings.MouseSensitivity, 0.02f, 0.5f);
            float volume = MenuStyle.Slider("Lautstärke", GameSettings.Volume, 0f, 1f);
            float slow = MenuStyle.Slider("Zeitlupe (Z)", GameSettings.SlowMotionScale, 0.05f, 1f, "0.00×");
            float command = MenuStyle.Slider("Kommandoansicht", GameSettings.CommandSlowMotionScale, 0.1f, 1f, "0.00×");
            int scheme = PlayerPrefs.GetInt(PlayerCombatInput.SchemePref, 0);
            int newScheme = MenuStyle.Chooser("Angriffs-Steuerung", scheme, SchemeNames);

            bool changed = !Mathf.Approximately(mouse, GameSettings.MouseSensitivity) || !Mathf.Approximately(volume, GameSettings.Volume) ||
                           !Mathf.Approximately(slow, GameSettings.SlowMotionScale) || !Mathf.Approximately(command, GameSettings.CommandSlowMotionScale) ||
                           newScheme != scheme;
            if (changed)
            {
                GameSettings.MouseSensitivity = mouse;
                GameSettings.Volume = volume;
                GameSettings.SlowMotionScale = slow;
                GameSettings.CommandSlowMotionScale = command;
                PlayerPrefs.SetInt(PlayerCombatInput.SchemePref, newScheme);
                foreach (var p in Object.FindObjectsByType<PlayerCombatInput>()) p.scheme = (PlayerCombatInput.Scheme)newScheme;
                GameSettings.Apply();
            }

            GUILayout.Label("Kommandoansicht: wie schnell die Zeit läuft, solange du sie offen hast (1,00× = keine Zeitlupe).", MenuStyle.Muted);
            GUILayout.Space(8);
            GUILayout.Label("Kampfgefühl (Schwer / Mittel / Tödlich), Gegner-Stufe und -Waffe stellst du beim Start eines Duells ein; im Duell wechseln F6, F7, F10, F11.", MenuStyle.Muted);
            GUILayout.Space(12);
            return MenuStyle.Small("Zurück", 120);
        }
    }
}
