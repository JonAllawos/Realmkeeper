# ADR-0044: The bestiary is data; creatures are drawn from aggregate populations into named instances

**Status.** Accepted, 2026-11-11 (Phase 10).

**Context.**
- Encounters used four hand-placed creatures. Wildlife existed only as scenery.
- Simulating every deer as an entity would cost more than it shows.
- Fights should leave traces: the wolf that fled is the same wolf next time, and a killed bandit band shrinks.

**Decision.**
1. **Bestiary as data.** `CreatureDefinition` gains:
   - category, behaviour profile, habitats, diet and social form;
   - traits (Charge, Regeneration, Undead Fortitude, Relentless, Nimble Escape, descriptive ones);
   - reactions (Parry) and loot;
   - attack riders (a save on hit, a grapple escape DC, recharge).
   `CreatureCatalog.Validate()` checks the whole catalog.
2. **Content and licence.** Statistics come from the SRD 5.1 (CC BY 4.0) with attribution:
   - deer, boar, the bears, dire wolf, giant rat, giant spider, goat;
   - horses, mastiff, goblin;
   - highwayman (the SRD 5.1 Thug), bandit captain, cultist, veteran;
   - skeleton, zombie, ogre, troll.
   Rabbit, fox, cattle, sheep and chicken have no SRD statistics. They are labelled Realmkeeper originals.
3. **Traits in the engine.** `CombatEngine.Creatures` applies traits at their rule points:
   - at the start of a turn: recharge, regeneration and its fire or acid block;
   - before death: Undead Fortitude and Relentless;
   - on hit: riders and conditions;
   - on movement: charge and Nimble Escape.
   Behaviour profiles drive the AI (`CombatAi.Behaviors`): skittish animals flee, packs flank, predators web first, the mindless press on.
4. **Aggregate populations.** `CreaturePopulationData` holds a creature, a region, a centre and range, a count, a capacity, deaths and draws. `FaunaGenesis` places:
   - wildlife niches per 32-cell sector;
   - road bands of bandits;
   - vermin in each settlement;
   - herds from each settlement's farms.
5. **Weekly fauna.** `FaunaSystem` (`realmkeeper.fauna`, order 460) runs once a week:
   - logistic breeding up to capacity;
   - wolves take deer in their sector;
   - settlement hunters take game;
   - herds move toward what the farms keep; a hungry settlement slaughters a tenth.
6. **Instances.** An encounter draws named `CreatureInstanceData` from a population (ADR-0046). The instance records its fate (alive, dead, fled), its hit points and its last encounter. The population counts its dead.

**Consequences.**
- Wildlife costs a few dozen records per region, not thousands of entities.
- Every creature in a fight has a source and leaves a mark.
- New creatures are data plus, rarely, a trait implementation.

**Alternatives considered.**
- *Individual simulated animals:* too costly.
- *Invented statistics for SRD creatures:* rejected; no non-SRD rules content.
- *Spawn tables with no population:* fights would leave no consequence.
