using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Orders;

namespace Shieldbreak.Sim.Formations
{
    /// <summary>
    /// Formation layer (CLAUDE.md 3.1, 3.6): owns formations and per-agent membership, applies FormationOrders,
    /// marches the anchor, closes gaps after losses and hands every member its slot as the steering target. Agents
    /// always steer to their own slot, never to the formation's destination.
    /// </summary>
    public sealed class FormationSystem : IDisposable
    {
        readonly FormationSettings settings;
        readonly AgentMoveSettings movement;
        NativeArray<FormationData> formations;
        NativeArray<FormationMember> members;
        int formationCount;

        // Reform scratch, sized for the largest formation
        NativeArray<int> rows;
        NativeArray<float2> relative;
        NativeArray<float2> slotLocal;
        NativeArray<float2> slotFacing;
        NativeArray<float> cost;
        NativeArray<int> slotOfRow;
        SlotAssignment assignment;
        NativeArray<float> errorSum;
        NativeArray<float2> anchorOut;

        public FormationSystem(int maxFormations, int maxAgents, in FormationSettings settings, in AgentMoveSettings movement)
        {
            if (maxFormations < 0) throw new ArgumentOutOfRangeException(nameof(maxFormations));
            if (maxFormations > 0) settings.Validate(movement.MaxSpeed);
            this.settings = settings;
            this.movement = movement;
            formations = new NativeArray<FormationData>(maxFormations, Allocator.Persistent);
            members = new NativeArray<FormationMember>(maxAgents, Allocator.Persistent);
            for (int i = 0; i < maxAgents; i++) members[i] = FormationMember.None;
            int n = maxFormations > 0 ? math.min(maxAgents, FormationLimits.MaxMembers) : 0;
            rows = new NativeArray<int>(n, Allocator.Persistent);
            relative = new NativeArray<float2>(n, Allocator.Persistent);
            slotLocal = new NativeArray<float2>(n, Allocator.Persistent);
            slotFacing = new NativeArray<float2>(n, Allocator.Persistent);
            cost = new NativeArray<float>(n * n, Allocator.Persistent);
            slotOfRow = new NativeArray<int>(n, Allocator.Persistent);
            assignment = new SlotAssignment(n, Allocator.Persistent);
            errorSum = new NativeArray<float>(maxFormations, Allocator.Persistent);
            anchorOut = new NativeArray<float2>(1, Allocator.Persistent);
        }

        public int Count => formationCount;
        public NativeArray<FormationData>.ReadOnly Formations => formations.GetSubArray(0, formationCount).AsReadOnly();
        public NativeArray<FormationMember>.ReadOnly Members(int agentCount) => members.GetSubArray(0, agentCount).AsReadOnly();
        public FormationSettings Settings => settings;

        public int Create(int team, int owner, FormationType type, int files, float2 position, float2 facing)
        {
            if (formationCount >= formations.Length) throw new InvalidOperationException($"Formation capacity {formations.Length} reached.");
            if ((uint)type >= FormationLimits.TypeCount) throw new ArgumentOutOfRangeException(nameof(type));
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(facing))) throw new ArgumentException("Position and facing must be finite.");
            int id = formationCount++;
            formations[id] = new FormationData
            {
                Id = id,
                Team = team,
                Owner = owner,
                Type = type,
                Files = math.clamp(files, 1, FormationLimits.MaxFiles),
                Position = position,
                Facing = math.normalizesafe(facing, new float2(0f, 1f)),
                Destination = position,
            };
            return id;
        }

        /// <summary>Scenario setup: adds the agent; slots are reassigned around the anchor on the next tick.</summary>
        public void Add(int formation, int agent, int team)
        {
            if (formation < 0 || formation >= formationCount) throw new ArgumentOutOfRangeException(nameof(formation));
            if (agent < 0 || agent >= members.Length) throw new ArgumentOutOfRangeException(nameof(agent));
            if (members[agent].Formation >= 0) throw new InvalidOperationException($"Agent {agent} already belongs to formation {members[agent].Formation}.");
            var f = formations[formation];
            if (f.Team != team) throw new InvalidOperationException($"Agent {agent} is not on team {f.Team}.");
            if (f.MemberCount >= math.min(FormationLimits.MaxMembers, rows.Length)) throw new InvalidOperationException($"Formation {formation} is full.");
            members[agent] = new FormationMember { Formation = formation, Slot = -1 };
            f.MemberCount++;
            f.Reform = math.max(f.Reform, FormationData.ReformKeepAnchor);
            f.NextReformTick = 0;
            formations[formation] = f;
        }

        /// <summary>
        /// Applies an order if <paramref name="issuer"/> owns the formation. Invalid orders are ignored (never thrown):
        /// they may come from a remote peer and must not crash the sim. Returns whether anything changed.
        /// </summary>
        public bool Apply(in FormationOrder order, int issuer)
        {
            if (order.Formation < 0 || order.Formation >= formationCount) return false;
            var f = formations[order.Formation];
            if (f.Owner != issuer || f.MemberCount == 0) return false;
            bool changed = false;
            if ((order.Flags & FormationOrderFlags.Move) != 0 && math.all(math.isfinite(order.Destination)) && math.all(math.isfinite(order.Facing)))
            {
                f.Destination = order.Destination;
                if (math.lengthsq(order.Facing) > 1e-8f)
                {
                    float2 facing = math.normalize(order.Facing);
                    if (math.dot(facing, f.Facing) < 0.9999f)
                    {
                        f.Facing = facing;
                        f.Reform = FormationData.ReformAroundMembers;
                    }
                }
                changed = true;
            }
            if ((order.Flags & FormationOrderFlags.Shape) != 0 && (uint)order.Type < FormationLimits.TypeCount && order.Type != f.Type)
            {
                f.Type = order.Type;
                f.Reform = FormationData.ReformAroundMembers;
                changed = true;
            }
            if ((order.Flags & FormationOrderFlags.Width) != 0 && order.Files >= 1)
            {
                int files = math.min(order.Files, FormationLimits.MaxFiles);
                if (files != f.Files)
                {
                    f.Files = files;
                    f.Reform = FormationData.ReformAroundMembers;
                    changed = true;
                }
            }
            if (f.Reform == FormationData.ReformAroundMembers) f.NextReformTick = 0;
            formations[order.Formation] = f;
            return changed;
        }

        /// <summary>
        /// One tick: drop dead members, reform where needed, march, then write every member's slot as its target.
        /// Runs before agent steering. <paramref name="combat"/> may be empty (worlds without combat).
        /// </summary>
        public void Step(int tick, NativeArray<AgentData> agents, NativeArray<CombatData> combat, int agentCount, bool hasCombat)
        {
            if (formationCount == 0) return;

            for (int i = 0; i < agentCount; i++)
            {
                var m = members[i];
                if (m.Formation < 0 || !agents[i].IsDead) continue;
                var f = formations[m.Formation];
                f.MemberCount--;
                if (f.Reform == FormationData.ReformNone)
                {
                    f.Reform = FormationData.ReformKeepAnchor;
                    f.NextReformTick = tick + settings.LossReformIntervalTicks;
                }
                formations[m.Formation] = f;
                members[i] = FormationMember.None;
            }

            for (int id = 0; id < formationCount; id++)
            {
                var f = formations[id];
                if (f.Reform != FormationData.ReformNone && tick >= f.NextReformTick && f.MemberCount > 0)
                {
                    Reform(ref f, agents, agentCount);
                    formations[id] = f;
                }
                errorSum[id] = 0f;
            }

            // Cohesion: average distance of the members to their slots
            for (int i = 0; i < agentCount; i++)
            {
                var m = members[i];
                if (m.Formation < 0 || m.Slot < 0) continue;
                var f = formations[m.Formation];
                errorSum[m.Formation] += math.distance(agents[i].Position, f.ToWorld(m.Local));
            }

            for (int id = 0; id < formationCount; id++)
            {
                var f = formations[id];
                f.Velocity = float2.zero;
                if (f.MemberCount > 0)
                {
                    float2 toDestination = f.Destination - f.Position;
                    float distance = math.length(toDestination);
                    if (distance > 1e-4f)
                    {
                        float averageError = errorSum[id] / f.MemberCount;
                        float factor = math.clamp(1f - (averageError - settings.CohesionSlack) / settings.CohesionRange, settings.MinMarchFactor, 1f);
                        float step = math.min(distance, settings.MarchSpeed * factor * SimConstants.DT);
                        f.Velocity = toDestination / distance * (step / SimConstants.DT);
                        f.Position += toDestination / distance * step;
                    }
                }
                formations[id] = f;
            }

            for (int i = 0; i < agentCount; i++)
            {
                var m = members[i];
                if (m.Formation < 0 || m.Slot < 0) continue;
                var f = formations[m.Formation];
                var a = agents[i];
                float2 slot = f.ToWorld(m.Local);
                float2 slotFacingWorld = FormationData.Rotate(m.LocalFacing, f.Facing);
                // Arrive-steering trails a moving target by v * SlowRadius / speed; leading the target by the same
                // amount keeps the soldier on the slot while marching
                float2 target = slot + f.Velocity * (movement.SlowRadius / (movement.MaxSpeed * math.max(a.BaseSpeedScale, 1e-3f)));
                bool atSlot = math.distancesq(a.Position, slot) < settings.FaceSlotDistance * settings.FaceSlotDistance;

                bool bot = hasCombat && combat[i].BotProfile > 0;
                if (hasCombat)
                {
                    var c = combat[i];
                    c.OrderTarget = target;
                    c.OrderFacing = atSlot ? slotFacingWorld : float2.zero;
                    combat[i] = c;
                }
                if (!bot)
                {
                    a.Target = target;
                    a.FaceAgent = atSlot ? AgentData.FaceFixed : -1;
                    a.FaceDirection = slotFacingWorld;
                    agents[i] = a;
                }
            }
        }

        /// <summary>Generates slots for the current members and assigns them (Burst job, deterministic float mode).</summary>
        void Reform(ref FormationData f, NativeArray<AgentData> agents, int agentCount)
        {
            int n = 0;
            float2 centroid = float2.zero;
            for (int i = 0; i < agentCount; i++)
            {
                if (members[i].Formation != f.Id) continue;
                rows[n++] = i;
                centroid += agents[i].Position;
            }
            centroid /= n;

            var job = new ReformJob
            {
                Count = n,
                Type = f.Type,
                Files = f.Files,
                Facing = f.Facing,
                Anchor = f.Position,
                Centroid = centroid,
                AroundMembers = f.Reform == FormationData.ReformAroundMembers ? 1 : 0,
                Settings = settings,
                Rows = rows,
                Agents = agents,
                Relative = relative,
                SlotLocal = slotLocal,
                SlotFacing = slotFacing,
                Cost = cost,
                SlotOfRow = slotOfRow,
                Assignment = assignment,
                AnchorOut = anchorOut,
            };
            job.Schedule().Complete();
            float2 anchor = anchorOut[0];

            // Reforming around the members re-centres the shape where the soldiers stand; the march then carries
            // it to the destination
            if (f.Reform == FormationData.ReformAroundMembers)
            {
                if (math.distancesq(f.Destination, f.Position) < 1e-8f) f.Destination = anchor;
                f.Position = anchor;
            }
            for (int r = 0; r < n; r++)
            {
                int s = slotOfRow[r];
                members[rows[r]] = new FormationMember { Formation = f.Id, Slot = s, Local = slotLocal[s], LocalFacing = slotFacing[s] };
            }
            f.Reform = FormationData.ReformNone;
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        struct ReformJob : IJob
        {
            public int Count;
            public FormationType Type;
            public int Files;
            public float2 Facing;
            public float2 Anchor;
            public float2 Centroid;
            public int AroundMembers;
            public FormationSettings Settings;
            [ReadOnly] public NativeArray<int> Rows;
            [ReadOnly] public NativeArray<AgentData> Agents;
            public NativeArray<float2> Relative;
            public NativeArray<float2> SlotLocal;
            public NativeArray<float2> SlotFacing;
            public NativeArray<float> Cost;
            public NativeArray<int> SlotOfRow;
            public SlotAssignment Assignment;
            public NativeArray<float2> AnchorOut;

            public void Execute()
            {
                int n = Count;
                SlotGenerator.Generate(Type, n, Files, Settings, SlotLocal, SlotFacing);

                float2 anchor = Anchor;
                if (AroundMembers == 1)
                {
                    // Put the anchor where the slot centroid lands on the members' centroid
                    float2 slotCentroid = float2.zero;
                    for (int s = 0; s < n; s++) slotCentroid += SlotLocal[s];
                    anchor = Centroid - FormationData.Rotate(slotCentroid / n, Facing);
                }
                AnchorOut[0] = anchor;

                for (int r = 0; r < n; r++)
                    Relative[r] = FormationData.InverseRotate(Agents[Rows[r]].Position - anchor, Facing);
                for (int r = 0; r < n; r++)
                for (int s = 0; s < n; s++)
                    Cost[r * n + s] = math.distancesq(Relative[r], SlotLocal[s]);
                Assignment.Solve(Cost, n, SlotOfRow);
            }
        }

        public void AddToHash(ref SimHash h, int agentCount)
        {
            h.Add(formations.GetSubArray(0, formationCount));
            h.Add(members.GetSubArray(0, agentCount));
        }

        public void Dispose()
        {
            if (formations.IsCreated) formations.Dispose();
            if (members.IsCreated) members.Dispose();
            if (rows.IsCreated) rows.Dispose();
            if (relative.IsCreated) relative.Dispose();
            if (slotLocal.IsCreated) slotLocal.Dispose();
            if (slotFacing.IsCreated) slotFacing.Dispose();
            if (cost.IsCreated) cost.Dispose();
            if (slotOfRow.IsCreated) slotOfRow.Dispose();
            assignment.Dispose();
            if (errorSum.IsCreated) errorSum.Dispose();
            if (anchorOut.IsCreated) anchorOut.Dispose();
        }
    }
}
