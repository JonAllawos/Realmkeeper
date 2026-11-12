# ADR-0086: A fight met on the road is fought on the ground at the road's nearer end, with the existing combat; the party stays on the road

**Context.** Phase 16's "Fight it out" on the atlas let the AI choose the player's actions, because the world map has no fight scene, even though the fight itself was already the one engine's encounter (`TravelCommandHandlers.Fight` → `WorldEncounters.StartPlan`, drawn near the road's nearer end). Phase 17 asks for a proper transition into the existing combat system and back, preserving participants, route state and companions, without a second combat engine.

**Decision.**
1. **The fight is named by the journey.** A road fight is the encounter the journey names (`Journey.EncounterId`) while the player is on the road. `TravelCommandHandlers.RoadFight` / `WorldSession.GetRoadFight` returns the ground (the settlement at the nearer end of the current segment, the same end the encounter was drawn near), the route, and the encounter.
2. **The view changes by itself.** The runtime's view mode becomes Exploration while a road fight exists. The exploration controller loads the roadside ground: the near settlement's terrain and land, without its buildings, props, people, herds, streets, walls or interactions (`RoadsideGround`). The player stands at the road's arrival point.
3. **The existing panel runs it.** The existing combat window and markers run the existing encounter: its participants (the band's own creatures, the player and the travelling companions), the tactical grid, movement budget, reactions and opportunity attacks. Positions, terrain and cover are the engine's; the scene only shows them.
4. **The world state is unchanged.** The party stays on the road throughout: `LocationKind` WorldMap, no settlement. Practice and outing buttons are hidden, and poses are not recorded.
5. **Leaving.** When the fight is over (won, lost or withdrawn from), the ground stays until the player leaves it: by closing the panel or the HUD's return. `LeaveRoadFightCommand`, which is refused while the fight goes on, clears the journey's encounter and halts the journey where it was ("after a fight on the road"). Travel resumes only when the player resumes it.
6. **Saving.** Saving mid-fight keeps the fight and the road. On load the ground is staged again.
7. **The AI option remains, labelled.** The atlas keeps an explicit, labelled "Let it play out (the AI plays your turns)", which is never the default. The scene opens on its own as soon as the fight begins.

**Consequences.**
- Road fights are played with full tactical control. Loot, witnesses and crime use the existing encounter end, because the encounter is the same one. Combat does not move the clock, as before.
- **Limitation:** the ground is the near settlement's land, not a generated stretch of the road itself. The terrain shown is that settlement's surroundings. Tactical terrain (difficult ground, cover) comes from the encounter's map cells, as for every fight; this scene presents it and does not decide it.

**Alternatives considered.**
- A full road-scene generator: deferred, as a larger presentation task than this closeout.
- Entering the settlement to fight: rejected, because it would move the party off the road and record a false arrival.
