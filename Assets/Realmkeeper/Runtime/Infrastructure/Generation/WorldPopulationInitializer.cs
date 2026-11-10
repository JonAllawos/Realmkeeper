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
