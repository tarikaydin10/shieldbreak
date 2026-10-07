using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Sim.Core;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// Must stay green before every commit (CLAUDE.md 3.3): same seed + same input log => same hash, tick for tick.
    /// </summary>
    public class DeterminismTests
    {
        const int TickCount = 10_000;
        const int HashInterval = 100;
        const ulong Seed = 0xC0FFEE;

        static SimConfig Config(ulong seed) => new SimConfig { Seed = seed, InputDelayTicks = 6 };

        /// <summary>Reproducible log: several players, commands every few ticks, sometimes multiple per tick.</summary>
        static List<InputCommand> BuildInputLog(ulong logSeed)
        {
            var rng = new SimRandom(logSeed);
            var log = new List<InputCommand>();
            var sequence = new int[4];
            for (int tick = 0; tick < TickCount; tick += rng.NextInt(1, 12))
            {
                int count = rng.NextInt(1, 4);
                for (int i = 0; i < count; i++)
                {
                    ushort player = (ushort)rng.NextInt(4);
                    log.Add(new InputCommand
                    {
                        Tick = tick,
                        Sequence = sequence[player]++,
                        PlayerId = player,
                        Type = InputCommandType.None,
                        IntArgs = new int4(rng.NextInt(-1000, 1000), rng.NextInt(100), tick, i),
                        FloatArgs = rng.NextFloat2(-50f, 50f).xyxy,
                    });
                }
            }
            return log;
        }

        static List<ulong> RunAndRecordHashes(SimConfig config, List<InputCommand> log, bool reverseArrival)
        {
            var hashes = new List<ulong>();
            using var world = new SimWorld(config);
            if (reverseArrival)
                for (int i = log.Count - 1; i >= 0; i--) world.Enqueue(log[i]);
            else
                foreach (var command in log) world.Enqueue(command);

            for (int t = 0; t < TickCount; t++)
            {
                world.Step();
                if (world.Tick % HashInterval == 0) hashes.Add(world.ComputeHash());
            }
            Assert.That(world.PendingInputCount, Is.Zero, "All logged inputs must have been applied.");
            return hashes;
        }

        [Test]
        public void SameSeedAndInputLog_ProduceIdenticalHashesEveryHundredTicks()
        {
            var log = BuildInputLog(42);
            // Second world receives the inputs in reverse order: application order must not depend on arrival order
            var a = RunAndRecordHashes(Config(Seed), log, reverseArrival: false);
            var b = RunAndRecordHashes(Config(Seed), log, reverseArrival: true);

            Assert.That(a.Count, Is.EqualTo(TickCount / HashInterval));
            for (int i = 0; i < a.Count; i++)
                Assert.That(b[i], Is.EqualTo(a[i]), $"Hash diverged at tick {(i + 1) * HashInterval}.");
        }

        [Test]
        public void DifferentSeed_ProducesDifferentHash()
        {
            var log = BuildInputLog(42);
            var a = RunAndRecordHashes(Config(Seed), log, reverseArrival: false);
            var b = RunAndRecordHashes(Config(Seed + 1), log, reverseArrival: false);

            Assert.That(b[b.Count - 1], Is.Not.EqualTo(a[a.Count - 1]));
        }

        [Test]
        public void DifferentInputLog_ProducesDifferentHash()
        {
            var a = RunAndRecordHashes(Config(Seed), BuildInputLog(42), reverseArrival: false);
            var b = RunAndRecordHashes(Config(Seed), BuildInputLog(43), reverseArrival: false);

            Assert.That(b[b.Count - 1], Is.Not.EqualTo(a[a.Count - 1]));
        }
    }
}
