using System;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// How a soldier is drawn: pipeline meshes per team (body, helmet, shield, weapon), where each is attached in the
    /// soldier's local frame (y up, z forward, origin at the feet), and the selection ring. Presentation only.
    /// </summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Soldier Visual", fileName = "SoldierVisual")]
    public class SoldierVisualDefinition : ScriptableObject
    {
        [Serializable]
        public class Part
        {
            public Mesh mesh;
            [Tooltip("Attach point in the soldier's local frame (m).")]
            public Vector3 offset;
            [Tooltip("Extra rotation around the soldier's up axis (degrees).")]
            public float yaw;
            [Tooltip("Mesh-to-model transform from the imported FBX (scale/rotation of its node). Set by the scene builder.")]
            public Matrix4x4 meshMatrix = Matrix4x4.identity;

            public Matrix4x4 LocalMatrix => Matrix4x4.TRS(offset, Quaternion.Euler(0f, yaw, 0f), Vector3.one) * meshMatrix;
        }

        [Serializable]
        public class TeamLook
        {
            public Part body = new Part();
            public Part helmet = new Part();
            public Part shield = new Part();
            public Part weapon = new Part();
            [Tooltip("Combat figure: body without arms; the arms are limb segments driven by IK.")]
            public Part armlessBody = new Part();
            [Tooltip("Limb segment (unit prism along +Y) in tunic colour, scaled to the upper arm.")]
            public Part upperArm = new Part();

            public Part[] Parts => new[] { body, helmet, shield, weapon };
        }

        [Tooltip("Shared palette material (M_Palette); must have GPU instancing enabled.")]
        public Material material;
        [Tooltip("Index = team.")]
        public TeamLook[] teams = { new TeamLook(), new TeamLook() };

        [Header("Combat figures")]
        [Tooltip("One-hand weapon drawn in the hand (grip at the origin, blade along +Y).")]
        public Part oneHandWeapon = new Part();
        [Tooltip("Limb segment in skin colour for the forearms, per team.")]
        public Part[] forearms = { new Part(), new Part() };

        [Header("Selection")]
        public Mesh selectionMesh;
        [Tooltip("Index = team.")]
        public Material[] selectionMaterials = new Material[2];
        [Min(0.1f)] public float selectionRadius = 0.45f;
    }
}
