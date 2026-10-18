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
    /// <summary>One weapon (Schicht C): archetype, physical values, one attack per direction and the archetype's special attack.</summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Weapon", fileName = "Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        public enum Archetype
        {
            [Tooltip("Shield on the arm; special: shield bash")] OneHandShield = WeaponArchetype.OneHandShield,
            [Tooltip("Both hands on the weapon; special: leaping overhead")] TwoHand = WeaponArchetype.TwoHand,
        }

        [Tooltip("Decides the special move and how the soldier holds the weapon (CLAUDE.md 3.5, Schicht B).")]
        public Archetype archetype = Archetype.OneHandShield;
        [Tooltip("Display name in menus.")]
        public string displayName = "Schwert und Schild";
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
        [Tooltip("Special attack of archetypes that have one (two-hand: leaping overhead). Unused for one-hand + shield.")]
        public AttackDefinition specialAttack;

        public bool HasSpecialAttack => archetype == Archetype.TwoHand;

        /// <summary>Starting values of the two-hand sword (preset "Mittel"): heavier, 90° weapon block, presses a blocker hard.</summary>
        public void ApplyTwoHandDefaults()
        {
            archetype = Archetype.TwoHand;
            displayName = "Zweihänder";
            mass = 2.0f;
            edgeFactor = 1f;
            pointFactor = 3f;
            blockArcDeg = 90f;
            blockPressure = 1.8f;
        }
    }
}
