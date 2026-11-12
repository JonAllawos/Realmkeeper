# ADR-0089: Places in every region, and three complete mechanism patterns

**Context.** The preflight audit found:
- each world held one instance of each of the ten place definitions, whatever its size;
- further regions held no places at all (`LivingWorldGenesis` returned before places for region-only generation);
- the only mechanism was "pull to open".

The brief asks for deeper locations, no duplicate archetypes, and a small set of complete puzzles: persistent, clue-based, with failure consequences, never reset on load and not solved by guessing.

**Decision.**
1. **Four new places on existing archetypes**, each with a reason to be as it is (`PlaceContent.Phase18.cs`):
   - Greywater Drift (mine);
   - the Ringers' Vault (crypt);
   - Hollin Tower (ruin);
   - Cairn Watch (keep).
2. **Every region gets them.** `PlaceGenesis.EnsurePhase18` sites the region's own set, once per region and deterministically from the world seed and the region:
   - the home region through `PlaceGenesis.Ensure`;
   - further regions through `WorldAtlasGenesis`, which regenerates a region's map only while that region still lacks its places.
   - Place IDs carry the region (`place:{def}@{region}`).
   - Cairn Watch is sited only where two factions exist.
3. **A finishing step.** `PlaceDefinition.Finish` is a deterministic last step on a generated layout. It wires each mechanism to what it works. If it cannot make the puzzle solvable (the workings would lie behind the door they open), the generator tries the next attempt. Tests check 60 seeds per place.
4. **The patterns** (`PlaceActions.Mechanisms.cs`). State lives in the place's own `Variables`, which are saved with it.
   - **Sequence.** Three bells, rung in the order a painted verse gives.
     - A wrong bell brings down grit (a Dexterity save against 1d6) and the sequence starts again.
     - A second wrong peal wakes the dead by the tomb.
     - The door can still be picked or forced at DC 22.
   - **Staged water control.**
     - Clear the intake: Athletics DC 11; a failure costs 1d4 and the attempt.
     - Then work the pump: full → half → dry, after which the flooded gallery needs no swim.
     - Working the pump with the intake choked cracks the barrel. It stays cracked until mended with a pump-wright's wrench or smith's tools.
     - The gallery can still be swum, at the deep-water risk.
   - **Raking light.**
     - A lintel's scoring shows only by a flame held close, and points to the hidden way.
     - A thorough search can also find the way.
5. **Older places deepen.** Bramble Cave and the Broken Hall gain story features in worlds made from now on. A saved layout is never changed.

**Consequences.**
- A six-region world holds up to 24 more places and their quest chains.
- The mechanisms are tested across save and load at every stage.
- Followers will not wade undrained deep water (`PlaceRoutes.Flooded`) and say why.
