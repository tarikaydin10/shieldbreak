using Shieldbreak.Presentation;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Sim.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Shieldbreak.DebugTools
{
    /// <summary>
    /// GL line overlay drawn after the camera renders: grid cells around the view centre and the map border, velocity
    /// arrows, and the targets of selected agents. Toggles via the DebugOverlay action map.
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
        public Color gridColor = new Color(1f, 1f, 1f, 0.12f);
        public Color borderColor = new Color(1f, 0.85f, 0.4f, 0.8f);
        public Color velocityColor = new Color(0.4f, 1f, 0.5f, 0.9f);
        public Color targetColor = new Color(1f, 0.85f, 0.4f, 0.6f);

        Material lineMaterial;
        InputActionMap map;
        InputAction toggleGrid, toggleVelocity, toggleTargets;

        public string AdminStatusLabel => "Overlay";
        public string AdminStatus => $"Grid {OnOff(showGrid)} · Geschwindigkeit {OnOff(showVelocity)} · Ziele {OnOff(showTargets)}";
        static string OnOff(bool b) => b ? "an" : "aus";

        void OnEnable()
        {
            map = GameInput.Map("DebugOverlay");
            toggleGrid = map.FindAction("ToggleGrid", true);
            toggleVelocity = map.FindAction("ToggleVelocity", true);
            toggleTargets = map.FindAction("ToggleTargets", true);
            map.Enable();
            RenderPipelineManager.endCameraRendering += Draw;
        }

        void OnDisable()
        {
            map?.Disable();
            RenderPipelineManager.endCameraRendering -= Draw;
        }

        void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        void Update()
        {
            if (toggleGrid.WasPressedThisFrame()) showGrid = !showGrid;
            if (toggleVelocity.WasPressedThisFrame()) showVelocity = !showVelocity;
            if (toggleTargets.WasPressedThisFrame()) showTargets = !showTargets;
        }

        void EnsureMaterial()
        {
            if (lineMaterial != null) return;
            lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
            // Ground patches are bumpy; draw on top so the overlay never disappears into the grass
            lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        }

        void Draw(ScriptableRenderContext context, Camera cam)
        {
            var world = runner != null ? runner.World : null;
            if (world == null || cam != viewCamera) return;
            EnsureMaterial();
            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

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

            GL.End();
            GL.PopMatrix();
        }

        static void Line(Vector3 a, Vector3 b)
        {
            GL.Vertex(a);
            GL.Vertex(b);
        }

        static Vector3 ViewCentre(Camera cam)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (ray.direction.y >= -1e-4f) return cam.transform.position;
            return ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
        }
    }
}
