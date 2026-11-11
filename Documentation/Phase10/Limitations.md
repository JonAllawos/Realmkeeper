# Phase 10: known limitations

What Phase 10 does not do, or does only in part. Nothing here is hidden behind a placeholder that claims more.

## Settlements

- **Plans don't grow.** A plan is made once and kept (ADR-0048). When a settlement grows, gains a shop or loses
  a building, the plan does not change. Growth will need an explicit, versioned plan edit; it is not designed yet.
- **Upgraded saves.** A v11 save gains plans made from its own state at upgrade time. These are deterministic,
  but not necessarily what a fresh world of the same seed would have, since the world has moved on.
- **Older environments.** A world without plans (only possible in tests that turn the living world off) still
  gets the older two-street environment.
- **Buildings look by kit, not by use.** Function sets size, materials, roof and storeys. The modular kit
  planner still builds a barn, a mill or a warehouse as a house of that size. Only the tavern and the shops
  are enterable.
- **Herds are placeholders.** Box-built animals, one for every few head (at most twelve per herd). They stand
  still: no animation, no wandering, no herding.
- **Water.** It is a surface with no collider. A character can walk down into a riverbed (shallow, never
  deadly). There is no swimming or current.
- **Ground.** One grass material for the terrain; fields are tinted. There is no texture blending on slopes or
  banks.
- **Map scale.** The strategic map draws buildings 1.6× larger than scale so they stay legible. Their
  positions, orientation and the wall are to scale.
- **Walls.** Wall runs are straight boxes between circuit points, with merlons. A lane or farm track that
  crosses the circuit gets a gap (a postern) rather than a gate structure.
- **Lighting budget.** In a planned town, night lamps are limited to 32 street lamps and the fronts of 24
  buildings near the square or at the sites.

## People and creatures

- **Promotion is not automatic.** People are promoted by a command (the DM tools use it). The simulation does
  not promote anyone yet.
- **Experience.** NPCs with sheets gain it from world encounters and from awards. Practice fights give none,
  as before.
- **Wildlife is aggregate.** Populations don't migrate between sectors, there are no seasons, and hunting
  takes a fixed share. Individual animals exist only once drawn into an encounter.
- **Experience.** Awarded for foes slain, or routed when the player's side wins. A withdrawal or a defeat gives
  none for those who got away.
- **Encounter purposes.** Only three (outskirts, road, farms), started from inside a settlement. There are no
  travel encounters on the strategic map yet.
- **Creature models.** Creatures in fights use the Phase 8 stand-in figures.

## Reactions

- **Who is asked.** Only player-controlled combatants are asked. Allies with sheets decide by the computer
  policy.
- **Hellish Rebuke and Shield.** Partial: damage or attacks from spells do not trigger them yet (spell
  catalog notes).
- **Missed attacks.** The rule point for a missed attack has no reaction using it yet.
- **One question at a time.** Several questions in one action are asked in turn, each replaying the action.
  That is correct, but slower for actions with many triggers.

## Tests

- **Equivalent mutants.** Some guards are backed up by a second guard, so removing one changes nothing
  observable (Testing.md lists them):
  - the experience-key check: the world validator refuses duplicate keys;
  - the encounter's resolved flag: each consequence is itself keyed;
  - the fauna capacity clamp: logistic growth cannot overshoot;
  - the migration's list initialisation: the mapper defaults a missing list.
- **Reaction test retries.** The PlayMode reaction test plays practice fights until a hit lands that Shield
  could answer, recovering between fights (as the Phase 9 driver does). It is inconclusive, not failed, if no
  such hit lands in six fights.

## Owner decisions still open

- **Scope.** The scope difference between the Product Foundation (§1) and the Game Design Bible is recorded,
  not resolved (see `Product/ProductFoundation.md`).
- **Original creatures.** Rabbit, fox, cattle, sheep and chicken use Realmkeeper statistics, labelled as such.
  The owner may replace them.
