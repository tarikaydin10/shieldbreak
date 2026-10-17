using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shieldbreak.Presentation.Admin;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// Keeps the admin menu (F1) truthful: every input is an action with a description, and nothing polls devices
    /// directly behind the menu's back.
    /// </summary>
    public class ControlsDocumentationTests
    {
        const string AssetPath = "Assets/_Project/Resources/Input/Shieldbreak.inputactions";
        const string TextPath = "Assets/_Project/Resources/Input/controls.de.json";

        static readonly Regex DirectDeviceAccess = new Regex(
            @"\b(Keyboard|Mouse|Gamepad|Joystick|Pointer|Touchscreen)\.current\b|\bInput\.(GetKey|GetKeyDown|GetKeyUp|GetMouseButton|GetMouseButtonDown|GetAxis|GetButton)\b");

        [Test]
        public void EveryActionHasADescriptionAndEveryMapATitle()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, AssetPath);
            var text = GameInput.ParseText(File.ReadAllText(TextPath));

            var described = new HashSet<string>(text.entries.Where(e => !string.IsNullOrWhiteSpace(e.text)).Select(e => e.action));
            var titled = new HashSet<string>(text.maps.Where(m => !string.IsNullOrWhiteSpace(m.title)).Select(m => m.map));
            var actions = asset.actionMaps.SelectMany(m => m.actions.Select(a => $"{m.name}/{a.name}")).ToList();

            var missing = actions.Where(a => !described.Contains(a)).ToList();
            Assert.That(missing, Is.Empty, $"Actions without description in {TextPath}: {string.Join(", ", missing)}");

            var stale = text.entries.Select(e => e.action).Where(a => !actions.Contains(a)).ToList();
            Assert.That(stale, Is.Empty, $"Descriptions for actions that do not exist: {string.Join(", ", stale)}");

            var untitled = asset.actionMaps.Select(m => m.name).Where(m => !titled.Contains(m)).ToList();
            Assert.That(untitled, Is.Empty, $"Action maps without title: {string.Join(", ", untitled)}");

            foreach (var map in asset.actionMaps)
            foreach (var action in map.actions)
                Assert.That(action.bindings.Count, Is.GreaterThan(0), $"{map.name}/{action.name} has no binding");
        }

        [Test]
        public void ScriptsDoNotPollDevicesDirectly()
        {
            var violations = new List<string>();
            foreach (var path in Directory.GetFiles(Path.Combine(Application.dataPath, "_Project", "Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = lines[i].Split(new[] { "//" }, System.StringSplitOptions.None)[0];
                    if (DirectDeviceAccess.IsMatch(code))
                        violations.Add($"{Path.GetFileName(path)}:{i + 1}: {lines[i].Trim()}");
                }
            }
            Assert.That(violations, Is.Empty,
                "Use an action from Shieldbreak.inputactions (via GameInput) so the admin menu lists it:\n" + string.Join("\n", violations));
        }
    }
}
