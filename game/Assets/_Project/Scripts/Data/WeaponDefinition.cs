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
            [Tooltip("Shoots arrows; no block, a side arm for melee")] Bow = WeaponArchetype.Bow,
            [Tooltip("Long reach, weak block; special: sweep")] Polearm = WeaponArchetype.Polearm,
            [Tooltip("Mounted: the couched lance, energy from the closing speed")] Lance = WeaponArchetype.Lance,
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
        [Tooltip("Multiplier on the stamina the wielder loses when blocking with this weapon (a pole is a poor shield).")]
        [Min(0f)] public float blockCost = 1f;
        [Tooltip("A bot fights from this much further away than with a sword (m).")]
        [Min(0f)] public float engageBonus;
        [Tooltip("Overhead, Left, Right, Thrust.")]
        public AttackDefinition[] attacks = new AttackDefinition[AttackDirection.Count];
        [Tooltip("Special attack of archetypes that have one (two-hand: leaping overhead, polearm: sweep). Unused for one-hand + shield.")]
        public AttackDefinition specialAttack;

        [Header("Bow (mass is the arrow, point factor its head)")]
        [Tooltip("Speed of the arrow across the ground (m/s).")]
        [Min(1f)] public float projectileSpeed = 45f;
        [Tooltip("Fall of the arrow (m/s²).")]
        [Min(0f)] public float projectileGravity = 9.81f;
        [Tooltip("Furthest a bot shoots (m).")]
        [Min(1f)] public float maxRange = 40f;
        [Tooltip("Metres of range gained per metre the shooter stands above the target, lost per metre below.")]
        [Min(0f)] public float rangePerMeterAbove = 1.5f;
        [Tooltip("An enemy closer than this makes a bot draw its side arm (m).")]
        [Min(0f)] public float minRange = 5f;

        public bool HasSpecialAttack => archetype == Archetype.TwoHand || archetype == Archetype.Polearm;

        /// <summary>Starting values of the war bow: an arrow of 100 g hits a torso for about a quarter of a soldier's health.</summary>
        public void ApplyBowDefaults()
        {
            archetype = Archetype.Bow;
            displayName = "Bogen";
            mass = 0.1f;
            edgeFactor = 0f;
            pointFactor = 3.5f;
            blockArcDeg = 30f;
            blockPressure = 0.4f;
        }

        /// <summary>Starting values of the spear: long reach, a point that goes in, an edge that barely does, a poor block.</summary>
        public void ApplyPolearmDefaults()
        {
            archetype = Archetype.Polearm;
            displayName = "Speer";
            mass = 2.2f;
            edgeFactor = 0.6f;
            pointFactor = 3.5f;
            blockArcDeg = 90f;
            blockPressure = 1f;
            blockCost = 1.6f;
            engageBonus = 0.7f;
        }

        /// <summary>Starting values of the two-hand sword (preset "Mittel"): heavier, 90° weapon block, presses a blocker hard.</summary>
        /// <summary>The mass is the effective mass of lance, arm and saddle behind the point: a gallop (9 m/s) into a
        /// standing man kills, a trot wounds, a standing horse does nothing.</summary>
        public void ApplyLanceDefaults()
        {
            archetype = Archetype.Lance;
            displayName = "Lanze";
            mass = 10f;
            edgeFactor = 0.3f;
            pointFactor = 3.5f;
            blockArcDeg = 30f;
            blockPressure = 1.5f;
            blockCost = 2f;
            engageBonus = 0f;
        }

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
