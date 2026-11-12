# ADR-0075: The party fights through the one engine; companions think in a layer of the one AI, and say why

**Context.** Companions must fight beside the player with their own HP, spell slots, features, conditions and equipment, and their decisions must be explainable. There is one combat engine (Phase 8) and one deterministic AI (`CombatAi.Next`, with creature behaviours and spell choice).

**Decision.**
1. **Joining.** `PartyCombat.Join` adds the active members who are with the player (following, or waiting where the player stands), alive and with a sheet, as `CombatantKind.Npc` participants on the player's side. It runs:
   - in the common start of every fight (practice, outings, generated encounters), on free squares around the player, nearest and behind first;
   - in place fights, on free cells of the room.

   Someone already in the setup (the guard of a practice fight) is not added twice.
2. **What happens stays with them.** The engine uses their sheet live (`SheetOf`), so wounds, spent slots and conditions persist. Lasting conditions return through the Phase 14 path. Their equipment comes from their own items (`CharacterRules.LoadoutOf(companion)`). Experience comes from the existing keyed encounter awards to people on the player's side.
3. **A companion layer in front of the baseline** (`CombatAi.ForCompanion`), in this order:
   1. a healing spell for someone on their side who is dying or at a quarter of their HP or less (the baseline's own healing rule picks the spell and slot);
   2. first aid (Stabilize) for a dying friend, going to them first if needed;
   3. their own Second Wind at half HP or less;
   4. Rage when there is a fight to rage in;
   5. defending the player from a foe beside them;
   6. otherwise the baseline, explained by what it chose.

   Every choice returns a reason in words. Proposals only: the engine validates every action, and a refused proposal ends the turn as before.
4. **Diagnostics in the log.** A new log kind, `Reason`, records "Name: why" whenever a companion's reason changes within a turn (`CombatEngine.Explain`). It changes no state. The combat HUD shows it in the log (styled apart), and saves keep it.
5. **The aftermath** (`PartyAftermath`, once per fight by key) applies the loyalty causes of ADR-0074 and removes the fallen from the party.
6. **Resting together.** The player's rest applies the same `CharacterRest` short or long rest to each member present, keyed per person, with the same once-a-day rule. Hit dice on a short rest are spent only as far as needed.

**Consequences.**
- No second combat engine, AI or rest system. A companion's turn is an ordinary AI turn.
- Determinism holds: the same state gives the same proposal and the same reason (tested).
- The baseline's healing threshold (a quarter of HP) is shared, so companions do not heal at a different threshold.

**Alternatives considered.**
- **Player control of companions' turns.** Deferred: the brief asks for deterministic companion AI. Direct control would add a second input path to every AI turn.
- **Reasons only in a debug overlay.** Rejected: the brief asks for explainable decisions the player can read.
