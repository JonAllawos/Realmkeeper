using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Quests;

namespace Realmkeeper.Domain.Campaigns
{
    /// <summary>
    /// Phase 17 (ADR-0082): checks a campaign definition before it may enter a world. Every reference must resolve
    /// (roles, quests, dialogues, arcs, chapters, decisions, items), prerequisites must not loop, every branch must be
    /// reachable, every decision must lead somewhere and every objective must be able to finish. The errors are
    /// written for the person authoring the content.
    /// </summary>
    public static class CampaignValidator
    {
        public static List<string> Validate(CampaignDefinition def)
        {
            var errors = new List<string>();
            if (def == null) { errors.Add("The campaign definition is missing."); return errors; }
            var where = $"Campaign '{def.Id}'";
            if (string.IsNullOrWhiteSpace(def.Id)) errors.Add("A campaign needs an ID.");
            if (string.IsNullOrWhiteSpace(def.Title)) errors.Add($"{where} has no title.");
            if (def.Version < 1) errors.Add($"{where} has version {def.Version}; versions start at 1.");

            var roles = Unique(errors, where, "role", def.Roles, r => r.Name);
            var quests = Unique(errors, where, "quest key", def.Quests, q => q.Key);
            var chapters = Unique(errors, where, "chapter", def.Chapters, c => c.Id);
            var arcs = Unique(errors, where, "arc", def.Arcs, a => a.Id);
            var decisions = Unique(errors, where, "decision", def.Decisions, d => d.Id);
            Unique(errors, where, "ending", def.Endings, e => e.Id);

            foreach (var r in def.Roles)
            {
                if (r.Of != null && !roles.Contains(r.Of)) errors.Add($"{where}: role '{r.Name}' is chosen from '{r.Of}', which is not a role.");
                foreach (var c in r.Candidates)
                    if (!roles.Contains(c)) errors.Add($"{where}: role '{r.Name}' falls back on '{c}', which is not a role.");
                if (r.Kind == CampaignRoleKind.FirstAvailable && r.Candidates.Count == 0) errors.Add($"{where}: role '{r.Name}' has no candidates to choose from.");
            }

            foreach (var q in def.Quests)
            {
                var qd = QuestCatalog.Find(q.QuestId);
                if (qd == null) { errors.Add($"{where}: quest key '{q.Key}' names quest '{q.QuestId}', which does not exist."); continue; }
                foreach (var pair in q.Roles)
                {
                    if (!roles.Contains(pair.Value)) errors.Add($"{where}: quest '{q.Key}' fills '{pair.Key}' from '{pair.Value}', which is not a role.");
                    var known = false;
                    foreach (var spec in qd.Roles) if (spec.Name == pair.Key) known = true;
                    if (!known) errors.Add($"{where}: quest '{q.Key}' fills '{pair.Key}', but quest '{q.QuestId}' has no such role.");
                }

                if (!q.Roles.ContainsKey("giver")) errors.Add($"{where}: quest '{q.Key}' does not say who gives it.");
                if (qd.DialogueId != null && DialogueCatalog.Find(qd.DialogueId) == null) errors.Add($"{where}: quest '{q.QuestId}' uses dialogue '{qd.DialogueId}', which does not exist.");
                if (qd.DialogueId != null) CheckDialogue(errors, where, DialogueCatalog.Find(qd.DialogueId), decisions, def);
                foreach (var reward in qd.Rewards)
                    if (reward.Kind == RewardKind.Item && Realmkeeper.Domain.World.ItemCatalog.Find(reward.Key) == null) errors.Add($"{where}: quest '{q.QuestId}' rewards '{reward.Key}', which is not an item.");
            }

            foreach (var c in def.Chapters)
                foreach (var after in c.After)
                    if (!chapters.Contains(after)) errors.Add($"{where}: chapter '{c.Id}' follows '{after}', which is not a chapter.");
            if (Loops(def.Chapters, c => c.Id, c => c.After) is string chapterLoop) errors.Add($"{where}: chapters wait on each other in a circle ({chapterLoop}).");

            foreach (var a in def.Arcs)
            {
                if (!chapters.Contains(a.ChapterId ?? string.Empty)) errors.Add($"{where}: arc '{a.Id}' belongs to chapter '{a.ChapterId}', which does not exist.");
                foreach (var after in a.After)
                    if (!arcs.Contains(after)) errors.Add($"{where}: arc '{a.Id}' follows '{after}', which is not an arc.");
                foreach (var q in a.StartQuests)
                    if (!quests.Contains(q)) errors.Add($"{where}: arc '{a.Id}' starts quest '{q}', which is not one of the campaign's quests.");
                foreach (var f in a.Fallbacks)
                {
                    if (!quests.Contains(f.Quest ?? string.Empty)) errors.Add($"{where}: arc '{a.Id}' falls back on quest '{f.Quest}', which is not one of the campaign's quests.");
                    if (f.When.Count == 0) errors.Add($"{where}: arc '{a.Id}' has a fallback with no conditions; it would always open.");
                    CheckConditions(errors, where + $", arc '{a.Id}' fallback", f.When, def);
                }
                if (a.Objectives.Count == 0) errors.Add($"{where}: arc '{a.Id}' has no objectives, so it can never finish.");
                var required = false;
                foreach (var o in a.Objectives)
                {
                    if (!o.Optional) required = true;
                    switch (o.Kind)
                    {
                        case ArcObjectiveKind.Quest:
                            if (!quests.Contains(o.QuestKey ?? string.Empty)) errors.Add($"{where}: objective '{a.Id}/{o.Id}' waits on quest '{o.QuestKey}', which is not one of the campaign's quests.");
                            else if (!Starts(def, o.QuestKey)) errors.Add($"{where}: objective '{a.Id}/{o.Id}' waits on quest '{o.QuestKey}', which nothing starts.");
                            break;
                        case ArcObjectiveKind.Decision:
                            var d = def.Decision(o.DecisionId);
                            if (d == null) errors.Add($"{where}: objective '{a.Id}/{o.Id}' waits on decision '{o.DecisionId}', which does not exist.");
                            else if (o.ChoiceId != null && d.Choice(o.ChoiceId) == null) errors.Add($"{where}: objective '{a.Id}/{o.Id}' waits on choice '{o.ChoiceId}', which decision '{d.Id}' does not offer.");
                            break;
                        case ArcObjectiveKind.State:
                            if (o.Conditions.Count == 0) errors.Add($"{where}: objective '{a.Id}/{o.Id}' waits on a state but names no condition.");
                            break;
                    }

                    CheckConditions(errors, where + $", objective '{a.Id}/{o.Id}'", o.Conditions, def);
                    CheckConditions(errors, where + $", objective '{a.Id}/{o.Id}'", o.UnavailableWhen, def);
                }

                if (!required) errors.Add($"{where}: arc '{a.Id}' has only optional objectives, so it can never finish.");
                CheckConditions(errors, where + $", arc '{a.Id}'", a.Conditions, def);
                CheckConditions(errors, where + $", arc '{a.Id}'", a.FailWhen, def);
                CheckEffects(errors, where + $", arc '{a.Id}'", a.OnComplete, def);
                CheckEffects(errors, where + $", arc '{a.Id}'", a.OnFail, def);
            }

            if (Loops(def.Arcs, a => a.Id, a => a.After) is string arcLoop) errors.Add($"{where}: arcs wait on each other in a circle ({arcLoop}).");

            // Reachability: an arc in a chapter that follows a later chapter, or that waits on an arc of a later chapter, can never begin.
            var order = ChapterOrder(def);
            foreach (var a in def.Arcs)
                foreach (var after in a.After)
                    if (def.Arc(after) is ArcDefinition prior && order.TryGetValue(prior.ChapterId ?? string.Empty, out var pi) && order.TryGetValue(a.ChapterId ?? string.Empty, out var ai) && pi > ai)
                        errors.Add($"{where}: arc '{a.Id}' (chapter '{a.ChapterId}') waits on '{after}' of a later chapter, so it can never begin.");

            foreach (var d in def.Decisions)
            {
                if (!arcs.Contains(d.ArcId ?? string.Empty)) errors.Add($"{where}: decision '{d.Id}' belongs to arc '{d.ArcId}', which does not exist.");
                if (d.SpeakerRole != null && !roles.Contains(d.SpeakerRole)) errors.Add($"{where}: decision '{d.Id}' is put by '{d.SpeakerRole}', which is not a role.");
                if (d.Choices.Count == 0) errors.Add($"{where}: decision '{d.Id}' has no choices.");
                Unique(errors, where + $", decision '{d.Id}'", "choice", d.Choices, c => c.Id);
                var anyOpen = false;
                foreach (var c in d.Choices)
                {
                    if (c.Conditions.Count == 0) anyOpen = true;
                    if (c.Effects.Count == 0 && c.Delayed.Count == 0 && c.Outcome == null) errors.Add($"{where}: choice '{d.Id}/{c.Id}' changes nothing; every choice must have a consequence.");
                    if (string.IsNullOrWhiteSpace(c.Summary)) errors.Add($"{where}: choice '{d.Id}/{c.Id}' has no summary for the journal.");
                    CheckConditions(errors, where + $", choice '{d.Id}/{c.Id}'", c.Conditions, def);
                    CheckEffects(errors, where + $", choice '{d.Id}/{c.Id}'", c.Effects, def);
                    var delayedIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var x in c.Delayed)
                    {
                        if (string.IsNullOrEmpty(x.Id) || !delayedIds.Add(x.Id)) errors.Add($"{where}: choice '{d.Id}/{c.Id}' has a delayed consequence without a unique ID.");
                        if (x.AfterDays < 0) errors.Add($"{where}: delayed consequence '{d.Id}/{c.Id}/{x.Id}' comes {x.AfterDays} days before it is chosen.");
                        if (x.Effects.Count == 0) errors.Add($"{where}: delayed consequence '{d.Id}/{c.Id}/{x.Id}' changes nothing.");
                        CheckConditions(errors, where + $", delayed '{d.Id}/{c.Id}/{x.Id}'", x.When, def);
                        CheckEffects(errors, where + $", delayed '{d.Id}/{c.Id}/{x.Id}'", x.Effects, def);
                    }

                    foreach (var l in c.Leanings)
                        if (!Enum.TryParse<Realmkeeper.Domain.World.NpcTrait>(l.Trait, out _)) errors.Add($"{where}: choice '{d.Id}/{c.Id}' leans on '{l.Trait}', which is not a personality trait.");
                }

                if (!anyOpen && d.Choices.Count > 0) errors.Add($"{where}: every choice of decision '{d.Id}' has conditions; one must always be open, or the decision can stall.");
                CheckConditions(errors, where + $", decision '{d.Id}'", d.Conditions, def);
            }

            if (def.FinalArc == null || !arcs.Contains(def.FinalArc)) errors.Add($"{where}: the final arc '{def.FinalArc}' does not exist, so the campaign can never end.");
            if (def.Endings.Count == 0) errors.Add($"{where} has no endings.");
            else
            {
                var fallback = false;
                foreach (var e in def.Endings)
                {
                    if (e.Conditions.Count == 0) fallback = true;
                    CheckConditions(errors, where + $", ending '{e.Id}'", e.Conditions, def);
                }

                if (!fallback) errors.Add($"{where}: no ending is unconditional, so the campaign could finish with no ending.");
            }

            CheckConditions(errors, where + ", start", def.StartConditions, def);
            return errors;
        }

        private static bool Starts(CampaignDefinition def, string questKey)
        {
            foreach (var a in def.Arcs)
            {
                foreach (var q in a.StartQuests) if (q == questKey) return true;
                foreach (var f in a.Fallbacks) if (f.Quest == questKey) return true;
                foreach (var e in a.OnComplete) if (e.Kind == CampaignEffectKind.StartQuest && e.Key == questKey) return true;
                foreach (var e in a.OnFail) if (e.Kind == CampaignEffectKind.StartQuest && e.Key == questKey) return true;
            }

            foreach (var d in def.Decisions)
                foreach (var c in d.Choices)
                {
                    foreach (var e in c.Effects) if (e.Kind == CampaignEffectKind.StartQuest && e.Key == questKey) return true;
                    foreach (var x in c.Delayed) foreach (var e in x.Effects) if (e.Kind == CampaignEffectKind.StartQuest && e.Key == questKey) return true;
                }

            return false;
        }

        private static Dictionary<string, int> ChapterOrder(CampaignDefinition def)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var pass = 0; pass <= def.Chapters.Count; pass++)
                foreach (var c in def.Chapters)
                {
                    var depth = 0;
                    var ready = true;
                    foreach (var after in c.After)
                    {
                        if (!order.TryGetValue(after, out var d)) { ready = false; break; }
                        depth = Math.Max(depth, d + 1);
                    }

                    if (ready) order[c.Id] = depth;
                }

            return order;
        }

        private static void CheckDialogue(List<string> errors, string where, DialogueDefinition dlg, HashSet<string> decisions, CampaignDefinition def)
        {
            if (dlg == null) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in dlg.Nodes) if (!ids.Add(n.Id)) errors.Add($"{where}: dialogue '{dlg.Id}' has two nodes called '{n.Id}'.");
            // A quest's dialogue is entered at the node of the role spoken to (the giver's first); any other at its start node.
            var entry = dlg.QuestId != null ? "giver" : dlg.StartNode;
            if (dlg.Find(entry) == null) errors.Add($"{where}: dialogue '{dlg.Id}' has no '{entry}' node to begin at.");
            foreach (var n in dlg.Nodes)
                foreach (var r in n.Responses)
                {
                    foreach (var next in new[] { r.Next, r.NextOnFailure })
                        if (next != null && next != "end" && !next.StartsWith("@", StringComparison.Ordinal) && dlg.Find(next) == null)
                            errors.Add($"{where}: dialogue '{dlg.Id}', response '{n.Id}/{r.Id}', leads to '{next}', which is not a node.");
                    foreach (var x in r.Consequences)
                        if (x.Kind == DialogueConsequenceKind.CampaignDecision)
                        {
                            var d = def.Decision(x.Key);
                            if (d == null) errors.Add($"{where}: dialogue '{dlg.Id}' records decision '{x.Key}', which does not exist.");
                            else if (d.Choice(x.Value) == null) errors.Add($"{where}: dialogue '{dlg.Id}' records choice '{x.Value}', which decision '{x.Key}' does not offer.");
                        }

                    CheckConditions(errors, where + $", dialogue '{dlg.Id}'", r.Conditions, def);
                }
        }

        private static void CheckConditions(List<string> errors, string where, IReadOnlyList<QuestCondition> conditions, CampaignDefinition def)
        {
            foreach (var c in conditions)
            {
                switch (c.Kind)
                {
                    case ConditionKind.CampaignArc:
                        if (def.Arc(c.Key) == null) errors.Add($"{where}: a condition reads arc '{c.Key}', which does not exist.");
                        break;
                    case ConditionKind.CampaignDecision:
                        var d = def.Decision(c.Key);
                        if (d == null) errors.Add($"{where}: a condition reads decision '{c.Key}', which does not exist.");
                        else if (c.Value != null) foreach (var v in c.Value.Split('|')) if (d.Choice(v) == null) errors.Add($"{where}: a condition reads choice '{v}', which decision '{c.Key}' does not offer.");
                        break;
                    case ConditionKind.CharacterStatus:
                    case ConditionKind.CompanionLoyalty:
                    case ConditionKind.SettlementStability:
                    case ConditionKind.RouteOpen:
                        if (c.Key != null && c.Key != "npc" && c.Key != "here" && def.Role(c.Key) == null) errors.Add($"{where}: a condition reads role '{c.Key}', which is not a role.");
                        if (c.Kind == ConditionKind.CharacterStatus && c.Value != null)
                            foreach (var v in c.Value.Split('|')) if (!Enum.TryParse<CharacterStatus>(v, out _)) errors.Add($"{where}: '{v}' is not a character status.");
                        break;
                    case ConditionKind.WorldEventOpen:
                        if (!Enum.TryParse<Realmkeeper.Domain.World.WorldEventType>(c.Key ?? string.Empty, out _)) errors.Add($"{where}: '{c.Key}' is not a kind of world event.");
                        if (c.Value != null && def.Role(c.Value) == null) errors.Add($"{where}: a world-event condition is about '{c.Value}', which is not a role.");
                        break;
                }
            }
        }

        private static void CheckEffects(List<string> errors, string where, IReadOnlyList<CampaignEffect> effects, CampaignDefinition def)
        {
            foreach (var e in effects)
            {
                if (e.Role != null && e.Role != "party" && def.Role(e.Role) == null) errors.Add($"{where}: an effect is about '{e.Role}', which is not a role.");
                switch (e.Kind)
                {
                    case CampaignEffectKind.StartQuest:
                        if (def.Quest(e.Key) == null) errors.Add($"{where}: an effect starts quest '{e.Key}', which is not one of the campaign's quests.");
                        break;
                    case CampaignEffectKind.ArcState:
                        if (def.Arc(e.Key) == null) errors.Add($"{where}: an effect moves arc '{e.Key}', which does not exist.");
                        if (!Enum.TryParse<ArcState>(e.Value ?? string.Empty, out _)) errors.Add($"{where}: '{e.Value}' is not an arc state.");
                        break;
                    case CampaignEffectKind.RaiseWorldEvent:
                        if (!Enum.TryParse<Realmkeeper.Domain.World.WorldEventType>(e.Key ?? string.Empty, out _)) errors.Add($"{where}: '{e.Key}' is not a kind of world event.");
                        break;
                    case CampaignEffectKind.RouteCondition:
                        if (!Enum.TryParse<Realmkeeper.Domain.World.RouteCondition>(e.Value ?? string.Empty, out _)) errors.Add($"{where}: '{e.Value}' is not a road condition.");
                        break;
                    case CampaignEffectKind.FactionOpinion:
                    case CampaignEffectKind.NpcRelation:
                        if (e.Key == null || def.Role(e.Key) == null) errors.Add($"{where}: an effect pairs '{e.Role}' with '{e.Key}', which is not a role.");
                        break;
                    case CampaignEffectKind.SettlementResource:
                        if (string.IsNullOrEmpty(e.Key)) errors.Add($"{where}: a resource effect names no resource.");
                        break;
                }
            }
        }

        private static HashSet<string> Unique<T>(List<string> errors, string where, string what, IReadOnlyList<T> items, Func<T, string> id)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var key = id(item);
                if (string.IsNullOrEmpty(key)) { errors.Add($"{where} has a {what} without an ID."); continue; }
                if (!set.Add(key)) errors.Add($"{where} has two of {what} '{key}'.");
            }

            return set;
        }

        /// <summary>A circle of prerequisites, as "a → b → a", or null.</summary>
        private static string Loops<T>(IReadOnlyList<T> items, Func<T, string> id, Func<T, IReadOnlyList<string>> after)
        {
            var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var i in items) if (id(i) != null) map[id(i)] = after(i);
            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            var path = new List<string>();
            string Visit(string node)
            {
                if (!map.ContainsKey(node)) return null;
                if (state.TryGetValue(node, out var s))
                {
                    if (s == 2) return null;
                    var start = path.IndexOf(node);
                    return string.Join(" → ", path.GetRange(start, path.Count - start)) + " → " + node;
                }

                state[node] = 1;
                path.Add(node);
                foreach (var next in map[node])
                {
                    var loop = Visit(next);
                    if (loop != null) return loop;
                }

                path.RemoveAt(path.Count - 1);
                state[node] = 2;
                return null;
            }

            foreach (var key in map.Keys)
            {
                var loop = Visit(key);
                if (loop != null) return loop;
            }

            return null;
        }
    }
}
