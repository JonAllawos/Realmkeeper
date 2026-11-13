using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>
    /// Creates regions, the player character and named residents. Every identity comes from a
    /// stream seeded by (world seed, stream name), so a freshly generated world and an older save
    /// upgraded on load get exactly the same IDs and names. Each step only fills in what is
    /// missing, and never replaces existing data.
    /// </summary>
    public static class WorldPopulationInitializer
    {
        public const string PlayerDefaultTitle = "Wayfarer";

        private static readonly NpcRole[] TownRoles = { NpcRole.Elder, NpcRole.Merchant, NpcRole.Guard, NpcRole.Smith, NpcRole.Innkeeper, NpcRole.Labourer };
        private static readonly NpcRole[] VillageRoles = { NpcRole.Elder, NpcRole.Farmer, NpcRole.Merchant, NpcRole.Innkeeper, NpcRole.Labourer };
        private static readonly NpcRole[] HamletRoles = { NpcRole.Elder, NpcRole.Farmer, NpcRole.Farmer, NpcRole.Labourer };

        public static void EnsureAll(WorldData world, int regionWidth, int regionDepth, IRandomSourceFactory random)
        {
            EnsureRegions(world, regionWidth, regionDepth, random);
            EnsurePlayer(world, random);
            EnsureNpcs(world, random);
            PoliticalInitializer.Ensure(world, random); // needs the residents (leaders) to exist
            Simulation.Npcs.NpcGenesis.EnsurePeople(world); // needs residents and leaders: locations, identity, inner life
        }

        /// <summary>
        /// Shops and the player's starting kit (Phase 5). Runs after the economy is initialised, because a
        /// shop's opening stock is made from the settlement's stores and paid for into its treasury.
        /// </summary>
        public static void EnsureCommerce(WorldData world)
        {
            Simulation.Commerce.CommerceGenesis.EnsureShops(world); // needs locations, keepers and stores
            Simulation.Commerce.CommerceGenesis.EnsurePlayer(world);
            Simulation.Commerce.StructureGenesis.Ensure(world); // Phase 8: needs locations and shops
        }

        /// <summary>
        /// Adds the world's single region if it has none, then assigns every settlement without a
        /// region to the region containing it. A settlement no region contains is an error: it is
        /// never silently attached to the wrong region.
        /// </summary>
        public static void EnsureRegions(WorldData world, int regionWidth, int regionDepth, IRandomSourceFactory random)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Regions.Count == 0)
            {
                var ids = Stream(world, "regions", random);
                var names = new NameGenerator(Stream(world, "region-names", random));
                world.Regions.Add(new RegionData
                {
                    RegionId = new RegionId(DeterministicGuid.Next(ids)),
                    Name = "Vale of " + names.NextPlaceName(),
                    Origin = new MapCoord(0, 0),
                    Width = regionWidth,
                    Depth = regionDepth,
                });
            }

            foreach (var s in world.Settlements)
            {
                if (!s.RegionId.IsEmpty) continue;
                var region = world.FindRegionContaining(s.Position)
                             ?? throw new InvalidOperationException($"Settlement '{s.Name}' at {s.Position} lies outside every region; it cannot be assigned automatically.");
                s.RegionId = region.RegionId;
            }
        }

        /// <summary>Creates the player character on the world map if the world has none.</summary>
        public static void EnsurePlayer(WorldData world, IRandomSourceFactory random)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Player != null) return;
            var ids = Stream(world, "player", random);
            var names = new PersonNameGenerator(Stream(world, "player-name", random));
            world.Player = new PlayerCharacterData
            {
                CharacterId = new CharacterId(DeterministicGuid.Next(ids)),
                Name = names.NextGivenName() + " the " + PlayerDefaultTitle,
                LocationKind = CharacterLocationKind.WorldMap,
                Perspective = CameraPerspective.ThirdPerson,
            };
        }

        /// <summary>
        /// Gives each settlement a few named residents if the world has none: 6 in a town, 5 in a
        /// village, 4 in a hamlet (never more than its population). They are members of the
        /// population, not additions to it.
        /// </summary>
        public static void EnsureNpcs(WorldData world, IRandomSourceFactory random)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Npcs.Count > 0) return;
            var ids = Stream(world, "npcs", random);
            var names = new PersonNameGenerator(Stream(world, "npc-names", random));
            foreach (var s in world.Settlements)
            {
                var roles = RolesFor(s.SettlementType);
                var count = Math.Min(roles.Length, s.Population);
                for (var i = 0; i < count; i++)
                {
                    world.Npcs.Add(new NpcData
                    {
                        NpcId = new NpcId(DeterministicGuid.Next(ids)),
                        Name = names.NextFullName(roles[i]),
                        HomeSettlementId = s.SettlementId,
                        FactionId = s.FactionId,
                        Role = roles[i],
                        MetPlayerOn = default(WorldDate),
                    });
                }
            }
        }

        /// <summary>
        /// Phase 10: the people a settlement's own places call for but nobody yet fills — a captain and a second guard
        /// for a town's gatehouse, a priest for a shrine nobody tends, a warden for a hamlet's watchtower. Deterministic
        /// (their own ID and name streams), added once: when the first of them already exists nothing is added, so a
        /// new world and an upgraded save get exactly the same people. Returns each person added with their post (their
        /// inner life is filled by <c>NpcGenesis.EnsurePeople</c>, then <see cref="SettlePhase10People"/> sets their posts).
        /// </summary>
        public static List<(NpcData npc, string post)> EnsurePhase10People(WorldData world, IRandomSourceFactory random)
        {
            var added = new List<(NpcData, string)>();
            var ids = Stream(world, "npcs-v12", random);
            var names = new PersonNameGenerator(Stream(world, "npc-names-v12", random));
            var first = true;
            foreach (var s in world.Settlements)
            {
                var plan = new List<(NpcRole role, string post)>();
                var named = 0;
                foreach (var n in world.Npcs) if (n.HomeSettlementId == s.SettlementId && !n.IsDeceased) named++;

                bool Tended(LocationKind kind)
                {
                    foreach (var n in world.Npcs)
                        if (n.HomeSettlementId == s.SettlementId && !n.IsDeceased && world.FindLocation(n.WorkplaceId)?.Kind == kind) return true;
                    return false;
                }

                bool Has(LocationKind kind)
                {
                    foreach (var l in world.Locations) if (l.SettlementId == s.SettlementId && l.Kind == kind) return true;
                    return false;
                }

                if (s.SettlementType >= SettlementType.Town)
                {
                    if (Has(LocationKind.GuardPost)) { plan.Add((NpcRole.Guard, "captain")); plan.Add((NpcRole.Guard, "guard")); }
                    if (Has(LocationKind.Worship) && !Tended(LocationKind.Worship)) plan.Add((NpcRole.Resident, "priest"));
                }
                else if (s.SettlementType == SettlementType.Hamlet && Has(LocationKind.GuardPost) && !Tended(LocationKind.GuardPost))
                {
                    plan.Add((NpcRole.Guard, "warden"));
                }

                foreach (var (role, post) in plan)
                {
                    var id = new NpcId(DeterministicGuid.Next(ids));
                    if (first && world.FindNpc(id) != null) return added; // already added once: never twice
                    var name = names.NextFullName(role);
                    for (var tries = 0; tries < 20 && NameTaken(world, name); tries++) name = names.NextFullName(role); // one name, one person
                    first = false;
                    if (named + 1 > s.Population) continue; // named people are a subset of the population, never more
                    var npc = new NpcData { NpcId = id, Name = name, HomeSettlementId = s.SettlementId, FactionId = s.FactionId, Role = role, MetPlayerOn = default(WorldDate) };
                    world.Npcs.Add(npc);
                    named++;
                    added.Add((npc, post));
                }
            }

            return added;
        }

        /// <summary>
        /// Phase 21 (ADR-0106): townspeople who keep the specialist shops. A town's named people are a few of its many; for
        /// each specialist its size calls for and it has an unused shop building for (and no free resident to keep), one more
        /// of its own population is named, as Phase 10 named its priests and guards: deterministic IDs and names from their
        /// own stream, never more named people than the population, and once only. Their homes, days and natures are filled
        /// by <c>NpcGenesis.EnsurePeople</c>; <c>CommerceGenesis.EnsureKinds</c> then gives each a shop.
        /// </summary>
        public static List<NpcData> EnsureTradespeople(WorldData world, IRandomSourceFactory random)
        {
            var added = new List<NpcData>();
            var ids = Stream(world, "npcs-v23-trades", random);
            var names = new PersonNameGenerator(Stream(world, "npc-names-v23-trades", random));
            var first = true;
            foreach (var s in world.Settlements)
            {
                if (s.SettlementType < SettlementType.Village) continue;
                var wanted = Simulation.Commerce.CommerceGenesis.SpecialistsWanted(world, s);
                if (wanted == 0) continue;
                var named = 0;
                var free = 0;
                foreach (var n in world.Npcs)
                {
                    if (n.HomeSettlementId != s.SettlementId || n.IsDeceased) continue;
                    named++;
                    if ((n.Role == NpcRole.Resident || n.Role == NpcRole.Labourer || n.Role == NpcRole.Farmer) && !n.ResidenceId.IsEmpty &&
                        !world.Shops.Exists(x => x.ShopkeeperNpcId == n.NpcId) && world.Party?.Find(n.NpcId) == null)
                        free++;
                }

                for (var i = free; i < wanted; i++)
                {
                    var id = new NpcId(DeterministicGuid.Next(ids));
                    if (first && world.FindNpc(id) != null) return added; // already added once: never twice
                    first = false;
                    var name = names.NextFullName(NpcRole.Resident);
                    for (var tries = 0; tries < 20 && NameTaken(world, name); tries++) name = names.NextFullName(NpcRole.Resident);
                    if (named + 1 > s.Population) break; // named people are a subset of the population, never more
                    var npc = new NpcData { NpcId = id, Name = name, HomeSettlementId = s.SettlementId, FactionId = s.FactionId, Role = NpcRole.Resident, MetPlayerOn = default(WorldDate) };
                    world.Npcs.Add(npc);
                    named++;
                    added.Add(npc);
                }
            }

            return added;
        }

        private static bool NameTaken(WorldData world, string name)
        {
            foreach (var n in world.Npcs) if (string.Equals(n.Name, name, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>After the NPC engine has given the new people their lives: the captain leads (notable); the priest tends the shrine.</summary>
        public static void SettlePhase10People(WorldData world, IEnumerable<(NpcData npc, string post)> added)
        {
            foreach (var (npc, post) in added)
            {
                if (post == "captain") npc.Tier = NpcTier.Notable;
                if (post != "priest") continue;
                foreach (var l in world.Locations)
                {
                    if (l.SettlementId != npc.HomeSettlementId || l.Kind != LocationKind.Worship) continue;
                    npc.WorkplaceId = l.LocationId;
                    Simulation.Npcs.NpcGenesis.PlanSchedule(world, npc, npc.HomeSettlementId);
                    npc.CurrentLocationId = npc.Schedule.For(world.CurrentSimulationDate);
                    break;
                }
            }
        }

        private static NpcRole[] RolesFor(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.City:
                case SettlementType.Town:
                case SettlementType.Fortress:
                    return TownRoles;
                case SettlementType.Village:
                    return VillageRoles;
                default:
                    return HamletRoles;
            }
        }

        private static IRandomSource Stream(WorldData world, string name, IRandomSourceFactory random) =>
            random.Create(SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), name));
    }

    /// <summary>Deterministic personal names from original given names and trade bynames.</summary>
    internal sealed class PersonNameGenerator
    {
        private static readonly string[] Given =
        {
            "Aldric", "Bertil", "Cwen", "Dunstan", "Edda", "Fenna", "Godric", "Hild", "Isolde", "Jory",
            "Kenna", "Leofric", "Maud", "Neville", "Osric", "Petra", "Quenby", "Rowena", "Sewell", "Tilda",
            "Ulric", "Wenna", "Agnes", "Brand", "Colm", "Elric", "Merry", "Ottilie", "Randal", "Sabine",
        };

        private static readonly string[] Bynames =
        {
            "Ashby", "Brooke", "Coldwell", "Dunmore", "Fairweather", "Greaves", "Hartley", "Inkpen", "Larkin", "Merrow",
            "Oakes", "Penrose", "Redmayne", "Stroud", "Thorne", "Underhill", "Wade", "Yarrow",
        };

        private readonly IRandomSource _random;
        private readonly HashSet<string> _used = new HashSet<string>();

        public PersonNameGenerator(IRandomSource random) { _random = random; }

        public string NextGivenName() => _random.Pick(Given);

        public string NextFullName(NpcRole role)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var name = _random.Pick(Given) + " " + _random.Pick(Bynames);
                if (_used.Add(name)) return name;
            }

            var fallback = role + " " + (_used.Count + 1);
            _used.Add(fallback);
            return fallback;
        }
    }
}
