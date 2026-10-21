using System.Collections.Generic;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Presentation.Cameras;
using Shieldbreak.Presentation.Menu;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Shieldbreak.Presentation.Player
{
    /// <summary>
    /// Command view (CLAUDE.md 3.9, M2 task 7): hold the key and the camera rises over the field; time keeps running
    /// (singleplayer: slower, option). Entering and leaving are sim inputs (HeroUnattended): meanwhile the hero falls
    /// back behind the own line and defends itself. Own formations are picked with a click or a box; a right-click
    /// marches the selection there (no selection: all), a right-drag draws the front line (position, width, facing),
    /// a right-click on an enemy formation advances on exactly that one, 1 to 5 set the shape (also circle and loose
    /// order, which the radial menu leaves out). Everything goes out as FormationOrders
    /// through the InputQueue, the same API the radial menu and the commander AI use; the radial menu orders the
    /// selection while the view is open. Every formation shows its outline, facing, type, size, order and morale.
    /// </summary>
    public class CommandView : MonoBehaviour, IAdminStatus
    {
        const ushort LocalPlayer = 0;

        public SimRunner runner;
        public Camera viewCamera;
        public CommandViewCamera commandCamera;
        [Tooltip("Gap between formations sharing one drawn front line (m).")]
        [Min(0f)] public float formationGap = 2f;
        [Tooltip("A right-drag shorter than this on screen counts as a click (px).")]
        [Min(1f)] public float dragPixels = 12f;
        [Tooltip("A click picks the formation of the nearest soldier within this distance on screen (px in 1080p scale).")]
        [Min(4f)] public float pickPixels = 28f;
        [Tooltip("Gamepad pointer speed at full stick (screen heights per second).")]
        [Min(0.05f)] public float padPointerSpeed = 0.8f;
        [Tooltip("Mouse wheel: at most one height step per this many seconds (smooth wheels and trackpads send many).")]
        [Min(0f)] public float wheelRepeatSeconds = 0.08f;
        [Tooltip("Margin around the soldiers of a formation for its outline (m).")]
        [Min(0f)] public float outlineMargin = 0.7f;
        [Tooltip("How long the confirmation of an order stays on screen (s).")]
        [Min(0f)] public float toastSeconds = 1.6f;
        public Color ownColor = new Color(0.35f, 0.72f, 1f, 0.9f);
        public Color selectedColor = new Color(1f, 0.85f, 0.35f, 1f);
        public Color enemyColor = new Color(1f, 0.38f, 0.3f, 0.9f);
        public Color pathColor = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>True while the key is held; PlayerCombatInput then leaves the hero alone and frees the cursor.</summary>
        public static bool IsOpen { get; private set; }

        static readonly List<int> selection = new List<int>();
        /// <summary>Selected own formations (ids); the radial menu orders these while the view is open.</summary>
        public static IReadOnlyList<int> Selection => selection;

        static readonly string[] TypeNames = { "Linie", "Schildwall", "Keil", "Kreis", "Locker" };
        static readonly string[] BehaviorNames = { "hält", "rückt vor", "greift an", "folgt", "zieht sich zurück" };
        static readonly string[] ShapeActions = { "ShapeLine", "ShapeShieldWall", "ShapeWedge", "ShapeCircle", "ShapeLoose" };

        InputActionMap map;
        InputAction hold, point, select, command, additive, zoom, padPointer;
        readonly InputAction[] shapes = new InputAction[FormationLimits.TypeCount];
        Vector2 pointer, lastMouse;
        bool usingPad;
        bool selecting, commanding, drawingLine;
        Vector2 selectStart, commandStartScreen;
        float2 commandStart;
        float lastWheel = -10f;
        string toast = "";
        float toastUntil;

        // Per formation, refreshed every frame: bounds of the living members in the formation frame (x right, y forward)
        float4[] bounds = new float4[0];
        int[] living = new int[0];
        float[] morale = new float[0];
        readonly List<int> scratch = new List<int>();

        Texture2D pixel;
        GUIStyle label, hint, toastStyle;

        public string AdminStatusLabel => "Kommandoansicht";
        public string AdminStatus => runner == null || runner.World == null || !runner.World.HasCommandView ? "in dieser Szene nicht verfügbar" :
            IsOpen ? $"offen · Auswahl {(selection.Count == 0 ? "keine (Befehle an alle)" : selection.Count.ToString())} · Zeit {runner.CommandTimeScale:0.00}×"
                   : $"{GameInput.KeyboardBinding(hold)} halten";

        void OnEnable()
        {
            map = GameInput.Map("CommandView");
            hold = map.FindAction("Hold", true);
            point = map.FindAction("Point", true);
            select = map.FindAction("Select", true);
            command = map.FindAction("Command", true);
            additive = map.FindAction("Additive", true);
            zoom = map.FindAction("Zoom", true);
            padPointer = map.FindAction("Cursor", true);
            for (int t = 0; t < shapes.Length; t++) shapes[t] = map.FindAction(ShapeActions[t], true);
            map.Enable();
            RenderPipelineManager.endCameraRendering += Draw;
        }

        void OnDisable()
        {
            if (IsOpen) Close(runner != null ? runner.World : null);
            selection.Clear();
            map?.Disable();
            RenderPipelineManager.endCameraRendering -= Draw;
        }

        void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }

        void Update()
        {
            var world = runner != null ? runner.World : null;
            if (world == null || runner.PlayerAgent < 0 || !world.HasCommandView)
            {
                if (IsOpen) Close(world);
                return;
            }
            bool menus = AdminMenu.IsOpen || PauseMenu.IsOpen || DuelDirector.ResultOpen || GamePause.IsPaused;
            if (!IsOpen && hold.WasPressedThisFrame() && !menus && !RadialMenu.IsOpen) Open(world);
            else if (IsOpen && !hold.IsPressed()) Close(world);
            // Outlines and labels also follow the field while the camera comes back down
            if (IsOpen || Alpha > 0f) Refresh(world);
            if (!IsOpen) return;

            // Singleplayer slow motion through fewer ticks; multiplayer (M6) will keep real time
            runner.CommandTimeScale = GameSettings.CommandSlowMotionScale;
            if (menus || RadialMenu.IsOpen)
            {
                selecting = commanding = drawingLine = false;
                return;
            }
            UpdatePointer();

            float wheel = zoom.ReadValue<float>();
            if (Mathf.Abs(wheel) > 0.01f && Time.unscaledTime - lastWheel >= wheelRepeatSeconds && commandCamera != null)
            {
                lastWheel = Time.unscaledTime;
                commandCamera.Zoom(Mathf.Sign(wheel));
            }

            if (select.WasPressedThisFrame() && !AdminMenu.BlocksPointer(pointer))
            {
                selecting = true;
                selectStart = pointer;
            }
            if (selecting && select.WasReleasedThisFrame())
            {
                selecting = false;
                FinishSelection(world, selectStart, pointer);
            }

            for (int t = 0; t < shapes.Length; t++)
                if (shapes[t].WasPressedThisFrame()) IssueShape(world, (FormationType)t);

            if (command.WasPressedThisFrame() && !AdminMenu.BlocksPointer(pointer) && TryGroundPoint(pointer, out commandStart))
            {
                commanding = true;
                commandStartScreen = pointer;
            }
            drawingLine = commanding && Vector2.Distance(commandStartScreen, pointer) >= dragPixels;
            if (commanding && command.WasReleasedThisFrame())
            {
                commanding = false;
                bool drawn = drawingLine;
                drawingLine = false;
                if (TryGroundPoint(pointer, out float2 end)) Issue(world, commandStart, end, drawn);
            }
        }

        void Open(SimWorld world)
        {
            IsOpen = true;
            world.Submit(LocalPlayer, InputCommandType.HeroUnattended, new int4(runner.PlayerAgent, 1, 0, 0));
            if (commandCamera != null) commandCamera.Active = true;
            pointer = lastMouse = point.ReadValue<Vector2>();
            usingPad = false;
            selecting = commanding = drawingLine = false;
        }

        void Close(SimWorld world)
        {
            IsOpen = false;
            if (world != null && runner.PlayerAgent >= 0)
                world.Submit(LocalPlayer, InputCommandType.HeroUnattended, new int4(runner.PlayerAgent, 0, 0, 0));
            if (commandCamera != null) commandCamera.Active = false;
            if (runner != null) runner.CommandTimeScale = 1f;
            selecting = commanding = drawingLine = false;
        }

        /// <summary>Mouse, or a pointer moved by the stick (the last device used wins).</summary>
        void UpdatePointer()
        {
            Vector2 mouse = point.ReadValue<Vector2>();
            Vector2 stick = padPointer.ReadValue<Vector2>();
            if (stick.sqrMagnitude > 0.04f)
            {
                usingPad = true;
                pointer += stick * (padPointerSpeed * Screen.height * Time.unscaledDeltaTime);
                pointer = new Vector2(Mathf.Clamp(pointer.x, 0f, Screen.width), Mathf.Clamp(pointer.y, 0f, Screen.height));
            }
            else if ((mouse - lastMouse).sqrMagnitude > 1f)
            {
                usingPad = false;
            }
            lastMouse = mouse;
            if (!usingPad) pointer = mouse;
        }

        // ---- What the view shows ----

        /// <summary>Outline bounds, living members and average morale of every formation.</summary>
        void Refresh(SimWorld world)
        {
            var formations = world.Formations;
            int n = formations.Length;
            if (bounds.Length < n)
            {
                bounds = new float4[n];
                living = new int[n];
                morale = new float[n];
            }
            for (int f = 0; f < n; f++)
            {
                bounds[f] = new float4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
                living[f] = 0;
                morale[f] = 0f;
            }
            var agents = world.Agents;
            var members = world.FormationMembers;
            bool hasMorale = world.HasMorale;
            for (int i = 0; i < agents.Length; i++)
            {
                int f = members[i].Formation;
                if (f < 0 || f >= n || agents[i].IsDead) continue;
                var fd = formations[f];
                float2 local = FormationData.InverseRotate(runner.InterpolatedPosition(i) - fd.Position, fd.Facing);
                bounds[f] = new float4(math.min(bounds[f].xy, local), math.max(bounds[f].zw, local));
                living[f]++;
                if (hasMorale) morale[f] += world.Morale[i].Morale;
            }
            for (int f = 0; f < n; f++) if (living[f] > 0) morale[f] /= living[f];
        }

        bool IsOwn(SimWorld world, int f)
        {
            var fd = world.Formations[f];
            int hero = runner.PlayerAgent;
            return fd.Owner == LocalPlayer && hero >= 0 && fd.Team == world.Agents[hero].Team;
        }

        /// <summary>Formation of the living soldier nearest to the screen point (within pickPixels), own or enemy, or -1.</summary>
        int PickFormation(SimWorld world, Vector2 screen, bool own)
        {
            var agents = world.Agents;
            var members = world.FormationMembers;
            float best = pickPixels * MenuStyle.Scale;
            int picked = -1;
            for (int i = 0; i < agents.Length; i++)
            {
                int f = members[i].Formation;
                if (f < 0 || agents[i].IsDead || IsOwn(world, f) != own) continue;
                Vector3 s = viewCamera.WorldToScreenPoint(SimRunner.ToWorld(runner.InterpolatedPosition(i), 0.9f));
                if (s.z <= 0f) continue;
                float d = Vector2.Distance(s, screen);
                if (d < best)
                {
                    best = d;
                    picked = f;
                }
            }
            return picked;
        }

        void FinishSelection(SimWorld world, Vector2 a, Vector2 b)
        {
            // The gamepad has no modifier: A toggles a formation, A on empty ground clears
            bool add = additive.IsPressed() || usingPad;
            var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            float click = 6f * MenuStyle.Scale;
            if (rect.width < click && rect.height < click)
            {
                int f = PickFormation(world, b, own: true);
                if (f < 0)
                {
                    if (!add || usingPad) selection.Clear();
                }
                else if (add)
                {
                    if (!selection.Remove(f)) selection.Add(f);
                }
                else
                {
                    selection.Clear();
                    selection.Add(f);
                }
                return;
            }

            scratch.Clear();
            var agents = world.Agents;
            var members = world.FormationMembers;
            for (int i = 0; i < agents.Length; i++)
            {
                int f = members[i].Formation;
                if (f < 0 || agents[i].IsDead || scratch.Contains(f) || !IsOwn(world, f)) continue;
                Vector3 s = viewCamera.WorldToScreenPoint(SimRunner.ToWorld(runner.InterpolatedPosition(i), 0.9f));
                if (s.z > 0f && rect.Contains(s)) scratch.Add(f);
            }
            if (!add) selection.Clear();
            foreach (int f in scratch) if (!selection.Contains(f)) selection.Add(f);
        }

        /// <summary>Selected formations still standing, or all own formations when nothing is selected.</summary>
        void Targets(SimWorld world, List<int> output)
        {
            output.Clear();
            for (int k = selection.Count - 1; k >= 0; k--)
                if (selection[k] >= world.Formations.Length || world.Formations[selection[k]].MemberCount == 0) selection.RemoveAt(k);
            if (selection.Count > 0)
            {
                output.AddRange(selection);
                output.Sort();
                return;
            }
            for (int f = 0; f < world.Formations.Length; f++)
                if (IsOwn(world, f) && world.Formations[f].MemberCount > 0) output.Add(f);
        }

        void Issue(SimWorld world, float2 start, float2 end, bool drawn)
        {
            Targets(world, scratch);
            if (scratch.Count == 0) return;
            string who = selection.Count == 0 ? "Alle" : scratch.Count == 1 ? Name(world, scratch[0]) : $"{scratch.Count} Formationen";

            // A click on an enemy formation: advance on exactly that one
            int enemy = drawn ? -1 : PickFormation(world, pointer, own: false);
            if (enemy >= 0)
            {
                foreach (int f in scratch) world.SubmitFormationOrder(LocalPlayer, FormationOrder.Against(f, FormationBehavior.Advance, enemy));
                Toast($"{who}: vorrücken gegen {Name(world, enemy)}");
                return;
            }

            using var ids = new NativeArray<int>(scratch.ToArray(), Allocator.Temp);
            using var orders = new NativeArray<FormationOrder>(scratch.Count, Allocator.Temp);
            var settings = world.FormationSettings;
            if (drawn && math.distance(start, end) >= 1f)
            {
                FormationLinePlanner.PlanLine(world.Formations, ids, ids.Length, start, end, true, formationGap, settings, orders);
                Toast($"{who}: Frontlinie");
            }
            else
            {
                FormationLinePlanner.PlanPoint(world.Formations, ids, ids.Length, end, formationGap, settings, orders);
                Toast($"{who}: hierher");
            }
            foreach (var order in orders) world.SubmitFormationOrder(LocalPlayer, order);
        }

        void IssueShape(SimWorld world, FormationType type)
        {
            Targets(world, scratch);
            if (scratch.Count == 0) return;
            foreach (int f in scratch) world.SubmitFormationOrder(LocalPlayer, FormationOrder.Reshape(f, type));
            Toast($"{(selection.Count == 0 ? "Alle" : scratch.Count == 1 ? Name(world, scratch[0]) : $"{scratch.Count} Formationen")}: {TypeNames[(int)type]}");
        }

        void Toast(string text)
        {
            toast = text;
            toastUntil = Time.unscaledTime + toastSeconds;
        }

        static string Name(SimWorld world, int f)
        {
            var fd = world.Formations[f];
            return $"{TypeNames[math.clamp((int)fd.Type, 0, TypeNames.Length - 1)]} ({fd.MemberCount})";
        }

        bool TryGroundPoint(Vector2 screen, out float2 ground)
        {
            ground = default;
            if (viewCamera == null) return false;
            Ray ray = viewCamera.ScreenPointToRay(screen);
            if (ray.direction.y >= -1e-4f) return false;
            Vector3 p = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
            ground = new float2(p.x, p.z);
            return true;
        }

        // ---- Drawing ----

        Color Tint(SimWorld world, int f, float alpha)
        {
            var c = !IsOwn(world, f) ? enemyColor : selection.Contains(f) ? selectedColor : ownColor;
            c.a *= alpha;
            return c;
        }

        float Alpha => commandCamera != null ? commandCamera.Blend : (IsOpen ? 1f : 0f);

        /// <summary>Outlines, facings, marches and targets of all formations, the hero and its post, the line being drawn.</summary>
        void Draw(ScriptableRenderContext context, Camera cam)
        {
            float alpha = Alpha;
            var world = runner != null ? runner.World : null;
            if (cam != viewCamera || world == null || alpha <= 0.05f || bounds.Length < world.Formations.Length) return;
            const float lift = 0.08f;
            var formations = world.Formations;
            var settings = world.FormationSettings;
            GroundLines.Begin();
            for (int f = 0; f < formations.Length; f++)
            {
                if (living[f] == 0) continue;
                var fd = formations[f];
                var b = bounds[f];
                GL.Color(Tint(world, f, alpha));
                Box(fd, b.xy - outlineMargin, b.zw + outlineMargin, lift);
                if (selection.Contains(f)) Box(fd, b.xy - outlineMargin - 0.2f, b.zw + outlineMargin + 0.2f, lift);
                Vector3 front = SimRunner.ToWorld(fd.ToWorld(new float2((b.x + b.z) * 0.5f, b.w + outlineMargin)), lift);
                GroundLines.Arrow(front, new Vector3(fd.Facing.x, 0f, fd.Facing.y) * 2.5f, 0.7f);
                if (!IsOwn(world, f)) continue;

                // Ordered target: a line to the enemy formation it goes for
                int target = fd.TargetFormation;
                if ((fd.Behavior == FormationBehavior.Advance || fd.Behavior == FormationBehavior.Charge) && target >= 0 && target < formations.Length && living[target] > 0)
                {
                    var t = formations[target];
                    var tb = bounds[target];
                    GL.Color(new Color(enemyColor.r, enemyColor.g, enemyColor.b, enemyColor.a * alpha * 0.7f));
                    GroundLines.Line(front, SimRunner.ToWorld(t.ToWorld((tb.xy + tb.zw) * 0.5f), lift));
                }
                // March: the path and the front it forms at its end
                if (math.distancesq(fd.Destination, fd.Position) > 0.09f)
                {
                    GL.Color(new Color(pathColor.r, pathColor.g, pathColor.b, pathColor.a * alpha));
                    Vector3 from = SimRunner.ToWorld(fd.Position, lift);
                    var path = world.FormationPath(f);
                    for (int k = 0; k < path.Length; k++)
                    {
                        Vector3 to = SimRunner.ToWorld(path[k], lift);
                        GroundLines.Line(from, to);
                        from = to;
                    }
                    GL.Color(Tint(world, f, alpha));
                    float half = FormationLinePlanner.FrontWidth(fd.Type, fd.Files, fd.MemberCount, settings) * 0.5f;
                    Vector3 facing = new Vector3(fd.TargetFacing.x, 0f, fd.TargetFacing.y);
                    Vector3 along = new Vector3(fd.TargetFacing.y, 0f, -fd.TargetFacing.x) * half;
                    Vector3 dest = SimRunner.ToWorld(fd.Destination, lift);
                    Vector3 end = fd.Type == FormationType.Circle ? dest + facing * half : dest;
                    GroundLines.Line(end - along, end + along);
                    GroundLines.Arrow(end, facing * 1.5f, 0.4f);
                }
            }

            // The hero: a ring, and while unattended a line to the post it falls back to
            int hero = runner.PlayerAgent;
            if (hero >= 0 && !world.Agents[hero].IsDead)
            {
                Vector3 h = SimRunner.ToWorld(runner.InterpolatedPosition(hero), lift);
                GL.Color(new Color(selectedColor.r, selectedColor.g, selectedColor.b, alpha));
                Ring(h, 0.9f);
                if (world.IsUnattended(LocalPlayer))
                {
                    Vector3 post = SimRunner.ToWorld(world.Combat[hero].OrderTarget, lift);
                    GroundLines.Line(h, post);
                    GroundLines.Cross(post, 0.4f);
                }
            }

            if (drawingLine && TryGroundPoint(pointer, out float2 lineEnd)) DrawLinePreview(world, lineEnd, lift);
            GroundLines.End();
        }

        void DrawLinePreview(SimWorld world, float2 end, float lift)
        {
            Targets(world, scratch);
            if (scratch.Count == 0) return;
            using var ids = new NativeArray<int>(scratch.ToArray(), Allocator.Temp);
            using var orders = new NativeArray<FormationOrder>(scratch.Count, Allocator.Temp);
            FormationLinePlanner.PlanLine(world.Formations, ids, ids.Length, commandStart, end, true, formationGap, world.FormationSettings, orders);
            GL.Color(selectedColor);
            GroundLines.Line(SimRunner.ToWorld(commandStart, lift), SimRunner.ToWorld(end, lift));
            for (int k = 0; k < orders.Length; k++)
            {
                var o = orders[k];
                var f = world.Formations[o.Formation];
                float half = FormationLinePlanner.FrontWidth(f.Type, o.Files, f.MemberCount, world.FormationSettings) * 0.5f;
                float2 along = new float2(o.Facing.y, -o.Facing.x);
                float2 front = f.Type == FormationType.Circle ? o.Destination + o.Facing * half : o.Destination;
                GroundLines.Line(SimRunner.ToWorld(front - along * half, lift), SimRunner.ToWorld(front + along * half, lift));
                GroundLines.Arrow(SimRunner.ToWorld(front, lift), new Vector3(o.Facing.x, 0f, o.Facing.y) * 2f, 0.5f);
            }
        }

        static void Box(in FormationData f, float2 min, float2 max, float lift)
        {
            Vector3 a = SimRunner.ToWorld(f.ToWorld(min), lift);
            Vector3 b = SimRunner.ToWorld(f.ToWorld(new float2(max.x, min.y)), lift);
            Vector3 c = SimRunner.ToWorld(f.ToWorld(max), lift);
            Vector3 d = SimRunner.ToWorld(f.ToWorld(new float2(min.x, max.y)), lift);
            GroundLines.Line(a, b);
            GroundLines.Line(b, c);
            GroundLines.Line(c, d);
            GroundLines.Line(d, a);
        }

        static void Ring(Vector3 centre, float radius)
        {
            const int Segments = 16;
            for (int k = 0; k < Segments; k++)
            {
                float a0 = k * 2f * Mathf.PI / Segments, a1 = (k + 1) * 2f * Mathf.PI / Segments;
                GroundLines.Line(centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius, centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
            }
        }

        /// <summary>A label with order and a morale bar over every formation, the selection box, the hint bar, the confirmation.</summary>
        void OnGUI()
        {
            var world = runner != null ? runner.World : null;
            float alpha = Alpha;
            bool showToast = Time.unscaledTime < toastUntil;
            if (world == null || viewCamera == null || (alpha <= 0.05f && !showToast) || AdminMenu.IsOpen || PauseMenu.IsOpen) return;
            Vector2 screen = MenuStyle.BeginScaled();
            EnsureStyles();
            if (showToast) GUI.Label(new Rect(screen.x * 0.5f - 260f, 90f, 520f, 40f), toast, toastStyle);
            if (IsOpen)
            {
                string who = selection.Count == 0 ? "keine Auswahl: Befehle gehen an alle" : $"Auswahl: {selection.Count} Formation(en)";
                string radial = GameInput.KeyboardBinding(GameInput.Action("Radial", "Open"));
                string text = $"KOMMANDOANSICHT · {who}\nLinksklick: wählen (Shift: dazu) · Rechtsklick: hierher · rechts ziehen: Frontlinie · Rechtsklick auf Feind: vorrücken gegen ihn\n" +
                              $"{radial} halten: Befehle an die Auswahl · 1–5: Form · Mausrad: Höhe · {GameInput.KeyboardBinding(hold)} loslassen: zurück · Der Held zieht sich hinter die Linie zurück und wehrt sich selbst";
                GUI.Label(new Rect(screen.x * 0.5f - 560f, screen.y - 104f, 1120f, 84f), text, hint);
            }

            // Labels, boxes and the pad pointer in screen pixels
            GUI.matrix = Matrix4x4.identity;
            if (alpha > 0.05f && bounds.Length >= world.Formations.Length) DrawLabels(world, alpha);
            if (!IsOpen) return;
            if (selecting)
            {
                var a = new Vector2(selectStart.x, Screen.height - selectStart.y);
                var b = new Vector2(pointer.x, Screen.height - pointer.y);
                Frame(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)), selectedColor);
            }
            if (usingPad)
            {
                // The stick moves a pointer of its own
                var p = new Vector2(pointer.x, Screen.height - pointer.y);
                Fill(new Rect(p.x - 7f, p.y - 1f, 14f, 2f), Color.white);
                Fill(new Rect(p.x - 1f, p.y - 7f, 2f, 14f), Color.white);
            }
        }

        void DrawLabels(SimWorld world, float alpha)
        {
            float scale = MenuStyle.Scale;
            var formations = world.Formations;
            for (int f = 0; f < formations.Length; f++)
            {
                if (living[f] == 0) continue;
                var fd = formations[f];
                var b = bounds[f];
                Vector3 s = viewCamera.WorldToScreenPoint(SimRunner.ToWorld(fd.ToWorld(new float2((b.x + b.z) * 0.5f, b.w + outlineMargin)), 2.5f));
                if (s.z <= 0f) continue;
                bool marching = math.distancesq(fd.Destination, fd.Position) > 0.09f;
                string state = fd.Behavior == FormationBehavior.Hold && marching ? "marschiert" : BehaviorNames[math.clamp((int)fd.Behavior, 0, BehaviorNames.Length - 1)];
                if ((fd.Behavior == FormationBehavior.Advance || fd.Behavior == FormationBehavior.Charge) && fd.TargetFormation >= 0) state += " (Ziel)";
                string text = $"{TypeNames[math.clamp((int)fd.Type, 0, TypeNames.Length - 1)]} · {living[f]} · {state}";
                float w = 260f * scale, h = 20f * scale;
                var rect = new Rect(s.x - w * 0.5f, Screen.height - s.y - h, w, h);
                var color = Tint(world, f, alpha);
                label.fontSize = Mathf.RoundToInt(14f * scale);
                label.normal.textColor = new Color(0f, 0f, 0f, alpha);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, label);
                label.normal.textColor = color;
                GUI.Label(rect, text, label);
                if (!world.HasMorale) continue;
                // Morale bar: green when steady, red near breaking
                float m = Mathf.Clamp01(morale[f]);
                var bar = new Rect(s.x - 40f * scale, rect.yMax + 2f * scale, 80f * scale, 5f * scale);
                Fill(bar, new Color(0f, 0f, 0f, 0.6f * alpha));
                Fill(new Rect(bar.x, bar.y, bar.width * m, bar.height), new Color(1f - m, 0.35f + 0.6f * m, 0.25f, alpha));
            }
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, pixel);
            GUI.color = old;
        }

        void Frame(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        void EnsureStyles()
        {
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            hint = new GUIStyle(MenuStyle.Hint) { fontSize = 14 };
            toastStyle = new GUIStyle(MenuStyle.Hint) { fontSize = 18, fontStyle = FontStyle.Bold };
        }
    }
}
