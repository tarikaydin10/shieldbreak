using Shieldbreak.Presentation;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Shieldbreak.DebugTools
{
    /// <summary>
    /// GL line overlay drawn after the camera renders: grid cells around the view centre and the map border, velocity
    /// arrows, the targets of selected agents, terrain (blocked and rough cells) and formations (slots, anchor,
    /// facing, planned path, destination, label with type, size and behavior).
    /// Toggles via the DebugOverlay action map.
    /// </summary>
    public class SimDebugOverlay : MonoBehaviour, IAdminStatus
    {
        public SimRunner runner;
        public Camera viewCamera;
        [Tooltip("Half size of the drawn grid patch around the view centre (cells).")]
        [Range(5, 100)] public int gridHalfExtent = 40;
        [Tooltip("Velocity arrow length per m/s.")]
        public float velocityScale = 0.6f;
        public bool showGrid = true;
        public bool showVelocity;
        public bool showTargets = true;
        public bool showFormations = true;
        public Color gridColor = new Color(1f, 1f, 1f, 0.12f);
        public Color borderColor = new Color(1f, 0.85f, 0.4f, 0.8f);
        public Color velocityColor = new Color(0.4f, 1f, 0.5f, 0.9f);
        public Color targetColor = new Color(1f, 0.85f, 0.4f, 0.6f);
        [Tooltip("Formation lines per team (A, B).")]
        public Color[] teamColors = { new Color(1f, 0.55f, 0.45f, 0.85f), new Color(0.55f, 0.75f, 1f, 0.85f) };
        public Color wallColor = new Color(0.95f, 0.35f, 0.25f, 0.8f);
        public Color roughColor = new Color(0.75f, 0.65f, 0.3f, 0.55f);
        public Color pathColor = new Color(1f, 1f, 1f, 0.7f);
        [Tooltip("Size of a slot marker (m).")]
        [Min(0.05f)] public float slotMarkerSize = 0.18f;

        InputActionMap map;
        InputAction toggleGrid, toggleVelocity, toggleTargets, toggleFormations;
        GUIStyle labelStyle;

        static readonly string[] TypeNames = { "Linie", "Schildwall", "Keil", "Kreis", "Locker" };
        static readonly string[] BehaviorNames = { "hält", "rückt vor", "greift an", "folgt", "zieht sich zurück" };

        public string AdminStatusLabel => "Overlay";
        public string AdminStatus => $"Grid {OnOff(showGrid)} · Geschwindigkeit {OnOff(showVelocity)} · Ziele {OnOff(showTargets)} · Formationen {OnOff(showFormations)}";
        static string OnOff(bool b) => b ? "an" : "aus";

        void OnEnable()
        {
            map = GameInput.Map("DebugOverlay");
            toggleGrid = map.FindAction("ToggleGrid", true);
            toggleVelocity = map.FindAction("ToggleVelocity", true);
            toggleTargets = map.FindAction("ToggleTargets", true);
            toggleFormations = map.FindAction("ToggleFormations", true);
            map.Enable();
            RenderPipelineManager.endCameraRendering += Draw;
        }

        void OnDisable()
        {
            map?.Disable();
            RenderPipelineManager.endCameraRendering -= Draw;
        }

        void Update()
        {
            if (toggleGrid.WasPressedThisFrame()) showGrid = !showGrid;
            if (toggleVelocity.WasPressedThisFrame()) showVelocity = !showVelocity;
            if (toggleTargets.WasPressedThisFrame()) showTargets = !showTargets;
            if (toggleFormations.WasPressedThisFrame()) showFormations = !showFormations;
        }

        void Draw(ScriptableRenderContext context, Camera cam)
        {
            var world = runner != null ? runner.World : null;
            if (world == null || cam != viewCamera) return;
            DebugLines.Begin();

            const float lift = 0.03f;
            var layout = world.Grid.Layout;
            float w = layout.Width * SimConstants.CellSize, h = layout.Height * SimConstants.CellSize;
            Vector3 origin = new Vector3(layout.Origin.x, lift, layout.Origin.y);

            if (showGrid)
            {
                Vector3 centre = ViewCentre(cam);
                int cx = Mathf.FloorToInt(centre.x - origin.x), cz = Mathf.FloorToInt(centre.z - origin.z);
                int x0 = Mathf.Max(0, cx - gridHalfExtent), x1 = Mathf.Min(layout.Width, cx + gridHalfExtent);
                int z0 = Mathf.Max(0, cz - gridHalfExtent), z1 = Mathf.Min(layout.Height, cz + gridHalfExtent);
                GL.Color(gridColor);
                for (int x = x0; x <= x1; x++) Line(origin + new Vector3(x, 0, z0), origin + new Vector3(x, 0, z1));
                for (int z = z0; z <= z1; z++) Line(origin + new Vector3(x0, 0, z), origin + new Vector3(x1, 0, z));
            }

            DrawTerrain(world, origin);

            GL.Color(borderColor);
            Line(origin, origin + new Vector3(w, 0, 0));
            Line(origin + new Vector3(w, 0, 0), origin + new Vector3(w, 0, h));
            Line(origin + new Vector3(w, 0, h), origin + new Vector3(0, 0, h));
            Line(origin + new Vector3(0, 0, h), origin);

            var agents = world.Agents;
            for (int i = 0; i < agents.Length; i++)
            {
                var a = agents[i];
                Vector3 p = SimRunner.ToWorld(runner.InterpolatedPosition(i), lift);
                if (showVelocity)
                {
                    GL.Color(velocityColor);
                    Line(p, p + new Vector3(a.Velocity.x, 0f, a.Velocity.y) * velocityScale);
                }
                if (showTargets && AgentSelection.IsSelected(i))
                {
                    Vector3 t = SimRunner.ToWorld(a.Target, lift);
                    GL.Color(targetColor);
                    Line(p, t);
                    Line(t + new Vector3(-0.2f, 0, -0.2f), t + new Vector3(0.2f, 0, 0.2f));
                    Line(t + new Vector3(-0.2f, 0, 0.2f), t + new Vector3(0.2f, 0, -0.2f));
                }
            }

            if (showFormations) DrawFormations(world, lift);
            DebugLines.End();
        }

        /// <summary>Blocked cells as boxed crosses, rough (expensive) cells hatched. There are no obstacle meshes before M4.</summary>
        void DrawTerrain(SimWorld world, Vector3 origin)
        {
            var layout = world.Grid.Layout;
            var cost = world.Grid.NavCost;
            for (int y = 0; y < layout.Height; y++)
            for (int x = 0; x < layout.Width; x++)
            {
                byte c = cost[y * layout.Width + x];
                if (c == WorldGridDefault) continue;
                Vector3 p = origin + new Vector3(x, 0f, y);
                if (c == Sim.Grid.WorldGrid.Impassable)
                {
                    GL.Color(wallColor);
                    Line(p, p + new Vector3(1f, 0f, 0f));
                    Line(p + new Vector3(1f, 0f, 0f), p + new Vector3(1f, 0f, 1f));
                    Line(p + new Vector3(1f, 0f, 1f), p + new Vector3(0f, 0f, 1f));
                    Line(p + new Vector3(0f, 0f, 1f), p);
                    Line(p, p + new Vector3(1f, 0f, 1f));
                    Line(p + new Vector3(1f, 0f, 0f), p + new Vector3(0f, 0f, 1f));
                }
                else
                {
                    GL.Color(roughColor);
                    Line(p + new Vector3(0f, 0f, 0.5f), p + new Vector3(0.5f, 0f, 1f));
                    Line(p + new Vector3(0.5f, 0f, 0f), p + new Vector3(1f, 0f, 0.5f));
                }
            }
        }

        const byte WorldGridDefault = Sim.Grid.WorldGrid.DefaultCost;

        /// <summary>
        /// Per formation: a marker on every slot (brighter when selected), the anchor with its facing, and while it
        /// marches a line to the destination with the front it will form there.
        /// </summary>
        void DrawFormations(SimWorld world, float lift)
        {
            var formations = world.Formations;
            var members = world.FormationMembers;
            var settings = world.FormationSettings;
            for (int i = 0; i < members.Length; i++)
            {
                var m = members[i];
                if (m.Formation < 0 || m.Slot < 0) continue;
                var f = formations[m.Formation];
                GL.Color(FormationColor(f, AgentSelection.IsSelected(i)));
                DebugLines.Cross(SimRunner.ToWorld(f.ToWorld(m.Local), lift), slotMarkerSize);
            }
            for (int id = 0; id < formations.Length; id++)
            {
                var f = formations[id];
                if (f.MemberCount == 0) continue;
                GL.Color(FormationColor(f, true));
                Vector3 anchor = SimRunner.ToWorld(f.Position, lift);
                Vector3 facing = new Vector3(f.Facing.x, 0f, f.Facing.y);
                DebugLines.Arrow(anchor, facing * 2.5f, 0.6f);
                if (math.distancesq(f.Destination, f.Position) > 0.09f)
                {
                    Vector3 dest = SimRunner.ToWorld(f.Destination, lift);
                    // The planned path (around obstacles), then the front the formation will form at its end
                    GL.Color(pathColor);
                    Vector3 from = anchor;
                    var path = world.FormationPath(id);
                    for (int k = 0; k < path.Length; k++)
                    {
                        Vector3 to = SimRunner.ToWorld(path[k], lift);
                        DebugLines.Line(from, to);
                        DebugLines.Cross(to, 0.25f);
                        from = to;
                    }
                    GL.Color(FormationColor(f, true));
                    float half = FormationLinePlanner.FrontWidth(f.Type, f.Files, f.MemberCount, settings) * 0.5f;
                    Vector3 final = new Vector3(f.TargetFacing.x, 0f, f.TargetFacing.y);
                    Vector3 along = new Vector3(f.TargetFacing.y, 0f, -f.TargetFacing.x) * half;
                    Vector3 front = f.Type == FormationType.Circle ? dest + final * half : dest;
                    DebugLines.Line(front - along, front + along);
                    DebugLines.Arrow(front, final * 1.5f, 0.4f);
                }
            }
        }

        Color FormationColor(in FormationData f, bool bright)
        {
            var c = teamColors != null && teamColors.Length > 0 ? teamColors[math.clamp(f.Team, 0, teamColors.Length - 1)] : Color.white;
            if (!bright) c.a *= 0.45f;
            return c;
        }

        /// <summary>Type, size and behavior above every formation's anchor.</summary>
        void OnGUI()
        {
            var world = runner != null ? runner.World : null;
            if (!showFormations || world == null || viewCamera == null) return;
            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 };
            var formations = world.Formations;
            for (int id = 0; id < formations.Length; id++)
            {
                var f = formations[id];
                if (f.MemberCount == 0) continue;
                Vector3 s = viewCamera.WorldToScreenPoint(SimRunner.ToWorld(f.Position, 2.2f));
                if (s.z <= 0f) continue;
                bool marching = math.distancesq(f.Destination, f.Position) > 0.09f;
                string state = f.Behavior == FormationBehavior.Hold && marching ? "marschiert" : BehaviorNames[math.clamp((int)f.Behavior, 0, BehaviorNames.Length - 1)];
                string text = $"{TypeNames[math.clamp((int)f.Type, 0, TypeNames.Length - 1)]} · {f.MemberCount} · {state}";
                var rect = new Rect(s.x - 120f, Screen.height - s.y - 11f, 240f, 22f);
                labelStyle.normal.textColor = Color.black;
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, labelStyle);
                labelStyle.normal.textColor = FormationColor(f, true);
                GUI.Label(rect, text, labelStyle);
            }
        }

        static void Line(Vector3 a, Vector3 b) => DebugLines.Line(a, b);

        static Vector3 ViewCentre(Camera cam)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (ray.direction.y >= -1e-4f) return cam.transform.position;
            return ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
        }
    }
}
