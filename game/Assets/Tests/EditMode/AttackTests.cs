using System;
using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;
using Shieldbreak.Sim.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Tests
{
    /// <summary>Checks the default overhead cut against docs/animation/overhead-hieb.md.</summary>
    public class AttackTests
    {
        AttackDefinition attack;
        AttackTiming timing;
        NativeArray<WeaponPathKey> keys;

        [SetUp]
        public void SetUp()
        {
            attack = ScriptableObject.CreateInstance<AttackDefinition>();
            timing = attack.ToTiming();
            keys = attack.ToKeys(Allocator.Temp);
        }

        [TearDown]
        public void TearDown()
        {
            keys.Dispose();
            UnityEngine.Object.DestroyImmediate(attack);
        }

        float3 Tip(float elapsedTicks)
        {
            WeaponPath.Evaluate(keys, timing.PoseTime(elapsedTicks), out float3 hand, out float3 blade);
            return WeaponPath.Socket(hand, blade, attack.bladeLength);
        }

        [Test]
        public void Timing_PresetMittel_IsWholeTicksWithPoseBoundaries()
        {
            Assert.That((timing.WindupTicks, timing.ReleaseTicks, timing.RecoveryTicks), Is.EqualTo((24, 9, 12)));
            Assert.That(timing.PhaseAt(0), Is.EqualTo(AttackPhase.Windup));
            Assert.That(timing.PhaseAt(23), Is.EqualTo(AttackPhase.Windup));
            Assert.That(timing.PhaseAt(24), Is.EqualTo(AttackPhase.Release));
            Assert.That(timing.PhaseAt(33), Is.EqualTo(AttackPhase.Recovery));
            Assert.That(timing.PhaseAt(45), Is.EqualTo(AttackPhase.Done));
            Assert.That(timing.PoseTime(24f), Is.EqualTo(0.55f).Within(1e-6f));
            Assert.That(timing.PoseTime(33f), Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(timing.PoseTime(45f), Is.EqualTo(1f));
            Assert.Throws<ArgumentException>(() => new AttackTiming { WindupTicks = 1, ReleaseTicks = 1, RecoveryTicks = 1, ReleaseStartPose = 0.8f, RecoveryStartPose = 0.7f }.Validate());
        }

        [Test]
        public void ElapsedForPose_InvertsPoseTime()
        {
            for (float ticks = 0f; ticks <= timing.TotalTicks; ticks += 0.25f)
                Assert.That(timing.ElapsedForPose(timing.PoseTime(ticks)), Is.EqualTo(ticks).Within(1e-3f), $"ticks {ticks}");
        }

        [Test]
        public void WeaponPath_HitsEveryKeyExactly()
        {
            foreach (var key in keys)
            {
                WeaponPath.Evaluate(keys, key.Time, out float3 hand, out float3 blade);
                Assert.That(math.distance(hand, key.Hand), Is.LessThan(1e-5f), $"t={key.Time}");
                Assert.That(math.dot(blade, math.normalize(key.Blade)), Is.GreaterThan(0.9999f), $"t={key.Time}");
            }
        }

        [Test]
        public void BladeTip_IsFastestInReleaseShortlyBeforeContact()
        {
            float peakSpeed = 0f, peakAt = 0f;
            for (int tick = 1; tick <= timing.TotalTicks; tick++)
            {
                float speed = math.distance(Tip(tick), Tip(tick - 1)) / SimConstants.DT;
                if (speed > peakSpeed)
                {
                    peakSpeed = speed;
                    peakAt = tick - 0.5f;
                }
            }
            float peakPose = timing.PoseTime(peakAt);
            TestContext.WriteLine($"peak tip speed {peakSpeed:0.0} m/s at pose time {peakPose:0.000} (contact 0.70)");
            Assert.That(timing.PhaseAt((int)peakAt), Is.EqualTo(AttackPhase.Release));
            Assert.That(peakPose, Is.InRange(0.58f, 0.70f));
            Assert.That(peakSpeed, Is.InRange(8f, 40f), "plausible one-hand blade tip speed");
        }

        [Test]
        public void WindupEnd_IsNearlyStill_ReadingWindow()
        {
            float peak = 0f;
            for (int tick = 1; tick <= timing.TotalTicks; tick++)
                peak = math.max(peak, math.distance(Tip(tick), Tip(tick - 1)));
            int from = (int)math.ceil(timing.WindupTicks * 0.85f);
            for (int tick = from + 1; tick <= timing.WindupTicks; tick++)
                Assert.That(math.distance(Tip(tick), Tip(tick - 1)), Is.LessThan(peak * 0.25f), $"tick {tick}");
        }

        [Test]
        public void ToKeys_RejectsUnsortedKeys()
        {
            var broken = ScriptableObject.CreateInstance<AttackDefinition>();
            try
            {
                (broken.keys[1].time, broken.keys[2].time) = (0.8f, 0.6f);
                Assert.Throws<InvalidOperationException>(() => broken.ToKeys(Allocator.Temp).Dispose());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(broken);
            }
        }
    }
}
