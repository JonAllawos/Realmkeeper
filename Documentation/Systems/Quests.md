# Quests (Phase 12)

Decisions: ADR-0056 (templates bound to the world), ADR-0057 (objectives and quest events), ADR-0060 (emergent activities and discovery).

## Model

| Part | Where | What |
|---|---|---|
| Definition | `Domain/Quests/QuestModel.cs`, `QuestCatalog.cs` | Template: roles (`RoleSpec`), text with `{role}` tokens, prerequisites, availability, objectives, failures, rewards, consequences, follow-ups, expiry, dialogue, content tags, source, version |
| Instance | `QuestInstanceData` (saved) | Bindings, state, objective progress, decisions, variables, outcome, rewards claimed, consequences applied, failure reason, history, tracked |
| Binder | `Simulation/Quests/QuestWorld.cs` `QuestBinder` | Fills roles from the world, deterministically |
| Engine | `QuestObjectives`, `QuestLifecycle`, `QuestEffects` (simulation); `QuestRuntime` (application) | Stages, alternatives, events, completion, rewards, consequences |
| Daily pass | `QuestSystem` (order 480) | Offers lapse, quests fail, timers run out, emergent quests arise, people react to deeds they heard of |

## States

Quests move Unavailable → Available → Discovered → Offered → Accepted → Active → Completed, Failed or Abandoned. An offer can also be declined, which returns it to Discovered.

Every move is checked (`QuestStates.CanMove`), stamped with world time and written to the quest's history, and reported to the Chronicle once. A closed quest reopens only if its definition is repeatable (none are yet).

## Objectives

There are 22 kinds:

- talk to, discover a location, visit a location, investigate;
- collect, deliver, acquire, craft;
- defeat a creature, defeat a named person, protect, escort, rescue;
- discover information, persuade, gain faction approval, a crafting task;
- reach a settlement, resolve a settlement problem, complete another quest, survive an encounter, make a decision.

How objectives combine:

- **Stages** are sequential; one stage holds parallel objectives.
- A **group** offers alternatives. Any member satisfies the group and can name the outcome it leads to.
- An objective can be optional, hidden, timed, conditional, unavailable when a condition holds, or fail the quest.

Events come from commands (ADR-0057) and are processed in the same transaction. Collect, acquire, faction approval and settlement problems are re-read from the world when an event arrives.

| Status | Kinds |
|---|---|
| Fed by real systems | Talk, persuade, investigate, discover information, deliver, decide, defeat creature, survive encounter, reach settlement, craft |
| Typed and evaluated, but no current world system emits their event | Escort, rescue, protect, defeat named (an NPC enemy in a world encounter) — **Partial** |

## The quests (all original writing)

| Quest | Category | Source | What makes it real |
|---|---|---|---|
| Short Measures (`granary_shortfall`) | Investigation | Authored, bound to the first town's elder, store-keeper and a member of a faction | The missing grain leaves the settlement's stores when the quest comes to be (Chronicle note). Three endings: the suspect returns it (Persuasion or Intimidation), the player makes it good (4 sacks of grain), or the player takes the suspect's silver. The stores, stability, the suspect's faction influence, regard, memories and notoriety change by ending. |
| Teeth at the Folds (`predators_at_the_folds`) | Hunting | Emergent: wolves or dire wolves (3 or more) within reach of a settlement with herds | The giver's "Show me where" starts an outing that seeks the quarry where it lives. Only wolves slain near that settlement count. The surviving objective is optional. The reward includes a potion. |
| Under Seal (`sealed_letter`) | Delivery | Authored: a town trader and a person in another settlement | The letter is a particular item instance. Reading it breaks the seal (a hidden objective) and reveals its contents. The recipient knows, and trusts the player less. |
| Bread Before Winter (`food_relief`) | Emergent | Emergent: a settlement short of food | Delivered food goes into the settlement's stores (by the food the items are made of). It lapses if the shortage ends first. |
| Empty Shelves (`shop_supplies`) | Crafting & gathering | Emergent: a shop out of a material it deals in | Delivered goods go onto the shelf (real stock). Payment is 125 % of the shop's price. |

## Vertical slice: Short Measures

The slice is played in the tests through the same commands the UI sends, and in PlayMode through the UI itself.

1. A town elder has the quest. The town's stores are short by the missing measures, and the Chronicle records grain gone missing.
2. Talking to the elder: "You look troubled…" leads to the offer. The player can negotiate (Persuasion), accept or decline. Declining keeps it Discovered, and it can be taken later.
3. Accepting: the journal lists it, it is tracked, and the elder says to start with the ledger and the keeper (whose whereabouts become known).
4. The ledger in the street (`InspectObjectCommand`) shows the shortfall in an unfamiliar hand. The objective is done and the discovery recorded.
5. The keeper tells of the suspect's cart. An Insight check can reveal the suspect's debt to their faction. Stage 1 opens: find the suspect.
6. Confronting the suspect reveals the three endings.
7. Ending A, returned: Persuasion or Intimidation. The grain returns to the stores (+missing), stability +3, the suspect resents the player (hostility +30, fear +15, a memory), and the suspect's faction loses influence.
   Ending B, made good: 4 sacks to the keeper. The stores are filled, stability +2, and the giver's gratitude rises.
   Ending C, silver: +50 coin, notoriety +5, stability −2, and the grain stays gone. Reporting it is a lie (Deception check). If caught, the giver's trust falls by 25 and they remember the lie.
8. Reporting to the elder completes the quest. Experience goes through the keyed ledger, coin into the purse, and regard, faction standing and settlement reputation through the keyed reputation ledger. The giver's deed spreads as a rumour.
9. Saved and reloaded, everything is kept, nothing is paid twice, and the elder's greeting reflects their gratitude.

## Phase 17: quests in campaigns and the world's stories

- **Campaign quests are ordinary quests.** A campaign (see `Campaigns.md`) offers existing quest definitions through `QuestLifecycle.Create`, their roles filled from the campaign's roles, with source key `campaign:{campaign}:{key}`. They are never pruned (they are the campaign's record). When one changes state, its campaign is re-read (`CampaignEngine.OnQuestMoved`, from `QuestLifecycle.Move`). There is no second journal: the journal's **Campaign** tab shows the campaign, and its quests stay in the usual lists.
- **The main-campaign category is now used**, by the provisional *Waystone* quests: *A Letter Across the Line*, *Counting Carts*, *Who Holds the Road*, *The Toll-Keeper*, *The Tally Sticks* and *Settling Up*.
- **Decision objectives** can name the person who puts the decision (`To`), so that person's general talk offers the way into the quest.
- **The world's stories** (ADR-0085) are emergent quests with source key `story:…`:
  - *What Became of the Caravan* (always holds: what became of a lost caravan can always be asked);
  - *Grain Down the Road* (holds while the town is short of food).
- **A companion's second story,** *Unfinished Business*, closes through the same loyalty rule as the first.

## Phase 18: four chains, new stories and new vocabulary (ADR-0091, ADR-0093)

- **Chains** (`QuestCatalog.Phase18.cs`). Each has two quests; the second follows through `FollowUps`, with the same people and place.

  | Chain | Ways to end |
  |---|---|
  | Ashes in the Record-House → The Reeve's Accounts | exposed, restored, bought off |
  | The Cairn Question → Who Keeps the Cairn | back the claim, uphold the holder, mediate, stay aside |
  | The Drowned Drift → Greywater's Future | reopened (only once drained), sealed |
  | The Miller's Measure → A Mill for Two | measure restored, miller judged right, shortfall supplied |

  Bound by `Phase18Quests` to people of the place's nearest settlement.
- **World stories** (`QuestCatalog.Phase18Stories.cs`): Bad Blood at the Crossing, Room at the Hearth, Hard Words in {settlement}, The Long Way Round, Word for a Friend. Each offer lapses after 10 days (`ExpiresAfterDays`).
- **Companion stories** (`QuestCatalog.Phase18Companions.cs`): Going Home, Words at the Grave.
- **New kinds:**
  - `ConditionKind.KnowsDiscovery` (38);
  - `ConsequenceKind.FactionRelation` (10), which moves two factions' opinion;
  - `ConsequenceKind.ResolveWorldEvent` (11), which ends the event a story is about.

  Faction-standing rewards and conditions now resolve a faction role directly (`QuestEffects.FactionFor`).
- **Rewards** are keyed and given once (ADR-0092). The new tools have uses in the place mechanics: the pump-wright's wrench and the shuttered lantern.
