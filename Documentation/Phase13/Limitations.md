# Phase 13: limitations and deferred work

Status words as in the brief: **Complete**, **Partial**, **Data-only**, **Deferred**, **Not implemented**.

## Partial

- **Conditions from traps and hazards.** Poisoned and similar conditions are reported but not kept on the character. There are no lasting conditions outside fights yet.
- **Line of sight.** In fights, walls stop movement, not missiles or spells: a ranged attack or spell through a wall is not refused.
- **Trespass.** Only garrisons enforce restricted rooms. A household's bedroom is marked restricted but nobody reacts. There is no theft.
- **People in interiors.** Occupants are placed by activity in a suitable room. They do not walk between rooms or react to the player beyond conversation.
- **Followers.** Their figures stay behind the player in the scene, carried along through transitions. They do not path through rooms on their own, and in a fight they appear on the player's side.
- **Stairs and ladders.** These are interactions (the floor changes when used), not walkable ramps. Floors are not physically joined.
- **Visuals.** Geometry is generated from cells (rectangular rooms, straight corridors) and props are primitive shapes. There are no new art assets, natural caves are not irregular, and there is no fog or particle effect for hazards (a translucent plane stands for foul air and water).
- **Lighting.** Lit rooms have a torch light, and the player carries a dim light (or a lantern's light). Unlit rooms are dark by design. Screenshots of unlit rooms are dim.
- **Camera and movement.**
  - The third-person camera's existing obstruction handling (sphere casts) meets the new mesh colliders.
  - First-person collision uses the existing character controller against the place's walls.
  - The PlayMode tests move the character by teleporting to rooms and interaction points. Walking through narrow corridors and doorways with the physics controller is not automatically tested.
- **The Player Hub's character preview.** It is empty inside places, which are built without the settlement art kit the preview uses.
- **Map travel.** Entering a place from the map is immediate, as entering a settlement is. There is no travel time or route distance, because the world model has none.

## Data-only

- **Five archetypes have no place in the world:** catacomb, temple, bandit hideout, underground works and abandoned hamlet. They are generated and validated (250 seeds each), but genesis places only the mine, crypt, fort, cave and ruin.
- **Inhabitants that wake on a feature** (`WakesOn`): the commands support it (opening a container, pulling a mechanism), but no placed content uses it yet. The vault lever feature exists in the catalog unused.
- **Bridges** (a connection kind) and the **fire** hazard (in the catalog, used by no archetype).
- **Keys for generated places.** Generated locked doors can be picked or forced; only the authored fort has a key.

## Deferred (from the preflight audit)

- Settlement layout archetypes Roadside and Fortified, which are never chosen, and a test of variety across seeds.
- Metal armour and enchanting recipes; quantity choice and drag and drop in the inventory.
- A crime system, repeatable quests and a content-settings screen.
- Wolf hunts beyond the nearest populations.

## Not implemented

- Companions or parties beyond bounded escorts.
- Fast travel.
- Place ownership changing hands (a fort taken by another faction).
- Places affecting settlement security or merchants.
