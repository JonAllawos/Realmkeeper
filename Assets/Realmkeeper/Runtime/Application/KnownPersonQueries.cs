using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>How well the player knows a person, which decides what the player-facing inspection shows (Phase 20).</summary>
    public enum Acquaintance
    {
        /// <summary>Seen, never spoken with: only what anyone can see.</summary>
        Seen = 0,

        /// <summary>Spoken with: where they come from and whom they serve, as people say in passing.</summary>
        Acquainted = 1,

        /// <summary>Known well (familiarity 50+): their training, what they are good at, the languages they speak.</summary>
        Familiar = 2,

        /// <summary>Travels with the player: their whole sheet, as the player's own.</summary>
        Companion = 3,
    }

    /// <summary>One line of what the player knows about a person, with how they know it.</summary>
    public sealed class KnownLine
    {
        public KnownLine(string label, string value, string how) { Label = label; Value = value; How = how; }
        public string Label { get; }
        public string Value { get; }

        /// <summary>"seen", "said in conversation", "known from long acquaintance", "travels with you".</summary>
        public string How { get; }
    }

    /// <summary>
    /// Phase 20: a person as the player character knows them. Built from the same character record the rules use
    /// (never a second set of statistics), filtered by acquaintance. Memories, private knowledge, exact experience and
    /// the numbers on a stranger's sheet are never shown; what is withheld is said ("you would have to know them better").
    /// The strategic map's people inspector is the DM's view and is unaffected.
    /// </summary>
    public sealed class KnownPersonView
    {
        public NpcId NpcId { get; set; }
        public string Name { get; set; }
        public Acquaintance Acquaintance { get; set; }
        public string AcquaintanceText { get; set; }
        public List<KnownLine> Lines { get; } = new List<KnownLine>();

        /// <summary>What knowing them better would add, or null at the companion level.</summary>
        public string Withheld { get; set; }
    }

    public static class KnownPersonQueries
    {
        public const int FamiliarAt = 50;

        public static Acquaintance AcquaintanceOf(WorldData world, NpcData n)
        {
            if (world.Companions.Any(c => c.NpcId == n.NpcId && c.Standing == CompanionStanding.InParty)) return Acquaintance.Companion;
            if (n.PlayerRegard.Familiarity >= FamiliarAt) return Acquaintance.Familiar;
            if (n.ConversationCount > 0 || n.HasMetPlayer || n.PlayerRegard.Conversations > 0) return Acquaintance.Acquainted;
            return Acquaintance.Seen;
        }

        public static KnownPersonView View(WorldData world, NpcData n)
        {
            if (n == null) return null;
            var a = AcquaintanceOf(world, n);
            var v = new KnownPersonView { NpcId = n.NpcId, Name = n.Name, Acquaintance = a };
            var sheet = n.Character?.Sheet;
            var rules = sheet != null && sheet.State == CreationState.Complete ? new CharacterRules(sheet) : null;
            const string seen = "seen";
            const string said = "said in conversation";
            const string known = "known from long acquaintance";
            const string party = "travels with you";

            // Seen: what anyone can see.
            v.Lines.Add(new KnownLine("Looks like", (rules?.Species?.Name ?? "Human") + ", " + (n.Sex == NpcSex.Female ? "a woman " : "a man ") + AgeBand(world, n), seen));
            v.Lines.Add(new KnownLine("Occupation", n.Character?.Occupation ?? n.Role.ToString().ToLowerInvariant(), seen));
            var worn = WornItems(n);
            if (worn.Count > 0) v.Lines.Add(new KnownLine("Wears and carries", string.Join(", ", worn), seen));
            if (n.IsDeceased) v.Lines.Add(new KnownLine("Condition", "dead", seen));
            else if (rules != null && sheet.CurrentHp < rules.MaxHp / 2) v.Lines.Add(new KnownLine("Condition", sheet.CurrentHp == 0 ? "down and badly hurt" : "visibly hurt", seen));

            if (a >= Acquaintance.Acquainted)
            {
                var home = world.FindSettlement(n.HomeSettlementId);
                if (home != null) v.Lines.Add(new KnownLine("From", home.Name, said));
                if (rules?.Background != null) v.Lines.Add(new KnownLine("Background", rules.Background.Name, said));
                var faction = n.FactionId.IsEmpty ? null : world.FindFaction(n.FactionId);
                if (faction != null) v.Lines.Add(new KnownLine("Serves", faction.Name, said));
            }

            if (a >= Acquaintance.Familiar && rules != null)
            {
                v.Lines.Add(new KnownLine("Trained as", rules.Class.Name + (rules.Subclass != null ? " (" + rules.Subclass.Name + ")" : string.Empty) + ", " + PowerBand(rules.Level), known));
                var skills = SkillRules.All.Where(s => rules.SkillRank(s) != ProficiencyRank.None).Select(SkillRules.Name).ToList();
                if (skills.Count > 0) v.Lines.Add(new KnownLine("Good at", string.Join(", ", skills), known));
                var languages = rules.Languages.Select(l => RpgCatalog.FindLanguage(l)?.Name ?? l).ToList();
                if (languages.Count > 0) v.Lines.Add(new KnownLine("Speaks", string.Join(", ", languages), known));
            }

            if (a == Acquaintance.Companion && rules != null)
            {
                v.Lines.Add(new KnownLine("Level", rules.Class.Name + " " + rules.Level, party));
                v.Lines.Add(new KnownLine("Hit points", sheet.CurrentHp + " / " + rules.MaxHp, party));
                v.Lines.Add(new KnownLine("Speed · proficiency", rules.Speed + " ft · +" + rules.ProficiencyBonus, party));
                v.Lines.Add(new KnownLine("Abilities", string.Join("  ", AbilityRules.All.Select(x => x.ToString().Substring(0, 3) + " " + rules.Score(x) + " (" + Breakdown.Signed(rules.Modifier(x)) + ")")), party));
                var saves = AbilityRules.All.Where(rules.SaveProficient).Select(x => x + " " + Breakdown.Signed(rules.SaveBonus(x).Total)).ToList();
                if (saves.Count > 0) v.Lines.Add(new KnownLine("Saving throws", string.Join(", ", saves), party));
                var features = rules.Features.Select(f => f.feature.Name).Distinct().ToList();
                if (features.Count > 0) v.Lines.Add(new KnownLine("Features", string.Join(", ", features), party));
            }

            v.AcquaintanceText = a == Acquaintance.Seen ? "A stranger: you know only what you can see."
                : a == Acquaintance.Acquainted ? "You have spoken: you know what they have told you."
                : a == Acquaintance.Familiar ? "You know them well." : "They travel with you: you know their measure.";
            v.Withheld = a == Acquaintance.Seen ? "Speak with them to learn where they come from and whom they serve."
                : a == Acquaintance.Acquainted ? "You would have to know them better to know their training and skills."
                : a == Acquaintance.Familiar ? "Only a companion's full measure (abilities, saves, features) is yours to know." : null;
            return v;
        }

        private static string AgeBand(WorldData world, NpcData n)
        {
            if (!n.BirthDate.IsValidDate) return "of middle years";
            var age = (int)((world.CurrentSimulationDate.ToDayNumber() - n.BirthDate.ToDayNumber()) / Domain.Calendar.WorldDate.DaysPerYear);
            return age < 20 ? "barely grown" : age < 35 ? "in early adulthood" : age < 55 ? "of middle years" : "in later life";
        }

        /// <summary>A level said the way people would say it, not as a number.</summary>
        public static string PowerBand(int level) => level <= 2 ? "untried" : level <= 5 ? "seasoned" : level <= 10 ? "veteran" : "renowned";

        private static List<string> WornItems(NpcData n)
        {
            var list = new List<string>();
            var c = n.Character;
            if (c == null) return list;
            foreach (var pair in c.Equipment) list.Add(ItemCatalog.Find(pair.Value.ItemId)?.Name ?? pair.Value.ItemId);
            if (list.Count == 0 && c.Gear != null)
                foreach (var id in new[] { c.Gear.Body, c.Gear.MainHand, c.Gear.OffHand, c.Gear.Shield })
                    if (!string.IsNullOrEmpty(id)) list.Add(ItemCatalog.Find(id)?.Name ?? id);
            return list;
        }
    }

    public sealed partial class WorldSession
    {
        /// <summary>Phase 20: a person as the player character knows them (see <see cref="KnownPersonQueries"/>).</summary>
        public KnownPersonView GetKnownPerson(NpcId id) => HasWorld ? KnownPersonQueries.View(_world, _world.FindNpc(id)) : null;
    }
}
