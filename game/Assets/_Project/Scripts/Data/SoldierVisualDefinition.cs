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
        [Tooltip("Two-hand weapon drawn in both hands (grip centre at the origin, blade along +Y).")]
        public Part twoHandWeapon = new Part();
        [Tooltip("The off hand holds a two-hand grip this far below the main hand (m).")]
        [Min(0f)] public float twoHandGripSpacing = 0.15f;
        [Tooltip("Two-hand block: main hand and blade direction in the soldier frame (weapon across the body).")]
        public Vector3 twoHandBlockHand = new Vector3(0.1f, 1.32f, 0.42f);
        public Vector3 twoHandBlockBlade = new Vector3(-0.9f, 0.4f, 0.15f);
        [Tooltip("Shield slung on the back while another weapon is drawn: position and rotation in the soldier frame.")]
        public Vector3 shieldBackOffset = new Vector3(0f, 1.1f, -0.2f);
        public Vector3 shieldBackEuler = new Vector3(0f, 180f, 0f);
        [Tooltip("Leaping overhead: height of the jump arc (m).")]
        [Min(0f)] public float leapHeight = 0.3f;
        [Tooltip("Limb segment in skin colour for the forearms, per team.")]
        public Part[] forearms = { new Part(), new Part() };

        [Header("Selection")]
        public Mesh selectionMesh;
        [Tooltip("Index = team.")]
        public Material[] selectionMaterials = new Material[2];
        [Min(0.1f)] public float selectionRadius = 0.45f;
    }
}
