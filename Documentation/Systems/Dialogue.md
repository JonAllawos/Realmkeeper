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
