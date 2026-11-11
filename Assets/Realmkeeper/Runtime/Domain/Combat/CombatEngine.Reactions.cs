using System;
using System.Collections.Generic;
using System.Reflection;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// Phase 10: reactions and roll decisions (ADR-0045). At each trigger the engine asks once. A computer-controlled
    /// reactor answers by a deterministic policy (remaining resources, hit points, expected damage, what is at stake).
    /// The player is asked: the action stops, everything it did is undone, and the opportunity waits in the encounter
    /// (saved with it). The answer replays the same action from the same state with the recorded answers — the dice
    /// are counter-based, so every roll before the question comes out the same — and either finishes it or stops at
    /// the next question. An opportunity is answered once; a stale answer is refused.
    /// </summary>
    public sealed partial class CombatEngine
    {
        private IReadOnlyList<bool> _answers;
        private int _asked;

        private sealed class ReactionPending : Exception
        {
            public ReactionPending(ReactionOpportunity opportunity) { Opportunity = opportunity; }
            public ReactionOpportunity Opportunity { get; }
        }

        /// <summary>
        /// Offers an opportunity. A computer-controlled reactor decides now; for the player the recorded answer is used,
        /// or the action is suspended to ask them. True to take it.
        /// </summary>
        private bool Ask(ReactionOpportunity o)
        {
            var reactor = Encounter.Find(o.ReactorId);
            if (reactor == null) return false;
            if (reactor.Control != CombatControl.Player) return AiReacts(reactor, o);
            if (_answers != null && _asked < _answers.Count) return _answers[_asked++];
            throw new ReactionPending(o);
        }

        // ----- Suspending and replaying ------------------------------------------------------------------

        private sealed class Snapshot
        {
            public EncounterData Encounter;
            public CharacterSheetData Player;
            public readonly Dictionary<NpcCharacterData, CharacterSheetData> Sheets = new Dictionary<NpcCharacterData, CharacterSheetData>();
            public int Chronicle;
            public int Deaths;
            public List<ItemStackData> Pack; // Phase 11: ammunition spent and wear are undone with the rest
            public Dictionary<EquipmentSlot, ItemStackData> Worn;
        }

        private Snapshot Take()
        {
            var s = new Snapshot { Encounter = Encounter.Clone(), Player = _world.Player?.Rpg?.Clone(), Chronicle = Chronicle.Count, Deaths = ResidentDeaths.Count };
            if (_world.Player != null)
            {
                s.Pack = _world.Player.Inventory.ConvertAll(x => x.Clone());
                s.Worn = new Dictionary<EquipmentSlot, ItemStackData>();
                foreach (var pair in _world.Player.Equipment) s.Worn[pair.Key] = pair.Value.Clone();
            }
            foreach (var c in Encounter.Combatants)
            {
                if (c.Kind != CombatantKind.Npc || !Guid.TryParse(c.SourceId, out var g)) continue;
                var record = _world.FindNpc(new Identifiers.NpcId(g))?.Character;
                if (record?.Sheet != null && !s.Sheets.ContainsKey(record)) s.Sheets[record] = record.Sheet.Clone();
            }

            return s;
        }

        private void Restore(Snapshot s)
        {
            // The same objects stay live (callers hold the encounter and its combatants): their state is put back in place.
            var live = Encounter.Combatants;
            CopyInto(s.Encounter, Encounter);
            foreach (var past in s.Encounter.Combatants)
            {
                var c = live.Find(x => x.CombatantId == past.CombatantId);
                if (c != null) CopyInto(past, c);
            }

            Encounter.Combatants = live;
            if (s.Player != null) CopyInto(s.Player, _world.Player.Rpg);
            if (s.Pack != null) { _world.Player.Inventory = s.Pack; _world.Player.Equipment = s.Worn; }
            foreach (var pair in s.Sheets) CopyInto(pair.Value, pair.Key.Sheet);
            Chronicle.RemoveRange(s.Chronicle, Chronicle.Count - s.Chronicle);
            ResidentDeaths.RemoveRange(s.Deaths, ResidentDeaths.Count - s.Deaths);
            _stats.Clear(); // the stats held the objects the action changed
        }

        /// <summary>Puts every public settable property of <paramref name="from"/> into <paramref name="to"/> (the same object stays in every list).</summary>
        private static void CopyInto<T>(T from, T to) where T : class
        {
            foreach (var p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0) p.SetValue(to, p.GetValue(from));
        }

        /// <summary>Why the player cannot answer now (no question, or a stale one), or null.</summary>
        public string ResponseProblem(int reactionId)
        {
            var p = Encounter.PendingReaction;
            if (p == null) return "Nothing is waiting on an answer.";
            if (p.Id != reactionId) return "That question was already answered.";
            if (p.Sequence != Encounter.ActionSequence) return "The encounter has moved on.";
            return null;
        }

        /// <summary>The player's answer: the waiting action is replayed with it (and every earlier answer).</summary>
        public ActionOutcome Respond(int reactionId, bool accept)
        {
            var problem = ResponseProblem(reactionId);
            if (problem != null) return ActionOutcome.Refused(problem);
            var p = Encounter.PendingReaction;
            Encounter.PendingReaction = null;
            var answers = new List<bool>(p.Answers) { accept };
            return Resolve(p.ActorId, ActionCodec.FromRecord(p.Action), answers, true);
        }

        /// <summary>Records the suspended action and its question (the state is already back to before the action).</summary>
        private ActionOutcome Suspend(CombatantData actor, CombatAction action, List<bool> answers, ReactionOpportunity o)
        {
            Encounter.PendingReaction = new PendingReactionData
            {
                Id = Encounter.NextReactionId++, Opportunity = o, ActorId = actor.CombatantId, Action = ActionCodec.ToRecord(action),
                Answers = answers ?? new List<bool>(), Sequence = Encounter.ActionSequence,
            };
            var r = Encounter.Find(o.ReactorId);
            return new ActionOutcome { Ok = true, Pending = Encounter.PendingReaction, Error = null, WaitingOn = r?.Name };
        }

        // ----- The computer's policy -----------------------------------------------------------------------

        /// <summary>
        /// A computer-controlled reactor's answer (deterministic). Opportunity attacks: taken by anything that fights.
        /// Shield and Parry: when they turn the hit into a miss and the blow matters (a quarter of its hit points, or it is
        /// below half). Counterspell: a hostile spell of level 1 or higher. Cutting Words: when the die can plausibly turn
        /// the hit. Uncanny Dodge, Deflect Attacks: damage worth halving. Hellish Rebuke: always. Decisions: always.
        /// </summary>
        private bool AiReacts(CombatantData reactor, ReactionOpportunity o)
        {
            var def = CreatureOf(reactor);
            var hp = Hp(reactor);
            switch (o.Kind)
            {
                case ReactionKind.OpportunityAttack:
                    return def == null || (def.Behavior.Kind != CreatureBehavior.Skittish && def.Behavior.Kind != CreatureBehavior.Passive);
                case ReactionKind.Shield:
                case ReactionKind.Parry:
                    return _aiExpectedDamage >= Math.Max(1, hp / 4) || hp * 2 < MaxHp(reactor);
                case ReactionKind.Counterspell:
                    return _aiSpellLevel >= 1;
                case ReactionKind.CuttingWords:
                    return _aiMargin <= 4;
                case ReactionKind.UncannyDodge:
                case ReactionKind.DeflectAttacks:
                    return _aiExpectedDamage >= 4;
                default:
                    return true;
            }
        }

        // What the policy weighs, set just before each question.
        private int _aiExpectedDamage;
        private int _aiSpellLevel;
        private int _aiMargin;

        // ----- The opportunities ---------------------------------------------------------------------------

        private bool HasReadySpell(CombatantData c, string spellId, out int slot)
        {
            slot = 0;
            if (!CanReact(c)) return false;
            var s = SpellCatalog.Find(spellId);
            if (s == null || !s.Castable) return false;
            var ready = false;
            foreach (var x in SpellsOf(c)) if (x.Id == spellId) ready = true;
            if (!ready) return false;
            var levels = CastLevels(c, s);
            if (levels.Count == 0) return false;
            slot = levels[0];
            return true;
        }

        private void SpendSlot(CombatantData c, int level)
        {
            var access = Access(c);
            SpendResource(c, access != null ? access.SlotResource(level) : "spell_slots_" + level, 1);
        }

        /// <summary>A hit about to land (not a natural 20): the target's Shield or Parry, then a foe's Cutting Words, may turn it.</summary>
        private void ReactToHit(CombatantData attacker, CombatantData target, AttackOption o, AttackResult r, int natural)
        {
            if (!r.Hit || r.Critical || natural == 20) return;
            var expected = Math.Max(1, o.Damage.Count * (o.Damage.Sides + 1) / 2 + o.Damage.Bonus);

            // Shield (+5 AC until the start of its next turn, against this attack too).
            if (HasReadySpell(target, "shield", out var slot) && FirstEffect(target, CombatEffectKind.ArmorClassBonus) == null)
            {
                var turns = r.Total < r.TargetAc + 5;
                _aiExpectedDamage = expected;
                if (Ask(new ReactionOpportunity
                {
                    Kind = ReactionKind.Shield, Name = "Shield", ReactorId = target.CombatantId, TriggerId = attacker.CombatantId, TargetId = target.CombatantId,
                    Trigger = $"{attacker.Name} hits {target.Name} with {o.Name}: {r.Total} against AC {r.TargetAc}.",
                    Cost = $"{target.Name}'s reaction and a level {slot} spell slot",
                    Consequence = $"+5 AC until {target.Name}'s next turn: {r.Total} against AC {r.TargetAc + 5} {(turns ? "would miss" : "would still hit")}.",
                }))
                {
                    target.Turn.ReactionUsed = true;
                    SpendSlot(target, slot);
                    AddEffect(NewInstance(), "shield", "Shield", target, target, CombatEffectKind.ArmorClassBonus, 5, expiry: EffectExpiry.StartOfTurn, expiresOn: target);
                    r.TargetAc += 5;
                    r.Hit = r.Total >= r.TargetAc;
                    if (!r.Hit) r.MissReason = $"{r.Total} is below AC {r.TargetAc} (Shield)";
                    Log(CombatLogKind.Spell, target.CombatantId, attacker.CombatantId, $"{target.Name} casts Shield as a reaction (a level {slot} slot): AC {r.TargetAc} — {(r.Hit ? "still a hit" : "the attack misses")}.");
                    if (!r.Hit) return;
                }
            }

            // Parry (a creature's reaction): +2 AC against one melee hit, holding a melee weapon.
            var parry = CreatureOf(target)?.Reaction(CreatureReactionKind.Parry);
            if (parry != null && o.Melee && CanReact(target) && Stats(target).BestMelee() != null)
            {
                _aiExpectedDamage = expected;
                if (r.Total < r.TargetAc + parry.Value && Ask(new ReactionOpportunity
                {
                    Kind = ReactionKind.Parry, Name = "Parry", ReactorId = target.CombatantId, TriggerId = attacker.CombatantId, TargetId = target.CombatantId,
                    Trigger = $"{attacker.Name} hits {target.Name}: {r.Total} against AC {r.TargetAc}.", Cost = $"{target.Name}'s reaction",
                    Consequence = $"+{parry.Value} AC against this attack: it would miss.",
                }))
                {
                    target.Turn.ReactionUsed = true;
                    r.TargetAc += parry.Value;
                    r.Hit = r.Total >= r.TargetAc;
                    r.MissReason = $"{r.Total} is below AC {r.TargetAc} (parried)";
                    Log(CombatLogKind.Action, target.CombatantId, attacker.CombatantId, $"{target.Name} parries: AC {r.TargetAc} — the attack misses.");
                    return;
                }
            }

            // Cutting Words (a bard of the College of Lore within 60 ft of the attacker, hostile to it).
            foreach (var bard in Encounter.Combatants)
            {
                if (!r.Hit || bard == attacker || !Hostile(bard, attacker) || !CanReact(bard) || bard.Position.DistanceFeet(attacker.Position) > 60) continue;
                if (!AttackerFeature(bard, "cutting_words") || ResourceLeft(bard, "bardic_inspiration") <= 0) continue;
                var sides = Math.Max(6, Stats(bard).Rules.Scaling("bardic_inspiration"));
                var margin = r.Total - r.TargetAc + 1;
                _aiMargin = margin;
                if (margin > sides) continue; // the die cannot turn it
                if (!Ask(new ReactionOpportunity
                {
                    Kind = ReactionKind.CuttingWords, Name = "Cutting Words", ReactorId = bard.CombatantId, TriggerId = attacker.CombatantId, TargetId = target.CombatantId,
                    Trigger = $"{attacker.Name} hits {target.Name}: {r.Total} against AC {r.TargetAc}.", Cost = $"{bard.Name}'s reaction and a Bardic Inspiration use",
                    Consequence = $"Subtract 1d{sides} from the roll: it misses on a {margin} or more.",
                })) continue;
                bard.Turn.ReactionUsed = true;
                SpendResource(bard, "bardic_inspiration", 1);
                var d = Die(sides);
                r.Total -= d;
                r.Hit = r.Total >= r.TargetAc;
                if (!r.Hit) r.MissReason = $"{r.Total} is below AC {r.TargetAc} (Cutting Words)";
                Log(CombatLogKind.Action, bard.CombatantId, attacker.CombatantId, $"{bard.Name}'s Cutting Words: −{d} — {r.Total} against AC {r.TargetAc}: {(r.Hit ? "still a hit" : "a miss")}.", d);
            }
        }

        /// <summary>Damage about to land: Uncanny Dodge halves it; Deflect Attacks reduces bludgeoning, piercing or slashing. The damage left.</summary>
        private int ReactToDamage(CombatantData attacker, CombatantData target, AttackOption o, AttackResult r, int amount)
        {
            if (amount <= 0) return amount;
            if (TargetFeature(target, "deflect_attacks") && CanReact(target) && (o.DamageType == DamageType.Bludgeoning || o.DamageType == DamageType.Piercing || o.DamageType == DamageType.Slashing))
            {
                var rules = Stats(target).Rules;
                var reduce = Stats(target).Modifier(Ability.Dexterity) + (rules?.Level ?? 0);
                _aiExpectedDamage = amount;
                if (Ask(new ReactionOpportunity
                {
                    Kind = ReactionKind.DeflectAttacks, Name = "Deflect Attacks", ReactorId = target.CombatantId, TriggerId = attacker.CombatantId, TargetId = target.CombatantId,
                    Trigger = $"{attacker.Name}'s {o.Name} hits {target.Name} for {amount} {o.DamageType.ToString().ToLowerInvariant()} damage.", Cost = $"{target.Name}'s reaction",
                    Consequence = $"Reduce it by 1d10 + {reduce}.",
                }))
                {
                    target.Turn.ReactionUsed = true;
                    var d = Die(10);
                    var after = Math.Max(0, amount - d - reduce);
                    r.DamageBreakdown.Note($"Deflect Attacks −{amount - after}");
                    Log(CombatLogKind.Action, target.CombatantId, attacker.CombatantId, $"{target.Name} deflects the blow: 1d10 [{d}] + {reduce} — {amount} damage becomes {after}.", d);
                    amount = after;
                }
            }

            if (amount > 0 && TargetFeature(target, "uncanny_dodge") && CanReact(target) && !Has(target, ConditionType.Blinded))
            {
                _aiExpectedDamage = amount;
                if (Ask(new ReactionOpportunity
                {
                    Kind = ReactionKind.UncannyDodge, Name = "Uncanny Dodge", ReactorId = target.CombatantId, TriggerId = attacker.CombatantId, TargetId = target.CombatantId,
                    Trigger = $"{attacker.Name}'s {o.Name} hits {target.Name} for {amount} damage.", Cost = $"{target.Name}'s reaction",
                    Consequence = $"Halve it to {amount / 2}.",
                }))
                {
                    target.Turn.ReactionUsed = true;
                    amount /= 2;
                    r.DamageBreakdown.Note("Uncanny Dodge halves it");
                    Log(CombatLogKind.Action, target.CombatantId, attacker.CombatantId, $"{target.Name} uses Uncanny Dodge: the damage is halved to {amount}.");
                }
            }

            return amount;
        }

        /// <summary>After damage from a creature it can see within 60 ft: a warlock's Hellish Rebuke (Dexterity save, 2d10 fire, half on a success).</summary>
        private void ReactToBeingHurt(CombatantData attacker, CombatantData target)
        {
            if (target.State != VitalState.Conscious || attacker.State != VitalState.Conscious || !Hostile(target, attacker) || target.Position.DistanceFeet(attacker.Position) > 60) return;
            if (!HasReadySpell(target, "hellish_rebuke", out var slot)) return;
            var dice = 2 + (slot - 1);
            if (!Ask(new ReactionOpportunity
            {
                Kind = ReactionKind.HellishRebuke, Name = "Hellish Rebuke", ReactorId = target.CombatantId, TriggerId = attacker.CombatantId, TargetId = attacker.CombatantId,
                Trigger = $"{attacker.Name} hurt {target.Name}.", Cost = $"{target.Name}'s reaction and a level {slot} spell slot",
                Consequence = $"{attacker.Name} makes a Dexterity save against DC {SpellSaveDc(target)}: {dice}d10 fire, half on a success.",
            })) return;
            target.Turn.ReactionUsed = true;
            SpendSlot(target, slot);
            var s = SavingThrow(attacker, Ability.Dexterity, SpellSaveDc(target), target.Name + "'s Hellish Rebuke");
            var total = 0;
            for (var i = 0; i < dice; i++) total += Die(10);
            Log(CombatLogKind.Spell, target.CombatantId, attacker.CombatantId, $"{target.Name} casts Hellish Rebuke (a level {slot} slot): {attacker.Name} {(s.Success ? "saves" : "fails")} — {(s.Success ? total / 2 : total)} fire.");
            ApplyDamage(attacker, s.Success ? total / 2 : total, DamageType.Fire, false, target.Name);
        }

        /// <summary>A hostile caster within 60 ft starts a spell: a foe's Counterspell. True when the spell fails (the caster's slot is kept, its action wasted).</summary>
        private bool Counterspelled(CombatantData caster, SpellDefinition s, int level)
        {
            foreach (var foe in Encounter.Combatants)
            {
                if (foe == caster || !Hostile(foe, caster) || foe.Position.DistanceFeet(caster.Position) > 60) continue;
                if (!HasReadySpell(foe, "counterspell", out var slot)) continue;
                _aiSpellLevel = s.IsCantrip ? 0 : level;
                if (!Ask(new ReactionOpportunity
                {
                    Kind = ReactionKind.Counterspell, Name = "Counterspell", ReactorId = foe.CombatantId, TriggerId = caster.CombatantId, TargetId = caster.CombatantId,
                    Trigger = $"{caster.Name} begins to cast {s.Name}{(s.IsCantrip ? string.Empty : $" (level {level})")}.", Cost = $"{foe.Name}'s reaction and a level {slot} spell slot",
                    Consequence = $"{caster.Name} makes a Constitution save against DC {SpellSaveDc(foe)}; on a failure the spell fails, its slot unspent.",
                })) continue;
                foe.Turn.ReactionUsed = true;
                SpendSlot(foe, slot);
                var save = SavingThrow(caster, Ability.Constitution, SpellSaveDc(foe), foe.Name + "'s Counterspell");
                Log(CombatLogKind.Spell, foe.CombatantId, caster.CombatantId,
                    $"{foe.Name} casts Counterspell (a level {slot} slot): {caster.Name} {(save.Success ? "holds the spell together" : $"loses {s.Name} — the action is wasted, the slot kept")}.");
                if (!save.Success) return true;
            }

            return false;
        }

        /// <summary>A failed save: the roller's own decisions — Bardic Inspiration (add the die), then Indomitable (reroll with the fighter level).</summary>
        private void DecideOnFailedSave(CombatantData c, Ability ability, int dc, SaveResult r, int rerollBonus, List<string> notes, int aura)
        {
            var e = FirstEffect(c, CombatEffectKind.InspirationDie);
            if (!r.Success && e != null && r.Total + e.DiceSides >= dc && Ask(new ReactionOpportunity
            {
                Kind = ReactionKind.BardicInspiration, Name = "Bardic Inspiration", ReactorId = c.CombatantId, TargetId = c.CombatantId,
                Trigger = $"{c.Name}'s {ability} save: {r.Total} against DC {dc} — a failure.", Cost = "the Bardic Inspiration die",
                Consequence = $"Add 1d{e.DiceSides}: a {dc - r.Total} or more succeeds.",
            }))
            {
                var d = Die(e.DiceSides);
                Encounter.Effects.Remove(e);
                r.Total += d;
                r.Success = r.Total >= dc;
                notes.Add($"Bardic Inspiration +{d}");
            }

            if (!r.Success && TargetFeature(c, "indomitable") && ResourceLeft(c, "indomitable") > 0 && Ask(new ReactionOpportunity
            {
                Kind = ReactionKind.Indomitable, Name = "Indomitable", ReactorId = c.CombatantId, TargetId = c.CombatantId,
                Trigger = $"{c.Name}'s {ability} save: {r.Total} against DC {dc} — a failure.", Cost = "one use of Indomitable",
                Consequence = $"Reroll the save, adding {Stats(c).Rules.Level} (the fighter level).",
            }))
            {
                SpendResource(c, "indomitable", 1);
                var again = Die(20);
                var level = Stats(c).Rules.Level;
                r.Total = again + rerollBonus + level + aura;
                r.Success = r.Total >= dc;
                notes.Add($"Indomitable: rerolled {again} +{level}");
            }
        }

        /// <summary>A failed attack roll: the attacker's held Bardic Inspiration die, if they choose.</summary>
        private void DecideOnMissedAttack(CombatantData attacker, AttackResult r, List<string> notes)
        {
            var e = FirstEffect(attacker, CombatEffectKind.InspirationDie);
            if (r.Hit || e == null || r.Total + e.DiceSides < r.TargetAc) return;
            if (!Ask(new ReactionOpportunity
            {
                Kind = ReactionKind.BardicInspiration, Name = "Bardic Inspiration", ReactorId = attacker.CombatantId, TargetId = attacker.CombatantId,
                Trigger = $"{attacker.Name}'s attack: {r.Total} against AC {r.TargetAc} — a miss.", Cost = "the Bardic Inspiration die",
                Consequence = $"Add 1d{e.DiceSides}: a {r.TargetAc - r.Total} or more hits.",
            })) return;
            var d = Die(e.DiceSides);
            Encounter.Effects.Remove(e);
            r.Total += d;
            r.Hit = r.Total >= r.TargetAc;
            notes.Add($"Bardic Inspiration +{d}");
            if (!r.Hit) r.MissReason = $"{r.Total} is below AC {r.TargetAc}";
        }
    }
}
