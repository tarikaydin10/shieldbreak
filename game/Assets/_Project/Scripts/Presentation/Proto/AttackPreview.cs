using System.Collections.Generic;
using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;
using Shieldbreak.Sim.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Shieldbreak.Presentation.Proto
{
    /// <summary>
    /// M0 animation prototype (Scenes/Proto_Animation): plays an AttackDefinition on a soldier. The weapon follows the
    /// sim-side WeaponPath, the right arm reaches it via analytic two-bone IK, the blade tip leaves a trail coloured by
    /// phase, and a tick timeline shows phases and keys. Body pose (hip, lean) waits for the rig decision.
    /// </summary>
    public class AttackPreview : MonoBehaviour, IAdminStatus
    {
        public AttackDefinition attack;
        public SoldierVisualDefinition visual;
        [Tooltip("Body without right arm; the arm is drawn procedurally.")]
        public SoldierVisualDefinition.Part body = new SoldierVisualDefinition.Part();
        public Material upperArmMaterial;
        public Material forearmMaterial;
        [Tooltip("Blade stand-in drawn with the AttackDefinition blade length, so the visible blade and the sim sockets match. Swords come with M1.")]
        public Material bladeMaterial;
        public Vector2 bladeSection = new Vector2(0.015f, 0.06f);

        [Header("Arm (soldier frame: x right, y up, z forward)")]
        public Vector3 shoulder = new Vector3(0.24f, 1.40f, 0f);
        [Min(0.1f)] public float upperArmLength = 0.32f;
        [Min(0.1f)] public float forearmLength = 0.32f;
        [Min(0.02f)] public float armThickness = 0.1f;
        [Tooltip("Direction the elbow points to (out, down, back).")]
        public Vector3 elbowHint = new Vector3(1f, -0.6f, -0.4f);

        [Header("Playback")]
        [Tooltip("Pause between two attacks (s).")]
        [Min(0f)] public float pauseBetween = 0.6f;
        [Range(0.05f, 1f)] public float slowMotionScale = 0.25f;
        public Color windupColor = new Color(0.35f, 0.6f, 1f);
        public Color releaseColor = new Color(1f, 0.35f, 0.25f);
        public Color recoveryColor = new Color(0.75f, 0.75f, 0.75f);

        AttackTiming timing;
        NativeArray<WeaponPathKey> keys;
        float elapsed;
        float pauseTimer;
        bool playing = true;
        bool slow;
        float tipSpeed;
        readonly List<(Vector3 point, AttackPhase phase)> trail = new List<(Vector3, AttackPhase)>();
        Mesh cube;
        Material lineMaterial;
        Texture2D pixel;
        InputActionMap map;
        InputAction playPause, stepTick, slowMotion;

        public string AdminStatusLabel => "Hieb";
        public string AdminStatus => attack == null ? "keine AttackDefinition" :
            $"{attack.name} · {timing.PhaseAt((int)elapsed)} · Tick {(int)elapsed}/{timing.TotalTicks} · Pose {timing.PoseTime(elapsed):0.00} · Spitze {tipSpeed:0} m/s" +
            (playing ? "" : " · angehalten") + (slow ? $" · Zeitlupe {slowMotionScale:0.00}x" : "");

        void Awake()
        {
            timing = attack.ToTiming();
            keys = attack.ToKeys(Allocator.Persistent);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = go.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(go);
        }

        void OnEnable()
        {
            map = GameInput.Map("ProtoAttack");
            playPause = map.FindAction("PlayPause", true);
            stepTick = map.FindAction("StepTick", true);
            slowMotion = map.FindAction("SlowMotion", true);
            map.Enable();
            RenderPipelineManager.endCameraRendering += DrawTrail;
        }

        void OnDisable()
        {
            map?.Disable();
            RenderPipelineManager.endCameraRendering -= DrawTrail;
        }

        void OnDestroy()
        {
            if (keys.IsCreated) keys.Dispose();
            if (lineMaterial != null) Destroy(lineMaterial);
            if (pixel != null) Destroy(pixel);
        }

        /// <summary>Jumps to a pose time and fills the trail with the whole attack (screenshot capture).</summary>
        public void ShowPose(float poseTime)
        {
            if (!keys.IsCreated) Awake();
            playing = false;
            trail.Clear();
            for (int t = 0; t <= timing.TotalTicks; t++) trail.Add((Tip(t), timing.PhaseAt(t)));
            elapsed = timing.ElapsedForPose(poseTime);
            tipSpeed = TipSpeedAt(elapsed);
            Render();
        }

        void Update()
        {
            if (playPause.WasPressedThisFrame()) playing = !playing;
            if (slowMotion.WasPressedThisFrame()) slow = !slow;
            if (stepTick.WasPressedThisFrame())
            {
                playing = false;
                Advance(1f);
            }

            if (playing)
            {
                if (pauseTimer > 0f) pauseTimer -= Time.unscaledDeltaTime;
                else Advance(Time.unscaledDeltaTime * SimConstants.TickRate * (slow ? slowMotionScale : 1f));
            }
            Render();
        }

        void Advance(float ticks)
        {
            if (elapsed <= 0f) trail.Clear();
            elapsed += ticks;
            if (elapsed >= timing.TotalTicks)
            {
                elapsed = 0f;
                pauseTimer = pauseBetween;
                return;
            }
            trail.Add((Tip(elapsed), timing.PhaseAt((int)elapsed)));
            tipSpeed = TipSpeedAt(elapsed);
        }

        float TipSpeedAt(float ticks)
        {
            float t0 = math.max(0f, ticks - 0.5f), t1 = math.min(timing.TotalTicks, ticks + 0.5f);
            return Vector3.Distance(Tip(t1), Tip(t0)) / ((t1 - t0) * SimConstants.DT);
        }

        void Evaluate(float ticks, out Vector3 hand, out Vector3 blade)
        {
            WeaponPath.Evaluate(keys, timing.PoseTime(ticks), out float3 h, out float3 b);
            hand = transform.TransformPoint(h);
            blade = transform.TransformDirection(b);
        }

        Vector3 Tip(float ticks)
        {
            Evaluate(ticks, out var hand, out var blade);
            return hand + blade * attack.bladeLength;
        }

        // One GameObject per part (DontSave): robust in play mode and for editor captures, where
        // Graphics.RenderMesh would pile up several poses in one frame
        Transform bodyPart, helmetPart, shieldPart, upperArmPart, forearmPart, bladePart;

        Transform MakePart(string partName, Mesh mesh, Material material)
        {
            var go = new GameObject(partName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        static void PlaceStatic(Transform part, SoldierVisualDefinition.Part data)
        {
            var m = data.LocalMatrix;
            part.localPosition = m.GetPosition();
            part.localRotation = m.rotation;
            part.localScale = m.lossyScale;
        }

        void EnsureParts()
        {
            if (bodyPart != null) return;
            var look = visual.teams[0];
            bodyPart = MakePart("Body", body.mesh, visual.material);
            helmetPart = MakePart("Helmet", look.helmet.mesh, visual.material);
            shieldPart = MakePart("Shield", look.shield.mesh, visual.material);
            upperArmPart = MakePart("UpperArm", cube, upperArmMaterial);
            forearmPart = MakePart("Forearm", cube, forearmMaterial);
            bladePart = MakePart("Blade", cube, bladeMaterial);
            PlaceStatic(bodyPart, body);
            PlaceStatic(helmetPart, look.helmet);
            PlaceStatic(shieldPart, look.shield);
        }

        void Render()
        {
            if (attack == null || visual == null) return;
            EnsureParts();
            Evaluate(elapsed, out var hand, out var blade);
            // Thin side along the soldier's right: the edge leads in the (vertical) swing plane
            PlaceSegment(bladePart, hand, hand + blade * attack.bladeLength, bladeSection, transform.right);

            Vector3 s = transform.TransformPoint(shoulder);
            Vector3 elbow = SolveElbow(s, hand, upperArmLength, forearmLength, transform.TransformDirection(elbowHint));
            var arm = new Vector2(armThickness, armThickness);
            PlaceSegment(upperArmPart, s, elbow, arm, Vector3.up);
            PlaceSegment(forearmPart, elbow, hand, arm, Vector3.up);
        }

        /// <summary>Analytic two-bone IK: elbow on the circle of possible positions, turned towards the hint.</summary>
        static Vector3 SolveElbow(Vector3 shoulderPos, Vector3 target, float a, float b, Vector3 hint)
        {
            Vector3 toTarget = target - shoulderPos;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-3f, (a + b) * 0.999f);
            Vector3 dir = toTarget.normalized;
            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            Vector3 pole = Vector3.ProjectOnPlane(hint, dir).normalized;
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(Vector3.down, dir).normalized;
            return shoulderPos + dir * (a * cosA) + pole * (a * Mathf.Sqrt(1f - cosA * cosA));
        }

        /// <summary>Box from <paramref name="from"/> to <paramref name="to"/>; section.x is the width along <paramref name="widthAxis"/>.</summary>
        static void PlaceSegment(Transform part, Vector3 from, Vector3 to, Vector2 section, Vector3 widthAxis)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 1e-6f) return;
            Vector3 up = Vector3.Cross(delta, widthAxis);
            if (up.sqrMagnitude < 1e-8f) up = Vector3.Cross(delta, Vector3.forward);
            part.SetPositionAndRotation((from + to) * 0.5f, Quaternion.LookRotation(delta, up));
            part.localScale = new Vector3(section.x, section.y, delta.magnitude);
        }

        Color PhaseColor(AttackPhase p) => p == AttackPhase.Windup ? windupColor : p == AttackPhase.Release ? releaseColor : recoveryColor;

        void DrawTrail(ScriptableRenderContext context, Camera cam)
        {
            if (trail.Count < 2 || cam.cameraType != CameraType.Game) return;
            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_ZWrite", 0);
                lineMaterial.SetInt("_Cull", (int)CullMode.Off);
                lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            }
            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);
            for (int i = 1; i < trail.Count; i++)
            {
                GL.Color(PhaseColor(trail[i].phase));
                GL.Vertex(trail[i - 1].point);
                GL.Vertex(trail[i].point);
            }
            // Blade sockets of the current pose
            Evaluate(elapsed, out var hand, out var blade);
            GL.Color(Color.white);
            for (int k = 0; k < attack.socketCount; k++)
            {
                Vector3 p = hand + blade * (attack.bladeLength * (k + 1) / attack.socketCount);
                GL.Vertex(p - Vector3.up * 0.04f);
                GL.Vertex(p + Vector3.up * 0.04f);
            }
            GL.End();
            GL.PopMatrix();
        }

        void OnGUI()
        {
            if (attack == null) return;
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }
            float width = Mathf.Min(720f, Screen.width - 40f), x0 = (Screen.width - width) * 0.5f, y = Screen.height - 46f;
            float perTick = width / timing.TotalTicks;
            DrawBar(x0, y, timing.WindupTicks * perTick, windupColor);
            DrawBar(x0 + timing.WindupTicks * perTick, y, timing.ReleaseTicks * perTick, releaseColor);
            DrawBar(x0 + (timing.WindupTicks + timing.ReleaseTicks) * perTick, y, timing.RecoveryTicks * perTick, recoveryColor);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            for (int t = 0; t <= timing.TotalTicks; t++) GUI.DrawTexture(new Rect(x0 + t * perTick, y + 10f, 1f, 4f), pixel);
            GUI.color = Color.white;
            foreach (var key in attack.keys) GUI.DrawTexture(new Rect(x0 + timing.ElapsedForPose(key.time) * perTick - 1f, y - 6f, 2f, 20f), pixel);
            GUI.color = Color.yellow;
            GUI.DrawTexture(new Rect(x0 + elapsed * perTick - 1.5f, y - 10f, 3f, 28f), pixel);
            GUI.color = old;
            GUI.Label(new Rect(x0, y - 30f, width, 22f), AdminStatus);
        }

        void DrawBar(float x, float y, float w, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, 10f), pixel);
            GUI.color = old;
        }
    }
}
