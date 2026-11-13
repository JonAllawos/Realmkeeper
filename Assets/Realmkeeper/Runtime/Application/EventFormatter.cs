using System;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Turns the structured facts the simulation records into readable sentences. The
    /// simulation never writes prose; changing wording (or adding a language later) only
    /// touches this class. Events from older saves that carry only text are shown as written.
    /// </summary>
    public static class EventFormatter
    {
        public static string Format(HistoricalEvent e, Func<SettlementId, string> nameOf, Func<FactionId, string> factionNameOf = null)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (e.Data.Count == 0 && !string.IsNullOrEmpty(e.Description)) return e.Description;

            string Name(int index)
            {
                if (index >= e.SettlementIds.Count) return "a settlement";
                return nameOf?.Invoke(e.SettlementIds[index]) ?? "a settlement";
            }

            string F(int index)
            {
                if (index >= e.FactionIds.Count) return "a faction";
                return factionNameOf?.Invoke(e.FactionIds[index]) ?? "a faction";
            }

            string F2(FactionId id) => factionNameOf?.Invoke(id) ?? "a faction";

            var s = Name(0);
            switch (e.EventType)
            {
                case HistoricalEventType.WorldFounded:
                    return string.IsNullOrEmpty(e.Description) ? "The chronicle begins." : e.Description;
                case HistoricalEventType.FoodShortageBegan:
                    return $"Food is running short in {s}: about {N(e, "daysLeft")} days of reserves remain.";
                case HistoricalEventType.FoodShortageEnded:
                    return $"The granaries of {s} have recovered ({N(e, "reserves")} food in store).";
                case HistoricalEventType.FoodReservesExhausted:
                    return $"{s} has run out of food. People will begin to starve after {N(e, "graceDays")} days.";
                case HistoricalEventType.StarvationBegan:
                    return $"{s} is starving: {N(e, "dailyLoss")} people are lost each day.";
                case HistoricalEventType.ResourceShortageBegan:
                    return $"{s} is short of {Resource(e)}: {N(e, "stock")} left against {N(e, "demand")} needed a day.";
                case HistoricalEventType.ResourceShortageEnded:
                    return $"{s} has enough {Resource(e)} again.";
                case HistoricalEventType.TradeShipmentDelivered:
                {
                    var lost = e.GetInt("lost");
                    var spoilage = lost > 0 ? $", {N(e, "lost")} spoiled on the road" : string.Empty;
                    return $"{N(e, "quantity")} {Resource(e)} from {s} reached {Name(1)} for {N(e, "payment")} coin{spoilage}.";
                }
                case HistoricalEventType.TradeShipmentReturned:
                    return $"A shipment of {Resource(e)} from {s} came home: {Name(1)} had no room to store it.";
                case HistoricalEventType.ProsperityChanged:
                    return $"Prosperity in {s} {(e.GetInt("to") > e.GetInt("from") ? "rose" : "fell")} from {N(e, "from")} to {N(e, "to")}.";
                case HistoricalEventType.StabilityChanged:
                    return $"Stability in {s} {(e.GetInt("to") > e.GetInt("from") ? "rose" : "fell")} from {N(e, "from")} to {N(e, "to")}.";
                case HistoricalEventType.SettlementGrew:
                    return $"{s} has grown to {N(e, "to")} people (from {N(e, "from")}).";
                case HistoricalEventType.SettlementDeclined:
                    return $"{s} has shrunk to {N(e, "to")} people (from {N(e, "from")}).";
                case HistoricalEventType.ProductionMilestone:
                    return $"{s} has produced {N(e, "amount")} {Resource(e)} since its founding.";
                case HistoricalEventType.EconomicMilestone:
                    return $"The treasury of {s} has passed {N(e, "treasury")} coin.";
                case HistoricalEventType.PlayerMetResident:
                    return $"{e.Get("player") ?? "A traveller"} met {e.Get("name") ?? "a resident"}, {(e.Get("role") ?? "resident").ToLowerInvariant()} of {s}.";
                case HistoricalEventType.PlayerPurchase:
                    return $"{e.Get("player") ?? "A traveller"} bought {Qty(e)} from {e.Get("keeper") ?? "a merchant"} at {e.Get("shop") ?? "a shop"} in {s} for {N(e, "total")} coin.";
                case HistoricalEventType.PlayerSale:
                    return $"{e.Get("player") ?? "A traveller"} sold {Qty(e)} to {e.Get("keeper") ?? "a merchant"} at {e.Get("shop") ?? "a shop"} in {s} for {N(e, "total")} coin.";
                case HistoricalEventType.PlayerCharacterCreated:
                    return $"{e.Get("player") ?? "A traveller"}, {An($"{(e.Get("species") ?? "traveller").ToLowerInvariant()} {(e.Get("background") ?? "wanderer").ToLowerInvariant()}")}, set out as {An((e.Get("class") ?? "adventurer").ToLowerInvariant())}.";
                case HistoricalEventType.PlayerLevelAdvanced:
                    return $"{e.Get("player") ?? "A traveller"} rose to level {e.Get("to") ?? "?"} as {An((e.Get("class") ?? "adventurer").ToLowerInvariant())}" + (string.IsNullOrEmpty(e.Get("subclass")) ? "." : $" of the {e.Get("subclass")}.");
                case HistoricalEventType.PlayerLevelReady:
                    return $"{e.Get("player") ?? "A traveller"} had earned enough experience to rise from level {e.Get("from") ?? "?"} to level {e.Get("to") ?? "?"}.";
                case HistoricalEventType.EncounterStarted:
                    return $"A fight broke out in {s}: {e.Get("name") ?? "an encounter"} ({e.Get("participants") ?? "?"} taking part).";
                case HistoricalEventType.EncounterEnded:
                    return $"The fight in {s} ({e.Get("name") ?? "an encounter"}) ended after {e.Get("rounds") ?? "?"} rounds: {e.Get("result") ?? "it was over"}.";
                case HistoricalEventType.CombatantDowned:
                    return $"{e.Get("name") ?? "Someone"} was struck down in {s}" + (string.IsNullOrEmpty(e.Get("by")) ? "." : $" by {e.Get("by")}.");
                case HistoricalEventType.CombatantKilled:
                    return $"{e.Get("name") ?? "Someone"} was killed in {s}" + (string.IsNullOrEmpty(e.Get("by")) ? "." : $" by {e.Get("by")}.");
                case HistoricalEventType.NpcPromoted:
                    return $"{e.Get("name") ?? "A resident"} of {s} took up arms as {An((e.Get("class") ?? "adventurer").ToLowerInvariant())} (level {e.Get("level") ?? "1"}).";
                case HistoricalEventType.NpcLevelAdvanced:
                    return $"{e.Get("name") ?? "A resident"} of {s} rose to level {e.Get("to") ?? "?"} as {An((e.Get("class") ?? "adventurer").ToLowerInvariant())}.";
                // Phase 12: quests, decisions, reputation and discoveries.
                case HistoricalEventType.QuestDiscovered: return $"The player heard of \"{e.Get("quest")}\"" + (string.IsNullOrEmpty(e.Get("reason")) ? "." : " (" + e.Get("reason") + ").");
                case HistoricalEventType.QuestOffered: return $"{e.Get("giver")} asked the player for help: \"{e.Get("quest")}\".";
                case HistoricalEventType.QuestAccepted: return $"The player took on \"{e.Get("quest")}\" for {e.Get("giver")}.";
                case HistoricalEventType.QuestDeclined: return $"The player turned down {e.Get("giver")}'s request, \"{e.Get("quest")}\".";
                case HistoricalEventType.QuestAbandoned: return $"The player gave up \"{e.Get("quest")}\".";
                case HistoricalEventType.QuestCompleted: return $"The player completed \"{e.Get("quest")}\" for {e.Get("giver")}" + (string.IsNullOrEmpty(e.Get("outcome")) ? "." : " (" + e.Get("outcome") + ").");
                case HistoricalEventType.QuestFailed: return $"\"{e.Get("quest")}\" failed: {e.Get("reason")}";
                case HistoricalEventType.DialogueDecision: return $"A decision: {e.Get("decision")}.";
                case HistoricalEventType.ReputationChanged: return $"{e.Get("target")}: {e.Get("dimension")?.ToLowerInvariant()} {(N(e, "amount").StartsWith("-") ? string.Empty : "+")}{N(e, "amount")} ({e.Get("reason")}).";
                case HistoricalEventType.SettlementProblemResolved: return $"A problem in {s} was resolved: {e.Get("what")}.";
                case HistoricalEventType.PlayerDiscovery: return $"Discovered: {e.Get("what")}";
                case HistoricalEventType.QuestItemDelivered: return $"The player delivered {e.Get("quantity")} × {e.Get("item")} to {e.Get("to")}.";
                case HistoricalEventType.QuestConsequence: return $"In {s}, {e.Get("what")}.";
                // Phase 13: places.
                case HistoricalEventType.PlaceDiscovered: return $"The player learned where {e.Get("place")} lies ({e.Get("how")}).";
                case HistoricalEventType.PlaceEntered: return $"{e.Get("by") ?? "The player"} went into {e.Get("place")} for the first time.";
                case HistoricalEventType.PlaceCleared: return $"{e.Get("place")} was cleared of its inhabitants by {e.Get("by") ?? "the player"}.";
                case HistoricalEventType.TrapSprung: return $"{e.Get("by") ?? "Someone"} set off a {(e.Get("trap") ?? "trap").ToLowerInvariant()} in {e.Get("place")}.";
                case HistoricalEventType.CaptiveFreed: return $"{e.Get("by") ?? "The player"} freed {e.Get("npc")} in {e.Get("place")}.";
                case HistoricalEventType.EscortArrived: return $"{e.Get("npc")} was brought home to {s} by {e.Get("by") ?? "the player"}.";
                case HistoricalEventType.Trespass: return $"{e.Get("by") ?? "A stranger"} was caught trespassing in the {(e.Get("room") ?? "keep").ToLowerInvariant()} of {e.Get("place")}.";
                case HistoricalEventType.PlaceSecretFound: return $"A hidden way was found in {e.Get("place")}.";
                case HistoricalEventType.BridgeCollapsed: return $"The bridge in {e.Get("place")} gave way under {e.Get("by") ?? "someone"} and fell.";
                case HistoricalEventType.CrimeWitnessed: return $"{e.Get("witness") ?? "Someone"} saw {e.Get("culprit") ?? "a stranger"} {e.Get("deed") ?? "commit a crime"} in {e.Get("place") ?? s}.";
                case HistoricalEventType.CrimeDiscovered: return $"{e.Get("victim") ?? "Someone"} found that {e.Get("deed") ?? "they had been robbed"}; nobody knows who did it.";
                case HistoricalEventType.CrimeSettled: return $"{e.Get("by") ?? "The player"} set right {e.Get("deed") ?? "a wrong"} in {s}{(e.Get("how") is string how ? " (" + how + ")" : string.Empty)}.";
                case HistoricalEventType.CreaturesWoke: return $"{e.Get("group") ?? "Something"} woke in {e.Get("place")} when {e.Get("by") ?? "someone"} disturbed {(e.Get("what") ?? "them").ToLowerInvariant()}.";
                case HistoricalEventType.OutlawsDeparted: return $"The outlaws of {e.Get("place")} left the road, {e.Get("how") ?? "talked off it"} by {e.Get("by") ?? "the player"}.";
                case HistoricalEventType.HarmTaken: return $"{e.Get("by") ?? "The player"} was hurt by {e.Get("source") ?? "a hazard"} in {e.Get("place")}: {e.Get("what") ?? "injured"}.";
                case HistoricalEventType.CompanionJoined: return $"{e.Get("name")} joined {e.Get("player") ?? "the player"}'s party{(e.Get("terms") is string t && t.Length > 0 ? " (" + t + ")" : string.Empty)}.";
                case HistoricalEventType.CompanionLeft: return $"{e.Get("name")} left {e.Get("player") ?? "the player"}'s party: {e.Get("why")}.";
                case HistoricalEventType.CompanionLoyalty: return $"{e.Get("name")}: {e.Get("why")}.";
                case HistoricalEventType.CaravanLost: return "Outlaws took the " + (e.Get("caravan") ?? "caravan") + " (" + e.Get("why") + "): " + e.Get("lost") + " " + e.Get("resource") + " lost.";
                case HistoricalEventType.WorldEventChanged: return UpperFirst(e.Get("what") ?? "something") + ": " + e.Get("state") + (string.IsNullOrEmpty(e.Get("why")) ? "." : " (" + e.Get("why") + ").");
                case HistoricalEventType.JourneyMilestone: return (e.Get("player") ?? "The player") + " " + e.Get("what") + ".";
                case HistoricalEventType.EncounterResolved:
                    return $"The aftermath of {e.Get("name") ?? "a fight"} near {s}: {e.Get("summary") ?? "nothing further"}.";
                case HistoricalEventType.LabourNoticePosted:
                    return $"{e.Get("player") ?? "A traveller"} posted a notice in {s} calling for more {(e.Get("focus") ?? "work").ToLowerInvariant()}.";
                case HistoricalEventType.TradeRouteSuspended:
                    return $"Trade from {s} to {Name(1)} was suspended.";
                case HistoricalEventType.TradeRouteResumed:
                    return $"Trade from {s} to {Name(1)} resumed.";
                case HistoricalEventType.FactionFounded:
                    return $"{F(0)} was founded.";
                case HistoricalEventType.FactionDissolved:
                    return $"{F(0)} has dissolved after a long decline.";
                case HistoricalEventType.FactionStatusChanged:
                    return $"{F(0)} is now {(e.Get("to") ?? "changed").ToLowerInvariant()} (influence {N(e, "influence")}).";
                case HistoricalEventType.FactionInfluenceChanged:
                    return $"The influence of {F(0)} {(e.GetInt("to") > e.GetInt("from") ? "rose" : "fell")} from {N(e, "from")} to {N(e, "to")}; its {(e.Get("factor") ?? "standing").ToLowerInvariant()} counts for most.";
                case HistoricalEventType.GovernanceChanged:
                {
                    var to = string.IsNullOrEmpty(e.Get("to")) ? "independent" : "governed by " + F(0);
                    return $"{By(e)}{s} is now {to}.";
                }
                case HistoricalEventType.TerritorialClaimFiled:
                    return $"{By(e)}{F(0)} laid claim to {s}.";
                case HistoricalEventType.TerritorialClaimContested:
                    return $"{By(e)}{F(0)}'s claim on {s} is contested{Because(e)}.";
                case HistoricalEventType.TerritorialClaimResolved:
                    return $"{By(e)}{F(0)}'s claim on {s} is {(e.Get("outcome") ?? "resolved").ToLowerInvariant()}{Because(e)}.";
                case HistoricalEventType.RelationshipChanged:
                    return e.Get("from") == e.Get("to")
                        ? $"{By(e)}{F(0)} and {F(1)} remain {(e.Get("to") ?? "neutral").ToLowerInvariant()} (opinion {N(e, "opinion")})."
                        : $"{By(e)}{F(0)} and {F(1)} went from {(e.Get("from") ?? "neutral").ToLowerInvariant()} to {(e.Get("to") ?? "neutral").ToLowerInvariant()}{CauseOf(e)}.";
                case HistoricalEventType.AgreementEstablished:
                    return $"{By(e)}{F(0)} and {F(1)} entered a {AgreementName(e)}.";
                case HistoricalEventType.AgreementBroken:
                    return $"{By(e)}the {AgreementName(e)} between {F(0)} and {F(1)} was broken.";
                case HistoricalEventType.FactionGoalCompleted:
                    return $"{F(0)} achieved a goal: {GoalText(e, s, F(1))}.";
                case HistoricalEventType.FactionGoalAbandoned:
                    return $"{By(e)}{F(0)} gave up a goal: {GoalText(e, s, F(1))}.";
                case HistoricalEventType.FactionGoalSet:
                    return $"{By(e)}{F(0)} took up a goal: {GoalText(e, s, F(1))}.";
                case HistoricalEventType.AffiliationShifted:
                    return $"Support for {F(0)} in {s} {e.Get("direction") ?? "changed"} to {N(e, "members")} people ({e.GetInt("toPerMille") / 10}% of the settlement).";
                case HistoricalEventType.FactionPresenceEstablished:
                    return $"{By(e)}{F(0)} established itself in {s}.";
                case HistoricalEventType.FactionPresenceWithdrawn:
                    return $"{By(e)}{F(0)} withdrew from {s}.";
                case HistoricalEventType.NpcDied:
                    return $"{e.Get("name") ?? "A resident"}, {(e.Get("role") ?? "resident").ToLowerInvariant()} of {s}, died.";
                case HistoricalEventType.NpcGoalSet:
                    return $"{By(e)}{e.Get("name") ?? "A resident"} set out to {NpcText.Goal(e.Get("goal"))}.";
                case HistoricalEventType.NpcGoalAchieved:
                    return $"{e.Get("name") ?? "A resident"} achieved their goal: {NpcText.Goal(e.Get("goal"))}.";
                case HistoricalEventType.NpcGoalAbandoned:
                    return $"{By(e)}{e.Get("name") ?? "A resident"} gave up trying to {NpcText.Goal(e.Get("goal"))}.";
                case HistoricalEventType.NpcRelationshipFormed:
                case HistoricalEventType.NpcRelationshipChanged:
                    return $"{By(e)}{e.Get("nameA") ?? "Two residents"} and {e.Get("nameB") ?? "another"} are now {NpcText.Relation(e.Get("to"))}{NpcText.Cause(e.Get("cause"))}.";
                case HistoricalEventType.NpcMoved:
                    return $"{By(e)}{e.Get("name") ?? "A resident"} moved to {e.Get("place") ?? s}.";
                case HistoricalEventType.NpcIdentityChanged:
                    return $"{By(e)}{e.Get("name") ?? "A resident"}'s identity was changed ({e.Get("fields") ?? "details"}).";
                case HistoricalEventType.NpcOccupationChanged:
                    return $"{By(e)}{e.Get("name") ?? "A resident"} is now a {(e.Get("role") ?? "resident").ToLowerInvariant()}.";
                case HistoricalEventType.NpcScheduleChanged:
                    return $"{By(e)}{e.Get("name") ?? "A resident"}'s weekly routine changed.";
                case HistoricalEventType.NpcMemoryAdded:
                    return $"{By(e)}{e.Get("name") ?? "A resident"} now remembers {e.Get("subject") ?? "something"}.";
                case HistoricalEventType.NpcKnowledgeAdded:
                    return $"{By(e)}{e.Get("name") ?? "A resident"} was told something ({NpcText.Topic(e.Get("topic"))}).";
                case HistoricalEventType.NpcAffiliationChanged:
                    return $"{By(e)}{e.Get("name") ?? "A resident"} now belongs to {(e.Get("faction") is string fid && Guid.TryParse(fid, out var g) ? F2(new FactionId(g)) : "no faction")}.";
                case HistoricalEventType.NpcConversation:
                    return $"{e.Get("player") ?? "A traveller"} spoke with {e.Get("name") ?? "a resident"} about {NpcText.Topic(e.Get("topic"))}.";
                default:
                    return string.IsNullOrEmpty(e.Description) ? e.EventType.ToString() : e.Description;
            }
        }

        /// <summary>Display name for a resource key ("iron" → "Iron").</summary>
        public static string ResourceName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "-";
            return char.ToUpperInvariant(key[0]) + key.Substring(1).Replace('_', ' ').Replace('.', ' ');
        }

        /// <summary>"By your hand, " for events a player command caused; empty for the simulation's.</summary>
        private static string By(HistoricalEvent e) => e.Get("source") == "dm" ? "By the DM: " : IsManual(e) ? "By your hand: " : string.Empty;

        /// <summary>True when a player command caused the event (its "source" fact is "player").</summary>
        public static bool IsManual(HistoricalEvent e) => e.Get("source") == "player" || e.Get("source") == "dm";

        private static string Because(HistoricalEvent e)
        {
            switch (e.Get("cause"))
            {
                case "competingClaim": return " by a competing claim";
                case "poorRelations": return " because relations with the governing authority are poor";
                case "goodRelations": return " because relations with the governing authority are good";
                case "administrator": return " because the claimant already administers it";
                case "noAuthority": return " because no authority governs it";
                case "governs": return ": the claimant now governs it";
                default: return string.Empty;
            }
        }

        private static string CauseOf(HistoricalEvent e) =>
            Enum.TryParse<RelationCause>(e.Get("cause"), out var cause) && cause != RelationCause.None ? " because of " + PoliticalText.Cause(cause) : string.Empty;

        private static string AgreementName(HistoricalEvent e) =>
            Enum.TryParse<AgreementType>(e.Get("agreement"), out var type) ? PoliticalText.Agreement(type) : "agreement";

        private static string GoalText(HistoricalEvent e, string settlement, string faction)
        {
            if (!Enum.TryParse<GoalType>(e.Get("goalType"), out var type)) return "a goal";
            var text = PoliticalText.Goal(type, e.GetInt("target"), e.SettlementIds.Count > 0 ? settlement : null, e.FactionIds.Count > 1 ? faction : null);
            return char.ToLowerInvariant(text[0]) + text.Substring(1);
        }

        private static string Resource(HistoricalEvent e) => e.Get("resource") ?? "goods";

        private static string An(string phrase) => string.IsNullOrEmpty(phrase) ? phrase : ("aeiou".IndexOf(phrase[0]) >= 0 ? "an " : "a ") + phrase;

        private static string Qty(HistoricalEvent e)
        {
            var q = e.GetInt("quantity");
            var item = e.Get("itemName") ?? e.Get("item") ?? "goods";
            return q > 1 ? q.ToString(System.Globalization.CultureInfo.InvariantCulture) + " × " + item : item;
        }

        private static string N(HistoricalEvent e, string key)
        {
            var raw = e.Get(key);
            if (raw == null) return "?";
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n.ToString("N0", CultureInfo.InvariantCulture)
                : raw;
        }

        private static string UpperFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
