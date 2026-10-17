using System.Collections.Generic;
using Shieldbreak.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shieldbreak.Presentation
{
    /// <summary>
    /// Draws every agent from the pipeline meshes in a SoldierVisualDefinition (body, helmet, shield, weapon) via
    /// Graphics.RenderMeshInstanced, no GameObject per agent. Selected agents get a ring in their team colour.
    /// Positions are interpolated by the SimRunner.
    /// </summary>
    public class AgentRenderer : MonoBehaviour
    {
        const int MaxPerCall = 1023;
        const int PartsPerTeam = 4;
        const int Teams = 2;

        public SimRunner runner;
        public SoldierVisualDefinition visual;

        // [team * PartsPerTeam + part] and [team] for selection rings
        readonly List<Matrix4x4>[] parts = new List<Matrix4x4>[Teams * PartsPerTeam];
        readonly List<Matrix4x4>[] rings = new List<Matrix4x4>[Teams];
        readonly Matrix4x4[] chunk = new Matrix4x4[MaxPerCall];
        Matrix4x4[] local = new Matrix4x4[Teams * PartsPerTeam];

        void Awake()
        {
            for (int i = 0; i < parts.Length; i++) parts[i] = new List<Matrix4x4>();
            for (int i = 0; i < rings.Length; i++) rings[i] = new List<Matrix4x4>();
        }

        void LateUpdate()
        {
            var world = runner != null ? runner.World : null;
            if (world == null || visual == null) return;

            for (int team = 0; team < Teams; team++)
            {
                var look = visual.teams[team].Parts;
                for (int p = 0; p < PartsPerTeam; p++) local[team * PartsPerTeam + p] = look[p].LocalMatrix;
            }
            foreach (var list in parts) list.Clear();
            foreach (var list in rings) list.Clear();

            var agents = world.Agents;
            float ring = visual.selectionRadius * 2f;
            for (int i = 0; i < agents.Length; i++)
            {
                var a = agents[i];
                int team = Mathf.Clamp(a.Team, 0, Teams - 1);
                Vector3 p = SimRunner.ToWorld(runner.InterpolatedPosition(i));
                var rotation = Quaternion.LookRotation(new Vector3(a.Facing.x, 0f, a.Facing.y), Vector3.up);
                var root = Matrix4x4.TRS(p, rotation, Vector3.one);
                for (int k = 0; k < PartsPerTeam; k++)
                    parts[team * PartsPerTeam + k].Add(root * local[team * PartsPerTeam + k]);
                if (AgentSelection.IsSelected(i))
                    rings[team].Add(Matrix4x4.TRS(p + Vector3.up * 0.04f, Quaternion.identity, new Vector3(ring, 0.01f, ring)));
            }

            for (int team = 0; team < Teams; team++)
            {
                var look = visual.teams[team].Parts;
                for (int k = 0; k < PartsPerTeam; k++)
                    Draw(look[k].mesh, visual.material, parts[team * PartsPerTeam + k], ShadowCastingMode.On);
                Draw(visual.selectionMesh, visual.selectionMaterials[team], rings[team], ShadowCastingMode.Off);
            }
        }

        void Draw(Mesh mesh, Material material, List<Matrix4x4> matrices, ShadowCastingMode shadows)
        {
            if (mesh == null || material == null || matrices.Count == 0) return;
            var rp = new RenderParams(material) { shadowCastingMode = shadows, receiveShadows = true };
            for (int start = 0; start < matrices.Count; start += MaxPerCall)
            {
                int n = Mathf.Min(MaxPerCall, matrices.Count - start);
                matrices.CopyTo(start, chunk, 0, n);
                Graphics.RenderMeshInstanced(rp, mesh, 0, chunk, n);
            }
        }
    }
}
