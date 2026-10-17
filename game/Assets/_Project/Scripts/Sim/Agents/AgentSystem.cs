using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Grid;

namespace Shieldbreak.Sim.Agents
{
    /// <summary>
    /// Per-tick agent movement: rebuild the spatial hash, steer (seek + arrive + separation, turn-rate limited facing),
    /// integrate. Every job writes only its own index from read-only inputs, so parallel scheduling cannot change
    /// results. FloatMode.Deterministic keeps Burst from emitting CPU-dependent float code (e.g. FMA only on AVX2
    /// machines), which would desync lockstep peers.
    /// </summary>
    public static class AgentSystem
    {
        const int BatchSize = 64;

        public static void Step(NativeArray<AgentData> agents, int count, ref SpatialHash spatial,
            NativeArray<float2> positionScratch, NativeArray<float2> velocityScratch, NativeArray<float2> facingScratch,
            in AgentMoveSettings settings)
        {
            if (count == 0) return;

            new GatherPositionsJob { Agents = agents, Positions = positionScratch }.Schedule(count, BatchSize).Complete();
            spatial.Build(positionScratch, count);

            new SteeringJob
            {
                Agents = agents,
                CellStart = spatial.CellStart,
                Entries = spatial.Entries,
                Layout = spatial.Layout,
                Settings = settings,
                NewVelocity = velocityScratch,
                NewFacing = facingScratch,
            }.Schedule(count, BatchSize).Complete();

            new IntegrateJob { Agents = agents, NewVelocity = velocityScratch, NewFacing = facingScratch, Layout = spatial.Layout }
                .Schedule(count, BatchSize).Complete();
        }

        /// <summary>Rotates unit vector <paramref name="from"/> towards <paramref name="to"/> by at most <paramref name="maxRadians"/>.</summary>
        public static float2 RotateTowards(float2 from, float2 to, float maxRadians)
        {
            float angle = math.atan2(from.x * to.y - from.y * to.x, math.dot(from, to));
            float step = math.clamp(angle, -maxRadians, maxRadians);
            math.sincos(step, out float s, out float c);
            return math.normalizesafe(new float2(from.x * c - from.y * s, from.x * s + from.y * c), to);
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        struct GatherPositionsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<AgentData> Agents;
            [WriteOnly] public NativeArray<float2> Positions;

            public void Execute(int i) => Positions[i] = Agents[i].Position;
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        struct SteeringJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<AgentData> Agents;
            [ReadOnly] public NativeArray<int> CellStart;
            [ReadOnly] public NativeArray<int> Entries;
            public GridLayout Layout;
            public AgentMoveSettings Settings;
            [WriteOnly] public NativeArray<float2> NewVelocity;
            [WriteOnly] public NativeArray<float2> NewFacing;

            public void Execute(int i)
            {
                AgentData a = Agents[i];
                if (a.IsDead)
                {
                    NewVelocity[i] = float2.zero;
                    NewFacing[i] = a.Facing;
                    return;
                }
                float maxSpeed = Settings.MaxSpeed * math.max(a.SpeedScale, 0f);

                // Seek with linear slow-down inside SlowRadius
                float2 desired = float2.zero;
                float2 toTarget = a.Target - a.Position;
                float dist = math.length(toTarget);
                if (dist > Settings.StopRadius)
                    desired = toTarget / dist * (maxSpeed * math.min(1f, dist / Settings.SlowRadius));

                // Separation over the 3x3 cells around the agent; push grows linearly as neighbours get closer
                float sep = Settings.SeparationRadius;
                float2 push = float2.zero;
                int2 cell = math.clamp(Layout.WorldToCell(a.Position), int2.zero, new int2(Layout.Width - 1, Layout.Height - 1));
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 c = cell + new int2(dx, dy);
                    if (!Layout.InBounds(c)) continue;
                    int index = Layout.CellIndex(c);
                    for (int e = CellStart[index]; e < CellStart[index + 1]; e++)
                    {
                        int j = Entries[e];
                        if (j == i || Agents[j].IsDead) continue;
                        float2 d = a.Position - Agents[j].Position;
                        float dsq = math.lengthsq(d);
                        if (dsq >= sep * sep) continue;
                        if (dsq > 1e-8f)
                        {
                            float len = math.sqrt(dsq);
                            push += d / len * (1f - len / sep);
                        }
                        else
                        {
                            // Coincident agents: split along x, direction decided by id so both sides agree
                            push += new float2(i < j ? -1f : 1f, 0f);
                        }
                    }
                }
                // Separation keeps its full strength even when the agent itself is slowed (no overlap while attacking)
                desired += push * (Settings.SeparationWeight * Settings.MaxSpeed);

                // Limited acceleration towards the desired velocity, then cap the speed
                float2 steer = desired - a.Velocity;
                float maxDelta = Settings.MaxAcceleration * SimConstants.DT;
                float steerSq = math.lengthsq(steer);
                if (steerSq > maxDelta * maxDelta) steer *= maxDelta / math.sqrt(steerSq);
                float2 v = a.Velocity + steer;
                float cap = math.max(maxSpeed, math.length(push) * Settings.SeparationWeight * Settings.MaxSpeed);
                float speedSq = math.lengthsq(v);
                if (speedSq > cap * cap) v *= cap / math.sqrt(speedSq);
                NewVelocity[i] = v;

                // Facing: towards the combat target if any, else along the walking direction; turn-rate limited
                const float FacingMinSpeed = 0.05f;
                float2 wanted = a.Facing;
                if (a.FaceAgent >= 0 && a.FaceAgent < Agents.Length && a.FaceAgent != i)
                    wanted = math.normalizesafe(Agents[a.FaceAgent].Position - a.Position, a.Facing);
                else if (math.lengthsq(v) > FacingMinSpeed * FacingMinSpeed)
                    wanted = math.normalize(v);
                NewFacing[i] = RotateTowards(a.Facing, wanted, math.radians(Settings.MaxTurnRateDeg) * SimConstants.DT);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        struct IntegrateJob : IJobParallelFor
        {
            public NativeArray<AgentData> Agents;
            [ReadOnly] public NativeArray<float2> NewVelocity;
            [ReadOnly] public NativeArray<float2> NewFacing;
            public GridLayout Layout;

            public void Execute(int i)
            {
                AgentData a = Agents[i];
                a.Velocity = NewVelocity[i];
                a.Position += a.Velocity * SimConstants.DT;

                // Stay on the map; a tiny inset keeps the position inside the last cell
                float2 max = Layout.Origin + new float2(Layout.Width, Layout.Height) * SimConstants.CellSize - 1e-3f;
                a.Position = math.clamp(a.Position, Layout.Origin, max);
                a.Facing = NewFacing[i];
                Agents[i] = a;
            }
        }
    }
}
