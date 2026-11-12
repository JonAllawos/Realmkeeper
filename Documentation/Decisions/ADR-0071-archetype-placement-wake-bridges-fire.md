# ADR-0071: New places reach every world once, chosen by context; creatures wake in sight; bridges and fire have state

**Status.** Accepted, 2026-11-11 (Phase 14).

**Context.** Five Phase 13 archetypes, waking creatures, bridges and fire were defined but never placed:
- `PlaceGenesis.Ensure` was all-or-nothing: once any place existed, nothing more could ever be added to a world.
- The wake hook started the fight in the group's own room, even when the player was elsewhere.
- Bridges were an enum value only.
- The fire hazard used the bypassing harm function.

**Decision.**
1. **Ensure per definition.** Each `PlaceDefinition` is ensured on its own, by its stable ID (`place:{definition}`). Loading a Phase 13 world adds only the definitions it lacks, once. Existing places, layouts and states are never touched or regenerated.
2. **Chosen by context, deterministically.** Each new definition has an eligibility rule from the world itself:

   | Place | Placed near |
   |---|---|
   | Catacomb | a town or village (its history) |
   | Temple | a settlement whose faction it was built by |
   | Bandit hideout | a road between two settlements that trade, preferring one with a trade route |
   | Underground works | a town |
   | Abandoned hamlet | open plains or forest |

   Among the eligible, a world takes three to five, chosen by a hash of the world seed. Worlds differ in which places they have, as well as in where they are and what is inside.
3. **Stories, not catalogue entries.** Each new place has its own:
   - authored story features, clues and discoveries;
   - creatures from the bestiary and items from the catalogue;
   - a quest through the Phase 12 engine (catacomb, temple, bandit hideout and culverts);
   - for the hamlet, a discovery chain explaining why it was left.

   The bandit hideout's lookouts parley (pay, persuade, intimidate or fight). While they hold the road, the trade route beside it loses part of each shipment. Clearing them or talking them off the road ends that.
4. **Waking.** A group may wake when a feature in its own room is opened, taken from or used (`GroupTemplate.WakesOn`). The fight starts only if the player is in that room or can see it through an open way. Otherwise the group is marked awake and fights on sight when the player arrives. The state changes once (Present → Fighting → Defeated or Survived), and the creature instances are keyed, so re-entering never wakes or spawns them again.
5. **Bridges** (`ConnectionKind.Bridge`) are generated on loop passages over water or a chasm where the layout has a loop, so a destroyed bridge does not cut the only way. Where a layout has no loop (a chain of rooms), the bridge goes on the passage to a side room that holds no required or story feature and no key (the generator skips any room that does): losing it closes that side room only, never the way to anything a quest needs. Each has a state (`PlaceData.Bridges`):
   - **Intact:** free to cross.
   - **Damaged:** an Acrobatics check to cross. A failure drops the player into the water below (damage through the shared pipeline) and brings the bridge down.
   - **Destroyed:** not a connection for movement, routes or followers.

   The place map draws each bridge with its state.
6. **Fire** is a hazard like any other (Dexterity save, fire damage through the shared pipeline, the Chronicle).
   - A burning room burns for a bounded time from when it was first found, then stays burnt out. Its state is the time recorded in the place's variables: no ticking, no spreading.
   - A torch or lantern can burn away a few marked obstacles (webs, a barricade), once.
   - The scene shows flames and an orange light while it burns, and charring after.
7. **Keys.** A definition may name a key item. The generator then gives its deepest locked door that key and puts the key in a container reachable without passing that door.

**Consequences.**
- Five more archetypes are real places in real worlds, each tested through generated instances.
- Existing saves gain them exactly once, keeping every explored room, opened chest, dead creature and active quest.

**Alternatives considered.**
- *All five in every world:* repetitive. Rejected by the brief and by design.
- *A fire simulation that spreads cell by cell:* unbounded and hard to save. Deferred.
