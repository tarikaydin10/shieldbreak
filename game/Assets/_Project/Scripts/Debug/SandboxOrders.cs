using System.Collections.Generic;
using Shieldbreak.Presentation;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Shieldbreak.DebugTools
{
    /// <summary>
    /// Sandbox control from the debug top-down view. Click or drag to select; a soldier of a formation selects the
    /// whole formation. Right-click marches the selection there, right-drag draws the front line (position, width,
    /// facing); H, R, C, X set the behavior (hold, advance, charge, retreat). Formations get FormationOrders, loose
    /// soldiers get per-soldier move targets; both go through the InputQueue. The same planner will serve the command
    /// view (M2 task 7).
    /// </summary>
    public class SandboxOrders : MonoBehaviour, IAdminStatus
    {
        const ushort LocalPlayer = 0;
        const float ClickPixels = 6f;
        const float PickPixels = 25f;

        public SimRunner runner;
        public Camera viewCamera;
        [Tooltip("Distance between loose soldiers in the target block (m).")]
        [Min(0.5f)] public float slotSpacing = 1.1f;
        [Tooltip("Loose soldiers: files per rank = round(sqrt(count * this)); larger = wider, shallower block.")]
        [Min(0.25f)] public float widthFactor = 3f;
        [Tooltip("Gap between formations placed side by side on one front line (m).")]
        [Min(0f)] public float formationGap = 2f;
        [Tooltip("Right-drag shorter than this on screen counts as a click (px).")]
        [Min(1f)] public float dragPixels = 12f;
        public Color boxColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        public Color lineColor = new Color(1f, 0.95f, 0.6f, 0.95f);

        InputActionMap map;
        InputAction point, select, command, additive, teamA, teamB, all, halt, advance, charge, retreat;
        readonly InputAction[] shapes = new InputAction[FormationLimits.TypeCount];
        bool dragging, commanding;
        Vector2 dragStart, commandStartScreen;
        float2 commandStart;
        Texture2D pixel;
        readonly List<int> ids = new List<int>();
        readonly List<int> formationIds = new List<int>();
        readonly List<int> looseIds = new List<int>();
        readonly List<(int id, float depth, float lateral)> sortBuffer = new List<(int, float, float)>();

        static readonly string[] ShapeActions = { "ShapeLine", "ShapeShieldWall", "ShapeWedge", "ShapeCircle", "ShapeLoose" };

        public string AdminStatusLabel => "Auswahl";
        public string AdminStatus
        {
            get
            {
                if (AgentSelection.Count == 0) return "keine";
                SplitSelection();
                return formationIds.Count > 0
                    ? $"{AgentSelection.Count} Soldaten, {formationIds.Count} Formation(en)"
                    : $"{AgentSelection.Count} Soldaten";
            }
        }

        /// <summary>True while a front line is being drawn (right-drag), for overlays.</summary>
        public bool IsDrawingLine { get; private set; }

        void OnEnable()
        {
            map = GameInput.Map("DebugOrders");
            point = map.FindAction("Point", true);
            select = map.FindAction("Select", true);
            command = map.FindAction("Command", true);
            additive = map.FindAction("Additive", true);
            teamA = map.FindAction("SelectTeamA", true);
            teamB = map.FindAction("SelectTeamB", true);
            all = map.FindAction("SelectAll", true);
            halt = map.FindAction("Halt", true);
            advance = map.FindAction("Advance", true);
            charge = map.FindAction("Charge", true);
            retreat = map.FindAction("Retreat", true);
            for (int t = 0; t < shapes.Length; t++) shapes[t] = map.FindAction(ShapeActions[t], true);
            map.Enable();
            RenderPipelineManager.endCameraRendering += DrawPreview;
        }

        void OnDisable()
        {
            map?.Disable();
            RenderPipelineManager.endCameraRendering -= DrawPreview;
        }

        void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }

        void Update()
        {
            var world = runner != null ? runner.World : null;
            if (world == null || viewCamera == null) return;
            Vector2 mouse = point.ReadValue<Vector2>();

            if (teamA.WasPressedThisFrame()) SelectTeam(0);
            if (teamB.WasPressedThisFrame()) SelectTeam(1);
            if (all.WasPressedThisFrame()) SelectTeam(-1);

            if (select.WasPressedThisFrame() && !AdminMenu.BlocksPointer(mouse))
            {
                dragging = true;
                dragStart = mouse;
            }
            if (dragging && select.WasReleasedThisFrame())
            {
                dragging = false;
                FinishSelection(dragStart, mouse);
            }

            if (command.WasPressedThisFrame() && !AdminMenu.BlocksPointer(mouse) && AgentSelection.Count > 0 &&
                TryGroundPoint(mouse, out commandStart))
            {
                commanding = true;
                commandStartScreen = mouse;
            }
            IsDrawingLine = commanding && Vector2.Distance(commandStartScreen, mouse) >= dragPixels;
            if (commanding && command.WasReleasedThisFrame())
            {
                commanding = false;
                bool drawn = IsDrawingLine;
                IsDrawingLine = false;
                if (TryGroundPoint(mouse, out float2 end)) IssueCommand(world, commandStart, end, drawn);
            }

            for (int t = 0; t < shapes.Length; t++)
                if (shapes[t].WasPressedThisFrame()) IssueShape(world, (FormationType)t);
            if (halt.WasPressedThisFrame()) IssueBehavior(world, FormationBehavior.Hold);
            if (advance.WasPressedThisFrame()) IssueBehavior(world, FormationBehavior.Advance);
            if (charge.WasPressedThisFrame()) IssueBehavior(world, FormationBehavior.Charge);
            if (retreat.WasPressedThisFrame()) IssueBehavior(world, FormationBehavior.Retreat);
        }

        // ---- Selection ----

        void SelectTeam(int team)
        {
            var agents = runner.World.Agents;
            ids.Clear();
            for (int i = 0; i < agents.Length; i++)
                if (team < 0 || agents[i].Team == team) ids.Add(i);
            AgentSelection.Set(ids, additive.IsPressed());
        }

        void FinishSelection(Vector2 a, Vector2 b)
        {
            var agents = runner.World.Agents;
            ids.Clear();
            var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            bool click = rect.width < ClickPixels && rect.height < ClickPixels;
            int best = -1;
            float bestDist = PickPixels;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsDead) continue;
                Vector3 s = viewCamera.WorldToScreenPoint(SimRunner.ToWorld(runner.InterpolatedPosition(i), 0.9f));
                if (s.z <= 0f) continue;
                if (click)
                {
                    float d = Vector2.Distance(s, b);
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                else if (rect.Contains(s))
                {
                    ids.Add(i);
                }
            }
            if (click && best >= 0) ids.Add(best);
            ExpandToFormations(ids);
            if (ids.Count == 0 && !additive.IsPressed()) AgentSelection.Clear();
            else AgentSelection.Set(ids, additive.IsPressed());
        }

        /// <summary>A soldier of a formation stands for the whole formation.</summary>
        void ExpandToFormations(List<int> list)
        {
            var members = runner.World.FormationMembers;
            formationIds.Clear();
            foreach (int id in list)
            {
                int f = members[id].Formation;
                if (f >= 0 && !formationIds.Contains(f)) formationIds.Add(f);
            }
            if (formationIds.Count == 0) return;
            for (int i = 0; i < members.Length; i++)
                if (members[i].Formation >= 0 && formationIds.Contains(members[i].Formation) && !list.Contains(i)) list.Add(i);
        }

        /// <summary>Selected formations (ascending id) and selected soldiers outside any formation.</summary>
        void SplitSelection()
        {
            AgentSelection.CopyTo(ids);
            formationIds.Clear();
            looseIds.Clear();
            var world = runner != null ? runner.World : null;
            if (world == null) return;
            var members = world.FormationMembers;
            foreach (int id in ids)
            {
                if (id >= members.Length || world.Agents[id].IsDead) continue;
                int f = members[id].Formation;
                if (f < 0) looseIds.Add(id);
                else if (!formationIds.Contains(f)) formationIds.Add(f);
            }
            formationIds.Sort();
        }

        // ---- Orders ----

        void IssueCommand(SimWorld world, float2 start, float2 end, bool drawn)
        {
            SplitSelection();
            if (formationIds.Count > 0)
            {
                using var fids = new NativeArray<int>(formationIds.ToArray(), Allocator.Temp);
                using var orders = new NativeArray<FormationOrder>(formationIds.Count, Allocator.Temp);
                var settings = world.FormationSettings;
                if (drawn && math.distance(start, end) >= 1f)
                    FormationLinePlanner.PlanLine(world.Formations, fids, fids.Length, start, end, true, formationGap, settings, orders);
                else
                    FormationLinePlanner.PlanPoint(world.Formations, fids, fids.Length, end, formationGap, settings, orders);
                foreach (var order in orders) world.SubmitFormationOrder(LocalPlayer, order);
            }
            if (looseIds.Count > 0) IssueLooseMove(world, end);
        }

        void IssueShape(SimWorld world, FormationType type)
        {
            SplitSelection();
            foreach (int f in formationIds) world.SubmitFormationOrder(LocalPlayer, FormationOrder.Reshape(f, type));
        }

        /// <summary>Hold stops where the formation stands; advance, charge and retreat pick their enemy in the sim.</summary>
        void IssueBehavior(SimWorld world, FormationBehavior behavior)
        {
            SplitSelection();
            foreach (int f in formationIds) world.SubmitFormationOrder(LocalPlayer, FormationOrder.SetBehavior(f, behavior));
        }

        /// <summary>Loose soldiers (M0 behaviour): march to the point and line up in ranks facing away from where they came from.</summary>
        void IssueLooseMove(SimWorld world, float2 target)
        {
            var agents = world.Agents;
            int n = looseIds.Count;

            float2 centroid = float2.zero;
            foreach (int id in looseIds) centroid += agents[id].Position;
            centroid /= n;
            float2 f = math.lengthsq(target - centroid) > 1f ? math.normalize(target - centroid) : agents[looseIds[0]].Facing;
            float2 right = new float2(f.y, -f.x);
            int files = math.clamp((int)math.round(math.sqrt(n * widthFactor)), 1, n);

            // Front-most soldiers take the front rank, left-most take the left files: few crossing paths
            sortBuffer.Clear();
            foreach (int id in looseIds)
            {
                float2 p = agents[id].Position - centroid;
                sortBuffer.Add((id, math.dot(p, f), math.dot(p, right)));
            }
            sortBuffer.Sort((x, y) => y.depth.CompareTo(x.depth) != 0 ? y.depth.CompareTo(x.depth) : x.id.CompareTo(y.id));

            for (int rankStart = 0, rank = 0; rankStart < n; rankStart += files, rank++)
            {
                int inRank = math.min(files, n - rankStart);
                sortBuffer.Sort(rankStart, inRank, Comparer<(int id, float depth, float lateral)>.Create(
                    (x, y) => x.lateral.CompareTo(y.lateral) != 0 ? x.lateral.CompareTo(y.lateral) : x.id.CompareTo(y.id)));
                for (int k = 0; k < inRank; k++)
                {
                    float lateral = (k - (inRank - 1) * 0.5f) * slotSpacing;
                    float2 slot = target + right * lateral - f * (rank * slotSpacing);
                    world.Submit(LocalPlayer, InputCommandType.SetTargetRange,
                        new int4(sortBuffer[rankStart + k].id, 1, 0, 0), new float4(slot, 0f, 0f));
                }
            }
        }

        bool TryGroundPoint(Vector2 screen, out float2 ground)
        {
            Ray ray = viewCamera.ScreenPointToRay(screen);
            ground = default;
            if (ray.direction.y >= -1e-4f) return false;
            float t = -ray.origin.y / ray.direction.y;
            Vector3 p = ray.origin + ray.direction * t;
            ground = new float2(p.x, p.z);
            return true;
        }

        // ---- Feedback ----

        /// <summary>The front line being drawn, with the facing the formations will get.</summary>
        void DrawPreview(ScriptableRenderContext context, Camera cam)
        {
            if (!IsDrawingLine || cam != viewCamera || runner == null || runner.World == null) return;
            if (!TryGroundPoint(point.ReadValue<Vector2>(), out float2 end)) return;
            SplitSelection();
            if (formationIds.Count == 0) return;

            var world = runner.World;
            using var fids = new NativeArray<int>(formationIds.ToArray(), Allocator.Temp);
            using var orders = new NativeArray<FormationOrder>(formationIds.Count, Allocator.Temp);
            FormationLinePlanner.PlanLine(world.Formations, fids, fids.Length, commandStart, end, true, formationGap, world.FormationSettings, orders);

            const float lift = 0.06f;
            DebugLines.Begin();
            DebugLines.Color(lineColor);
            DebugLines.Line(SimRunner.ToWorld(commandStart, lift), SimRunner.ToWorld(end, lift));
            for (int k = 0; k < orders.Length; k++)
            {
                var o = orders[k];
                var f = world.Formations[o.Formation];
                float half = FormationLinePlanner.FrontWidth(f.Type, o.Files, f.MemberCount, world.FormationSettings) * 0.5f;
                float2 along = new float2(o.Facing.y, -o.Facing.x);
                float2 front = f.Type == FormationType.Circle ? o.Destination + o.Facing * half : o.Destination;
                DebugLines.Line(SimRunner.ToWorld(front - along * half, lift), SimRunner.ToWorld(front + along * half, lift));
                DebugLines.Arrow(SimRunner.ToWorld(front, lift), new Vector3(o.Facing.x, 0f, o.Facing.y) * 2f, 0.5f);
            }
            DebugLines.End();
        }

        void OnGUI()
        {
            if (!dragging) return;
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }
            Vector2 mouse = point.ReadValue<Vector2>();
            // Input space has y up, GUI space y down
            var a = new Vector2(dragStart.x, Screen.height - dragStart.y);
            var b = new Vector2(mouse.x, Screen.height - mouse.y);
            var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            var old = GUI.color;
            GUI.color = new Color(boxColor.r, boxColor.g, boxColor.b, 0.12f);
            GUI.DrawTexture(r, pixel);
            GUI.color = boxColor;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), pixel);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), pixel);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), pixel);
            GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), pixel);
            GUI.color = old;
        }
    }
}
