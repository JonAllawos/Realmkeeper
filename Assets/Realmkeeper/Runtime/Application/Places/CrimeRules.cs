using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Places
{
    /// <summary>A place's floor as a sight grid (ADR-0067): anything that is not room or passage is wall, closed doors too.</summary>
    public sealed class PlaceSightGrid : ISightGrid
    {
        private readonly HashSet<(int, int)> _floor;
        private readonly HashSet<(int, int)> _doors = new HashSet<(int, int)>();

        public PlaceSightGrid(PlaceData place, int floor)
        {
            _floor = PlaceEncounters.Floor(place, floor);
            foreach (var c in place.Layout.Connections)
            {
                if (c.IsVertical || c.Kind == ConnectionKind.Entrance || c.Kind == ConnectionKind.Opening || c.Kind == ConnectionKind.Bridge) continue;
                if (place.Layout.Room(c.A)?.Floor != floor) continue;
                var s = place.DoorOf(c);
                if (s == DoorState.Open || s == DoorState.Broken) continue;
                for (var x = c.Rect.X; x < c.Rect.MaxX; x++)
                    for (var z = c.Rect.Z; z < c.Rect.MaxZ; z++)
                        if (place.Layout.RoomAt(floor, x, z) == null) _doors.Add((x, z));
            }
        }

        public bool IsWall(int x, int y) => !_floor.Contains((x, y)) || _doors.Contains((x, y));
        public string WallName(int x, int y) => _doors.Contains((x, y)) ? "a closed door" : "a wall";
        public bool IsLowCover(int x, int y) => false;
        public string LowCoverName(int x, int y) => string.Empty;
        public string CreatureAt(int x, int y) => null;
    }

    /// <summary>Someone who perceived an act, and how.</summary>
    public sealed class Witness
    {
        public NpcData Npc { get; set; }
        public string How { get; set; }
        public bool Recognized { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Phase 14 (ADR-0069): crime and witnesses. A witness must be there by their own schedule (or posted there), awake
    /// (sleepers wake only for loud acts), within 60 feet with a clear line (the shared sight rule over the place's own
    /// walls and closed doors), with light enough to see, and — for a quiet act — not out-matched by the player's
    /// Sleight of Hand or Stealth. They recognise the player if they have met, or the player is notorious. Every
    /// consequence is keyed by the crime, so nothing is applied twice, and only those who perceived it know of it; the
    /// daily hearsay carries it on from there.
    /// </summary>
    public static class CrimeRules
    {
        public const int SightCells = 12; // 60 ft

        /// <summary>The people who would perceive an act at a cell of a room now.</summary>
        public static List<Witness> Witnesses(WorldData w, PlaceData place, PlaceRoom room, int x, int z, bool loud, int quietTotal = -1, string quietSkill = null)
        {
            var list = new List<Witness>();
            var grid = new PlaceSightGrid(place, room.Floor);
            var at = new GridPos(x, z, room.Floor);
            var light = room.Lit || PlaceRules.CarriesLight(w.Player);
            var now = QuestClock.Now(w);

            void Consider(NpcData npc, string theirRoom, int nx, int nz, bool asleep)
            {
                if (npc == null || npc.IsDeceased || list.Exists(o => o.Npc.NpcId == npc.NpcId)) return;
                if (w.FindEscort(npc.NpcId) is EscortData e && (e.State == EscortState.Following || e.State == EscortState.Waiting || e.State == EscortState.Captive)) return; // with the player, or held
                var r = place.Layout.Room(theirRoom);
                if (r == null || r.Floor != room.Floor) return;
                if (asleep && !loud) return;
                if (Math.Max(Math.Abs(nx - x), Math.Abs(nz - z)) > SightCells) return;
                if (theirRoom != room.Id && !PlaceRules.InSight(place, theirRoom, room.Id)) return;
                if (!GridSight.CanSee(grid, new GridPos(nx, nz, room.Floor), at)) return;
                if (!light && !loud) return; // too dark to make out a quiet hand
                if (quietTotal >= 0 && !loud && quietTotal >= Passive(npc)) return; // the player was too deft
                var known = npc.HasMetPlayer || (w.Player.Social?.Notoriety ?? 0) >= 30;
                list.Add(new Witness
                {
                    Npc = npc, Recognized = known, Description = known ? w.Player.Name : Describe(w),
                    How = asleep ? "woken by the noise" : theirRoom == room.Id ? "in the same room" : "through the open way",
                });
            }

            if (place.Category == PlaceCategory.Interior)
                foreach (var p in IndoorSchedule.Everyone(w, place, now))
                    Consider(p.Npc, p.Room, p.X, p.Z, p.Activity == ActivityKind.Sleeping);

            foreach (var g in place.Layout.Groups)
            {
                var s = place.GroupStateOf(g.Id);
                if (s == GroupState.Defeated || s == GroupState.Departed || s == GroupState.Fighting) continue;
                foreach (var m in g.Members)
                    if (!m.NpcId.IsEmpty) Consider(w.FindNpc(m.NpcId), g.Room, m.X, m.Z, false);
            }

            return list;
        }

        /// <summary>A witness's passive Perception: 10, their Wisdom (from their sheet when they have one), and a guard's watchfulness.</summary>
        public static int Passive(NpcData npc)
        {
            if (npc.Character?.Sheet is CharacterSheetData sheet && sheet.State == CreationState.Complete) return 10 + new CharacterRules(sheet).SkillBonus(Skill.Perception).Total;
            return 10 + (npc.Role == NpcRole.Guard ? 3 : 0);
        }

        /// <summary>What a stranger can tell of the player: their kind, their build of armour.</summary>
        public static string Describe(WorldData w)
        {
            var sheet = w.Player.Rpg;
            var species = RpgCatalog.FindSpecies(sheet?.SpeciesId)?.Name?.ToLowerInvariant() ?? "someone";
            var armour = sheet != null && PlaceRules.Rules(w).Loadout.Armor ? "in armour" : "in travelling clothes";
            return $"a stranger — {Article(species)} {species}, {armour}";
        }

        private static string Article(string word) => word.Length > 0 && "aeiou".IndexOf(word[0]) >= 0 ? "an" : "a";

        /// <summary>
        /// Records a crime and applies what follows from it, once: the witnesses' memories and knowledge, and — only if
        /// someone recognised the player — a bounty and the reputation it costs. Returns the record and what the player sees.
        /// </summary>
        public static CrimeRecordData Record(CommandContext context, WorldData w, CrimeKind kind, PlaceData place, PlaceRoom room, NpcId victim, FactionId faction,
            string deed, List<Witness> witnesses, Dictionary<string, int> taken = null, int coin = 0, int value = 0)
        {
            var now = QuestClock.Now(w);
            var c = new CrimeRecordData
            {
                CrimeId = "crime:" + w.NextCrimeNumber.ToString(CultureInfo.InvariantCulture), Kind = kind, At = now, SettlementId = place?.SettlementId ?? w.Player.SettlementId,
                PlaceId = place?.PlaceId, Room = room?.Id, VictimNpcId = victim, VictimFactionId = faction, Detail = deed, Coin = coin,
                Status = witnesses.Count > 0 ? CrimeStatus.Witnessed : CrimeStatus.Undiscovered, DiscoveredAt = witnesses.Count > 0 ? now : -1,
            };
            if (taken != null) foreach (var pair in taken) c.Taken[pair.Key] = pair.Value;
            w.NextCrimeNumber++;
            w.Crimes.Add(c);
            var report = QuestRuntime.Reporter(context);
            var settlement = c.SettlementId;
            foreach (var x in witnesses)
            {
                c.Witnesses.Add(new CrimeWitnessData { NpcId = x.Npc.NpcId, Recognized = x.Recognized, Description = x.Description, How = x.How });
                var npc = w.FindNpc(x.Npc.NpcId);
                NpcMind.Remember(npc, new NpcMemoryData
                {
                    MemoryId = new MemoryId(context.NextGuid("npc-memory")), Date = w.CurrentSimulationDate, Category = MemoryCategory.Personal,
                    Subject = (x.Recognized ? w.Player.Name : x.Description) + " " + deed, SettlementId = settlement, Salience = kind == CrimeKind.Murder ? 95 : 70,
                    Source = MemorySource.Witnessed,
                });
                if (x.Recognized)
                    NpcMind.Learn(npc, KnowledgeTopic.PlayerDeed, c.CrimeId, Words(kind) + ":" + settlement, KnowledgeStatus.Known, 100, w.CurrentSimulationDate, default, 0, default,
                        () => new KnowledgeId(context.NextGuid("npc-knowledge")));
                else
                    NpcMind.Learn(npc, KnowledgeTopic.RecentEvent, c.CrimeId, Words(kind) + " by " + x.Description, KnowledgeStatus.Known, 100, w.CurrentSimulationDate, default, 0, default,
                        () => new KnowledgeId(context.NextGuid("npc-knowledge")));
                context.ReportEvent(HistoricalEventType.CrimeWitnessed, EventSeverity.Warning, EventImportance.Minor, settlement.IsEmpty ? Array.Empty<SettlementId>() : new[] { settlement },
                    new Dictionary<string, string> { ["witness"] = npc.Name, ["culprit"] = x.Recognized ? w.Player.Name : x.Description, ["deed"] = deed, ["place"] = place?.Name ?? string.Empty },
                    npcIds: new[] { npc.NpcId });
            }

            if (c.KnownToBeThePlayer)
            {
                c.Bounty = Bounty(kind, value);
                var key = c.CrimeId;
                if (!settlement.IsEmpty) Reputation.Change(w, "settlement:" + settlement, "Reputation", -Penalty(kind), key + ":settlement", deed, report);
                if (!victim.IsEmpty) Reputation.Change(w, "npc:" + victim, "Trust", -Penalty(kind) * 2, key + ":victim", deed, report);
                if (!faction.IsEmpty) Reputation.Change(w, "faction:" + faction, "Standing", -Penalty(kind), key + ":faction", deed, report);
                Reputation.Change(w, "notoriety", "Notoriety", kind == CrimeKind.Murder ? 10 : 3, key + ":notoriety", deed, report);
            }

            return c;
        }

        public static string Words(CrimeKind k)
        {
            switch (k)
            {
                case CrimeKind.Theft: return "theft";
                case CrimeKind.ShopTheft: return "theft from a shop";
                case CrimeKind.UnauthorizedEntry: return "breaking in";
                case CrimeKind.Trespass: return "trespass";
                case CrimeKind.Assault: return "assault";
                case CrimeKind.Murder: return "murder";
                default: return "damage to property";
            }
        }

        public static int Bounty(CrimeKind k, int value)
        {
            switch (k)
            {
                case CrimeKind.Theft:
                case CrimeKind.ShopTheft: return Math.Max(10, value);
                case CrimeKind.Assault: return 25;
                case CrimeKind.Murder: return 100;
                case CrimeKind.Trespass: return 5;
                default: return 10;
            }
        }

        public static int Penalty(CrimeKind k) => k == CrimeKind.Murder ? 30 : k == CrimeKind.Assault ? 15 : k == CrimeKind.Trespass ? 4 : 8;

        /// <summary>The unsettled crimes this person knows the player committed (first-hand, or by hearsay they are fairly sure of).</summary>
        public static List<CrimeRecordData> KnownTo(WorldData w, NpcData npc, int minConfidence = 50)
        {
            var list = new List<CrimeRecordData>();
            if (npc == null) return list;
            foreach (var c in w.Crimes)
            {
                if (c.Status == CrimeStatus.Settled || !c.KnownToBeThePlayer) continue;
                var k = NpcMind.Find(npc, KnowledgeTopic.PlayerDeed, c.CrimeId);
                if (k != null && k.Confidence >= minConfidence) list.Add(c);
            }

            return list;
        }

        /// <summary>The coin owed to settle a set of crimes.</summary>
        public static int Owed(List<CrimeRecordData> crimes)
        {
            var n = 0;
            foreach (var c in crimes) n += c.Bounty;
            return n;
        }

        /// <summary>A merchant who knows of an unsettled crime by the player in their settlement will not trade (null when they will).</summary>
        public static string RefusesTrade(WorldData w, NpcData merchant)
        {
            foreach (var c in KnownTo(w, merchant))
                if (c.SettlementId == merchant.HomeSettlementId || c.VictimNpcId == merchant.NpcId)
                    return $"{merchant.Name} will not deal with you: \"I know what you did — the {Words(c.Kind)}. Set it right first.\"";
            return null;
        }

        /// <summary>
        /// Settles every crime this person knows of: the goods are given back where the player still has them, the rest is
        /// paid in coin; the bounty is cleared. Memories stay (people do not forget), but the refusals end.
        /// </summary>
        public static string Settle(CommandContext context, WorldData w, NpcData npc)
        {
            var crimes = KnownTo(w, npc);
            if (crimes.Count == 0) return "There is nothing to settle.";
            var owed = 0;
            var returned = new List<string>();
            foreach (var c in crimes)
            {
                var due = c.Bounty;
                foreach (var pair in c.Taken)
                {
                    var have = 0;
                    foreach (var s in w.Player.Inventory) if (s.ItemId == pair.Key) have += s.Quantity;
                    var give = Math.Min(have, pair.Value);
                    if (give <= 0) continue;
                    var left = give;
                    foreach (var s in new List<ItemStackData>(w.Player.Inventory))
                    {
                        if (left <= 0) break;
                        if (s.ItemId != pair.Key) continue;
                        var part = Math.Min(left, s.Quantity);
                        ItemCommandHandlers.TakeFrom(w.Player.Inventory, s, part, context);
                        left -= part;
                    }

                    returned.Add(ItemCatalog.Find(pair.Key)?.Name ?? pair.Key);
                    due = Math.Max(c.Bounty / 2, due - (ItemCatalog.Find(pair.Key)?.BaseValue ?? 0) * give);
                }

                owed += due;
            }

            if (w.Player.Purse < owed) return null; // the caller explains
            w.Player.Purse -= owed;
            foreach (var c in crimes)
            {
                c.Status = CrimeStatus.Settled;
                c.Bounty = 0;
                context.ReportEvent(HistoricalEventType.CrimeSettled, EventSeverity.Info, EventImportance.Minor, c.SettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { c.SettlementId },
                    new Dictionary<string, string> { ["by"] = w.Player.Name, ["deed"] = "the " + Words(c.Kind), ["how"] = (returned.Count > 0 ? "returned " + string.Join(", ", returned) + "; " : string.Empty) + owed + " coin" });
            }

            return (returned.Count > 0 ? "You hand back " + string.Join(", ", returned) + ". " : string.Empty) + $"You pay {owed} coin. It is settled.";
        }
    }
}
