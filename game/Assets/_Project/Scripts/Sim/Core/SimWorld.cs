using System;
using Unity.Collections;
using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Grid;
using Shieldbreak.Sim.Morale;
using Shieldbreak.Sim.Orders;

namespace Shieldbreak.Sim.Core
{
    public struct SimConfig
    {
        public ulong Seed;
        /// <summary>Locally submitted inputs execute this many ticks in the future (lockstep latency budget).</summary>
        public int InputDelayTicks;
        /// <summary>Map size in cells and world position of the grid's lower-left corner (from map data later).</summary>
        public int GridWidth;
        public int GridHeight;
        public float2 GridOrigin;
        /// <summary>Capacity of the agent arrays; agents are spawned during scenario setup.</summary>
        public int MaxAgents;
        public AgentMoveSettings Movement;
        /// <summary>Capacity of the formation array (0 = no formations).</summary>
        public int MaxFormations;
        public FormationSettings Formations;
        /// <summary>Panic field, morale and rout; Enabled = 0 turns them off (duel).</summary>
        public MoraleSettings Morale;
    }

    /// <summary>
    /// Root of the deterministic simulation. Owns every NativeArray of sim state and advances in fixed ticks.
    /// All external influence goes through the InputQueue.
    /// </summary>
    public sealed class SimWorld : IDisposable
    {
        public readonly SimConfig Config;
        public int Tick { get; private set; }
        public SimRandom Random;
        public readonly WorldGrid Grid;
        public int AgentCount { get; private set; }

        readonly InputQueue inputQueue;
        NativeList<InputCommand> tickInputs;
        NativeArray<int> nextSequence;
        NativeArray<int> playerAgent;
        /// <summary>Walk direction per player (sim state: re-applied every tick, so a late command never stalls the hero).</summary>
        NativeArray<float2> playerMoveIntent;
        NativeArray<int> playerSprint;
        /// <summary>1 while the player is in the command view and the hero's bot brain fights (HeroUnattended).</summary>
        NativeArray<int> playerUnattended;
        NativeArray<int> attackerScratch;
        // Running hash of every applied command; diverging input streams show up in the world hash
        // immediately, before they cause visible divergence.
        ulong appliedInputHash;

        NativeArray<AgentData> agents;
        NativeArray<CombatData> combat;
        SpatialHash spatial;
        NativeArray<float2> positionScratch;
        NativeArray<float2> velocityScratch;
        NativeArray<float2> facingScratch;
        NativeList<CombatEvent> events;
        readonly CombatSetup combatSetup;
        readonly FormationSystem formations;
        readonly MoraleSystem morale;

        /// <param name="combatSetup">Weapons, attacks and rules; copied, so the caller keeps ownership. Null = no combat.</param>
        public SimWorld(SimConfig config, CombatSetup combatSetup = null)
        {
            if (config.InputDelayTicks < 0) throw new ArgumentOutOfRangeException(nameof(config), "InputDelayTicks must be >= 0.");
            if (config.MaxAgents < 0) throw new ArgumentOutOfRangeException(nameof(config), "MaxAgents must be >= 0.");
            if (config.MaxAgents > 0) config.Movement.Validate();
            combatSetup?.Validate();
            Config = config;
            Random = new SimRandom(config.Seed);
            var layout = new GridLayout(config.GridWidth, config.GridHeight, config.GridOrigin);
            Grid = new WorldGrid(layout, Allocator.Persistent);
            inputQueue = new InputQueue(256, Allocator.Persistent);
            tickInputs = new NativeList<InputCommand>(64, Allocator.Persistent);
            nextSequence = new NativeArray<int>(SimConstants.MaxPlayers, Allocator.Persistent);
            playerAgent = new NativeArray<int>(SimConstants.MaxPlayers, Allocator.Persistent);
            for (int p = 0; p < playerAgent.Length; p++) playerAgent[p] = -1;
            playerMoveIntent = new NativeArray<float2>(SimConstants.MaxPlayers, Allocator.Persistent);
            playerSprint = new NativeArray<int>(SimConstants.MaxPlayers, Allocator.Persistent);
            playerUnattended = new NativeArray<int>(SimConstants.MaxPlayers, Allocator.Persistent);
            appliedInputHash = SimHash.Create().Value;

            agents = new NativeArray<AgentData>(config.MaxAgents, Allocator.Persistent);
            combat = new NativeArray<CombatData>(config.MaxAgents, Allocator.Persistent);
            spatial = new SpatialHash(layout, config.MaxAgents, Allocator.Persistent);
            positionScratch = new NativeArray<float2>(config.MaxAgents, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            velocityScratch = new NativeArray<float2>(config.MaxAgents, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            facingScratch = new NativeArray<float2>(config.MaxAgents, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            events = new NativeList<CombatEvent>(64, Allocator.Persistent);
            attackerScratch = new NativeArray<int>(config.MaxAgents, Allocator.Persistent);

            this.combatSetup = Copy(combatSetup);
            formations = new FormationSystem(config.MaxFormations, config.MaxAgents, config.Formations, config.Movement, Grid);
            morale = new MoraleSystem(config.MaxAgents, config.Morale, Grid);
        }

        static CombatSetup Copy(CombatSetup source)
        {
            if (source == null) return new CombatSetup(0, 0, 0, 0, Allocator.Persistent);
            var copy = new CombatSetup(source.Weapons.Length, source.Attacks.Length, source.Keys.Length, source.BotProfiles.Length, Allocator.Persistent)
            {
                Tuning = source.Tuning,
            };
            copy.Weapons.CopyFrom(source.Weapons);
            copy.Attacks.CopyFrom(source.Attacks);
            copy.Keys.CopyFrom(source.Keys);
            copy.BotProfiles.CopyFrom(source.BotProfiles);
            return copy;
        }

        public int PendingInputCount => inputQueue.Count;

        /// <summary>Live agents for reading (presentation, debug). Valid until the next Step.</summary>
        public NativeArray<AgentData>.ReadOnly Agents => agents.GetSubArray(0, AgentCount).AsReadOnly();

        /// <summary>Combat state per agent (same index as Agents). Valid until the next Step.</summary>
        public NativeArray<CombatData>.ReadOnly Combat => combat.GetSubArray(0, AgentCount).AsReadOnly();

        /// <summary>Hits, blocks, deaths of the last Step, for presentation feedback. Valid until the next Step.</summary>
        public NativeArray<CombatEvent>.ReadOnly LastEvents => events.AsArray().AsReadOnly();

        public CombatSetup CombatSetup => combatSetup;
        public bool HasCombat => combatSetup.Weapons.Length > 0;

        /// <summary>Formations for reading (presentation, debug, commander AI). Valid until the next Step.</summary>
        public NativeArray<FormationData>.ReadOnly Formations => formations.Formations;

        /// <summary>Formation membership per agent (same index as Agents). Valid until the next Step.</summary>
        public NativeArray<FormationMember>.ReadOnly FormationMembers => formations.Members(AgentCount);

        public FormationSettings FormationSettings => formations.Settings;

        /// <summary>Morale per agent (same index as Agents). Valid until the next Step.</summary>
        public NativeArray<MoraleData>.ReadOnly Morale => morale.Agents(AgentCount);

        public MoraleSettings MoraleSettings => morale.Settings;
        public bool HasMorale => morale.Enabled;

        /// <summary>Remaining waypoints of a formation's path (debug overlay, command view). Valid until the next Step.</summary>
        public NativeArray<float2>.ReadOnly FormationPath(int formationId) => formations.RemainingPath(formationId);

        /// <summary>
        /// Blocks or weights the cells of a map rectangle (inclusive, cell coordinates). Scenario setup before tick 0:
        /// every peer must make the same calls. Marching formations replan on the next tick.
        /// </summary>
        public void SetNavCost(int2 minCell, int2 maxCell, byte cost) => Grid.SetCost(minCell, maxCell, cost);

        /// <summary>
        /// Adds panic (negative = confidence) for <paramref name="team"/> in the cell at <paramref name="position"/>.
        /// Scenario setup before tick 0 (an army that starts shaken) and tests; every peer must make the same calls.
        /// </summary>
        public void AddPanic(int team, float2 position, float amount)
        {
            if ((uint)team >= SimConstants.MaxTeams || !math.all(math.isfinite(position)) || !math.isfinite(amount)) throw new ArgumentOutOfRangeException(nameof(team));
            int index = Grid.PanicIndex(team, Grid.Layout.ClampedCellIndex(position));
            Grid.Panic[index] += amount;
        }

        /// <summary>Spatial hash from the last Step, for debug overlays.</summary>
        public SpatialHash Spatial => spatial;

        /// <summary>
        /// Adds an agent standing at <paramref name="position"/> with its target on the spot. Scenario setup only:
        /// every peer must run the same spawns before tick 0. Returns the agent id.
        /// </summary>
        public int SpawnAgent(int team, float2 position, float radius, float2 facing = default, int weaponId = 0, int botProfile = 0)
        {
            if (AgentCount >= agents.Length) throw new InvalidOperationException($"Agent capacity {agents.Length} reached.");
            if (HasCombat && (weaponId < 0 || weaponId >= combatSetup.Weapons.Length)) throw new ArgumentOutOfRangeException(nameof(weaponId));
            if (botProfile < 0 || (botProfile > 0 && botProfile >= combatSetup.BotProfiles.Length)) throw new ArgumentOutOfRangeException(nameof(botProfile));
            facing = math.normalizesafe(facing, new float2(0f, 1f));
            int id = AgentCount++;
            agents[id] = new AgentData
            {
                Id = id,
                Team = team,
                Position = position,
                Velocity = float2.zero,
                Facing = facing,
                Target = position,
                Radius = radius,
                SpeedScale = 1f,
                FaceAgent = -1,
                Flags = 0,
                FaceDirection = facing,
                BaseSpeedScale = 1f,
                AccelScale = 1f,
            };
            combat[id] = new CombatData
            {
                RngState = new SimRandom(Config.Seed ^ (0x9E3779B97F4A7C15UL * (ulong)(id + 1))).State,
                State = CombatState.Idle,
                WeaponId = weaponId,
                Loadout = new int4(weaponId, -1, -1, -1),
                ActiveSlot = 0,
                PreviousWeaponId = weaponId,
                BlockPressedTick = -100000,
                LastStaminaUseTick = -100000,
                Stamina = combatSetup.Tuning.StaminaMax,
                Health = combatSetup.Tuning.HealthMax,
                HitIds = new int4(-1),
                BotProfile = botProfile,
                BotTarget = -1,
                BotThreatStartTick = -100000,
                BotReactAtTick = -1,
                BotPlannedBlockTick = -1,
                BotDefenseDecidedFor = -100000,
                OrderTarget = position,
            };
            return id;
        }

        /// <summary>Per-agent movement multipliers (scenario setup), e.g. a hero that runs and turns like a player.</summary>
        public void SetAgentMotion(int agentId, float baseSpeedScale, float accelScale)
        {
            if (agentId < 0 || agentId >= AgentCount) throw new ArgumentOutOfRangeException(nameof(agentId));
            if (baseSpeedScale <= 0f || accelScale <= 0f) throw new ArgumentOutOfRangeException(nameof(baseSpeedScale), "Scales must be positive.");
            var a = agents[agentId];
            a.BaseSpeedScale = baseSpeedScale;
            a.AccelScale = accelScale;
            agents[agentId] = a;
        }

        /// <summary>
        /// Weapon slots of an agent (scenario setup), -1 = empty. Slot 0 must hold a weapon; it is drawn at once.
        /// </summary>
        public void SetLoadout(int agentId, int4 loadout)
        {
            if (agentId < 0 || agentId >= AgentCount) throw new ArgumentOutOfRangeException(nameof(agentId));
            for (int s = 0; s < 4; s++)
            {
                int w = loadout[s];
                if (w < -1 || w >= combatSetup.Weapons.Length || (s == 0 && w < 0))
                    throw new ArgumentOutOfRangeException(nameof(loadout), $"Slot {s}: weapon {w} does not exist.");
            }
            var c = combat[agentId];
            c.Loadout = loadout;
            c.ActiveSlot = 0;
            c.WeaponId = loadout.x;
            c.PreviousWeaponId = loadout.x;
            combat[agentId] = c;
        }

        /// <summary>Gives a player control of one agent (scenario setup). Combat commands only act on the own agent.</summary>
        public void AssignPlayerAgent(ushort playerId, int agentId)
        {
            if (playerId >= SimConstants.MaxPlayers) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (agentId < 0 || agentId >= AgentCount) throw new ArgumentOutOfRangeException(nameof(agentId));
            if (formations.Members(AgentCount)[agentId].Formation >= 0) throw new InvalidOperationException($"Agent {agentId} belongs to a formation.");
            playerAgent[playerId] = agentId;
        }

        /// <summary>
        /// Creates an empty formation (scenario setup). <paramref name="owner"/> is the player id allowed to order it.
        /// <paramref name="position"/> is the anchor: centre of the front rank (centre for Circle).
        /// </summary>
        public int CreateFormation(int team, int owner, FormationType type, int files, float2 position, float2 facing) =>
            formations.Create(team, owner, type, files, position, facing);

        /// <summary>Puts an agent into a formation (scenario setup). Heroes controlled by a player stay outside.</summary>
        public void AddToFormation(int formationId, int agentId)
        {
            if (agentId < 0 || agentId >= AgentCount) throw new ArgumentOutOfRangeException(nameof(agentId));
            for (int p = 0; p < playerAgent.Length; p++)
                if (playerAgent[p] == agentId) throw new InvalidOperationException($"Agent {agentId} is controlled by player {p}.");
            formations.Add(formationId, agentId, agents[agentId].Team);
        }

        /// <summary>
        /// Scenario setup: spawns <paramref name="count"/> soldiers standing in their slots and groups them into a new
        /// formation. Returns the formation id; the agents get contiguous ids in slot order.
        /// </summary>
        public int SpawnFormation(int team, int owner, FormationType type, int files, int count, float2 position, float2 facing,
            float radius, int weaponId = 0, int botProfile = 0)
        {
            if (count < 1 || count > FormationLimits.MaxMembers) throw new ArgumentOutOfRangeException(nameof(count));
            int id = CreateFormation(team, owner, type, files, position, facing);
            var f = formations.Formations[id];
            using var local = new NativeArray<float2>(count, Allocator.Temp);
            using var localFacing = new NativeArray<float2>(count, Allocator.Temp);
            SlotGenerator.Generate(f.Type, count, f.Files, formations.Settings, local, localFacing);
            for (int s = 0; s < count; s++)
            {
                int agent = SpawnAgent(team, f.ToWorld(local[s]), radius, FormationData.Rotate(localFacing[s], f.Facing), weaponId, botProfile);
                formations.Add(id, agent, team);
            }
            return id;
        }

        /// <summary>Order path of sim-internal clients (commander AI): same FormationOrder, applied at once.</summary>
        public bool ApplyFormationOrder(in FormationOrder order, int issuer) => formations.Apply(order, issuer);

        /// <summary>Order path of players: stamps the order like any other input (lockstep).</summary>
        public InputCommand SubmitFormationOrder(ushort playerId, in FormationOrder order)
        {
            order.ToArgs(out int4 intArgs, out float4 floatArgs);
            return Submit(playerId, InputCommandType.FormationOrder, intArgs, floatArgs);
        }

        public int PlayerAgent(ushort playerId) => playerId < SimConstants.MaxPlayers ? playerAgent[playerId] : -1;

        /// <summary>True while the player's hero is unattended (the player is in the command view).</summary>
        public bool IsUnattended(ushort playerId) => playerId < SimConstants.MaxPlayers && playerUnattended[playerId] == 1;

        /// <summary>The scenario allows leaving the hero for the command view (combat and a configured bot level).</summary>
        public bool HasCommandView => HasCombat && formations.Settings.HeroUnattendedLevel > 0 &&
                                      formations.Settings.HeroUnattendedLevel < combatSetup.BotProfiles.Length;

        /// <summary>Enqueues an already stamped command (from a remote peer or a replayed input log).</summary>
        public void Enqueue(in InputCommand command)
        {
            inputQueue.Enqueue(command);
        }

        /// <summary>
        /// Stamps a local input with tick (now + InputDelayTicks) and the player's next sequence number, enqueues it
        /// and returns the stamped command so it can be sent to peers or recorded.
        /// </summary>
        public InputCommand Submit(ushort playerId, InputCommandType type, int4 intArgs = default, float4 floatArgs = default)
        {
            if (playerId >= SimConstants.MaxPlayers) throw new ArgumentOutOfRangeException(nameof(playerId));
            var command = new InputCommand
            {
                Tick = Tick + Config.InputDelayTicks,
                Sequence = nextSequence[playerId]++,
                PlayerId = playerId,
                Type = type,
                IntArgs = intArgs,
                FloatArgs = floatArgs,
            };
            inputQueue.Enqueue(command);
            return command;
        }

        public void Step()
        {
            events.Clear();
            inputQueue.DrainTick(Tick, tickInputs);
            for (int i = 0; i < tickInputs.Length; i++)
                ApplyInput(tickInputs[i]);

            formations.Step(Tick, agents, combat, AgentCount, HasCombat);
            ApplyMoveIntents();
            PostUnattendedHeroes();
            AgentSystem.Step(agents, AgentCount, ref spatial, positionScratch, velocityScratch, facingScratch, Config.Movement, Grid.NavCost);
            CombatSystem.Step(Tick, agents, combat, AgentCount, spatial, combatSetup, events, attackerScratch,
                formations.FormationBuffer, formations.MemberBuffer, formations.Settings);
            morale.Step(Tick, agents, combat, AgentCount, events, spatial, playerAgent, formations.FormationBuffer, formations.MemberBuffer);

            Tick++;
        }

        void ApplyInput(in InputCommand command)
        {
            var h = new SimHash { Value = appliedInputHash };
            h.Add(command);
            appliedInputHash = h.Value;

            switch (command.Type)
            {
                case InputCommandType.None:
                    break;
                case InputCommandType.SetTargetRange:
                {
                    // Invalid ranges are clamped, never thrown: a remote peer's input must not crash the sim
                    int first = math.max(command.IntArgs.x, 0);
                    int end = (int)Math.Min((long)first + math.max(command.IntArgs.y, 0), AgentCount);
                    float2 target = command.FloatArgs.xy;
                    if (!math.all(math.isfinite(target))) break;
                    for (int i = first; i < end; i++)
                    {
                        AgentData a = agents[i];
                        a.Target = target;
                        agents[i] = a;
                        CombatData c = combat[i];
                        c.OrderTarget = target;
                        combat[i] = c;
                    }
                    break;
                }
                case InputCommandType.CombatAttack:
                case InputCommandType.CombatBlock:
                case InputCommandType.CombatFeint:
                case InputCommandType.CombatStep:
                case InputCommandType.CombatSpecial:
                case InputCommandType.CombatSwitchWeapon:
                case InputCommandType.CombatJump:
                    ApplyCombatInput(command);
                    break;
                case InputCommandType.PlayerMove:
                    ApplyPlayerMove(command);
                    break;
                case InputCommandType.FormationOrder:
                    formations.Apply(FormationOrder.FromCommand(command), command.PlayerId);
                    break;
                case InputCommandType.HeroUnattended:
                    ApplyHeroUnattended(command);
                    break;
            }
        }

        /// <summary>
        /// The player leaves the hero for the command view (or takes it back). The hero then fights with the bot brain
        /// at FormationSettings.HeroUnattendedLevel and ignores the player's move and combat commands until taken back.
        /// </summary>
        void ApplyHeroUnattended(in InputCommand command)
        {
            ushort p = command.PlayerId;
            int id = command.IntArgs.x;
            if (p >= SimConstants.MaxPlayers || playerAgent[p] != id || id < 0 || id >= AgentCount || !HasCommandView) return;
            bool leave = command.IntArgs.y != 0;
            if (leave == (playerUnattended[p] == 1)) return;
            var a = agents[id];
            var c = combat[id];
            playerUnattended[p] = leave ? 1 : 0;
            playerMoveIntent[p] = float2.zero;
            playerSprint[p] = 0;
            c.BotTarget = -1;
            c.BotPlannedBlockTick = -1;
            c.BotNextDecisionTick = Tick;
            c.Sprinting = 0;
            if (leave)
            {
                a.Flags |= AgentFlags.Unattended;
                c.BotProfile = formations.Settings.HeroUnattendedLevel;
                c.OrderTarget = a.Position;
                c.OrderFacing = a.Facing;
            }
            else
            {
                // Back to the player: nothing of the bot stays, the block comes down (the player raises it again)
                a.Flags &= ~AgentFlags.Unattended;
                a.Target = a.Position;
                c.BotProfile = 0;
                c.OrderFacing = float2.zero;
                if (c.BlockHeld == 1 && !a.IsDead)
                {
                    var weapon = combatSetup.Weapons[c.WeaponId];
                    CombatSystem.SetBlock(ref c, false, Tick, combatSetup.Attacks[weapon.AttackIndex(c.Direction)], combatSetup.Tuning, events, id);
                }
            }
            agents[id] = a;
            combat[id] = c;
        }

        /// <summary>
        /// Post of every unattended hero: behind the rear rank of the nearest formation of its player, facing that
        /// formation's front (inside the ring of a circle). Without such a formation (none left, or all follow the
        /// hero) the hero holds where it was left.
        /// </summary>
        void PostUnattendedHeroes()
        {
            for (int p = 0; p < SimConstants.MaxPlayers; p++)
            {
                if (playerUnattended[p] == 0) continue;
                int id = playerAgent[p];
                var a = agents[id];
                if (a.IsDead) continue;
                var all = formations.Formations;
                var members = formations.Members(AgentCount);
                int best = -1;
                float bestSq = float.MaxValue;
                for (int f = 0; f < all.Length; f++)
                {
                    var fd = all[f];
                    // A formation following the hero would back away with it: the hero holds instead
                    if (fd.Owner != p || fd.Team != a.Team || fd.MemberCount == 0) continue;
                    if (fd.Behavior == FormationBehavior.Follow && fd.FollowAgent == id) continue;
                    float dsq = math.distancesq(fd.Position, a.Position);
                    if (dsq < bestSq) { bestSq = dsq; best = f; }
                }
                if (best < 0) continue;
                var line = all[best];
                float2 post = line.Position;
                if (line.Type != FormationType.Circle)
                {
                    float rear = 0f;
                    for (int m = 0; m < members.Length; m++)
                        if (members[m].Formation == best && !agents[m].IsDead) rear = math.min(rear, members[m].Local.y);
                    post = line.ToWorld(new float2(0f, rear - formations.Settings.HeroRetreatGap));
                }
                var c = combat[id];
                c.OrderTarget = post;
                c.OrderFacing = line.Facing;
                combat[id] = c;
            }
        }

        /// <summary>Walk and look direction of the player's own agent; they hold until the next PlayerMove.</summary>
        void ApplyPlayerMove(in InputCommand command)
        {
            int id = command.IntArgs.x;
            if (command.PlayerId >= SimConstants.MaxPlayers || playerAgent[command.PlayerId] != id || id < 0 || id >= AgentCount) return;
            if (playerUnattended[command.PlayerId] == 1) return;
            float2 move = command.FloatArgs.xy;
            float2 look = command.FloatArgs.zw;
            if (!math.all(math.isfinite(move)) || !math.all(math.isfinite(look))) return;
            float moveLen = math.length(move);
            if (moveLen > 1f) move /= moveLen;
            var a = agents[id];
            if (a.IsDead) return;
            playerMoveIntent[command.PlayerId] = moveLen > 0.05f ? move : float2.zero;
            playerSprint[command.PlayerId] = command.IntArgs.y != 0 ? 1 : 0;
            if (math.lengthsq(look) > 1e-6f)
            {
                a.FaceAgent = AgentData.FaceFixed;
                a.FaceDirection = math.normalize(look);
            }
            agents[id] = a;
        }

        /// <summary>Steering arrives at its target; a point ahead of each hero turns that into "walk this way".</summary>
        void ApplyMoveIntents()
        {
            const float Lead = 1.5f;
            for (int p = 0; p < SimConstants.MaxPlayers; p++)
            {
                int id = playerAgent[p];
                if (id < 0 || id >= AgentCount || playerUnattended[p] == 1) continue;
                var a = agents[id];
                if (a.IsDead) continue;
                float2 move = playerMoveIntent[p];
                a.Target = math.lengthsq(move) > 0f ? a.Position + move * Lead : a.Position;
                agents[id] = a;
                var c = combat[id];
                c.OrderTarget = a.Target;
                c.Sprinting = playerSprint[p] == 1 && math.lengthsq(move) > 0f ? 1 : 0;
                combat[id] = c;
            }
        }

        void ApplyCombatInput(in InputCommand command)
        {
            int id = command.IntArgs.x;
            // Only the player's own agent obeys; anything else is ignored (never thrown) to keep lockstep robust
            if (!HasCombat || command.PlayerId >= SimConstants.MaxPlayers || playerAgent[command.PlayerId] != id || id < 0 || id >= AgentCount) return;
            if (playerUnattended[command.PlayerId] == 1) return;
            var c = combat[id];
            var weapon = combatSetup.Weapons[c.WeaponId];
            var attack = combatSetup.Attacks[weapon.AttackIndex(c.Direction)];
            var tuning = combatSetup.Tuning;
            switch (command.Type)
            {
                case InputCommandType.CombatAttack:
                    CombatSystem.TryAttack(ref c, command.IntArgs.y, Tick, attack, tuning, events, id);
                    break;
                case InputCommandType.CombatBlock:
                    CombatSystem.SetBlock(ref c, command.IntArgs.y != 0, Tick, attack, tuning, events, id);
                    break;
                case InputCommandType.CombatFeint:
                    CombatSystem.TryFeint(ref c, Tick, attack, tuning, events, id);
                    break;
                case InputCommandType.CombatStep:
                    CombatSystem.TryStep(ref c, command.IntArgs.y, Tick, tuning, events, id);
                    break;
                case InputCommandType.CombatSpecial:
                    CombatSystem.TrySpecial(ref c, Tick, weapon, tuning, events, id);
                    break;
                case InputCommandType.CombatSwitchWeapon:
                    CombatSystem.TrySwitch(ref c, command.IntArgs.y, Tick, tuning, events, id);
                    break;
                case InputCommandType.CombatJump:
                    CombatSystem.TryJump(ref c, Tick, tuning, events, id);
                    break;
            }
            combat[id] = c;
        }

        public ulong ComputeHash()
        {
            var h = SimHash.Create();
            h.Add(Tick);
            h.Add(Random.State);
            h.Add(appliedInputHash);
            h.Add(Grid.NavCost);
            h.Add(AgentCount);
            h.Add(agents.GetSubArray(0, AgentCount));
            h.Add(combat.GetSubArray(0, AgentCount));
            h.Add(playerAgent);
            h.Add(playerMoveIntent);
            h.Add(playerSprint);
            h.Add(playerUnattended);
            h.Add(formations.Count);
            formations.AddToHash(ref h, AgentCount);
            h.Add(Grid.Panic);
            morale.AddToHash(ref h, AgentCount);
            return h.Value;
        }

        public void Dispose()
        {
            inputQueue.Dispose();
            Grid.Dispose();
            spatial.Dispose();
            combatSetup.Dispose();
            formations.Dispose();
            morale.Dispose();
            if (tickInputs.IsCreated) tickInputs.Dispose();
            if (nextSequence.IsCreated) nextSequence.Dispose();
            if (playerAgent.IsCreated) playerAgent.Dispose();
            if (playerMoveIntent.IsCreated) playerMoveIntent.Dispose();
            if (playerSprint.IsCreated) playerSprint.Dispose();
            if (playerUnattended.IsCreated) playerUnattended.Dispose();
            if (attackerScratch.IsCreated) attackerScratch.Dispose();
            if (agents.IsCreated) agents.Dispose();
            if (combat.IsCreated) combat.Dispose();
            if (positionScratch.IsCreated) positionScratch.Dispose();
            if (velocityScratch.IsCreated) velocityScratch.Dispose();
            if (facingScratch.IsCreated) facingScratch.Dispose();
            if (events.IsCreated) events.Dispose();
        }
    }
}
