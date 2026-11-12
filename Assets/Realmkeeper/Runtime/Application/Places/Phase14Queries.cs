using System.Collections.Generic;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Quests;

using Realmkeeper.Application.Places;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Application
{
    /// <summary>A lasting condition as the HUD shows it: what it is, what it does, how it ends.</summary>
    public sealed class ConditionView
    {
        public ConditionType Type { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public string Reason { get; set; }
        public string Effect { get; set; }
        public string HowToEnd { get; set; }

        /// <summary>The player can try to break free now (a net).</summary>
        public bool CanBreakFree { get; set; }

        /// <summary>The player has Lesser Restoration ready to end it.</summary>
        public bool CanRestore { get; set; }
    }

    /// <summary>A crime the player knows of, as the journal shows it.</summary>
    public sealed class CrimeView
    {
        public string Id { get; set; }
        public string What { get; set; }
        public string Where { get; set; }
        public CrimeStatus Status { get; set; }

        /// <summary>Someone recognised the player: it is confirmed as theirs.</summary>
        public bool Known { get; set; }

        public int Bounty { get; set; }

        /// <summary>Who saw it, as far as the player can tell ("Edda Thorne saw you in the same room").</summary>
        public List<string> Seen { get; } = new List<string>();

        public string Consequence { get; set; }
    }

    /// <summary>Phase 14: queries for the HUD and journal (ADR-0068, ADR-0069, ADR-0070).</summary>
    public sealed partial class WorldSession
    {
        public List<ConditionView> GetConditions()
        {
            var list = new List<ConditionView>();
            var sheet = _world?.Player?.Rpg;
            if (sheet == null) return list;
            var now = QuestClock.Now(_world);
            var restore = sheet.State == CreationState.Complete && new Realmkeeper.Domain.Magic.SpellAccess(new CharacterRules(sheet)).IsReady("lesser_restoration");
            foreach (var k in LastingConditions.Active(sheet, now))
                list.Add(new ConditionView
                {
                    Type = k.Type, Name = LastingConditions.Name(k.Type) + (k.Type == ConditionType.Exhaustion ? " " + k.Level : string.Empty), Level = k.Level, Reason = k.Reason,
                    Effect = LastingConditions.Effect(k.Type, k.Level), HowToEnd = LastingConditions.HowToEnd(k, now),
                    CanBreakFree = (k.Remedies & ConditionRemedy.Escape) != 0, CanRestore = restore && (k.Remedies & ConditionRemedy.Restoration) != 0,
                });
            return list;
        }

        /// <summary>
        /// The crimes the player knows they committed: those someone saw are confirmed, with the bounty and what follows;
        /// those nobody saw are listed as the player's own secret, and as "found out" once the victim notices.
        /// </summary>
        public List<CrimeView> GetCrimes()
        {
            var list = new List<CrimeView>();
            if (_world == null) return list;
            foreach (var c in _world.Crimes)
            {
                var v = new CrimeView
                {
                    Id = c.CrimeId, What = char.ToUpperInvariant(c.Detail[0]) + c.Detail.Substring(1), Status = c.Status, Known = c.KnownToBeThePlayer, Bounty = c.Bounty,
                    Where = _world.FindPlace(c.PlaceId)?.Name ?? _world.FindSettlement(c.SettlementId)?.Name ?? string.Empty,
                };
                foreach (var x in c.Witnesses)
                    v.Seen.Add((_world.FindNpc(x.NpcId)?.Name ?? "someone") + " saw it (" + x.How + ")" + (x.Recognized ? " and knows it was you" : " but could not say who it was"));
                v.Consequence = c.Status == CrimeStatus.Settled ? "Settled." :
                    v.Known ? $"Bounty {c.Bounty} coin. Merchants who know will not trade; the victim or a guard who knows can be paid to settle it." :
                    c.Status == CrimeStatus.Discovered ? "Found out, but nobody knows it was you." :
                    c.Status == CrimeStatus.Witnessed ? "Seen, but not recognised: only a description goes round." : "Nobody knows yet.";
                list.Add(v);
            }

            return list;
        }

        /// <summary>Followers in the place and whether they are with the player, or waiting and why.</summary>
        public List<(string Name, string Room, string State)> GetFollowers()
        {
            var list = new List<(string, string, string)>();
            if (_world == null) return list;
            var place = PlaceRules.Current(_world);
            foreach (var e in _world.Escorts)
            {
                var npc = _world.FindNpc(e.NpcId);
                if (npc == null || npc.IsDeceased) continue;
                if (e.State == EscortState.Following && (place == null || e.PlaceId == null || e.Room == _world.Player.PlaceRoom)) list.Add((npc.Name, null, "with you"));
                else if ((e.State == EscortState.Waiting || e.State == EscortState.Following) && e.PlaceId != null && _world.FindPlace(e.PlaceId) is PlaceData p && !IsHeld(p, e))
                    list.Add((npc.Name, p.Layout.Room(e.Room)?.Name + " of " + p.Name,
                        place == p && PlaceRoutes.Find(p, e.Room, _world.Player.PlaceRoom, Mover.Follower) is List<RouteStep> r ? "waiting — can reach you" : "waiting — " + (place == p ? PlaceRoutes.Blocker(p, e.Room, _world.Player.PlaceRoom) : "left behind")));
            }

            return list;
        }

        private static bool IsHeld(PlaceData p, EscortData e)
        {
            foreach (var f in p.Layout.Features)
                if (f.Kind == FeatureKind.Captive && f.Get("npc") == e.NpcId.ToString() && !p.Has(f.Id, FeatureFlags.Freed)) return true;
            return false;
        }
    }
}
