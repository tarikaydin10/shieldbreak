using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Admin
{
    /// <summary>
    /// Single entry point for player input. Every control is an action in Resources/Input/Shieldbreak.inputactions
    /// and has a description in Resources/Input/controls.de.json, so the admin menu can always list what each
    /// input does. Features enable their own action map while they are active; direct device polling
    /// (Keyboard.current etc.) is not allowed (ControlsDocumentationTests).
    /// </summary>
    public static class GameInput
    {
        public const string AssetResource = "Input/Shieldbreak";
        public const string TextResource = "Input/controls.de";

        [Serializable]
        public class MapTitle
        {
            public string map;
            public string title;
        }

        [Serializable]
        public class Entry
        {
            public string action;
            public string text;
        }

        [Serializable]
        public class ControlsText
        {
            public MapTitle[] maps = Array.Empty<MapTitle>();
            public Entry[] entries = Array.Empty<Entry>();
        }

        static InputActionAsset asset;
        static Dictionary<string, string> descriptions;
        static Dictionary<string, string> mapTitles;

        public static InputActionAsset Asset
        {
            get
            {
                if (asset == null)
                {
                    asset = Resources.Load<InputActionAsset>(AssetResource);
                    if (asset == null) throw new InvalidOperationException($"Resources/{AssetResource}.inputactions missing.");
                    Menu.GameSettings.LoadBindingOverrides();
                }
                return asset;
            }
        }

        public static InputActionMap Map(string name) => Asset.FindActionMap(name, throwIfNotFound: true);

        public static InputAction Action(string map, string action) => Map(map).FindAction(action, throwIfNotFound: true);

        /// <summary>
        /// The keyboard binding of an action as shown to the player (e.g. "F2"), else all its bindings. The asset has
        /// no control schemes, so the keyboard binding is found by its (possibly overridden) path.
        /// </summary>
        public static string KeyboardBinding(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].effectivePath.StartsWith("<Keyboard>"))
                    return action.GetBindingDisplayString(i);
            string any = action.GetBindingDisplayString();
            return string.IsNullOrEmpty(any) ? "–" : any;
        }

        public static ControlsText ParseText(string json) => JsonUtility.FromJson<ControlsText>(json) ?? new ControlsText();

        public static string Describe(InputAction action)
        {
            EnsureText();
            return descriptions.TryGetValue($"{action.actionMap.name}/{action.name}", out var text) ? text : action.name;
        }

        public static string MapTitleOf(InputActionMap map)
        {
            EnsureText();
            return mapTitles.TryGetValue(map.name, out var title) ? title : map.name;
        }

        static void EnsureText()
        {
            if (descriptions != null) return;
            var file = Resources.Load<TextAsset>(TextResource);
            var parsed = file != null ? ParseText(file.text) : new ControlsText();
            descriptions = new Dictionary<string, string>();
            foreach (var e in parsed.entries) descriptions[e.action] = e.text;
            mapTitles = new Dictionary<string, string>();
            foreach (var m in parsed.maps) mapTitles[m.map] = m.title;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload may be disabled in the editor; start every play session from a clean state
            asset = null;
            descriptions = null;
            mapTitles = null;
        }
    }
}
