using System.Collections.Generic;
using Shieldbreak.Presentation.Admin;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Controls page of the menus: every action map with its actions, the German description from controls.de.json,
    /// the current binding per device, and "Ändern" to rebind a key or button (Input System interactive rebinding).
    /// Overrides persist in PlayerPrefs. Pointer axes (look, scroll, position) cannot be rebound.
    /// </summary>
    public class ControlsPage
    {
        Vector2 scroll;
        InputActionRebindingExtensions.RebindingOperation rebinding;
        InputAction rebindingAction;
        int rebindingIndex = -1;
        readonly List<InputActionMap> disabledForRebind = new List<InputActionMap>();

        public bool IsRebinding => rebinding != null;

        /// <summary>Draws the page inside an open layout group. Returns true when "Zurück" was pressed.</summary>
        public bool Draw(float height, bool showOnlyGameplay)
        {
            GUILayout.Label("STEUERUNG", MenuStyle.Heading);
            GUILayout.Label(IsRebinding ? $"Drücke die neue Taste für: {GameInput.Describe(rebindingAction)} (Esc bricht ab)" : "Ändern: eine neue Taste oder Gamepad-Taste drücken. Änderungen gelten sofort und bleiben gespeichert.", MenuStyle.Muted);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
            foreach (var map in GameInput.Asset.actionMaps)
            {
                if (showOnlyGameplay && (map.name.StartsWith("Proto") || map.name.StartsWith("Debug"))) continue;
                GUILayout.Label(GameInput.MapTitleOf(map), MenuStyle.Heading);
                foreach (var action in map.actions) DrawAction(action);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            bool back = MenuStyle.Small("Zurück", 120, !IsRebinding);
            GUILayout.FlexibleSpace();
            if (MenuStyle.Small("Alle zurücksetzen", 160, !IsRebinding)) GameSettings.ResetBindings();
            GUILayout.EndHorizontal();
            return back;
        }

        void DrawAction(InputAction action)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(GameInput.Describe(action), MenuStyle.Text, GUILayout.Width(380));
            GUILayout.BeginVertical();
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite) continue;
                GUILayout.BeginHorizontal();
                string label = b.ToDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);
                if (b.isPartOfComposite) label = $"{b.name}: {label}";
                bool active = rebindingAction == action && rebindingIndex == i;
                GUILayout.Label(active ? "…" : (string.IsNullOrEmpty(label) ? "–" : label), MenuStyle.Key, GUILayout.Width(170));
                bool canRebind = !IsRebinding && CanRebind(action, b);
                if (canRebind && MenuStyle.Small("Ändern", 80)) StartRebind(action, i);
                if (!canRebind && !IsRebinding) GUILayout.Space(84);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        static bool CanRebind(InputAction action, InputBinding b)
        {
            // Buttons and composite parts (WASD) can be rebound; mouse axes (delta, scroll, position) cannot
            if (b.isPartOfComposite) return true;
            if (action.expectedControlType == "Button") return true;
            return b.effectivePath.Contains("Keyboard") || b.effectivePath.Contains("Gamepad") && !b.effectivePath.Contains("Stick");
        }

        void StartRebind(InputAction action, int index)
        {
            // Every enabled map is paused while listening, so the new key does not also fire gameplay
            disabledForRebind.Clear();
            foreach (var map in GameInput.Asset.actionMaps)
                if (map.enabled) { map.Disable(); disabledForRebind.Add(map); }

            rebindingAction = action;
            rebindingIndex = index;
            rebinding = action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => { Finish(); GameSettings.SaveBindingOverrides(); })
                .OnCancel(_ => Finish())
                .Start();
        }

        void Finish()
        {
            rebinding?.Dispose();
            rebinding = null;
            rebindingAction = null;
            rebindingIndex = -1;
            foreach (var map in disabledForRebind) map.Enable();
            disabledForRebind.Clear();
        }

        public void Cancel()
        {
            rebinding?.Cancel();
        }
    }
}
