# Phase 12: known limitations, partial features and deferred work

Status words: **Complete** = built, used by the slice, and tested through commands and the UI; **Partial** = works where named, with the gap stated; **Data-only** = typed and validated, with nothing in the game that uses it yet; **Deferred** = not built.

## Partial

- **Escort, rescue, protect and defeat-named objectives.** These are typed and matched by the engine. Escort and rescue events are emitted by nothing: there is no escort AI and no captives. A protect objective would need a hook from Phase 10's farm-defence fights (`EncounterObjective.Protect` exists there). Defeat-named is emitted when an NPC enemy dies in a world encounter, but no quest uses it.
- **Theft, assault and killing as reputation sources.** The model is ready: deeds, notoriety, witnesses, keyed changes. But there is no player theft mechanic, and attacking residents outside practice fights is not a player action. Reputation in Phase 12 comes from quests, dialogue, abandoning, and deeds spreading.
- **Faction-standing effects.** Prices and merchant access are implemented. Recruitment and political opportunities are only named in the brief for future phases. Quest availability by standing has a condition kind (`FactionStanding`), which no Phase 12 quest uses.
- **Notice boards.** One per settlement (the plan's labour notice board) lists the posted quests. No other object type (letters on walls, shrines, monuments) has an inspect action yet: the command takes keys of the ledger and board kinds only, and adding a kind is one case in it.
- **Incorrect beliefs.** Knowledge can be out of date (Believed, Rumor). No quest is yet built around a person believing something false.
- **Hunts depend on the world.** "Teeth at the Folds" exists only where wolves or dire wolves (three or more) are in reach of a settlement with herds. The outing it starts seeks them, but a fight is a fight: the player may meet a smaller group, or lose.

## Data-only

- **Quest categories** without authored or emergent quests yet: main campaign (deliberately: no canon is fixed), faction, exploration, bounty and contract. The engine and the journal handle them.
- **Content tags.** Profanity, blood and injury, mature themes, adult relationships and sensitive subjects exist and are honoured by the presentation rules. No Phase 12 line uses them, and there is no content-settings screen yet (`SetContentPresentationCommand` is the hook).
- **Repeatable quests.** Supported by the state machine; none are repeatable.

## Simplifications

- One conversation at a time. Interruptions (a fight starting from a response) end it; nothing else interrupts.
- People keep no inventories. An item given to someone who is not a shopkeeper is recorded as delivered and leaves the world. A shopkeeper puts it on the shelf.
- Quest offers lapse on their expiry or cause. Accepted authored quests have time limits (21 days for the granary, 30 for the letter); emergent ones fail when their cause ends.
- The journal's Chronicle shows the player's own doings and notable events in visited settlements. It does not yet know what individual people told the player about other places.
- Quest markers on the map are not implemented. The journal says where people and places are, as far as the player knows.

## Deferred (not started)

- A main campaign and its canon (kingdoms, the protagonist's identity, an antagonist): kept configurable, as the brief requires.
- Companions, recruitment, and political quests.
- A content-settings screen.
- Voice, portraits and cinematic dialogue presentation. The dialogue window is functional, not final art.
