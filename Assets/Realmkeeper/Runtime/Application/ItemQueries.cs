using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Commerce;

namespace Realmkeeper.Application
{
    // Phase 11 (ADR-0055): what the inventory, container, comparison and crafting screens show. Read-only views built
    // from the world and the shared rules engine: the numbers on screen are the numbers the rules use.

    public enum InventorySort
    {
        Category = 0,
        Name = 1,
        Weight = 2,
        Value = 3,
        Recent = 4,
    }

    /// <summary>One instance as the inventory shows it.</summary>
    public sealed class InventoryRow
    {
        public ItemStackId StackId { get; internal set; }
        public string ItemId { get; internal set; }
        public string Name { get; internal set; }
        public string Category { get; internal set; }
        public string Group { get; internal set; }
        public int CategoryOrder { get; internal set; }
        public int Quantity { get; internal set; }
        public int MaxStack { get; internal set; }
        public int UnitGrams { get; internal set; }
        public int TotalGrams { get; internal set; }

        /// <summary>Base value of one, scaled by condition and quality (what it is worth, not what a shop pays).</summary>
        public int UnitValue { get; internal set; }
        public ItemRarity Rarity { get; internal set; }
        public ItemCondition Condition { get; internal set; }
        public bool Wears { get; internal set; }
        public ItemQuality Quality { get; internal set; }
        public int Charges { get; internal set; }
        public int MaxCharges { get; internal set; }
        public bool IsMagic { get; internal set; }
        public bool RequiresAttunement { get; internal set; }
        public bool Attuned { get; internal set; }
        public bool Bound { get; internal set; }
        public EquipmentSlot EquippedIn { get; internal set; }
        public bool CanEquip { get; internal set; }

        /// <summary>The verb for using it outside a fight ("Drink", "Eat"), or null.</summary>
        public string UseVerb { get; internal set; }
        public long AcquiredAt { get; internal set; }
        public string Visual { get; internal set; }

        /// <summary>A short line under the name: condition, quality, charges, attunement.</summary>
        public string Badges { get; internal set; }
    }

    public sealed class InventoryPage
    {
        public List<InventoryRow> Rows { get; } = new List<InventoryRow>();
        public Dictionary<EquipmentSlot, InventoryRow> Equipped { get; } = new Dictionary<EquipmentSlot, InventoryRow>();

        /// <summary>Filter groups with how many rows each holds before filtering.</summary>
        public List<(string Group, int Count)> Groups { get; } = new List<(string, int)>();
        public int TotalRows { get; internal set; }
        public int CarriedGrams { get; internal set; }
        public int CapacityGrams { get; internal set; }
        public int LimitGrams { get; internal set; }
        public bool Encumbered { get; internal set; }
        public int Attuned { get; internal set; }
        public int MaxAttuned => Carrying.MaxAttuned;
        public int Purse { get; internal set; }
        public int PossessionsValue { get; internal set; }

        /// <summary>The player's armour class and main attack as the rules compute them (never Phase 5's protection numbers).</summary>
        public int ArmorClass { get; internal set; }
        public string ArmorClassText { get; internal set; }
        public string AttackText { get; internal set; }
        public WorkView ActiveWork { get; internal set; }
    }

    public sealed class ItemInspection
    {
        public string Name { get; internal set; }
        public string Subtitle { get; internal set; }
        public string Description { get; internal set; }
        public string Lore { get; internal set; }
        public List<(string Label, string Value)> Lines { get; } = new List<(string, string)>();
        public List<string> Warnings { get; } = new List<string>();
        public string Source { get; internal set; }
    }

    public sealed class ComparisonLine
    {
        public string Label { get; internal set; }
        public string Before { get; internal set; }
        public string After { get; internal set; }

        /// <summary>+1 better, −1 worse, 0 the same or not comparable.</summary>
        public int Better { get; internal set; }
    }

    public sealed class ItemComparison
    {
        public string Candidate { get; internal set; }
        public string Current { get; internal set; }
        public EquipmentSlot Slot { get; internal set; }
        public List<ComparisonLine> Lines { get; } = new List<ComparisonLine>();
        public List<string> Notes { get; } = new List<string>();
    }

    public sealed class ContainerView
    {
        public string Key { get; internal set; }
        public string Name { get; internal set; }
        public ContainerKind Kind { get; internal set; }

        /// <summary>The plan building it stands at, or null.</summary>
        public string LayoutKey { get; internal set; }
        public int Coin { get; internal set; }
        public bool CanDeposit { get; internal set; }
        public bool Reachable { get; internal set; }
        public string Problem { get; internal set; }
        public int Grams { get; internal set; }
        public int CapacityGrams { get; internal set; }
        public bool Opened { get; internal set; }
        public List<InventoryRow> Rows { get; } = new List<InventoryRow>();
    }

    public sealed class RecipeView
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string Discipline { get; internal set; }
        public List<(string Name, int Need, int Have)> Inputs { get; } = new List<(string, int, int)>();
        public string Tool { get; internal set; }
        public bool HasTool { get; internal set; }
        public bool Proficient { get; internal set; }
        public string Station { get; internal set; }
        public bool HasStation { get; internal set; }
        public int Minutes { get; internal set; }
        public int Dc { get; internal set; }
        public int Bonus { get; internal set; }
        public Ability Ability { get; internal set; }

        /// <summary>Chance of success in percent (d20 + bonus ≥ DC).</summary>
        public int ChancePercent { get; internal set; }
        public string Output { get; internal set; }
        public string Problem { get; internal set; }
        public bool CanStart => Problem == null;
    }

    public sealed class SiteView
    {
        public string Key { get; internal set; }
        public string Name { get; internal set; }
        public string Item { get; internal set; }
        public int Available { get; internal set; }
        public int Capacity { get; internal set; }
        public int Yield { get; internal set; }
        public int Minutes { get; internal set; }
        public string Tool { get; internal set; }
        public string Problem { get; internal set; }
    }

    public sealed class WorkView
    {
        public string Key { get; internal set; }
        public WorkKind Kind { get; internal set; }
        public string What { get; internal set; }
        public long MinutesLeft { get; internal set; }
        public bool Ready => MinutesLeft <= 0;
        public string Where { get; internal set; }
    }

    public sealed class WorkshopView
    {
        public List<RecipeView> Recipes { get; } = new List<RecipeView>();
        public List<SiteView> Sites { get; } = new List<SiteView>();

        /// <summary>Things in the pack the player could mend themselves (with the DC and their bonus).</summary>
        public List<(InventoryRow Row, string Discipline, int Dc, int Bonus, string Problem)> Repairs { get; } = new List<(InventoryRow, string, int, int, string)>();

        /// <summary>Carried magic items awaiting attunement.</summary>
        public List<(InventoryRow Row, string Problem)> Attunable { get; } = new List<(InventoryRow, string)>();
        public WorkView Active { get; internal set; }
        public List<string> RecentOutcomes { get; } = new List<string>();
        public bool InSettlement { get; internal set; }
    }

    public sealed partial class WorldSession
    {
        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        internal static InventoryRow Row(ItemStackData s, EquipmentSlot slot)
        {
            var d = ItemCatalog.Find(s.ItemId);
            if (d == null) return null;
            var cat = ItemCategories.Of(d.Category);
            var badges = new List<string>();
            if (d.Wears) badges.Add(ItemConditions.Name(s.Condition));
            if (s.Quality == ItemQuality.Fine) badges.Add("fine");
            else if (s.Quality == ItemQuality.Poor) badges.Add("poor");
            if (d.IsMagic && d.Rarity > ItemRarity.Common) badges.Add(Rarity(d.Rarity));
            if (d.Magic?.MaxCharges > 0) badges.Add($"{Math.Max(0, s.Charges)}/{d.Magic.MaxCharges} charges");
            if (d.Magic?.RequiresAttunement == true) badges.Add(s.Attuned ? "attuned" : "needs attunement");
            if (s.Bound) badges.Add("cursed — bound to you");
            return new InventoryRow
            {
                StackId = s.StackId, ItemId = d.Id, Name = d.Name, Category = cat.Name, Group = d.IsMagic ? "Magic" : cat.Group, CategoryOrder = cat.Order,
                Quantity = s.Quantity, MaxStack = d.Stackable ? d.MaxStack : 1, UnitGrams = d.WeightGrams, TotalGrams = d.WeightGrams * s.Quantity,
                UnitValue = Math.Max(d.BaseValue > 0 ? 1 : 0, d.BaseValue * ItemStacks.ValuePercent(s) / 100), Rarity = d.Rarity, Condition = s.Condition, Wears = d.Wears,
                Quality = s.Quality, Charges = s.Charges, MaxCharges = d.Magic?.MaxCharges ?? 0, IsMagic = d.IsMagic, RequiresAttunement = d.Magic?.RequiresAttunement == true,
                Attuned = s.Attuned, Bound = s.Bound, EquippedIn = slot, CanEquip = d.IsEquippable,
                UseVerb = d.Consumable != null && (!d.Consumable.Healing.IsEmpty || d.Consumable.Nourishment > 0) ? d.Consumable.Verb : d.Nourishment > 0 ? "Eat" : null,
                AcquiredAt = s.AcquiredAt, Visual = d.Visual, Badges = string.Join(" · ", badges),
            };
        }

        public static string Rarity(ItemRarity r) => r == ItemRarity.VeryRare ? "very rare" : r.ToString().ToLowerInvariant();

        /// <summary>The inventory, filtered by group and search text and sorted (ADR-0055). Equipped items are listed apart.</summary>
        public InventoryPage GetInventory(string group = null, string search = null, InventorySort sort = InventorySort.Category, bool descending = false)
        {
            var p = _world?.Player;
            if (p == null) return null;
            var page = new InventoryPage
            {
                CarriedGrams = ItemWeights.CarriedGrams(p), CapacityGrams = PlayerMovement.UnencumberedFor(p), LimitGrams = ItemCommandHandlers.PackLimitGrams(p),
                Attuned = ItemCommandHandlers.AttunedCount(p), Purse = p.Purse,
            };
            page.Encumbered = page.CarriedGrams > page.CapacityGrams;
            var all = new List<InventoryRow>();
            foreach (var s in p.Inventory)
            {
                var r = Row(s, EquipmentSlot.None);
                if (r == null) continue;
                all.Add(r);
                page.PossessionsValue += r.UnitValue * r.Quantity;
            }

            foreach (var pair in p.Equipment)
            {
                var r = Row(pair.Value, pair.Key);
                if (r == null) continue;
                page.Equipped[pair.Key] = r;
                page.PossessionsValue += r.UnitValue;
            }

            var counts = new Dictionary<string, int>();
            foreach (var r in all) counts[r.Group] = (counts.TryGetValue(r.Group, out var n) ? n : 0) + 1;
            foreach (var g in ItemCategories.Groups) if (counts.TryGetValue(g, out var n)) page.Groups.Add((g, n));
            page.TotalRows = all.Count;

            var needle = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();
            foreach (var r in all)
            {
                if (group != null && r.Group != group) continue;
                if (needle != null && !Matches(r, needle)) continue;
                page.Rows.Add(r);
            }

            Comparison<InventoryRow> by;
            switch (sort)
            {
                case InventorySort.Name: by = (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); break;
                case InventorySort.Weight: by = (a, b) => a.TotalGrams.CompareTo(b.TotalGrams); break;
                case InventorySort.Value: by = (a, b) => (a.UnitValue * a.Quantity).CompareTo(b.UnitValue * b.Quantity); break;
                case InventorySort.Recent: by = (a, b) => a.AcquiredAt.CompareTo(b.AcquiredAt); break;
                default: by = (a, b) => a.CategoryOrder != b.CategoryOrder ? a.CategoryOrder.CompareTo(b.CategoryOrder) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); break;
            }

            // Ties fall back to name then instance ID so the order is stable from frame to frame.
            page.Rows.Sort((a, b) =>
            {
                var c = descending ? by(b, a) : by(a, b);
                if (c != 0) return c;
                c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : a.StackId.Value.CompareTo(b.StackId.Value);
            });

            if (p.Rpg != null && p.Rpg.State == CreationState.Complete)
            {
                var stats = CombatStats.For(_world, new CombatantData { Kind = CombatantKind.Player, Name = p.Name });
                page.ArmorClass = stats.ArmorClass.Total;
                page.ArmorClassText = Explain(stats.ArmorClass);
                var main = stats.Attacks.Count > 0 ? stats.Attacks[0] : null;
                if (main != null) page.AttackText = $"{main.Name}: {Signed(main.AttackBonus.Total)} to hit, {main.Damage}{(main.DamageBonus.Total != 0 ? Signed(main.DamageBonus.Total) : string.Empty)} {main.DamageType.ToString().ToLowerInvariant()}";
            }

            page.ActiveWork = GetWork();
            return page;
        }

        private static bool Matches(InventoryRow r, string needle)
        {
            if (r.Name.ToLowerInvariant().Contains(needle) || r.Category.ToLowerInvariant().Contains(needle) || r.Badges.ToLowerInvariant().Contains(needle)) return true;
            var d = ItemCatalog.Find(r.ItemId);
            if (d == null) return false;
            foreach (var t in d.Tags) if (t.ToLowerInvariant().Contains(needle)) return true;
            return (d.Description ?? string.Empty).ToLowerInvariant().Contains(needle);
        }

        private static string Signed(int v) => v >= 0 ? "+" + I(v) : "−" + I(-v);

        private static string Explain(Breakdown b)
        {
            var parts = new List<string>();
            foreach (var (label, value) in b.Parts) parts.Add($"{label} {Signed(value)}");
            return string.Join(", ", parts);
        }

        /// <summary>The instance with this ID the player holds, in the pack or worn.</summary>
        private ItemStackData PlayerStack(ItemStackId id, out EquipmentSlot slot)
        {
            slot = EquipmentSlot.None;
            var p = _world?.Player;
            return p == null ? null : ItemCommandHandlers.Held(p, id, out slot);
        }

        /// <summary>Everything about one instance (ADR-0055): its rules, state, value, weight and where it came from.</summary>
        public ItemInspection InspectItem(ItemStackId id)
        {
            var s = PlayerStack(id, out var slot);
            if (s == null)
                foreach (var c in _world?.Containers ?? new List<ContainerData>())
                    foreach (var x in c.Items)
                        if (x.StackId == id) s = x;
            if (s == null) return null;
            return Inspect(s, slot);
        }

        internal static ItemInspection Inspect(ItemStackData s, EquipmentSlot slot)
        {
            var d = ItemCatalog.Find(s.ItemId);
            if (d == null) return null;
            var cat = ItemCategories.Of(d.Category);
            var i = new ItemInspection
            {
                Name = d.Name,
                Subtitle = $"{(d.Subcategory ?? cat.Name)}{(d.IsMagic ? ", " + Rarity(d.Rarity) : string.Empty)}{(d.Magic?.RequiresAttunement == true ? " (requires attunement)" : string.Empty)}",
                Description = d.Description, Lore = d.Lore,
                Source = d.Source == ContentSource.Srd521 ? "SRD 5.2.1 (CC BY 4.0)" : "Realmkeeper original",
            };
            if (d.Weapon != null)
            {
                var w = d.Weapon;
                i.Lines.Add(("Damage", $"{w.Damage} {w.DamageType.ToString().ToLowerInvariant()}{(w.Versatile.IsEmpty ? string.Empty : $" ({w.Versatile} two-handed)")}"));
                i.Lines.Add(("Weapon", $"{(w.Simple ? "simple" : "martial")} {(w.Ranged ? "ranged" : "melee")} ({w.Kind})"));
                var props = w.Properties.ToString();
                if (props != "None") i.Lines.Add(("Properties", props.Replace(", ", " · ")));
                if (w.Range > 0) i.Lines.Add(("Range", $"{w.Range}/{w.LongRange} ft"));
                if (!string.IsNullOrEmpty(w.Mastery)) i.Lines.Add(("Mastery", w.Mastery));
            }

            if (d.Armor != null)
            {
                var a = d.Armor;
                i.Lines.Add(("Armour", a.Category == ArmorCategory.Shield ? $"shield, +{a.BaseAc} AC" : $"{a.Category.ToString().ToLowerInvariant()}, AC {a.BaseAc}{(a.Category == ArmorCategory.Light ? " + Dex" : a.Category == ArmorCategory.Medium ? " + Dex (max 2)" : string.Empty)}"));
                if (a.StrengthRequirement > 0) i.Lines.Add(("Strength", $"{a.StrengthRequirement} or speed −10 ft"));
                if (a.StealthDisadvantage) i.Lines.Add(("Stealth", "disadvantage"));
            }

            if (d.AmmunitionFor.Count > 0) i.Lines.Add(("Ammunition for", string.Join(", ", d.AmmunitionFor)));
            if (d.ToolDiscipline != null) i.Lines.Add(("Tool", d.ToolDiscipline));
            if (d.MaterialFor.Count > 0) i.Lines.Add(("Used in", string.Join(", ", d.MaterialFor)));
            if (d.Consumable != null && !d.Consumable.Healing.IsEmpty) i.Lines.Add(("Heals", $"{d.Consumable.Healing} hit points{(d.Consumable.InCombat != null ? $" ({d.Consumable.InCombat} in a fight)" : string.Empty)}"));
            var nourish = d.Nourishment > 0 ? d.Nourishment : d.Consumable?.Nourishment ?? 0;
            if (nourish > 0) i.Lines.Add(("Nourishes", $"+{I(nourish)} fed"));
            var m = d.Magic;
            if (m != null)
            {
                if (m.WeaponBonus != 0) i.Lines.Add(("Magic", $"+{I(m.WeaponBonus)} to attack and damage rolls"));
                if (m.AcBonus != 0) i.Lines.Add(("Magic", $"+{I(m.AcBonus)} to AC while {(d.Armor != null ? "worn" : "worn")}"));
                if (m.SaveBonus != 0) i.Lines.Add(("Magic", $"+{I(m.SaveBonus)} to saving throws"));
                foreach (var e in m.Effects) i.Lines.Add(("Magic", Describe(e)));
                if (m.SpellId != null) i.Lines.Add(("Casts", $"{Domain.Magic.SpellCatalog.Find(m.SpellId)?.Name ?? m.SpellId} (1 charge per spell level)"));
                if (m.MaxCharges > 0) i.Lines.Add(("Charges", $"{I(Math.Max(0, s.Charges))} of {I(m.MaxCharges)}; regains {m.Recharge} after a long rest"));
                if (m.RequiresAttunement) i.Lines.Add(("Attunement", s.Attuned ? "attuned" : "not attuned: its magic is dormant"));
                if (m.Curse != null && s.Attuned) i.Warnings.Add("Cursed: " + m.Curse);
                if (m.StatusNote != null) i.Warnings.Add(m.StatusNote);
            }

            if (d.Wears) i.Lines.Add(("Condition", $"{ItemConditions.Name(s.Condition)} — {ItemConditions.Effect(s.Condition, d.Weapon != null)}"));
            if (s.Quality != ItemQuality.Common) i.Lines.Add(("Make", s.Quality == ItemQuality.Fine ? "fine (worth half again as much)" : "poor (worth half as much)"));
            if (s.Bound) i.Warnings.Add("A curse binds it to you: it cannot be taken off until a shrine's priest lifts it.");
            i.Lines.Add(("Weight", $"{FormatWeight(d.WeightGrams)}{(s.Quantity > 1 ? $" each ({FormatWeight(d.WeightGrams * s.Quantity)} in all)" : string.Empty)}"));
            i.Lines.Add(("Value", $"{I(Math.Max(0, d.BaseValue * ItemStacks.ValuePercent(s) / 100))} coin{(s.Quantity > 1 ? " each" : string.Empty)}{(ItemStacks.ValuePercent(s) != 100 ? $" (base {I(d.BaseValue)})" : string.Empty)}"));
            if (slot != EquipmentSlot.None) i.Lines.Add(("Equipped", PlayerCommandHandlers.Slot(slot)));
            if (!string.IsNullOrEmpty(s.Provenance)) i.Lines.Add(("Provenance", s.Provenance));
            return i;
        }

        public static string FormatWeight(int grams) => grams >= 1000 ? (grams / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + " kg" : I(grams) + " g";

        private static string Describe(RuleEffect e)
        {
            switch (e.Kind)
            {
                case EffectKind.AbilityMinimum: return $"Your {e.Key} score is {e.Value} (unless it is already higher)";
                case EffectKind.Darkvision: return $"Darkvision {e.Value} ft";
                case EffectKind.SaveBonus: return $"+{e.Value} to saving throws";
                case EffectKind.MaxHpPerLevel: return $"+{e.Value} hit point maximum per level";
                case EffectKind.Advantage: return $"Advantage on {e.Key} checks";
                default: return $"{e.Kind} {e.Key} {(e.Value != 0 ? e.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}".Trim();
            }
        }

        /// <summary>
        /// Compares wearing an item with what is worn now, by running the rules engine on both (ADR-0055): armour class,
        /// the main attack, speed, saving throws. A ring is compared on the left hand unless a hand is given.
        /// </summary>
        public ItemComparison CompareItem(ItemStackId id, EquipmentSlot slot = EquipmentSlot.None)
        {
            var p = _world?.Player;
            if (p == null || p.Rpg == null || p.Rpg.State != CreationState.Complete) return null;
            var i = PlayerCommandHandlers.IndexOf(p, id);
            if (i < 0) return null;
            return Compare(p, p.Inventory[i], slot, id);
        }

        /// <summary>
        /// Phase 26: the same comparison for an item the player does not own yet, such as a shop's stock: a fresh item of that kind
        /// (the given condition) worn on a copy of the character. Nothing in the world changes. Null when it cannot be worn.
        /// </summary>
        public ItemComparison CompareCatalogItem(string itemId, ItemCondition condition = ItemCondition.Good, EquipmentSlot slot = EquipmentSlot.None)
        {
            var p = _world?.Player;
            if (p == null || p.Rpg == null || p.Rpg.State != CreationState.Complete || ItemCatalog.Find(itemId) == null) return null;
            return Compare(p, new ItemStackData { ItemId = itemId, Quantity = 1, Condition = condition }, slot, default);
        }

        private ItemComparison Compare(PlayerCharacterData p, ItemStackData s, EquipmentSlot slot, ItemStackId owned)
        {
            var id = owned;
            var d = ItemCatalog.Find(s.ItemId);
            if (d == null || !d.IsEquippable) return null;
            var target = PlayerCommandHandlers.TargetSlot(p, d, slot, out var why);
            var cmp = new ItemComparison { Candidate = d.Name, Slot = target };
            if (target == EquipmentSlot.None)
            {
                cmp.Notes.Add(why);
                return cmp;
            }

            var current = p.Equipped(target);
            cmp.Current = current == null ? "nothing" : ItemCatalog.Find(current.ItemId)?.Name;

            // The same character with the candidate on: a copy, changed the way equipping changes it.
            var copy = p.Clone();
            ItemStackData wear;
            if (id.IsEmpty) wear = s.Clone(); // not owned: a fresh one, as if bought
            else
            {
                var ci = PlayerCommandHandlers.IndexOf(copy, id);
                wear = copy.Inventory[ci].Clone();
                copy.Inventory.RemoveAt(ci);
            }

            wear.Quantity = 1;
            foreach (var x in PlayerCommandHandlers.Displaced(copy, d, target))
            {
                var off = copy.Equipped(x);
                if (off == null) continue;
                copy.Equipment.Remove(x);
                copy.Inventory.Add(off);
            }

            copy.Equipment[target] = wear;
            var before = Snapshot(_world, p);
            var after = Snapshot(_world, copy);
            void Line(string label, int b, int a, bool higherIsBetter = true) =>
                cmp.Lines.Add(new ComparisonLine { Label = label, Before = I(b), After = I(a), Better = a == b ? 0 : (a > b) == higherIsBetter ? 1 : -1 });
            Line("Armour class", before.Ac, after.Ac);
            if (before.Attack != after.Attack || before.AttackBonus != after.AttackBonus || d.Weapon != null)
                cmp.Lines.Add(new ComparisonLine
                {
                    Label = "Main attack", Before = before.Attack, After = after.Attack,
                    Better = after.Average > before.Average ? 1 : after.Average < before.Average ? -1 : after.AttackBonus.CompareTo(before.AttackBonus),
                });
            Line("Speed (ft)", before.Speed, after.Speed);
            if (before.Save != after.Save) Line("Saving throws", before.Save, after.Save);
            if (d.Magic?.RequiresAttunement == true && !s.Attuned) cmp.Notes.Add("Its magic needs attunement: the numbers above are without it.");
            if (s.Condition == ItemCondition.Damaged || s.Condition == ItemCondition.Broken) cmp.Notes.Add($"It is {ItemConditions.Name(s.Condition)}: {ItemConditions.Effect(s.Condition, d.Weapon != null)}.");
            if (d.Armor?.StealthDisadvantage == true) cmp.Notes.Add("Disadvantage on Stealth checks.");
            if (p.Equipped(target)?.Bound == true) cmp.Notes.Add("What you wear there is cursed and cannot be taken off.");
            return cmp;
        }

        private struct Numbers
        {
            public int Ac, Speed, Save, AttackBonus;
            public double Average;
            public string Attack;
        }

        private static Numbers Snapshot(WorldData world, PlayerCharacterData p)
        {
            var w = new WorldData { Player = p, Seed = world.Seed };
            var stats = CombatStats.For(w, new CombatantData { Kind = CombatantKind.Player, Name = p.Name });
            var n = new Numbers { Ac = stats.ArmorClass.Total, Speed = stats.Rules.Speed, Save = stats.Rules.SaveBonus(Ability.Dexterity).Total, Attack = "none" };
            AttackOption best = null;
            foreach (var a in stats.Attacks)
                if (a.Kind == AttackKind.Weapon && !a.OffHand && (best == null || Avg(a) > Avg(best))) best = a;
            if (best != null)
            {
                n.AttackBonus = best.AttackBonus.Total;
                n.Average = Avg(best);
                n.Attack = $"{best.Name} {Signed(best.AttackBonus.Total)}, {best.Damage}{(best.DamageBonus.Total != 0 ? Signed(best.DamageBonus.Total) : string.Empty)} (avg {n.Average.ToString("0.#", CultureInfo.InvariantCulture)})";
            }

            return n;
        }

        private static double Avg(AttackOption a) => a.Damage.Count * (a.Damage.Sides + 1) / 2.0 + a.Damage.Bonus + a.DamageBonus.Total;

        // ----- Containers ---------------------------------------------------------------------------

        public ContainerView GetContainer(string key)
        {
            var p = _world?.Player;
            var c = _world?.FindContainer(key);
            if (p == null || c == null) return null;
            ItemCommandHandlers.Reachable(_world, p, key, out var failure);
            var v = new ContainerView
            {
                Key = c.Key, Name = c.Name, Kind = c.Kind, LayoutKey = c.LayoutKey, Coin = c.Coin, CanDeposit = c.AcceptsDeposits, Reachable = failure == null, Problem = failure?.Message,
                Grams = ItemWeights.Grams(c.Items), CapacityGrams = c.CapacityGrams, Opened = c.Opened,
            };
            foreach (var s in c.Items)
            {
                var r = Row(s, EquipmentSlot.None);
                if (r != null) v.Rows.Add(r);
            }

            v.Rows.Sort((a, b) => a.CategoryOrder != b.CategoryOrder ? a.CategoryOrder.CompareTo(b.CategoryOrder) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return v;
        }

        /// <summary>The containers the player can reach where they are (their chest, a cache), and loot piles not yet emptied.</summary>
        public IReadOnlyList<ContainerView> GetReachableContainers()
        {
            var list = new List<ContainerView>();
            var p = _world?.Player;
            if (p == null) return list;
            foreach (var c in _world.Containers)
            {
                if (!c.PlayerAccess) continue;
                if (c.Kind == ContainerKind.Loot && c.IsEmpty) continue;
                if (c.PlaceId != null)
                {
                    // Phase 13: in a place, the containers of that place the player has opened, and what fights left there.
                    if (!p.IsInPlace || p.PlaceId != c.PlaceId || (!c.Opened && c.Kind != ContainerKind.Loot)) continue;
                    list.Add(GetContainer(c.Key));
                    continue;
                }

                if (!c.SettlementId.IsEmpty && (!p.IsInSettlement || p.SettlementId != c.SettlementId)) continue;
                list.Add(GetContainer(c.Key));
            }

            return list;
        }

        /// <summary>The loot a resolved encounter left, or null.</summary>
        public ContainerView GetEncounterLoot(EncounterId id) => GetContainer(WorldEncounters.LootKey(id));

        // ----- Work ---------------------------------------------------------------------------------

        public WorkView GetWork()
        {
            var j = _world == null ? null : ItemCommandHandlers.ActiveJob(_world);
            if (j == null) return null;
            return new WorkView
            {
                Key = j.Key, Kind = j.Kind, What = ItemCommandHandlers.Describe(j), MinutesLeft = Math.Max(0, j.ReadyAt - SpellCommandHandlers.Now(_world)),
                Where = _world.FindSettlement(j.SettlementId)?.Name,
            };
        }

        /// <summary>Recipes, gathering sites, mending and attunement as the player stands now (ADR-0054).</summary>
        public WorkshopView GetWorkshop()
        {
            var p = _world?.Player;
            if (p == null) return null;
            var v = new WorkshopView { InSettlement = p.IsInSettlement, Active = GetWork() };
            var created = p.Rpg != null && p.Rpg.State == CreationState.Complete;
            foreach (var r in RecipeCatalog.Recipes)
            {
                var rv = new RecipeView
                {
                    Id = r.Id, Name = r.Name, Discipline = r.Discipline, Minutes = r.Minutes, Dc = r.Dc, Ability = r.CheckAbility,
                    // Phase 21 (ADR-0107): the physical station and where it is kept here.
                    Station = r.StationKind != StationKind.None ? StationWords.Name(r.StationKind) : ItemCommandHandlers.StationName(r.Station),
                    HasStation = r.StationKind != StationKind.None
                        ? (p.IsInSettlement && (Simulation.Commerce.CraftingStations.In(_world, p.SettlementId, r.StationKind).Count > 0 || (r.Station != null && ItemCommandHandlers.HasStation(_world, p, r.Station))))
                        : ItemCommandHandlers.HasStation(_world, p, r.Station),
                    Output = (r.OutputQuantity > 1 ? I(r.OutputQuantity) + " × " : string.Empty) + ItemCatalog.Find(r.OutputItemId)?.Name,
                };
                foreach (var (item, qty) in r.Inputs) rv.Inputs.Add((ItemCatalog.Find(item)?.Name ?? item, qty, ItemCommandHandlers.CountInPack(p, item)));
                if (r.ToolId != null)
                {
                    rv.Tool = ItemCatalog.Find(r.ToolId)?.Name;
                    rv.HasTool = ItemCommandHandlers.Tool(p, r.ToolId, null) != null;
                }
                else rv.HasTool = true;

                if (created)
                {
                    rv.Bonus = ItemCommandHandlers.CheckBonus(p, r.CheckAbility, r.ToolId, out var prof);
                    rv.Proficient = prof;
                }

                rv.ChancePercent = r.Dc <= 0 ? 100 : Math.Max(5, Math.Min(100, (21 - (r.Dc - rv.Bonus)) * 5));
                rv.Problem = !created ? "Create your character first." : ItemCommandHandlers.CraftProblem(_world, p, r);
                v.Recipes.Add(rv);
            }

            foreach (var site in _world.GatheringSites)
            {
                if (!p.IsInSettlement || site.SettlementId != p.SettlementId) continue;
                var tool = site.ToolDiscipline == null ? null : ItemCommandHandlers.Tool(p, null, site.ToolDiscipline);
                v.Sites.Add(new SiteView
                {
                    Key = site.Key, Name = site.Name, Item = ItemCatalog.Find(site.ItemId)?.Name, Available = site.Available, Capacity = site.Capacity, Yield = site.Yield,
                    Minutes = site.Minutes, Tool = site.ToolDiscipline,
                    Problem = site.Available <= 0 ? "Worked out for now; it recovers week by week." : site.ToolDiscipline != null && tool == null ? $"Needs a {site.ToolDiscipline} tool." : null,
                });
            }

            foreach (var s in p.Inventory)
            {
                var d = ItemCatalog.Find(s.ItemId);
                if (d == null) continue;
                if (d.Wears && ItemStacks.RepairSteps(s) > 0 && created)
                {
                    var discipline = ItemStacks.RepairDiscipline(d);
                    var tool = ItemCommandHandlers.Tool(p, null, discipline);
                    var ability = discipline == "blacksmithing" ? Ability.Strength : Ability.Dexterity;
                    var bonus = ItemCommandHandlers.CheckBonus(p, ability, tool?.ItemId, out _);
                    v.Repairs.Add((Row(s, EquipmentSlot.None), discipline, 10 + 2 * ItemStacks.RepairSteps(s), bonus, tool == null || tool.StackId == s.StackId ? $"Needs {discipline} tools." : null));
                }
            }

            var held = new List<(ItemStackData, EquipmentSlot)>();
            foreach (var s in p.Inventory) held.Add((s, EquipmentSlot.None));
            foreach (var pair in p.Equipment) held.Add((pair.Value, pair.Key));
            foreach (var (s, slot) in held)
            {
                var d = ItemCatalog.Find(s.ItemId);
                if (d?.Magic?.RequiresAttunement != true || s.Attuned) continue;
                v.Attunable.Add((Row(s, slot), ItemCommandHandlers.AttuneProblem(p, s)));
            }

            for (var k = _world.WorkJobs.Count - 1; k >= 0 && v.RecentOutcomes.Count < 5; k--)
                if (_world.WorkJobs[k].Done) v.RecentOutcomes.Add(_world.WorkJobs[k].Outcome);
            return v;
        }

        /// <summary>A shop's particular goods (worn, fine or otherwise distinct items) with their prices.</summary>
        public IReadOnlyList<(InventoryRow Row, int Price)> GetShopGoods(ShopId id)
        {
            var list = new List<(InventoryRow, int)>();
            var shop = _world?.FindShop(id);
            if (shop == null) return list;
            foreach (var s in shop.Goods)
            {
                var r = Row(s, EquipmentSlot.None);
                if (r != null) list.Add((r, ItemCommandHandlers.GoodsPrice(_world, shop, s)));
            }

            return list;
        }

        /// <summary>What a smith at this shop charges to mend each worn or damaged thing the player carries (pack and worn).</summary>
        public IReadOnlyList<(InventoryRow Row, int Price)> GetRepairQuotes(ShopId id)
        {
            var list = new List<(InventoryRow, int)>();
            var shop = _world?.FindShop(id);
            var p = _world?.Player;
            if (shop == null || p == null || !ItemCommandHandlers.Repairs(shop)) return list;
            void Add(ItemStackData s, EquipmentSlot slot)
            {
                var d = ItemCatalog.Find(s.ItemId);
                if (d == null || !d.Wears) return;
                var price = ItemStacks.RepairPrice(d, s);
                if (price > 0) list.Add((Row(s, slot), price));
            }

            foreach (var pair in p.Equipment) Add(pair.Value, pair.Key);
            foreach (var s in p.Inventory) Add(s, EquipmentSlot.None);
            return list;
        }

        /// <summary>What a shop would pay for one of this pack instance (condition and quality included), or why it will not.</summary>
        public Quote GetSaleOffer(ShopId id, ItemStackId stackId)
        {
            var shop = _world?.FindShop(id);
            var p = _world?.Player;
            if (shop == null || p == null) return null;
            var i = PlayerCommandHandlers.IndexOf(p, stackId);
            return i < 0 ? null : PlayerCommandHandlers.OfferFor(_world, shop, p.Inventory[i]);
        }
    }
}
