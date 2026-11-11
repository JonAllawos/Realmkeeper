# ADR-0033: Combat rules configuration (SRD 5.2.1), with recorded Realmkeeper choices

**Status.** Accepted, 2026-11-11 (Phase 8).

**Context.**
- Phase 7's armour class was provisional: a protection number.
- Phase 8 needs real armour, weapons, attacks, conditions and death from the SRD 5.2.1.
- It also needs decisions on points the SRD leaves to the table or the interface.

**Decision.**
1. **SRD armour replaces the provisional armour class.**
   - Categories, base AC and Dex caps come from the SRD; a shield gives +2.
   - Untrained armour gives disadvantage on Strength and Dexterity tests. An untrained shield gives
     no AC.
   - Heavy armour without the Strength requirement costs 10 ft of speed.
   - The AC breakdown names every part.
2. **Weapons carry SRD profiles** (damage, properties, ranges, mastery name), keyed by the existing
   item IDs.
   - Item mappings: arming sword = longsword, hunting bow = shortbow, leather and forester jerkins =
     leather and studded leather.
   - Chain mail is added as a new item.
3. **Realmkeeper choices, recorded:**
   - **Grid:** 5-ft squares with Chebyshev distance, so a diagonal step costs 5 ft.
   - **Reactions:** opportunity attacks are taken automatically.
   - **Ammunition:** a property only; there is no ammunition count.
   - **Off-hand attacks:** none, because equipment holds one weapon hand.
   - **Defences:** resistance halves, then vulnerability doubles.
   - **Grapple:** needs a free hand.
4. **Weapon mastery:** effects are deferred; only the names are recorded.

**Consequences.**
- Armour now matters. The Phase 7 armour class test was updated to the new rule; it was not
  weakened.
- The deferred items are listed in `Systems/Combat.md`.

**Alternatives considered.**
- Keeping provisional armour until a later phase: rejected, because combat needs a real armour
  class.
