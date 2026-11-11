using System;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Npcs
{
    /// <summary>What a person is doing (Phase 5). Presentation shows walking while they travel and talking in conversation.</summary>
    public enum ActivityKind
    {
        None = 0,
        Sleeping = 1,
        Eating = 2,
        Working = 3,
        Trading = 4,
        Guarding = 5,
        Sitting = 6,
        Socialising = 7,
        Resting = 8,
        Worshipping = 9,
        Standing = 10,
    }

    /// <summary>Where someone is at a given minute and what they are doing there, with the reason.</summary>
    public readonly struct NpcWhereabouts
    {
        public NpcWhereabouts(LocationId location, ActivityKind activity, string reason, int until)
        {
            Location = location;
            Activity = activity;
            Reason = reason;
            Until = until;
        }

        public LocationId Location { get; }
        public ActivityKind Activity { get; }
        public string Reason { get; }

        /// <summary>Minute of the day this lasts until (exclusive; may exceed 1440 for the night).</summary>
        public int Until { get; }

        public bool IsKnown => !Location.IsEmpty;
    }

    /// <summary>
    /// The hourly layer over Phase 4's daily routine. A person's place and activity at a minute
    /// follow from their day's plan (work day or rest day), their role, personality and pressing
    /// needs. They sleep and eat at home, work at their workplace in working hours (shopkeepers keep
    /// their shop's hours), and spend evenings at the tavern if sociable, or at home. It is a pure
    /// function of world data and time, so it is deterministic, needs no stored state, and is the
    /// same for the simulation, the shops, the dialogue and exploration.
    /// </summary>
    public static class NpcActivityPlan
    {
        public static NpcWhereabouts At(WorldData world, NpcData npc, WorldDate date, int minuteOfDay, DayPartRules parts = null)
        {
            if (npc == null || npc.IsDeceased) return default;
            if (Realmkeeper.Simulation.Places.PlacePresence.IsAway(world, npc)) return default; // Phase 13: held, following, or posted to a place
            parts = parts ?? DefaultParts;
            var m = WorldTime.Normalize(minuteOfDay);
            var home = npc.ResidenceId;
            var seed = NpcHash.Of(world.Seed, "day-rhythm", npc.NpcId.ToString());
            var wake = parts.MorningStarts + NpcHash.Roll(seed, 60);                     // 06:00–07:00
            var sleep = parts.NightStarts - 30 + NpcHash.Roll(NpcHash.Next(seed, 1), 90); // 21:30–23:00
            var (workStart, workEnd) = WorkHours(world, npc);
            if (npc.Role == NpcRole.Innkeeper)
            {
                wake += 60;
                sleep = Math.Max(sleep, 23 * 60);
            }

            // Pressing needs override the routine, day and night.
            NpcGoalData top = null;
            foreach (var g in npc.Goals) if (g.IsActive && (top == null || g.Priority > top.Priority)) top = g;
            if (top != null && top.Priority >= 60 && top.Type == NpcGoalType.Recover && !home.IsEmpty)
                return new NpcWhereabouts(home, m >= sleep || m < wake ? ActivityKind.Sleeping : ActivityKind.Resting, "Unwell: resting at home.", m < wake ? wake : WorldTime.MinutesPerDay + wake);

            if (m >= sleep || m < wake)
                return new NpcWhereabouts(home, ActivityKind.Sleeping, "Night: asleep at home.", m < wake ? wake : WorldTime.MinutesPerDay + wake);

            // Today's main place is the one the day committed (the routine, a pressing need, or the DM's word);
            // other days follow the weekly routine.
            var scheduled = npc.Schedule.For(date);
            var planned = date == world.CurrentSimulationDate && !npc.CurrentLocationId.IsEmpty ? npc.CurrentLocationId : scheduled;
            var workDay = !npc.WorkplaceId.IsEmpty && planned == npc.WorkplaceId;
            // A pressing need takes them elsewhere. Today that is already in the day's committed place (the
            // daily engine chose it, or the DM did); only other days are worked out here.
            var today = date == world.CurrentSimulationDate;
            var urgent = top != null && top.Priority >= 60;
            if (urgent && !today)
            {
                var place = NpcSystem.PlaceFor(world, npc, date);
                if (place != planned && m >= wake + 45 && m < sleep - 120)
                    return new NpcWhereabouts(place, top.Type == NpcGoalType.SecureFood ? ActivityKind.Trading : ActivityKind.Standing,
                        "Urgent: trying to " + GoalWords(top.Type) + ".", sleep - 120);
            }

            if (m < wake + 45) return new NpcWhereabouts(home, ActivityKind.Eating, "Breakfast at home.", wake + 45);

            if (workDay)
            {
                if (m < workStart) return new NpcWhereabouts(home, ActivityKind.Resting, "At home before work.", workStart);
                if (m < workEnd)
                {
                    var lunch = 12 * 60 + NpcHash.Roll(NpcHash.Next(seed, 2), 30);
                    if (m >= lunch && m < lunch + 30 && npc.Role != NpcRole.Guard) return new NpcWhereabouts(npc.WorkplaceId, ActivityKind.Eating, "Midday meal at work.", lunch + 30);
                    return new NpcWhereabouts(npc.WorkplaceId, WorkActivity(world, npc), "Working hours.", workEnd);
                }
            }
            else if (!planned.IsEmpty && planned != home && m < parts.EveningStarts)
            {
                return new NpcWhereabouts(planned,
                    urgent && top.Type == NpcGoalType.SecureFood && planned != scheduled ? ActivityKind.Trading : RestActivity(world, planned),
                    planned == scheduled ? "Rest day." : urgent && planned == NpcSystem.PlaceFor(world, npc, date) ? "Urgent: trying to " + GoalWords(top.Type) + "." : "Here for the day.",
                    parts.EveningStarts);
            }
            else if (planned == home && m < parts.EveningStarts)
            {
                return new NpcWhereabouts(home, ActivityKind.Resting, "At home today.", parts.EveningStarts);
            }

            // Evening: the sociable go to the tavern, others go home.
            var tavern = NpcGenesis.FirstOf(world, npc.HomeSettlementId, LocationKind.Tavern);
            var social = npc.Personality[NpcTrait.Sociability] >= 55 || npc.Role == NpcRole.Innkeeper;
            if (social && tavern != null && m < sleep - 60)
                return new NpcWhereabouts(tavern.LocationId, npc.Role == NpcRole.Innkeeper ? ActivityKind.Working : ActivityKind.Socialising,
                    npc.Role == NpcRole.Innkeeper ? "Serving the evening trade." : "An evening at the tavern.", sleep - 60);
            return new NpcWhereabouts(home, m < parts.NightStarts - 60 ? ActivityKind.Sitting : ActivityKind.Resting, "Evening at home.", sleep);
        }

        public static NpcWhereabouts Now(WorldData world, NpcData npc) => At(world, npc, world.CurrentSimulationDate, world.MinuteOfDay);

        public static readonly DayPartRules DefaultParts = new DayPartRules();

        /// <summary>Working hours by role; a shopkeeper keeps their shop's hours.</summary>
        public static (int start, int end) WorkHours(WorldData world, NpcData npc)
        {
            foreach (var shop in world.Shops)
                if (shop.ShopkeeperNpcId == npc.NpcId && shop.LocationId == npc.WorkplaceId) return (shop.OpensAt, shop.ClosesAt);
            switch (npc.Role)
            {
                case NpcRole.Guard: return (7 * 60, 19 * 60);
                case NpcRole.Innkeeper: return (10 * 60, 22 * 60);
                case NpcRole.Farmer: return (7 * 60, 17 * 60);
                case NpcRole.Elder: return (9 * 60, 16 * 60);
                default: return (8 * 60, 17 * 60);
            }
        }

        private static ActivityKind WorkActivity(WorldData world, NpcData npc)
        {
            var place = world.FindLocation(npc.WorkplaceId);
            switch (place?.Kind)
            {
                case LocationKind.Shop:
                case LocationKind.Market: return ActivityKind.Trading;
                case LocationKind.GuardPost: return ActivityKind.Guarding;
                case LocationKind.Administration: return ActivityKind.Sitting;
                case LocationKind.Worship: return ActivityKind.Worshipping;
                default: return ActivityKind.Working;
            }
        }

        private static ActivityKind RestActivity(WorldData world, LocationId place)
        {
            switch (world.FindLocation(place)?.Kind)
            {
                case LocationKind.Tavern: return ActivityKind.Socialising;
                case LocationKind.Worship: return ActivityKind.Worshipping;
                case LocationKind.Market:
                case LocationKind.Square: return ActivityKind.Standing;
                default: return ActivityKind.Resting;
            }
        }

        private static string GoalWords(NpcGoalType type) =>
            type == NpcGoalType.SecureFood ? "find food" : type == NpcGoalType.SeekSafety ? "find safety" : "see to a need";
    }
}
