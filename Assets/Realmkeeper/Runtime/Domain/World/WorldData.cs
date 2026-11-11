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

        public WorldSettings Clone() => new WorldSettings
        {
            SimulationSpeed = SimulationSpeed,
            StartingDate = StartingDate,
            EnabledSimulationSystems = new List<string>(EnabledSimulationSystems ?? new List<string>()),
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
        public const int CurrentSchemaVersion = 10;
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
