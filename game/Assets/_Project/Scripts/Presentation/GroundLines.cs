using UnityEngine;
using UnityEngine.Rendering;

namespace Shieldbreak.Presentation
{
    /// <summary>
    /// Immediate-mode GL lines on the ground for overlays (debug views, command view). Call Begin, then
    /// Line/Cross/Arrow, then End, from a RenderPipelineManager.endCameraRendering callback (BeginQuads, Quad, End for
    /// filled cells). Drawn on top of everything (bumpy ground never hides them).
    /// </summary>
    public static class GroundLines
    {
        static Material material;

        public static void Begin() => Begin(GL.LINES);

        /// <summary>Filled quads (heat maps); Quad takes four corners in order.</summary>
        public static void BeginQuads() => Begin(GL.QUADS);

        public static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            GL.Vertex(a);
            GL.Vertex(b);
            GL.Vertex(c);
            GL.Vertex(d);
        }

        static void Begin(int mode)
        {
            if (material == null)
            {
                material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_Cull", (int)CullMode.Off);
                material.SetInt("_ZWrite", 0);
                material.SetInt("_ZTest", (int)CompareFunction.Always);
            }
            material.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(mode);
        }

        public static void End()
        {
            GL.End();
            GL.PopMatrix();
        }

        public static void Color(Color c) => GL.Color(c);

        public static void Line(Vector3 a, Vector3 b)
        {
            GL.Vertex(a);
            GL.Vertex(b);
        }

        public static void Cross(Vector3 p, float size)
        {
            Line(p + new Vector3(-size, 0, -size), p + new Vector3(size, 0, size));
            Line(p + new Vector3(-size, 0, size), p + new Vector3(size, 0, -size));
        }

        /// <summary>Arrow from <paramref name="from"/> along <paramref name="direction"/> (ground plane), head included.</summary>
        public static void Arrow(Vector3 from, Vector3 direction, float headSize)
        {
            Vector3 to = from + direction;
            Line(from, to);
            if (direction.sqrMagnitude < 1e-6f) return;
            Vector3 back = -direction.normalized * headSize;
            Vector3 side = new Vector3(-back.z, 0f, back.x) * 0.6f;
            Line(to, to + back + side);
            Line(to, to + back - side);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (material != null) Object.DestroyImmediate(material);
            material = null;
        }
    }
}
