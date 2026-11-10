using System;
using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using UnityEngine.UIElements;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// The people part of the HUD (Phase 4): the People list with filters, the NPC inspector (ten
    /// explained sections, plus the DM's tools behind "Manage"), and the Residents section of the
    /// settlement inspector. Query snapshots in, typed commands out; it never touches domain objects.
    /// </summary>
    public sealed partial class RealmkeeperHud
    {
        public const string AllChoice = "All";

        private NpcId _selectedNpc;
        private bool _manageNpc;
        private readonly NpcFilter _peopleFilter = new NpcFilter();
        private DropdownField _filterSettlement, _filterRole, _filterTier;
        private TextField _filterName;
        private readonly List<SettlementId> _filterSettlementIds = new List<SettlementId>();

        public VisualElement PeoplePanel { get; private set; }
        public ScrollView PeopleList { get; private set; }
        public VisualElement NpcInspector { get; private set; }
        public VisualElement NpcInspectorBody => Root?.Q<ScrollView>("ninsp-body");
        public VisualElement SettlementResidents => Root?.Q<VisualElement>("insp-residents");
        public NpcId SelectedNpcId => _selectedNpc;
        public bool ManagingNpc => _manageNpc;
        public bool NpcInspectorVisible => NpcInspector != null && !NpcInspector.ClassListContains("rk-hidden");
        public bool PeoplePanelVisible => PeoplePanel != null && !PeoplePanel.ClassListContains("rk-hidden");

        private void BindPeople()
        {
            PeoplePanel = Root.Q<VisualElement>("people-panel");
            NpcInspector = Root.Q<VisualElement>("npc-inspector");
            Root.Q<Button>("nav-people").clicked += () => ShowPeople(!PeoplePanelVisible);
            Root.Q<Button>("ninsp-close").clicked += () => SelectNpc(default);
            Root.Q<Button>("ninsp-manage").clicked += () => SetManagingNpc(!_manageNpc);

            var filters = new VisualElement { name = "people-filters" };
            filters.AddToClassList("rk-people-filters");
            _filterSettlement = new DropdownField("Place") { name = "people-filter-settlement" };
            _filterRole = new DropdownField("Role", new List<string> { AllChoice }.Concat(Enum.GetNames(typeof(NpcRole))).ToList(), 0) { name = "people-filter-role" };
            _filterTier = new DropdownField("Tier", new List<string> { AllChoice, "Notable", "Resident" }, 0) { name = "people-filter-tier" };
            _filterName = new TextField("Name") { name = "people-filter-name" };
            _filterSettlement.RegisterValueChangedCallback(_ => ApplyFilterControls());
            _filterRole.RegisterValueChangedCallback(_ => ApplyFilterControls());
            _filterTier.RegisterValueChangedCallback(_ => ApplyFilterControls());
            _filterName.RegisterValueChangedCallback(_ => ApplyFilterControls());
            filters.Add(_filterSettlement);
            filters.Add(_filterRole);
            filters.Add(_filterTier);
            filters.Add(_filterName);
            PeoplePanel.Add(filters);
            PeopleList = new ScrollView { name = "people-list" };
            PeopleList.AddToClassList("rk-people-list");
            PeoplePanel.Add(PeopleList);
        }

        public void ShowPeople(bool show)
        {
            PeoplePanel?.EnableInClassList("rk-hidden", !show);
            if (show) RefreshPeople();
        }

        /// <summary>Sets the list filter (tests and code); null leaves a field as it is.</summary>
        public void SetPeopleFilter(SettlementId? settlement = null, NpcRole? role = null, NpcTier? tier = null, string name = null, bool clearRole = false, bool clearTier = false)
        {
            if (settlement.HasValue) _peopleFilter.Settlement = settlement.Value;
            if (role.HasValue || clearRole) _peopleFilter.Role = role;
            if (tier.HasValue || clearTier) _peopleFilter.Tier = tier;
            if (name != null) _peopleFilter.NameContains = name;
            RefreshPeopleList();
        }

        private void ApplyFilterControls()
        {
            var i = _filterSettlement.index;
            _peopleFilter.Settlement = i > 0 && i - 1 < _filterSettlementIds.Count ? _filterSettlementIds[i - 1] : default;
            _peopleFilter.Role = _filterRole.index > 0 ? (NpcRole?)(NpcRole)Enum.Parse(typeof(NpcRole), _filterRole.value) : null;
            _peopleFilter.Tier = _filterTier.index == 1 ? NpcTier.Notable : _filterTier.index == 2 ? NpcTier.Resident : (NpcTier?)null;
            _peopleFilter.NameContains = _filterName.value;
            RefreshPeopleList();
        }

        public void SetManagingNpc(bool manage)
        {
            _manageNpc = manage;
            var button = Root?.Q<Button>("ninsp-manage");
            if (button != null) button.text = manage ? "Done" : "Manage";
            RefreshNpcInspector();
        }

        /// <summary>Opens a person's inspector (closing the settlement and faction inspectors); empty closes it.</summary>
        public void SelectNpc(NpcId id)
        {
            if (!id.IsEmpty && (_session == null || _session.FindNpc(id) == null)) id = default;
            _selectedNpc = id;
            if (!id.IsEmpty)
            {
                if (_selection.HasSelection) _selection.Clear();
                if (!_selectedFaction.IsEmpty) SelectFaction(default);
            }

            RefreshPeopleList();
            RefreshNpcInspector();
        }

        private void CloseNpcInspectorForOtherSelection()
        {
            if (_selectedNpc.IsEmpty) return;
            _selectedNpc = default;
            RefreshNpcInspector();
        }

        private void RefreshPeople()
        {
            if (Root == null || PeoplePanel == null) return;
            var names = new List<string> { AllChoice };
            _filterSettlementIds.Clear();
            if (_session != null && _session.HasWorld)
            {
                foreach (var s in _session.GetSettlements())
                {
                    names.Add(s.Name);
                    _filterSettlementIds.Add(s.SettlementId);
                }
            }

            var current = _peopleFilter.Settlement.IsEmpty ? 0 : _filterSettlementIds.IndexOf(_peopleFilter.Settlement) + 1;
            _filterSettlement.choices = names;
            _filterSettlement.SetValueWithoutNotify(names[Math.Max(0, current)]);
            RefreshPeopleList();
            RefreshNpcInspector();
        }

        private void RefreshPeopleList()
        {
            if (PeopleList == null) return;
            PeopleList.Clear();
            if (_session == null || !_session.HasWorld) return;
            foreach (var p in _session.GetNpcs(_peopleFilter))
            {
                var id = p.NpcId;
                var item = new Button(() => SelectNpc(id)) { name = "person-" + id };
                item.AddToClassList("rk-settlement-item");
                item.AddToClassList("rk-person-item");
                var text = new VisualElement();
                text.AddToClassList("rk-faction-text");
                text.Add(Cell(p.Name + (p.Tier == NpcTier.Notable ? " ★" : string.Empty), "rk-faction-name"));
                text.Add(Cell($"{p.Role} · {p.SettlementName} · {p.LocationName}", "rk-person-sub"));
                item.Add(text);
                item.tooltip = $"{p.Name}, {p.Age}, {p.Role} of {p.SettlementName} ({p.FactionName}). Today: {p.LocationName}. Morale {p.Morale}.";
                item.EnableInClassList("rk-settlement-item--selected", id == _selectedNpc);
                PeopleList.Add(item);
            }

            if (PeopleList.childCount == 0) PeopleList.Add(Cell("Nobody matches.", "rk-trade-line"));
        }

        private void RefreshNpcInspector()
        {
            if (Root == null || NpcInspector == null) return;
            var d = !_selectedNpc.IsEmpty && _session != null && _session.HasWorld ? _session.GetNpcDetail(_selectedNpc) : null;
            NpcInspector.EnableInClassList("rk-hidden", d == null);
            if (d == null) return;
            Root.Q<Label>("ninsp-name").text = d.Name;
            Root.Q<Label>("ninsp-type").text = d.Subtitle;
            var body = Root.Q<ScrollView>("ninsp-body");
            body.Clear();

            void SectionOf(string title, IReadOnlyList<NpcLine> lines, string empty, Action<VisualElement, NpcLine> extra = null)
            {
                var header = Section(title);
                header.name = "ninsp-section-" + title.ToLowerInvariant();
                body.Add(header);
                if (NpcDetail.Explanations.TryGetValue(title, out var explain)) body.Add(Cell(explain, "rk-npc-explain"));
                if (lines.Count == 0) body.Add(Cell(empty, "rk-trade-line"));
                foreach (var l in lines)
                {
                    var line = new VisualElement();
                    line.AddToClassList("rk-npc-line");
                    var main = new VisualElement();
                    main.AddToClassList("rk-npc-line-main");
                    main.Add(Cell(l.Label, "rk-npc-line-key"));
                    main.Add(Cell(l.Value, "rk-npc-line-value"));
                    line.Add(main);
                    if (!string.IsNullOrEmpty(l.Why)) line.Add(Cell(l.Why, "rk-npc-line-why"));
                    extra?.Invoke(line, l);
                    body.Add(line);
                }
            }

            SectionOf("Identity", d.Identity, "-");
            SectionOf("Personality", d.Personality, "-");
            SectionOf("Skills", d.Skills, "-");
            SectionOf("Condition", d.Condition, "-");
            SectionOf("Goals", d.Goals, "No goals.");
            SectionOf("Whereabouts", d.Whereabouts, "-");
            SectionOf("Relationships", d.Relationships, "Nobody yet.");
            SectionOf("Memories", d.Memories, "Nothing remembered yet.");
            SectionOf("Knowledge", d.Knowledge, "Knows nothing worth recording.");
            SectionOf("History", d.History, "Nothing in the chronicle yet.");

            if (_manageNpc && d.IsAlive) body.Add(BuildNpcTools(d));
        }

        // ----- The DM's tools ---------------------------------------------------------------------------

        private VisualElement BuildNpcTools(NpcDetail d)
        {
            var id = d.NpcId;
            var tools = new VisualElement { name = "ninsp-tools" };
            tools.AddToClassList("rk-npc-tools");
            tools.Add(Section("DM tools"));
            tools.Add(Cell("Changes made here are recorded in the chronicle as the DM's.", "rk-npc-explain"));
            var dm = ChangeSource.DungeonMaster;
            var person = _session.FindNpc(id);
            var locations = new List<LocationSummary>();
            foreach (var s in _session.GetSettlements()) locations.AddRange(_session.GetLocations(s.SettlementId));
            string Where(LocationSummary l) => (_session.FindSettlement(l.SettlementId)?.Name ?? "?") + " – " + l.Name;
            var home = locations.Where(l => l.SettlementId == person.HomeSettlementId).ToList();

            VisualElement ToolRow(params VisualElement[] parts)
            {
                var row = new VisualElement();
                row.AddToClassList("rk-npc-tool-row");
                foreach (var p in parts) row.Add(p);
                return row;
            }

            var name = new TextField { name = "tool-name", value = d.Name };
            tools.Add(ToolRow(name, SmallButton("Rename", "tool-rename", () => Do(new EditNpcIdentityCommand(id, name.value, source: dm)))));

            var roles = Enum.GetNames(typeof(NpcRole)).ToList();
            var role = new DropdownField(roles, roles.IndexOf(person.Role.ToString())) { name = "tool-role" };
            tools.Add(ToolRow(role, SmallButton("Set work", "tool-occupation", () =>
                Do(new SetNpcOccupationCommand(id, (NpcRole)Enum.Parse(typeof(NpcRole), role.value), source: dm)))));

            var move = new DropdownField(locations.Select(Where).ToList(), 0) { name = "tool-move" };
            tools.Add(ToolRow(move, SmallButton("Move", "tool-move-button", () => Do(new MoveNpcCommand(id, locations[move.index].LocationId, dm)))));

            if (home.Count > 0)
            {
                var homeNames = home.Select(l => l.Name).ToList();
                var work = new DropdownField(homeNames, 0) { name = "tool-routine-work" };
                var rest = new DropdownField(homeNames, Math.Max(0, home.FindIndex(l => l.Kind == LocationKind.Tavern))) { name = "tool-routine-rest" };
                var restDay = new DropdownField(Enumerable.Range(1, 7).Select(i => "Day " + i).ToList(), 6) { name = "tool-routine-day" };
                tools.Add(Cell("Routine: works at, rests at, rest day", "rk-npc-explain"));
                tools.Add(ToolRow(work));
                tools.Add(ToolRow(rest, restDay));
                tools.Add(ToolRow(SmallButton("Set routine", "tool-routine", () =>
                {
                    var days = new LocationId[7];
                    for (var i = 0; i < 7; i++) days[i] = i == restDay.index ? home[rest.index].LocationId : home[work.index].LocationId;
                    Do(new SetNpcScheduleCommand(id, days, dm));
                })));
            }

            var goals = Enum.GetNames(typeof(NpcGoalType)).ToList();
            var goal = new DropdownField(goals, goals.IndexOf(nameof(NpcGoalType.GainStanding))) { name = "tool-goal" };
            var priority = new SliderInt(0, 100) { name = "tool-goal-priority", value = 60, showInputField = true };
            tools.Add(ToolRow(goal, priority, SmallButton("Set goal", "tool-goal-button", () =>
            {
                var type = (NpcGoalType)Enum.Parse(typeof(NpcGoalType), goal.value);
                Do(new SetNpcGoalCommand(id, type, priority.value, type == NpcGoalType.LearnSkill ? NpcSkill.Lore : (NpcSkill?)null, dm));
            })));

            var factions = _session.GetFactions(false);
            var factionNames = new List<string> { "None" };
            factionNames.AddRange(factions.Select(f => f.Name));
            var faction = new DropdownField(factionNames, 0) { name = "tool-faction" };
            tools.Add(ToolRow(faction, SmallButton("Affiliate", "tool-faction-button", () =>
                Do(new ChangeNpcAffiliationCommand(id, faction.index <= 0 ? default : factions[faction.index - 1].FactionId, dm)))));

            var others = _session.GetNpcs().Where(n => n.NpcId != id).ToList();
            if (others.Count > 0)
            {
                var other = new DropdownField(others.Select(n => n.Name).ToList(), 0) { name = "tool-relation-other" };
                var affinity = new SliderInt(-100, 100) { name = "tool-relation-affinity", value = 50, showInputField = true };
                tools.Add(ToolRow(other, affinity, SmallButton("Set affinity", "tool-relation", () =>
                    Do(new SetNpcRelationshipCommand(id, others[other.index].NpcId, affinity: affinity.value, source: dm)))));
            }

            var memory = new TextField { name = "tool-memory", value = "" };
            var salience = new SliderInt(1, 100) { name = "tool-memory-salience", value = 60, showInputField = true };
            tools.Add(Cell("A memory, and how much it matters (salience):", "rk-npc-explain"));
            tools.Add(ToolRow(memory));
            tools.Add(ToolRow(salience, SmallButton("Add memory", "tool-memory-button", () =>
                Do(new AddNpcMemoryCommand(id, MemoryCategory.Personal, memory.value, salience.value, source: dm)))));

            var settlements = _session.GetSettlements();
            var about = new DropdownField(settlements.Select(s => s.Name).ToList(), 0) { name = "tool-knowledge-settlement" };
            var value = new DropdownField(new List<string> { "plenty", "short", "starving" }, 1) { name = "tool-knowledge-value" };
            var status = new DropdownField(new List<string> { "Known", "Believed", "Rumor" }, 2) { name = "tool-knowledge-status" };
            tools.Add(Cell("Tell them how a settlement is fed (and how sure they are):", "rk-npc-explain"));
            tools.Add(ToolRow(about));
            tools.Add(ToolRow(value, status, SmallButton("Tell", "tool-knowledge", () =>
                Do(new AddNpcKnowledgeCommand(id, KnowledgeTopic.SettlementFood, settlements[about.index].SettlementId.ToString(), value.value,
                    (KnowledgeStatus)status.index, status.index == 0 ? 100 : status.index == 1 ? 70 : 45, dm)))));
            return tools;
        }

        // ----- Settlement inspector: residents -------------------------------------------------------------

        private void RefreshSettlementResidents(SettlementSummary s)
        {
            var box = Root.Q<VisualElement>("insp-residents");
            if (box == null) return;
            box.Clear();
            var residents = _session.GetNpcs(new NpcFilter { Settlement = s.SettlementId });
            if (residents.Count == 0)
            {
                box.Add(Cell("No named residents.", "rk-trade-line"));
                return;
            }

            var named = residents.Count;
            box.Add(Cell($"{named} named of {N(s.Population)} people; the other {N(Math.Max(0, s.Population - named))} are simulated together.", "rk-npc-explain"));
            foreach (var p in residents)
            {
                var id = p.NpcId;
                var link = new Button(() => SelectNpc(id)) { name = "resident-" + id, text = $"{p.Name}{(p.Tier == NpcTier.Notable ? " ★" : string.Empty)} — {p.Role}, at the {p.LocationName}" };
                link.AddToClassList("rk-button");
                link.AddToClassList("rk-resident-link");
                link.tooltip = $"Morale {p.Morale}. Click to inspect.";
                box.Add(link);
            }
        }
    }
}
