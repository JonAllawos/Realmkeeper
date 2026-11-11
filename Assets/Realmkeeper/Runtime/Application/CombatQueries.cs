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

        /// <summary>For the stand-in figure: a beast (the wolf) rather than a person.</summary>
        public bool CreatureIsBeast { get; set; }
    }

    /// <summary>An action the player can take now, or why not.</summary>
    public sealed class CombatOption
    {
        public string Label { get; set; }
        public CombatAction Action { get; set; }
        public string Unavailable { get; set; }
        public bool IsBonus { get; set; }
    }

    public sealed class EncounterSnapshot
    {
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

        public bool DifficultTerrainContains(GridPos p)
        {
            foreach (var d in DifficultTerrain) if (d.Equals(p)) return true;
            return false;
        }
        public bool IsPlayersTurn => Status == EncounterStatus.Active && Active != null && Active.Control == CombatControl.Player;

        /// <summary>Attacks, actions and bonus actions for the player's turn (empty otherwise), each with why it is unavailable.</summary>
        public IReadOnlyList<CombatOption> PlayerOptions { get; set; }
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
                    Hp = engine.Hp(c), MaxHp = engine.MaxHp(c), TempHp = engine.TempHp(c), ArmorClass = s.ArmorClass.Total, ArmorClassText = s.ArmorClass.Text,
                    Speed = engine.Speed(c), MovementLeft = c == e.Active ? engine.MovementLeft(c) : engine.Speed(c), Position = c.Position, State = c.State,
                    Conditions = conditions, Weapon = s.WeaponName, Attacks = string.Join("; ", attacks),
                    DeathSaveSuccesses = engine.Successes(c), DeathSaveFailures = engine.Failures(c),
                    ActionUsed = c.Turn.ActionUsed, BonusActionUsed = c.Turn.BonusActionUsed, ReactionUsed = c.Turn.ReactionUsed, AttacksLeft = c.Turn.AttacksLeft,
                    Stats = string.Join(" · ", scores) + $" · speed {s.BaseSpeed} ft · proficiency +{s.ProficiencyBonus}",
                    CreatureIsBeast = s.Creature != null && s.Creature.Type == "Beast",
                };
            }

            var order = new List<CombatantView>();
            foreach (var id in e.Order) order.Add(View(e.Find(id)));
            var snap = new EncounterSnapshot
            {
                Id = e.EncounterId, Name = e.Name, Kind = e.Kind, Lethality = e.Lethality, Status = e.Status, Outcome = e.Outcome, WinningSide = e.WinningSide,
                Round = e.Round, Sequence = e.ActionSequence, Order = order, Log = e.Log, DifficultTerrain = e.DifficultTerrain,
                Active = e.Active != null ? View(e.Active) : null, Player = player != null ? View(player) : null,
            };
            snap.PlayerOptions = player != null && e.Active == player && e.IsActive ? Options(engine, player) : new List<CombatOption>();
            return snap;
        }

        private static List<CombatOption> Options(CombatEngine engine, CombatantData player)
        {
            var list = new List<CombatOption>();
            var e = engine.Encounter;
            var s = engine.Stats(player);
            foreach (var a in s.Attacks)
                foreach (var target in e.Combatants)
                {
                    if (!CombatEngine.Hostile(player, target) || target.State == VitalState.Dead || target.State == VitalState.Fled) continue;
                    var action = new AttackAction(target.CombatantId, a.Id);
                    var d = player.Position.DistanceFeet(target.Position);
                    list.Add(new CombatOption
                    {
                        Label = $"{a.Name} → {target.Name} ({d} ft)", Action = action, Unavailable = engine.Unavailable(player, action),
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

        /// <summary>A short, readable result for a finished encounter (for the HUD).</summary>
        public static string Summary(EncounterSnapshot s) =>
            s == null ? string.Empty :
            s.Status == EncounterStatus.Active ? $"{s.Name} · round {s.Round.ToString(CultureInfo.InvariantCulture)} · {s.Active?.Name}'s turn" :
            $"{s.Name} · over after {s.Round.ToString(CultureInfo.InvariantCulture)} rounds · {s.Outcome}" + (string.IsNullOrEmpty(s.WinningSide) ? string.Empty : $" ({s.WinningSide})");
    }
}
