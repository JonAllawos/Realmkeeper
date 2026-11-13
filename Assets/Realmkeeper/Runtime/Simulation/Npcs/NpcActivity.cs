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
            // Phase 18 (ADR-0094): character shows in the day. The disciplined are at work early and leave late; the
            // careless drift in late. (Shopkeepers keep their shop's hours whatever their nature.)
            var keepsShop = world.Shops.Exists(s => s.ShopkeeperNpcId == npc.NpcId && s.LocationId == npc.WorkplaceId);
            if (!keepsShop && npc.Role != NpcRole.Guard)
            {
                var discipline = npc.Personality[NpcTrait.Discipline];
                if (discipline >= 65) { workStart -= 30; workEnd += 30; }
                else if (discipline <= 30) workStart += 30;
            }

            var town = world.FindSettlement(npc.HomeSettlementId);
            // Phase 21: today's weather where they live (other days' weather is not known yet, so it changes nothing there).
            var weather = town != null && date == world.CurrentSimulationDate ? world.Environment?.Find(town.RegionId)?.At(m) : null;
            var severe = weather != null && WeatherWords.IsSevere(weather.Kind);
            var falling = weather != null && (WeatherWords.IsRain(weather.Kind) || WeatherWords.IsSnow(weather.Kind));
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
                    // Phase 21: open-air work stops in a storm or heavy snow (guards keep their posts); they wait it out at home
                    // until the weather's period ends.
                    if (severe && npc.Role != NpcRole.Guard && !home.IsEmpty && IsOpenAir(world, npc.WorkplaceId))
                        return new NpcWhereabouts(home, ActivityKind.Sitting, $"Sheltering at home from the {WeatherWords.Name(weather.Kind).ToLowerInvariant()}.",
                            Math.Min(workEnd, (m / RegionWeatherData.MinutesPerPeriod + 1) * RegionWeatherData.MinutesPerPeriod));
                    return new NpcWhereabouts(npc.WorkplaceId, WorkActivity(world, npc), "Working hours.", workEnd);
                }
            }
            else if (!workDay && !urgent && !falling && planned == scheduled && m < parts.EveningStarts && npc.Personality[NpcTrait.Curiosity] >= 65 &&
                     NpcGenesis.FirstOf(world, npc.HomeSettlementId, LocationKind.Market) is LocationData market)
            {
                // Phase 18: the curious spend a free day where the news is — the market.
                return new NpcWhereabouts(market.LocationId, ActivityKind.Standing, "A free day: at the market, watching the comings and goings.", parts.EveningStarts);
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
                // Phase 21 (ADR-0108): a free afternoon's errand at one of the town's shops (one in two days, an hour, by
                // seed), unless the weather keeps them in.
                var errand = ErrandAt(world, npc, date, severe || falling);
                if (errand.shop != null && m >= errand.start && m < errand.start + 60)
                    return new NpcWhereabouts(errand.shop.LocationId, ActivityKind.Trading, "An errand at " + errand.shop.Name + ".", errand.start + 60);
                return new NpcWhereabouts(home, ActivityKind.Resting, "At home today.", errand.shop != null && m < errand.start ? errand.start : parts.EveningStarts);
            }

            // Phase 18 (ADR-0094): the town's condition shows in the evening. In a hungry town the compassionate help hand out
            // bread in the square; in a restless one the timid stay indoors; in a prosperous one more people go out, in a
            // poor one fewer.
            if (town != null && m < sleep - 60 && npc.Role != NpcRole.Innkeeper)
            {
                if (town.IsInFoodShortage && npc.Personality[NpcTrait.Compassion] >= 65 && NpcGenesis.FirstOf(world, npc.HomeSettlementId, LocationKind.Square) is LocationData square)
                    return new NpcWhereabouts(square.LocationId, ActivityKind.Working, "Helping hand out bread while the town is short.", sleep - 60);
                if (world.WorldEvents.Exists(e => e.IsOpen && e.Type == WorldEventType.LocalUnrest && e.SettlementId == town.SettlementId) && npc.Personality[NpcTrait.Courage] <= 35)
                    return new NpcWhereabouts(home, ActivityKind.Sitting, "Keeping indoors while the town is restless.", sleep);
            }

            // Evening: the sociable go to the tavern, others go home.
            var tavern = NpcGenesis.FirstOf(world, npc.HomeSettlementId, LocationKind.Tavern);
            var outgoing = town == null ? 55 : town.Prosperity >= 70 ? 45 : town.Prosperity <= 30 ? 70 : 55; // Phase 18: a prosperous town goes out more
            if (severe) outgoing += 20; // Phase 21: only the most sociable go out in a storm
            var social = npc.Personality[NpcTrait.Sociability] >= outgoing || npc.Role == NpcRole.Innkeeper;
            if (severe && !social && tavern != null && m < sleep - 60 && npc.Personality[NpcTrait.Sociability] >= outgoing - 20)
                return new NpcWhereabouts(home, ActivityKind.Sitting, $"Staying in: {WeatherWords.Name(weather.Kind).ToLowerInvariant()} outside.", sleep);
            if (social && tavern != null && m < sleep - 60)
                return new NpcWhereabouts(tavern.LocationId, npc.Role == NpcRole.Innkeeper ? ActivityKind.Working : ActivityKind.Socialising,
                    npc.Role == NpcRole.Innkeeper ? "Serving the evening trade." : "An evening at the tavern.", sleep - 60);
            return new NpcWhereabouts(home, m < parts.NightStarts - 60 ? ActivityKind.Sitting : ActivityKind.Resting, "Evening at home.", sleep);
        }

        public static NpcWhereabouts Now(WorldData world, NpcData npc) => At(world, npc, world.CurrentSimulationDate, world.MinuteOfDay);

        /// <summary>
        /// Phase 21 (ADR-0108): a person's errand on a free day: which shop of their town (not their own) and from when, or
        /// none. Deterministic from the seed, the person and the day; only within the shop's hours.
        /// </summary>
        public static (ShopData shop, int start) ErrandAt(WorldData world, NpcData npc, WorldDate date, bool stayIn)
        {
            if (stayIn || npc.Role == NpcRole.Guard || !date.IsValidDate) return (null, 0);
            var seed = NpcHash.Of(world.Seed, "errand", npc.NpcId.ToString(), date.ToDayNumber());
            if (NpcHash.Roll(seed, 2) != 0) return (null, 0);
            var shops = new System.Collections.Generic.List<ShopData>();
            foreach (var s in world.Shops)
                if (s.SettlementId == npc.HomeSettlementId && s.ShopkeeperNpcId != npc.NpcId) shops.Add(s);
            if (shops.Count == 0) return (null, 0);
            shops.Sort((a, b) => a.ShopId.CompareTo(b.ShopId));
            var shop = shops[NpcHash.Roll(NpcHash.Next(seed, 1), shops.Count)];
            var start = 13 * 60 + 30 * NpcHash.Roll(NpcHash.Next(seed, 2), 6); // 13:00–15:30
            if (start < shop.OpensAt || start + 60 > shop.ClosesAt) return (null, 0);
            return (shop, start);
        }

        /// <summary>Phase 21: places whose work is done in the open (the market and the square).</summary>
        public static bool IsOpenAir(WorldData world, LocationId id)
        {
            foreach (var l in world.Locations)
                if (l.LocationId == id) return l.Kind == LocationKind.Market || l.Kind == LocationKind.Square;
            return false;
        }

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
