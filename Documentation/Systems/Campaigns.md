# Campaigns, story arcs, decisions and the world's own stories

Phase 17. Decisions: ADR-0082 (campaigns), ADR-0083 (decisions and consequences), ADR-0084 (narrative resilience), ADR-0085 (emergent stories), ADR-0087 (companions in the story).

A campaign is authored narrative laid over the one persistent world. It is **not** a separate mode, a second quest engine, or a copy of anyone. Its definition is data. Its instance, saved in the world, points at the world's own people, settlements, factions, roads and quest instances. The world never waits for it.

## Where the code is

| Layer | Files |
|---|---|
| Domain | `Runtime/Domain/Campaigns/CampaignModel.cs` (definitions, instances, states, `CharacterStatus`), `CampaignValidator.cs`, `CampaignCatalog.cs`, `CampaignCatalog.Waystone.cs` (the demonstration). Condition kinds 22–37 in `Domain/Quests/QuestModel.cs`; dialogue consequence kinds 32–36 and response requirements in `DialogueModel.cs`; the campaign's quests and dialogue in `QuestCatalog.Phase17.cs`. |
| Simulation | `Runtime/Simulation/Campaigns/CampaignEngine.cs` (start, evaluate, decide, effects, roles), `CampaignWorld.cs` (binder, `CampaignCharacters`, `CampaignConditions`), `StorySynthesis.cs` (stories, and the daily `CampaignSystem`, order 482); `Simulation/Party/CompanionStories.cs`. |
| Application | `Runtime/Application/Campaigns/CampaignCommands.cs` (start, set aside, take up, give up, refuse an arc, decide from the journal), `CampaignQueries.cs` (the journal's view); `WorldSession.GetCampaigns`. |
| Persistence | `WorldSaveMapper.Campaigns.cs`; `MigrationV18ToV19` (schema v19). |
| Presentation | The journal's **Campaign** tab (`PlayerHubWindow.Campaign.cs`); disabled choices and decision marks in `DialogueWindow`; companions' stories, remarks and relationships in the Party section. |

## A definition

| Part | What it is |
|---|---|
| Roles | How each part is cast from the world: the home town, a road out of the home region, the far end of a road, a person living in a settlement (preferred trades first, then anyone; never a party member), a settlement's faction, someone's household, a region. Two kinds are read when asked: `FactionLeader` (whoever leads now) and `FirstAvailable` (the first of several roles whose person can still be met). A role may be optional. |
| Quests | Existing quest definitions with their roles filled from campaign roles. They are offered through the ordinary quest lifecycle (source key `campaign:{campaign}:{key}`) and are never pruned. |
| Chapters | They open when the chapters they follow are closed, and close when their required arcs are closed (completed, failed or refused). Optional arcs never hold a chapter. |
| Arcs | They begin when their chapter is open, the arcs they follow are closed, and their conditions hold. An arc's objectives are linked quests, decisions or world-state conditions, in stages and alternative groups as quests have. An arc can also have: a time limit, failure conditions, fallbacks (another quest offered when, say, a person is gone), effects when it completes or fails, and whether it may be refused. |
| Decisions | Choices with typed effects, delayed consequences and personality leanings (below). |
| Endings | The first ending whose conditions hold when the final arc closes; one is unconditional. |

`CampaignValidator` rejects a definition with:
- missing references or duplicates;
- prerequisite loops, or arcs waiting on a later chapter;
- arcs that can never finish, or quests nothing starts;
- decisions without choices, choices that change nothing, or decisions with no unconditional choice;
- broken dialogue;
- no fallback ending.

The errors are written for the author, and a rejected definition can never be started.

## Lifecycle

1. **Begin.** From the journal's Campaign tab ("Begin"), or `StartCampaignCommand`. If it cannot begin here, the reason is given: an older world with no road out of its home region, nobody to play a part, or a start condition. The roles are cast from the world as it is now; history is not rewritten.
2. **Play.** The campaign is re-read when one of its quests changes state, when a decision is made, and once a day (time limits, people gone, world-state objectives, fallbacks, delayed consequences). It is never re-read per frame. Re-reading one campaign costs about 0.015 ms.
3. **Set aside.** Nothing advances, nothing is lost, and the world goes on. Decisions cannot be made while it is set aside.
4. **Take up again**, or **give up**: open arcs are then abandoned, and what was done stays done.
5. **End.** Completed or Failed, by its ending. A closed campaign can never reopen and cannot be begun again in the same world. Its delayed consequences still come in their time: they are the world's.

## Decisions and consequences

- **Made once.**
  - A decision a person puts is made in conversation, through a dialogue `CampaignDecision` consequence, inside the choose command.
  - A decision with no speaker is made from the journal.
  - The choice is recorded with when and who put it, never undone, and added to the player's decision log.
- **Effects.** Each effect is typed and applied once by key, through the authoritative path of the system it touches:
  - regard, faction standing, influence, faction opinion;
  - settlement stability, prosperity, resources;
  - NPC memory, relation, morale; companion loyalty;
  - start a campaign quest, discovery, spread (a settlement's people learn it), player deed;
  - raise a world event, road condition;
  - flag, arc state, experience, coin, Chronicle note.
- **Delayed consequences** are saved with their due time and optional conditions, and fire once. In the demonstration, six days after the player backs one town, the other town's carters stop paying and trade on the crossing is disrupted (a real world event). A shared count mends the road eight days later, but only once the road is open.
- **Disabled choices.** A choice whose conditions do not hold can be shown disabled with the story's reason ("You'd need proof the count is being cheated…"). Choosing it is refused.

## People in the story

A person's status is read from the systems that decide it: dead, captured, travelling with the player, left the player, hostile (hostility 70+), missing, away, hurt, or available. Nothing protects a story person, and nothing brings one back. The story adapts:
- another person speaks (`FirstAvailable`, `FactionLeader`);
- an alternative objective closes the arc another way ("the letter went unanswered");
- a fallback quest asks someone else (the toll-keeper's household);
- the arc is lost when nobody is left who knows.

## Companions in the story

ADR-0087 has the full rules. In short:
- **Reactions.** Companions travelling with the player react to decisions by their own traits. Loyalty moves once per decision, with the reason, and they remember it.
- **Remarks.** They say what they think when asked ("Something on your mind?"). Remarks come from decisions, falling out or making up, arriving where their people live, and trouble at home.
- **Relationships.** Two companions on opposite sides of a decision grow apart (Phase 4 relation); on the same side, closer. A quarrel can be talked round (Persuasion).
- **A second story.** "Unfinished Business" is asked at loyalty 70, bound to a real person elsewhere, with three endings. "Old Ground" is unchanged.
- **Reserves** take no part.

## The world's own stories

Once a day, at most one new story is offered, with at most three open. A story is offered only where something actually happened and real people are bound up in it:

| Story | Cause | People | Result |
|---|---|---|---|
| *What Became of the Caravan* | A caravan lost on a road (a world event) | Someone of the town it left; a witness at an end of the road who holds the event as knowledge | The account is the event's own telling and the leader's real fate. Done: the town is steadier (+2), and the giver remembers. |
| *Grain Down the Road* | A town short of food, with a full town (30+ days) on an open road, preferably in another region | The hungry town's elder; a merchant of the full town (persuaded, or paid 40 coin) | 60 measures of food really move between the towns' stores. It lapses if the shortage ends first. |

Each cause gives one story, ever; the player's ledger remembers it even after the quest is pruned. The giver carries it as news, so it spreads by the people's own talk.

## The demonstration: *The Waystone Count* (provisional)

**Provisional and non-canonical.** Two towns on either side of a border crossing in the generated world share the tolls of the road on a count kept at a toll-house. The count has come up short. It establishes no kingdom, antagonist, world history or destiny for the player.

| Chapter | Arcs |
|---|---|
| **The Short Count** | *A Letter Across the Line*: the reeve's letter to the warden (or whoever speaks for the far town); cross the border, deliver. Alternative: the warden is gone, the letter goes unanswered. *Counting Carts* (optional): a carter's suspicion; look at the tally sticks (Investigation or Insight), find proof or not. |
| **Two Accounts** | *Who Holds the Road* (20 days, or the towns settle it themselves): the decision `road_claim`, with four choices — the home town, the far town, a shared count (needs proof, a winning argument, or friendship with the far town's faction), or refusing to take a side. *The Toll-Keeper*: the decision `tollkeeper_fate` — expose (they turn hostile), let them repay (word comes back later), or take a share (word gets out later). Fallbacks: their household, or the truth lost. |
| **Settling Up** | *Settling Up*: tell the home town's voice how it ended (their words depend on the decisions). |

There are five endings: *A Count Kept Twice*, *the home town's road*, *the far town's road*, *Their Own Quarrel*, and *Settled Without You*.

## Limits

- One authored campaign, kept small to prove the engine. Its content is provisional.
- Decisions with no speaker (made from the journal) are supported but unused in the demonstration.
- Two kinds of world story. A merchant's death, a faction losing influence and similar causes are not yet stories.

## Phase 18 additions (ADR-0091, ADR-0093)

- ***The Waystone Count*** is deepened but stays provisional: no new arc, chapter or canon.
  - The Cairn Watch log or the ringers' verse can make the case for a shared count.
  - "Witness" settles the toll-keeper's count before both towns, with its own delayed consequence and companion leanings.
- **The world's own stories** now come in seven kinds: those in `WorldEvents.md` and `Quests.md`. The lifecycle rules there replace the Phase 17 limit of three open stories.
