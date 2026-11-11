# ADR-0052: Equipment state and magic items act only through the rules engine

**Status.** Accepted, 2026-11-11 (Phase 11).

**Context.**
- Phase 5's "protection" and "damage" numbers did not match the SRD rules that combat used.
- Magic items, condition, ammunition and two-weapon fighting had no effect on play.

**Decision.**
1. **One calculation.** `CharacterRules.LoadoutOf(player)` passes worn items to the rules as typed effects. These are AC bonus, save bonus, ability minimum and darkvision, together with body-armour and shield condition and carried weight. Unattuned items that need attunement, and broken items, give nothing. The hub, the comparison and combat all read the same `CombatStats`.
2. **Condition.**
   - Damaged: −1 to attack rolls with a weapon, or −1 AC for armour or a shield.
   - Broken: a weapon cannot attack; armour or a shield gives no AC.
   - Every step changes value.
   - A natural 1 wears the weapon. A critical hit taken wears the armour (or the shield when no armour is worn).
   - Only the player's instances wear.
3. **Magic.**
   - Weapon bonus is added to attack and damage as a named part of the breakdown.
   - AC and save bonuses apply while the item is worn.
   - Attunement is limited to three items and takes a short rest (an hour of work, ADR-0054).
   - A cursed item binds itself when attuned: it cannot be unequipped or unattuned until a shrine's priest lifts the curse for 60 coin.
   - A wand casts its spell through the spell engine (`item:{stack}`), with one charge per spell level, and regains its rolled recharge after a long rest.
4. **Weapons in use.**
   - Ranged attacks spend the pack's ammunition, and half is recovered after the fight (SRD 5.2.1).
   - A Light weapon in the off hand gives the off-hand attack as a bonus action, with no positive ability modifier to damage.
   - Rings go on either hand.
   - Potions are a bonus action in a fight, and can be drunk outside one.

**Consequences.**
- Numbers on screen are the numbers in play.
- Recharge comes after a long rest, where SRD 5.2.1 says "at dawn". This is documented, and it stops the charges being farmed by waiting.

**Alternatives considered.**
- *Item-specific code paths in combat:* two sources of truth for armour class.
- *Recharge at dawn through a daily system:* charges would come back even when the player never rested.
