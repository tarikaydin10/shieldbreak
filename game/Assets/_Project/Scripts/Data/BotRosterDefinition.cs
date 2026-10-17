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
    /// <summary>Bot skill levels (index = level, 1..5). Level 0 is reserved for "no bot".</summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Bot Roster", fileName = "BotRoster")]
    public class BotRosterDefinition : ScriptableObject
    {
        [Serializable]
        public class Level
        {
            public string label = "L?";
            [Tooltip("Reaction time to a seen attack (ms).")]
            public Vector2 reactionMs = new Vector2(250f, 450f);
            [Range(0f, 1f)] public float blockChance = 0.6f;
            [Range(0f, 1f)] public float perfectBlockChance = 0.1f;
            [Range(0f, 1f)] public float feintChance = 0.05f;
            [Range(0f, 1f)] public float attackChance = 0.25f;
            [Range(0f, 1f)] public float openingAttackChance = 0.5f;
            [Min(17f)] public float decisionIntervalMs = 200f;
            [Min(0.5f)] public float engageRange = 1.25f;
            [Min(1f)] public float acquireRange = 8f;
            public bool holdBlock;
        }

        public Level[] levels = DefaultLevels();

        public static Level[] DefaultLevels() => new[]
        {
            new Level { label = "L1", reactionMs = new Vector2(500f, 750f), blockChance = 0.3f, perfectBlockChance = 0f, feintChance = 0f, attackChance = 0.08f, openingAttackChance = 0.15f, decisionIntervalMs = 330f, holdBlock = true },
            new Level { label = "L2", reactionMs = new Vector2(400f, 600f), blockChance = 0.5f, perfectBlockChance = 0.02f, feintChance = 0f, attackChance = 0.15f, openingAttackChance = 0.3f, decisionIntervalMs = 250f },
            new Level { label = "L3", reactionMs = new Vector2(250f, 450f), blockChance = 0.6f, perfectBlockChance = 0.1f, feintChance = 0.05f, attackChance = 0.25f, openingAttackChance = 0.5f, decisionIntervalMs = 200f },
            new Level { label = "L4", reactionMs = new Vector2(200f, 330f), blockChance = 0.6f, perfectBlockChance = 0.25f, feintChance = 0.1f, attackChance = 0.3f, openingAttackChance = 0.65f, decisionIntervalMs = 170f },
            new Level { label = "L5", reactionMs = new Vector2(150f, 250f), blockChance = 0.55f, perfectBlockChance = 0.4f, feintChance = 0.15f, attackChance = 0.3f, openingAttackChance = 0.8f, decisionIntervalMs = 130f },
        };

        public static BotProfile ToProfile(Level l) => new BotProfile
        {
            ReactionMinTicks = AttackTiming.MsToTicks(l.reactionMs.x),
            ReactionMaxTicks = math.max(AttackTiming.MsToTicks(l.reactionMs.x), AttackTiming.MsToTicks(l.reactionMs.y)),
            BlockChance = l.blockChance,
            PerfectBlockChance = l.perfectBlockChance,
            FeintChance = l.feintChance,
            AttackChance = l.attackChance,
            OpeningAttackChance = l.openingAttackChance,
            DecisionIntervalTicks = AttackTiming.MsToTicks(l.decisionIntervalMs),
            EngageRange = l.engageRange,
            AcquireRange = l.acquireRange,
            HoldBlock = l.holdBlock ? 1 : 0,
        };
    }
}
