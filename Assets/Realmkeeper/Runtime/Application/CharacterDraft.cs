using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    public enum CreationStep
    {
        Identity = 0,
        Appearance = 1,
        Species = 2,
        Background = 3,
        Class = 4,
        Abilities = 5,
        Skills = 6,
        Equipment = 7,
        Review = 8,
    }

    /// <summary>
    /// The character creator's working state (Phase 6), engine-free so its rules are tested without Unity.
    /// It holds the choices, keeps them consistent as earlier steps change (a new class clears its skills
    /// and kit; a background that grants a skill removes it from the class picks), and says what each
    /// step still needs. Nothing is applied until <see cref="CreateCharacterCommand"/> is dispatched.
    /// </summary>
    public sealed class CharacterDraft
    {
        public static readonly IReadOnlyList<CreationStep> Steps = (CreationStep[])Enum.GetValues(typeof(CreationStep));

        public CharacterDraft(PlayerCharacterData basis = null)
        {
            Choices = new CharacterCreationChoices
            {
                Name = basis?.Name ?? "Traveller",
                Age = basis != null && basis.Age >= CharacterCommandHandlers.MinAge ? basis.Age : 25,
                Description = basis?.Description ?? string.Empty,
                Appearance = basis?.Appearance?.Clone() ?? new PlayerAppearanceData(),
                SpeciesId = "human",
                Method = AbilityMethod.StandardArray,
                BaseScores = new[] { 15, 14, 13, 12, 10, 8 },
            };
        }

        public CharacterCreationChoices Choices { get; }
        public CreationStep Step { get; private set; }

        public SpeciesDefinition Species => RpgCatalog.FindSpecies(Choices.SpeciesId);
        public BackgroundDefinition Background => RpgCatalog.FindBackground(Choices.BackgroundId);
        public ClassDefinition Class => RpgCatalog.FindClass(Choices.ClassId);

        /// <summary>The rules as the draft stands (for live modifiers, skills, hit points).</summary>
        public CharacterRules Rules
        {
            get
            {
                var sheet = new CharacterSheetData
                {
                    State = CreationState.Complete,
                    SpeciesId = Choices.SpeciesId,
                    BackgroundId = Choices.BackgroundId,
                    ClassId = Choices.ClassId,
                    Method = Choices.Method,
                    BaseScores = (int[])Choices.BaseScores.Clone(),
                };
                foreach (var pair in Choices.Choices) sheet.Choices[pair.Key] = new List<string>(pair.Value);
                return new CharacterRules(sheet);
            }
        }

        // ----- Navigation ---------------------------------------------------------------------------

        public void GoTo(CreationStep step) => Step = step;

        public bool Next()
        {
            if (Step == CreationStep.Review) return false;
            Step = Step + 1;
            return true;
        }

        public bool Back()
        {
            if (Step == CreationStep.Identity) return false;
            Step = Step - 1;
            return true;
        }

        // ----- Choices ------------------------------------------------------------------------------

        public List<string> Picked(string choiceId)
        {
            if (!Choices.Choices.TryGetValue(choiceId, out var list)) Choices.Choices[choiceId] = list = new List<string>();
            return list;
        }

        public void SetSpecies(string id)
        {
            if (RpgCatalog.FindSpecies(id) == null || id == Choices.SpeciesId) return;
            foreach (var key in new List<string>(Choices.Choices.Keys)) if (key.StartsWith("species:", StringComparison.Ordinal)) Choices.Choices.Remove(key);
            Choices.SpeciesId = id;
            if (!Species.Appearance.AllowsBeard) Choices.Appearance.BeardId = null;
            DropGrantedSkills();
        }

        public void SetBackground(string id)
        {
            if (RpgCatalog.FindBackground(id) == null) return;
            Choices.BackgroundId = id;
            DropGrantedSkills();
        }

        /// <summary>Selects a class: its skills, options and expertise start empty; each kit group starts on its first option; scores are arranged for its primary ability.</summary>
        public void SetClass(string id)
        {
            var cls = RpgCatalog.FindClass(id);
            if (cls == null || id == Choices.ClassId) return;
            foreach (var key in new List<string>(Choices.Choices.Keys))
                if (key.StartsWith("class:", StringComparison.Ordinal) || key.StartsWith("equipment:", StringComparison.Ordinal)) Choices.Choices.Remove(key);
            Choices.ClassId = id;
            foreach (var g in cls.Equipment) Choices.Choices[CharacterValidation.EquipmentChoiceId(g)] = new List<string> { g.Options[0].Id };
            if (Choices.Method == AbilityMethod.StandardArray) SuggestScores();
        }

        /// <summary>Arranges the standard array for the class: primary abilities first, then Constitution, then the rest.</summary>
        public void SuggestScores()
        {
            var order = new List<Ability>();
            if (Class != null) foreach (var a in Class.PrimaryAbilities) if (!order.Contains(a)) order.Add(a);
            if (!order.Contains(Ability.Constitution)) order.Add(Ability.Constitution);
            foreach (var a in new[] { Ability.Dexterity, Ability.Wisdom, Ability.Strength, Ability.Charisma, Ability.Intelligence })
                if (!order.Contains(a)) order.Add(a);
            Choices.Method = AbilityMethod.StandardArray;
            for (var i = 0; i < 6; i++) Choices.BaseScores[(int)order[i]] = AbilityRules.StandardArray[i];
        }

        public void SetMethod(AbilityMethod method)
        {
            if (method == Choices.Method) return;
            Choices.Method = method;
            if (method == AbilityMethod.PointBuy) for (var i = 0; i < 6; i++) Choices.BaseScores[i] = AbilityRules.PointBuyMin;
            else SuggestScores();
        }

        /// <summary>Standard array: exchanges the values of two abilities.</summary>
        public void Swap(Ability a, Ability b)
        {
            var s = Choices.BaseScores;
            var t = s[(int)a];
            s[(int)a] = s[(int)b];
            s[(int)b] = t;
        }

        public int PointsLeft => AbilityRules.PointBuyBudget - AbilityRules.PointsSpent(Choices.BaseScores);

        /// <summary>Point buy: raises or lowers one score within 8–15 and the budget. Returns false if not allowed.</summary>
        public bool Adjust(Ability a, int delta)
        {
            if (Choices.Method != AbilityMethod.PointBuy) return false;
            var s = Choices.BaseScores;
            var next = s[(int)a] + delta;
            if (next < AbilityRules.PointBuyMin || next > AbilityRules.PointBuyMax) return false;
            var old = s[(int)a];
            s[(int)a] = next;
            if (AbilityRules.PointsSpent(s) <= AbilityRules.PointBuyBudget) return true;
            s[(int)a] = old;
            return false;
        }

        /// <summary>Adds or removes a pick; refuses past the choice's count, granted skills and invalid picks.</summary>
        public bool Toggle(string choiceId, string value)
        {
            var list = Picked(choiceId);
            if (list.Remove(value)) return true;
            var (count, allowed) = Limits(choiceId, value);
            if (!allowed) return false;
            if (count == 1 && list.Count == 1 && IsOptionChoice(choiceId)) list.Clear(); // a single option: picking another replaces it
            if (list.Count >= count) return false;
            list.Add(value);
            return true;
        }

        private bool IsOptionChoice(string choiceId) =>
            choiceId.StartsWith("equipment:", StringComparison.Ordinal) || FindChoice(choiceId)?.Kind == ChoiceKind.Option;

        public ChoiceDefinition FindChoice(string id)
        {
            if (Species != null) foreach (var c in Species.Choices) if (c.Id == id) return c;
            if (Class != null) foreach (var c in Class.Choices) if (c.Id == id) return c;
            return null;
        }

        private (int count, bool allowed) Limits(string choiceId, string value)
        {
            var granted = Rules.GrantedSkills();
            if (choiceId == ClassDefinition.SkillChoiceId)
            {
                if (Class == null || !SkillRules.TryParse(value, out var skill)) return (0, false);
                var ok = false;
                foreach (var s in Class.SkillOptions) if (s == skill) ok = true;
                return (Class.SkillChoices, ok && !Contains(granted, value) && !PickedElsewhere(choiceId, value));
            }

            if (choiceId.StartsWith("equipment:", StringComparison.Ordinal))
            {
                if (Class == null) return (0, false);
                foreach (var g in Class.Equipment)
                    if (CharacterValidation.EquipmentChoiceId(g) == choiceId)
                        foreach (var o in g.Options)
                            if (o.Id == value) return (1, true);
                return (0, false);
            }

            var c = FindChoice(choiceId);
            if (c == null) return (0, false);
            switch (c.Kind)
            {
                case ChoiceKind.Option:
                    foreach (var o in c.Options) if (o.Id == value) return (c.Count, true);
                    return (0, false);
                case ChoiceKind.Skill:
                    return (c.Count, SkillRules.TryParse(value, out _) && !Contains(granted, value) && !PickedElsewhere(choiceId, value));
                case ChoiceKind.Expertise:
                    return (c.Count, SkillRules.TryParse(value, out var e) && Rules.SkillRank(e) != ProficiencyRank.None);
                default:
                    return (0, false);
            }
        }

        private bool PickedElsewhere(string choiceId, string skill)
        {
            foreach (var pair in Choices.Choices)
            {
                if (pair.Key == choiceId || pair.Key.StartsWith("equipment:", StringComparison.Ordinal)) continue;
                var c = FindChoice(pair.Key);
                if (pair.Key != ClassDefinition.SkillChoiceId && (c == null || c.Kind != ChoiceKind.Skill)) continue;
                if (pair.Value.Contains(skill)) return true;
            }

            return false;
        }

        /// <summary>Skills now granted outright cannot also be picked; expertise must stay on proficient skills.</summary>
        private void DropGrantedSkills()
        {
            var granted = Rules.GrantedSkills();
            foreach (var pair in Choices.Choices)
            {
                if (pair.Key == ClassDefinition.SkillChoiceId || FindChoice(pair.Key)?.Kind == ChoiceKind.Skill) pair.Value.RemoveAll(s => Contains(granted, s));
            }

            var rules = Rules;
            if (Class != null)
                foreach (var c in Class.Choices)
                    if (c.Kind == ChoiceKind.Expertise && Choices.Choices.TryGetValue(c.Id, out var ex))
                        ex.RemoveAll(s => !SkillRules.TryParse(s, out var k) || rules.SkillRank(k) == ProficiencyRank.None);
        }

        private static bool Contains(IReadOnlyList<string> list, string s)
        {
            foreach (var x in list) if (x == s) return true;
            return false;
        }

        // ----- What each step still needs -----------------------------------------------------------

        public IReadOnlyList<string> Problems(CreationStep step)
        {
            var list = new List<string>();
            var c = Choices;
            switch (step)
            {
                case CreationStep.Identity:
                    if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > PlayerCharacterData.MaxNameLength) list.Add($"Give a name (1–{PlayerCharacterData.MaxNameLength} characters).");
                    if (c.Age < CharacterCommandHandlers.MinAge || c.Age > CharacterCommandHandlers.MaxAge) list.Add($"Age must be {CharacterCommandHandlers.MinAge}–{CharacterCommandHandlers.MaxAge}.");
                    if ((c.Title ?? string.Empty).Length > CharacterSheetData.MaxTitleLength) list.Add($"A title is at most {CharacterSheetData.MaxTitleLength} characters.");
                    if ((c.History ?? string.Empty).Length > CharacterSheetData.MaxHistoryLength) list.Add($"A history is at most {CharacterSheetData.MaxHistoryLength} characters.");
                    break;
                case CreationStep.Appearance:
                    var look = PlayerAppearanceOptions.Check(c.Appearance);
                    if (look != null) list.Add(look);
                    break;
                case CreationStep.Species:
                    if (Species == null) list.Add("Choose a species.");
                    else
                        foreach (var ch in Species.Choices)
                            if (ch.Kind == ChoiceKind.Option && !ch.CountAllowed(Picked(ch.Id).Count)) list.Add($"{ch.Name}: {ch.Prompt}");
                    break;
                case CreationStep.Background:
                    if (Background == null) list.Add("Choose a background.");
                    break;
                case CreationStep.Class:
                    if (Class == null) list.Add("Choose a class.");
                    else
                        foreach (var ch in Class.Choices)
                            if (ch.Kind == ChoiceKind.Option && !ch.CountAllowed(Picked(ch.Id).Count)) list.Add($"{ch.Name}: {ch.Prompt}");
                    break;
                case CreationStep.Abilities:
                    var scores = AbilityRules.Check(c.Method, c.BaseScores);
                    if (scores != null) list.Add(scores);
                    break;
                case CreationStep.Skills:
                    if (Class == null) { list.Add("Choose a class first."); break; }
                    var n = Picked(ClassDefinition.SkillChoiceId).Count;
                    if (n != Class.SkillChoices) list.Add($"Choose {Class.SkillChoices} {Class.Name} skills ({n} chosen).");
                    if (Species != null)
                        foreach (var ch in Species.Choices)
                            if (ch.Kind == ChoiceKind.Skill && Picked(ch.Id).Count != ch.Count) list.Add($"{ch.Name}: {ch.Prompt}");
                    foreach (var ch in Class.Choices)
                        if (ch.Kind == ChoiceKind.Expertise && Picked(ch.Id).Count != ch.Count) list.Add($"{ch.Name}: {ch.Prompt}");
                    break;
                case CreationStep.Equipment:
                    if (Class == null) { list.Add("Choose a class first."); break; }
                    foreach (var g in Class.Equipment)
                        if (Picked(CharacterValidation.EquipmentChoiceId(g)).Count != 1) list.Add($"Choose your {g.Id}.");
                    break;
                case CreationStep.Review:
                    list.AddRange(CharacterCommandHandlers.Check(null, Clean()));
                    break;
            }

            return list;
        }

        public bool StepComplete(CreationStep step) => Problems(step).Count == 0;

        /// <summary>The choices without empty lists (what the command receives).</summary>
        public CharacterCreationChoices Clean()
        {
            var c = Choices;
            var copy = new CharacterCreationChoices
            {
                Name = c.Name, Title = c.Title, Age = c.Age, Description = c.Description, History = c.History,
                Appearance = c.Appearance?.Clone(), SpeciesId = c.SpeciesId, BackgroundId = c.BackgroundId, ClassId = c.ClassId,
                Method = c.Method, BaseScores = (int[])c.BaseScores.Clone(),
            };
            foreach (var pair in c.Choices) if (pair.Value.Count > 0) copy.Choices[pair.Key] = new List<string>(pair.Value);
            return copy;
        }

        public CreateCharacterCommand ToCommand() => new CreateCharacterCommand(Clean());
    }
}
