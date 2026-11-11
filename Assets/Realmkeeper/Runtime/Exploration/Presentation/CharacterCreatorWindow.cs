using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using UnityEngine;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The character creator (Phase 6): Identity, Appearance, Species, Background, Class, Abilities, Skills,
    /// Equipment and Review, beside a live 3D preview wearing the chosen kit. It edits a
    /// <see cref="CharacterDraft"/> (engine-free; it keeps choices consistent and says what each step still
    /// needs) and raises <see cref="ConfirmRequested"/> with the finished command; the controller dispatches
    /// it. Nothing is applied to the world until then.
    /// </summary>
    public sealed class CharacterCreatorWindow
    {
        private static readonly string[] StepNames = { "Identity", "Appearance", "Species", "Background", "Class", "Abilities", "Skills", "Equipment", "Review" };

        private readonly Dictionary<CreationStep, Button> _steps = new Dictionary<CreationStep, Button>();
        private readonly VisualElement _content;
        private readonly VisualElement _preview;
        private readonly Label _summary;
        private readonly Label _problems;
        private readonly Button _back;
        private readonly Button _next;
        private readonly Button _create;
        private readonly Label _placeholder;
        private bool _dragging;
        private Vector2 _last;

        public CharacterCreatorWindow(VisualElement hudRoot)
        {
            Root = new VisualElement { name = "xk-creator" };
            Root.AddToClassList("xk-window");
            Root.AddToClassList("xk-creator");
            Root.AddToClassList("xk-hidden");

            var head = Row("xk-w-head");
            var title = Text("Create your character", "xk-w-title");
            title.style.flexGrow = 1;
            head.Add(title);
            var close = new Button(() => CloseRequested?.Invoke()) { text = "Later (Esc)", name = "xk-creator-close" };
            close.AddToClassList("xk-button");
            head.Add(close);
            Root.Add(head);

            var steps = Row("xk-cc-steps");
            foreach (CreationStep s in CharacterDraft.Steps)
            {
                var step = s;
                var b = new Button(() => { Draft?.GoTo(step); Render(); }) { text = (int)s + 1 + "  " + StepNames[(int)s], name = "xk-cc-step-" + s };
                b.AddToClassList("xk-cc-step");
                _steps[s] = b;
                steps.Add(b);
            }

            Root.Add(steps);

            var body = Row("xk-hub-body");
            _content = new ScrollView { name = "xk-cc-content" };
            _content.AddToClassList("xk-hub-content");
            body.Add(_content);
            var side = Column("xk-cc-side");
            _preview = new VisualElement { name = "xk-cc-preview" };
            _preview.AddToClassList("xk-hub-preview");
            _preview.AddToClassList("xk-cc-preview");
            _placeholder = Text(string.Empty, "xk-cc-placeholder");
            _preview.Add(_placeholder);
            _preview.Add(Text("Drag to turn · wheel to zoom", "xk-hub-preview-hint"));
            _preview.RegisterCallback<PointerDownEvent>(e => { _dragging = true; _last = e.position; _preview.CapturePointer(e.pointerId); });
            _preview.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_dragging) return;
                PreviewTurned?.Invoke(-(e.position.x - _last.x) * 0.6f);
                _last = e.position;
            });
            _preview.RegisterCallback<PointerUpEvent>(e => { _dragging = false; _preview.ReleasePointer(e.pointerId); });
            _preview.RegisterCallback<WheelEvent>(e => PreviewZoomed?.Invoke(-e.delta.y * 0.08f));
            side.Add(_preview);
            _summary = Text("-", "xk-cc-summary");
            _summary.name = "xk-cc-summary";
            side.Add(_summary);
            body.Add(side);
            Root.Add(body);

            var foot = Row("xk-cc-foot");
            _problems = Text(string.Empty, "xk-cc-problems");
            _problems.name = "xk-cc-problems";
            _problems.style.flexGrow = 1;
            foot.Add(_problems);
            _back = Button("Back", "xk-cc-back", () => { Draft?.Back(); Render(); });
            _next = Button("Next", "xk-cc-next", () => { Draft?.Next(); Render(); });
            _create = Button("Create character", "xk-cc-create", () => { if (Draft != null) ConfirmRequested?.Invoke(Draft.ToCommand()); });
            _create.AddToClassList("xk-button--primary");
            foot.Add(_back);
            foot.Add(_next);
            foot.Add(_create);
            Root.Add(foot);
            hudRoot.Add(Root);
        }

        public VisualElement Root { get; }
        public bool IsOpen => !Root.ClassListContains("xk-hidden");
        public CharacterDraft Draft { get; private set; }
        public VisualElement Content => _content;
        public string Problems => _problems.text;

        public event Action CloseRequested;
        public event Action<CreateCharacterCommand> ConfirmRequested;
        public event Action<float> PreviewTurned;
        public event Action<float> PreviewZoomed;

        /// <summary>Raised whenever the look may have changed (frame, hair, colours, species, kit).</summary>
        public event Action<CharacterDraft> DraftChanged;

        public void SetPreview(Texture texture)
        {
            _preview.style.backgroundImage = texture is RenderTexture rt ? Background.FromRenderTexture(rt) : new StyleBackground(StyleKeyword.None);
        }

        public void Open(CharacterDraft draft)
        {
            Draft = draft;
            Root.RemoveFromClassList("xk-hidden");
            Render();
        }

        public void Hide() => Root.AddToClassList("xk-hidden");

        public void ShowError(string text)
        {
            _problems.text = text;
            _problems.EnableInClassList("xk-message--error", true);
        }

        /// <summary>Redraws the current step (call after changing the draft from outside, e.g. tests).</summary>
        public void Render()
        {
            if (Draft == null) return;
            foreach (var pair in _steps)
            {
                pair.Value.EnableInClassList("xk-cc-step--active", pair.Key == Draft.Step);
                pair.Value.EnableInClassList("xk-cc-step--done", pair.Key != CreationStep.Review && Draft.StepComplete(pair.Key));
            }

            _content.Clear();
            switch (Draft.Step)
            {
                case CreationStep.Identity: Identity(); break;
                case CreationStep.Appearance: Appearance(); break;
                case CreationStep.Species: Species(); break;
                case CreationStep.Background: BackgroundStep(); break;
                case CreationStep.Class: ClassStep(); break;
                case CreationStep.Abilities: Abilities(); break;
                case CreationStep.Skills: Skills(); break;
                case CreationStep.Equipment: Equipment(); break;
                default: Review(); break;
            }

            var problems = Draft.Problems(Draft.Step);
            _problems.text = problems.Count == 0 ? (Draft.Step == CreationStep.Review ? "Ready. Creating the character gives your starting equipment once." : "✓ This step is complete.") : string.Join("  ·  ", problems);
            _problems.EnableInClassList("xk-message--error", problems.Count > 0);
            _back.SetEnabled(Draft.Step != CreationStep.Identity);
            _next.EnableInClassList("xk-hidden", Draft.Step == CreationStep.Review);
            _create.EnableInClassList("xk-hidden", Draft.Step != CreationStep.Review);
            _create.SetEnabled(problems.Count == 0);
            Summarise();
            DraftChanged?.Invoke(Draft);
        }

        private void Changed() => Render();

        // ----- Steps ------------------------------------------------------------------------------------

        private void Identity()
        {
            var c = Draft.Choices;
            _content.Add(Text("Who are you? Only a name is required; the rest is yours to fill in now or never.", "xk-w-note"));
            var name = new TextField("Name") { value = c.Name, name = "xk-cc-name", maxLength = PlayerCharacterData.MaxNameLength };
            name.RegisterValueChangedCallback(e => { c.Name = e.newValue; Summarise(); RefreshProblems(); });
            var title = new TextField("Title or epithet (optional)") { value = c.Title, name = "xk-cc-title", maxLength = CharacterSheetData.MaxTitleLength };
            title.RegisterValueChangedCallback(e => { c.Title = e.newValue; Summarise(); });
            var age = new IntegerField("Age") { value = c.Age, name = "xk-cc-age" };
            age.RegisterValueChangedCallback(e => { c.Age = e.newValue; RefreshProblems(); });
            var description = new TextField("Description (optional)") { value = c.Description, multiline = true, name = "xk-cc-description", maxLength = PlayerCommandHandlers.MaxDescriptionLength };
            description.RegisterValueChangedCallback(e => c.Description = e.newValue);
            var history = new TextField("Personal history (optional)") { value = c.History, multiline = true, name = "xk-cc-history", maxLength = CharacterSheetData.MaxHistoryLength };
            history.RegisterValueChangedCallback(e => c.History = e.newValue);
            foreach (var f in new VisualElement[] { name, title, age, description, history })
            {
                f.AddToClassList("xk-hub-field");
                _content.Add(f);
            }

            _content.Add(Text("Your background, species and class shape your skills and gear, not your fate: you are no chosen one. The world will judge you by what you do.", "xk-w-note"));
        }

        private void Appearance()
        {
            var a = Draft.Choices.Appearance;
            var species = Draft.Species;
            _content.Add(Text("Only what the character art can show is offered." + (species?.Appearance.PlaceholderNote != null ? " " + species.Appearance.PlaceholderNote : string.Empty), "xk-w-note"));
            _content.Add(Choices("Frame", new[] { "Male", "Female" }, a.Frame == NpcSex.Female ? 1 : 0, i =>
            {
                a.Frame = i == 1 ? NpcSex.Female : NpcSex.Male;
                a.HairId = PlayerAppearanceOptions.HairFor(a.Frame)[0];
                a.BeardId = null;
                Changed();
            }));
            var hair = PlayerAppearanceOptions.HairFor(a.Frame);
            var labels = new List<string>();
            var current = 0;
            for (var i = 0; i < hair.Count; i++)
            {
                labels.Add(hair[i] == null ? "None" : hair[i].Replace("Hair_", string.Empty));
                if (hair[i] == a.HairId) current = i;
            }

            _content.Add(Choices("Hair", labels, current, i => { a.HairId = hair[i]; Changed(); }));
            if (a.Frame == NpcSex.Male && (species == null || species.Appearance.AllowsBeard))
                _content.Add(Choices("Beard", new[] { "Beard", "Clean-shaven" }, a.BeardId == null ? 1 : 0, i => { a.BeardId = i == 0 ? "Hair_Beard" : null; Changed(); }));
            _content.Add(Swatches("Skin", NpcAppearanceResolver.SkinTones, a.SkinTone, i => { a.SkinTone = i; Changed(); }));
            _content.Add(Swatches("Hair colour", NpcAppearanceResolver.HairColours, a.HairColour, i => { a.HairColour = i; Changed(); }));
            _content.Add(Swatches("Cloth tint", NpcAppearanceResolver.OutfitTints, a.ClothTint, i => { a.ClothTint = i; Changed(); }));
        }

        private void Species()
        {
            var list = Row("xk-cc-split");
            var picker = Column("xk-cc-picker");
            foreach (var s in RpgCatalog.Species)
            {
                var id = s.Id;
                var b = Button(s.Name + (s.Appearance.HasFinishedModel ? string.Empty : "  ◇"), "xk-cc-species-" + id, () => { Draft.SetSpecies(id); Changed(); });
                b.AddToClassList("xk-cc-pick");
                b.EnableInClassList("xk-cc-pick--on", Draft.Choices.SpeciesId == id);
                picker.Add(b);
            }

            picker.Add(Text("◇ placeholder body (final model pending)", "xk-w-desc"));
            list.Add(picker);
            var s0 = Draft.Species;
            var detail = Column("xk-cc-detail");
            if (s0 != null)
            {
                detail.Add(Text(s0.Name, "xk-cc-name"));
                detail.Add(Text(RpgCatalog.SourceText(s0.Source), "xk-cc-source"));
                if (s0.Appearance.PlaceholderNote != null) detail.Add(Text(s0.Appearance.PlaceholderNote + " Needs: " + string.Join(", ", s0.Appearance.Features) + ".", "xk-cc-warn"));
                detail.Add(Text(s0.Description, "xk-w-value"));
                detail.Add(Text(s0.Lore, "xk-w-desc"));
                detail.Add(Pair("Size · speed", $"{s0.Size} · {s0.Speed} ft{(s0.Speed + SpeedBonus(s0) != s0.Speed ? " (" + (s0.Speed + SpeedBonus(s0)) + " ft with traits)" : string.Empty)}"));
                detail.Add(Pair("Ability increases", Bonuses(s0)));
                detail.Add(Pair("Languages", Names(s0.Languages, l => RpgCatalog.FindLanguage(l)?.Name)));
                detail.Add(Text("Traits", "xk-w-heading"));
                foreach (var t in s0.Traits) detail.Add(Pair(t.Name, t.Description));
                foreach (var ch in s0.Choices)
                    if (ch.Kind == ChoiceKind.Option) detail.Add(OptionChoice(ch));
                    else detail.Add(Text($"{ch.Name}: {ch.Prompt} (on the Skills step)", "xk-w-note"));
            }

            list.Add(detail);
            _content.Add(list);
        }

        private void BackgroundStep()
        {
            var list = Row("xk-cc-split");
            var picker = Column("xk-cc-picker");
            foreach (var b in RpgCatalog.Backgrounds)
            {
                var id = b.Id;
                var button = Button(b.Name, "xk-cc-background-" + id, () => { Draft.SetBackground(id); Changed(); });
                button.AddToClassList("xk-cc-pick");
                button.EnableInClassList("xk-cc-pick--on", Draft.Choices.BackgroundId == id);
                picker.Add(button);
            }

            list.Add(picker);
            var bg = Draft.Background;
            var detail = Column("xk-cc-detail");
            if (bg == null) detail.Add(Text("Choose where you come from. It grants two skills, tools, perhaps a language, a little gear and coin, and a feature the world will recognise. It does not decide your future.", "xk-w-note"));
            else
            {
                detail.Add(Text(bg.Name, "xk-cc-name"));
                detail.Add(Text(RpgCatalog.SourceText(bg.Source), "xk-cc-source"));
                detail.Add(Text(bg.Description, "xk-w-value"));
                detail.Add(Text(bg.Lore, "xk-w-desc"));
                detail.Add(Pair("Skills", Names(bg.Skills, s => SkillRules.Name(s))));
                detail.Add(Pair("Tools", Names(bg.Tools, t => RpgCatalog.FindTool(t)?.Name)));
                if (bg.Languages.Count > 0) detail.Add(Pair("Languages", Names(bg.Languages, l => RpgCatalog.FindLanguage(l)?.Name)));
                detail.Add(Pair("Equipment", Kit(bg.Equipment) + $" and {bg.Coin} coin"));
                detail.Add(Pair(bg.Feature.Name, bg.Feature.Description));
                foreach (var s in bg.Suggestions) detail.Add(Text("“" + s + "”", "xk-w-desc"));
            }

            list.Add(detail);
            _content.Add(list);
        }

        private void ClassStep()
        {
            var list = Row("xk-cc-split");
            var picker = Column("xk-cc-picker");
            foreach (var c in RpgCatalog.Classes)
            {
                var id = c.Id;
                var b = Button(c.Name + "  d" + c.HitDie, "xk-cc-class-" + id, () => { Draft.SetClass(id); Changed(); });
                b.AddToClassList("xk-cc-pick");
                b.EnableInClassList("xk-cc-pick--on", Draft.Choices.ClassId == id);
                picker.Add(b);
            }

            list.Add(picker);
            var cls = Draft.Class;
            var detail = Column("xk-cc-detail");
            if (cls == null) detail.Add(Text("Choose your class: how you fight, what you know and what power you draw on. Level 1 for now; later levels arrive with progression.", "xk-w-note"));
            else
            {
                detail.Add(Text(cls.Name, "xk-cc-name"));
                detail.Add(Text(RpgCatalog.SourceText(cls.Source), "xk-cc-source"));
                detail.Add(Text(cls.Description, "xk-w-value"));
                detail.Add(Text(cls.Identity, "xk-w-desc"));
                detail.Add(Pair("Primary ability", Names(cls.PrimaryAbilities, a => a.ToString())));
                detail.Add(Pair("Hit die", $"d{cls.HitDie} (level 1: {cls.HitDie} + Constitution)"));
                detail.Add(Pair("Saving throws", Names(cls.SavingThrows, a => a.ToString())));
                detail.Add(Pair("Armour", cls.ArmorProficiencies.Count == 0 ? "None" : string.Join(", ", cls.ArmorProficiencies)));
                detail.Add(Pair("Weapons", string.Join(", ", cls.WeaponProficiencies)));
                if (cls.Tools.Count > 0) detail.Add(Pair("Tools", Names(cls.Tools, t => RpgCatalog.FindTool(t)?.Name)));
                detail.Add(Pair("Skills", $"Choose {cls.SkillChoices} from {(cls.SkillOptions.Count == 18 ? "any skill" : Names(cls.SkillOptions, s => SkillRules.Name(s)))}"));
                if (cls.Spellcasting != null)
                {
                    var sc = cls.Spellcasting;
                    detail.Add(Pair("Spellcasting", $"{sc.Ability} · {sc.Style} · {sc.CantripsKnown} cantrips · {sc.FirstLevelSlots} first-level slot{(sc.FirstLevelSlots == 1 ? string.Empty : "s")}" +
                                                     $"{(sc.SlotRecovery == ResourceRecovery.ShortRest ? " (short rest)" : string.Empty)} · focus: {sc.Focus}. Spells arrive with the magic phase."));
                }

                detail.Add(Text("Level 1 features", "xk-w-heading"));
                foreach (var f in cls.Features) detail.Add(Pair(f.Name, f.Description));
                foreach (var ch in cls.Choices)
                    if (ch.Kind == ChoiceKind.Option) detail.Add(OptionChoice(ch));
                detail.Add(Text($"Subclass ({cls.SubclassTitle}): chosen at level {cls.SubclassLevel}, with progression.", "xk-w-desc"));
            }

            list.Add(detail);
            _content.Add(list);
        }

        private void Abilities()
        {
            var c = Draft.Choices;
            _content.Add(Choices("Method", new[] { "Standard array", "Point buy" }, c.Method == AbilityMethod.PointBuy ? 1 : 0, i =>
            {
                Draft.SetMethod(i == 1 ? AbilityMethod.PointBuy : AbilityMethod.StandardArray);
                Changed();
            }));
            _content.Add(Text(c.Method == AbilityMethod.PointBuy
                ? $"Point buy: every score starts at 8; raise any to 15. Scores up to 13 cost 1 point each step, 14 costs 2 more, 15 costs 2 more again. {Draft.PointsLeft} of {AbilityRules.PointBuyBudget} points left."
                : "Standard array: 15, 14, 13, 12, 10 and 8, once each. Use ▲ ▼ to swap a score with its neighbour in the array.", "xk-w-note"));
            var rules = Draft.Rules;
            var head = Row("xk-w-row");
            foreach (var h in new[] { "Ability", "Base", "Species", "Score", "Modifier", string.Empty }) head.Add(Text(h, "xk-cc-col"));
            _content.Add(head);
            foreach (var a in AbilityRules.All)
            {
                var ability = a;
                var row = Row("xk-w-row");
                row.name = "xk-cc-ability-" + a;
                var primary = Draft.Class != null && Contains(Draft.Class.PrimaryAbilities, a);
                row.Add(Text(a + (primary ? " ★" : string.Empty), "xk-cc-col"));
                row.Add(Text(c.BaseScores[(int)a].ToString(CultureInfo.InvariantCulture), "xk-cc-col"));
                row.Add(Text(rules.SpeciesBonus(a) == 0 ? "—" : Breakdown.Signed(rules.SpeciesBonus(a)), "xk-cc-col"));
                row.Add(Text(rules.Score(a).ToString(CultureInfo.InvariantCulture), "xk-cc-col"));
                row.Add(Text(Breakdown.Signed(rules.Modifier(a)), "xk-cc-col"));
                if (c.Method == AbilityMethod.PointBuy)
                {
                    row.Add(Button("−", "xk-cc-minus-" + a, () => { Draft.Adjust(ability, -1); Changed(); }));
                    row.Add(Button("+", "xk-cc-plus-" + a, () => { Draft.Adjust(ability, 1); Changed(); }));
                }
                else
                {
                    row.Add(Button("▲", "xk-cc-up-" + a, () => { SwapInArray(ability, +1); Changed(); }));
                    row.Add(Button("▼", "xk-cc-down-" + a, () => { SwapInArray(ability, -1); Changed(); }));
                }

                _content.Add(row);
            }

            _content.Add(Text("★ the class's primary ability. Modifiers are floor((score − 10) ÷ 2) and feed every skill, save and attack.", "xk-w-desc"));
        }

        /// <summary>Standard array: swap with the ability holding the next higher (+1) or lower (−1) array value.</summary>
        private void SwapInArray(Ability a, int direction)
        {
            var s = Draft.Choices.BaseScores;
            var index = -1;
            for (var i = 0; i < 6; i++) if (AbilityRules.StandardArray[i] == s[(int)a]) index = i;
            var target = index - direction; // the array is in descending order
            if (index < 0 || target < 0 || target > 5) return;
            for (var i = 0; i < 6; i++)
                if (s[i] == AbilityRules.StandardArray[target]) { Draft.Swap(a, (Ability)i); return; }
        }

        private void Skills()
        {
            var cls = Draft.Class;
            if (cls == null) { _content.Add(Text("Choose a class first.", "xk-w-empty")); return; }
            var rules = Draft.Rules;
            var granted = rules.GrantedSkills();
            var classPicks = Draft.Picked(ClassDefinition.SkillChoiceId);
            _content.Add(Text($"{cls.Name}: choose {cls.SkillChoices} ({classPicks.Count} chosen). Skills your species or background already grant are marked and cannot be chosen twice.", "xk-w-note"));
            foreach (var s in SkillRules.All)
            {
                var skill = s;
                var row = Row("xk-w-row");
                row.name = "xk-cc-skill-" + s;
                var bonus = rules.SkillBonus(s);
                // Fixed columns so the bonus lines up whether or not a row has a tag or a button.
                var name = Text(SkillRules.Name(s) + " (" + SkillRules.AbilityOf(s).ToString().Substring(0, 3) + ")", "xk-w-name");
                name.style.width = 260; name.style.flexGrow = 0;
                row.Add(name);
                var value = Text(Breakdown.Signed(bonus.Total), "xk-cc-col");
                value.style.width = 70; value.style.flexGrow = 0;
                row.Add(value);
                var status = Contains(granted, s.ToString()) ? "granted: " + string.Join(", ", rules.SkillSources(s)) : classPicks.Contains(s.ToString()) ? "chosen" : string.Empty;
                var tag = Text(status, "xk-w-desc");
                tag.style.flexGrow = 1;
                row.Add(tag);
                var slot = new VisualElement();
                slot.style.width = 110; slot.style.flexShrink = 0;
                if (Contains(cls.SkillOptions, s) && !Contains(granted, s.ToString()))
                {
                    var b = Button(classPicks.Contains(s.ToString()) ? "Remove" : "Choose", "xk-cc-skillpick-" + s, () => { Draft.Toggle(ClassDefinition.SkillChoiceId, skill.ToString()); Changed(); });
                    b.EnableInClassList("xk-w-choice--on", classPicks.Contains(s.ToString()));
                    slot.Add(b);
                }

                row.Add(slot);

                _content.Add(row);
            }

            var species = Draft.Species;
            if (species != null)
                foreach (var ch in species.Choices)
                    if (ch.Kind == ChoiceKind.Skill) _content.Add(SkillChoice(ch, rules, false));
            foreach (var ch in cls.Choices)
                if (ch.Kind == ChoiceKind.Expertise) _content.Add(SkillChoice(ch, rules, true));
        }

        private VisualElement SkillChoice(ChoiceDefinition ch, CharacterRules rules, bool expertise)
        {
            var box = Column("xk-cc-choice");
            var picks = Draft.Picked(ch.Id);
            box.Add(Text($"{ch.Name}: {ch.Prompt} ({picks.Count} of {ch.Count})", "xk-w-heading"));
            var row = Row("xk-cc-wrap");
            foreach (var s in SkillRules.All)
            {
                var skill = s;
                var allowed = expertise ? rules.SkillRank(s) != ProficiencyRank.None || picks.Contains(s.ToString()) : !Contains(rules.GrantedSkills(), s.ToString());
                if (!allowed) continue;
                var b = Button(SkillRules.Name(s), "xk-cc-" + ch.Id.Replace(':', '-') + "-" + s, () => { Draft.Toggle(ch.Id, skill.ToString()); Changed(); });
                b.AddToClassList("xk-w-choice");
                b.EnableInClassList("xk-w-choice--on", picks.Contains(s.ToString()));
                row.Add(b);
            }

            box.Add(row);
            return box;
        }

        private void Equipment()
        {
            var cls = Draft.Class;
            if (cls == null) { _content.Add(Text("Choose a class first.", "xk-w-empty")); return; }
            _content.Add(Text("Your class's gear is added to the clothes you already wear. Pieces marked (worn) are put on; the rest go in your pack.", "xk-w-note"));
            foreach (var g in cls.Equipment)
            {
                var id = CharacterValidation.EquipmentChoiceId(g);
                var box = Column("xk-cc-choice");
                box.Add(Text(char.ToUpperInvariant(g.Id[0]) + g.Id.Substring(1), "xk-w-heading"));
                var row = Row("xk-cc-wrap");
                foreach (var o in g.Options)
                {
                    var option = o;
                    var b = Button(o.Name + "  (" + Kit(o.Items) + ")", "xk-cc-kit-" + g.Id + "-" + o.Id, () => { Draft.Toggle(id, option.Id); if (Draft.Picked(id).Count == 0) Draft.Toggle(id, option.Id); Changed(); });
                    b.AddToClassList("xk-w-choice");
                    b.EnableInClassList("xk-w-choice--on", Draft.Picked(id).Contains(o.Id));
                    row.Add(b);
                }

                box.Add(row);
                _content.Add(box);
            }

            var bg = Draft.Background;
            if (bg != null) _content.Add(Pair("From your background", Kit(bg.Equipment) + $" and {bg.Coin} coin"));
        }

        private void Review()
        {
            var c = Draft.Choices;
            var rules = Draft.Rules;
            _content.Add(Text((string.IsNullOrEmpty(c.Title) ? c.Name : c.Name + " " + c.Title) +
                              $" · {Draft.Species?.Name ?? "?"} {Draft.Background?.Name ?? "?"} · {Draft.Class?.Name ?? "?"} 1", "xk-cc-name"));
            if (Draft.Species?.Appearance.PlaceholderNote != null) _content.Add(Text(Draft.Species.Appearance.PlaceholderNote, "xk-cc-warn"));
            _content.Add(Pair("Age · description", c.Age + (string.IsNullOrEmpty(c.Description) ? string.Empty : " · " + c.Description)));
            var scores = new List<string>();
            foreach (var a in AbilityRules.All) scores.Add($"{a.ToString().Substring(0, 3)} {rules.Score(a)} ({Breakdown.Signed(rules.Modifier(a))})");
            _content.Add(Pair("Abilities", string.Join("  ", scores)));
            if (Draft.Class != null)
            {
                _content.Add(Pair("Hit points", rules.MaxHp + " (" + rules.MaxHitPoints().Text + ")"));
                var saves = new List<string>();
                foreach (var a in AbilityRules.All) if (rules.SaveProficient(a)) saves.Add($"{a} {Breakdown.Signed(rules.SaveBonus(a).Total)}");
                _content.Add(Pair("Saving throws", string.Join(", ", saves)));
                _content.Add(Pair("Armour · weapons", (rules.ArmorProficiencies.Count == 0 ? "no armour" : string.Join(", ", rules.ArmorProficiencies)) + " · " + string.Join(", ", rules.WeaponProficiencies)));
            }

            var skills = new List<string>();
            foreach (var s in SkillRules.All)
                if (rules.SkillRank(s) != ProficiencyRank.None) skills.Add(SkillRules.Name(s) + " " + Breakdown.Signed(rules.SkillBonus(s).Total) + (rules.SkillRank(s) == ProficiencyRank.Expertise ? " (expertise)" : string.Empty));
            _content.Add(Pair("Skills", skills.Count == 0 ? "none yet" : string.Join(", ", skills)));
            _content.Add(Pair("Tools", Names(rules.ToolProficiencies, t => RpgCatalog.FindTool(t)?.Name)));
            _content.Add(Pair("Languages", Names(rules.Languages, l => RpgCatalog.FindLanguage(l)?.Name)));
            if (Draft.Class != null) _content.Add(Pair("Starting equipment", Kit(CharacterCommandHandlers.KitFor(CharacterCommandHandlers.BuildSheet(Draft.Clean(), out _) ?? new CharacterSheetData()))
                                                                              + (Draft.Background != null ? $" and {Draft.Background.Coin} coin" : string.Empty)));
            foreach (var p in Draft.Problems(CreationStep.Review)) _content.Add(Text("• " + p, "xk-cc-warn"));
        }

        // ----- Bits -------------------------------------------------------------------------------------

        private void RefreshProblems()
        {
            var problems = Draft.Problems(Draft.Step);
            _problems.text = problems.Count == 0 ? "✓ This step is complete." : string.Join("  ·  ", problems);
            _problems.EnableInClassList("xk-message--error", problems.Count > 0);
            _steps[Draft.Step].EnableInClassList("xk-cc-step--done", problems.Count == 0);
        }

        private void Summarise()
        {
            if (Draft == null) return;
            var rules = Draft.Rules;
            var c = Draft.Choices;
            var parts = new List<string> { string.IsNullOrEmpty(c.Title) ? c.Name : c.Name + " " + c.Title };
            parts.Add($"{Draft.Species?.Name ?? "—"} · {Draft.Background?.Name ?? "—"} · {(Draft.Class != null ? Draft.Class.Name + " 1" : "—")}");
            if (Draft.Class != null) parts.Add($"HP {rules.MaxHp} · speed {rules.Speed} ft · proficiency +{rules.ProficiencyBonus}");
            var scores = new List<string>();
            foreach (var a in AbilityRules.All) scores.Add($"{a.ToString().Substring(0, 3).ToUpperInvariant()} {rules.Score(a)}");
            parts.Add(string.Join("  ", scores));
            _summary.text = string.Join("\n", parts);
            var note = Draft.Species?.Appearance.PlaceholderNote;
            _placeholder.text = note == null ? string.Empty : "Placeholder body";
            _placeholder.EnableInClassList("xk-hidden", note == null);
        }

        private VisualElement OptionChoice(ChoiceDefinition ch)
        {
            var box = Column("xk-cc-choice");
            box.Add(Text($"{ch.Name}: {ch.Prompt}", "xk-w-heading"));
            var picks = Draft.Picked(ch.Id);
            foreach (var o in ch.Options)
            {
                var option = o;
                var b = Button(o.Name, "xk-cc-" + ch.Id.Replace(':', '-') + "-" + o.Id, () => { if (!picks.Contains(option.Id)) Draft.Toggle(ch.Id, option.Id); Changed(); });
                b.AddToClassList("xk-w-choice");
                b.EnableInClassList("xk-w-choice--on", picks.Contains(o.Id));
                var row = Row("xk-w-pair");
                row.Add(b);
                row.Add(Text(o.Description, "xk-w-value"));
                box.Add(row);
            }

            return box;
        }

        private static int SpeedBonus(SpeciesDefinition s)
        {
            var n = 0;
            foreach (var t in s.Traits) foreach (var e in t.Effects) if (e.Kind == EffectKind.SpeedBonus) n += e.Value;
            return n;
        }

        private static string Bonuses(SpeciesDefinition s)
        {
            var parts = new List<string>();
            foreach (var a in AbilityRules.All) if (s.AbilityBonuses.TryGetValue(a, out var v)) parts.Add(a + " " + Breakdown.Signed(v));
            return string.Join(", ", parts);
        }

        private static string Kit(IReadOnlyList<KitItem> items)
        {
            var parts = new List<string>();
            foreach (var k in items)
                parts.Add((k.Quantity > 1 ? k.Quantity + " × " : string.Empty) + (ItemCatalog.Find(k.ItemId)?.Name ?? k.ItemId) + (k.Equip ? " (worn)" : string.Empty));
            return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
        }

        private static string Names<T>(IEnumerable<T> items, Func<T, string> name)
        {
            var parts = new List<string>();
            foreach (var i in items) parts.Add(name(i) ?? i.ToString());
            return parts.Count == 0 ? "None" : string.Join(", ", parts);
        }

        private static bool Contains<T>(IReadOnlyList<T> list, T x)
        {
            foreach (var y in list) if (EqualityComparer<T>.Default.Equals(x, y)) return true;
            return false;
        }

        private static VisualElement Pair(string key, string value)
        {
            var row = Row("xk-w-pair");
            row.Add(Text(key, "xk-w-key"));
            row.Add(Text(value ?? "-", "xk-w-value"));
            return row;
        }

        private static Button Button(string text, string name, Action act)
        {
            var b = new UnityEngine.UIElements.Button(act) { text = text, name = name };
            b.AddToClassList("xk-button");
            return b;
        }

        private static VisualElement Choices(string label, IReadOnlyList<string> options, int selected, Action<int> pick)
        {
            var row = Row("xk-w-pair");
            row.Add(Text(label, "xk-w-key"));
            for (var i = 0; i < options.Count; i++)
            {
                var index = i;
                var b = Button(options[i], "xk-cc-" + label.ToLowerInvariant().Replace(' ', '-') + "-" + i, () => pick(index));
                b.AddToClassList("xk-w-choice");
                b.EnableInClassList("xk-w-choice--on", i == selected);
                row.Add(b);
            }

            return row;
        }

        private static VisualElement Swatches(string label, IReadOnlyList<(float r, float g, float b)> colours, int selected, Action<int> pick)
        {
            var row = Row("xk-w-pair");
            row.Add(Text(label, "xk-w-key"));
            for (var i = 0; i < colours.Count; i++)
            {
                var index = i;
                var c = colours[i];
                var b = new UnityEngine.UIElements.Button(() => pick(index)) { text = string.Empty, name = "xk-cc-" + label.ToLowerInvariant().Replace(' ', '-') + "-" + i };
                b.AddToClassList("xk-w-swatch");
                b.EnableInClassList("xk-w-swatch--on", i == selected);
                b.style.backgroundColor = new Color(Mathf.Pow(c.r, 0.6f), Mathf.Pow(c.g, 0.6f), Mathf.Pow(c.b, 0.6f));
                row.Add(b);
            }

            return row;
        }
    }
}
