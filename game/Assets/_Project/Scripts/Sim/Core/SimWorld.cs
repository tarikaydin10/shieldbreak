using System;
using Unity.Collections;
using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Grid;

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
            appliedInputHash = SimHash.Create().Value;

            agents = new NativeArray<AgentData>(config.MaxAgents, Allocator.Persistent);
            combat = new NativeArray<CombatData>(config.MaxAgents, Allocator.Persistent);
            spatial = new SpatialHash(layout, config.MaxAgents, Allocator.Persistent);
            positionScratch = new NativeArray<float2>(config.MaxAgents, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            velocityScratch = new NativeArray<float2>(config.MaxAgents, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            facingScratch = new NativeArray<float2>(config.MaxAgents, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            events = new NativeList<CombatEvent>(64, Allocator.Persistent);

            this.combatSetup = Copy(combatSetup);
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

        /// <summary>Gives a player control of one agent (scenario setup). Combat commands only act on the own agent.</summary>
        public void AssignPlayerAgent(ushort playerId, int agentId)
        {
            if (playerId >= SimConstants.MaxPlayers) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (agentId < 0 || agentId >= AgentCount) throw new ArgumentOutOfRangeException(nameof(agentId));
            playerAgent[playerId] = agentId;
        }

        public int PlayerAgent(ushort playerId) => playerId < SimConstants.MaxPlayers ? playerAgent[playerId] : -1;

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

            ApplyMoveIntents();
            AgentSystem.Step(agents, AgentCount, ref spatial, positionScratch, velocityScratch, facingScratch, Config.Movement);
            CombatSystem.Step(Tick, agents, combat, AgentCount, spatial, combatSetup, events);

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
                    ApplyCombatInput(command);
                    break;
                case InputCommandType.PlayerMove:
                    ApplyPlayerMove(command);
                    break;
            }
        }

        /// <summary>Walk and look direction of the player's own agent; they hold until the next PlayerMove.</summary>
        void ApplyPlayerMove(in InputCommand command)
        {
            int id = command.IntArgs.x;
            if (command.PlayerId >= SimConstants.MaxPlayers || playerAgent[command.PlayerId] != id || id < 0 || id >= AgentCount) return;
            float2 move = command.FloatArgs.xy;
            float2 look = command.FloatArgs.zw;
            if (!math.all(math.isfinite(move)) || !math.all(math.isfinite(look))) return;
            float moveLen = math.length(move);
            if (moveLen > 1f) move /= moveLen;
            var a = agents[id];
            if (a.IsDead) return;
            playerMoveIntent[command.PlayerId] = moveLen > 0.05f ? move : float2.zero;
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
                if (id < 0 || id >= AgentCount) continue;
                var a = agents[id];
                if (a.IsDead) continue;
                float2 move = playerMoveIntent[p];
                a.Target = math.lengthsq(move) > 0f ? a.Position + move * Lead : a.Position;
                agents[id] = a;
                var c = combat[id];
                c.OrderTarget = a.Target;
                combat[id] = c;
            }
        }

        void ApplyCombatInput(in InputCommand command)
        {
            int id = command.IntArgs.x;
            // Only the player's own agent obeys; anything else is ignored (never thrown) to keep lockstep robust
            if (!HasCombat || command.PlayerId >= SimConstants.MaxPlayers || playerAgent[command.PlayerId] != id || id < 0 || id >= AgentCount) return;
            var c = combat[id];
            var attack = combatSetup.Attacks[combatSetup.Weapons[c.WeaponId].FirstAttack + c.Direction];
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
            return h.Value;
        }

        public void Dispose()
        {
            inputQueue.Dispose();
            Grid.Dispose();
            spatial.Dispose();
            combatSetup.Dispose();
            if (tickInputs.IsCreated) tickInputs.Dispose();
            if (nextSequence.IsCreated) nextSequence.Dispose();
            if (playerAgent.IsCreated) playerAgent.Dispose();
            if (playerMoveIntent.IsCreated) playerMoveIntent.Dispose();
            if (agents.IsCreated) agents.Dispose();
            if (combat.IsCreated) combat.Dispose();
            if (positionScratch.IsCreated) positionScratch.Dispose();
            if (velocityScratch.IsCreated) velocityScratch.Dispose();
            if (facingScratch.IsCreated) facingScratch.Dispose();
            if (events.IsCreated) events.Dispose();
        }
    }
}
