using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Domain.Rpg;
using UnityEngine;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The Player Hub (Phase 5; Phase 11 adds Crafting &amp; work and Storage): Character, Appearance, Inventory, Equipment, Weapons,
    /// Clothing &amp; armour, Currency &amp; possessions, Info — beside a live 3D view of the character
    /// (drag to turn, wheel to zoom). It shows a <see cref="PlayerHub"/> snapshot and raises an event for
    /// every action; the controller dispatches the command and refreshes it. Nothing here changes the world.
    /// </summary>
    public sealed partial class PlayerHubWindow
    {
        public static readonly IReadOnlyList<string> Sections = new[]
        {
            "Character", "Party", "Spells & rest", "Appearance", "Inventory", "Equipment", "Weapons", "Clothing & armour", "Crafting & work", "Storage", "Journal", "Currency & possessions", "Info",
        };

        private readonly Dictionary<string, Button> _tabs = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly VisualElement _content;
        private readonly VisualElement _preview;
        private readonly Label _summary;
        private readonly Label _message;
        private bool _dragging;
        private Vector2 _last;

        public PlayerHubWindow(VisualElement hudRoot)
        {
            Root = new VisualElement { name = "xk-hub" };
            Root.AddToClassList("xk-window");
            Root.AddToClassList("xk-hub");
            Root.AddToClassList("xk-hidden");

            var head = Row("xk-w-head");
            var title = Text("Character", "xk-w-title");
            title.name = "xk-hub-title";
            title.style.flexGrow = 1;
            head.Add(title);
            _summary = Text("-", "xk-w-purse");
            _summary.name = "xk-hub-summary";
            head.Add(_summary);
            var close = new Button(() => CloseRequested?.Invoke()) { text = "Close (H)", name = "xk-hub-close" };
            close.AddToClassList("xk-button");
            head.Add(close);
            Root.Add(head);

            var body = Row("xk-hub-body");
            var nav = Column("xk-hub-nav");
            foreach (var s in Sections)
            {
                var section = s;
                var tab = new Button(() => Select(section)) { text = section, name = "xk-hub-tab-" + section };
                tab.AddToClassList("xk-hub-tab");
                _tabs[s] = tab;
                nav.Add(tab);
            }

            body.Add(nav);
            _content = new ScrollView { name = "xk-hub-content" };
            _content.AddToClassList("xk-hub-content");
            body.Add(_content);

            _preview = new VisualElement { name = "xk-hub-preview" };
            _preview.AddToClassList("xk-hub-preview");
            _preview.Add(Text("Drag to turn · wheel to zoom", "xk-hub-preview-hint"));
            _preview.RegisterCallback<PointerDownEvent>(e =>
            {
                _dragging = true;
                _last = e.position;
                _preview.CapturePointer(e.pointerId);
            });
            _preview.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_dragging) return;
                var dx = e.position.x - _last.x;
                _last = e.position;
                PreviewTurned?.Invoke(-dx * 0.6f);
            });
            _preview.RegisterCallback<PointerUpEvent>(e =>
            {
                _dragging = false;
                _preview.ReleasePointer(e.pointerId);
            });
            _preview.RegisterCallback<WheelEvent>(e => PreviewZoomed?.Invoke(-e.delta.y * 0.08f));
            body.Add(_preview);
            Root.Add(body);

            _message = Text(string.Empty, "xk-w-message");
            _message.name = "xk-hub-message";
            Root.Add(_message);
            hudRoot.Add(Root);
            Section = Sections[0];
        }

        public VisualElement Root { get; }
        public bool IsOpen => !Root.ClassListContains("xk-hidden");
        public string Section { get; private set; }
        public PlayerHub Hub { get; private set; }
        public VisualElement Content => _content;
        public string LastMessage => _message.text;

        public event Action CloseRequested;
        public event Action<ItemStackId> EquipRequested;
        public event Action<EquipmentSlot> UnequipRequested;
        public event Action<ItemStackId> UseRequested;
        public event Action<ItemStackId, int> SplitRequested;
        public event Action<ItemStackId, int> DiscardRequested;
        public event Action<string, int?, string, string> ProfileSaved;
        public event Action<PlayerAppearanceData> AppearanceChosen;
        public event Action<float> PreviewTurned;
        public event Action<float> PreviewZoomed;

        public void SetPreview(Texture texture)
        {
            _preview.style.backgroundImage = texture is RenderTexture rt ? Background.FromRenderTexture(rt) : new StyleBackground(StyleKeyword.None);
        }

        public void Show(PlayerHub hub, WorldTimeSummary time, string message = null, bool failed = false)
        {
            Hub = hub;
            _time = time;
            Root.RemoveFromClassList("xk-hidden");
            if (message != null)
            {
                _message.text = message;
                _message.EnableInClassList("xk-message--error", failed);
            }

            Render();
        }

        public void Hide() => Root.AddToClassList("xk-hidden");

        public void Select(string section)
        {
            if (!_tabs.ContainsKey(section)) return;
            Section = section;
            Render();
        }

        private WorldTimeSummary _time;

        private void Render()
        {
            foreach (var pair in _tabs) pair.Value.EnableInClassList("xk-hub-tab--active", pair.Key == Section);
            Root.Q<Label>("xk-hub-title").text = Section;
            _content.Clear();
            var h = Hub;
            if (h == null) return;
            _summary.text = $"{h.Name} · {h.Purse} coin · {h.FedText} · {Kg(h.CarriedGrams)} carried";
            switch (Section)
            {
                case "Character": Character(h); break;
                case "Party": Party(); break; // Phase 15
                case "Spells & rest": Spells(); break; // Phase 9
                case "Appearance": Appearance(h); break;
                case "Inventory": Inventory(h); break;
                case "Equipment": Equipment(h); break;
                case "Weapons": Filtered(h, "Weapons & armour"); break; // Phase 11: inventory groups (ADR-0055)
                case "Clothing & armour": Filtered(h, "Clothing"); break;
                case "Crafting & work": Workshop(); break;
                case "Storage": Storage(); break;
                case "Journal": Journal(); break; // Phase 12
                case "Currency & possessions": Possessions(h); break;
                default: Info(h); break;
            }
        }

        // ----- Sections ---------------------------------------------------------------------------------

        /// <summary>Phase 6: the character for the sheet (set by the controller before showing).</summary>
        public CharacterSnapshot Sheet { get; set; }

        /// <summary>The Character section's sub-panel: Sheet, Skills, Features, Proficiencies, Identity.</summary>
        public string CharacterPanel { get; private set; } = "Sheet";

        public static readonly IReadOnlyList<string> CharacterPanels = new[] { "Sheet", "Skills", "Features", "Proficiencies", "Progression", "Identity" };

        public event Action CreateCharacterRequested;

        /// <summary>Phase 7: the player asked to level up.</summary>
        public event Action LevelUpRequested;

        public void SelectCharacterPanel(string panel)
        {
            CharacterPanel = panel;
            Section = Sections[0];
            Render();
        }

        private void Character(PlayerHub h)
        {
            var ch = Sheet;
            if (ch == null || ch.NeedsCreation)
            {
                var banner = Column("xk-cc-banner");
                banner.name = "xk-hub-create-banner";
                banner.Add(Text(ch != null && ch.State == CreationState.Legacy
                    ? "Your character has no species, background or class yet: they travelled before these existed. Set them up once; everything you own and have done is kept."
                    : "You have not created your character yet: species, background, class, abilities and skills.", "xk-w-value"));
                var create = new Button(() => CreateCharacterRequested?.Invoke()) { text = "Create character (C)", name = "xk-hub-create" };
                create.AddToClassList("xk-button");
                create.AddToClassList("xk-button--primary");
                banner.Add(create);
                _content.Add(banner);
                Profile(h, true);
                return;
            }

            var tabs = Row("xk-cc-wrap");
            foreach (var p in CharacterPanels)
            {
                var panel = p;
                var b = new Button(() => SelectCharacterPanel(panel)) { text = panel, name = "xk-hub-charpanel-" + panel };
                b.AddToClassList("xk-button");
                b.AddToClassList("xk-w-choice");
                b.EnableInClassList("xk-w-choice--on", panel == CharacterPanel);
                tabs.Add(b);
            }

            _content.Add(tabs);
            var r = ch.Rules;
            switch (CharacterPanel)
            {
                case "Skills":
                    _content.Add(Text("Each skill adds its ability modifier, and your proficiency bonus if you are proficient (twice with expertise). The sources are listed.", "xk-w-note"));
                    foreach (var s in SkillRules.All)
                    {
                        var bonus = r.SkillBonus(s);
                        var rank = r.SkillRank(s);
                        var row = Row("xk-w-row");
                        row.name = "xk-hub-skill-" + s;
                        row.Add(Text((rank == ProficiencyRank.None ? "○ " : rank == ProficiencyRank.Expertise ? "◆ " : "● ") + SkillRules.Name(s), "xk-w-name"));
                        row.Add(Text(Breakdown.Signed(bonus.Total), "xk-cc-col"));
                        var sources = r.SkillSources(s);
                        var roll = r.SkillRoll(s);
                        row.Add(Text(bonus.Text + (sources.Count > 0 ? " · from " + string.Join(", ", sources) : string.Empty) + (roll.Text.Length > 0 ? " · " + roll.Text : string.Empty), "xk-w-desc"));
                        _content.Add(row);
                    }

                    _content.Add(Text($"Passive Perception {r.PassivePerception}, Insight {r.Passive(Skill.Insight).Total}, Investigation {r.Passive(Skill.Investigation).Total}. ● proficient  ◆ expertise  ○ untrained." +
                                      (r.ProficientCheckFloor > 0 ? $" Reliable Talent: a d20 below {r.ProficientCheckFloor} counts as {r.ProficientCheckFloor} on proficient checks." : string.Empty), "xk-w-desc"));
                    break;
                case "Features":
                    foreach (var (source, feature) in r.Features)
                    {
                        var needs = CharacterRules.Needs(feature);
                        var scaling = feature.ScalingId != null ? r.Class.FindScaling(feature.ScalingId) : null;
                        _content.Add(Pair(feature.Name + (scaling != null ? " (" + scaling.Text(r.Level) + ")" : string.Empty),
                            feature.Description + "  (" + source + ")" + (needs != IntegrationNeed.None ? "  · full effect with " + CharacterAdvancementWindow.NeedsText(needs) : string.Empty)));
                    }

                    if (r.Scalings.Count > 0)
                    {
                        _content.Add(Text("Class table", "xk-w-heading"));
                        foreach (var (scaling, text) in r.Scalings) _content.Add(Pair(scaling.Name, text));
                    }

                    _content.Add(Text("Resources", "xk-w-heading"));
                    var any = false;
                    foreach (var res in r.ResourceDefinitions)
                    {
                        any = true;
                        var left = ch.Player.Rpg.Resources.TryGetValue(res.Id, out var v) ? v : 0;
                        var row = Pair(res.Name, $"{left} / {r.ResourceMax(res)} · recovers on a {(r.RecoveryOf(res) == ResourceRecovery.ShortRest ? "short" : "long")} rest" + (res.Amount == RecoveryAmount.Half ? " (half)" : string.Empty));
                        row.name = "xk-hub-resource-" + res.Id;
                        _content.Add(row);
                    }

                    if (!any) _content.Add(Text("No limited resources.", "xk-w-empty"));
                    if (r.Class.Spellcasting != null)
                    {
                        var sc = r.Class.Spellcasting;
                        _content.Add(Pair("Spellcasting", $"{sc.Ability}; save DC {r.SpellSaveDc().Total}, attack {Breakdown.Signed(r.SpellAttack().Total)}; {r.CantripsKnown} cantrips, {r.PreparedSpells} prepared; slots: {r.SpellSlotText}; focus: {sc.Focus}. Spells arrive with the magic phase."));
                    }

                    break;
                case "Progression":
                    Progression(ch);
                    break;
                case "Proficiencies":
                    _content.Add(Pair("Armour", r.ArmorProficiencies.Count == 0 ? "None" : string.Join(", ", r.ArmorProficiencies)));
                    _content.Add(Pair("Weapons", string.Join(", ", r.WeaponProficiencies)));
                    var tools = new List<string>();
                    foreach (var t in r.ToolProficiencies) tools.Add(r.ToolName(t));
                    _content.Add(Pair("Tools", tools.Count == 0 ? "None" : string.Join(", ", tools)));
                    var langs = new List<string>();
                    foreach (var l in r.Languages) langs.Add(r.LanguageName(l));
                    _content.Add(Pair("Languages", string.Join(", ", langs)));
                    var senses = new List<string>();
                    if (r.Darkvision > 0) senses.Add($"Darkvision {r.Darkvision} ft");
                    foreach (var (sense, range) in r.Senses) senses.Add($"{char.ToUpperInvariant(sense[0])}{sense.Substring(1).Replace('_', ' ')} {range} ft");
                    _content.Add(Pair("Senses", senses.Count > 0 ? string.Join(", ", senses) : "Normal sight"));
                    _content.Add(Pair("Movement", "Walk " + r.SpeedBreakdown().Text.Replace(" +", " ") + (r.MovementModes.Count > 0 ? " · " + string.Join(", ", r.MovementModes) : string.Empty)));
                    if (r.FightingStyles.Count > 0) _content.Add(Pair("Fighting styles", string.Join(", ", r.FightingStyles).Replace('_', ' ')));
                    _content.Add(Pair("Resistances", r.Resistances.Count == 0 ? "None" : string.Join(", ", r.Resistances)));
                    _content.Add(Pair("Advantage on saves", r.SaveAdvantages.Count == 0 ? "None" : "against " + string.Join(", ", r.SaveAdvantages)));
                    break;
                case "Identity":
                    Profile(h, false);
                    break;
                default:
                    _content.Add(Text(ch.FullName, "xk-cc-name"));
                    _content.Add(Text($"{r.Species.Name} · {r.Background.Name} · {r.Class.Name} {r.Level}" + (r.Subclass != null ? " (" + r.Subclass.Name + ")" : string.Empty), "xk-w-value"));
                    if (r.Species.Appearance.PlaceholderNote != null) _content.Add(Text(r.Species.Appearance.PlaceholderNote, "xk-cc-warn"));
                    _content.Add(ExperienceBar(ch));
                    if (ch.VitalText != null) _content.Add(Text(ch.VitalText, "xk-cc-warn")); // Phase 8
                    if (ch.CanAdvance) _content.Add(LevelUpBanner(ch));
                    var vit = Row("xk-cc-wrap");
                    vit.name = "xk-hub-vitals";
                    foreach (var (k, v) in new[]
                             {
                                 ("Hit points", $"{ch.Player.Rpg.CurrentHp} / {r.MaxHp}" + (ch.Player.Rpg.TemporaryHp > 0 ? $" (+{ch.Player.Rpg.TemporaryHp})" : string.Empty)),
                                 ("Armour class", ch.ArmorClass.Total.ToString(CultureInfo.InvariantCulture)),
                                 ("Speed", r.Speed + " ft"),
                                 ("Proficiency", "+" + r.ProficiencyBonus),
                                 ("Initiative", Breakdown.Signed(r.Initiative().Total) + (r.InitiativeRoll.HasAdvantage ? " (adv.)" : string.Empty)),
                                 ("Hit dice", (ch.Player.Rpg.Resources.TryGetValue("hit_dice", out var hd) ? hd : 0) + " / " + r.Level + " × d" + r.HitDie),
                             })
                    {
                        var card = Column("xk-cc-card");
                        card.Add(Text(v, "xk-cc-card-value"));
                        card.Add(Text(k, "xk-cc-card-label"));
                        vit.Add(card);
                    }

                    _content.Add(vit);
                    _content.Add(Text("Armour class: " + ch.ArmorClass.Text + " (provisional until combat defines armour types).", "xk-w-desc"));
                    _content.Add(Text("Abilities", "xk-w-heading"));
                    var head = Row("xk-w-row");
                    foreach (var t in new[] { "Ability", "Score", "Modifier", "Save" }) head.Add(Text(t, "xk-cc-col"));
                    _content.Add(head);
                    foreach (var a in AbilityRules.All)
                    {
                        var row = Row("xk-w-row");
                        row.name = "xk-hub-ability-" + a;
                        row.Add(Text(a.ToString(), "xk-cc-col"));
                        row.Add(Text(r.Score(a).ToString(CultureInfo.InvariantCulture), "xk-cc-col"));
                        row.Add(Text(Breakdown.Signed(r.Modifier(a)), "xk-cc-col"));
                        row.Add(Text((r.SaveProficient(a) ? "● " : "○ ") + Breakdown.Signed(r.SaveBonus(a).Total), "xk-cc-col"));
                        row.Add(Text(r.ScoreBreakdown(a).Text, "xk-w-desc"));
                        _content.Add(row);
                    }

                    _content.Add(Pair("Initiative", r.Initiative().Text + (r.InitiativeRoll.Text.Length > 0 ? " · " + r.InitiativeRoll.Text : string.Empty)));
                    _content.Add(Pair("Speed", r.SpeedBreakdown().Text + " ft"));

                    _content.Add(Pair("Condition", $"{h.FedText} (fed {h.Fed}/100) · {h.SpeedReason} ({Mathf.RoundToInt(h.SpeedFactor * 100)}% speed)"));
                    _content.Add(Pair("Arms", $"{h.Weapon} (damage {h.Damage})"));
                    break;
            }
        }

        // ----- Phase 7: experience and progression ----------------------------------------------------------

        private static VisualElement ExperienceBar(CharacterSnapshot ch)
        {
            var box = Column("xk-cc-choice");
            box.name = "xk-hub-xp";
            var next = ch.NextLevelExperience;
            box.Add(Text(next != null
                ? $"Experience {ch.Experience.ToString("N0", CultureInfo.InvariantCulture)} / {next.Value.ToString("N0", CultureInfo.InvariantCulture)} for level {ch.Level + 1}"
                : $"Experience {ch.Experience.ToString("N0", CultureInfo.InvariantCulture)} · level 20, the highest", "xk-w-value"));
            var track = new VisualElement { name = "xk-hub-xp-track" };
            track.AddToClassList("xk-xp-track");
            var fill = new VisualElement { name = "xk-hub-xp-fill" };
            fill.AddToClassList("xk-xp-fill");
            fill.style.width = new Length(Mathf.Clamp01(ch.Progress) * 100f, LengthUnit.Percent);
            track.Add(fill);
            box.Add(track);
            return box;
        }

        private VisualElement LevelUpBanner(CharacterSnapshot ch)
        {
            var banner = Column("xk-cc-banner");
            banner.name = "xk-hub-levelup-banner";
            banner.Add(Text(ch.LevelsAvailable == 1
                ? $"You have enough experience for level {ch.Level + 1}."
                : $"You have enough experience for {ch.LevelsAvailable} more levels (up to {ch.Level + ch.LevelsAvailable}); each is taken in turn with its own choices.", "xk-w-value"));
            if (ch.Outstanding.Count > 0) banner.Add(Text("Some level-1 choices added since your character was made will be asked for too.", "xk-w-desc"));
            var b = new Button(() => LevelUpRequested?.Invoke()) { text = $"Level up to {ch.Level + 1} (L)", name = "xk-hub-levelup" };
            b.AddToClassList("xk-button");
            b.AddToClassList("xk-button--primary");
            banner.Add(b);
            return banner;
        }

        private void Progression(CharacterSnapshot ch)
        {
            var r = ch.Rules;
            var s = ch.Player.Rpg;
            _content.Add(ExperienceBar(ch));
            if (ch.CanAdvance) _content.Add(LevelUpBanner(ch));

            _content.Add(Text("Levels gained", "xk-w-heading"));
            if (s.Advancements.Count == 0) _content.Add(Text("None yet: experience from quests, encounters and discoveries opens the next level.", "xk-w-empty"));
            foreach (var a in s.Advancements)
            {
                var row = Pair($"Level {a.Level}", a.Summary + (a.Date.IsValidDate ? "  (" + a.Date.ToIsoString() + ")" : string.Empty));
                row.name = "xk-hub-advancement-" + a.Level;
                _content.Add(row);
            }

            _content.Add(Text("Experience", "xk-w-heading"));
            if (s.ExperienceLog.Count == 0) _content.Add(Text("No experience earned yet.", "xk-w-empty"));
            for (var i = s.ExperienceLog.Count - 1; i >= Math.Max(0, s.ExperienceLog.Count - 12); i--)
            {
                var e = s.ExperienceLog[i];
                _content.Add(Pair("+" + e.Amount.ToString("N0", CultureInfo.InvariantCulture), (e.Reason.Length > 0 ? e.Reason : e.Source.ToString()) + " · " + e.Source + (e.Date.IsValidDate ? " · " + e.Date.ToIsoString() : string.Empty)));
            }

            if (s.ExperienceLog.Count > 12) _content.Add(Text($"… and {s.ExperienceLog.Count - 12} earlier awards.", "xk-w-desc"));

            _content.Add(Text($"Ahead: {r.Class.Name}", "xk-w-heading"));
            for (var level = r.Level + 1; level <= Math.Min(AbilityRules.MaxLevel, r.Level + 4); level++)
            {
                var names = new List<string>();
                if (r.Class.LaterFeatures.TryGetValue(level, out var fs)) foreach (var f in fs) names.Add(f.Name);
                if (r.Subclass != null && r.Subclass.Features.TryGetValue(level, out var sfs)) foreach (var f in sfs) names.Add(f.Name);
                _content.Add(Pair($"Level {level} ({ProgressionRules.ExperienceFor(level).ToString("N0", CultureInfo.InvariantCulture)} XP)", names.Count == 0 ? "Hit points and class-table increases" : string.Join(", ", names)));
            }

            if (ch.Outstanding.Count > 0)
            {
                _content.Add(Text("Open choices", "xk-w-heading"));
                foreach (var o in ch.Outstanding) _content.Add(Pair(o.Choice.Name, o.Choice.Prompt + " (made with your next level-up)"));
            }
        }

        private void Profile(PlayerHub h, bool legacyBackground)
        {
            _content.Add(Text("Identity", "xk-w-heading"));
            _content.Add(Pair("Where", h.Location));
            var name = new TextField("Name") { value = h.Name, name = "xk-hub-name", maxLength = PlayerCharacterData.MaxNameLength };
            var age = new IntegerField("Age") { value = h.Age, name = "xk-hub-age" };
            var background = new TextField("Background") { value = h.Background, name = "xk-hub-background", maxLength = PlayerCommandHandlers.MaxBackgroundLength };
            var description = new TextField("Description") { value = h.Description, multiline = true, name = "xk-hub-description", maxLength = PlayerCommandHandlers.MaxDescriptionLength };
            background.SetEnabled(legacyBackground);
            foreach (var f in new VisualElement[] { name, age, background, description }) f.AddToClassList("xk-hub-field");
            _content.Add(name);
            _content.Add(age);
            _content.Add(background);
            _content.Add(description);
            if (Sheet != null && !string.IsNullOrEmpty(Sheet.History)) _content.Add(Pair("History", Sheet.History));
            var save = new Button(() => ProfileSaved?.Invoke(name.value, age.value, legacyBackground ? background.value : null, description.value)) { text = "Save identity", name = "xk-hub-save" };
            save.AddToClassList("xk-button");
            save.AddToClassList("xk-button--primary");
            _content.Add(save);
        }

        private void Appearance(PlayerHub h)
        {
            var a = h.Appearance;
            _content.Add(Text("Only what the character art can show is offered.", "xk-w-note"));
            _content.Add(Choices("Frame", new[] { "Male", "Female" }, a.Frame == NpcSex.Female ? 1 : 0, i =>
            {
                var frame = i == 1 ? NpcSex.Female : NpcSex.Male;
                Choose(new PlayerAppearanceData
                {
                    Frame = frame, HairId = PlayerAppearanceOptions.HairFor(frame)[0], BeardId = null, SkinTone = a.SkinTone, HairColour = a.HairColour, ClothTint = a.ClothTint,
                });
            }));
            var hair = PlayerAppearanceOptions.HairFor(a.Frame);
            var labels = new List<string>();
            var current = 0;
            for (var i = 0; i < hair.Count; i++)
            {
                labels.Add(hair[i] == null ? "None" : hair[i].Replace("Hair_", string.Empty));
                if (hair[i] == a.HairId) current = i;
            }

            _content.Add(Choices("Hair", labels, current, i => Choose(With(a, hairId: hair[i], keepHair: false))));
            if (a.Frame == NpcSex.Male) _content.Add(Choices("Beard", new[] { "Beard", "Clean-shaven" }, a.BeardId == null ? 1 : 0, i => Choose(With(a, beard: i == 0 ? "Hair_Beard" : null, keepBeard: false))));
            _content.Add(Swatches("Skin", NpcAppearanceResolver.SkinTones, a.SkinTone, i => Choose(With(a, skin: i))));
            _content.Add(Swatches("Hair colour", NpcAppearanceResolver.HairColours, a.HairColour, i => Choose(With(a, hairColour: i))));
            _content.Add(Swatches("Cloth tint", NpcAppearanceResolver.OutfitTints, a.ClothTint, i => Choose(With(a, tint: i))));
        }

        private void Choose(PlayerAppearanceData data) => AppearanceChosen?.Invoke(data);

        private static PlayerAppearanceData With(PlayerAppearanceData a, string hairId = null, bool keepHair = true, string beard = null, bool keepBeard = true,
            int? skin = null, int? hairColour = null, int? tint = null) => new PlayerAppearanceData
        {
            Frame = a.Frame,
            HairId = keepHair ? a.HairId : hairId,
            BeardId = keepBeard ? a.BeardId : beard,
            SkinTone = skin ?? a.SkinTone,
            HairColour = hairColour ?? a.HairColour,
            ClothTint = tint ?? a.ClothTint,
        };

        private void Possessions(PlayerHub h)
        {
            _content.Add(Pair("Purse", h.Purse + " coin"));
            _content.Add(Pair("Possessions worth", h.PossessionsValue + " coin (base value)"));
            _content.Add(Pair("Carried", $"{Kg(h.CarriedGrams)} of {Kg(h.EncumbranceGrams)} before it slows you"));
            _content.Add(Text("Recent trades", "xk-w-heading"));
            if (h.RecentTrades.Count == 0) _content.Add(Text("None yet.", "xk-w-empty"));
            foreach (var t in h.RecentTrades) _content.Add(Text(t.Text, "xk-w-ledger-line"));
        }

        private void Info(PlayerHub h)
        {
            if (_time != null) _content.Add(Pair("Time", _time.Text));
            _content.Add(Text("People who know you", "xk-w-heading"));
            if (h.KnownBy.Count == 0) _content.Add(Text("Nobody yet. Talk and trade to become known.", "xk-w-empty"));
            foreach (var k in h.KnownBy) _content.Add(Pair(k.Name, $"{k.Role} · familiarity {k.Familiarity}, regard {k.Affinity} · {k.Why}"));
            _content.Add(Text("How regard grows: once a day per person, from talking (+2 familiarity) and trading (+3 familiarity, +1 regard). Repeating it the same day adds nothing.", "xk-w-note"));
            _content.Add(Text("Controls", "xk-w-heading"));
            _content.Add(Text("H hub · E talk / trade at a counter · F practice fight · T wait an hour · P pause time · Esc free the cursor or close a window", "xk-w-note"));
            ShowCredits();
        }

        /// <summary>Phase 8: the shipped credits and the SRD attribution (the same text as CREDITS.md).</summary>
        private void ShowCredits()
        {
            var box = new VisualElement { name = "xk-hub-credits" };
            box.Add(Text("Credits", "xk-w-heading"));
            box.Add(Text(Realmkeeper.Application.Credits.SrdNotice, "xk-w-note"));
            box.Add(Text(Realmkeeper.Application.Credits.Srd51Notice, "xk-w-note"));
            box.Add(Text(Realmkeeper.Application.Credits.RulesUse, "xk-w-note"));
            foreach (var (work, author, licence) in Realmkeeper.Application.Credits.Assets) box.Add(Pair(author, work + " · " + licence));
            _content.Add(box);
        }

        // ----- Bits -------------------------------------------------------------------------------------

        private static VisualElement Pair(string key, string value)
        {
            var row = Row("xk-w-pair");
            row.Add(Text(key, "xk-w-key"));
            row.Add(Text(value ?? "-", "xk-w-value"));
            return row;
        }

        private static Button Act(string label, Action act)
        {
            var b = new Button(act) { text = label };
            b.AddToClassList("xk-button");
            b.AddToClassList("xk-w-act");
            return b;
        }

        private static VisualElement Choices(string label, IReadOnlyList<string> options, int selected, Action<int> pick)
        {
            var row = Row("xk-w-pair");
            row.Add(Text(label, "xk-w-key"));
            for (var i = 0; i < options.Count; i++)
            {
                var index = i;
                var b = new Button(() => pick(index)) { text = options[i], name = "xk-hub-" + label.ToLowerInvariant().Replace(' ', '-') + "-" + i };
                b.AddToClassList("xk-button");
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
                var b = new Button(() => pick(index)) { text = string.Empty, name = "xk-hub-" + label.ToLowerInvariant().Replace(' ', '-') + "-" + i };
                b.AddToClassList("xk-w-swatch");
                b.EnableInClassList("xk-w-swatch--on", i == selected);
                b.style.backgroundColor = new Color(Mathf.Pow(c.r, 0.6f), Mathf.Pow(c.g, 0.6f), Mathf.Pow(c.b, 0.6f));
                row.Add(b);
            }

            return row;
        }

        private static string Kg(int grams) => (grams / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + " kg";
    }
}
