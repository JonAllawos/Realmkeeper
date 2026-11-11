# ADR-0060: Activities arise from world conditions; the player discovers them; the journal shows only what is known

**Status.** Accepted, 2026-11-11 (Phase 12).

**Context.**
- The brief asks for emergent tasks that have a reason to exist, expire when the reason ends, and are not duplicated.
- It also asks for discovery to be separate from existence, and for a Chronicle that does not reveal the undiscovered.

**Decision.**
1. **Causes.** `QuestGenesis.Emerge` reads the world. Each cause has a deterministic key:
   - a settlement short of food (`shortage:food:{settlement}`, checked daily) asks for food;
   - predators (wolves, dire wolves) within the encounter planner's reach of a settlement with herds (`predators:{settlement}:{creature}`, three or more, weekly) start a hunt;
   - a shop out of a material it deals in (`supplies:{shop}:{item}`, weekly) asks for stock.
   There is at most one open instance per cause. An offer lapses when its expiry passes or its cause no longer holds (`QuestCauses.Holds`). An accepted emergent quest fails with its reason if the cause ends first.
2. **The authored slice** (the granary shortfall and the sealed letter) is made once, bound to the first town's people, at world creation, or when an old save first loads (as Phases 10 and 11 add theirs).
3. **Discovery states.** A quest the player has not heard of is Available: it is the world's, not the player's, and appears nowhere. It becomes Discovered through:
   - the notice board;
   - the ledger;
   - a person's news or rumours;
   - the giver's trouble, which makes it Offered.
   Places are separate: known (told or read, `KnownPlaces`) or visited (`VisitedSettlements`). A quest needing a place does not reveal it; the giver says where, and that is recorded.
4. **One object command.** `InspectObjectCommand(key)` handles every world object by its stable key (`ledger:{settlement}`, `notice:{settlement}`; containers have their own commands from Phase 11). `ReadItemCommand` reads letters and books. There is no per-object system.
5. **What the player knows.**
   - The journal lists only Discovered and later states.
   - A quest's detail shows only revealed objectives, people with what the player knows of their whereabouts, and known places.
   - The journal's Chronicle shows the player's own doings and notable events where the player has been.

**Consequences.**
- No quest exists without a cause, and none persists after its cause, unless the player already took it on.
- The journal is honest about what the character knows.

**Alternatives considered.**
- *Random quest generation:* no reason to exist, and contradictions.
- *Revealing every marker:* the brief rules it out.
