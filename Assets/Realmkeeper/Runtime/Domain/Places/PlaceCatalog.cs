using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Places
{
    /// <summary>One kind of inhabitant group an archetype may hold.</summary>
    public sealed class GroupTemplate
    {
        public string Name { get; set; }
        public string[] Creatures { get; set; } = Array.Empty<string>();
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 1;
        public GroupBehaviour Behaviour { get; set; }

        /// <summary>Rooms it prefers (it goes to the deepest such room not yet taken).</summary>
        public RoomKind[] Rooms { get; set; } = Array.Empty<RoomKind>();
    }

    /// <summary>
    /// The rules that shape a kind of place (ADR-0063): how many floors and rooms, their sizes, what rooms there are,
    /// how it is lit and built, who lives there, its traps, hazards, containers and the clues that tell its story.
    /// </summary>
    public sealed class ArchetypeDefinition
    {
        public string Id { get; set; }
        public PlaceCategory Category { get; set; }
        public string Name { get; set; }

        public int MinFloors { get; set; } = 1;
        public int MaxFloors { get; set; } = 1;

        /// <summary>Floors go down (mines, crypts) or up (towers).</summary>
        public bool Descends { get; set; } = true;

        public int MinRooms { get; set; } = 4;
        public int MaxRooms { get; set; } = 7;

        /// <summary>Cells per slot (rooms sit one to a slot; corridors run between neighbouring slots).</summary>
        public int Slot { get; set; } = 12;

        public int MinRoom { get; set; } = 4;
        public int MaxRoom { get; set; } = 9;

        /// <summary>Corridor width in cells (2 lets a Large creature pass).</summary>
        public int CorridorWidth { get; set; } = 2;

        public float Ceiling { get; set; } = 3.5f;

        /// <summary>Chance (0–100) that a room is lit.</summary>
        public int LitChance { get; set; } = 30;

        /// <summary>Chance (0–100) that an extra loop joins two neighbouring rooms.</summary>
        public int LoopChance { get; set; } = 20;

        /// <summary>Chance (0–100) that a passage has a door; of those, how many are locked.</summary>
        public int DoorChance { get; set; } = 30;
        public int LockedChance { get; set; } = 25;

        public int SecretRooms { get; set; }

        public RoomKind EntranceRoom { get; set; } = RoomKind.Entrance;
        public RoomKind DeepRoom { get; set; } = RoomKind.Chamber;
        public RoomKind[] RoomPool { get; set; } = { RoomKind.Chamber };

        /// <summary>The look the builder uses: "rock", "shored", "dressed_stone", "brick", "timber".</summary>
        public string Material { get; set; } = "rock";

        public GroupTemplate[] Inhabitants { get; set; } = Array.Empty<GroupTemplate>();
        public string[] Traps { get; set; } = Array.Empty<string>();
        public int MaxTraps { get; set; }
        public string[] Hazards { get; set; } = Array.Empty<string>();
        public int HazardChance { get; set; }
        public string ContainerFeature { get; set; } = "crate";
        public string LootTable { get; set; } = "old_cache";
        public int MinContainers { get; set; } = 1;
        public int MaxContainers { get; set; } = 2;
        public string[] Clues { get; set; } = Array.Empty<string>();
        public int ClueCount { get; set; } = 2;
    }

    /// <summary>
    /// Phase 13 (ADR-0063): the archetypes. Each is distinct in rooms, layout, materials, light, inhabitants, traps,
    /// hazards, loot and the clues that tell what happened. All descriptive text is Realmkeeper's own.
    /// </summary>
    public static class ArchetypeCatalog
    {
        public const int GeneratorVersion = 1;

        private static readonly Dictionary<string, ArchetypeDefinition> ById = new Dictionary<string, ArchetypeDefinition>(StringComparer.Ordinal);

        public static IEnumerable<ArchetypeDefinition> All => ById.Values;

        public static ArchetypeDefinition Find(string id) => id != null && ById.TryGetValue(id, out var a) ? a : null;

        private static void Add(ArchetypeDefinition a) => ById[a.Id] = a;

        static ArchetypeCatalog()
        {
            Add(new ArchetypeDefinition
            {
                Id = "cave", Category = PlaceCategory.Cave, Name = "Natural cave", MinRooms = 4, MaxRooms = 7, Slot = 13, MinRoom = 5, MaxRoom = 10, CorridorWidth = 2,
                Ceiling = 4.5f, LitChance = 0, LoopChance = 30, DoorChance = 0, SecretRooms = 1, EntranceRoom = RoomKind.Entrance, DeepRoom = RoomKind.Den,
                RoomPool = new[] { RoomKind.Chamber, RoomKind.Chamber, RoomKind.Gallery, RoomKind.Den }, Material = "rock",
                Inhabitants = new[]
                {
                    new GroupTemplate { Name = "a bear's den", Creatures = new[] { "black_bear" }, Min = 1, Max = 1, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Den } },
                    new GroupTemplate { Name = "rats in the dark", Creatures = new[] { "giant_rat" }, Min = 2, Max = 4, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Gallery, RoomKind.Chamber } },
                },
                Traps = Array.Empty<string>(), Hazards = new[] { "darkness", "deep_water" }, HazardChance = 40, ContainerFeature = "bones_pile", LootTable = "old_cache",
                MinContainers = 0, MaxContainers = 1, Clues = new[] { "claw_marks", "old_campfire", "bones" }, ClueCount = 2,
            });
            Add(new ArchetypeDefinition
            {
                Id = "mine", Category = PlaceCategory.Mine, Name = "Abandoned mine", MinFloors = 2, MaxFloors = 2, MinRooms = 4, MaxRooms = 5, Slot = 12, MinRoom = 4, MaxRoom = 8,
                CorridorWidth = 2, Ceiling = 3.0f, LitChance = 20, LoopChance = 15, DoorChance = 15, LockedChance = 50, SecretRooms = 0, EntranceRoom = RoomKind.Entrance,
                DeepRoom = RoomKind.Den, RoomPool = new[] { RoomKind.Gallery, RoomKind.Gallery, RoomKind.Storage, RoomKind.Shaft, RoomKind.Office }, Material = "shored",
                Inhabitants = new[]
                {
                    new GroupTemplate { Name = "a giant spider", Creatures = new[] { "giant_spider" }, Min = 1, Max = 1, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Den } },
                    new GroupTemplate { Name = "rats in the workings", Creatures = new[] { "giant_rat" }, Min = 2, Max = 3, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Gallery, RoomKind.Storage } },
                },
                Traps = new[] { "falling_rocks" }, MaxTraps = 1, Hazards = new[] { "toxic_air", "darkness" }, HazardChance = 35, ContainerFeature = "ore_crate", LootTable = "miners_stores",
                MinContainers = 1, MaxContainers = 2, Clues = new[] { "abandoned_tools", "shoring_split", "tally_marks", "webbing" }, ClueCount = 3,
            });
            Add(new ArchetypeDefinition
            {
                Id = "ruin", Category = PlaceCategory.Ruin, Name = "Ancient ruin", MinRooms = 4, MaxRooms = 7, Slot = 12, MinRoom = 4, MaxRoom = 9, CorridorWidth = 2, Ceiling = 4.0f,
                LitChance = 40, LoopChance = 35, DoorChance = 20, SecretRooms = 1, EntranceRoom = RoomKind.Hall, DeepRoom = RoomKind.Shrine,
                RoomPool = new[] { RoomKind.Chamber, RoomKind.Hall, RoomKind.Collapse, RoomKind.Storage }, Material = "dressed_stone",
                Inhabitants = new[] { new GroupTemplate { Name = "goblin squatters", Creatures = new[] { "goblin" }, Min = 2, Max = 3, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Hall, RoomKind.Chamber } } },
                Traps = new[] { "pressure_darts", "tripwire_alarm" }, MaxTraps = 1, Hazards = new[] { "unstable" }, HazardChance = 25, ContainerFeature = "chest", LootTable = "old_cache",
                Clues = new[] { "carved_symbol", "old_campfire", "broken_statue" }, ClueCount = 2,
            });
            Add(new ArchetypeDefinition
            {
                Id = "keep", Category = PlaceCategory.Keep, Name = "Fortified keep", MinFloors = 2, MaxFloors = 2, Descends = false, MinRooms = 4, MaxRooms = 5, Slot = 11, MinRoom = 4,
                MaxRoom = 8, CorridorWidth = 2, Ceiling = 3.5f, LitChance = 90, LoopChance = 10, DoorChance = 70, LockedChance = 20, EntranceRoom = RoomKind.Gatehouse,
                DeepRoom = RoomKind.Quarters, RoomPool = new[] { RoomKind.Barracks, RoomKind.Armoury, RoomKind.Storage, RoomKind.Kitchen, RoomKind.Yard }, Material = "dressed_stone",
                Inhabitants = new[] { new GroupTemplate { Name = "the watch", Creatures = new[] { "guard" }, Min = 2, Max = 2, Behaviour = GroupBehaviour.Guard, Rooms = new[] { RoomKind.Gatehouse } } },
                Traps = Array.Empty<string>(), ContainerFeature = "chest", LootTable = "garrison_stores", Clues = new[] { "duty_roster", "faction_banner" }, ClueCount = 2,
            });
            Add(new ArchetypeDefinition
            {
                Id = "crypt", Category = PlaceCategory.Crypt, Name = "Crypt", MinFloors = 1, MaxFloors = 2, MinRooms = 4, MaxRooms = 6, Slot = 11, MinRoom = 4, MaxRoom = 8, CorridorWidth = 2,
                Ceiling = 3.0f, LitChance = 10, LoopChance = 15, DoorChance = 50, LockedChance = 30, SecretRooms = 1, EntranceRoom = RoomKind.Entrance, DeepRoom = RoomKind.Tomb,
                RoomPool = new[] { RoomKind.Tomb, RoomKind.Tomb, RoomKind.Chamber, RoomKind.Shrine }, Material = "dressed_stone",
                Inhabitants = new[] { new GroupTemplate { Name = "the restless dead", Creatures = new[] { "skeleton", "zombie" }, Min = 2, Max = 3, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Tomb } } },
                Traps = new[] { "pressure_darts", "poison_needle", "glyph_ward" }, MaxTraps = 2, Hazards = new[] { "darkness" }, HazardChance = 50, ContainerFeature = "sarcophagus",
                LootTable = "tomb_offerings", Clues = new[] { "epitaph", "disturbed_dust", "carved_symbol" }, ClueCount = 2,
            });
            Add(new ArchetypeDefinition
            {
                Id = "catacomb", Category = PlaceCategory.Catacomb, Name = "Catacombs", MinFloors = 2, MaxFloors = 3, MinRooms = 5, MaxRooms = 8, Slot = 10, MinRoom = 3, MaxRoom = 7,
                CorridorWidth = 1, Ceiling = 2.6f, LitChance = 0, LoopChance = 40, DoorChance = 20, SecretRooms = 1, DeepRoom = RoomKind.Tomb,
                RoomPool = new[] { RoomKind.Tomb, RoomKind.Corridor, RoomKind.Chamber }, Material = "brick",
                Inhabitants = new[] { new GroupTemplate { Name = "bone-walkers", Creatures = new[] { "skeleton" }, Min = 2, Max = 4, Behaviour = GroupBehaviour.Ambush, Rooms = new[] { RoomKind.Tomb } } },
                Traps = new[] { "pressure_darts", "spike_pit" }, MaxTraps = 2, Hazards = new[] { "darkness", "toxic_air" }, HazardChance = 40, ContainerFeature = "niche",
                LootTable = "tomb_offerings", Clues = new[] { "epitaph", "disturbed_dust" }, ClueCount = 2,
            });
            Add(new ArchetypeDefinition
            {
                Id = "temple", Category = PlaceCategory.Temple, Name = "Forsaken temple", MinRooms = 4, MaxRooms = 6, Slot = 12, MinRoom = 5, MaxRoom = 9, Ceiling = 5.0f, LitChance = 50,
                DoorChance = 40, LockedChance = 30, SecretRooms = 1, EntranceRoom = RoomKind.Hall, DeepRoom = RoomKind.Shrine,
                RoomPool = new[] { RoomKind.Shrine, RoomKind.Chamber, RoomKind.Quarters, RoomKind.Storage }, Material = "dressed_stone",
                Inhabitants = new[] { new GroupTemplate { Name = "cultists at prayer", Creatures = new[] { "cultist" }, Min = 2, Max = 3, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Shrine } } },
                Traps = new[] { "glyph_ward" }, MaxTraps = 1, ContainerFeature = "chest", LootTable = "tomb_offerings", Clues = new[] { "carved_symbol", "broken_statue" }, ClueCount = 2,
            });
            Add(new ArchetypeDefinition
            {
                Id = "bandit_hideout", Category = PlaceCategory.BanditHideout, Name = "Bandit hideout", MinRooms = 4, MaxRooms = 6, Slot = 11, MinRoom = 4, MaxRoom = 8, Ceiling = 3.2f,
                LitChance = 70, LoopChance = 20, DoorChance = 30, LockedChance = 40, SecretRooms = 1, DeepRoom = RoomKind.Quarters,
                RoomPool = new[] { RoomKind.Den, RoomKind.Storage, RoomKind.Chamber }, Material = "shored",
                Inhabitants = new[]
                {
                    new GroupTemplate { Name = "a band of outlaws", Creatures = new[] { "bandit" }, Min = 2, Max = 3, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Den } },
                    new GroupTemplate { Name = "their captain", Creatures = new[] { "bandit_captain" }, Min = 1, Max = 1, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Quarters } },
                },
                Traps = new[] { "tripwire_alarm" }, MaxTraps = 1, ContainerFeature = "chest", LootTable = "outlaw_leader", Clues = new[] { "old_campfire", "stolen_goods" }, ClueCount = 2,
            });
            Add(new ArchetypeDefinition
            {
                Id = "underground", Category = PlaceCategory.Underground, Name = "Underground works", MinFloors = 2, MaxFloors = 3, MinRooms = 4, MaxRooms = 7, Slot = 12, Ceiling = 3.5f,
                LitChance = 10, LoopChance = 30, DoorChance = 40, LockedChance = 30, SecretRooms = 1, DeepRoom = RoomKind.Vault,
                RoomPool = new[] { RoomKind.Chamber, RoomKind.Storage, RoomKind.Hall }, Material = "brick",
                Inhabitants = new[] { new GroupTemplate { Name = "vermin", Creatures = new[] { "giant_rat" }, Min = 2, Max = 4, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Storage, RoomKind.Chamber } } },
                Traps = new[] { "pressure_darts", "spike_pit" }, MaxTraps = 2, Hazards = new[] { "deep_water", "darkness" }, HazardChance = 30, ContainerFeature = "chest",
                LootTable = "old_cache", Clues = new[] { "carved_symbol", "tally_marks" }, ClueCount = 2,
            });
            Add(new ArchetypeDefinition
            {
                Id = "abandoned_settlement", Category = PlaceCategory.AbandonedSettlement, Name = "Abandoned hamlet", MinRooms = 4, MaxRooms = 6, Slot = 14, MinRoom = 5, MaxRoom = 10,
                CorridorWidth = 3, Ceiling = 6.0f, LitChance = 100, LoopChance = 40, DoorChance = 30, LockedChance = 20, EntranceRoom = RoomKind.Yard, DeepRoom = RoomKind.Quarters,
                RoomPool = new[] { RoomKind.Yard, RoomKind.Quarters, RoomKind.Storage, RoomKind.Workshop }, Material = "timber",
                Inhabitants = new[] { new GroupTemplate { Name = "wolves among the houses", Creatures = new[] { "wolf" }, Min = 2, Max = 3, Behaviour = GroupBehaviour.Lair, Rooms = new[] { RoomKind.Yard } } },
                Hazards = new[] { "unstable" }, HazardChance = 30, ContainerFeature = "crate", LootTable = "old_cache", Clues = new[] { "abandoned_tools", "bones", "old_campfire" }, ClueCount = 3,
            });
        }

        public static List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var a in All)
            {
                if (a.MinRooms < 2 || a.MaxRooms < a.MinRooms) errors.Add($"{a.Id}: room counts.");
                if (a.MinRoom < 3 || a.MaxRoom > a.Slot - 2 || a.MaxRoom < a.MinRoom) errors.Add($"{a.Id}: room sizes do not fit the slot.");
                if (a.CorridorWidth < 1 || a.CorridorWidth > a.MinRoom - 2) errors.Add($"{a.Id}: corridors must be narrower than the smallest room.");
                if (a.Ceiling < 2.4f) errors.Add($"{a.Id}: ceiling below head height.");
                foreach (var g in a.Inhabitants)
                    foreach (var c in g.Creatures)
                        if (CreatureCatalog.Find(c) == null) errors.Add($"{a.Id}: unknown creature '{c}'.");
                foreach (var t in a.Traps) if (TrapCatalog.Find(t) == null) errors.Add($"{a.Id}: unknown trap '{t}'.");
                foreach (var h in a.Hazards) if (HazardCatalog.Find(h) == null) errors.Add($"{a.Id}: unknown hazard '{h}'.");
                foreach (var c in a.Clues) if (FeatureCatalog.Find(c) == null) errors.Add($"{a.Id}: unknown clue '{c}'.");
                if (FeatureCatalog.Find(a.ContainerFeature) == null) errors.Add($"{a.Id}: unknown container '{a.ContainerFeature}'.");
            }

            return errors;
        }
    }

    /// <summary>A check a feature or action asks for: a skill (or an ability's save) against a DC.</summary>
    public sealed class FeatureCheck
    {
        public Skill Skill { get; set; }
        public int Dc { get; set; }

        /// <summary>An item the player must carry (thieves' tools), or null.</summary>
        public string Tool { get; set; }
    }

    /// <summary>
    /// A kind of interactable object (ADR-0064): its name, what looking tells, and the check that learns more. The
    /// application decides the actions a feature offers from its kind and state; this holds the words and numbers.
    /// </summary>
    public sealed class FeatureDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public FeatureKind Kind { get; set; }

        /// <summary>What anyone sees.</summary>
        public string Look { get; set; }

        /// <summary>The check that learns more (a skill and DC), or null.</summary>
        public FeatureCheck Study { get; set; }

        /// <summary>What a successful study reveals (also recorded as a discovery).</summary>
        public string Insight { get; set; }

        /// <summary>What a failed study says (the object stays; another person, or more light, may do better).</summary>
        public string Missed { get; set; }

        /// <summary>The discovery key the insight records ("mine_collapse_cause"), or null.</summary>
        public string Discovery { get; set; }
    }

    public static class FeatureCatalog
    {
        private static readonly Dictionary<string, FeatureDefinition> ById = new Dictionary<string, FeatureDefinition>(StringComparer.Ordinal);

        public static IEnumerable<FeatureDefinition> All => ById.Values;

        public static FeatureDefinition Find(string id) => id != null && ById.TryGetValue(id, out var f) ? f : null;

        private static void Add(string id, string name, FeatureKind kind, string look, Skill? skill = null, int dc = 0, string insight = null, string missed = null, string discovery = null) =>
            ById[id] = new FeatureDefinition
            {
                Id = id, Name = name, Kind = kind, Look = look, Study = skill.HasValue ? new FeatureCheck { Skill = skill.Value, Dc = dc } : null, Insight = insight,
                Missed = missed ?? "Nothing more comes of it — not yet, anyway.", Discovery = discovery,
            };

        static FeatureCatalog()
        {
            // Containers (each holds a Phase 11 container).
            Add("crate", "Crate", FeatureKind.Container, "A nailed crate, its boards gone grey.");
            Add("ore_crate", "Ore crate", FeatureKind.Container, "A miner's crate, stencilled with a tally number. Rust stains the corners.");
            Add("chest", "Chest", FeatureKind.Container, "An iron-bound chest.");
            Add("sarcophagus", "Sarcophagus", FeatureKind.Container, "A stone coffin with a carved lid. The lid sits slightly askew.");
            Add("niche", "Burial niche", FeatureKind.Container, "A shelf cut into the wall, with grave goods behind a crumbling seal.");
            Add("bones_pile", "Pile of bones", FeatureKind.Container, "Bones, old and gnawed. Something glints among them.");

            // Clues: environmental storytelling.
            Add("claw_marks", "Claw marks", FeatureKind.Clue, "Long gouges in the rock at shoulder height.", Skill.Survival, 10,
                "A bear, and a big one. The marks are fresh enough that it still comes this way.");
            Add("old_campfire", "Old campfire", FeatureKind.Clue, "A ring of stones and a little charcoal.", Skill.Survival, 11,
                "Someone camped here not long ago — a few days at most. They left in a hurry: the pot is still on its hook.");
            Add("bones", "Bones", FeatureKind.Clue, "Scattered bones on the floor.", Skill.Medicine, 12,
                "Animal bones, mostly; one is a man's forearm. Gnawed, not cut.");
            Add("abandoned_tools", "Abandoned tools", FeatureKind.Clue, "Picks and shovels dropped where they were used, a lamp still on its hook.", Skill.Investigation, 10,
                "Nobody packed up. The tools were dropped mid-shift, as if everyone left at once.", discovery: "mine_left_in_haste");
            Add("shoring_split", "Split shoring", FeatureKind.Clue, "A timber prop, cracked down its length. Dust trickles from above.", Skill.Investigation, 12,
                "The props were too thin for this rock. The seam shifted — the collapse further in was not bad luck.", discovery: "mine_collapse_cause");
            Add("tally_marks", "Tally board", FeatureKind.Document, "A board of chalked tallies: carts out, carts in.", Skill.Investigation, 9,
                "The tallies stop mid-week. The last line reads \"lower gallery — webs again — Corwen won't go down\".", discovery: "mine_webs");
            Add("webbing", "Webbing", FeatureKind.Clue, "Thick grey webbing strung across the timbers.", Skill.Nature, 11,
                "Giant spider silk — and recent. Whatever spun it nests below.", discovery: "mine_spiders");
            Add("carved_symbol", "Carved symbol", FeatureKind.Clue, "A worn symbol cut into the stone: a circle crossed by three lines.", Skill.History, 13,
                "An old warding sign, the kind once cut above graves to keep their sleepers in.");
            Add("broken_statue", "Broken statue", FeatureKind.Clue, "A statue knocked from its plinth. The face has been chiselled away on purpose.", Skill.Religion, 12,
                "Defaced deliberately: someone wanted this figure forgotten, not just thrown down.");
            Add("epitaph", "Epitaph", FeatureKind.Document, "Lettering cut into a slab, filled with grime.", Skill.History, 11,
                "\"Here the keepers of the ford, laid down with their oaths.\" The rest is worn smooth.");
            Add("disturbed_dust", "Disturbed dust", FeatureKind.Clue, "The dust on the floor is scuffed in a path.", Skill.Investigation, 12,
                "Footprints, one set, going in and not out — and drag marks beside them.");
            Add("duty_roster", "Duty roster", FeatureKind.Document, "A slate of watches and names.", Skill.Investigation, 8,
                "The watch changes at dusk and dawn. The side door is listed as \"barred — do not use\".", discovery: "fort_side_door");
            Add("faction_banner", "Banner", FeatureKind.Clue, "A banner hangs from a beam, colours faded.", Skill.History, 8, "The colours of the faction that holds this place.");
            Add("stolen_goods", "Stolen goods", FeatureKind.Clue, "Bolts of cloth, a cartwheel, sacks with merchants' marks.", Skill.Investigation, 10,
                "Goods taken from the road — the marks belong to traders from nearby towns.");

            // Authored story features.
            Add("foremans_ledger", "Foreman's ledger", FeatureKind.Document, "A ledger left open on the foreman's table.", Skill.Investigation, 8,
                "The last entries: shoring timber refused as too dear; webs in the lower gallery; then, \"Gallery four down. Two not out. We close tomorrow.\"",
                discovery: "mine_why_abandoned");
            Add("collapse_rubble", "Collapsed gallery", FeatureKind.Hazard, "Fallen rock fills half the gallery. The roof above sags.", Skill.Survival, 10,
                "Keep to the left wall, where the props still hold, and do not touch the hanging timbers.");
            Add("captive_chains", "Trapped person", FeatureKind.Captive, "Someone huddled against the rock, pinned by fallen timber and wrapped in webbing.");
            Add("vault_lever", "Iron lever", FeatureKind.Mechanism, "An iron lever set in the wall, its handle worn bright.", Skill.Investigation, 10,
                "A chain runs from it up into the ceiling, towards the sealed door.");
            Add("ward_altar", "Altar", FeatureKind.Altar, "A low altar with a dish for offerings.", Skill.Religion, 12, "An altar to the keepers of the dead. Prayers here are for rest, not for gain.");
            Add("hidden_panel", "Loose panel", FeatureKind.Clue, "One panel of the wall sounds hollow.", Skill.Investigation, 13, "A panel that swings inward.");
            Add("garrison_orders", "Sealed orders", FeatureKind.Document, "Orders under a faction's seal, on the captain's desk.", Skill.Investigation, 9,
                "Patrols on the road are doubled; the fort is to stop and search wagons bound for the towns.", discovery: "fort_orders");
            Add("forge", "Forge", FeatureKind.Workstation, "A smith's forge and anvil.");
            Add("workbench", "Workbench", FeatureKind.Workstation, "A workbench with tools hung above it.");
            Add("bed", "Bed", FeatureKind.Furniture, "A bed with a straw mattress.");
            Add("hearth", "Hearth", FeatureKind.Furniture, "A hearth, its fire banked.");
            Add("desk", "Desk", FeatureKind.Document, "A desk with papers and an inkwell.", Skill.Investigation, 10, "Accounts and letters — ordinary household business.");
            Add("granary_ledger_in", "Store-room ledger", FeatureKind.Document, "The keeper's working ledger, kept by the door.", Skill.Investigation, 8,
                "Measures in and out, day by day — the same figures the public tally shows.");
        }

        public static List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var f in All)
            {
                if (string.IsNullOrEmpty(f.Name) || string.IsNullOrEmpty(f.Look)) errors.Add($"{f.Id}: no name or description.");
                if (f.Study != null && (f.Study.Dc < 5 || f.Study.Dc > 25)) errors.Add($"{f.Id}: study DC out of range.");
                if (f.Study != null && string.IsNullOrEmpty(f.Insight)) errors.Add($"{f.Id}: a study with nothing to learn.");
            }

            return errors;
        }
    }

    public enum TrapReset
    {
        /// <summary>Springs once and is spent.</summary>
        Once = 0,

        /// <summary>Re-arms itself (a pressure plate's spring): springs again on a later visit until disarmed.</summary>
        Rearms = 1,
    }

    /// <summary>A kind of trap (ADR-0064): how it is found, disarmed and what it does. Damage is fair: it can hurt badly, never kill outright.</summary>
    public sealed class TrapDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>DC to notice it (passive Perception on entering; Investigation when searching).</summary>
        public int DetectDc { get; set; }

        public Skill DisarmSkill { get; set; } = Skill.SleightOfHand;
        public int DisarmDc { get; set; }

        /// <summary>The tool disarming needs ("thieves_tools"), or null.</summary>
        public string DisarmTool { get; set; }

        /// <summary>Failing the disarm by this much or more sets it off.</summary>
        public int SpringsOnFailBy { get; set; } = 5;

        public Ability Save { get; set; } = Ability.Dexterity;
        public int SaveDc { get; set; }

        /// <summary>Damage dice ("2d6") and type; a successful save halves it.</summary>
        public Dice Damage { get; set; }
        public DamageType DamageType { get; set; }

        /// <summary>A condition on a failed save ("Poisoned"), or null.</summary>
        public string Condition { get; set; }

        /// <summary>Wakes the place's inhabitants.</summary>
        public bool Alarm { get; set; }

        public TrapReset Reset { get; set; }

        /// <summary>An item recovered when it is disarmed (a spring, darts), or null.</summary>
        public string Salvage { get; set; }

        /// <summary>What a found trap looks like; what springing it is like.</summary>
        public string Seen { get; set; }
        public string Sprung { get; set; }
    }

    public static class TrapCatalog
    {
        private static readonly Dictionary<string, TrapDefinition> ById = new Dictionary<string, TrapDefinition>(StringComparer.Ordinal);

        public static IEnumerable<TrapDefinition> All => ById.Values;

        public static TrapDefinition Find(string id) => id != null && ById.TryGetValue(id, out var t) ? t : null;

        private static void Add(TrapDefinition t) => ById[t.Id] = t;

        static TrapCatalog()
        {
            Add(new TrapDefinition
            {
                Id = "pressure_darts", Name = "Dart plate", DetectDc = 13, DisarmDc = 13, DisarmTool = "thieves_tools", SaveDc = 13, Damage = new Dice(2, 4), DamageType = DamageType.Piercing,
                Reset = TrapReset.Rearms, Salvage = "arrows", Seen = "A flagstone sits a finger's width proud of the rest; tiny holes pock the wall beside it.",
                Sprung = "A click underfoot — darts hiss from the wall.",
            });
            Add(new TrapDefinition
            {
                Id = "tripwire_alarm", Name = "Tripwire and bells", DetectDc = 11, DisarmDc = 10, DisarmTool = null, Save = Ability.Dexterity, SaveDc = 0, Alarm = true,
                Reset = TrapReset.Once, Seen = "A cord stretched ankle-high across the way, tied off to a string of bells.", Sprung = "The cord snaps taut and bells jangle somewhere ahead.",
            });
            Add(new TrapDefinition
            {
                Id = "falling_rocks", Name = "Loose roof", DetectDc = 12, DisarmSkill = Skill.Athletics, DisarmDc = 12, DisarmTool = null, SaveDc = 12, Damage = new Dice(2, 6),
                DamageType = DamageType.Bludgeoning, Reset = TrapReset.Once, Seen = "A wedge holds up a slab of loose roof; a trip-line runs from the wedge across the floor.",
                Sprung = "The wedge jerks free and rock comes down.",
            });
            Add(new TrapDefinition
            {
                Id = "spike_pit", Name = "Spiked pit", DetectDc = 12, DisarmSkill = Skill.Athletics, DisarmDc = 11, DisarmTool = null, SaveDc = 12, Damage = new Dice(2, 6),
                DamageType = DamageType.Piercing, Reset = TrapReset.Rearms, Seen = "The floor here is a lid of planks under dust — a pit, and stakes below.",
                Sprung = "The floor gives way.",
            });
            Add(new TrapDefinition
            {
                Id = "poison_needle", Name = "Poisoned needle", DetectDc = 14, DisarmDc = 14, DisarmTool = "thieves_tools", Save = Ability.Constitution, SaveDc = 13, Damage = new Dice(1, 4),
                DamageType = DamageType.Poison, Condition = "Poisoned", Reset = TrapReset.Rearms, Seen = "A needle's point, stained dark, in the keyhole's throat.",
                Sprung = "A sting in the fingertip; the hand goes numb.",
            });
            Add(new TrapDefinition
            {
                Id = "glyph_ward", Name = "Warding glyph", DetectDc = 14, DisarmSkill = Skill.Arcana, DisarmDc = 14, DisarmTool = null, Save = Ability.Dexterity, SaveDc = 13, Damage = new Dice(3, 4),
                DamageType = DamageType.Fire, Alarm = true, Reset = TrapReset.Once, Seen = "Faint lines of a glyph traced across the threshold, visible when the light catches them.",
                Sprung = "The glyph flares and fire washes over the threshold.",
            });
        }

        public static List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var t in All)
            {
                if (t.DetectDc < 8 || t.DetectDc > 20) errors.Add($"{t.Id}: detection DC out of range (fair traps can be found).");
                if (t.DisarmDc < 8 || t.DisarmDc > 20) errors.Add($"{t.Id}: disarm DC out of range.");
                if (!t.Damage.IsEmpty && t.Damage.Count * t.Damage.Sides > 16) errors.Add($"{t.Id}: damage could kill a low-level character outright.");
                if (string.IsNullOrEmpty(t.Seen) || string.IsNullOrEmpty(t.Sprung)) errors.Add($"{t.Id}: no description.");
            }

            return errors;
        }
    }

    /// <summary>A room-wide hazard (ADR-0064). Effects go through the rules engine's saves and the sheet's hit points.</summary>
    public sealed class HazardDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Describe { get; set; }

        /// <summary>A save on entering (once per visit), or none (Strength = no save).</summary>
        public Ability? Save { get; set; }
        public int SaveDc { get; set; }
        public Dice Damage { get; set; }
        public DamageType DamageType { get; set; }
        public string Condition { get; set; }

        /// <summary>The skill that reads the danger (on inspection) and what it tells.</summary>
        public Skill ReadSkill { get; set; }
        public int ReadDc { get; set; }
        public string Reading { get; set; }

        /// <summary>Darkness: Perception and Investigation here are at disadvantage unless the room is lit or the player sees in the dark.</summary>
        public bool Dark { get; set; }

        /// <summary>Deep water: crossing needs an Athletics check; failing costs a little health and the attempt.</summary>
        public int SwimDc { get; set; }
    }

    public static class HazardCatalog
    {
        private static readonly Dictionary<string, HazardDefinition> ById = new Dictionary<string, HazardDefinition>(StringComparer.Ordinal);

        public static IEnumerable<HazardDefinition> All => ById.Values;

        public static HazardDefinition Find(string id) => id != null && ById.TryGetValue(id, out var h) ? h : null;

        static HazardCatalog()
        {
            ById["toxic_air"] = new HazardDefinition
            {
                Id = "toxic_air", Name = "Foul air", Describe = "The air is close and sour; lamps burn low and blue.", Save = Ability.Constitution, SaveDc = 11, Damage = new Dice(1, 6),
                DamageType = DamageType.Poison, Condition = "Poisoned", ReadSkill = Skill.Nature, ReadDc = 10, Reading = "Damp — the kind of air that pools in old workings. Hold your breath and move through quickly.",
            };
            ById["darkness"] = new HazardDefinition
            {
                Id = "darkness", Name = "Darkness", Describe = "No light reaches here.", Dark = true, ReadSkill = Skill.Survival, ReadDc = 5,
                Reading = "Hard to see a thing here: details will be missed without light or dark-sight.",
            };
            ById["unstable"] = new HazardDefinition
            {
                Id = "unstable", Name = "Unstable ground", Describe = "Cracks run across floor and ceiling; dust sifts down.", Save = Ability.Dexterity, SaveDc = 10, Damage = new Dice(1, 6),
                DamageType = DamageType.Bludgeoning, ReadSkill = Skill.Investigation, ReadDc = 11, Reading = "The cracks are old; step where the stone is whole and it will hold.",
            };
            ById["deep_water"] = new HazardDefinition
            {
                Id = "deep_water", Name = "Deep water", Describe = "Black water fills part of the floor; there is no telling how deep.", SwimDc = 10, Damage = new Dice(1, 4),
                DamageType = DamageType.Bludgeoning, ReadSkill = Skill.Survival, ReadDc = 10, Reading = "Cold and deep, but still — a strong swimmer will cross.",
            };
            ById["fire"] = new HazardDefinition
            {
                Id = "fire", Name = "Fire", Describe = "Flames lick across the floor.", Save = Ability.Dexterity, SaveDc = 12, Damage = new Dice(1, 10), DamageType = DamageType.Fire,
                ReadSkill = Skill.Survival, ReadDc = 8, Reading = "It burns along the old oil; it will gutter out in time.",
            };
        }
    }

    /// <summary>
    /// A place to be made (ADR-0062): an archetype, a name, and what the world or a story adds. Hand-authored places
    /// use the same model with a fixed layout (<see cref="Authored"/>).
    /// </summary>
    public sealed class PlaceDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ArchetypeId { get; set; }

        /// <summary>A hand-built layout (or null to generate from the archetype).</summary>
        public Func<PlaceLayout> Authored { get; set; }

        /// <summary>Story features added to the generated layout, each in a room of the given kind (deepest first).</summary>
        public List<(string Feature, RoomKind Room, bool Required)> Story { get; set; } = new List<(string, RoomKind, bool)>();
    }

    public static class PlaceCatalog
    {
        public const string Mine = "hollow_mine";
        public const string Crypt = "ford_crypt";
        public const string Fort = "watch_fort";
        public const string Cave = "wild_cave";
        public const string Ruin = "old_ruin";

        private static readonly Dictionary<string, PlaceDefinition> ById = new Dictionary<string, PlaceDefinition>(StringComparer.Ordinal);

        public static IEnumerable<PlaceDefinition> All => ById.Values;

        public static PlaceDefinition Find(string id) => id != null && ById.TryGetValue(id, out var d) ? d : null;

        private static void Add(PlaceDefinition d) => ById[d.Id] = d;

        static PlaceCatalog()
        {
            Add(new PlaceDefinition
            {
                Id = Mine, Name = "The Hollow Seam", ArchetypeId = "mine",
                Description = "A worked-out silver seam in the hills, closed after a gallery fell in. Locals say something moved in after the miners left.",
                Story =
                {
                    ("foremans_ledger", RoomKind.Office, false),
                    ("collapse_rubble", RoomKind.Den, false),
                    ("captive_chains", RoomKind.Den, true),
                },
            });
            Add(new PlaceDefinition
            {
                Id = Crypt, Name = "The Fordkeepers' Crypt", ArchetypeId = "crypt",
                Description = "An old burial vault under a ruined chapel, where the keepers of a long-gone ford were laid to rest.",
                Story = { ("ward_altar", RoomKind.Shrine, false), ("hidden_panel", RoomKind.Tomb, false) },
            });
            Add(new PlaceDefinition
            {
                Id = Fort, Name = "Watch Fort", ArchetypeId = "keep", Authored = AuthoredPlaces.WatchFort,
                Description = "A walled watch fort over the road, held by a faction's garrison. Visitors are stopped at the gate.",
            });
            Add(new PlaceDefinition { Id = Cave, Name = "Bramble Cave", ArchetypeId = "cave", Description = "A cave mouth behind brambles, known to hunters and avoided by them." });
            Add(new PlaceDefinition { Id = Ruin, Name = "The Broken Hall", ArchetypeId = "ruin", Description = "The shell of an old hall, roofless in places, older than any town nearby." });
        }
    }
}
