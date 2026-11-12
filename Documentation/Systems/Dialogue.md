# Dialogue (Phase 12)

Decision: ADR-0058. Code: `Domain/Quests/DialogueModel.cs`, `DialogueCatalog.cs`; `Application/Quests/DialogueEngine.cs`, `QuestCommands.cs`; `Exploration/Presentation/DialogueWindow.cs`.

## How a conversation works

1. **Start.** "Talk…" in a resident's topics, or `StartConversationCommand(npc)`, starts it. The person must be here and awake (Phase 4's rule). Meeting someone counts toward familiarity once a day. The first meeting is recorded in the Chronicle.
2. **Their line.** They say the first line of the current node whose conditions hold and which the content rules allow. For example, a hostile person keeps their distance, a frightened one is wary, a grateful one is warm, a busy one is short, and a low-morale one is tired.
3. **Your responses.** You see the responses whose conditions hold, numbered 1–9.
   - At the start of anyone's talk, the engine adds a way into each quest they play a part in, and their shop, unless they are hostile.
   - A response with a check names it, for example "Persuasion DC 13".
4. **Choosing.** `ChooseResponseCommand(id)`:
   1. checks that the response is still open;
   2. rolls the check;
   3. applies the consequences;
   4. applies the quest events they caused;
   5. moves to the next node, or ends the conversation, or starts a fight.
   All of this is one transaction.
5. **Leaving.** Esc, "Leave", or a response leading to `end`, ends the conversation (`EndConversationCommand`). The conversation in progress is saved, so a reload resumes it.

## Checks

A check is d20 plus the player's skill bonus from the rules engine, against the DC. A natural 20 always succeeds and a natural 1 always fails.

The DC moves with the person: (trait − 50) / 10, using greed against persuasion, courage against intimidation, and trustfulness inverted against deception. The dice are the world's, keyed by the conversation and the turn.

## Consequences (typed)

- **Talk:** answer a topic; offer, accept, decline or abandon a quest; record a decision; set a variable; set the outcome; complete or reveal an objective.
- **Standing:** regard (any dimension), faction standing, settlement reputation, notoriety.
- **Knowledge:** reveal a fact, hear a rumour, a memory, a witnessed deed, reveal a location.
- **Items and money:** give items (a particular instance, any food, a role's item, or an item ID; a keeper shelves them), receive an item, pay or receive coin.
- **Other:** start an encounter, the person's morale.

## Implemented interactions

| Conversation | Choices |
|---|---|
| Anyone | Greeting by regard, mood and activity; 11 Phase 4 topics; quest hooks; trade with a keeper (refused when hostile) |
| Short Measures giver | Accept · negotiate (Persuasion) · decline · report each ending · lie about the silver (Deception, with consequences if caught) |
| Short Measures keeper | Thanks (clue) · "You're holding something back" (Insight) · hand over four sacks |
| Short Measures suspect | Challenge · persuade (Persuasion) · intimidate (Intimidation, tagged Threats) · ask the price · take or refuse the silver |
| Teeth at the Folds giver | Accept · decline · "Show me where" (starts the outing, seeking the quarry) · report |
| Under Seal giver and recipient | Accept (receive the letter) · ask what's in it · decline · hand it over sealed or opened (only what is true is offered) |
| Bread Before Winter, Empty Shelves | Accept · decline · give what was asked · report |

Lines are written for their speakers: the elder is anxious about the ledger, the keeper practical, the suspect cornered, and the recipient wary of a broken seal.

## Phase 17: campaign decisions, disabled choices, companions (ADR-0083, ADR-0087)

- **New condition kinds** (in the shared evaluator), the campaign ones answered by the simulation's `CampaignConditions`:
  - campaign arc, decision, flag, state, quest and arc-days;
  - what has become of a person (`CharacterStatus`: dead, captured, hostile, away, …);
  - a world event open on a road or at a place (`WorldEventOpen`);
  - companion loyalty, settlement stability, a road being open, two people's bond;
  - a companion having something to say, campaign standing with a faction;
  - the player's coin, and a companion quarrel.

  Roles in these conditions are the campaign's roles first, then the quest's.
- **New consequences:**
  - `CampaignDecision` makes a decision through the campaign engine (recorded once; effects applied once; later consequences scheduled; companions react). It fails the whole choose command if the decision is not open.
  - `CampaignFlag`.
  - `HearRemark`: a companion says what is on their mind.
  - `Reconcile`: with a check, talks two companions round.
  - `Relation`: changes two quest people's bond.
- **Disabled choices.** A response may carry `Requirements`. When they fail and `ShowWhenUnavailable` is set, the response is shown disabled with `UnavailableText` as the reason (for example, the shared count without proof). Choosing it is refused, by the window and by the command. The keyboard numbers skip it.
- **Decisions are marked.** A response that makes a campaign decision is marked "◆ … [decision]" in the window: its consequences last.
- **Campaign roles in text.** In a campaign quest's dialogue, `{role}` tokens name the campaign's people and places (the warden, the far town) as well as the quest's own.
- **Companions in general talk.** "Something on your mind?" (when they have something to say), and "You two need to settle this." (Persuasion, when they quarrel with another companion present).

## Phase 18: discoveries in conversation (ADR-0088, ADR-0091)

- **`ConditionKind.KnowsDiscovery`** opens lines for players who found something: the Cairn Watch log, or the ringers' verse, as precedent in *The Waystone Count*.
- **Sharing a finding.** General conversation offers at most two findings the person would care about (`DiscoveryRules.Offerable`), as "share:" lines.
  - Telling one gives what such a person has: standing, regard, or a little coin for trade news.
  - This happens once per settlement, recorded on the discovery's `SharedIn` (consequence `ShareDiscovery`, 37).
  - Unverified minor guesses are not offered.
- **"Witness"** is a fourth way to settle the toll-keeper's count in *The Waystone Count*.
