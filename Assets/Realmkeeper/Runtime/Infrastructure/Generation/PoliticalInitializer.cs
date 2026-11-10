using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>
    /// Builds the initial political structure on top of the governing factions world generation
    /// already creates (their IDs and names are kept). The same procedure runs for a fresh world
    /// and for an older save being upgraded, with IDs and names from streams seeded by
    /// (world seed, stream name), so both get the same factions. It runs once: a world that
    /// already has a faction with a founding date is left untouched, so loading or re-entering a
    /// settlement can never create factions twice.
    ///
    /// Result for the default three-settlement world: the kingdom governing the two largest
    /// settlements; the faction governing the third, typed as a noble house; a noble house
    /// administering the second settlement for the kingdom; a traders' guild in the two largest;
    /// and a religious order with a following everywhere (its leadership vacant).
    /// </summary>
    public static class PoliticalInitializer
    {
        private static readonly string[] HouseNames = { "Aldane", "Corwin", "Harrow", "Vance", "Wyck", "Marlowe", "Tresk", "Osmere" };
        private static readonly string[] OrderNames = { "the Dawn", "the Hearth", "Still Waters", "the Green Bough", "the Lantern", "the Open Hand" };

        public static void Ensure(WorldData world, IRandomSourceFactory random, PoliticalRules rules = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Factions.Count == 0 || world.Settlements.Count == 0) return; // nothing to build on: invent nothing
            foreach (var f in world.Factions)
            {
                if (f.FoundedOn.IsValidDate) return; // already political
            }

            rules = rules ?? new PoliticalRules();
            var start = world.WorldSettings.StartingDate;
            var ids = Stream(world, "politics", random);
            var names = Stream(world, "politics-names", random);

            // Settlements by size: capital first.
            var bySize = new List<SettlementData>(world.Settlements);
            bySize.Sort((a, b) => b.Population != a.Population ? b.Population.CompareTo(a.Population) : a.SettlementId.CompareTo(b.SettlementId));
            var capital = bySize[0];
            var second = bySize.Count > 1 ? bySize[1] : null;
            var third = bySize.Count > 2 ? bySize[2] : null;

            // 1. The existing governing factions become first-class factions; nothing about them is replaced.
            FactionData kingdom = null;
            long largest = -1;
            foreach (var f in world.Factions)
            {
                long governed = 0;
                foreach (var s in world.Settlements)
                {
                    if (s.FactionId == f.FactionId) governed += s.Population;
                }

                if (governed > largest)
                {
                    largest = governed;
                    kingdom = f;
                }
            }

            foreach (var f in world.Factions)
            {
                var governs = PoliticalMath.GovernsAnything(world, f);
                f.Type = f == kingdom && governs ? FactionType.Kingdom : governs ? FactionType.NobleHouse : FactionType.Guild;
                f.Description = f.Type == FactionType.Kingdom ? "The crown: the realm's governing authority."
                    : f.Type == FactionType.NobleHouse ? "A local governing house that answers to no crown."
                    : "An association of the realm.";
                Found(f, start);
                foreach (var s in world.Settlements)
                {
                    if (s.FactionId == f.FactionId) AddPresence(f, s, 70, start);
                }
            }

            // 2. New factions.
            FactionData house = null;
            if (second != null && kingdom != null && second.FactionId == kingdom.FactionId)
            {
                house = Create(world, ids, "House " + HouseNames[names.NextInt(0, HouseNames.Length)], FactionType.NobleHouse,
                    "A noble house that administers land on the crown's behalf.", start);
                second.AdministratorFactionId = house.FactionId;
                AddPresence(house, second, 55, start);
                AddPresence(house, capital, 25, start);
            }

            var guild = Create(world, ids, $"{capital.Name} Traders' Guild", FactionType.Guild,
                "Merchants and carriers who work the roads between the settlements.", start);
            AddPresence(guild, capital, 50, start);
            if (second != null) AddPresence(guild, second, 35, start);

            var order = Create(world, ids, "Order of " + OrderNames[names.NextInt(0, OrderNames.Length)], FactionType.ReligiousOrder,
                "A faith with shrines and a following in every settlement.", start);
            foreach (var s in bySize) AddPresence(order, s, 45, start);

            // The crown keeps an eye on the settlement it does not hold.
            if (kingdom != null && third != null && third.FactionId != kingdom.FactionId) AddPresence(kingdom, third, 30, start);

            // 3. Leaders are existing named residents (never new people). The order's leadership starts vacant.
            if (kingdom != null) Lead(world, kingdom, capital, NpcRole.Elder);
            foreach (var f in world.Factions)
            {
                if (f == kingdom || f.Type != FactionType.NobleHouse || f == house) continue;
                foreach (var s in world.Settlements)
                {
                    if (s.FactionId == f.FactionId && f.LeaderNpcId.IsEmpty) Lead(world, f, s, NpcRole.Elder);
                }
            }

            if (house != null) Lead(world, house, second, NpcRole.Elder);
            Lead(world, guild, capital, NpcRole.Merchant);

            // 4. Relationships: one record for every pair, with the starting agreements.
            for (var i = 0; i < world.Factions.Count; i++)
            {
                for (var j = i + 1; j < world.Factions.Count; j++)
                {
                    var (a, b) = PoliticalMath.OrderPair(world.Factions[i].FactionId, world.Factions[j].FactionId);
                    world.Relations.Add(new FactionRelationData { FactionA = a, FactionB = b, Trust = 50 });
                }
            }

            if (kingdom != null) Agree(world, kingdom, guild, AgreementType.TradeAgreement, start);
            if (kingdom != null && house != null) Agree(world, kingdom, house, AgreementType.MutualSupport, start);

            // 5. Starting values sit at their own targets, so the first weeks are not a burst of change.
            foreach (var f in world.Factions) f.Influence = 50;
            SettleMembers(world, rules);
            foreach (var f in world.Factions)
            {
                var steadyDues = 0L;
                foreach (var p in f.Presence) steadyDues += (long)p.Members * rules.Profile(f.Type).DuesPerThousandMembers / 1000;
                f.Treasury = rules.UpkeepPercent > 0 ? (int)(steadyDues * 100 / rules.UpkeepPercent) : 0;
            }

            foreach (var r in world.Relations) SettleRelation(world, r, rules);
            foreach (var f in world.Factions)
            {
                f.Influence = PoliticalMath.InfluenceTarget(PoliticalMath.InfluenceContributions(world, f, rules));
                f.InfluenceTarget = f.Influence;
                f.LastReportedInfluence = f.Influence;
                f.Status = f.Influence < rules.DecliningInfluence ? FactionStatus.Declining : FactionStatus.Active;
            }

            SettleMembers(world, rules);

            // 6. Goals, from what each faction is and holds.
            foreach (var f in world.Factions)
            {
                if (PoliticalMath.GovernsAnything(world, f)) AddGoal(world, f, ids, GoalType.MaintainControl, 5, start, rules);
            }

            if (kingdom != null && third != null && third.FactionId != kingdom.FactionId)
                AddGoal(world, kingdom, ids, GoalType.SecureClaim, 3, start, rules, third.SettlementId);
            AddGoal(world, guild, ids, GoalType.IncreaseMembership, 4, start, rules, value: guild.TotalMembers * 5 / 4 + 10);
            if (third != null) AddGoal(world, guild, ids, GoalType.EstablishPresence, 2, start, rules, third.SettlementId, value: 30);
            AddGoal(world, order, ids, GoalType.IncreaseMembership, 4, start, rules, value: order.TotalMembers * 6 / 5 + 10);
            if (house != null)
            {
                AddGoal(world, house, ids, GoalType.GainInfluence, 4, start, rules, value: Math.Min(100, house.Influence + 10));
                AddGoal(world, house, ids, GoalType.ImproveRelationship, 3, start, rules, faction: kingdom.FactionId, value: 60);
            }
        }

        private static void Found(FactionData f, Domain.Calendar.WorldDate start)
        {
            f.FoundedOn = start;
            f.Status = FactionStatus.Active;
        }

        private static FactionData Create(WorldData world, IRandomSource ids, string name, FactionType type, string description, Domain.Calendar.WorldDate start)
        {
            var f = new FactionData { FactionId = new FactionId(DeterministicGuid.Next(ids)), Name = name, Type = type, Description = description };
            Found(f, start);
            world.Factions.Add(f);
            return f;
        }

        private static void AddPresence(FactionData f, SettlementData s, int strength, Domain.Calendar.WorldDate start)
        {
            if (f.FindPresence(s.SettlementId) != null) return;
            f.Presence.Add(new FactionPresenceData { SettlementId = s.SettlementId, Strength = strength, Status = PresenceStatus.Established, EstablishedOn = start });
        }

        private static void Lead(WorldData world, FactionData f, SettlementData s, NpcRole role)
        {
            if (s == null) return;
            foreach (var n in world.Npcs)
            {
                if (n.HomeSettlementId != s.SettlementId || n.Role != role) continue;
                var taken = false;
                foreach (var other in world.Factions)
                {
                    if (other.LeaderNpcId == n.NpcId) taken = true;
                }

                if (taken) continue;
                f.LeaderNpcId = n.NpcId;
                n.FactionId = f.FactionId;
                return;
            }
        }

        private static void Agree(WorldData world, FactionData a, FactionData b, AgreementType type, Domain.Calendar.WorldDate start)
        {
            var r = world.FindRelation(a.FactionId, b.FactionId);
            if (r == null) return;
            r.Agreement = type;
            r.AgreementSince = start;
            r.Trust = 60;
        }

        private static void SettleMembers(WorldData world, PoliticalRules rules)
        {
            foreach (var f in world.Factions)
            {
                foreach (var p in f.Presence)
                {
                    var s = world.FindSettlement(p.SettlementId);
                    p.Members = PoliticalMath.MembershipTarget(f, p, s, rules);
                    p.Status = p.Strength >= rules.EstablishedStrength ? PresenceStatus.Established : PresenceStatus.Waning;
                }
            }

            PoliticalMath.ClampMembers(world);
            foreach (var f in world.Factions)
            {
                foreach (var p in f.Presence)
                    p.LastReportedSharePerMille = PoliticalMath.SharePerMille(p.Members, world.FindSettlement(p.SettlementId).Population);
            }
        }

        private static void SettleRelation(WorldData world, FactionRelationData r, PoliticalRules rules)
        {
            var evaluation = PoliticalMath.EvaluateRelation(world, world.FindFaction(r.FactionA), world.FindFaction(r.FactionB), r, rules);
            r.Opinion = evaluation.OpinionTarget;
            r.Rivalry = evaluation.RivalryTarget;
            r.LastCause = evaluation.MainCause;
            r.LastReportedState = PoliticalMath.StateOf(r);
        }

        private static void AddGoal(WorldData world, FactionData f, IRandomSource ids, GoalType type, int priority, Domain.Calendar.WorldDate start,
            PoliticalRules rules, SettlementId settlement = default, FactionId faction = default, int value = 0)
        {
            var goal = new FactionGoalData
            {
                GoalId = new GoalId(DeterministicGuid.Next(ids)),
                Type = type,
                TargetSettlementId = settlement,
                TargetFactionId = faction,
                TargetValue = value,
                Priority = priority,
                Status = GoalStatus.Active,
                CreatedOn = start,
            };
            goal.Progress = Math.Min(99, PoliticalMath.GoalProgress(world, f, goal, rules)); // completion is the simulation's call
            if (type == GoalType.MaintainControl) goal.Progress = PoliticalMath.GoalProgress(world, f, goal, rules);
            f.Goals.Add(goal);
        }

        private static IRandomSource Stream(WorldData world, string name, IRandomSourceFactory random) =>
            random.Create(SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), name));
    }
}
