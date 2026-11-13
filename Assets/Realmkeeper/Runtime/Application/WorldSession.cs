using System;
using System.Collections.Generic;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Application
{
    public enum WorldChangeReason
    {
        Created = 0,
        Loaded = 1,
        Advanced = 2,
        SettingsChanged = 3,

        /// <summary>A registered command committed a change (see <see cref="WorldChange.CommandType"/>).</summary>
        CommandApplied = 4,

        /// <summary>The session was closed; there is no world any more.</summary>
        Closed = 5,

        /// <summary>Only the clock (and the player's hunger) moved; no day was simulated (Phase 5).</summary>
        TimeAdvanced = 6,

        /// <summary>Phase 16: the player is now in another region; the map shown is that region's geography.</summary>
        RegionChanged = 7,
    }

    public sealed class WorldChange
    {
        internal WorldChange(WorldChangeReason reason, IReadOnlyList<HistoricalEvent> newEvents, Type commandType = null)
        {
            Reason = reason;
            NewEvents = newEvents ?? Array.Empty<HistoricalEvent>();
            CommandType = commandType;
        }

        public WorldChangeReason Reason { get; }
        public IReadOnlyList<HistoricalEvent> NewEvents { get; }

        /// <summary>The command that caused a <see cref="WorldChangeReason.CommandApplied"/> change.</summary>
        public Type CommandType { get; }

        /// <summary>True when geography was replaced and visuals must be rebuilt.</summary>
        public bool GeographyChanged => Reason == WorldChangeReason.Created || Reason == WorldChangeReason.Loaded || Reason == WorldChangeReason.RegionChanged;
    }

    /// <summary>
    /// Application service that owns the active world. All state changes go through
    /// Execute(command); presentation reads through queries and listens to WorldChanged.
    /// Single-threaded by design: call from one thread (Unity's main thread).
    /// </summary>
    public sealed partial class WorldSession
    {
        private readonly SimulationScheduler _scheduler;
        private readonly IWorldGenerator _generator;
        private readonly IWorldStore _store;
        private readonly EconomyRules _rules;
        private readonly IRandomSourceFactory _random;
        private readonly CommandRegistry _commands = new CommandRegistry();
        private WorldData _world;
        private bool _dispatching;

        public WorldSession(SimulationScheduler scheduler, IWorldGenerator generator, IWorldStore store, EconomyRules rules,
            IRandomSourceFactory random)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _rules = rules ?? new EconomyRules();
            _random = random ?? throw new ArgumentNullException(nameof(random));

            // Built-in commands that go through the registry.
            _commands.Register<SaveWorldCommand>(new SaveWorldHandler());
            _commands.Register<SetSimulationSpeedCommand>(new SetSpeedHandler());
            _commands.Register<SetTradeRouteSuspendedCommand>(new SetTradeRouteSuspendedHandler());
            PoliticalCommandHandlers.Register(_commands);
            NpcCommandHandlers.Register(_commands);
            PlayerCommandHandlers.Register(_commands);
            CharacterCommandHandlers.Register(_commands);
            ProgressionCommandHandlers.Register(_commands);
            CombatCommandHandlers.Register(_commands);
            SpellCommandHandlers.Register(_commands); // Phase 9
            ItemCommandHandlers.Register(_commands); // Phase 11
            QuestCommandHandlers.Register(_commands); // Phase 12
            Places.PlaceCommandHandlers.Register(_commands); // Phase 13
            Places.RestCommandHandlers.Register(_commands); // Phase 14
            NpcCharacterCommandHandlers.Register(_commands); // Phase 10
            Party.PartyCommandHandlers.Register(_commands); // Phase 15
            Atlas.TravelCommandHandlers.Register(_commands); // Phase 16
            Campaigns.CampaignCommandHandlers.Register(_commands); // Phase 17
        }

        /// <summary>Phase 17: the campaign journal (what could begin here, and every campaign begun), as the player may see it.</summary>
        public Campaigns.CampaignsView GetCampaigns() => HasWorld ? Campaigns.CampaignQueries.View(_world) : new Campaigns.CampaignsView();

        /// <summary>Phase 17: the person a campaign role names now (empty when none), for tools and tests that drive the story through the UI.</summary>
        public NpcId GetCampaignPerson(string campaignId, string role)
        {
            var c = HasWorld ? Simulation.Campaigns.CampaignEngine.Find(_world, campaignId) : null;
            return c == null ? default : Simulation.Campaigns.CampaignEngine.Npc(_world, c, role)?.NpcId ?? default;
        }

        /// <summary>Phase 17: the settlement a campaign role names (empty when none).</summary>
        public SettlementId GetCampaignSettlement(string campaignId, string role)
        {
            var c = HasWorld ? Simulation.Campaigns.CampaignEngine.Find(_world, campaignId) : null;
            return c == null ? default : Simulation.Campaigns.CampaignEngine.Settlement(_world, c, role)?.SettlementId ?? default;
        }

        /// <summary>Phase 17: the quest instance a campaign quest key is (null when not offered).</summary>
        public string GetCampaignQuest(string campaignId, string key)
        {
            var c = HasWorld ? Simulation.Campaigns.CampaignEngine.Find(_world, campaignId) : null;
            return c != null && c.Quests.TryGetValue(key, out var id) && !string.IsNullOrEmpty(id) ? id : null;
        }

        public EconomyRules Rules => _rules;

        /// <summary>Political constants used by queries and commands (the scheduler holds its own copy for the simulation).</summary>
        public PoliticalRules PoliticalRules { get; set; } = new PoliticalRules();

        /// <summary>Phase 4: the NPC engine's tunables, as commands see them.</summary>
        public Simulation.Npcs.NpcRules NpcRules { get; set; } = new Simulation.Npcs.NpcRules();

        /// <summary>Command handlers for this session. Modules register theirs at composition time.</summary>
        public CommandRegistry Commands => _commands;

        public event Action<WorldChange> WorldChanged;

        /// <summary>Number of current <see cref="WorldChanged"/> subscribers (diagnostics and duplicate-subscription tests).</summary>
        public int WorldChangedSubscriberCount => WorldChanged?.GetInvocationList().Length ?? 0;

        public bool HasWorld => _world != null;

        /// <summary>True after <see cref="Close"/>: the session holds no world and accepts no commands.</summary>
        public bool IsClosed { get; private set; }

        /// <summary>
        /// Executes a registered command: resolve the handler by type, let it validate and edit a
        /// working copy, validate the result with <see cref="WorldValidator"/>, then commit.
        /// A failure at any step leaves the committed world unchanged.
        /// </summary>
        public CommandResult Dispatch<TCommand>(TCommand command) where TCommand : ICommand
        {
            if (command == null) return CommandResult.Invalid("A command is required.");
            if (IsClosed) return CommandResult.InvalidState("The world session is closed.");
            if (_dispatching) return CommandResult.InvalidState("Commands cannot be dispatched from inside another command.");
            var handler = _commands.Resolve<TCommand>();
            if (handler == null) return CommandResult.Unsupported($"No handler is registered for {typeof(TCommand).Name}.");

            var context = new CommandContext(this, _world, _random);
            CommandResult result;
            _dispatching = true;
            try
            {
                result = handler.Handle(command, context) ?? CommandResult.Fail($"{typeof(TCommand).Name} returned no result.");
            }
            catch (Exception ex)
            {
                return CommandResult.Fail($"{typeof(TCommand).Name} failed unexpectedly; nothing was changed.", new[] { ex.Message });
            }
            finally
            {
                _dispatching = false;
            }

            if (result.IsSuccess && context.QuestEvents.Count > 0)
            {
                // Phase 12: quest progress, completion and rewards, in the same transaction (ADR-0057).
                var questFailure = QuestRuntime.Process(context);
                if (questFailure != null) return questFailure;
            }

            if (!result.IsSuccess || !context.HasEdits) return result;

            var working = context.Working;
            working.HistoricalEvents.AddRange(context.Events);
            var validation = WorldValidator.Validate(working);
            if (!validation.IsValid)
                return CommandResult.Invalid($"{typeof(TCommand).Name} would leave the world invalid; nothing was changed.", validation.Errors);

            _world = working;
            WorldChanged?.Invoke(new WorldChange(WorldChangeReason.CommandApplied, context.Events, typeof(TCommand)));
            return result;
        }

        /// <summary>Ends the session: drops the world and refuses further commands. Listeners get a <see cref="WorldChangeReason.Closed"/> change.</summary>
        public void Close()
        {
            if (IsClosed) return;
            IsClosed = true;
            _world = null;
            Map = null;
            WorldChanged?.Invoke(new WorldChange(WorldChangeReason.Closed, null));
            WorldChanged = null;
        }

        /// <summary>Geography for rendering. Read-only by convention; edits go through IRegionMapEditor.</summary>
        public RegionMap Map { get; private set; }

        private readonly Dictionary<RegionId, RegionMap> _maps = new Dictionary<RegionId, RegionMap>();

        /// <summary>
        /// Phase 16: a region's geography, regenerated from its own seed the first time it is asked for and kept for the
        /// session (it is derived data, never saved). Null for an unknown region.
        /// </summary>
        public RegionMap MapFor(RegionId id)
        {
            if (!HasWorld) return null;
            if (_maps.TryGetValue(id, out var m)) return m;
            var region = _world.FindRegion(id);
            if (region == null) return null;
            m = _generator.RegenerateRegion(_world, region);
            _maps[id] = m;
            return m;
        }

        private RegionMap MapOfPlayersRegion()
        {
            var r = _world?.Player?.RegionId ?? default;
            return r.IsEmpty || _world.FindRegion(r) == null ? null : MapFor(r);
        }

        /// <summary>Phase 16: after the player crosses into another region, the map shown becomes that region's.</summary>
        internal void FollowPlayersRegion()
        {
            var m = MapOfPlayersRegion();
            if (m == null || ReferenceEquals(m, Map)) return;
            Map = m;
            WorldChanged?.Invoke(new WorldChange(WorldChangeReason.RegionChanged, null));
        }

        /// <summary>
        /// Phase 16: an older, single-region world discovers the lands beyond: the further regions, their people and the
        /// route network are added once, deterministically from the seed. Nothing that exists is regenerated.
        /// </summary>
        public CommandResult DiscoverLandsBeyond(int regions = 6)
        {
            if (!HasWorld) return CommandResult.InvalidState("There is no active world.");
            if (_world.ActiveEncounter != null) return CommandResult.InvalidState(EncounterBlocksTime);
            if (_world.Regions.Count >= regions) return CommandResult.Ok("The world is already connected.");
            var copy = _world.DeepClone();
            var home = MapFor(copy.Regions[0].RegionId);
            try { _generator.EnsureAtlas(copy, home, regions); }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is WorldGenerationException)
            {
                return CommandResult.Fail("The lands beyond could not be made.", new[] { ex.Message });
            }

            var check = WorldValidator.Validate(copy);
            if (!check.IsValid) return CommandResult.Fail("The connected world is invalid.", check.Errors);
            copy.WorldSettings.Regions = regions;
            Replace(copy, home, WorldChangeReason.Loaded);
            return CommandResult.Ok("The roads now lead beyond the vale: " + copy.Regions.Count + " regions.");
        }

        // ----- Commands ------------------------------------------------------------------

        public CommandResult Execute(CreateWorldCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (IsClosed) return CommandResult.InvalidState("The world session is closed.");
            var settings = command.Settings?.Clone() ?? DefaultSettings();
            try
            {
                var generated = _generator.Generate(new WorldGenerationRequest(command.WorldName, command.Seed, settings));
                if (generated.World.Regions.Count > 0) // Phase 16 (ADR-0076): the connected world (and the route network of a single vale)
                {
                    _generator.EnsureAtlas(generated.World, generated.Map, settings.Regions);
                    var check = WorldValidator.Validate(generated.World);
                    if (!check.IsValid) return CommandResult.Fail("World generation failed: the connected world is invalid.", check.Errors);
                }

                Replace(generated.World, generated.Map, WorldChangeReason.Created);
                return CommandResult.Ok($"Created '{generated.World.WorldName}' from seed {command.Seed}.");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is NotSupportedException || ex is WorldGenerationException)
            {
                return CommandResult.Fail("World generation failed.", new[] { ex.Message });
            }
        }

        public AdvanceResult Execute(AdvanceDayCommand command) => Advance(1);

        public AdvanceResult Execute(AdvanceDaysCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            return Advance(command.Days);
        }

        /// <summary>Same as <c>Dispatch(command)</c>; kept for Phase 1 and 2 callers.</summary>
        public CommandResult Execute(SaveWorldCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            return Dispatch(command);
        }

        /// <summary>Loads a world. On any failure the current world stays active and unchanged.</summary>
        public CommandResult Execute(LoadWorldCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (IsClosed) return CommandResult.InvalidState("The world session is closed.");
            var slotError = SaveSlot.Validate(command.SlotName);
            if (slotError != null) return CommandResult.Invalid(slotError);
            var load = _store.Load(command.SlotName);
            if (!load.IsSuccess)
            {
                return load.Status == LoadStatus.NotFound
                    ? CommandResult.NotFound(DescribeLoadFailure(load.Status) + $" ('{command.SlotName}')")
                    : CommandResult.Fail(DescribeLoadFailure(load.Status), load.Errors);
            }

            if (!_generator.SupportsVersion(load.World.GeneratorVersion))
            {
                return CommandResult.Fail("Load failed: unsupported generator version.",
                    new[] { $"The save uses generator v{load.World.GeneratorVersion}; this build has v{_generator.GeneratorVersion}." });
            }

            RegionMap map;
            try
            {
                map = _generator.RegenerateRegion(load.World);
            }
            catch (Exception ex)
            {
                return CommandResult.Fail("Load failed: geography could not be rebuilt.", new[] { ex.Message });
            }

            // Phase 16: the home map holds the home region's settlements (each other region is checked against its own map when it is built).
            var homeRegion = load.World.Regions.Count > 0 ? load.World.Regions[0].RegionId : default;
            var placement = RegionMapValidator.Validate(map, load.World.Settlements.FindAll(s => s.RegionId.IsEmpty || s.RegionId == homeRegion));
            if (!placement.IsValid) return CommandResult.Fail("Load failed: settlements do not match the regenerated region.", placement.Errors);

            // Saves from before the economy (schema v1) get their economy from the terrain now.
            var world = load.World;
            var upgraded = false;
            if (NeedsEconomy(world))
            {
                try
                {
                    _generator.InitializeEconomy(world, map);
                    upgraded = true;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                {
                    return CommandResult.Fail("Load failed: the economy could not be initialized.", new[] { ex.Message });
                }

                var check = WorldValidator.Validate(world);
                if (!check.IsValid) return CommandResult.Fail("Load failed: the upgraded world is invalid.", check.Errors);
            }

            // Phase 10: an older save gains its living-world data (people, sheets, settlement layouts, wildlife) once.
            var living = false;
            try
            {
                living = _generator.EnsureLivingWorld(world, map);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                return CommandResult.Fail("Load failed: the living world could not be added.", new[] { ex.Message });
            }

            if (living)
            {
                var check = WorldValidator.Validate(world);
                if (!check.IsValid) return CommandResult.Fail("Load failed: the upgraded world is invalid.", check.Errors);
            }

            // Phase 16: the route network and the home region's atlas record, once (an older world keeps its single region).
            try
            {
                if (_generator.EnsureAtlas(world, map, Math.Max(1, world.Regions.Count)))
                {
                    var check = WorldValidator.Validate(world);
                    if (!check.IsValid) return CommandResult.Fail("Load failed: the route network is invalid.", check.Errors);
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is WorldGenerationException)
            {
                return CommandResult.Fail("Load failed: the route network could not be built.", new[] { ex.Message });
            }

            Replace(world, map, WorldChangeReason.Loaded);
            return CommandResult.Ok(upgraded ? $"Loaded '{world.WorldName}' (economy added to an older save)." :
                living ? $"Loaded '{world.WorldName}' (living-world data added to an older save)." : $"Loaded '{world.WorldName}'.");
        }

        /// <summary>Same as <c>Dispatch(command)</c>; kept for Phase 1 and 2 callers.</summary>
        public CommandResult Execute(SetSimulationSpeedCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            return Dispatch(command);
        }

        private sealed class SaveWorldHandler : ICommandHandler<SaveWorldCommand>
        {
            public CommandResult Handle(SaveWorldCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no world to save.");
                var slotError = SaveSlot.Validate(command.SlotName);
                if (slotError != null) return CommandResult.Invalid(slotError);
                var result = context.Session._store.Save(context.Committed, command.SlotName);
                return result.IsSuccess ? CommandResult.Ok($"Saved to {result.Location}.") : CommandResult.Fail("Save failed.", new[] { result.Error });
            }
        }

        private sealed class SetSpeedHandler : ICommandHandler<SetSimulationSpeedCommand>
        {
            public CommandResult Handle(SetSimulationSpeedCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                if (!Enum.IsDefined(typeof(SimulationSpeed), command.Speed)) return CommandResult.Invalid("Unknown simulation speed.");
                context.Edit().WorldSettings.SimulationSpeed = command.Speed;
                return CommandResult.Ok();
            }
        }

        // ----- Queries -------------------------------------------------------------------

        public WorldOverview GetOverview()
        {
            if (!HasWorld) return null;
            return new WorldOverview(_world.WorldId, _world.WorldName, _world.Seed, _world.CurrentSimulationDate,
                _world.WorldSettings.SimulationSpeed, _world.Settlements.Count, _world.HistoricalEvents.Count);
        }

        public IReadOnlyList<SettlementSummary> GetSettlements()
        {
            var list = new List<SettlementSummary>();
            if (!HasWorld) return list;
            foreach (var s in _world.Settlements) list.Add(Summarize(s));
            return list;
        }

        /// <summary>Returns null when no settlement has the given ID.</summary>
        public SettlementSummary FindSettlement(SettlementId id)
        {
            if (!HasWorld) return null;
            var s = _world.FindSettlement(id);
            return s == null ? null : Summarize(s);
        }

        /// <summary>Most recent first, optionally only events at or above an importance and/or involving a settlement.</summary>
        public IReadOnlyList<EventSummary> GetRecentEvents(int maxCount, EventImportance minimumImportance = EventImportance.Minor, SettlementId involving = default)
        {
            var list = new List<EventSummary>();
            if (!HasWorld || maxCount <= 0) return list;
            for (var i = _world.HistoricalEvents.Count - 1; i >= 0 && list.Count < maxCount; i--)
            {
                var e = _world.HistoricalEvents[i];
                if (e.Importance < minimumImportance) continue;
                if (!involving.IsEmpty && !Involves(e, involving)) continue;
                list.Add(new EventSummary(e, _world.FindSettlement(e.SettlementId)?.Name, EventFormatter.Format(e, NameOf, FactionNameOf)));
            }

            return list;
        }

        /// <summary>Trade routes, optionally only those starting or ending at a settlement, in stable route order.</summary>
        public IReadOnlyList<TradeRouteSummary> GetTradeRoutes(SettlementId involving = default)
        {
            var list = new List<TradeRouteSummary>();
            if (!HasWorld) return list;
            foreach (var r in _world.TradeRoutes)
            {
                if (!involving.IsEmpty && r.OriginId != involving && r.DestinationId != involving) continue;
                ShipmentData onRoad = null;
                foreach (var s in _world.Shipments)
                {
                    if (s.TradeRouteId == r.TradeRouteId) onRoad = s;
                }

                list.Add(new TradeRouteSummary(r, NameOf(r.OriginId), NameOf(r.DestinationId), onRoad, _world.CurrentSimulationDate));
            }

            return list;
        }

        public IReadOnlyList<RegionSummary> GetRegions()
        {
            var list = new List<RegionSummary>();
            if (!HasWorld) return list;
            foreach (var r in _world.Regions) list.Add(new RegionSummary(r, CountSettlements(r.RegionId)));
            return list;
        }

        /// <summary>Returns null when no region has the given ID.</summary>
        public RegionSummary FindRegion(RegionId id)
        {
            var r = _world?.FindRegion(id);
            return r == null ? null : new RegionSummary(r, CountSettlements(id));
        }

        /// <summary>The player character, or null when the world has none.</summary>
        public PlayerSummary GetPlayer()
        {
            var p = _world?.Player;
            return p == null ? null : new PlayerSummary(p, NameOf(p.SettlementId));
        }

        /// <summary>Named residents of a settlement, in stable record order.</summary>
        public IReadOnlyList<NpcSummary> GetResidents(SettlementId settlementId)
        {
            var list = new List<NpcSummary>();
            if (!HasWorld) return list;
            foreach (var n in _world.Npcs)
            {
                if (n.HomeSettlementId == settlementId) list.Add(new NpcSummary(n));
            }

            return list;
        }

        /// <summary>Returns null when no named person has the given ID.</summary>
        public NpcSummary FindNpc(NpcId id)
        {
            var n = _world?.FindNpc(id);
            return n == null ? null : new NpcSummary(n);
        }

        private int CountSettlements(RegionId id)
        {
            var count = 0;
            foreach (var s in _world.Settlements)
            {
                if (s.RegionId == id) count++;
            }

            return count;
        }

        /// <summary>Total quantity of a resource across all settlements plus goods on the road (for checks and debugging).</summary>
        public long TotalOf(Domain.Economy.ResourceTypeId resource)
        {
            if (!HasWorld) return 0;
            long total = 0;
            foreach (var s in _world.Settlements) total += s.ResourceInventory.Get(resource);
            foreach (var s in _world.Shipments)
            {
                if (s.ResourceTypeId == resource) total += s.Quantity;
            }

            return total;
        }

        // ----- Internals -----------------------------------------------------------------

        public static WorldSettings DefaultSettings()
        {
            var settings = new WorldSettings();
            settings.EnabledSimulationSystems.AddRange(StandardSimulationSystems.AllIds);
            return settings;
        }

        /// <summary>Phase 16: the settings of a new game: the standard systems and a connected world of six regions.</summary>
        public static WorldSettings NewGameSettings()
        {
            var settings = DefaultSettings();
            settings.Regions = 6;
            return settings;
        }

        private AdvanceResult Advance(int days)
        {
            if (IsClosed || !HasWorld) return new AdvanceResult(false, "There is no active world.", null, 0, null);
            if (_world.ActiveEncounter != null) return new AdvanceResult(false, EncounterBlocksTime, null, 0, null);
            if (days > SimulationScheduler.MaxDaysPerRun)
                return new AdvanceResult(false, $"Cannot advance more than {SimulationScheduler.MaxDaysPerRun} days at once.", null, 0, null);

            var run = _scheduler.AdvanceDays(_world, days);
            if (run.DaysCompleted > 0)
            {
                _world = run.World;
                WorldChanged?.Invoke(new WorldChange(WorldChangeReason.Advanced, run.Events));
            }

            return run.Completed
                ? new AdvanceResult(true, $"Advanced {days} day(s).", null, days, run.Events)
                : new AdvanceResult(false, $"Simulation stopped after {run.DaysCompleted} of {days} day(s).", run.Errors, run.DaysCompleted, run.Events);
        }

        private void Replace(WorldData world, RegionMap map, WorldChangeReason reason)
        {
            // Phase 21: a new, loaded or newly connected world has today's weather at once, not from its first midnight
            // (deterministic from the seed and the saved weather; a save that has it is unchanged).
            if (world != null && world.CurrentSimulationDate.IsValidDate) WeatherModel.AdvanceTo(world, world.CurrentSimulationDate.ToDayNumber());
            if (world != null) Simulation.Commerce.CommerceGenesis.EnsureKinds(world); // Phase 21 (ADR-0106): shop kinds; specialists in unused shop buildings
            _world = world;
            _maps.Clear();
            if (map != null && world?.Regions.Count > 0) { if (map.RegionId.IsEmpty) map.RegionId = world.Regions[0].RegionId; _maps[map.RegionId] = map; }
            Map = MapOfPlayersRegion() ?? map;
            WorldChanged?.Invoke(new WorldChange(reason, null));
        }

        private SettlementSummary Summarize(SettlementData s)
        {
            var trading = false;
            foreach (var shipment in _world.Shipments)
            {
                if (!shipment.IsReturning && (shipment.OriginId == s.SettlementId || shipment.DestinationId == s.SettlementId)) trading = true;
            }

            return new SettlementSummary(s, _world.FindFaction(s.FactionId)?.Name, _rules, trading);
        }

        // ----- Political queries (Phase 3) ---------------------------------------------------

        /// <summary>All factions in stable record order. Dissolved factions are included unless excluded: they are history.</summary>
        public IReadOnlyList<FactionSummary> GetFactions(bool includeDissolved = true)
        {
            var list = new List<FactionSummary>();
            if (!HasWorld) return list;
            foreach (var f in _world.Factions)
            {
                if (!includeDissolved && f.IsDissolved) continue;
                list.Add(new FactionSummary(_world, f, PoliticalRules));
            }

            return list;
        }

        /// <summary>Returns null when no faction has the given ID.</summary>
        public FactionSummary FindFaction(FactionId id)
        {
            if (!HasWorld) return null;
            var f = _world.FindFaction(id);
            return f == null ? null : new FactionSummary(_world, f, PoliticalRules);
        }

        /// <summary>Every relationship, optionally only those involving one faction.</summary>
        public IReadOnlyList<FactionRelationSummary> GetRelations(FactionId involving = default)
        {
            var list = new List<FactionRelationSummary>();
            if (!HasWorld) return list;
            foreach (var r in _world.Relations)
            {
                if (!involving.IsEmpty && !r.Involves(involving)) continue;
                var a = _world.FindFaction(r.FactionA);
                var b = _world.FindFaction(r.FactionB);
                if (a == null || b == null) continue;
                var live = !a.IsDissolved && !b.IsDissolved;
                list.Add(new FactionRelationSummary(r, a, b, live ? PoliticalMath.EvaluateRelation(_world, a, b, r, PoliticalRules) : null));
            }

            return list;
        }

        /// <summary>Territorial claims, optionally only those on one settlement and/or only open ones.</summary>
        public IReadOnlyList<TerritorialClaimSummary> GetClaims(SettlementId settlementId = default, bool openOnly = false)
        {
            var list = new List<TerritorialClaimSummary>();
            if (!HasWorld) return list;
            foreach (var c in _world.Claims)
            {
                if (!settlementId.IsEmpty && c.SettlementId != settlementId) continue;
                if (openOnly && !c.IsOpen) continue;
                list.Add(new TerritorialClaimSummary(c, _world.FindFaction(c.ClaimantId)?.Name, NameOf(c.SettlementId)));
            }

            return list;
        }

        /// <summary>Governance, faction presence, affiliation and claims of one settlement; null when it does not exist.</summary>
        public SettlementPoliticsSummary GetSettlementPolitics(SettlementId id)
        {
            if (!HasWorld) return null;
            var s = _world.FindSettlement(id);
            return s == null ? null : new SettlementPoliticsSummary(_world, s);
        }

        /// <summary>Most recent first: events that involve a faction (including after it has dissolved).</summary>
        public IReadOnlyList<EventSummary> GetFactionEvents(FactionId factionId, int maxCount)
        {
            var list = new List<EventSummary>();
            if (!HasWorld || maxCount <= 0 || factionId.IsEmpty) return list;
            for (var i = _world.HistoricalEvents.Count - 1; i >= 0 && list.Count < maxCount; i--)
            {
                var e = _world.HistoricalEvents[i];
                var involved = false;
                foreach (var id in e.FactionIds)
                {
                    if (id == factionId) involved = true;
                }

                if (involved) list.Add(new EventSummary(e, _world.FindSettlement(e.SettlementId)?.Name, EventFormatter.Format(e, NameOf, FactionNameOf)));
            }

            return list;
        }

        private string FactionNameOf(FactionId id) => _world?.FindFaction(id)?.Name;

        private string NameOf(SettlementId id) => _world?.FindSettlement(id)?.Name;

        private static bool Involves(HistoricalEvent e, SettlementId id)
        {
            foreach (var s in e.SettlementIds)
            {
                if (s == id) return true;
            }

            return false;
        }

        private static bool NeedsEconomy(WorldData world)
        {
            if (world.TradeRoutes.Count == 0 && world.Settlements.Count > 1) return true;
            foreach (var s in world.Settlements)
            {
                if (s.Economy == null || !s.Economy.IsInitialized) return true;
            }

            return false;
        }

        private static string DescribeLoadFailure(LoadStatus status)
        {
            switch (status)
            {
                case LoadStatus.NotFound: return "No saved world was found in that slot.";
                case LoadStatus.Corrupt: return "The save file is damaged or is not a Realmkeeper world.";
                case LoadStatus.UnsupportedVersion: return "The save file was written by an unsupported version.";
                case LoadStatus.Invalid: return "The save file contains invalid world data.";
                default: return "Load failed.";
            }
        }
    }
}
