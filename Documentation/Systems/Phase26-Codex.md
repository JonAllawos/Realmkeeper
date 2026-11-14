# The codex (Phase 26)

**Decision.** ADR-0117: the codex is derived from existing knowledge records.

**Where.** Hub ▸ Codex (H, then Codex).

**Code.**
- `WorldSession.GetCodex` in `Application/Interface/CodexQueries.cs`.
- The screen: `PlayerHubWindow.Codex.cs`.

## What it holds

| Category | An entry exists when | Facts shown | Never shown |
|---|---|---|---|
| People | The player has met or spoken with them | The Phase 20 known-person lines, each with how it is known ("seen", "said in conversation", "known from long acquaintance"); quests they gave that the player knows | Anyone never met; a stranger's numbers; memories; what longer acquaintance would add (said as a note) |
| Companions | They travel, or travelled, with the player | As people, plus "With you" | — |
| Factions | The player has a standing with them, or has visited a settlement they govern | Kind; the player's standing in words; who leads them (only if met); the visited settlements they govern | Influence, treasury and other simulation numbers |
| Settlements | Visited, or heard of | Region; once visited: kind, people, governing faction | Details of settlements only heard of |
| Places | Rumoured, found or visited | Region, nearby settlement; once found: its description; discoveries made there | Unknown places; descriptions of rumoured ones |
| Creatures | Met in a fight (the bestiary record, ADR-0116) | Fights, defeated, habitat; once defeated: armour and hit points | Creatures never fought |
| Discoveries | In the journal | The discovery's text, kind, certainty, place | — |
| Events | In the journal's chronicle as the player can know it (their deeds, events where they have been) | The event | Events the player could not know |

## Using it

- **Category chips** with counts. A category with nothing known is not offered.
- **Search** across names and known facts, ignoring case.
- **Sort** by name (grouped by category), or most recent first (when the record has a date).
- **The inspector:** every fact with "Known from …" on hover, what the entry connects to, what knowing more would add, and where the knowledge came from.

## Persistence

**No codex state.** The codex is rebuilt from the saved records every time it is shown. Consequently:
- it survives save and load exactly;
- it cannot hold duplicates;
- it updates the moment a record changes (a conversation, a visit, a fight).

**Tested:** `Phase26CodexAndTradeTests`.

## Lore restrictions

The codex never invents. Every name and description is the world's own data. The ancient realm has no entries unless the campaign creates the records.
