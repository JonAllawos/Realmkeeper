using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>One combatant as the UI shows it (Phase 8). Every number comes from the engine; nothing here is calculated by the UI.</summary>
    public sealed class CombatantView
    {
        public CombatantId Id { get; set; }
        public string Name { get; set; }
        public CombatantKind Kind { get; set; }
        public string Side { get; set; }
        public CombatControl Control { get; set; }
        public bool IsActive { get; set; }
        public bool HostileToPlayer { get; set; }
        public int Initiative { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int TempHp { get; set; }
        public int ArmorClass { get; set; }
        public string ArmorClassText { get; set; }
        public int Speed { get; set; }
        public int MovementLeft { get; set; }
        public GridPos Position { get; set; }
        public VitalState State { get; set; }
        public IReadOnlyList<string> Conditions { get; set; }
        public string Weapon { get; set; }
        public string Attacks { get; set; }
        public int DeathSaveSuccesses { get; set; }
        public int DeathSaveFailures { get; set; }
        public bool ActionUsed { get; set; }
        public bool BonusActionUsed { get; set; }
        public bool ReactionUsed { get; set; }
        public int AttacksLeft { get; set; }
        public string Stats { get; set; }

        /// <summary>Phase 10: what the bestiary says of a creature (category, challenge, behaviour, traits, source), or null.</summary>
        public string Bestiary { get; set; }

        /// <summary>Phase 14 (ADR-0067): this combatant's cover against the player ("half cover from a wall"), and whether the player has a clear line to it.</summary>
        public string CoverFromPlayer { get; set; }

        public bool InPlayersLine { get; set; } = true;

        /// <summary>For the stand-in figure: a beast (the wolf) rather than a person.</summary>
        public bool CreatureIsBeast { get; set; }

        // ----- Phase 9 -----

        /// <summary>What it concentrates on, or null.</summary>
        public string Concentration { get; set; }

        /// <summary>Spell and feature effects on it, in words ("Bless (from Ada)", "Rage" …).</summary>
        public IReadOnlyList<string> Effects { get; set; } = new List<string>();

        public bool IsCaster { get; set; }
        public int SpellSaveDc { get; set; }
        public int SpellAttack { get; set; }

        /// <summary>Spell slots and class resources left, in words.</summary>
        public string Resources { get; set; }

        /// <summary>Wild Shape: the beast form it is in, or null.</summary>
        public string Form { get; set; }
    }

    /// <summary>An action the player can take now, or why not.</summary>
    public sealed class CombatOption
    {
        public string Label { get; set; }
        public CombatAction Action { get; set; }
        public string Unavailable { get; set; }
        public bool IsBonus { get; set; }
    }

    /// <summary>Phase 10: the question an action waits on, as the prompt shows it.</summary>
    public sealed class ReactionPromptView
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Reactor { get; set; }
        public string Trigger { get; set; }
        public string Cost { get; set; }
        public string Consequence { get; set; }
        public bool IsDecision { get; set; }

        /// <summary>True when the player answers it (always, today: only the player is ever asked).</summary>
        public bool ForPlayer { get; set; }
    }

    public sealed class EncounterSnapshot
    {
        /// <summary>Phase 11: the container of what this fight left (null when nothing), and what it holds in words.</summary>
        public string LootKey { get; set; }
        public string LootSummary { get; set; }

        /// <summary>Phase 10: the reaction or decision the player is being asked for, or null.</summary>
        public ReactionPromptView Prompt { get; set; }

        public EncounterId Id { get; set; }
        public string Name { get; set; }
        public EncounterKind Kind { get; set; }
        public Lethality Lethality { get; set; }
        public EncounterStatus Status { get; set; }
        public EncounterOutcome Outcome { get; set; }
        public string WinningSide { get; set; }
        public int Round { get; set; }
        public int Sequence { get; set; }
        public CombatantView Active { get; set; }
        public CombatantView Player { get; set; }
        public IReadOnlyList<CombatantView> Order { get; set; }
        public IReadOnlyList<CombatLogEntry> Log { get; set; }

        /// <summary>Squares of difficult terrain (moving into one costs double).</summary>
        public IReadOnlyList<GridPos> DifficultTerrain { get; set; } = new List<GridPos>();

        /// <summary>Phase 13: walls (a place's geometry) no one can enter.</summary>
        public IReadOnlyList<GridPos> Blocked { get; set; } = new List<GridPos>();

        /// <summary>Phase 14 (ADR-0067): the fight's doors (closed ones block movement, sight and spells) and low cover.</summary>
        public IReadOnlyList<EncounterDoorData> Doors { get; set; } = new List<EncounterDoorData>();

        public IReadOnlyList<LowCoverData> LowCover { get; set; } = new List<LowCoverData>();

        public bool BlockedContains(GridPos p)
        {
            foreach (var d in Blocked) if (d.Equals(p)) return true;
            return false;
        }

        public bool DifficultTerrainContains(GridPos p)
        {
            foreach (var d in DifficultTerrain) if (d.Equals(p)) return true;
            return false;
        }
        public bool IsPlayersTurn => Status == EncounterStatus.Active && Active != null && Active.Control == CombatControl.Player;

        /// <summary>Attacks, actions and bonus actions for the player's turn (empty otherwise), each with why it is unavailable.</summary>
        public IReadOnlyList<CombatOption> PlayerOptions { get; set; }

        /// <summary>Phase 9: the spells the player can try to cast this turn, each with its slot levels, targets and why not.</summary>
        public IReadOnlyList<SpellCastOption> PlayerSpells { get; set; } = new List<SpellCastOption>();

        /// <summary>Phase 9: the player's targeted or optioned class features (Bardic Inspiration, Lay on Hands, Flurry of Blows …).</summary>
        public IReadOnlyList<FeatureOption> PlayerFeatures { get; set; } = new List<FeatureOption>();
    }

    public sealed partial class WorldSession
    {
        /// <summary>The encounter going on, or the most recent one when <paramref name="includeEnded"/>; null when none.</summary>
        public EncounterSnapshot GetEncounter(bool includeEnded = false)
        {
            if (_world == null) return null;
            EncounterData e = _world.ActiveEncounter;
            if (e == null && includeEnded && _world.Encounters.Count > 0) e = _world.Encounters[_world.Encounters.Count - 1];
            if (e == null) return null;
            var world = _world.DeepClone();
            EncounterData copy = null;
            foreach (var x in world.Encounters) if (x.EncounterId == e.EncounterId) copy = x;
            return Snapshot(world, copy);
        }

        private static EncounterSnapshot Snapshot(WorldData world, EncounterData e)
        {
            var engine = new CombatEngine(world, e);
            CombatantData player = null;
            foreach (var c in e.Combatants) if (c.Kind == CombatantKind.Player) player = c;
            CombatantView View(CombatantData c)
            {
                var s = engine.Stats(c);
                var conditions = new List<string>();
                foreach (var k in c.Conditions)
                    conditions.Add(k.Type == ConditionType.Exhaustion ? $"exhaustion {k.Level}" : CombatEngine.Describe(k.Type) + (string.IsNullOrEmpty(k.Reason) ? string.Empty : $" ({k.Reason})"));
                if (c.Turn.Dodging) conditions.Add("dodging");
                if (c.Turn.Disengaged && c == e.Active) conditions.Add("disengaged");
                var attacks = new List<string>();
                foreach (var a in s.Attacks)
                    if (a.Kind == AttackKind.Weapon || a.Kind == AttackKind.Unarmed)
                        attacks.Add($"{a.Name} {(a.AttackBonus.Total >= 0 ? "+" : string.Empty)}{a.AttackBonus.Total}, {a.Damage}{(a.DamageBonus != null && a.DamageBonus.Total != 0 ? (a.DamageBonus.Total > 0 ? "+" : string.Empty) + a.DamageBonus.Total : string.Empty)} {a.DamageType.ToString().ToLowerInvariant()}" +
                                    (a.Melee ? $", reach {a.Reach} ft" : $", range {a.Range}/{a.LongRange} ft"));
                var scores = new List<string>();
                foreach (var ab in Domain.Rpg.AbilityRules.All) scores.Add(ab.ToString().Substring(0, 3) + " " + s.Score(ab));
                return new CombatantView
                {
                    Id = c.CombatantId, Name = c.Name, Kind = c.Kind, Side = c.Side, Control = c.Control, IsActive = c == e.Active,
                    HostileToPlayer = player != null && CombatEngine.Hostile(player, c), Initiative = c.Initiative,
                    Hp = engine.Hp(c), MaxHp = engine.MaxHp(c), TempHp = engine.TempHp(c), ArmorClass = engine.ArmorClass(c).Total, ArmorClassText = engine.ArmorClass(c).Text,
                    Speed = engine.Speed(c), MovementLeft = c == e.Active ? engine.MovementLeft(c) : engine.Speed(c), Position = c.Position, State = c.State,
                    Conditions = conditions, Weapon = s.WeaponName, Attacks = string.Join("; ", attacks),
                    DeathSaveSuccesses = engine.Successes(c), DeathSaveFailures = engine.Failures(c),
                    ActionUsed = c.Turn.ActionUsed, BonusActionUsed = c.Turn.BonusActionUsed, ReactionUsed = c.Turn.ReactionUsed, AttacksLeft = c.Turn.AttacksLeft,
                    Stats = string.Join(" · ", scores) + $" · speed {s.BaseSpeed} ft · proficiency +{s.ProficiencyBonus}",
                    CreatureIsBeast = s.Creature != null && s.Creature.Type == "Beast" || c.FormCreatureId != null,
                    Concentration = c.Concentration?.Name, Effects = EffectLines(engine, c), IsCaster = s.IsCaster && c.FormCreatureId == null,
                    SpellSaveDc = s.IsCaster ? engine.SpellSaveDc(c) : 0, SpellAttack = s.IsCaster ? s.SpellAttack().Total : 0, Resources = ResourceText(engine, c),
                    Form = c.FormCreatureId == null ? null : CreatureCatalog.Find(c.FormCreatureId)?.Name,
                    Bestiary = c.CreatureId != null && c.FormCreatureId == null ? BestiaryText(CreatureCatalog.Find(c.CreatureId)) : null,
                    CoverFromPlayer = player != null && c != player ? engine.CoverOf(player, c).Describe() : null,
                    InPlayersLine = player == null || c == player || engine.CoverOf(player, c).HasLineOfEffect,
                };
            }

            var order = new List<CombatantView>();
            foreach (var id in e.Order) order.Add(View(e.Find(id)));
            var snap = new EncounterSnapshot
            {
                Id = e.EncounterId, Name = e.Name, Kind = e.Kind, Lethality = e.Lethality, Status = e.Status, Outcome = e.Outcome, WinningSide = e.WinningSide,
                Round = e.Round, Sequence = e.ActionSequence, Order = order, Log = e.Log, DifficultTerrain = e.DifficultTerrain, Blocked = e.Blocked, Doors = e.Doors, LowCover = e.LowCover,
                Active = e.Active != null ? View(e.Active) : null, Player = player != null ? View(player) : null,
            };
            var loot = world.FindContainer(WorldEncounters.LootKey(e.EncounterId)); // Phase 11: what the fight left
            if (loot != null && !loot.IsEmpty)
            {
                snap.LootKey = loot.Key;
                var n = 0;
                foreach (var x in loot.Items) n += x.Quantity;
                snap.LootSummary = $"{n} item{(n == 1 ? string.Empty : "s")}{(loot.Coin > 0 ? $" and {loot.Coin} coin" : string.Empty)}";
            }

            if (e.PendingReaction != null)
            {
                var o = e.PendingReaction.Opportunity;
                snap.Prompt = new ReactionPromptView
                {
                    Id = e.PendingReaction.Id, Name = o.Name, Reactor = e.Find(o.ReactorId)?.Name, Trigger = o.Trigger, Cost = o.Cost, Consequence = o.Consequence,
                    IsDecision = o.IsDecision, ForPlayer = e.Find(o.ReactorId)?.Control == CombatControl.Player,
                };
            }

            snap.PlayerOptions = player != null && e.Active == player && e.IsActive ? Options(engine, player) : new List<CombatOption>();
            if (player != null && e.Active == player && e.IsActive)
            {
                snap.PlayerSpells = SpellOptions(engine, player);
                snap.PlayerFeatures = FeatureOptions(engine, player);
            }
            return snap;
        }

        private static List<CombatOption> Options(CombatEngine engine, CombatantData player)
        {
            var list = new List<CombatOption>();
            var e = engine.Encounter;
            var s = engine.Stats(player);
            foreach (var door in e.Doors) // Phase 14: doors within reach can be opened or shut
            {
                var act = new DoorAction(door.Id);
                var why = engine.Unavailable(player, act);
                if (why != null && why.Contains("out of reach")) continue;
                list.Add(new CombatOption { Label = (door.Open ? "Shut the " : "Open the ") + door.Name, Action = act, Unavailable = why });
            }

            foreach (var a in s.Attacks)
                foreach (var target in e.Combatants)
                {
                    if (!CombatEngine.Hostile(player, target) || target.State == VitalState.Dead || target.State == VitalState.Fled) continue;
                    var action = new AttackAction(target.CombatantId, a.Id);
                    var d = player.Position.DistanceFeet(target.Position);
                    list.Add(new CombatOption
                    {
                        Label = $"{a.Name} → {target.Name} ({d} ft)", Action = action, Unavailable = engine.Unavailable(player, action), IsBonus = a.OffHand, // Phase 11: the off-hand attack is a bonus action
                    });
                }

            foreach (var (label, action) in new (string, CombatAction)[]
                     {
                         ("Dash", new DashAction()), ("Disengage", new DisengageAction()), ("Dodge", new DodgeAction()), ("Stand up", new StandAction()), ("Escape grapple", new EscapeAction()),
                     })
                list.Add(new CombatOption { Label = label, Action = action, Unavailable = engine.Unavailable(player, action) });
            foreach (var target in e.Combatants)
                if (target.State == VitalState.Dying && target != player)
                {
                    var action = new StabilizeAction(target.CombatantId);
                    list.Add(new CombatOption { Label = "Stabilize " + target.Name, Action = action, Unavailable = engine.Unavailable(player, action) });
                }

            foreach (var (id, name, unavailable) in engine.BonusAbilities(player))
            {
                var action = new BonusAbilityAction(id);
                list.Add(new CombatOption { Label = name, Action = action, IsBonus = true, Unavailable = unavailable ?? engine.Unavailable(player, action) });
            }

            // Phase 11: potions and other things from the pack that can be used in a fight (a bonus action).
            var pack = engine.World.Player;
            if (player.Kind == CombatantKind.Player && pack != null)
                foreach (var stack in pack.Inventory)
                {
                    var def = ItemCatalog.Find(stack.ItemId);
                    if (def?.Consumable?.InCombat == null) continue;
                    var action = new UseItemAction(stack.StackId);
                    list.Add(new CombatOption { Label = $"{def.Consumable.Verb} {def.Name} ({stack.Quantity} left)", Action = action, IsBonus = true, Unavailable = engine.Unavailable(player, action) });
                }

            list.Add(new CombatOption { Label = "End turn", Action = new EndTurnAction() });
            return list;
        }

        /// <summary>Why the player cannot move to a square now (null when they can), and the path the move would take.</summary>
        public string PlayerMoveProblem(GridPos to, out GridPos[] path)
        {
            path = null;
            var e = _world?.ActiveEncounter;
            if (e == null) return "There is no fight going on.";
            var world = _world.DeepClone();
            EncounterData copy = null;
            foreach (var x in world.Encounters) if (x.EncounterId == e.EncounterId) copy = x;
            var engine = new CombatEngine(world, copy);
            var player = copy.Active;
            if (player == null || player.Control != CombatControl.Player) return "It is not your turn.";
            var steps = new List<GridPos>();
            var at = player.Position;
            for (var guard = 0; guard < 40 && !at.Equals(to); guard++)
            {
                at = at.Step(to.X - at.X, to.Y - at.Y);
                steps.Add(at);
            }

            path = steps.ToArray();
            return engine.Unavailable(player, new MoveAction(path));
        }

        /// <summary>
        /// Combat Overhaul: the player's move to a square as the rules see it — the cheapest route round walls, shut doors
        /// and enemies, its cost, what Dash would add, and the opportunity attacks it would invite. Reads a copy: nothing
        /// changes. A route that can be walked now is also checked by the engine itself (fear, and anything else it refuses).
        /// </summary>
        public MovePlan PlanPlayerMove(GridPos to)
        {
            var e = _world?.ActiveEncounter;
            if (e == null) return new MovePlan { Destination = to, Problem = "There is no fight going on." };
            var world = _world.DeepClone();
            EncounterData copy = null;
            foreach (var x in world.Encounters) if (x.EncounterId == e.EncounterId) copy = x;
            var engine = new CombatEngine(world, copy);
            var player = copy.Active;
            if (player == null || player.Control != CombatControl.Player) return new MovePlan { Destination = to, Problem = "It is not your turn." };
            var plan = CombatPaths.Plan(engine, player, to);
            if (plan.Reachable)
            {
                var refused = engine.Unavailable(player, new MoveAction(plan.Path.ToArray()));
                if (refused != null) plan.Problem = refused;
            }

            return plan;
        }

        /// <summary>A short, readable result for a finished encounter (for the HUD).</summary>
        public static string Summary(EncounterSnapshot s) =>
            s == null ? string.Empty :
            s.Status == EncounterStatus.Active ? $"{s.Name} · round {s.Round.ToString(CultureInfo.InvariantCulture)} · {s.Active?.Name}'s turn" :
            $"{s.Name} · over after {s.Round.ToString(CultureInfo.InvariantCulture)} rounds · {s.Outcome}" + (string.IsNullOrEmpty(s.WinningSide) ? string.Empty : $" ({s.WinningSide})");
    }
}
