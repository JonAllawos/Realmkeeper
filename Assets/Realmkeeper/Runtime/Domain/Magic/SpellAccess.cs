using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Domain.Magic
{
    /// <summary>
    /// What a character can do with spells now (Phase 9), derived from its sheet and the catalog: the spells it knows,
    /// has in its book or has prepared, which of them it can cast, and the slots it can spend. Shared by the combat
    /// engine, the spellbook, the commands and validation; nothing here is stored.
    /// </summary>
    public sealed class SpellAccess
    {
        public SpellAccess(CharacterRules rules)
        {
            Rules = rules;
            Sheet = rules.Sheet;
            Class = rules.Class;
            Keeping = RpgCatalog.KeepingOf(Class);
            MaxSpellLevel = Class?.Spellcasting == null ? 0 : RpgCatalog.MaxSpellLevel(Class.Spellcasting, rules.Level);
        }

        public CharacterRules Rules { get; }
        public CharacterSheetData Sheet { get; }
        public ClassDefinition Class { get; }
        public SpellKeeping Keeping { get; }

        /// <summary>The highest spell level castable with a slot (0 = no slots).</summary>
        public int MaxSpellLevel { get; }

        public bool IsCaster => Keeping != SpellKeeping.None;
        public bool UsesPactMagic => Class?.Spellcasting?.Progression == CasterProgression.Pact;

        /// <summary>The warlock's slot level (all pact slots are this level).</summary>
        public int PactSlotLevel => UsesPactMagic ? Rules.Scaling("pact_slot_level") : 0;

        public int PreparedLimit => Rules.PreparedSpells;

        private List<string> Picks(string suffix)
        {
            var list = new List<string>();
            foreach (var pair in Sheet.Choices)
                if (pair.Key == "class:" + suffix || pair.Key.EndsWith(":" + suffix, StringComparison.Ordinal) && pair.Key.StartsWith("lv", StringComparison.Ordinal))
                    foreach (var v in pair.Value) if (!list.Contains(v)) list.Add(v);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        public IReadOnlyList<string> Cantrips => Picks("cantrips");

        /// <summary>Known casters' chosen spells (bard, sorcerer, warlock).</summary>
        public IReadOnlyList<string> KnownSpells => Keeping == SpellKeeping.Known ? Picks("spells") : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>The wizard's spellbook.</summary>
        public IReadOnlyList<string> Spellbook => Keeping == SpellKeeping.Spellbook ? Picks("spellbook") : (IReadOnlyList<string>)Array.Empty<string>();

        public IReadOnlyList<string> AlwaysPrepared
        {
            get
            {
                var list = new List<string>();
                foreach (var id in RpgCatalog.AlwaysPrepared(Rules)) if (!list.Contains(id) && SpellCatalog.Find(id) != null) list.Add(id);
                return list;
            }
        }

        public IReadOnlyList<string> Prepared => Keeping == SpellKeeping.Known || Keeping == SpellKeeping.None ? (IReadOnlyList<string>)Array.Empty<string>() : Sheet.Spellcasting.Prepared;

        /// <summary>Every leveled spell the character can cast now (known, prepared or always prepared; within its slot level).</summary>
        public List<SpellDefinition> ReadySpells()
        {
            var list = new List<SpellDefinition>();
            void Add(string id)
            {
                var s = SpellCatalog.Find(id);
                if (s != null && !s.IsCantrip && !list.Contains(s)) list.Add(s);
            }

            foreach (var id in KnownSpells) Add(id);
            foreach (var id in Prepared) Add(id);
            foreach (var id in AlwaysPrepared) Add(id);
            list.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

        public List<SpellDefinition> ReadyCantrips()
        {
            var list = new List<SpellDefinition>();
            foreach (var id in Cantrips)
            {
                var s = SpellCatalog.Find(id);
                if (s != null) list.Add(s);
            }

            return list;
        }

        /// <summary>Whether the character has a spell ready (a cantrip, or a leveled spell known or prepared).</summary>
        public bool IsReady(string spellId)
        {
            foreach (var s in ReadyCantrips()) if (s.Id == spellId) return true;
            foreach (var s in ReadySpells()) if (s.Id == spellId) return true;
            return false;
        }

        /// <summary>Slots left at a level (pact slots count at the pact slot level only).</summary>
        public int SlotsLeft(int level)
        {
            if (level < 1) return 0;
            if (UsesPactMagic) return level == PactSlotLevel ? Left("pact_slots") : 0;
            return Left("spell_slots_" + level);
        }

        private int Left(string id) => Sheet.Resources.TryGetValue(id, out var n) ? n : 0;

        /// <summary>The resource a cast at this level spends.</summary>
        public string SlotResource(int level) => UsesPactMagic ? "pact_slots" : "spell_slots_" + level;

        /// <summary>The slot levels this spell can be cast with now (cantrips: none needed).</summary>
        public List<int> CastLevels(SpellDefinition s)
        {
            var list = new List<int>();
            if (s.IsCantrip) return list;
            if (UsesPactMagic)
            {
                if (PactSlotLevel >= s.Level && SlotsLeft(PactSlotLevel) > 0) list.Add(PactSlotLevel);
                return list;
            }

            for (var l = s.Level; l <= 9; l++) if (SlotsLeft(l) > 0) list.Add(l);
            return list;
        }

        /// <summary>
        /// Why a new prepared list is not allowed (null when it is). Spells must be on the class list (the wizard's: in
        /// the book), of a level the character can cast, not cantrips, not repeated, within the limit. Removing or replacing
        /// spells needs the preparation window a long rest opens (the whole list, or one spell for the paladin and ranger).
        /// </summary>
        public string PreparationProblem(IReadOnlyList<string> next)
        {
            if (Keeping == SpellKeeping.None) return "This character does not cast spells.";
            if (Keeping == SpellKeeping.Known) return $"{Class.Name}s know their spells: they choose them at creation and level-up, not by preparing.";
            if (next.Count > PreparedLimit) return $"At most {PreparedLimit} spells can be prepared at level {Rules.Level}.";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var always = AlwaysPrepared;
            foreach (var id in next)
            {
                var s = SpellCatalog.Find(id);
                if (s == null) return $"There is no spell '{id}'.";
                if (!seen.Add(id)) return $"{s.Name} is listed twice.";
                if (s.IsCantrip) return $"{s.Name} is a cantrip; cantrips are always ready.";
                if (Contains(always, id)) return $"{s.Name} is always prepared already.";
                if (Keeping == SpellKeeping.Spellbook ? !Contains(Spellbook, id) : !SpellCatalog.OnList(s, Class.Id))
                    return Keeping == SpellKeeping.Spellbook ? $"{s.Name} is not in your spellbook." : $"{s.Name} is not a {Class.Name} spell.";
                if (s.Level > MaxSpellLevel) return $"{s.Name} is level {s.Level}; you cast up to level {MaxSpellLevel}.";
            }

            var removed = 0;
            foreach (var id in Sheet.Spellcasting.Prepared) if (!Contains(next, id)) removed++;
            var changes = Sheet.Spellcasting.PreparationChanges;
            if (removed > 0 && changes >= 0 && removed > changes)
                return changes == 0 ? "Prepared spells can be changed only after a long rest (you can still add spells while your list is not full)." :
                    $"After a long rest you can replace {changes} prepared spell{(changes == 1 ? string.Empty : "s")}; this replaces {removed}.";
            return null;
        }

        /// <summary>Problems with the stored prepared list (validation on save, load and every commit).</summary>
        public IEnumerable<string> StoredPreparedProblems()
        {
            var p = Sheet.Spellcasting.Prepared;
            if (Keeping == SpellKeeping.Known || Keeping == SpellKeeping.None)
            {
                if (p.Count > 0) yield return "Only prepared casters keep a prepared list.";
                yield break;
            }

            if (p.Count > PreparedLimit) yield return $"{p.Count} spells are prepared; the limit at level {Rules.Level} is {PreparedLimit}.";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in p)
            {
                var s = SpellCatalog.Find(id);
                if (s == null) { yield return $"Unknown prepared spell '{id}'."; continue; }
                if (!seen.Add(id)) yield return $"{s.Name} is prepared twice.";
                if (s.IsCantrip) yield return $"{s.Name} is a cantrip and cannot be prepared.";
                else if (Keeping == SpellKeeping.Spellbook ? !Contains(Spellbook, id) : !SpellCatalog.OnList(s, Class.Id)) yield return $"{s.Name} cannot be prepared by this character.";
                else if (s.Level > MaxSpellLevel) yield return $"{s.Name} is above the character's spell level.";
            }
        }

        private static bool Contains(IReadOnlyList<string> list, string x)
        {
            foreach (var y in list) if (y == x) return true;
            return false;
        }
    }
}
