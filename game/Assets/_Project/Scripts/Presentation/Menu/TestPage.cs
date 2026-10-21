using UnityEngine;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Test mode page of the main menu: every scene catalog entry, grouped by section, with its progress; the selected
    /// one shows objective, checklist (tick off here or in the game through Esc) and controls, and starts with one
    /// click. Meant for testing everything at the end of an iteration without knowing where it hides.
    /// </summary>
    public class TestPage
    {
        string selectedId;
        Vector2 listScroll, detailScroll;
        bool confirmResetAll;

        /// <summary>Draws the page inside an open layout group. Returns true when "Zurück" was pressed.</summary>
        public bool Draw(float height)
        {
            GUILayout.Label("TESTMODUS", MenuStyle.Heading);
            var catalog = SceneLaunch.Catalog;
            if (catalog == null || catalog.entries.Length == 0)
            {
                GUILayout.Label("Der Szenen-Katalog fehlt (Resources/SceneCatalog).", MenuStyle.Text);
                return MenuStyle.Small("Zurück", 120);
            }

            int total = 0, done = 0;
            foreach (var e in catalog.entries)
            {
                total += e.checks.Length;
                done += TestChecklist.CheckedCount(e);
            }
            GUILayout.Label($"Alles, was es gibt, mit Ziel, Steuerung und Prüfliste. Abhaken hier oder im Spiel unter Esc → Prüfliste. Geprüft: {done} von {total}.", MenuStyle.Muted);
            var selected = catalog.Find(selectedId) ?? catalog.entries[0];

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(300));
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(height));
            string section = null;
            foreach (var e in catalog.entries)
            {
                if (e.section != section)
                {
                    section = e.section;
                    GUILayout.Label(section.ToUpperInvariant(), MenuStyle.Heading);
                }
                string progress = TestChecklist.CheckedCount(e) == e.checks.Length ? "fertig" : TestChecklist.Progress(e);
                if (GUILayout.Button($"{e.title}  ({progress})", e == selected ? MenuStyle.ListItemSelected : MenuStyle.ListItem))
                {
                    selectedId = e.id;
                    detailScroll = Vector2.zero;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(16);

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(selected.title, MenuStyle.Large);
            GUILayout.FlexibleSpace();
            if (MenuStyle.Small("Starten", 140)) SceneLaunch.Start(selected);
            GUILayout.EndHorizontal();
            detailScroll = GUILayout.BeginScrollView(detailScroll, GUILayout.Height(height - 44f));
            TestChecklist.DrawObjective(selected);
            TestChecklist.DrawChecks(selected, editable: true);
            TestChecklist.DrawControls(selected);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            bool back = MenuStyle.Small("Zurück", 120);
            GUILayout.FlexibleSpace();
            if (MenuStyle.Small("Diese Prüfliste zurücksetzen", 230)) TestChecklist.Reset(selected);
            if (MenuStyle.Small(confirmResetAll ? "Wirklich alle?" : "Alle zurücksetzen", 170))
            {
                if (confirmResetAll) foreach (var e in catalog.entries) TestChecklist.Reset(e);
                confirmResetAll = !confirmResetAll;
            }
            GUILayout.EndHorizontal();
            if (back) confirmResetAll = false;
            return back;
        }
    }
}
