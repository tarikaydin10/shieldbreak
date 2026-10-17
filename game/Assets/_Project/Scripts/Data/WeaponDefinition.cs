using System;
using System.Collections.Generic;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>One weapon (Schicht C): physical values plus one attack per direction.</summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Weapon", fileName = "Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        [Tooltip("kg; enters E = ½·m·v².")]
        [Min(0.1f)] public float mass = 1.2f;
        [Tooltip("Penetration for motion across the blade (cuts).")]
        [Min(0f)] public float edgeFactor = 1f;
        [Tooltip("Penetration for motion along the blade (thrusts). Higher than the edge: a point concentrates the energy.")]
        [Min(0f)] public float pointFactor = 3.5f;
        [Tooltip("Front arc the block covers (shield ~120°, weapon ~90°).")]
        [Range(30f, 180f)] public float blockArcDeg = 120f;
        [Tooltip("Multiplier on the stamina a blocker loses against this weapon (two-handers press harder).")]
        [Min(0f)] public float blockPressure = 1f;
        [Tooltip("Overhead, Left, Right, Thrust.")]
        public AttackDefinition[] attacks = new AttackDefinition[AttackDirection.Count];
    }
}
