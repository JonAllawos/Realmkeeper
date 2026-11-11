using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// Spellcasting in combat (Phase 9, ADR-0037). One pipeline for every caster — the player, residents with a caster
    /// profile, creatures — and every spell: validate (spell ready, action economy, slot, targets, range, area), pay
    /// (slot or feature resource, sorcery points), start concentration, then resolve each target through the shared
    /// attack roll, saving throw, damage, healing and condition routines. A refused cast changes nothing.
    /// </summary>
    public sealed partial class CombatEngine
    {
        public static readonly string[] SupportedMetamagic = { "careful", "distant", "empowered", "heightened", "quickened", "twinned" };

        private static int MetamagicCost(string id, SpellDefinition s) =>
            id == "quickened" || id == "heightened" ? 2 : 1;

        // ===== What a combatant can cast ===================================================================

        private SpellAccess Access(CombatantData c)
        {
            if (c.Kind != CombatantKind.Player) return null;
            var rules = Stats(c).Rules;
            return rules?.Class?.Spellcasting == null ? null : new SpellAccess(rules);
        }

        /// <summary>The spells a combatant has ready (cantrips first, then by level), castable or not.</summary>
        public List<SpellDefinition> SpellsOf(CombatantData c)
        {
            var list = new List<SpellDefinition>();
            var access = Access(c);
            if (access != null)
            {
                list.AddRange(access.ReadyCantrips());
                list.AddRange(access.ReadySpells());
            }
            else if (Stats(c).Creature?.Spellcasting is CreatureSpellcasting block && c.FormCreatureId == null)
            {
                foreach (var id in block.Cantrips) if (SpellCatalog.Find(id) is SpellDefinition s) list.Add(s);
                foreach (var id in block.Spells) if (SpellCatalog.Find(id) is SpellDefinition s) list.Add(s);
            }

            if (c.Kind == CombatantKind.Player && Stats(c).Rules != null && Stats(c).Rules.Sheet.Chosen("class:invocations").Count + 0 >= 0 && HasInvocation(c, "armor_of_shadows"))
                if (!list.Exists(x => x.Id == "mage_armor")) list.Add(SpellCatalog.Find("mage_armor"));
            return list;
        }

        private bool HasInvocation(CombatantData c, string id)
        {
            var sheet = Stats(c).Rules?.Sheet;
            if (sheet == null) return false;
            foreach (var pair in sheet.Choices)
                if (pair.Key.EndsWith("invocations", StringComparison.Ordinal) && pair.Value.Contains(id)) return true;
            return false;
        }

        /// <summary>Cast without a slot: cantrips, and Mage Armour for a warlock with Armour of Shadows.</summary>
        private bool AtWill(CombatantData c, SpellDefinition s) => s.IsCantrip || s.Id == "mage_armor" && HasInvocation(c, "armor_of_shadows");

        private int SlotsLeft(CombatantData c, int level)
        {
            var access = Access(c);
            if (access != null) return access.SlotsLeft(level);
            return c.Resources.TryGetValue("spell_slots_" + level, out var n) ? n : 0;
        }

        /// <summary>The slot levels a combatant can cast this spell with now (empty for cantrips and at-will spells).</summary>
        public List<int> CastLevels(CombatantData c, SpellDefinition s)
        {
            if (AtWill(c, s)) return new List<int>();
            var access = Access(c);
            if (access != null) return access.CastLevels(s);
            var list = new List<int>();
            for (var l = s.Level; l <= 9; l++) if (SlotsLeft(c, l) > 0) list.Add(l);
            return list;
        }

        /// <summary>A feature resource that can pay for this spell instead of a slot (Favoured Enemy: Hunter's Mark), when it has uses left.</summary>
        public string FreeCastResource(CombatantData c, SpellDefinition s) =>
            s.Id == "hunters_mark" && AttackerFeature(c, "favored_enemy") && ResourceLeft(c, "favored_enemy") > 0 ? "favored_enemy" : null;

        public int SpellSaveDc(CombatantData c) => Stats(c).SpellSaveDc().Total + (HasEffect(c, CombatEffectKind.InnateSorcery) ? 1 : 0);

        private static int CantripTier(int level) => 1 + (level >= 5 ? 1 : 0) + (level >= 11 ? 1 : 0) + (level >= 17 ? 1 : 0);

        public int MaxTargets(SpellDefinition s, int slotLevel, bool twinned)
        {
            var extra = s.IsCantrip ? 0 : Math.Max(0, slotLevel - s.Level) * s.TargetsPerSlotAbove;
            var n = s.MaxTargets + extra + (twinned ? 1 : 0);
            return Math.Max(n, s.Instances > 1 || s.ScalesByInstances ? Instances(s, slotLevel, 20) : 0);
        }

        private int Instances(SpellDefinition s, int slotLevel, int casterLevel)
        {
            if (s.ScalesByInstances) return CantripTier(casterLevel);
            return s.Instances + (s.IsCantrip ? 0 : Math.Max(0, slotLevel - s.Level) * s.InstancesPerSlotAbove);
        }

        // ===== Validation ==================================================================================

        /// <summary>Why a cast is not allowed now (null when it is). Shared by the engine, the AI and the UI.</summary>
        public string SpellProblem(CombatantData actor, CastSpellAction a)
        {
            var s = SpellCatalog.Find(a.SpellId);
            if (s == null) return "There is no such spell.";
            if (!s.Castable) return $"{s.Name} cannot be cast yet: {s.StatusNote}";
            if (Incapacitated(actor)) return $"{actor.Name} is incapacitated.";
            if (!SpellsOf(actor).Exists(x => x.Id == s.Id)) return $"{actor.Name} does not have {s.Name} ready.";
            if (HasEffect(actor, CombatEffectKind.Raging)) return "No spells while raging.";
            if (actor.FormCreatureId != null) return "No spells in Wild Shape.";
            if (s.CastingTime == CastingTime.Longer) return $"{s.Name} takes longer than a turn to cast.";

            var t = actor.Turn;
            var meta = new List<string>(a.Metamagic);
            var quickened = meta.Contains("quickened");
            var time = quickened ? CastingTime.BonusAction : s.CastingTime;
            if (time == CastingTime.Action)
            {
                if (t.ActionUsed) return "The action this turn is used.";
                if (t.SurgeActionPending) return "The extra action from Action Surge cannot be a Magic action.";
            }
            else if (time == CastingTime.BonusAction && t.BonusActionUsed) return "The bonus action this turn is used.";

            if (AtWill(actor, s))
            {
                if (a.SlotLevel != 0) return $"{s.Name} needs no spell slot.";
            }
            else if (a.FreeWith != null)
            {
                if (a.FreeWith != FreeCastResource(actor, s)) return $"{s.Name} cannot be cast free that way now.";
                if (a.SlotLevel != s.Level) return $"A free {s.Name} is cast at level {s.Level}.";
            }
            else
            {
                if (a.SlotLevel < s.Level) return $"{s.Name} needs a level {s.Level} slot or higher.";
                if (t.SlotSpellCast) return "Only one spell with a spell slot per turn.";
                if (!CastLevels(actor, s).Contains(a.SlotLevel)) return $"No level {a.SlotLevel} slot is left{(Access(actor)?.UsesPactMagic == true ? $" (pact slots are level {Access(actor).PactSlotLevel})" : string.Empty)}.";
            }

            var metaProblem = MetamagicProblem(actor, s, meta);
            if (metaProblem != null) return metaProblem;

            var range = (s.Touch ? 5 : s.Range) * (meta.Contains("distant") ? (s.Touch ? 6 : 2) : 1);
            switch (s.Targeting)
            {
                case SpellTargeting.Self:
                    return null;
                case SpellTargeting.Creatures:
                {
                    if (a.Targets.Count == 0) return "Choose a target.";
                    var limit = MaxTargets(s, Math.Max(a.SlotLevel, s.Level), meta.Contains("twinned"));
                    var multi = s.Instances > 1 || s.ScalesByInstances;
                    if (a.Targets.Count > limit) return $"{s.Name} can target at most {limit} creature{(limit == 1 ? string.Empty : "s")} at that level.";
                    var seen = new HashSet<CombatantId>();
                    foreach (var id in a.Targets)
                    {
                        if (!seen.Add(id) && !multi) return "A creature is chosen twice.";
                        var target = Encounter.Find(id);
                        if (target == null) return "Choose a creature in the fight.";
                        var why = TargetProblem(actor, s, target, range);
                        if (why != null) return why;
                    }

                    if (s.AfterMeleeHit)
                    {
                        var target = Encounter.Find(a.Targets[0]);
                        if (t.LastHitTargetId != target.CombatantId || !t.LastHitMelee) return $"{s.Name} follows a melee hit: hit {target.Name} with a melee weapon or unarmed strike this turn first.";
                    }

                    return null;
                }

                default:
                {
                    if (a.Point == null) return "Choose where to aim.";
                    var p = a.Point.Value;
                    if (s.Targeting == SpellTargeting.FromSelf) return p.Equals(actor.Position) ? "Aim away from yourself." : null;
                    var d = actor.Position.DistanceFeet(p);
                    if (d > range) return $"That point is {d} ft away; {s.Name} reaches {range} ft.";
                    foreach (var e in s.Effects)
                        if (e.Kind == CombatEffectKind.Teleport && At(p) != null) return $"{p} is occupied.";
                    return null;
                }
            }
        }

        private string TargetProblem(CombatantData actor, SpellDefinition s, CombatantData target, int range)
        {
            if (target.State == VitalState.Dead || target.State == VitalState.Fled) return $"{target.Name} is out of the fight.";
            if (s.Affects == SpellAffects.Hostile && !Hostile(actor, target)) return $"{s.Name} is aimed at foes; {target.Name} is not hostile.";
            if (s.Affects == SpellAffects.Ally && target != actor && target.Side != actor.Side) return $"{s.Name} works on willing allies; {target.Name} is not one.";
            if (s.HumanoidsOnly && TypeOf(target) != "Humanoid") return $"{s.Name} affects only humanoids; {target.Name} is a {TypeOf(target).ToLowerInvariant()}.";
            if (!s.Heals && target.State != VitalState.Conscious && s.Affects != SpellAffects.Hostile && !s.DealsDamage) { }
            var d = actor.Position.DistanceFeet(target.Position);
            if (d > range) return $"{target.Name} is {d} ft away; {s.Name} reaches {range} ft.";
            foreach (var k in actor.Conditions)
                if (k.Type == ConditionType.Charmed && k.SourceId == target.CombatantId && s.Affects == SpellAffects.Hostile) return $"{actor.Name} is charmed by {target.Name} and cannot harm them.";
            return null;
        }

        private string TypeOf(CombatantData c)
        {
            if (c.FormCreatureId != null) return CreatureCatalog.Find(c.FormCreatureId)?.Type ?? "Beast";
            return c.Kind == CombatantKind.Player ? "Humanoid" : Stats(c).Creature?.Type ?? "Humanoid";
        }

        private string MetamagicProblem(CombatantData actor, SpellDefinition s, List<string> meta)
        {
            if (meta.Count == 0) return null;
            var rules = Stats(actor).Rules;
            if (rules == null || !rules.HasFeature("metamagic")) return "Only a sorcerer with Metamagic can shape spells.";
            var known = new HashSet<string>();
            foreach (var pair in rules.Sheet.Choices) if (pair.Key.EndsWith("metamagic", StringComparison.Ordinal)) foreach (var v in pair.Value) known.Add(v);
            var cost = 0;
            var main = 0;
            foreach (var m in meta)
            {
                if (!known.Contains(m)) return $"{m} is not one of your Metamagic options.";
                if (Array.IndexOf(SupportedMetamagic, m) < 0) return $"{m} Metamagic is not simulated yet.";
                if (m != "empowered") main++;
                cost += MetamagicCost(m, s);
                if (m == "quickened" && s.CastingTime != CastingTime.Action) return "Quickened Spell turns an action into a bonus action.";
                if (m == "twinned" && s.TargetsPerSlotAbove == 0) return $"{s.Name} cannot take another target.";
                if ((m == "careful" || m == "heightened") && s.Resolution != SpellResolution.Save) return $"{s.Name} forces no saving throw.";
                if (m == "empowered" && !s.DealsDamage) return $"{s.Name} deals no damage.";
                if (m == "distant" && s.Range == 0 && !s.Touch) return $"{s.Name} has no range to extend.";
            }

            if (main > 1) return "Only one Metamagic option per spell (Empowered Spell can join another).";
            if (ResourceLeft(actor, "sorcery_points") < cost) return $"That needs {cost} sorcery points; {ResourceLeft(actor, "sorcery_points")} left.";
            return null;
        }

        // ===== Areas =======================================================================================

        /// <summary>
        /// The squares an area covers on the 5-ft grid, with the grid's own distance (Chebyshev, so a sphere is a square
        /// of squares). Spheres and cylinders: within the radius of the point. Cubes at a point: centred on it. From the
        /// caster: cubes adjacent toward the point; cones within the length and the cone's angle (its width equals its
        /// distance); lines straight toward the point.
        /// </summary>
        public static List<GridPos> AreaSquares(GridPos caster, GridPos point, AreaShape shape, int sizeFeet, bool fromSelf)
        {
            var list = new List<GridPos>();
            var n = Math.Max(1, sizeFeet / GridPos.Feet);
            var z = caster.Z;
            if (!fromSelf)
            {
                if (shape == AreaShape.Cube)
                {
                    var x0 = point.X - (n - 1) / 2;
                    var y0 = point.Y - (n - 1) / 2;
                    for (var x = x0; x < x0 + n; x++) for (var y = y0; y < y0 + n; y++) list.Add(new GridPos(x, y, point.Z));
                }
                else
                    for (var x = point.X - n; x <= point.X + n; x++) for (var y = point.Y - n; y <= point.Y + n; y++) list.Add(new GridPos(x, y, point.Z));
                return list;
            }

            var dx = point.X - caster.X;
            var dy = point.Y - caster.Y;
            if (dx == 0 && dy == 0) return list;
            switch (shape)
            {
                case AreaShape.Cube:
                {
                    int sx = Math.Sign(dx), sy = Math.Sign(dy);
                    var diagonal = sx != 0 && sy != 0 && Math.Abs(Math.Abs(dx) - Math.Abs(dy)) <= Math.Min(Math.Abs(dx), Math.Abs(dy));
                    if (diagonal)
                    {
                        for (var i = 1; i <= n; i++) for (var j = 1; j <= n; j++) list.Add(new GridPos(caster.X + sx * i, caster.Y + sy * j, z));
                    }
                    else if (Math.Abs(dx) >= Math.Abs(dy))
                    {
                        for (var i = 1; i <= n; i++) for (var j = -(n - 1) / 2; j <= n / 2; j++) list.Add(new GridPos(caster.X + sx * i, caster.Y + j, z));
                    }
                    else
                    {
                        for (var i = 1; i <= n; i++) for (var j = -(n - 1) / 2; j <= n / 2; j++) list.Add(new GridPos(caster.X + j, caster.Y + sy * i, z));
                    }

                    break;
                }

                case AreaShape.Cone:
                {
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    for (var x = caster.X - n; x <= caster.X + n; x++)
                        for (var y = caster.Y - n; y <= caster.Y + n; y++)
                        {
                            int ox = x - caster.X, oy = y - caster.Y;
                            if (ox == 0 && oy == 0) continue;
                            var cos = (ox * dx + oy * dy) / (Math.Sqrt(ox * ox + oy * oy) * len);
                            if (cos >= 0.89) list.Add(new GridPos(x, y, z)); // half-angle atan(1/2) ≈ 26.6°
                        }

                    break;
                }

                case AreaShape.Line:
                {
                    var m = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    double ux = dx / (double)m, uy = dy / (double)m;
                    for (var i = 1; i <= n; i++) list.Add(new GridPos(caster.X + (int)Math.Round(ux * i, MidpointRounding.AwayFromZero), caster.Y + (int)Math.Round(uy * i, MidpointRounding.AwayFromZero), z));
                    break;
                }

                default:
                    for (var x = caster.X - n; x <= caster.X + n; x++) for (var y = caster.Y - n; y <= caster.Y + n; y++) if (x != caster.X || y != caster.Y) list.Add(new GridPos(x, y, z));
                    break;
            }

            return list;
        }

        /// <summary>The creatures an area spell aimed at a point would affect (for the cast, the UI preview and the AI).</summary>
        public List<CombatantData> AreaTargets(CombatantData caster, SpellDefinition s, GridPos point)
        {
            var list = new List<CombatantData>();
            if (s.Area == AreaShape.None) return list;
            var squares = AreaSquares(caster.Position, point, s.Area, s.AreaSize, s.Targeting == SpellTargeting.FromSelf);
            foreach (var c in Encounter.Combatants)
            {
                if (c.State == VitalState.Dead || c.State == VitalState.Fled) continue;
                if (s.Targeting == SpellTargeting.FromSelf && c == caster) continue;
                foreach (var q in squares) if (q.X == c.Position.X && q.Y == c.Position.Y) { list.Add(c); break; }
            }

            return list;
        }

        // ===== Resolution ==================================================================================

        private void DoCastSpell(CombatantData actor, CastSpellAction a)
        {
            var s = SpellCatalog.Find(a.SpellId);
            var t = actor.Turn;
            var meta = new List<string>(a.Metamagic);
            var stats = Stats(actor);
            var level = s.IsCantrip || AtWill(actor, s) ? s.Level : a.SlotLevel;

            // Pay.
            var time = meta.Contains("quickened") ? CastingTime.BonusAction : s.CastingTime;
            if (time == CastingTime.Action) UseAction(actor);
            else if (time == CastingTime.BonusAction) t.BonusActionUsed = true;
            string cost;
            if (AtWill(actor, s)) cost = s.IsCantrip ? "cantrip" : "at will";
            else if (a.FreeWith != null)
            {
                SpendResource(actor, a.FreeWith, 1);
                cost = "free (Favoured Enemy)";
            }
            else
            {
                var access = Access(actor);
                SpendResource(actor, access != null ? access.SlotResource(a.SlotLevel) : "spell_slots_" + a.SlotLevel, 1);
                t.SlotSpellCast = true;
                cost = access != null && access.UsesPactMagic ? $"a level {a.SlotLevel} pact slot" : $"a level {a.SlotLevel} slot";
            }

            var points = 0;
            foreach (var m in meta) points += MetamagicCost(m, s);
            if (points > 0)
            {
                SpendResource(actor, "sorcery_points", points);
                cost += $", {points} sorcery point{(points == 1 ? string.Empty : "s")} ({string.Join(", ", meta)})";
            }

            HostileActBy(actor); // casting ends Invisibility
            var instance = NewInstance();
            if (s.Concentration) StartConcentration(actor, instance, s.Id, s.Name);
            var result = new SpellCastResult { SpellId = s.Id, Name = s.Name, CasterId = actor.CombatantId, SlotLevel = level, Cost = cost, Concentration = s.Concentration };
            _current?.Spells.Add(result);
            Log(CombatLogKind.Spell, actor.CombatantId, default, $"{actor.Name} casts {s.Name}{(level > 0 ? $" at level {level}" : string.Empty)} ({cost}{(s.Concentration ? "; concentration" : string.Empty)}).");

            // Targets.
            var targets = new List<CombatantData>();
            var perTarget = new Dictionary<CombatantId, int>();
            if (s.Targeting == SpellTargeting.Self) targets.Add(actor);
            else if (s.Targeting == SpellTargeting.Creatures)
            {
                var instances = Math.Max(1, Instances(s, level, stats.CasterLevel));
                var multi = s.Instances > 1 || s.ScalesByInstances;
                for (var i = 0; i < (multi ? instances : a.Targets.Count); i++)
                {
                    var id = a.Targets[Math.Min(i, a.Targets.Count - 1)];
                    var c = Encounter.Find(id);
                    if (!targets.Contains(c)) targets.Add(c);
                    perTarget[id] = (perTarget.TryGetValue(id, out var n) ? n : 0) + 1;
                }
            }
            else if (s.HasArea) targets.AddRange(AreaTargets(actor, s, a.Point.Value));

            // Misty Step and Expeditious Retreat act on the caster first.
            foreach (var e in s.Effects)
            {
                if (e.Kind == CombatEffectKind.Teleport && a.Point.HasValue)
                {
                    var from = actor.Position;
                    actor.Position = a.Point.Value;
                    Log(CombatLogKind.Moved, actor.CombatantId, default, $"{actor.Name} vanishes and reappears: {from} → {actor.Position} (no opportunity attacks).");
                    ReleaseBrokenGrapples();
                }

                if (e.Kind == CombatEffectKind.BonusDash)
                {
                    t.MovementGranted += Speed(actor);
                    Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} dashes (+{Speed(actor)} ft) and can dash as a bonus action while the spell lasts.");
                }
            }

            var dc = SpellSaveDc(actor);
            var careful = new HashSet<CombatantData>();
            if (meta.Contains("careful"))
                foreach (var c in targets) if (c.Side == actor.Side && careful.Count < Math.Max(1, stats.Modifier(Ability.Charisma))) careful.Add(c);
            var sculpt = new HashSet<CombatantData>();
            if (AttackerFeature(actor, "sculpt_spells") && s.School == SpellSchool.Evocation && s.HasArea)
                foreach (var c in targets) if (c.Side == actor.Side && c != actor && sculpt.Count < 1 + s.Level) sculpt.Add(c);
            var heightenedFor = meta.Contains("heightened") ? targets.Find(c => Hostile(actor, c)) : null;
            var empoweredEvocationUsed = false;

            foreach (var target in targets)
            {
                var tr = new SpellTargetResult { TargetId = target.CombatantId, Name = target.Name };
                result.Targets.Add(tr);
                if (Hostile(actor, target)) t.AttackedOrForcedSave = true;
                var damage = new Dictionary<DamageType, int>();
                var critical = false;
                var applyEffects = true;

                void AddDamage(SpellEffect e, bool crit, double factor, string why)
                {
                    var n = e.Dice.Count;
                    if (s.IsCantrip && s.CantripScaling && !s.ScalesByInstances) n *= CantripTier(stats.CasterLevel);
                    if (!s.IsCantrip && level > s.Level) n += e.PerSlotAbove.Count * (level - s.Level);
                    if (crit) n *= 2;
                    if (s.AfterMeleeHit && t.LastHitCritical) n *= 2;
                    if (s.ExtraDiceAgainstFiendsAndUndead > 0 && (TypeOf(target) == "Fiend" || TypeOf(target) == "Undead")) n += s.ExtraDiceAgainstFiendsAndUndead;
                    var rolls = new List<int>();
                    for (var i = 0; i < n; i++) rolls.Add(Die(e.Dice.Sides));
                    if (meta.Contains("empowered"))
                    {
                        var rerolls = Math.Max(1, stats.Modifier(Ability.Charisma));
                        var avg = (e.Dice.Sides + 1) / 2;
                        for (var i = 0; i < rolls.Count && rerolls > 0; i++) if (rolls[i] < avg) { rolls[i] = Die(e.Dice.Sides); rerolls--; }
                    }

                    var sum = e.Dice.Bonus;
                    foreach (var r in rolls) sum += r;
                    var parts = new List<string> { $"{n}d{e.Dice.Sides} [{string.Join(", ", rolls)}]" };
                    if (e.Dice.Bonus != 0) parts.Add(Breakdown.Signed(e.Dice.Bonus));
                    if (s.Id == "eldritch_blast" && HasInvocation(actor, "agonizing_blast"))
                    {
                        var cha = stats.Modifier(Ability.Charisma);
                        sum += cha;
                        parts.Add($"Agonizing Blast {Breakdown.Signed(cha)}");
                    }

                    if (!empoweredEvocationUsed && s.School == SpellSchool.Evocation && AttackerFeature(actor, "empowered_evocation"))
                    {
                        var intl = stats.Modifier(Ability.Intelligence);
                        sum += intl;
                        empoweredEvocationUsed = true;
                        parts.Add($"Empowered Evocation {Breakdown.Signed(intl)}");
                    }

                    var final = (int)(Math.Max(0, sum) * factor);
                    tr.RawDamage += Math.Max(0, sum);
                    damage[e.DamageType] = (damage.TryGetValue(e.DamageType, out var d0) ? d0 : 0) + final;
                    Log(CombatLogKind.Damage, actor.CombatantId, target.CombatantId, $"{s.Name} → {target.Name}: {string.Join(" ", parts)} = {Math.Max(0, sum)}{(factor < 1 ? $", {why}: {final}" : string.Empty)} {e.DamageType.ToString().ToLowerInvariant()}.", final);
                }

                switch (s.Resolution)
                {
                    case SpellResolution.Attack:
                    {
                        var shots = perTarget.TryGetValue(target.CombatantId, out var k) ? k : 1;
                        var option = stats.SpellAttackOption(s, meta.Contains("distant") ? 2 : 1);
                        var anyHit = false;
                        for (var i = 0; i < shots; i++)
                        {
                            var roll = AttackRoll(actor, target, option, false);
                            tr.Attacks.Add(roll);
                            if (roll.Hit)
                            {
                                anyHit = true;
                                critical |= roll.Critical;
                                foreach (var e in s.Effects) if (e.Kind == CombatEffectKind.Damage) AddDamage(e, roll.Critical, 1, null);
                            }
                            else if (s.IsCantrip && AttackerFeature(actor, "potent_cantrip"))
                                foreach (var e in s.Effects) if (e.Kind == CombatEffectKind.Damage) AddDamage(e, false, 0.5, "Potent Cantrip: half on a miss");
                        }

                        applyEffects = anyHit;
                        break;
                    }

                    case SpellResolution.Save:
                    {
                        SaveResult save;
                        if (careful.Contains(target) || sculpt.Contains(target))
                        {
                            save = new SaveResult { Ability = s.SaveAbility, Dc = dc, Success = true, AutomaticFailure = null, Roll = new D20Roll(), Bonus = new Breakdown() };
                            Log(CombatLogKind.Save, target.CombatantId, actor.CombatantId, $"{target.Name} is shielded from {s.Name} ({(sculpt.Contains(target) ? "Sculpt Spells" : "Careful Spell")}): an automatic success{(sculpt.Contains(target) ? " and no damage" : string.Empty)}.");
                        }
                        else save = SavingThrow(target, s.SaveAbility, dc, $"{actor.Name}'s {s.Name}", target == heightenedFor);

                        tr.Save = save;
                        var evasion = s.SaveAbility == Ability.Dexterity && (TargetFeature(target, "evasion")) && !Incapacitated(target);
                        foreach (var e in s.Effects)
                        {
                            if (e.Kind != CombatEffectKind.Damage) continue;
                            if (sculpt.Contains(target) && e.HalfOnSave) continue;
                            if (save.Success)
                            {
                                if (e.HalfOnSave && !evasion) AddDamage(e, false, 0.5, "half on a successful save");
                                else if (e.HalfOnSave && evasion) Log(CombatLogKind.Save, target.CombatantId, default, $"{target.Name}'s Evasion: no damage on a successful save.");
                                else if (s.IsCantrip && AttackerFeature(actor, "potent_cantrip")) AddDamage(e, false, 0.5, "Potent Cantrip: half on a save");
                            }
                            else AddDamage(e, false, evasion && e.HalfOnSave ? 0.5 : 1, "Evasion: half on a failed save");
                        }

                        applyEffects = !save.Success;
                        break;
                    }

                    default:
                    {
                        var shots = perTarget.TryGetValue(target.CombatantId, out var k) ? k : 1;
                        for (var i = 0; i < shots; i++)
                            foreach (var e in s.Effects) if (e.Kind == CombatEffectKind.Damage) AddDamage(e, false, 1, null);
                        break;
                    }
                }

                foreach (var pair in damage)
                {
                    if (target.State == VitalState.Dead) break;
                    var dr = ApplyDamage(target, pair.Value, pair.Key, critical, actor.Name);
                    tr.Damage += dr.Final;
                }

                // Healing, then every other effect (on a hit, a failed save, or always for spells without a roll).
                foreach (var e in s.Effects)
                {
                    if (e.Kind == CombatEffectKind.Healing)
                    {
                        var n = e.Dice.Count + (level > s.Level ? e.PerSlotAbove.Count * (level - s.Level) : 0);
                        var rolls = RollDice(n, e.Dice.Sides, out var sum);
                        var mod = e.AddAbilityModifier ? stats.Modifier(stats.SpellAbility) : 0;
                        var life = AttackerFeature(actor, "disciple_of_life") && s.Level >= 1 ? 2 + level : 0;
                        var amount = Math.Max(1, sum + mod + life);
                        Log(CombatLogKind.Healing, actor.CombatantId, target.CombatantId, $"{s.Name} → {target.Name}: {n}d{e.Dice.Sides} [{rolls}] {Breakdown.Signed(mod)}{(life > 0 ? $" Disciple of Life +{life}" : string.Empty)} = {amount}.", amount);
                        var before = Hp(target);
                        Heal(target, amount, s.Name);
                        tr.Healing += Hp(target) - before;
                        continue;
                    }

                    if (e.Kind == CombatEffectKind.Damage || e.Kind == CombatEffectKind.Teleport || e.Kind == CombatEffectKind.BonusDash && target != actor) continue;
                    if (!applyEffects && s.Resolution != SpellResolution.Automatic) continue;
                    if (target.State == VitalState.Dead) continue;
                    var text = ApplySpellEffect(actor, target, s, e, level, instance, dc);
                    if (text != null) tr.Effects.Add(text);
                }
            }

            // A concentration spell that took hold nowhere ends at once.
            if (s.Concentration && actor.Concentration?.InstanceId == instance)
            {
                var held = false;
                foreach (var x in Encounter.Effects) if (x.InstanceId == instance) held = true;
                foreach (var c in Encounter.Combatants) foreach (var k in c.Conditions) if (k.InstanceId == instance) held = true;
                if (!held) EndConcentration(actor, "the spell took hold on no one");
            }
        }

        /// <summary>Applies one non-damage effect of a spell to a target; returns what happened, in words.</summary>
        private string ApplySpellEffect(CombatantData caster, CombatantData target, SpellDefinition s, SpellEffect e, int level, int instance, int dc)
        {
            var origin = "spell:" + s.Id;
            var (expiry, on, round) = ExpiryFor(s, e, caster, target);
            switch (e.Kind)
            {
                case CombatEffectKind.Condition:
                {
                    var c = e.Condition.Value;
                    var duration = expiry == EffectExpiry.StartOfTurn ? ConditionDuration.StartOfTurn : expiry == EffectExpiry.EndOfTurn ? ConditionDuration.EndOfTurn : ConditionDuration.UntilRemoved;
                    var why = ApplyCondition(target, c, caster.CombatantId, s.Name, duration, on?.CombatantId ?? default, round, 1, e.EscapeWithStrengthCheck ? dc : 0);
                    if (why != null) return why;
                    foreach (var k in target.Conditions)
                        if (k.Type == c)
                        {
                            k.InstanceId = instance;
                            k.RepeatSave = e.RepeatSaveEachTurn;
                            k.SaveDc = dc;
                            k.SaveAbility = s.SaveAbility;
                            k.EscapeByCheck = e.EscapeWithStrengthCheck;
                        }

                    AddEffect(instance, origin, s.Name, caster, target, CombatEffectKind.Condition, (int)c, spellLevel: level, concentration: s.Concentration, expiry: expiry, expiresOn: on, expiresRound: round);
                    return $"{target.Name} is {Describe(c)}";
                }

                case CombatEffectKind.RemoveCondition:
                    foreach (var c in e.Conditions)
                        if (Has(target, c))
                        {
                            RemoveCondition(target, c, s.Name);
                            return $"{target.Name} is no longer {Describe(c)}";
                        }

                    return $"{target.Name} had nothing {s.Name} could end";

                case CombatEffectKind.HitPointMaximumBonus:
                {
                    var v = e.Value + (level > s.Level ? e.PerSlotAbove.Bonus * (level - s.Level) : 0);
                    if (target.Kind == CombatantKind.Player)
                    {
                        CharacterVitals.GrantTemporaryHitPoints(_world.Player.Rpg, v);
                        return $"{target.Name} gains {v} temporary hit points (the player's maximum is not raised)";
                    }

                    target.MaxHp += v;
                    if (target.State == VitalState.Conscious) target.CurrentHp += v;
                    AddEffect(instance, origin, s.Name, caster, target, CombatEffectKind.HitPointMaximumBonus, v, spellLevel: level);
                    return $"{target.Name}'s hit point maximum rises by {v}";
                }

                case CombatEffectKind.TemporaryHitPoints:
                    if (target.Kind == CombatantKind.Player) CharacterVitals.GrantTemporaryHitPoints(_world.Player.Rpg, e.Value);
                    else target.TemporaryHp = Math.Max(target.TemporaryHp, e.Value);
                    return $"{target.Name} gains {e.Value} temporary hit points";

                case CombatEffectKind.Push:
                    PushAway(caster.Position, target, e.Value);
                    return $"{target.Name} is pushed back";

                case CombatEffectKind.EndSpells:
                {
                    var limit = Math.Max(e.Value, level);
                    var ended = new List<int>();
                    foreach (var x in EffectsOn(target))
                        if (x.Origin != null && x.Origin.StartsWith("spell:", StringComparison.Ordinal) && x.SpellLevel <= limit && !ended.Contains(x.InstanceId)) ended.Add(x.InstanceId);
                    foreach (var i in ended)
                    {
                        foreach (var c in Encounter.Combatants) if (c.Concentration?.InstanceId == i) c.Concentration = null;
                        EndInstance(i, "dispelled");
                    }

                    return ended.Count == 0 ? $"no spell of level {limit} or lower affected {target.Name}" : $"{ended.Count} spell{(ended.Count == 1 ? string.Empty : "s")} on {target.Name} ended";
                }

                default:
                {
                    var dice = e.Dice;
                    AddEffect(instance, origin, s.Name, caster, target, e.Kind, e.Value, dice, e.DamageType, level, s.Concentration, expiry, on, round,
                        singleUse: e.Kind == CombatEffectKind.NextAttackAgainstHasAdvantage || e.Kind == CombatEffectKind.NextAttackHasDisadvantage);
                    return $"{s.Name} affects {target.Name}";
                }
            }
        }

        private (EffectExpiry, CombatantData, int) ExpiryFor(SpellDefinition s, SpellEffect e, CombatantData caster, CombatantData target)
        {
            var round = Encounter.Round;
            if (e.UntilTargetsNextTurnEnds)
            {
                var targetIndex = Encounter.Order.IndexOf(target.CombatantId);
                return (EffectExpiry.EndOfTurn, target, targetIndex > Encounter.TurnIndex ? round : round + 1);
            }

            if (e.UntilCastersNextTurnStarts || s.Duration == SpellDurationKind.UntilCasterNextTurn) return (EffectExpiry.StartOfTurn, caster, round + 1);
            if (e.UntilCastersNextTurnEnds) return (EffectExpiry.EndOfTurn, caster, round + 1);
            if (s.Duration == SpellDurationKind.Timed && s.DurationRounds > 0) return (EffectExpiry.StartOfTurn, caster, round + s.DurationRounds);
            return (EffectExpiry.None, null, 0);
        }

        /// <summary>A restraint the holder can try to break with its action (Entangle), or null.</summary>
        private ConditionInstance EscapableRestraint(CombatantData c)
        {
            foreach (var k in c.Conditions) if (k.EscapeByCheck && k.EscapeDc > 0) return k;
            return null;
        }

        internal static string Format(int n) => n.ToString(CultureInfo.InvariantCulture);
    }
}
