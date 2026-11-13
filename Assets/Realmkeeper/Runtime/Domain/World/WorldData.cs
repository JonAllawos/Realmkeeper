using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    public enum SimulationSpeed
    {
        Slow = 0,
        Normal = 1,
        Fast = 2,
        VeryFast = 3,
    }

    public sealed class WorldSettings
    {
        public SimulationSpeed SimulationSpeed { get; set; } = SimulationSpeed.Normal;
        public WorldDate StartingDate { get; set; } = new WorldDate(412, 3, 1);

        /// <summary>Simulation system IDs that run each day. Unknown IDs are ignored by the scheduler.</summary>
        public List<string> EnabledSimulationSystems { get; set; } = new List<string>();

        /// <summary>Phase 12 (ADR-0061): how tagged quest and dialogue content is presented.</summary>
        public Realmkeeper.Domain.Quests.ContentSettingsData Content { get; set; } = new Realmkeeper.Domain.Quests.ContentSettingsData();

        /// <summary>Phase 16: how many connected regions a new world has (1: the single vale of earlier phases).</summary>
        public int Regions { get; set; } = 1;

        public WorldSettings Clone() => new WorldSettings
        {
            SimulationSpeed = SimulationSpeed,
            StartingDate = StartingDate,
            EnabledSimulationSystems = new List<string>(EnabledSimulationSystems ?? new List<string>()),
            Content = Content?.Clone() ?? new Realmkeeper.Domain.Quests.ContentSettingsData(),
            Regions = Regions,
        };
    }

    /// <summary>
    /// The authoritative, serializable state of one world. Contains no Unity types and no
    /// references to presentation objects. Terrain geometry is NOT stored here: it is
    /// reproduced from <see cref="Seed"/> + <see cref="GeneratorVersion"/> (see ADR-0005).
    /// </summary>
    public sealed class WorldData
    {
        /// <summary>Schema version written by this build. Bump when the save layout changes.</summary>
        public const int CurrentSchemaVersion = 22; // Phase 20A (ADR-0104): eye colour, the per-hit ancestry boon flag; Phase 20 (ADR-0101): lineages, identity, height and build; // Phase 18 (ADR-0096): discoveries with kind and certainty; Phase 18 places
        public const int MaxNameLength = 64;

        public WorldId WorldId { get; set; }
        public string WorldName { get; set; }
        public long Seed { get; set; }
        public int GeneratorVersion { get; set; }
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public WorldDate CurrentSimulationDate { get; set; }
        public WorldSettings WorldSettings { get; set; } = new WorldSettings();
        /// <summary>Geographic regions; every settlement references one by <see cref="RegionId"/>.</summary>
        public List<RegionData> Regions { get; set; } = new List<RegionData>();

        public List<SettlementData> Settlements { get; set; } = new List<SettlementData>();

        /// <summary>The player character, or null for worlds that have none (e.g. focused test worlds).</summary>
        public PlayerCharacterData Player { get; set; }
        public List<FactionData> Factions { get; set; } = new List<FactionData>();
        public List<NpcData> Npcs { get; set; } = new List<NpcData>();
        public List<HistoricalEvent> HistoricalEvents { get; set; } = new List<HistoricalEvent>();

        /// <summary>One-way trade routes (generated in pairs along the roads).</summary>
        public List<TradeRouteData> TradeRoutes { get; set; } = new List<TradeRouteData>();

        /// <summary>Goods currently on the road.</summary>
        public List<ShipmentData> Shipments { get; set; } = new List<ShipmentData>();

        /// <summary>Relationships between factions, one record per pair (FactionA &lt; FactionB).</summary>
        public List<FactionRelationData> Relations { get; set; } = new List<FactionRelationData>();

        /// <summary>Territorial claims, open and closed (closed ones are history).</summary>
        public List<TerritorialClaimData> Claims { get; set; } = new List<TerritorialClaimData>();

        /// <summary>Phase 4: persistent places in settlements (homes, workplaces, gathering places).</summary>
        public List<LocationData> Locations { get; set; } = new List<LocationData>();

        /// <summary>Phase 4: relationships between named people, one record per related pair (NpcA &lt; NpcB).</summary>
        public List<NpcRelationData> NpcRelations { get; set; } = new List<NpcRelationData>();

        /// <summary>Phase 5: the minute of the current day (0–1439). The daily simulation runs once per midnight.</summary>
        public int MinuteOfDay { get; set; } = Calendar.WorldTime.DefaultStartMinute;

        /// <summary>Phase 5: shops, each kept by a named person and owning its stock.</summary>
        public List<ShopData> Shops { get; set; } = new List<ShopData>();

        /// <summary>Phase 5: trades with the player, newest last (bounded by <see cref="TransactionData.MaxHistory"/>).</summary>
        public List<TransactionData> Transactions { get; set; } = new List<TransactionData>();

        /// <summary>Phase 8: encounters: at most one active, plus the most recent finished ones (bounded by <see cref="MaxEndedEncounters"/>).</summary>
        public List<Realmkeeper.Domain.Combat.EncounterData> Encounters { get; set; } = new List<Realmkeeper.Domain.Combat.EncounterData>();

        public const int MaxEndedEncounters = 20;

        /// <summary>Phase 8: settlement structures (buildings), each owned by its settlement and linked to its location and shop where it has one.</summary>
        public List<StructureData> Structures { get; set; } = new List<StructureData>();

        /// <summary>Phase 10: regional creature populations (aggregate), creatures drawn into encounters, and settlement herds.</summary>
        public List<CreaturePopulationData> CreaturePopulations { get; set; } = new List<CreaturePopulationData>();

        public List<CreatureInstanceData> Creatures { get; set; } = new List<CreatureInstanceData>();

        public List<LivestockHerdData> Livestock { get; set; } = new List<LivestockHerdData>();

        /// <summary>Phase 10: each settlement's persisted spatial plan (made once, never rearranged).</summary>
        public List<Realmkeeper.Domain.Settlements.SettlementLayoutData> SettlementLayouts { get; set; } = new List<Realmkeeper.Domain.Settlements.SettlementLayoutData>();

        /// <summary>Phase 11: containers (the player's chests, caches, loot), with persistent contents.</summary>
        public List<ContainerData> Containers { get; set; } = new List<ContainerData>();

        /// <summary>Phase 11: the player's work in progress (crafting, gathering, repairing).</summary>
        public List<WorkJobData> WorkJobs { get; set; } = new List<WorkJobData>();

        /// <summary>Phase 11: the places materials are gathered from, in each settlement's land.</summary>
        public List<GatheringSiteData> GatheringSites { get; set; } = new List<GatheringSiteData>();

        /// <summary>Phase 11: the item catalog version this world was saved with (ADR-0050).</summary>
        public int ItemCatalogVersion { get; set; } = ItemCatalog.Version;

        /// <summary>Phase 12: the player's quests (instances bound to real entities; ADR-0056).</summary>
        public List<Realmkeeper.Domain.Quests.QuestInstanceData> Quests { get; set; } = new List<Realmkeeper.Domain.Quests.QuestInstanceData>();

        /// <summary>Phase 12: the conversation in progress, or null.</summary>
        public Realmkeeper.Domain.Quests.ConversationData Conversation { get; set; }

        /// <summary>Phase 13 (ADR-0062): explorable places (wilderness places, and the interiors of buildings once entered).</summary>
        public List<Realmkeeper.Domain.Places.PlaceData> Places { get; set; } = new List<Realmkeeper.Domain.Places.PlaceData>();

        /// <summary>Phase 13 (ADR-0066): people held captive, following the player, or brought home.</summary>
        public List<Realmkeeper.Domain.Places.EscortData> Escorts { get; set; } = new List<Realmkeeper.Domain.Places.EscortData>();

        /// <summary>Phase 14 (ADR-0069): crimes as they happened (who knows of them lives in people's memories, not here).</summary>
        public List<CrimeRecordData> Crimes { get; set; } = new List<CrimeRecordData>();

        /// <summary>The number the next crime record takes ("crime:{n}").</summary>
        public int NextCrimeNumber { get; set; } = 1;

        /// <summary>Phase 15: the player's party (members are the world's own people).</summary>
        public PartyData Party { get; set; } = new PartyData();

        /// <summary>Phase 15: everyone who has ever been the player's companion, with their loyalty and history.</summary>
        public List<CompanionRecordData> Companions { get; set; } = new List<CompanionRecordData>();

        /// <summary>Phase 16 (ADR-0077): the persistent route network (segments between settlements, within and across regions).</summary>
        public List<RouteSegmentData> Routes { get; set; } = new List<RouteSegmentData>();

        /// <summary>Phase 16 (ADR-0078): the player's journey, if one is under way or stopped (null when none).</summary>
        public JourneyData Journey { get; set; }

        /// <summary>Phase 16 (ADR-0079): groups on the roads (caravans, patrols, bandits, refugees, pilgrims).</summary>
        public List<TravelingGroupData> Groups { get; set; } = new List<TravelingGroupData>();

        /// <summary>Phase 16 (ADR-0080): persistent world events and the number the next takes.</summary>
        public List<WorldEventData> WorldEvents { get; set; } = new List<WorldEventData>();
        public int NextWorldEventNumber { get; set; } = 1;

        /// <summary>Phase 17 (ADR-0082): campaigns begun in this world (empty: an open-ended sandbox world).</summary>
        public List<Realmkeeper.Domain.Campaigns.CampaignInstanceData> Campaigns { get; set; } = new List<Realmkeeper.Domain.Campaigns.CampaignInstanceData>();

        public RouteSegmentData FindRoute(string id)
        {
            if (Routes != null && id != null) foreach (var r in Routes) if (r.RouteId == id) return r;
            return null;
        }

        public RouteSegmentData FindRoute(SettlementId a, SettlementId b) => FindRoute(RouteSegmentData.IdFor(a, b));

        public TravelingGroupData FindGroup(string id)
        {
            if (Groups != null && id != null) foreach (var g in Groups) if (g.GroupId == id) return g;
            return null;
        }

        public WorldEventData FindWorldEvent(string id)
        {
            if (WorldEvents != null && id != null) foreach (var e in WorldEvents) if (e.EventId == id) return e;
            return null;
        }

        public CompanionRecordData FindCompanion(NpcId id)
        {
            foreach (var c in Companions) if (c.NpcId == id) return c;
            return null;
        }

        public CrimeRecordData FindCrime(string id)
        {
            if (Crimes != null) foreach (var c in Crimes) if (c != null && c.CrimeId == id) return c;
            return null;
        }

        public Realmkeeper.Domain.Places.PlaceData FindPlace(string placeId)
        {
            if (placeId != null && Places != null) foreach (var p in Places) if (p != null && p.PlaceId == placeId) return p;
            return null;
        }

        public Realmkeeper.Domain.Places.EscortData FindEscort(NpcId npc)
        {
            if (Escorts != null) foreach (var e in Escorts) if (e != null && e.NpcId == npc) return e;
            return null;
        }

        public Realmkeeper.Domain.Quests.QuestInstanceData FindQuest(string instanceId)
        {
            if (instanceId != null && Quests != null) foreach (var q in Quests) if (q != null && q.InstanceId == instanceId) return q;
            return null;
        }

        public ContainerData FindContainer(string key)
        {
            if (key != null && Containers != null) foreach (var c in Containers) if (c != null && c.Key == key) return c;
            return null;
        }

        public Realmkeeper.Domain.Settlements.SettlementLayoutData FindLayout(SettlementId id)
        {
            if (SettlementLayouts != null) foreach (var l in SettlementLayouts) if (l != null && l.SettlementId == id) return l;
            return null;
        }

        /// <summary>The encounter going on, or null.</summary>
        public Realmkeeper.Domain.Combat.EncounterData ActiveEncounter
        {
            get
            {
                foreach (var e in Encounters) if (e.IsActive) return e;
                return null;
            }
        }

        public Calendar.WorldTime CurrentTime => new Calendar.WorldTime(CurrentSimulationDate, MinuteOfDay);

        public static WorldData CreateNew(WorldId worldId, string worldName, long seed, int generatorVersion, WorldSettings settings)
        {
            if (worldId.IsEmpty) throw new ArgumentException("World ID is required.", nameof(worldId));
            if (string.IsNullOrWhiteSpace(worldName)) throw new ArgumentException("World name is required.", nameof(worldName));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.StartingDate.IsValidDate) throw new ArgumentException("Starting date is invalid.", nameof(settings));

            return new WorldData
            {
                WorldId = worldId,
                WorldName = worldName.Trim(),
                Seed = seed,
                GeneratorVersion = generatorVersion,
                SchemaVersion = CurrentSchemaVersion,
                CurrentSimulationDate = settings.StartingDate,
                WorldSettings = settings.Clone(),
            };
        }

        public SettlementData FindSettlement(SettlementId id)
        {
            for (var i = 0; i < Settlements.Count; i++)
            {
                if (Settlements[i].SettlementId == id) return Settlements[i];
            }

            return null;
        }

        public FactionData FindFaction(FactionId id)
        {
            for (var i = 0; i < Factions.Count; i++)
            {
                if (Factions[i].FactionId == id) return Factions[i];
            }

            return null;
        }

        /// <summary>Full independent copy. Historical events are immutable and shared by reference.</summary>
        public WorldData DeepClone()
        {
            var copy = new WorldData
            {
                WorldId = WorldId,
                WorldName = WorldName,
                Seed = Seed,
                GeneratorVersion = GeneratorVersion,
                SchemaVersion = SchemaVersion,
                CurrentSimulationDate = CurrentSimulationDate,
                WorldSettings = WorldSettings?.Clone(),
                Settlements = new List<SettlementData>(Settlements.Count),
                Factions = new List<FactionData>(Factions.Count),
                Npcs = new List<NpcData>(Npcs.Count),
                HistoricalEvents = new List<HistoricalEvent>(HistoricalEvents),
                TradeRoutes = new List<TradeRouteData>(TradeRoutes?.Count ?? 0),
                Shipments = new List<ShipmentData>(Shipments?.Count ?? 0),
                Regions = new List<RegionData>(Regions?.Count ?? 0),
                Player = Player?.Clone(),
            };

            if (Regions != null) foreach (var r in Regions) copy.Regions.Add(r?.Clone());

            foreach (var s in Settlements) copy.Settlements.Add(s?.Clone());
            foreach (var f in Factions) copy.Factions.Add(f?.Clone());
            foreach (var n in Npcs) copy.Npcs.Add(n?.Clone());
            if (TradeRoutes != null) foreach (var r in TradeRoutes) copy.TradeRoutes.Add(r?.Clone());
            if (Shipments != null) foreach (var s in Shipments) copy.Shipments.Add(s?.Clone());
            if (Relations != null) foreach (var r in Relations) copy.Relations.Add(r?.Clone());
            if (Claims != null) foreach (var c in Claims) copy.Claims.Add(c?.Clone());
            if (Locations != null) foreach (var l in Locations) copy.Locations.Add(l?.Clone());
            if (NpcRelations != null) foreach (var r in NpcRelations) copy.NpcRelations.Add(r?.Clone());
            copy.MinuteOfDay = MinuteOfDay;
            if (Shops != null) foreach (var s in Shops) copy.Shops.Add(s?.Clone());
            if (Transactions != null) foreach (var t in Transactions) copy.Transactions.Add(t?.Clone());
            if (Encounters != null) foreach (var e in Encounters) copy.Encounters.Add(e?.Clone());
            if (Structures != null) foreach (var s in Structures) copy.Structures.Add(s?.Clone());
            if (CreaturePopulations != null) foreach (var p in CreaturePopulations) copy.CreaturePopulations.Add(p?.Clone());
            if (Creatures != null) foreach (var c in Creatures) copy.Creatures.Add(c?.Clone());
            if (Livestock != null) foreach (var h in Livestock) copy.Livestock.Add(h?.Clone());
            // Plans are made once and never changed by the simulation (ADR-0048): copies share them.
            if (SettlementLayouts != null) copy.SettlementLayouts.AddRange(SettlementLayouts);
            if (Containers != null) foreach (var c in Containers) copy.Containers.Add(c?.Clone());
            if (WorkJobs != null) foreach (var j in WorkJobs) copy.WorkJobs.Add(j?.Clone());
            if (GatheringSites != null) foreach (var g in GatheringSites) copy.GatheringSites.Add(g?.Clone());
            copy.ItemCatalogVersion = ItemCatalogVersion;
            if (Quests != null) foreach (var q in Quests) copy.Quests.Add(q?.Clone());
            copy.Conversation = Conversation?.Clone();
            if (Places != null) foreach (var p in Places) copy.Places.Add(p?.Clone());
            if (Escorts != null) foreach (var e in Escorts) copy.Escorts.Add(e?.Clone());
            if (Crimes != null) foreach (var c in Crimes) copy.Crimes.Add(c?.Clone());
            copy.NextCrimeNumber = NextCrimeNumber;
            copy.Party = (Party ?? new PartyData()).Clone();
            if (Companions != null) foreach (var c in Companions) copy.Companions.Add(c?.Clone());
            if (Routes != null) foreach (var r in Routes) copy.Routes.Add(r?.Clone());
            copy.Journey = Journey?.Clone();
            if (Groups != null) foreach (var g in Groups) copy.Groups.Add(g?.Clone());
            if (WorldEvents != null) foreach (var e in WorldEvents) copy.WorldEvents.Add(e?.Clone());
            copy.NextWorldEventNumber = NextWorldEventNumber;
            if (Campaigns != null) foreach (var c in Campaigns) copy.Campaigns.Add(c?.Clone());
            return copy;
        }

        /// <summary>The relationship between two factions (order does not matter), or null.</summary>
        public FactionRelationData FindRelation(FactionId a, FactionId b)
        {
            for (var i = 0; i < Relations.Count; i++)
            {
                var r = Relations[i];
                if ((r.FactionA == a && r.FactionB == b) || (r.FactionA == b && r.FactionB == a)) return r;
            }

            return null;
        }

        public TerritorialClaimData FindClaim(ClaimId id)
        {
            for (var i = 0; i < Claims.Count; i++)
            {
                if (Claims[i].ClaimId == id) return Claims[i];
            }

            return null;
        }

        public RegionData FindRegion(RegionId id)
        {
            for (var i = 0; i < Regions.Count; i++)
            {
                if (Regions[i].RegionId == id) return Regions[i];
            }

            return null;
        }

        /// <summary>The region whose bounds contain a map cell, or null when no region covers it.</summary>
        public RegionData FindRegionContaining(MapCoord cell)
        {
            for (var i = 0; i < Regions.Count; i++)
            {
                if (Regions[i].Contains(cell)) return Regions[i];
            }

            return null;
        }

        public NpcData FindNpc(NpcId id)
        {
            for (var i = 0; i < Npcs.Count; i++)
            {
                if (Npcs[i].NpcId == id) return Npcs[i];
            }

            return null;
        }

        public ShopData FindShop(ShopId id)
        {
            for (var i = 0; i < Shops.Count; i++)
            {
                if (Shops[i].ShopId == id) return Shops[i];
            }

            return null;
        }

        public LocationData FindLocation(LocationId id)
        {
            for (var i = 0; i < Locations.Count; i++)
            {
                if (Locations[i].LocationId == id) return Locations[i];
            }

            return null;
        }

        /// <summary>The relationship between two named people (order does not matter), or null.</summary>
        public NpcRelationData FindNpcRelation(NpcId a, NpcId b)
        {
            for (var i = 0; i < NpcRelations.Count; i++)
            {
                var r = NpcRelations[i];
                if (r.NpcA == a && r.NpcB == b || r.NpcA == b && r.NpcB == a) return r;
            }

            return null;
        }

        public TradeRouteData FindTradeRoute(TradeRouteId id)
        {
            for (var i = 0; i < TradeRoutes.Count; i++)
            {
                if (TradeRoutes[i].TradeRouteId == id) return TradeRoutes[i];
            }

            return null;
        }
    }
}
