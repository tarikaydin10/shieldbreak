using System;
using NUnit.Framework;
using Shieldbreak.Sim.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    public class SimCoreTests
    {
        [Test]
        public void SimRandom_SameSeed_SameSequence()
        {
            var a = new SimRandom(7);
            var b = new SimRandom(7);
            for (int i = 0; i < 1000; i++)
                Assert.That(b.NextULong(), Is.EqualTo(a.NextULong()));
        }

        [Test]
        public void SimRandom_ZeroSeed_IsUsable()
        {
            var rng = new SimRandom(0);
            Assert.That(rng.State, Is.Not.Zero);
            Assert.That(rng.NextULong(), Is.Not.EqualTo(rng.NextULong()));
        }

        [Test]
        public void SimRandom_RangesStayInBounds()
        {
            var rng = new SimRandom(123);
            for (int i = 0; i < 10_000; i++)
            {
                float f = rng.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                int n = rng.NextInt(-5, 5);
                Assert.That(n, Is.GreaterThanOrEqualTo(-5).And.LessThan(5));
            }
        }

        [Test]
        public void InputQueue_DrainsOnlyRequestedTick_InTickPlayerSequenceOrder()
        {
            using var queue = new InputQueue(8, Allocator.Temp);
            var output = new NativeList<InputCommand>(8, Allocator.Temp);
            try
            {
                queue.Enqueue(new InputCommand { Tick = 5, PlayerId = 1, Sequence = 0 });
                queue.Enqueue(new InputCommand { Tick = 6, PlayerId = 0, Sequence = 9 });
                queue.Enqueue(new InputCommand { Tick = 5, PlayerId = 0, Sequence = 3 });
                queue.Enqueue(new InputCommand { Tick = 5, PlayerId = 0, Sequence = 1 });

                queue.DrainTick(5, output);

                Assert.That(output.Length, Is.EqualTo(3));
                Assert.That((output[0].PlayerId, output[0].Sequence), Is.EqualTo(((ushort)0, 1)));
                Assert.That((output[1].PlayerId, output[1].Sequence), Is.EqualTo(((ushort)0, 3)));
                Assert.That((output[2].PlayerId, output[2].Sequence), Is.EqualTo(((ushort)1, 0)));
                Assert.That(queue.Count, Is.EqualTo(1));
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public void InputQueue_LateInput_Throws()
        {
            using var queue = new InputQueue(4, Allocator.Temp);
            var output = new NativeList<InputCommand>(4, Allocator.Temp);
            try
            {
                queue.Enqueue(new InputCommand { Tick = 3 });
                Assert.Throws<InvalidOperationException>(() => queue.DrainTick(4, output));
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public void InputQueue_DuplicatePlayerSequence_Throws()
        {
            using var queue = new InputQueue(4, Allocator.Temp);
            var output = new NativeList<InputCommand>(4, Allocator.Temp);
            try
            {
                queue.Enqueue(new InputCommand { Tick = 2, PlayerId = 1, Sequence = 4 });
                queue.Enqueue(new InputCommand { Tick = 2, PlayerId = 1, Sequence = 4, IntArgs = new int4(1) });
                Assert.Throws<InvalidOperationException>(() => queue.DrainTick(2, output));
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public void SimWorld_Submit_StampsDelayedTickAndPerPlayerSequence()
        {
            using var world = new SimWorld(new SimConfig { Seed = 1, InputDelayTicks = 6, GridWidth = 64, GridHeight = 64 });
            world.Step();
            world.Step();

            var first = world.Submit(2, InputCommandType.None);
            var second = world.Submit(2, InputCommandType.None);
            var other = world.Submit(0, InputCommandType.None);

            Assert.That(first.Tick, Is.EqualTo(8));
            Assert.That((first.Sequence, second.Sequence, other.Sequence), Is.EqualTo((0, 1, 0)));

            // Ticks 2..7 run without them; tick 8 applies them
            for (int i = 0; i < 6; i++) world.Step();
            Assert.That(world.PendingInputCount, Is.EqualTo(3));
            world.Step();
            Assert.That(world.PendingInputCount, Is.Zero);
        }

        [Test]
        public void SimHash_DependsOnContentAndLength()
        {
            var a = new NativeArray<int>(new[] { 1, 2, 3 }, Allocator.Temp);
            var b = new NativeArray<int>(new[] { 1, 2, 4 }, Allocator.Temp);
            var c = new NativeArray<int>(new[] { 1, 2 }, Allocator.Temp);
            try
            {
                ulong Hash(NativeArray<int> array) { var h = SimHash.Create(); h.Add(array); return h.Value; }
                Assert.That(Hash(a), Is.EqualTo(Hash(a)));
                Assert.That(Hash(a), Is.Not.EqualTo(Hash(b)));
                Assert.That(Hash(a), Is.Not.EqualTo(Hash(c)));
            }
            finally
            {
                a.Dispose();
                b.Dispose();
                c.Dispose();
            }
        }
    }
}
