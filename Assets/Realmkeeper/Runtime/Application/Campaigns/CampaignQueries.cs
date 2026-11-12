using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Campaigns;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Campaigns;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Campaigns
{
    public sealed class CampaignOfferView
    {
        public string CampaignId { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public bool Provisional { get; set; }
        public bool CanStart { get; set; }

        /// <summary>Why it cannot start here (null when it can).</summary>
        public string Why { get; set; }
    }

    public sealed class CampaignObjectiveView
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public bool Done { get; set; }
        public bool Failed { get; set; }
        public bool Optional { get; set; }

        /// <summary>The linked quest's name and state, when it has one the player knows of.</summary>
        public string Quest { get; set; }
        public string QuestInstanceId { get; set; }
    }

    public sealed class CampaignArcView
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public ArcState State { get; set; }
        public bool Optional { get; set; }
        public bool MayRefuse { get; set; }
        public string Reason { get; set; }

        /// <summary>Days left before the world stops waiting (−1: no limit).</summary>
        public int DaysLeft { get; set; } = -1;
        public List<CampaignObjectiveView> Objectives { get; } = new List<CampaignObjectiveView>();
    }

    public sealed class CampaignChapterView
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public ChapterState State { get; set; }
        public List<CampaignArcView> Arcs { get; } = new List<CampaignArcView>();
    }

    public sealed class CampaignChoiceView
    {
        public string ChoiceId { get; set; }
        public string Text { get; set; }
        public bool Enabled { get; set; }
        public string Why { get; set; }
    }

    /// <summary>A decision made: what the player did, and what has come of it so far (only what has happened, or what was said would).</summary>
    public sealed class CampaignDecisionView
    {
        public string DecisionId { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string When { get; set; }
        public string Source { get; set; }
        public List<string> Happened { get; } = new List<string>();
        public List<string> Expected { get; } = new List<string>();
    }

    /// <summary>A decision waiting for the player, from the journal (one a person will put is shown with who will ask).</summary>
    public sealed class CampaignPendingDecisionView
    {
        public string DecisionId { get; set; }
        public string Title { get; set; }
        public string Prompt { get; set; }

        /// <summary>Who will put it (a name), or null when the player decides from the journal.</summary>
        public string AskedBy { get; set; }
        public List<CampaignChoiceView> Choices { get; } = new List<CampaignChoiceView>();
    }

    /// <summary>A person in the story, as far as the player knows them.</summary>
    public sealed class CampaignCharacterView
    {
        public string Role { get; set; }
        public string Name { get; set; }
        public string Part { get; set; }

        /// <summary>"met", "heard of" or "unknown" — what the player knows of them.</summary>
        public string Knowledge { get; set; }

        /// <summary>What has become of them, when the player could know it (else null).</summary>
        public string Status { get; set; }
        public CharacterStatus? StatusValue { get; set; }
    }

    public sealed class CampaignView
    {
        public string InstanceId { get; set; }
        public string CampaignId { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Premise { get; set; }
        public bool Provisional { get; set; }
        public CampaignState State { get; set; }
        public string Started { get; set; }
        public string CurrentChapter { get; set; }
        public string Ending { get; set; }
        public string EndingText { get; set; }

        /// <summary>True when the campaign's definition is missing from this build (it is then kept, untouched).</summary>
        public bool DefinitionMissing { get; set; }
        public List<CampaignChapterView> Chapters { get; } = new List<CampaignChapterView>();
        public List<CampaignDecisionView> Decisions { get; } = new List<CampaignDecisionView>();
        public List<CampaignPendingDecisionView> Pending { get; } = new List<CampaignPendingDecisionView>();
        public List<CampaignCharacterView> Characters { get; } = new List<CampaignCharacterView>();
        public List<string> History { get; } = new List<string>();
    }

    public sealed class CampaignsView
    {
        public List<CampaignOfferView> Offers { get; } = new List<CampaignOfferView>();
        public List<CampaignView> Campaigns { get; } = new List<CampaignView>();

        /// <summary>No campaign has ever been started here: an open-ended (sandbox) world.</summary>
        public bool Sandbox { get; set; }
    }

    /// <summary>
    /// Phase 17: the campaign journal, as the player may see it. Hidden arcs and objectives stay hidden until they open;
    /// endings are not shown before they are reached; a consequence is shown once it has happened, or as the hint the
    /// story gave when the choice was made; a person's fate only once the player has met them.
    /// </summary>
    public static class CampaignQueries
    {
        public static CampaignsView View(WorldData w)
        {
            var v = new CampaignsView();
            if (w == null) return v;
            v.Sandbox = w.Campaigns.Count == 0;
            var defs = new List<CampaignDefinition>(CampaignCatalog.All);
            defs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var def in defs)
            {
                if (CampaignEngine.Find(w, CampaignEngine.InstanceIdFor(def.Id)) != null) continue;
                var why = CampaignEngine.Compatibility(w, def);
                v.Offers.Add(new CampaignOfferView { CampaignId = def.Id, Title = def.Title, Summary = def.Summary, Provisional = def.Provisional, CanStart = why == null, Why = why });
            }

            foreach (var c in w.Campaigns) v.Campaigns.Add(One(w, c));
            return v;
        }

        public static CampaignView One(WorldData w, CampaignInstanceData c)
        {
            var def = CampaignEngine.Definition(c);
            var v = new CampaignView
            {
                InstanceId = c.InstanceId, CampaignId = c.CampaignId, State = c.State, Started = TravelRules.Clock(c.StartedAt), DefinitionMissing = def == null,
                Title = def?.Title ?? c.CampaignId,
            };
            for (var i = c.History.Count - 1; i >= 0; i--) v.History.Add(TravelRules.Clock(c.History[i].At) + " — " + c.History[i].Text);
            if (def == null) return v;
            v.Summary = def.Summary;
            v.Premise = CampaignEngine.Text(w, c, def.Premise);
            v.Provisional = def.Provisional;
            var now = QuestClock.Now(w);

            foreach (var chd in def.Chapters)
            {
                var ch = c.Chapter(chd.Id);
                if (ch == null || ch.State == ChapterState.Locked) continue;
                var cv = new CampaignChapterView { Id = chd.Id, Title = chd.Title, Summary = CampaignEngine.Text(w, c, chd.Summary), State = ch.State };
                if (ch.State == ChapterState.Active) v.CurrentChapter = chd.Title;
                foreach (var ad in def.Arcs)
                {
                    if (ad.ChapterId != chd.Id) continue;
                    var a = c.Arc(ad.Id);
                    if (a == null || (a.State == ArcState.Locked && ad.Hidden)) continue;
                    var av = new CampaignArcView
                    {
                        Id = ad.Id, Title = ad.Title, Summary = CampaignEngine.Text(w, c, ad.Summary), State = a.State, Optional = ad.Optional, MayRefuse = ad.Optional || ad.MayRefuse,
                        Reason = a.Reason,
                    };
                    if (a.State == ArcState.Active && ad.TimeLimitDays > 0 && a.StartedAt >= 0)
                        av.DaysLeft = (int)Math.Max(0, (a.StartedAt + ad.TimeLimitDays * (long)Domain.Calendar.WorldTime.MinutesPerDay - now) / Domain.Calendar.WorldTime.MinutesPerDay);
                    foreach (var o in ad.Objectives)
                    {
                        var p = a.Objective(o.Id);
                        if (p == null || !p.Revealed || a.State == ArcState.Locked) continue;
                        var ov = new CampaignObjectiveView { Id = o.Id, Text = CampaignEngine.Text(w, c, o.Text), Done = p.Done, Failed = p.Failed, Optional = o.Optional };
                        if (o.Kind == ArcObjectiveKind.Quest && c.Quests.TryGetValue(o.QuestKey ?? string.Empty, out var qid) && w.FindQuest(qid) is QuestInstanceData q && q.State != QuestState.Unavailable && q.State != QuestState.Available)
                        {
                            ov.Quest = QuestText.Fill(w, q, QuestCatalog.Find(q.DefinitionId)?.Name) + " (" + q.State.ToString().ToLowerInvariant() + ")";
                            ov.QuestInstanceId = q.InstanceId;
                        }

                        av.Objectives.Add(ov);
                    }

                    cv.Arcs.Add(av);
                }

                v.Chapters.Add(cv);
            }

            foreach (var d in c.Decisions)
            {
                var dd = def.Decision(d.DecisionId);
                var choice = dd?.Choice(d.ChoiceId);
                var dv = new CampaignDecisionView
                {
                    DecisionId = d.DecisionId, Title = dd?.Title ?? d.DecisionId, Summary = CampaignEngine.Text(w, c, choice?.Summary ?? d.ChoiceId), When = TravelRules.Clock(d.At), Source = d.Source,
                };
                foreach (var p in c.Pending)
                {
                    if (p.DecisionId != d.DecisionId) continue;
                    var x = CampaignEngine.Delayed(def, p);
                    if (x == null) continue;
                    if (p.Fired) dv.Happened.Add(CampaignEngine.Text(w, c, x.Text));
                    else if (!p.Lapsed && x.Hint != null) dv.Expected.Add(CampaignEngine.Text(w, c, x.Hint));
                }

                v.Decisions.Add(dv);
            }

            if (c.State == CampaignState.Active)
                foreach (var dd in def.Decisions)
                {
                    if (c.Decision(dd.Id) != null || c.Arc(dd.ArcId)?.State != ArcState.Active || !CampaignConditions.All(w, c, null, null, dd.Conditions)) continue;
                    var pv = new CampaignPendingDecisionView
                    {
                        DecisionId = dd.Id, Title = dd.Title, Prompt = CampaignEngine.Text(w, c, dd.Prompt),
                        AskedBy = dd.SpeakerRole == null ? null : CampaignEngine.Npc(w, c, dd.SpeakerRole)?.Name ?? "someone",
                    };
                    foreach (var ch in dd.Choices)
                    {
                        var ok = CampaignConditions.All(w, c, null, null, ch.Conditions);
                        if (!ok && ch.UnavailableText == null) continue;
                        pv.Choices.Add(new CampaignChoiceView { ChoiceId = ch.Id, Text = CampaignEngine.Text(w, c, ch.Text), Enabled = ok && dd.SpeakerRole == null, Why = ok ? null : CampaignEngine.Text(w, c, ch.UnavailableText) });
                    }

                    v.Pending.Add(pv);
                }

            if (c.Ending != null && def.Endings is IReadOnlyList<EndingDefinition> endings)
                foreach (var e in endings)
                    if (e.Id == c.Ending) { v.Ending = CampaignEngine.Text(w, c, e.Title); v.EndingText = CampaignEngine.Text(w, c, e.Text); }

            foreach (var spec in def.Roles)
            {
                if (spec.Kind != CampaignRoleKind.Npc && spec.Kind != CampaignRoleKind.FactionLeader && spec.Kind != CampaignRoleKind.FirstAvailable) continue;
                if (spec.Kind == CampaignRoleKind.FirstAvailable) continue; // the people behind it are listed for themselves
                var npc = CampaignEngine.Npc(w, c, spec.Name);
                if (npc == null) continue;
                var met = npc.HasMetPlayer || w.Party.Contains(npc.NpcId);
                var heard = met || Mentioned(w, c, npc) || (def.Premise ?? string.Empty).Contains("{" + spec.Name + "}"); // the premise names them
                if (!heard) continue; // the player has not heard of them yet: the journal does not name them
                var status = CampaignCharacters.StatusOf(w, npc);
                v.Characters.Add(new CampaignCharacterView
                {
                    Role = spec.Name, Name = npc.Name, Part = CampaignEngine.Text(w, c, spec.Part ?? spec.Name), Knowledge = met ? "met" : "heard of",
                    Status = met ? CampaignCharacters.Words(status) : null, StatusValue = met ? status : (CharacterStatus?)null,
                });
            }

            return v;
        }

        /// <summary>The player has heard of a person when a quest they know of names them.</summary>
        private static bool Mentioned(WorldData w, CampaignInstanceData c, NpcData npc)
        {
            foreach (var id in c.Quests.Values)
            {
                if (string.IsNullOrEmpty(id) || !(w.FindQuest(id) is QuestInstanceData q)) continue;
                if (q.State == QuestState.Available || q.State == QuestState.Unavailable) continue;
                foreach (var b in q.Bindings.Values) if (b == npc.NpcId.ToString()) return true;
            }

            return false;
        }
    }
}
