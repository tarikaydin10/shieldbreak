using System.Collections.Generic;
using Shieldbreak.Presentation;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Sim.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.DebugTools
{
    /// <summary>
    /// M0 sandbox control: click or drag to select agents, right-click to march them to a point and line up in ranks
    /// facing away from where they came from. Every move is a SetTargetRange command through the InputQueue.
    /// Placeholder for the FormationOrder API (M2).
    /// </summary>
    public class SandboxOrders : MonoBehaviour, IAdminStatus
    {
        const ushort LocalPlayer = 0;
        const float ClickPixels = 6f;
        const float PickPixels = 25f;

        public SimRunner runner;
        public Camera viewCamera;
        [Tooltip("Distance between soldiers in the target block (m).")]
        [Min(0.5f)] public float slotSpacing = 1.1f;
        [Tooltip("Files per rank = round(sqrt(count * this)); larger = wider, shallower block.")]
        [Min(0.25f)] public float widthFactor = 3f;
        public Color boxColor = new Color(1f, 0.85f, 0.4f, 0.9f);

        InputActionMap map;
        InputAction point, select, command, additive, teamA, teamB, all;
        bool dragging;
        Vector2 dragStart;
        Texture2D pixel;
        readonly List<int> ids = new List<int>();
        readonly List<(int id, float depth, float lateral)> sortBuffer = new List<(int, float, float)>();

        public string AdminStatusLabel => "Auswahl";
        public string AdminStatus => AgentSelection.Count == 0 ? "keine" : $"{AgentSelection.Count} Soldaten";

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
            map.Enable();
        }

        void OnDisable() => map?.Disable();

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
                TryGroundPoint(mouse, out float2 target))
                IssueMove(world, target);
        }

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
            if (ids.Count == 0 && !additive.IsPressed()) AgentSelection.Clear();
            else AgentSelection.Set(ids, additive.IsPressed());
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

        void IssueMove(SimWorld world, float2 target)
        {
            AgentSelection.CopyTo(ids);
            var agents = world.Agents;
            int n = ids.Count;

            float2 centroid = float2.zero;
            foreach (int id in ids) centroid += agents[id].Position;
            centroid /= n;
            float2 f = math.lengthsq(target - centroid) > 1f ? math.normalize(target - centroid) : agents[ids[0]].Facing;
            float2 right = new float2(f.y, -f.x);
            int files = math.clamp((int)math.round(math.sqrt(n * widthFactor)), 1, n);

            // Front-most soldiers take the front rank, left-most take the left files: few crossing paths
            sortBuffer.Clear();
            foreach (int id in ids)
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
