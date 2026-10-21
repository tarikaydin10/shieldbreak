using Shieldbreak.Presentation.Admin;
using Shieldbreak.Presentation.Menu;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Player
{
    /// <summary>
    /// Radial menu (CLAUDE.md 2 and 3.9, M2 task 6): quick orders from the fight. Hold the menu key, point with the
    /// mouse or the right stick, let go to give the order; letting go in the centre gives none. The mouse wheel (LB)
    /// picks the target: all own formations or one of them. Time keeps running and the hero keeps walking, but he
    /// neither looks around nor fights while the menu is open: commanding costs attention. Orders are QuickOrders,
    /// sent through the InputQueue like every player order.
    /// </summary>
    public class RadialMenu : MonoBehaviour, IAdminStatus
    {
        const ushort LocalPlayer = 0;

        public SimRunner runner;
        [Tooltip("Radius of the ring of commands (px in 1080p scale).")]
        [Min(40f)] public float radius = 150f;
        [Tooltip("Pointer distance from the centre below which nothing is chosen (px in 1080p scale).")]
        [Min(5f)] public float deadZone = 35f;
        [Tooltip("Pointer movement per mouse count.")]
        [Min(0.05f)] public float mouseScale = 1f;
        [Tooltip("Stick deflection that points at a command; a released stick keeps the last choice.")]
        [Range(0.1f, 0.95f)] public float stickThreshold = 0.5f;
        [Tooltip("Mouse wheel: at most one target change per this many seconds (smooth wheels send many steps).")]
        [Min(0f)] public float wheelRepeatSeconds = 0.15f;
        [Tooltip("How long the confirmation of an order stays on screen (s).")]
        [Min(0f)] public float toastSeconds = 1.6f;

        /// <summary>True while the menu is open: PlayerCombatInput then neither turns the view nor fights.</summary>
        public static bool IsOpen { get; private set; }

        // Clockwise from the top: forward up, back down, shapes on the left
        static readonly QuickCommand[] Ring =
        {
            QuickCommand.Advance, QuickCommand.Charge, QuickCommand.Follow, QuickCommand.Wedge,
            QuickCommand.Retreat, QuickCommand.ShieldWall, QuickCommand.Hold, QuickCommand.Line,
        };
        static readonly string[] CommandNames = { "Halten", "Vorrücken", "Angriff", "Folgen", "Rückzug", "Linie", "Schildwall", "Keil" };
        static readonly string[] TypeNames = { "Linie", "Schildwall", "Keil", "Kreis", "Locker" };

        InputActionMap map;
        InputAction open, point, cycle, nextTarget;
        Vector2 pointer;
        int hovered = -1;
        /// <summary>-1 = all own formations, else index into the commanded list.</summary>
        int target = -1;
        NativeArray<int> commanded;
        int commandedCount;
        float lastWheel = -10f;
        string toast = "", lastOrder = "–";
        float toastUntil;
        GUIStyle cell, cellHot, cellOff, centre, toastStyle;
        Texture2D cellTexture, hotTexture;

        public string AdminStatusLabel => "Befehle";
        public string AdminStatus => runner == null || runner.World == null ? "–" :
            $"Ziel {TargetName(runner.World)} · letzter Befehl {lastOrder} · Radialmenü: F halten (RB)";

        void OnEnable()
        {
            map = GameInput.Map("Radial");
            open = map.FindAction("Open", true);
            point = map.FindAction("Point", true);
            cycle = map.FindAction("CycleTarget", true);
            nextTarget = map.FindAction("NextTarget", true);
            map.Enable();
        }

        void OnDisable()
        {
            map?.Disable();
            IsOpen = false;
        }

        void OnDestroy()
        {
            if (commanded.IsCreated) commanded.Dispose();
            if (cellTexture != null) Destroy(cellTexture);
            if (hotTexture != null) Destroy(hotTexture);
        }

        void Update()
        {
            var world = runner != null ? runner.World : null;
            bool blocked = world == null || runner.PlayerAgent < 0 || AdminMenu.IsOpen || PauseMenu.IsOpen || DuelDirector.ResultOpen || GamePause.IsPaused;
            if (blocked)
            {
                IsOpen = false;
                return;
            }
            RefreshCommanded(world);

            if (open.WasPressedThisFrame())
            {
                IsOpen = true;
                pointer = Vector2.zero;
                hovered = -1;
            }
            if (!IsOpen) return;

            // The mouse moves a pointer from the centre; the stick points directly (a released stick keeps the choice)
            Vector2 delta = point.ReadValue<Vector2>();
            bool stick = point.activeControl != null && point.activeControl.device is Gamepad;
            if (stick)
            {
                if (delta.magnitude >= stickThreshold) pointer = delta.normalized * radius;
            }
            else pointer = Vector2.ClampMagnitude(pointer + delta * mouseScale, radius);
            hovered = pointer.magnitude < deadZone ? -1 : Segment(pointer);

            float wheel = cycle.ReadValue<float>();
            if (Mathf.Abs(wheel) > 0.01f && Time.unscaledTime - lastWheel >= wheelRepeatSeconds)
            {
                lastWheel = Time.unscaledTime;
                CycleTarget(wheel > 0f ? 1 : -1);
            }
            if (nextTarget.WasPressedThisFrame()) CycleTarget(1);

            if (open.WasReleasedThisFrame())
            {
                if (hovered >= 0) Issue(world, Ring[hovered]);
                IsOpen = false;
            }
        }

        /// <summary>Ring index under a pointer (screen y up), clockwise from the top.</summary>
        static int Segment(Vector2 p)
        {
            float degrees = Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;
            return Mathf.RoundToInt(degrees / (360f / Ring.Length)) % Ring.Length;
        }

        void RefreshCommanded(SimWorld world)
        {
            var formations = world.Formations;
            if (!commanded.IsCreated || commanded.Length < formations.Length)
            {
                if (commanded.IsCreated) commanded.Dispose();
                commanded = new NativeArray<int>(Mathf.Max(4, formations.Length), Allocator.Persistent);
            }
            commandedCount = QuickOrders.Commanded(formations, LocalPlayer, commanded);
            if (target >= commandedCount) target = -1;
        }

        void CycleTarget(int step)
        {
            // -1 (all), 0, 1, ..., count-1, back to all
            int states = commandedCount + 1;
            target = ((target + 1 + step) % states + states) % states - 1;
        }

        string TargetName(SimWorld world)
        {
            if (commandedCount == 0) return "keine eigenen Formationen";
            if (target < 0) return commandedCount == 1 ? "die Formation" : $"alle {commandedCount} Formationen";
            var f = world.Formations[commanded[target]];
            return $"Formation {target + 1} ({TypeNames[Mathf.Clamp((int)f.Type, 0, TypeNames.Length - 1)]}, {f.MemberCount})";
        }

        bool Available(QuickCommand command) => commandedCount > 0 && QuickOrders.IsAvailable(command, runner.PlayerAgent);

        void Issue(SimWorld world, QuickCommand command)
        {
            if (!Available(command)) return;
            int first = target < 0 ? 0 : target;
            int last = target < 0 ? commandedCount - 1 : target;
            for (int k = first; k <= last; k++)
                if (QuickOrders.TryBuild(command, commanded[k], runner.PlayerAgent, out var order))
                    world.SubmitFormationOrder(LocalPlayer, order);
            lastOrder = CommandNames[(int)command];
            toast = $"{TargetName(world)}: {lastOrder}";
            toastUntil = Time.unscaledTime + toastSeconds;
        }

        void OnGUI()
        {
            var world = runner != null ? runner.World : null;
            if (world == null) return;
            bool showToast = Time.unscaledTime < toastUntil;
            if (!IsOpen && !showToast) return;
            Vector2 screen = MenuStyle.BeginScaled();
            EnsureStyles();
            if (showToast) GUI.Label(new Rect(screen.x * 0.5f - 260f, 90f, 520f, 40f), toast, toastStyle);
            if (!IsOpen) return;

            Vector2 c = screen * 0.5f;
            const float w = 132f, h = 40f;
            for (int k = 0; k < Ring.Length; k++)
            {
                float angle = k * (2f * Mathf.PI / Ring.Length);
                // Screen y grows downwards
                Vector2 at = c + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * radius;
                var style = !Available(Ring[k]) ? cellOff : k == hovered ? cellHot : cell;
                GUI.Label(new Rect(at.x - w * 0.5f, at.y - h * 0.5f, w, h), CommandNames[(int)Ring[k]], style);
            }
            GUI.Label(new Rect(c.x - 120f, c.y - 34f, 240f, 68f), $"{TargetName(world)}\n<size=12>Mausrad / LB: Ziel wechseln</size>", centre);
            // Where the pointer is (only the mouse needs it, the stick points directly)
            Vector2 p = c + new Vector2(pointer.x, -pointer.y);
            GUI.Label(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), GUIContent.none, cellHot);
        }

        void EnsureStyles()
        {
            if (cell != null && cellTexture != null) return;
            cellTexture = Solid(new Color(0.07f, 0.08f, 0.09f, 0.88f));
            hotTexture = Solid(new Color(MenuStyle.Accent.r, MenuStyle.Accent.g, MenuStyle.Accent.b, 0.95f));
            cell = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { background = cellTexture, textColor = MenuStyle.Fg },
            };
            cellHot = new GUIStyle(cell) { normal = { background = hotTexture, textColor = Color.white } };
            cellOff = new GUIStyle(cell) { normal = { background = cellTexture, textColor = new Color(0.45f, 0.46f, 0.5f) } };
            centre = new GUIStyle(cell) { fontSize = 15, fontStyle = FontStyle.Normal, richText = true, wordWrap = true };
            toastStyle = new GUIStyle(cell) { fontSize = 18 };
        }

        static Texture2D Solid(Color color)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, color);
            t.Apply();
            return t;
        }
    }
}
