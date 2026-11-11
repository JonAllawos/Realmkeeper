# ADR-0054: Crafting, gathering, repair and attunement are work that holds its inputs and takes world time

**Status.** Accepted, 2026-11-11 (Phase 11).

**Context.**
- The brief asks for crafting, gathering and repair that are real systems, not menus that conjure items.

**Decision.**
1. **Recipes are data.** `RecipeCatalog` has 11 recipes across blacksmithing, leatherworking, woodworking, tailoring, alchemy, cooking and general. Each lists:
   - its inputs and the tool (with its proficiency);
   - the station, as a location kind in the settlement: a workshop, a tavern kitchen, a shrine;
   - minutes, the check (ability and DC), the output, whether great success makes it Fine, and the share of materials lost on failure.
   `Validate` checks every reference.
2. **Work.** `StartWorkCommand` (Craft, Gather, Repair, Attune) creates a `WorkJobData`:
   - it holds the materials or the item under repair;
   - it settles the check at once from the world's dice, seeded by the job key;
   - it sets `ReadyAt`.
   Only one piece of work runs at a time. `CollectWorkCommand` pays out once the clock has passed `ReadyAt`. `CancelWorkCommand` returns everything held. `WorldSession.FinishWork` advances world time (the world lives through it) and then collects.
3. **Gathering is finite.** Each settlement's sites come from what it produces. Hedgerows yield silverleaf everywhere. Its woods give deadfall timber if it logs, a seam gives iron ore if it mines, and water gives fish if it fishes. A site has stock and capacity, and the weekly `GatheringSystem` (order 470) restores part of it.
4. **Repair.**
   - At an armourer, mending to Good costs 15 % of base value per step, paid to the keeper.
   - Doing it yourself needs the discipline's tools (smith's, leatherworker's or carpenter's), an hour per step, and a DC 10 + 2 × steps check.
5. **Attunement** is the same work: an hour, with the item kept on the person. The limit and curses are checked again when it is collected.

**Consequences.**
- Nothing is created from nothing. Materials are taken when work starts and returned on cancel or partly on failure, and every movement is validated.
- Disciplines with no recipes yet (armorsmithing, enchanting) are listed as prepared, not built.

**Alternatives considered.**
- *Instant crafting:* no cost in time.
- *A roll on collection:* outcomes could be save-scummed by reloading before collecting.
