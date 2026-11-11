using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// The combat rules (Phase 8; SRD 5.2.1 as configured in Documentation/Systems/Combat.md), applied to one encounter
    /// in a world. Plain C#: no Unity, no clock. Every method validates before it changes anything; the Application
    /// commands run it on a working copy that is validated whole before commit, so a refused or failing action
    /// leaves the world untouched. All dice come from the encounter's seed and roll counter (reproducible after a
    /// save). The player's hit points and death saves are read and written on their character sheet through the
    /// Phase 7 vitals rules; everyone else's live on the combatant.
    /// </summary>
    public sealed partial class CombatEngine
    {
        public const string Neutral = "neutral";

        private readonly WorldData _world;
        private readonly Dictionary<CombatantId, CombatStats> _stats = new Dictionary<CombatantId, CombatStats>();

        public CombatEngine(WorldData world, EncounterData encounter)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        }

        public EncounterData Encounter { get; }
        public WorldData World => _world;

        /// <summary>Chronicle moments for the command to report (start, end, downed, killed).</summary>
        public List<CombatChronicleNote> Chronicle { get; } = new List<CombatChronicleNote>();

        /// <summary>Named residents killed (the command applies the shared death routine).</summary>
        public List<NpcId> ResidentDeaths { get; } = new List<NpcId>();

        private ActionOutcome _current;

        public CombatStats Stats(CombatantData c)
        {
            if (!_stats.TryGetValue(c.CombatantId, out var s)) _stats[c.CombatantId] = s = CombatStats.For(_world, c);
            return s;
        }

        // ===== Creation and initiative =====================================================================

        /// <summary>Builds an encounter from a setup (not started), or null with the reasons.</summary>
        public static EncounterData Create(WorldData world, EncounterSetup setup, Func<string, Guid> nextGuid, out List<string> errors)
        {
            errors = new List<string>();
            if (setup == null || setup.Participants.Count < 2) { errors.Add("An encounter needs at least two participants."); return null; }
            foreach (var e in world.Encounters) if (e.IsActive) { errors.Add("Another encounter is still going on."); return null; }
            var enc = new EncounterData
            {
                Name = string.IsNullOrWhiteSpace(setup.Name) ? "Encounter" : setup.Name.Trim(),
                Kind = setup.Kind, Lethality = setup.Lethality, SettlementId = setup.SettlementId, Status = EncounterStatus.Active,
                StartedOn = world.CurrentSimulationDate, StartedAtMinute = world.MinuteOfDay, QuestRef = setup.QuestRef, WorldEventRef = setup.WorldEventRef,
            };
            enc.EncounterId = new EncounterId(nextGuid("encounter"));
            enc.Seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "encounter:" + enc.EncounterId);
            enc.DifficultTerrain.AddRange(setup.DifficultTerrain);
            foreach (var p in setup.Participants)
            {
                var c = BuildCombatant(world, enc, p, nextGuid, errors);
                if (c != null) enc.Combatants.Add(c);
            }

            var hostile = false;
            foreach (var a in enc.Combatants)
                foreach (var b in enc.Combatants)
                    if (Hostile(a, b)) hostile = true;
            if (!hostile) errors.Add("An encounter needs two sides that are hostile to each other.");
            return errors.Count == 0 ? enc : null;
        }

        /// <summary>
        /// A combatant for a participant, checked against those already in the encounter: one player at most, a resident
        /// at most once (their identity is the NPC's, never a copy), no two on one square. Null with the reason otherwise.
        /// </summary>
        private static CombatantData BuildCombatant(WorldData world, EncounterData enc, ParticipantSetup p, Func<string, Guid> nextGuid, List<string> errors)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Side)) { errors.Add("Every participant needs a side."); return null; }
            foreach (var x in enc.Combatants)
            {
                if (x.Position.Equals(p.Position) && x.State != VitalState.Dead) { errors.Add($"Two participants stand on {p.Position}."); return null; }
                if (p.Kind == CombatantKind.Player && x.Kind == CombatantKind.Player) { errors.Add("The player can take part only once."); return null; }
                if (p.Kind == CombatantKind.Npc && x.Kind == CombatantKind.Npc && x.SourceId == p.NpcId.ToString()) { errors.Add($"{x.Name} is already taking part."); return null; }
            }

            var c = new CombatantData { Kind = p.Kind, Side = p.Side.Trim(), Position = p.Position, Control = p.Control, JoinedRound = Math.Max(1, enc.Round) };
            switch (p.Kind)
            {
                case CombatantKind.Player:
                    var player = world.Player;
                    if (player?.Rpg == null || player.Rpg.State != CreationState.Complete) { errors.Add("Create your character before fighting."); return null; }
                    if (player.Rpg.IsDead) { errors.Add($"{player.Name} is dead."); return null; }
                    c.SourceId = player.CharacterId.ToString();
                    c.Name = player.Name;
                    c.UsesDeathSaves = true;
                    c.Control = CombatControl.Player;
                    c.State = player.Rpg.CurrentHp > 0 ? VitalState.Conscious : player.Rpg.IsStable ? VitalState.Stable : VitalState.Dying;
                    break;
                case CombatantKind.Npc:
                    var npc = world.FindNpc(p.NpcId);
                    if (npc == null || npc.IsDeceased) { errors.Add("That resident cannot take part."); return null; }
                    c.SourceId = npc.NpcId.ToString();
                    c.Name = npc.Name;
                    c.FactionId = npc.FactionId;
                    c.CreatureId = p.CreatureId ?? CreatureCatalog.ProfileFor(npc.Role);
                    c.UsesDeathSaves = true; // named people are characters: they fall unconscious and fight for their lives
                    break;
                default:
                    var def = CreatureCatalog.Find(p.CreatureId);
                    if (def == null) { errors.Add($"Unknown creature '{p.CreatureId}'."); return null; }
                    c.SourceId = def.Id;
                    c.CreatureId = def.Id;
                    c.Name = string.IsNullOrWhiteSpace(p.Name) ? def.Name : p.Name.Trim();
                    break;
            }

            if (c.Kind != CombatantKind.Player)
            {
                var def = CreatureCatalog.Find(c.CreatureId);
                if (def == null) { errors.Add($"Unknown creature profile '{c.CreatureId}'."); return null; }
                c.MaxHp = def.MaxHp;
                c.CurrentHp = def.MaxHp;
                if (def.Spellcasting != null) // Phase 9: a caster profile's slots, on the combatant
                    for (var l = 0; l < def.Spellcasting.Slots.Count; l++) c.Resources["spell_slots_" + (l + 1)] = def.Spellcasting.Slots[l];
            }

            c.CombatantId = new CombatantId(nextGuid("combatant"));
            return c;
        }

        /// <summary>Rolls initiative, orders the combatants and starts the first turn.</summary>
        public void Begin()
        {
            var e = Encounter;
            e.Round = 1;
            Log(CombatLogKind.Started, default, default, $"{e.Name} begins ({e.Lethality.ToString().ToLowerInvariant()}).");
            foreach (var c in e.Combatants) RollInitiative(c);
            e.Order.Clear();
            var ordered = new List<CombatantData>(e.Combatants);
            ordered.Sort(CompareInitiative);
            foreach (var c in ordered) e.Order.Add(c.CombatantId);
            var names = new List<string>();
            foreach (var c in ordered) names.Add($"{c.Name} {c.Initiative}");
            Log(CombatLogKind.Initiative, default, default, "Initiative: " + string.Join(", ", names) + ".");
            var sides = new List<string>();
            foreach (var c in e.Combatants) if (!sides.Contains(c.Side)) sides.Add(c.Side);
            Note(HistoricalEventType.EncounterStarted, default, ("name", e.Name), ("kind", e.Kind.ToString()), ("participants", e.Combatants.Count.ToString(CultureInfo.InvariantCulture)),
                ("sides", string.Join(", ", sides)));
            e.TurnIndex = -1;
            AdvanceTurn();
        }

        /// <summary>Adds a combatant to a running encounter (reinforcements): it rolls initiative and takes its place in the order.</summary>
        public string Join(ParticipantSetup p, Func<string, Guid> nextGuid)
        {
            var e = Encounter;
            if (!e.IsActive) return "The encounter is over.";
            var errors = new List<string>();
            var c = BuildCombatant(_world, e, p, nextGuid, errors);
            if (c == null) return string.Join(" ", errors);
            RollInitiative(c);
            e.Combatants.Add(c);
            var active = e.Active?.CombatantId;
            var index = e.Order.Count;
            for (var i = 0; i < e.Order.Count; i++)
                if (CompareInitiative(c, e.Find(e.Order[i])) < 0) { index = i; break; }
            e.Order.Insert(index, c.CombatantId);
            if (active.HasValue) e.TurnIndex = e.Order.IndexOf(active.Value);
            Log(CombatLogKind.Joined, c.CombatantId, default, $"{c.Name} joins the fight for {c.Side} (initiative {c.Initiative}).");
            return null;
        }

        private void RollInitiative(CombatantData c)
        {
            var s = Stats(c);
            var bonus = s.InitiativeBonus();
            var modes = s.RollModes("initiative");
            var roll = new D20Roll();
            roll.AdvantageSources.AddRange(modes.Advantage);
            roll.DisadvantageSources.AddRange(modes.Disadvantage);
            if (Has(c, ConditionType.Invisible)) roll.AdvantageSources.Add("invisible");
            if (Incapacitated(c)) roll.DisadvantageSources.Add("incapacitated");
            Roll(roll);
            c.InitiativeRoll = roll.Natural;
            c.Initiative = roll.Natural + bonus.Total - 2 * ExhaustionLevel(c);
            c.TieBreak = Die(1000);
        }

        /// <summary>Higher initiative first; ties by higher Dexterity modifier, then a seeded tie-break, then ID (deterministic).</summary>
        private int CompareInitiative(CombatantData a, CombatantData b)
        {
            var t = b.Initiative.CompareTo(a.Initiative);
            if (t != 0) return t;
            t = Stats(b).Modifier(Ability.Dexterity).CompareTo(Stats(a).Modifier(Ability.Dexterity));
            if (t != 0) return t;
            t = b.TieBreak.CompareTo(a.TieBreak);
            return t != 0 ? t : a.CombatantId.CompareTo(b.CombatantId);
        }

        // ===== Turns =======================================================================================

        /// <summary>Ends the active turn and starts the next combatant able to take one; a new round when the order wraps.</summary>
        private void AdvanceTurn()
        {
            var e = Encounter;
            if (e.TurnIndex >= 0 && e.Active != null) EndTurn(e.Active);
            for (var guard = 0; guard < e.Order.Count * 3 + 3 && e.IsActive; guard++)
            {
                e.TurnIndex++;
                if (e.TurnIndex >= e.Order.Count)
                {
                    e.TurnIndex = 0;
                    e.Round++;
                }

                var c = e.Active;
                if (c == null || c.State == VitalState.Dead || c.State == VitalState.Fled) continue;
                StartTurn(c);
                if (CheckEnd()) return;
                if (c.State == VitalState.Conscious) return;
                EndTurn(c); // dying or stable: the turn passes after its death save
            }

            if (e.IsActive) Finish(EncounterOutcome.Stalemate, null);
        }

        private void StartTurn(CombatantData c)
        {
            c.Turn = new TurnResources(); // every per-turn resource and flag resets
            Expire(ConditionDuration.StartOfTurn, c);
            ExpireEffects(EffectExpiry.StartOfTurn, c);
            Log(CombatLogKind.TurnStarted, c.CombatantId, default, $"Round {Encounter.Round}: {c.Name}'s turn.");
            if (c.State == VitalState.Dying) DeathSave(c);
        }

        private void Expire(ConditionDuration when, CombatantData owner)
        {
            foreach (var x in Encounter.Combatants)
                for (var i = x.Conditions.Count - 1; i >= 0; i--)
                {
                    var k = x.Conditions[i];
                    if (k.Duration == when && k.ExpiresOn == owner.CombatantId && Encounter.Round >= k.ExpiresRound)
                        RemoveCondition(x, k.Type, "its time ran out");
                }
        }

        // ===== Acting ======================================================================================

        /// <summary>Validates and resolves one action of the active combatant.</summary>
        public ActionOutcome Act(CombatantId actorId, CombatAction action)
        {
            var e = Encounter;
            if (!e.IsActive) return ActionOutcome.Refused("The encounter is over.");
            var actor = e.Find(actorId);
            if (actor == null) return ActionOutcome.Refused("That combatant is not in this encounter.");
            if (e.Active != actor) return ActionOutcome.Refused($"It is {e.Active?.Name ?? "nobody"}'s turn, not {actor.Name}'s.");
            if (actor.State != VitalState.Conscious) return ActionOutcome.Refused($"{actor.Name} cannot act ({actor.State.ToString().ToLowerInvariant()}).");
            var why = Unavailable(actor, action);
            if (why != null) return ActionOutcome.Refused(why);

            _current = new ActionOutcome { Ok = true };
            switch (action)
            {
                case MoveAction m: DoMove(actor, m); break;
                case AttackAction a: DoAttack(actor, a); break;
                case DashAction _:
                    UseAction(actor);
                    actor.Turn.MovementGranted += Speed(actor);
                    Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} dashes (+{Speed(actor)} ft of movement).");
                    break;
                case DisengageAction _:
                    UseAction(actor);
                    actor.Turn.Disengaged = true;
                    Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} disengages: no opportunity attacks this turn.");
                    break;
                case DodgeAction _:
                    UseAction(actor);
                    actor.Turn.Dodging = true;
                    Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} dodges: attacks against it have disadvantage until its next turn.");
                    break;
                case StandAction _:
                    var cost = Speed(actor) / 2;
                    actor.Turn.MovementUsed += cost;
                    RemoveCondition(actor, ConditionType.Prone, "stood up");
                    Log(CombatLogKind.Moved, actor.CombatantId, default, $"{actor.Name} stands up ({cost} ft of movement).");
                    break;
                case EscapeAction _: DoEscape(actor); break;
                case StabilizeAction s: DoStabilize(actor, e.Find(s.Target)); break;
                case BonusAbilityAction b: DoFeature(actor, new UseFeatureAction(b.AbilityId)); break;
                case UseFeatureAction f: DoFeature(actor, f); break;
                case CastSpellAction cs: DoCastSpell(actor, cs); break;
                case EndTurnAction _:
                    Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} ends their turn.");
                    AdvanceTurn();
                    break;
            }

            e.ActionSequence++;
            CheckEnd();
            var outcome = _current;
            _current = null;
            return outcome;
        }

        /// <summary>Why an action is not allowed now (null when it is). Shared by the engine, the AI and the UI.</summary>
        public string Unavailable(CombatantData actor, CombatAction action)
        {
            var t = actor.Turn;
            var incapacitated = Incapacitated(actor);
            switch (action)
            {
                case EndTurnAction _: return null;
                case MoveAction m: return MoveProblem(actor, m.Path, out _);
                case AttackAction a:
                {
                    var o = Stats(actor).Find(a.OptionId);
                    if (o == null) return "That attack is not available.";
                    if (incapacitated) return $"{actor.Name} is incapacitated.";
                    if (t.ActionUsed && t.AttacksLeft <= 0) return "The action this turn is used.";
                    var target = Encounter.Find(a.Target);
                    if (target == null || target == actor) return "Choose a target.";
                    if (target.State == VitalState.Dead || target.State == VitalState.Fled) return $"{target.Name} is out of the fight.";
                    foreach (var k in actor.Conditions)
                        if (k.Type == ConditionType.Charmed && k.SourceId == target.CombatantId) return $"{actor.Name} is charmed by {target.Name} and cannot attack them.";
                    var d = actor.Position.DistanceFeet(target.Position);
                    if (o.Melee && d > o.Reach) return $"{target.Name} is {d} ft away; {o.Name} reaches {o.Reach} ft.";
                    if (!o.Melee && d > o.LongRange) return $"{target.Name} is {d} ft away; {o.Name} reaches at most {o.LongRange} ft.";
                    if (o.Kind == AttackKind.Grapple && Has(target, ConditionType.Grappled)) return $"{target.Name} is already grappled.";
                    return null;
                }

                case DashAction _:
                case DisengageAction _:
                case DodgeAction _:
                case EscapeAction _:
                case StabilizeAction _:
                {
                    if (incapacitated) return $"{actor.Name} is incapacitated.";
                    if (t.ActionUsed) return "The action this turn is used.";
                    if (action is EscapeAction && !Has(actor, ConditionType.Grappled) && EscapableRestraint(actor) == null) return $"{actor.Name} is not grappled or held by something it can break free of.";
                    if (action is StabilizeAction st)
                    {
                        var target = Encounter.Find(st.Target);
                        if (target == null || target.State != VitalState.Dying) return "Only a dying creature can be stabilized.";
                        if (!actor.Position.IsAdjacent(target.Position)) return $"{target.Name} is out of reach.";
                    }

                    return null;
                }

                case StandAction _:
                    if (!Has(actor, ConditionType.Prone)) return $"{actor.Name} is not prone.";
                    if (Speed(actor) == 0) return "Standing needs movement, and speed is 0.";
                    return MovementLeft(actor) < Speed(actor) / 2 ? $"Standing costs {Speed(actor) / 2} ft; {MovementLeft(actor)} ft left." : null;

                case BonusAbilityAction b: return FeatureProblem(actor, new UseFeatureAction(b.AbilityId));
                case UseFeatureAction f: return FeatureProblem(actor, f);
                case CastSpellAction cs: return SpellProblem(actor, cs);
            }

            return "Unknown action.";
        }

        // ===== Movement ====================================================================================

        public int ExhaustionLevel(CombatantData c)
        {
            foreach (var k in c.Conditions) if (k.Type == ConditionType.Exhaustion) return k.Level;
            return 0;
        }

        /// <summary>Speed after conditions: 0 when grappled, restrained, paralyzed, petrified or unconscious; −5 ft per exhaustion level.</summary>
        public int Speed(CombatantData c)
        {
            if (c.State != VitalState.Conscious) return 0;
            if (Has(c, ConditionType.Grappled) || Has(c, ConditionType.Restrained) || Has(c, ConditionType.Paralyzed) || Has(c, ConditionType.Petrified) || Has(c, ConditionType.Unconscious)) return 0;
            var speed = Stats(c).BaseSpeed - 5 * ExhaustionLevel(c);
            var multiplier = 1;
            foreach (var e in EffectsOn(c))
            {
                if (e.Kind == Realmkeeper.Domain.Magic.CombatEffectKind.SpeedChange) speed += e.Value;
                if (e.Kind == Realmkeeper.Domain.Magic.CombatEffectKind.SpeedMultiplier) multiplier = Math.Max(multiplier, e.Value);
            }

            return Math.Max(0, speed * multiplier); // Phase 9: Ray of Frost, Slow, Haste
        }

        public int MovementLeft(CombatantData c) => Math.Max(0, Speed(c) + c.Turn.MovementGranted - c.Turn.MovementUsed);

        /// <summary>The cost in feet of stepping into a square: 5, plus 5 for difficult terrain and 5 for crawling while prone.</summary>
        public int StepCost(CombatantData c, GridPos to)
        {
            var cost = GridPos.Feet;
            if (Encounter.DifficultTerrain.Contains(to)) cost += GridPos.Feet;
            if (Has(c, ConditionType.Prone)) cost += GridPos.Feet;
            return cost;
        }

        public CombatantData At(GridPos p)
        {
            foreach (var c in Encounter.Combatants) if (c.Position.Equals(p) && c.State != VitalState.Dead && c.State != VitalState.Fled) return c;
            return null;
        }

        private string MoveProblem(CombatantData actor, IReadOnlyList<GridPos> path, out int cost)
        {
            cost = 0;
            if (path == null || path.Count == 0) return "Choose where to move.";
            if (Speed(actor) == 0) return $"{actor.Name} cannot move (speed 0).";
            var from = actor.Position;
            CombatantData fear = null;
            foreach (var k in actor.Conditions)
                if (k.Type == ConditionType.Frightened && !k.SourceId.IsEmpty) fear = Encounter.Find(k.SourceId);
            for (var i = 0; i < path.Count; i++)
            {
                var to = path[i];
                if (!from.IsAdjacent(to)) return $"Step {i + 1} to {to} is not next to {from}.";
                var there = At(to);
                if (there != null && there != actor && Hostile(actor, there) && there.State == VitalState.Conscious) return $"{there.Name} blocks {to}.";
                if (fear != null && fear.State != VitalState.Dead && to.DistanceFeet(fear.Position) < from.DistanceFeet(fear.Position))
                    return $"{actor.Name} is frightened of {fear.Name} and cannot move closer.";
                cost += StepCost(actor, to);
                from = to;
            }

            var end = At(path[path.Count - 1]);
            if (end != null && end != actor) return $"{path[path.Count - 1]} is occupied by {end.Name}.";
            return cost > MovementLeft(actor) ? $"That needs {cost} ft of movement; {MovementLeft(actor)} ft left." : null;
        }

        private void DoMove(CombatantData actor, MoveAction m)
        {
            var start = actor.Position;
            var moved = 0;
            foreach (var to in m.Path)
            {
                // Leaving an enemy's reach provokes an opportunity attack (unless disengaged), taken right before leaving.
                if (!actor.Turn.Disengaged)
                    foreach (var enemy in new List<CombatantData>(Encounter.Combatants))
                    {
                        if (!CanReact(enemy) || !Hostile(enemy, actor)) continue;
                        var reach = Stats(enemy).MeleeReach;
                        if (actor.Position.DistanceFeet(enemy.Position) <= reach && to.DistanceFeet(enemy.Position) > reach)
                        {
                            var o = Stats(enemy).BestMelee();
                            if (o == null) continue;
                            enemy.Turn.ReactionUsed = true;
                            Log(CombatLogKind.Action, enemy.CombatantId, actor.CombatantId, $"{enemy.Name} makes an opportunity attack as {actor.Name} leaves its reach.");
                            ResolveAttack(enemy, actor, o, true);
                            if (actor.State != VitalState.Conscious) break;
                        }
                    }

                if (actor.State != VitalState.Conscious)
                {
                    Log(CombatLogKind.Moved, actor.CombatantId, default, $"{actor.Name}'s movement ends at {actor.Position}: struck down.");
                    return;
                }

                var cost = StepCost(actor, to);
                actor.Turn.MovementUsed += cost;
                moved += cost;
                actor.Position = to;
            }

            Log(CombatLogKind.Moved, actor.CombatantId, default, $"{actor.Name} moves {start} → {actor.Position} ({moved} ft; {MovementLeft(actor)} ft left).");
            ReleaseBrokenGrapples();
        }

        /// <summary>Can make a reaction now: conscious, not incapacitated, reaction unused, and able to see.</summary>
        public bool CanReact(CombatantData c) => c.State == VitalState.Conscious && !Incapacitated(c) && !c.Turn.ReactionUsed && !Has(c, ConditionType.Blinded) && !HasEffect(c, Realmkeeper.Domain.Magic.CombatEffectKind.NoReactions);

        private void ReleaseBrokenGrapples()
        {
            foreach (var c in Encounter.Combatants)
                for (var i = c.Conditions.Count - 1; i >= 0; i--)
                {
                    var k = c.Conditions[i];
                    if (k.Type != ConditionType.Grappled) continue;
                    var g = Encounter.Find(k.SourceId);
                    if (g == null || g.State != VitalState.Conscious || Incapacitated(g) || !g.Position.IsAdjacent(c.Position))
                        RemoveCondition(c, ConditionType.Grappled, "the grappler let go");
                }
        }

        // ===== Damage, healing, death ======================================================================

        public int Hp(CombatantData c) => c.Kind == CombatantKind.Player ? _world.Player.Rpg.CurrentHp : c.CurrentHp;
        public int TempHp(CombatantData c) => c.Kind == CombatantKind.Player ? _world.Player.Rpg.TemporaryHp : c.TemporaryHp;
        public int MaxHp(CombatantData c) => Stats(c).MaxHp;
        public int Successes(CombatantData c) => c.Kind == CombatantKind.Player ? _world.Player.Rpg.DeathSaveSuccesses : c.DeathSaveSuccesses;
        public int Failures(CombatantData c) => c.Kind == CombatantKind.Player ? _world.Player.Rpg.DeathSaveFailures : c.DeathSaveFailures;

        private void SetDeathSaves(CombatantData c, int successes, int failures)
        {
            if (c.Kind == CombatantKind.Player)
            {
                _world.Player.Rpg.DeathSaveSuccesses = successes;
                _world.Player.Rpg.DeathSaveFailures = failures;
            }
            else
            {
                c.DeathSaveSuccesses = successes;
                c.DeathSaveFailures = failures;
            }
        }

        private void SetState(CombatantData c, VitalState state)
        {
            c.State = state;
            if (c.Kind != CombatantKind.Player) return;
            var s = _world.Player.Rpg;
            s.IsStable = state == VitalState.Stable;
            s.IsDead = state == VitalState.Dead;
            if (state == VitalState.Conscious || state == VitalState.Dead) s.DeathSaveSuccesses = s.DeathSaveFailures = 0;
        }

        /// <summary>
        /// Applies damage after defences — immunity (none), resistance (halved, rounded down), vulnerability (doubled), each
        /// once — then temporary hit points first. At 0 hit points: characters (the player, named residents) fall
        /// unconscious and start dying, unless the damage left over is at least their hit point maximum (killed
        /// outright); creatures die; in a non-lethal fight everyone is knocked out and stable instead. Damage to someone
        /// already at 0 costs a death save (two on a critical), or kills when it reaches their maximum (lethal fights only).
        /// </summary>
        public DamageResult ApplyDamage(CombatantData target, int amount, DamageType type, bool critical, string byName)
        {
            var s = Stats(target);
            var r = new DamageResult { Rolled = amount, Type = type, HpBefore = Hp(target) };
            var final = Math.Max(0, amount);
            if (s.Immune(type)) { final = 0; r.Adjustments.Add("immune"); }
            else
            {
                if (s.Resists(type) || Has(target, ConditionType.Petrified) || RageResists(target, type)) { final /= 2; r.Adjustments.Add("resistance (halved)"); }
                if (s.Vulnerable(type)) { final *= 2; r.Adjustments.Add("vulnerability (doubled)"); }
            }

            r.Final = final;
            var name = type.ToString().ToLowerInvariant();
            if (target.State == VitalState.Dead) return r;
            if (target.State == VitalState.Dying || target.State == VitalState.Stable)
            {
                if (final <= 0 || Encounter.Lethality == Lethality.NonLethal) return r; // a non-lethal fight never kills
                if (final >= MaxHp(target)) { Kill(target, $"{final} {name} damage at 0 hit points", byName); r.Killed = true; return r; }
                if (Encounter.Lethality == Lethality.NonLethal || !target.UsesDeathSaves) return r;
                var add = critical ? 2 : 1;
                r.DeathSaveFailuresAdded = add;
                SetState(target, VitalState.Dying);
                SetDeathSaves(target, Successes(target), Math.Min(3, Failures(target) + add));
                Log(CombatLogKind.DeathSave, target.CombatantId, default, $"{target.Name} takes {final} {name} damage while down: {add} death save failure{(add > 1 ? "s" : string.Empty)} ({Failures(target)}/3).", final);
                if (Failures(target) >= 3) { Kill(target, "three failed death saves", byName); r.Killed = true; }
                return r;
            }

            int absorbed;
            if (target.Kind == CombatantKind.Player)
            {
                var sheet = _world.Player.Rpg;
                var before = sheet.TemporaryHp;
                if (final > 0) CharacterVitals.ChangeHitPoints(sheet, -final); // the Phase 7 rule: temporary hit points first, stop at 0
                absorbed = before - sheet.TemporaryHp;
            }
            else
            {
                absorbed = Math.Min(target.TemporaryHp, final);
                target.TemporaryHp -= absorbed;
                target.CurrentHp = Math.Max(0, target.CurrentHp - (final - absorbed));
            }

            r.TemporaryAbsorbed = absorbed;
            r.HpAfter = Hp(target);
            r.HitPointsLost = r.HpBefore - r.HpAfter;
            Log(CombatLogKind.Damage, target.CombatantId, default,
                $"{target.Name} takes {final} {name} damage{(r.Adjustments.Count > 0 ? " (" + string.Join(", ", r.Adjustments) + $", from {amount})" : string.Empty)}" +
                (absorbed > 0 ? $"; {absorbed} absorbed by temporary hit points" : string.Empty) + $": {r.HpBefore} → {r.HpAfter} HP.", final);
            if (r.HpAfter > 0)
            {
                ConcentrationCheck(target, final); // Phase 9
                return r;
            }

            EndConcentration(target, "dropped to 0 hit points"); // Phase 9: concentration, rage and Wild Shape end
            EndOwnEffects(target, CombatEffectKind.Raging, "dropped to 0 hit points");
            EndOwnEffects(target, CombatEffectKind.WildShapeForm, "dropped to 0 hit points");

            var overflow = final - absorbed - r.HpBefore;
            if (overflow >= MaxHp(target) && Encounter.Lethality == Lethality.Lethal)
            {
                Kill(target, $"massive damage ({overflow} beyond 0)", byName);
                r.Killed = true;
                return r;
            }

            r.Downed = true;
            if (Encounter.Lethality == Lethality.NonLethal)
            {
                SetState(target, VitalState.Stable);
                MarkUnconscious(target, "knocked out");
                Log(CombatLogKind.Downed, target.CombatantId, default, $"{target.Name} is knocked out (non-lethal fight): unconscious and stable.");
            }
            else if (target.UsesDeathSaves)
            {
                SetState(target, VitalState.Dying);
                SetDeathSaves(target, 0, 0);
                MarkUnconscious(target, "dropped to 0 hit points");
                Log(CombatLogKind.Downed, target.CombatantId, default, $"{target.Name} falls unconscious and is dying.");
            }
            else
            {
                Kill(target, "dropped to 0 hit points", byName);
                r.Killed = true;
                return r;
            }

            if (target.Kind != CombatantKind.Creature)
                Note(HistoricalEventType.CombatantDowned, target.Kind == CombatantKind.Npc ? ParseNpc(target) : default, ("name", target.Name), ("by", byName ?? string.Empty));
            return r;
        }

        private void MarkUnconscious(CombatantData c, string reason)
        {
            ApplyCondition(c, ConditionType.Unconscious, default, reason, ConditionDuration.UntilRemoved, default, 0);
            ApplyCondition(c, ConditionType.Prone, default, reason, ConditionDuration.UntilRemoved, default, 0);
        }

        private void Kill(CombatantData c, string how, string byName)
        {
            SetState(c, VitalState.Dead);
            EndConcentration(c, "died"); // Phase 9: the dead sustain nothing, and nothing lingers on them
            foreach (var e in new List<ActiveEffectData>(EffectsOn(c))) RemoveEffect(e, "the creature died");
            if (c.Kind == CombatantKind.Player) _world.Player.Rpg.CurrentHp = 0;
            else c.CurrentHp = 0;
            c.Conditions.Clear();
            Log(CombatLogKind.Died, c.CombatantId, default, $"{c.Name} dies ({how}).");
            if (c.Kind == CombatantKind.Player)
                Note(HistoricalEventType.CombatantKilled, default, ("name", c.Name), ("by", byName ?? string.Empty), ("cause", how));
            if (c.Kind == CombatantKind.Npc) ResidentDeaths.Add(ParseNpc(c));
        }

        private static NpcId ParseNpc(CombatantData c) => NpcId.TryParse(c.SourceId, out var id) ? id : default;

        /// <summary>Heals: up to the maximum; someone at 0 hit points regains consciousness and stops dying. The dead cannot be healed.</summary>
        public string Heal(CombatantData c, int amount, string source)
        {
            if (amount < 1) return "Heal at least 1 hit point.";
            if (c.State == VitalState.Dead) return $"{c.Name} is dead; healing cannot help.";
            if (c.State == VitalState.Fled) return $"{c.Name} has left the fight.";
            var before = Hp(c);
            if (c.Kind == CombatantKind.Player) CharacterVitals.ChangeHitPoints(_world.Player.Rpg, amount);
            else c.CurrentHp = Math.Min(c.MaxHp, c.CurrentHp + amount);
            Log(CombatLogKind.Healing, c.CombatantId, default, $"{c.Name} regains {Hp(c) - before} hit points from {source}: {before} → {Hp(c)} HP.", Hp(c) - before);
            if (before == 0 && Hp(c) > 0)
            {
                SetState(c, VitalState.Conscious);
                SetDeathSaves(c, 0, 0);
                RemoveCondition(c, ConditionType.Unconscious, "regained hit points");
            }

            return null;
        }

        /// <summary>A death saving throw: d20 (exhaustion applies), 10+ succeeds; a natural 20 regains 1 hit point; a natural 1 counts twice.</summary>
        private void DeathSave(CombatantData c)
        {
            var d = Die(20);
            var total = d - 2 * ExhaustionLevel(c);
            int s = Successes(c), f = Failures(c);
            string text;
            if (d == 20)
            {
                text = "natural 20: regains 1 hit point and wakes.";
                Log(CombatLogKind.DeathSave, c.CombatantId, default, $"{c.Name}'s death save: {text}", d, new D20Roll { First = d, Natural = d });
                Heal(c, 1, "a natural 20 on a death save");
                return;
            }

            if (d == 1) { f += 2; text = "natural 1: two failures"; }
            else if (total >= 10) { s += 1; text = "success"; }
            else { f += 1; text = "failure"; }
            SetDeathSaves(c, Math.Min(3, s), Math.Min(3, f));
            Log(CombatLogKind.DeathSave, c.CombatantId, default, $"{c.Name}'s death save: {d}{(total != d ? $" ({total})" : string.Empty)}, {text} ({Successes(c)} successes, {Failures(c)} failures).", total, new D20Roll { First = d, Natural = d });
            if (Failures(c) >= 3) Kill(c, "three failed death saves", null);
            else if (Successes(c) >= 3)
            {
                SetState(c, VitalState.Stable);
                SetDeathSaves(c, 0, 0);
                Log(CombatLogKind.Stabilized, c.CombatantId, default, $"{c.Name} is stable.");
            }
        }

        private void DoStabilize(CombatantData actor, CombatantData target)
        {
            UseAction(actor);
            var check = Check(actor, Ability.Wisdom, Skill.Medicine, 10, $"stabilizing {target.Name}");
            if (check.Success)
            {
                SetState(target, VitalState.Stable);
                SetDeathSaves(target, 0, 0);
                Log(CombatLogKind.Stabilized, actor.CombatantId, target.CombatantId, $"{actor.Name} stabilizes {target.Name} (Medicine {check.Total} vs DC 10).", check.Total, check.Roll);
            }
            else Log(CombatLogKind.Action, actor.CombatantId, target.CombatantId, $"{actor.Name} fails to stabilize {target.Name} (Medicine {check.Total} vs DC 10).", check.Total, check.Roll);
        }

        private void DoEscape(CombatantData actor)
        {
            UseAction(actor);
            var k = Find(actor, ConditionType.Grappled);
            if (k == null)
            {
                // Phase 9: tearing free of a spell's restraint (Entangle): a Strength (Athletics) check against its DC.
                var hold = EscapableRestraint(actor);
                var pull = Check(actor, Ability.Strength, Skill.Athletics, hold.EscapeDc, "breaking free");
                Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} strains against {(string.IsNullOrEmpty(hold.Reason) ? "the restraint" : hold.Reason)} (Athletics {pull.Total} vs DC {hold.EscapeDc}): {(pull.Success ? "free" : "still held")}.", pull.Total, pull.Roll);
                if (pull.Success) RemoveCondition(actor, hold.Type, "broke free");
                return;
            }

            var athletics = Check(actor, Ability.Strength, Skill.Athletics, k.EscapeDc, "escaping", dryRun: true);
            var acrobatics = Check(actor, Ability.Dexterity, Skill.Acrobatics, k.EscapeDc, "escaping", dryRun: true);
            var useStrength = athletics.Bonus.Total >= acrobatics.Bonus.Total;
            var check = useStrength ? Check(actor, Ability.Strength, Skill.Athletics, k.EscapeDc, "escaping") : Check(actor, Ability.Dexterity, Skill.Acrobatics, k.EscapeDc, "escaping");
            Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} tries to escape ({(useStrength ? "Athletics" : "Acrobatics")} {check.Total} vs DC {k.EscapeDc}): {(check.Success ? "free" : "still held")}.", check.Total, check.Roll);
            if (check.Success) RemoveCondition(actor, ConditionType.Grappled, "escaped");
        }


        /// <summary>
        /// A saving throw against a DC: the save bonus (proficiency included), roll modes from features and conditions,
        /// exhaustion (−2 per level), and automatic failures (Strength and Dexterity saves while paralyzed, petrified,
        /// stunned or unconscious).
        /// </summary>

        /// <summary>An ability check with a skill (the player's skill bonus; a creature's ability modifier).</summary>
        public SaveResult Check(CombatantData c, Ability ability, Skill skill, int dc, string purpose, bool dryRun = false)
        {
            var s = Stats(c);
            var bonus = s.Rules != null ? s.Rules.SkillBonus(skill) : new Breakdown().Add(ability.ToString(), s.Modifier(ability));
            var r = new SaveResult { Ability = ability, Skill = SkillRules.Name(skill), Dc = dc, Bonus = bonus };
            if (dryRun) return r;
            var roll = new D20Roll();
            var modes = s.RollModes("skill:" + skill, "check:" + ability);
            roll.AdvantageSources.AddRange(modes.Advantage);
            roll.DisadvantageSources.AddRange(modes.Disadvantage);
            if (Has(c, ConditionType.Poisoned)) roll.DisadvantageSources.Add("poisoned");
            if (Has(c, ConditionType.Frightened)) roll.DisadvantageSources.Add("frightened");
            Roll(roll);
            r.Roll = roll;
            r.Total = roll.Natural + bonus.Total - 2 * ExhaustionLevel(c);
            r.Success = r.Total >= dc;
            return r;
        }

        // ===== Conditions ==================================================================================

        public bool Has(CombatantData c, ConditionType t)
        {
            foreach (var k in c.Conditions) if (k.Type == t) return true;
            return false;
        }

        private static ConditionInstance Find(CombatantData c, ConditionType t)
        {
            foreach (var k in c.Conditions) if (k.Type == t) return k;
            return null;
        }

        /// <summary>Incapacitated: no actions, bonus actions or reactions (by itself, or paralyzed, petrified, stunned, unconscious; or down).</summary>
        public bool Incapacitated(CombatantData c) =>
            c.State != VitalState.Conscious || Has(c, ConditionType.Incapacitated) || Has(c, ConditionType.Paralyzed) || Has(c, ConditionType.Petrified) ||
            Has(c, ConditionType.Stunned) || Has(c, ConditionType.Unconscious);

        /// <summary>
        /// Applies a condition. Immunities are respected (a creature's, and a petrified creature's to poison). The same
        /// condition does not stack: re-applying it keeps one instance with the longer duration — except Exhaustion,
        /// whose levels add up (death at 6). Returns why it could not be applied, or null.
        /// </summary>
        public string ApplyCondition(CombatantData c, ConditionType t, CombatantId source, string reason, ConditionDuration duration, CombatantId expiresOn, int expiresRound, int level = 1, int escapeDc = 0)
        {
            if (c.State == VitalState.Dead) return $"{c.Name} is dead.";
            if (Stats(c).ImmuneTo(t) || (t == ConditionType.Poisoned && Has(c, ConditionType.Petrified)))
            {
                Log(CombatLogKind.ConditionApplied, c.CombatantId, default, $"{c.Name} is immune to being {t.ToString().ToLowerInvariant()}.");
                return $"{c.Name} is immune to {t}.";
            }

            var existing = Find(c, t);
            if (existing != null)
            {
                if (t == ConditionType.Exhaustion)
                {
                    existing.Level = Math.Min(6, existing.Level + Math.Max(1, level));
                    Log(CombatLogKind.ConditionApplied, c.CombatantId, source, $"{c.Name}'s exhaustion rises to level {existing.Level} ({reason}).", existing.Level);
                    if (existing.Level >= 6) Kill(c, "exhaustion", null);
                    return null;
                }

                if (existing.Duration != ConditionDuration.UntilRemoved && (duration == ConditionDuration.UntilRemoved || expiresRound > existing.ExpiresRound))
                {
                    existing.Duration = duration;
                    existing.ExpiresOn = expiresOn;
                    existing.ExpiresRound = expiresRound;
                }

                existing.Reason = reason;
                return null;
            }

            c.Conditions.Add(new ConditionInstance
            {
                Type = t, SourceId = source, Reason = reason ?? string.Empty, AppliedRound = Encounter.Round, Duration = duration, ExpiresOn = expiresOn,
                ExpiresRound = expiresRound, Level = t == ConditionType.Exhaustion ? Math.Max(1, level) : 0, EscapeDc = escapeDc,
            });
            Log(CombatLogKind.ConditionApplied, c.CombatantId, source, $"{c.Name} is {Describe(t)}{(string.IsNullOrEmpty(reason) ? string.Empty : $" ({reason})")}.");
            if (c.Concentration != null && Incapacitated(c)) EndConcentration(c, "incapacitated"); // Phase 9
            if (c.FormCreatureId != null && Incapacitated(c)) EndOwnEffects(c, Realmkeeper.Domain.Magic.CombatEffectKind.WildShapeForm, "incapacitated");
            if (t == ConditionType.Exhaustion && level >= 6) Kill(c, "exhaustion", null);
            return null;
        }

        public bool RemoveCondition(CombatantData c, ConditionType t, string reason)
        {
            var k = Find(c, t);
            if (k == null) return false;
            c.Conditions.Remove(k);
            Log(CombatLogKind.ConditionRemoved, c.CombatantId, default, $"{c.Name} is no longer {t.ToString().ToLowerInvariant()} ({reason}).");
            return true;
        }

        public static string Describe(ConditionType t) => t == ConditionType.Exhaustion ? "exhausted" : t.ToString().ToLowerInvariant();

        // ===== Sides and the end ===========================================================================

        public static bool Hostile(CombatantData a, CombatantData b) =>
            a != b && !string.Equals(a.Side, b.Side, StringComparison.Ordinal) && a.Side != Neutral && b.Side != Neutral;

        /// <summary>Ends the encounter when at most one side (neutrals aside) still has someone able to fight.</summary>
        public bool CheckEnd()
        {
            if (!Encounter.IsActive) return true;
            var able = new List<string>();
            foreach (var c in Encounter.Combatants)
                if (c.State == VitalState.Conscious && c.Side != Neutral && !able.Contains(c.Side)) able.Add(c.Side);
            if (able.Count > 1) return false;
            if (able.Count == 1) Finish(EncounterOutcome.Victory, able[0]);
            else Finish(EncounterOutcome.Stalemate, null);
            return true;
        }

        /// <summary>
        /// Ends the encounter. A player still dying when the fight ends is stabilized by the end of it (Realmkeeper rule:
        /// the fight is over and someone tends them; the dead stay dead). Conditions belong to the encounter and end with
        /// it; hit points stay where they are.
        /// </summary>
        public void Finish(EncounterOutcome outcome, string winner)
        {
            var e = Encounter;
            if (!e.IsActive) return;
            e.Status = EncounterStatus.Ended;
            e.Outcome = outcome;
            e.WinningSide = winner;
            e.EndedOn = _world.CurrentSimulationDate;
            EndAllEffects(); // Phase 9: spell and feature effects are encounter time only
            foreach (var c in e.Combatants)
                if (c.Kind == CombatantKind.Player && c.State == VitalState.Dying)
                {
                    SetState(c, VitalState.Stable);
                    Log(CombatLogKind.Stabilized, c.CombatantId, default, $"{c.Name} is stabilized as the fight ends.");
                }

            var result = outcome == EncounterOutcome.Victory ? $"{winner} prevailed" : outcome == EncounterOutcome.Withdrawn ? "the player's side withdrew" :
                outcome == EncounterOutcome.Stalemate ? "nobody was left standing" : "it was called off";
            Log(CombatLogKind.Ended, default, default, $"The fight is over after {e.Round} round{(e.Round == 1 ? string.Empty : "s")}: {result}.");
            Note(HistoricalEventType.EncounterEnded, default, ("name", e.Name), ("outcome", outcome.ToString()), ("winner", winner ?? string.Empty),
                ("rounds", e.Round.ToString(CultureInfo.InvariantCulture)), ("result", result));
        }

        // ===== Dice and log ================================================================================

        /// <summary>One die from the encounter's seeded stream (counter-based: reproducible after a save).</summary>
        public int Die(int sides)
        {
            if (sides <= 1) return 1;
            var e = Encounter;
            e.RollCount++;
            var v = SeedMixer.Mix(SeedMixer.Combine(e.Seed, (ulong)e.RollCount));
            return (int)(v % (ulong)sides) + 1;
        }

        /// <summary>Rolls a d20 for the roll's sources: two dice with advantage or disadvantage (they cancel each other).</summary>
        private void Roll(D20Roll roll)
        {
            var adv = roll.AdvantageSources.Count > 0;
            var dis = roll.DisadvantageSources.Count > 0;
            roll.Advantage = adv && !dis;
            roll.Disadvantage = dis && !adv;
            roll.First = Die(20);
            if (roll.Advantage || roll.Disadvantage)
            {
                roll.Second = Die(20);
                roll.Natural = roll.Advantage ? Math.Max(roll.First, roll.Second) : Math.Min(roll.First, roll.Second);
            }
            else roll.Natural = roll.First;
        }

        private void Log(CombatLogKind kind, CombatantId actor, CombatantId target, string text, int value = 0, D20Roll roll = null)
        {
            var e = Encounter;
            var entry = new CombatLogEntry { Sequence = e.Log.Count == 0 ? 1 : e.Log[e.Log.Count - 1].Sequence + 1, Round = e.Round, Kind = kind, ActorId = actor, TargetId = target, Text = text, Value = value };
            if (roll != null)
            {
                if (roll.First > 0) entry.Rolls.Add(roll.First);
                if (roll.Second > 0) entry.Rolls.Add(roll.Second);
            }

            e.Log.Add(entry);
            if (e.Log.Count > EncounterData.MaxLogEntries) e.Log.RemoveAt(0);
            _current?.Entries.Add(entry);
        }

        private void Note(HistoricalEventType type, NpcId npc, params (string key, string value)[] facts)
        {
            var n = new CombatChronicleNote { Type = type, NpcId = npc };
            foreach (var (k, v) in facts) n.Facts[k] = v;
            Chronicle.Add(n);
        }
    }
}
