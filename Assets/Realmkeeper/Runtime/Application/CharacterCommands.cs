using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>Everything chosen in the character creator (Phase 6).</summary>
    public sealed class CharacterCreationChoices
    {
        public string Name { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Age { get; set; } = 25;
        public string Description { get; set; } = string.Empty;
        public string History { get; set; } = string.Empty;

        /// <summary>Phase 20: optional pronouns and a one-line concept, in the player's words.</summary>
        public string Pronouns { get; set; } = string.Empty;
        public string Concept { get; set; } = string.Empty;
        public PlayerAppearanceData Appearance { get; set; }
        public string SpeciesId { get; set; }
        public string BackgroundId { get; set; }
        public string ClassId { get; set; }
        public AbilityMethod Method { get; set; }
        public int[] BaseScores { get; set; } = new int[6];

        /// <summary>Creation choices by ID (class skills, species and class options, "equipment:{group}").</summary>
        public Dictionary<string, List<string>> Choices { get; set; } = new Dictionary<string, List<string>>();
    }

    /// <summary>
    /// Creates the player's character in one transaction: identity, appearance, species, background, class,
    /// scores and choices; full hit points and resources; the class and background kit given once (and the
    /// marked pieces equipped); the background's coin. Refused if anything is invalid or the character
    /// already exists. Location, belongings carried before, purse and identity are kept.
    /// </summary>
    public sealed class CreateCharacterCommand : ICommand
    {
        public CreateCharacterCommand(CharacterCreationChoices choices) { Choices = choices; }
        public CharacterCreationChoices Choices { get; }
    }

    public static class CharacterCommandHandlers
    {
        public const int MinAge = PlayerCommandHandlers.MinAge;
        public const int MaxAge = PlayerCommandHandlers.MaxAge;

        public static void Register(CommandRegistry commands)
        {
            commands.Register<CreateCharacterCommand>(new CreateHandler());
        }

        /// <summary>The sheet the choices describe (state Complete, hit points and resources full), or the reasons it cannot be.</summary>
        public static CharacterSheetData BuildSheet(CharacterCreationChoices c, out List<string> errors)
        {
            errors = new List<string>();
            var sheet = new CharacterSheetData
            {
                State = CreationState.Complete,
                Title = (c.Title ?? string.Empty).Trim(),
                History = (c.History ?? string.Empty).Trim(),
                Pronouns = (c.Pronouns ?? string.Empty).Trim(),
                Concept = (c.Concept ?? string.Empty).Trim(),
                SpeciesId = c.SpeciesId,
                BackgroundId = c.BackgroundId,
                ClassId = c.ClassId,
                Level = 1,
                Experience = 0,
                Method = c.Method,
                BaseScores = c.BaseScores != null && c.BaseScores.Length == 6 ? (int[])c.BaseScores.Clone() : new int[6],
                StartingEquipmentGranted = true,
            };
            foreach (var pair in c.Choices ?? new Dictionary<string, List<string>>())
                if (pair.Value != null && pair.Value.Count > 0) sheet.Choices[pair.Key] = new List<string>(pair.Value);
            var rules = new CharacterRules(sheet);
            if (rules.Species == null) errors.Add("Choose a species.");
            if (rules.Background == null) errors.Add("Choose a background.");
            if (rules.Class == null) errors.Add("Choose a class.");
            if (errors.Count > 0) return null;
            sheet.CurrentHp = rules.MaxHp;
            foreach (var r in rules.ResourceDefinitions) sheet.Resources[r.Id] = rules.ResourceMax(r);
            return sheet;
        }

        /// <summary>The items the choices grant: the background kit and each chosen class option.</summary>
        public static List<KitItem> KitFor(CharacterSheetData sheet)
        {
            var list = new List<KitItem>();
            var background = RpgCatalog.FindBackground(sheet.BackgroundId);
            var cls = RpgCatalog.FindClass(sheet.ClassId);
            if (cls != null)
                foreach (var g in cls.Equipment)
                {
                    var picked = sheet.Chosen(CharacterValidation.EquipmentChoiceId(g));
                    foreach (var o in g.Options)
                        if (picked.Count == 1 && o.Id == picked[0]) list.AddRange(o.Items);
                }

            if (background != null) list.AddRange(background.Equipment);
            return list;
        }

        /// <summary>Every problem with the choices (identity, appearance and the character rules), as the command checks them.</summary>
        public static List<string> Check(PlayerCharacterData current, CharacterCreationChoices c)
        {
            var errors = new List<string>();
            if (c == null)
            {
                errors.Add("Nothing was chosen.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > PlayerCharacterData.MaxNameLength)
                errors.Add($"A name must be 1–{PlayerCharacterData.MaxNameLength} characters.");
            if (c.Age < MinAge || c.Age > MaxAge) errors.Add($"Age must be {MinAge}–{MaxAge}.");
            if ((c.Description ?? string.Empty).Length > PlayerCommandHandlers.MaxDescriptionLength)
                errors.Add($"A description is at most {PlayerCommandHandlers.MaxDescriptionLength} characters.");
            var look = PlayerAppearanceOptions.Check(c.Appearance, c.SpeciesId);
            if (look != null) errors.Add(look);

            var sheet = BuildSheet(c, out var missing);
            errors.AddRange(missing);
            if (sheet == null) return errors;
            var preview = (current ?? new PlayerCharacterData()).Clone();
            preview.Rpg = sheet;
            preview.Appearance = c.Appearance ?? preview.Appearance;
            var rules = new CharacterRules(sheet);
            preview.Species = rules.Species.Name;
            preview.Background = rules.Background.Name;
            errors.AddRange(CharacterValidation.Check(preview));
            return errors;
        }

        private sealed class CreateHandler : ICommandHandler<CreateCharacterCommand>
        {
            public CommandResult Handle(CreateCharacterCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var current = context.Committed.Player;
                if (current == null) return CommandResult.NotFound("This world has no player character.");
                if (current.Rpg != null && current.Rpg.State == CreationState.Complete)
                    return CommandResult.InvalidState($"{current.Name} has already been created; creation and its starting equipment happen once.");
                var c = command.Choices;
                var errors = Check(current, c);
                if (errors.Count > 0) return CommandResult.Invalid("The character is not complete.", errors);

                var sheet = BuildSheet(c, out _);
                var rules = new CharacterRules(sheet);
                var edit = context.Edit();
                var p = edit.Player;
                p.Name = c.Name.Trim();
                p.Age = c.Age;
                p.Description = (c.Description ?? string.Empty).Trim();
                p.Appearance = c.Appearance.Clone();
                p.Species = rules.Species.Name;
                p.Background = rules.Background.Name;
                sheet.CreatedOn = edit.CurrentSimulationDate;
                // Experience earned before creation (legacy or pending players) is kept, with its ledger.
                sheet.Experience = current.Rpg?.Experience ?? 0;
                if (current.Rpg != null) foreach (var a in current.Rpg.ExperienceLog) sheet.ExperienceLog.Add(a.Clone());
                p.Rpg = sheet;

                // The kit, once: into the pack through the inventory rules, then the marked pieces equipped.
                var granted = new List<string>();
                foreach (var k in KitFor(sheet))
                {
                    var def = ItemCatalog.Find(k.ItemId);
                    var before = new HashSet<ItemStackId>();
                    foreach (var s in p.Inventory) before.Add(s.StackId);
                    if (!PlayerCommandHandlers.AddToPack(context, p, def, k.Quantity)) return CommandResult.InvalidState("Your pack is too full for your starting equipment.");
                    granted.Add((k.Quantity > 1 ? k.Quantity.ToString(CultureInfo.InvariantCulture) + " × " : string.Empty) + def.Name);
                    if (!k.Equip) continue;
                    ItemStackData fresh = null;
                    foreach (var s in p.Inventory) if (s.ItemId == def.Id && !before.Contains(s.StackId)) fresh = s;
                    if (fresh == null)
                        foreach (var s in p.Inventory) if (s.ItemId == def.Id) fresh = s; // topped up an existing stack
                    if (fresh == null || PlayerCommandHandlers.EquipFromPack(context, p, fresh.StackId, def) == null)
                        return CommandResult.InvalidState("Your pack is too full to change into your starting equipment.");
                }

                p.Purse += rules.Background.Coin;
                var facts = new Dictionary<string, string>
                {
                    ["source"] = "player",
                    ["player"] = p.Name,
                    ["species"] = rules.Species.Name,
                    ["background"] = rules.Background.Name,
                    ["class"] = rules.Class.Name,
                    ["level"] = "1",
                };
                var where = p.IsInSettlement ? new[] { p.SettlementId } : Array.Empty<SettlementId>();
                context.ReportEvent(HistoricalEventType.PlayerCharacterCreated, EventSeverity.Info, EventImportance.Minor, where, facts);
                return CommandResult.Ok($"{p.Name}, {Article(rules.Species.Name)} {rules.Background.Name.ToLowerInvariant()} turned {rules.Class.Name.ToLowerInvariant()}. " +
                                        $"Received: {string.Join(", ", granted)}, and {rules.Background.Coin} coin.");
            }

            private static string Article(string w) => ("AEIOU".IndexOf(char.ToUpperInvariant(w[0])) >= 0 ? "an " : "a ") + w.ToLowerInvariant();
        }
    }
}
