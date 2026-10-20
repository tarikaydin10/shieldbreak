using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Grid;
using Shieldbreak.Sim.Nav;
using Shieldbreak.Sim.Orders;

namespace Shieldbreak.Sim.Formations
{
    /// <summary>
    /// Formation layer (CLAUDE.md 3.1, 3.6): owns formations and per-agent membership, applies FormationOrders,
    /// runs the behaviors (hold, advance, charge, follow, retreat) by choosing where to march, plans the anchor's path
    /// on the WorldGrid (A*), marches it, closes gaps after losses and hands every member its slot as the steering
    /// target. Agents always steer to their own slot, never to the formation's destination; how far they may leave it
    /// to fight is decided by Combat/Context/FormationCombatRules.
    /// </summary>
    public sealed class FormationSystem : IDisposable
    {
        readonly FormationSettings settings;
        readonly AgentMoveSettings movement;
        readonly WorldGrid grid;
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

        // Paths: MaxPathPoints waypoints per formation
        NativeArray<float2> paths;
        GridAStar astar;
        NativeArray<int> pathResult;
        int clearanceVersion = -1;

        public FormationSystem(int maxFormations, int maxAgents, in FormationSettings settings, in AgentMoveSettings movement, WorldGrid grid)
        {
            if (maxFormations < 0) throw new ArgumentOutOfRangeException(nameof(maxFormations));
            if (maxFormations > 0) settings.Validate(movement.MaxSpeed);
            this.settings = settings;
            this.movement = movement;
            this.grid = grid;
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
            paths = new NativeArray<float2>(maxFormations * FormationLimits.MaxPathPoints, Allocator.Persistent);
            pathResult = new NativeArray<int>(3, Allocator.Persistent);
            if (maxFormations > 0) astar = new GridAStar(grid.Layout, Allocator.Persistent);
        }

        /// <summary>Waypoints of a formation's current path, the next one first (empty when it stands).</summary>
        public NativeArray<float2>.ReadOnly RemainingPath(int formation)
        {
            var f = formations[formation];
            int start = formation * FormationLimits.MaxPathPoints;
            return paths.GetSubArray(start + f.PathIndex, math.max(0, f.PathCount - f.PathIndex)).AsReadOnly();
        }

        public int Count => formationCount;
        /// <summary>Full buffers for the combat job (formation context of each soldier); read-only there.</summary>
        internal NativeArray<FormationData> FormationBuffer => formations;
        internal NativeArray<FormationMember> MemberBuffer => members;
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
                TargetFacing = math.normalizesafe(facing, new float2(0f, 1f)),
                Destination = position,
                Behavior = FormationBehavior.Hold,
                FollowAgent = -1,
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
            bool move = (order.Flags & FormationOrderFlags.Move) != 0 && math.all(math.isfinite(order.Destination)) && math.all(math.isfinite(order.Facing));
            if (move)
            {
                f.Destination = ClampToMap(order.Destination);
                f.NeedsPath = 1;
                // The formation turns in Step: towards its path on a long march, to this facing on arrival
                f.TargetFacing = math.lengthsq(order.Facing) > 1e-8f ? math.normalize(order.Facing) : f.Facing;
                changed = true;
            }
            bool behavior = (order.Flags & FormationOrderFlags.Behavior) != 0 && (uint)order.Behavior < FormationLimits.BehaviorCount &&
                            (order.Behavior != FormationBehavior.Follow || (order.FollowAgent >= 0 && order.FollowAgent < members.Length));
            if (behavior)
            {
                f.Behavior = order.Behavior;
                f.FollowAgent = order.Behavior == FormationBehavior.Follow ? order.FollowAgent : -1;
                // Look at the target on the next tick; a retreat to a given point has nothing to look for
                f.NextBehaviorTick = order.Behavior == FormationBehavior.Retreat && move ? int.MaxValue : 0;
                if (order.Behavior == FormationBehavior.Hold && !move) Stop(ref f);
                changed = true;
            }
            else if (move)
            {
                // A plain move order means: go there and hold
                f.Behavior = FormationBehavior.Hold;
                f.FollowAgent = -1;
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

            // The dead and the routing (Sim/Morale) leave their formation; it closes the gaps
            for (int i = 0; i < agentCount; i++)
            {
                var m = members[i];
                if (m.Formation < 0 || !(agents[i].IsDead || agents[i].IsRouting)) continue;
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
                if (f.MemberCount > 0) UpdateBehavior(ref f, tick, agents, agentCount);
                if (f.Reform != FormationData.ReformNone && tick >= f.NextReformTick && f.MemberCount > 0)
                    Reform(ref f, agents, agentCount);
                // Plan after the reform, which may have moved the anchor; a changed map replans whoever marches
                bool marching = math.distancesq(f.Destination, f.Position) > 1e-8f;
                if (f.MemberCount > 0 && (f.NeedsPath == 1 || (marching && f.PathGridVersion != grid.Version)))
                    PlanPath(ref f);
                formations[id] = f;
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
                if (f.MemberCount > 0 && f.PathIndex < f.PathCount)
                {
                    float averageError = errorSum[id] / f.MemberCount;
                    float factor = math.clamp(1f - (averageError - settings.CohesionSlack) / settings.CohesionRange, settings.MinMarchFactor, 1f);
                    float step = settings.MarchSpeed * factor * SimConstants.DT;
                    float2 start = f.Position;
                    // Walk the waypoints; a step that reaches one carries on towards the next
                    while (step > 0f && f.PathIndex < f.PathCount)
                    {
                        float2 waypoint = paths[id * FormationLimits.MaxPathPoints + f.PathIndex];
                        float2 to = waypoint - f.Position;
                        float distance = math.length(to);
                        if (distance <= step)
                        {
                            f.Position = waypoint;
                            step -= distance;
                            f.PathIndex++;
                        }
                        else
                        {
                            f.Position += to / distance * step;
                            step = 0f;
                        }
                    }
                    f.Velocity = (f.Position - start) / SimConstants.DT;
                    if (f.PathIndex == f.PathCount)
                    {
                        if (f.PathTruncated == 1) f.NeedsPath = 1;
                        else f.Destination = f.Position;
                    }
                }
                if (f.MemberCount > 0) Turn(ref f, tick);
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

        /// <summary>
        /// Behaviors choose where the formation marches; the march, path and turn are the same as for a move order.
        /// Advance and Charge head for the nearest enemy formation (Advance stops a contact gap short, Charge goes in),
        /// Follow trails its agent, Retreat falls back once and then holds. Targets are looked at every
        /// BehaviorIntervalTicks and the march is only re-ordered when the target point moved noticeably.
        /// </summary>
        void UpdateBehavior(ref FormationData f, int tick, NativeArray<AgentData> agents, int agentCount)
        {
            if (f.Behavior == FormationBehavior.Hold) return;
            if (f.Behavior == FormationBehavior.Retreat)
            {
                if (tick >= f.NextBehaviorTick)
                {
                    f.NextBehaviorTick = int.MaxValue;
                    float2 away = FindEnemy(f, agents, agentCount, out float2 aim, out _) ? math.normalizesafe(f.Position - aim, -f.Facing) : -f.Facing;
                    f.Destination = ClampToMap(f.Position + away * settings.RetreatDistance);
                    f.TargetFacing = -away;
                    f.NeedsPath = 1;
                }
                else if (f.NeedsPath == 0 && f.PathIndex >= f.PathCount && math.distancesq(f.Destination, f.Position) < 1e-8f)
                {
                    f.Behavior = FormationBehavior.Hold;
                }
                return;
            }
            if (tick < f.NextBehaviorTick) return;
            f.NextBehaviorTick = tick + settings.BehaviorIntervalTicks;

            float2 goal, facing;
            if (f.Behavior == FormationBehavior.Follow)
            {
                int leader = f.FollowAgent;
                if (leader < 0 || leader >= agentCount || agents[leader].IsDead || agents[leader].Team != f.Team)
                {
                    f.Behavior = FormationBehavior.Hold;
                    f.FollowAgent = -1;
                    Stop(ref f);
                    return;
                }
                // Trail the leader along the line from the formation to it, so looking around does not move the formation
                float2 to = agents[leader].Position - f.Position;
                float distance = math.length(to);
                if (distance <= settings.FollowDistance)
                {
                    // The leader came back towards the formation: stop instead of marching through it
                    if (math.distancesq(f.Destination, f.Position) > 1e-8f) Stop(ref f);
                    return;
                }
                facing = to / distance;
                goal = agents[leader].Position - facing * settings.FollowDistance;
            }
            else
            {
                if (!FindEnemy(f, agents, agentCount, out float2 aim, out float contact))
                {
                    f.Behavior = FormationBehavior.Hold;
                    Stop(ref f);
                    return;
                }
                float2 to = aim - f.Position;
                facing = math.lengthsq(to) > 1e-8f ? math.normalize(to) : f.Facing;
                float gap = f.Behavior == FormationBehavior.Charge ? 0f : settings.AdvanceContactGap;
                goal = f.Position + facing * math.max(0f, contact - gap);
                // In contact the formation keeps turning to the enemy even when it does not march
                f.TargetFacing = facing;
            }
            goal = ClampToMap(goal);
            if (math.distancesq(goal, f.Destination) > settings.RetargetDistance * settings.RetargetDistance)
            {
                f.Destination = goal;
                f.TargetFacing = facing;
                f.NeedsPath = 1;
            }
        }

        /// <summary>Halts where the anchor stands, keeping the current facing.</summary>
        static void Stop(ref FormationData f)
        {
            f.Destination = f.Position;
            f.TargetFacing = f.Facing;
            f.NeedsPath = 1;
        }

        /// <summary>
        /// The enemy this formation goes for: the formation with the nearest anchor (lowest id on ties), else the
        /// nearest enemy soldier outside formations that still fights (not routing). <paramref name="aim"/> is where to head (the enemy's centre),
        /// <paramref name="contact"/> how far along that direction its nearest soldier stands from the anchor.
        /// </summary>
        bool FindEnemy(in FormationData f, NativeArray<AgentData> agents, int agentCount, out float2 aim, out float contact)
        {
            int target = -1;
            float bestSq = float.MaxValue;
            for (int g = 0; g < formationCount; g++)
            {
                var other = formations[g];
                if (other.Team == f.Team || other.MemberCount == 0) continue;
                float dsq = math.distancesq(other.Position, f.Position);
                if (dsq < bestSq)
                {
                    bestSq = dsq;
                    target = g;
                }
            }

            aim = f.Position;
            contact = 0f;
            if (target >= 0)
            {
                float2 sum = float2.zero;
                int n = 0;
                for (int i = 0; i < agentCount; i++)
                {
                    if (members[i].Formation != target || agents[i].IsDead) continue;
                    sum += agents[i].Position;
                    n++;
                }
                aim = n > 0 ? sum / n : formations[target].Position;
                float2 dir = math.normalizesafe(aim - f.Position, f.Facing);
                contact = float.MaxValue;
                for (int i = 0; i < agentCount; i++)
                {
                    if (members[i].Formation != target || agents[i].IsDead) continue;
                    contact = math.min(contact, math.dot(agents[i].Position - f.Position, dir));
                }
                if (n == 0) contact = math.dot(aim - f.Position, dir);
                return true;
            }

            int nearest = -1;
            bestSq = float.MaxValue;
            for (int i = 0; i < agentCount; i++)
            {
                var a = agents[i];
                if (a.IsDead || a.IsRouting || a.Team == f.Team) continue;
                float dsq = math.distancesq(a.Position, f.Position);
                if (dsq < bestSq)
                {
                    bestSq = dsq;
                    nearest = i;
                }
            }
            if (nearest < 0) return false;
            aim = agents[nearest].Position;
            contact = math.sqrt(bestSq);
            return true;
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
                else f.NeedsPath = 1;
                f.Position = anchor;
            }
            for (int r = 0; r < n; r++)
            {
                int s = slotOfRow[r];
                members[rows[r]] = new FormationMember { Formation = f.Id, Slot = s, Local = slotLocal[s], LocalFacing = slotFacing[s] };
            }
            f.Reform = FormationData.ReformNone;
        }

        /// <summary>
        /// Turns towards the path while marching a long way, else towards the ordered facing. Small turns wheel around
        /// the anchor, limited so the flank never outruns a soldier; large ones re-form at once (the rear rank becomes
        /// the front): around the soldiers before a march, on the spot at the destination.
        /// </summary>
        void Turn(ref FormationData f, int tick)
        {
            bool marching = f.PathIndex < f.PathCount;
            float2 wanted = f.TargetFacing;
            if (marching && f.FaceMarch == 1)
            {
                float2 toWaypoint = paths[f.Id * FormationLimits.MaxPathPoints + f.PathIndex] - f.Position;
                if (math.lengthsq(toWaypoint) > 1e-6f) wanted = math.normalize(toWaypoint);
            }
            float cos = math.dot(f.Facing, wanted);
            if (cos > 0.99999f) return;
            if (cos < settings.ReformTurnCos)
            {
                f.Facing = wanted;
                if (f.Reform != FormationData.ReformAroundMembers)
                    f.Reform = marching ? FormationData.ReformAroundMembers : FormationData.ReformKeepAnchor;
                f.NextReformTick = tick + 1;
                return;
            }
            float halfFront = math.max(FormationLinePlanner.FrontWidth(f.Type, f.Files, f.MemberCount, settings) * 0.5f, 1f);
            float maxStep = settings.WheelFlankSpeed / halfFront * SimConstants.DT;
            f.Facing = RotateTowards(f.Facing, wanted, maxStep);
        }

        /// <summary>
        /// Rotates unit vector <paramref name="from"/> towards <paramref name="to"/> by at most <paramref name="maxRadians"/>
        /// (small, below 0.3). Taylor series instead of sin/cos: this runs in managed code, where trigonometry is not
        /// guaranteed bit-identical between runtimes.
        /// </summary>
        static float2 RotateTowards(float2 from, float2 to, float maxRadians)
        {
            float t = math.min(maxRadians, 0.3f), t2 = t * t;
            float c = 1f - t2 * 0.5f + t2 * t2 * (1f / 24f);
            float s = t * (1f - t2 * (1f / 6f) + t2 * t2 * (1f / 120f));
            if (math.dot(from, to) >= c) return to;
            if (from.x * to.y - from.y * to.x < 0f) s = -s;
            return math.normalize(new float2(from.x * c - from.y * s, from.x * s + from.y * c));
        }

        /// <summary>
        /// Plans the anchor's path to the destination (Burst job). Wide formations want room to the sides; an
        /// unreachable destination becomes the reachable point closest to it.
        /// </summary>
        void PlanPath(ref FormationData f)
        {
            f.NeedsPath = 0;
            f.PathGridVersion = grid.Version;
            f.PathIndex = 0;
            f.PathCount = 0;
            f.PathTruncated = 0;
            if (math.distancesq(f.Destination, f.Position) < 1e-8f) return;

            float halfFront = FormationLinePlanner.FrontWidth(f.Type, f.Files, f.MemberCount, settings) * 0.5f;
            var job = new PathJob
            {
                AStar = astar,
                NavCost = grid.NavCost,
                RefreshClearance = clearanceVersion != grid.Version ? 1 : 0,
                Request = new PathRequest
                {
                    Start = f.Position,
                    Goal = f.Destination,
                    RequiredClearance = math.min((int)math.ceil(halfFront / SimConstants.CellSize), settings.PathMaxClearance),
                    ClearancePenalty = settings.PathClearancePenalty,
                },
                Output = paths.GetSubArray(f.Id * FormationLimits.MaxPathPoints, FormationLimits.MaxPathPoints),
                Result = pathResult,
            };
            job.Schedule().Complete();
            clearanceVersion = grid.Version;
            f.PathCount = pathResult[0];
            float length = 0f;
            float2 previous = f.Position;
            for (int k = 0; k < f.PathCount; k++)
            {
                float2 p = paths[f.Id * FormationLimits.MaxPathPoints + k];
                length += math.distance(previous, p);
                previous = p;
            }
            f.FaceMarch = length >= settings.FaceMarchMinDistance ? 1 : 0;
            f.PathTruncated = pathResult[1];
            if (pathResult[2] == 0 && f.PathCount > 0) f.Destination = paths[f.Id * FormationLimits.MaxPathPoints + f.PathCount - 1];
            if (f.PathCount == 0) f.Destination = f.Position;
        }

        float2 ClampToMap(float2 p)
        {
            var layout = grid.Layout;
            const float inset = 0.5f;
            return math.clamp(p, layout.Origin + inset, layout.Origin + new float2(layout.Width, layout.Height) * SimConstants.CellSize - inset);
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        struct PathJob : IJob
        {
            public GridAStar AStar;
            [ReadOnly] public NativeArray<byte> NavCost;
            public int RefreshClearance;
            public PathRequest Request;
            public NativeArray<float2> Output;
            /// <summary>(waypoints, truncated, reached goal).</summary>
            public NativeArray<int> Result;

            public void Execute()
            {
                if (RefreshClearance == 1) AStar.RefreshClearance(NavCost);
                int count = AStar.FindPath(NavCost, Request, Output, out bool truncated, out bool reached);
                Result[0] = count;
                Result[1] = truncated ? 1 : 0;
                Result[2] = reached ? 1 : 0;
            }
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
            h.Add(paths);
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
            if (paths.IsCreated) paths.Dispose();
            if (pathResult.IsCreated) pathResult.Dispose();
            astar.Dispose();
        }
    }
}
