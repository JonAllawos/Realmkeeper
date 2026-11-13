# ADR-0102: People are known as far as the player knows them

**Status.** Accepted, 2026-11-12 (Phase 20).

**Context.** Named people carry full character sheets (Phase 10). The strategic map's people inspector is the DM's view and shows everything. Before Phase 20, the player character had no in-world way to look at a person.

**Decision.** `KnownPersonQueries.View` builds a person as the player knows them, from the same record the rules use, by acquaintance:

| Acquaintance | When | Adds |
|---|---|---|
| Seen | Always | Apparent species, sex and age, occupation, what they wear and carry, a visible injury or death |
| Acquainted | Spoken with (or met) | Where they are from, their background, whom they serve |
| Familiar | Familiarity 50+ | Their training and subclass, with level in words (untried, seasoned, veteran, renowned); what they are good at; languages |
| Companion | In the party | The numbers: level, HP, speed, abilities, saving throws, features |

Each line says how it is known. What is withheld is said ("You would have to know them better…").

**Never shown:** memories, private knowledge, exact experience, or a stranger's numbers.

The view appears as **"About them"** in conversation. The DM's inspector is unchanged.

**Consequences.** It is cheap: 0.06 ms per person. It is tested at each threshold.
