# Phase 8 pre-flight: Combat Foundation, Encounter Engine & Settlement Structures

Written 2026-11-11, before any Phase 8 code.

## 1. Repository and Git baseline

| Check | Actual state |
|---|---|
| Branch | `feature/phase-0-1-foundation` (no separate Phase 8 branch exists; work continues here) |
| HEAD | `a3cd9f5b7f647ab1053caa4e83ed03c6ff02311d` (Phase 7) |
| Origin | `a3cd9f5` (`origin/feature/phase-0-1-foundation`) |
| Ahead / behind | **0 / 0** |
| Modified | `ProjectSettings/GraphicsSettings.asset` only (formatting-only, preserved, not to be staged) |
| Untracked | None |
| Ignored (relevant) | Old `Documentation/Screenshots/dev-*.png` captures, `Library/`, `Logs/`, `UserSettings/`, generated `.csproj` files |
| Save schema | **v9** |
| Baseline tests | .NET **424/424** (run now, at `a3cd9f5`). Unity EditMode **424/424** and PlayMode **67/67**: the final Phase 7 runs, on identical code. |

**Differences from the brief's description of the Phase 7 state:**
- **Phase 7 is not "unpushed, three commits ahead".** After the Phase 7 report, the owner explicitly
  asked for a push, and it was pushed: origin moved `277bd48 → a3cd9f5`, a fast-forward.
- The Phase 6 and Phase 7 reports still say "nothing was pushed". That was true when they were
  written.
- The GraphicsSettings modification is as described: formatting only, still uncommitted.

**Documentation paths differ from the brief:**
- The system docs live in `Documentation/Systems/` (`CharacterProgression.md`, `RulesEngine.md`,
  `Persistence.md`, `Characters.md`, `Classes.md`).
- The roadmap is `Documentation/Roadmap.md`.
- There is no `Documentation/Realmkeeper-Game-Design-Bible.md`. The Bible is
  `Documentation/Product/GameDesignBible.md`.
- I read all of them, the Phase 5–7 reports and pre-flights, and ADR-0001…0029.
- New Phase 8 docs follow the existing layout: `Systems/Combat.md` and
  `Systems/SettlementStructures.md`.

## 2. Design authority and the scope conflict

- **Still open.** `ProductFoundation.md` ("simulation and exploration platform") versus the Bible
  (full RPG) remains an owner decision.
- **The owner has now stated the intended direction:**
  - a persistent simulated world with a full RPG on top;
  - a custom protagonist;
  - an authored campaign and an emergent sandbox;
  - D&D-inspired rules;
  - solo play and companions;
  - first- and third-person exploration;
  - hybrid combat.
- **Phase 8's minimum change.** Add a short "direction confirmed by the owner (Phase 8 brief)" note
  to `ProductFoundation.md`, pointing to the Bible. No product-vision rewrite and no new
  worldbuilding.

## 3. Existing architecture relevant to Phase 8

| System | Where | Reuse in Phase 8 |
|---|---|---|
| Character sheet, rules, progression | `Domain/Rpg` (`CharacterSheetData`, `CharacterRules`, `CharacterVitals`, `CharacterValidation`) | The player combatant reads scores, saves, proficiencies, initiative, speed, resistances and roll modes from `CharacterRules`; HP changes go through `CharacterVitals` |
| Typed effects | `RuleEffect`, `EffectKind`, `EffectCondition`, `Loadout` | Combat consumes them: Advantage/Disadvantage targets, resistances, AC bonuses, speed |
| Items | `ItemDefinition` (`Protection`, `Damage`, `WeaponClass`, `TwoHanded`), `ItemCatalog` (30 wearables and weapons) | Extended in place with SRD armour and weapon data; IDs kept |
| Equipment | `PlayerCharacterData.Equipment` (8 slots) | The player's loadout and weapon in combat |
| NPCs | `NpcData` (role, tier, skills, `IsDeceased`, `DiedOn`); `NpcSystem.Die` (shops shut, faction leadership passes on) | Persistent NPC combatants keep their `NpcId`. A death in a lethal encounter reuses the same death routine, refactored to be shared. |
| Factions | `FactionData`, relations | The combatant's allegiance can carry a faction ID |
| Settlements | `SettlementData` (type: Hamlet / Village / Town / City / Fortress); `LocationData` (Residence, Shop, Workshop, Tavern, Market, Administration, Worship, GuardPost, Square) via `NpcGenesis.PlanLocations` | Structures build **on** locations, rather than duplicating them |
| Shops | `ShopData` (shop ID, location ID, keeper, categories, stock); `ShopSystem.Plan`: Farm Stall (hamlet); General Store and Tavern Kitchen (village); Outfitters, Smithy, Tavern Kitchen and Granary Yard (town and up) | Linked by ID from structures; unchanged |
| Exploration layout | `LocalEnvironmentLayout`: Buildings (House, Hall, Granary, Landmark) and props (Well, MarketStall …), sites from `SettlementSites` | Structures reference layout keys; signs are placed on those buildings |
| Strategic inspector | `Presentation/UI/RealmkeeperHud*.cs` | Gains a Structures list |
| Commands | `CommandContext.Edit()` (copy, validate, commit), `NextGuid(purpose)`, `ReportEvent` | Every combat mutation is a command |
| Randomness | `SeedMixer`, deterministic RNG (`_random.Create(seed)`) | Combat rolls: a counter-based stream per encounter (seed + roll index), stored in the encounter, so save/load and replays are exact |
| Persistence | JSON DTOs, mapper, migrations, `WorldValidator` | Schema v10 |

**Existing combat functionality.**
- **None.** There are no encounters, attacks or turns.
- **Combat-shaped data only:** typed `CombatRule` effects (sneak attack dice, rage …),
  `Resistance` and `SaveAdvantage` effects, and resources (rage, second wind).
- **`ItemDefinition.Damage`** is a single number used only as a "damage" figure on the Hub.

## 4. Current armour and AC

**`CharacterRules.ArmorClass(player)`**, labelled provisional:
- 10 + Dexterity;
- the best unarmoured defence if there is no Chest-slot armour;
- **plus the sum of `Protection` on every worn item**, including bracers, leggings, pauldrons, iron
  cap and shield;
- plus conditional `ArmorClassBonus` (Defense).

**Problems found:**
- There are no armour categories, so a Dexterity cap and heavy armour cannot be expressed.
- Pieces of armour stack, which SRD armour does not do: one suit plus one shield.
- No Strength requirement and no stealth disadvantage.
- No training check: anyone wearing anything gets its full benefit.
- `Loadout.HeavyArmor` is always false.

**Plan** (ADR):
- Give items explicit armour data. Mapped to SRD 5.2.1:

  | Item | SRD armour |
  |---|---|
  | `jerkin_leather` | Leather armour, light, 11 + Dex |
  | `jerkin_forester` | Studded leather, light, 12 + Dex |
  | `chain_shirt` | Chain shirt, medium, 13 + Dex (max 2) |
  | new `chain_mail` | Chain mail, heavy, 16, Strength 13, stealth disadvantage |
  | `shield_round`, `shield_buckler` | Shield, +2 |

- Bracers, leggings, pauldrons and the iron cap become accessories with no AC (the SRD has one suit
  of armour). They stay as items, visuals and trade goods.
- **AC is derived only by `CharacterRules`.** Untrained armour gives disadvantage on Strength and
  Dexterity D20 tests (SRD 5.2.1 "Armor Training"). An untrained shield gives no AC.
- **This changes existing AC numbers.** For example, the Phase 6 cleric's 17 (provisional) becomes
  10 + 1 (leather) + 1 (Dex) + 2 (shield) = 14 with leather armour. That is intended, and it is
  documented.

## 5. Rules configuration for Phase 8

- **The authority.** The existing configuration is rules version 7 (ADR-0022, ADR-0026). Classes,
  progression and feats come from SRD 5.2.1; species increases follow the SRD 5.1 style.
  - The only 5.1 content touching combat is the fighter's level-1 Fighting Style options (Dueling and
    Protection are SRD 5.1). These stay as typed data.
- **Combat rules follow SRD 5.2.1** (*Playing the Game* and the *Rules Glossary*). I verified these
  online for this pre-flight:
  - armour and armour training;
  - weapons and properties (including mastery names);
  - Unarmed Strike (damage, Grapple, Shove; DC 8 + Strength + proficiency);
  - conditions (including 5.2.1 Exhaustion: −2 × level to D20 tests, −5 × level ft speed, death at
    6);
  - opportunity attacks, Dash, Disengage, Dodge and Help;
  - critical hits (dice rolled twice);
  - resistance (halve) and vulnerability (double), each applied once.
- **Death saves** follow the standard 5.2.1 rules: DC 10; a natural 20 regains 1 HP; a natural 1
  counts as two failures; damage at 0 HP is one failure (two on a critical); damage that leaves a
  remainder at least the HP maximum kills outright. The glossary page I fetched did not quote these.
- **Creatures** (SRD 5.2.1 stat blocks, verified): Commoner, Bandit, Guard, Wolf.
- **Rules clarifications needing an ADR:**
  - who makes death saves: the player and named NPCs make them, while temporary creatures die at 0;
  - grid distance: 5-ft squares, diagonal = 5 ft;
  - reactions are taken automatically by policy (no prompt yet);
  - initiative tie-breaks;
  - ammunition is tracked as a property only (no arrow or bolt items exist yet).

## 6. Licensing and the shipped credits

- **The attribution was never added to the build.** The CC BY 4.0 statement appears only in
  `Documentation/Systems/Characters.md` and in code comments.
  - The README, the game UI and any NOTICE or credits file do not contain it.
  - The Phase 6 report said it "must appear in the shipped credits", but that was never done.
- **Phase 8 will:**
  - add `CREDITS.md` at the repository root;
  - add a Credits section in the game: the Player Hub's Info section, plus the strategic map's help;
  - add a credits line in the README.

## 7. Settlement and building architecture

- **No building or structure model exists.** What exists:
  - **`LocationData`:** persistent places with kinds and layout keys, generated by settlement type.
    - *Hamlet:* square, market, tavern, granary, watchtower.
    - *Village:* plus chapel, shop and smithy.
    - *Town and up:* plus keep, gatehouse, shop, smithy and shrine.
  - **Shops** on those locations.
- **Plan.** A structure catalog (definitions) plus `StructureData` instances owned by the settlement.
  - **Generation:** created once per settlement, deterministically.
  - **Links:** each instance links to its existing `LocationId`, `ShopId`, keeper NPC and faction
    where one exists.
  - **No duplicates:** existing shops and locations are never duplicated.
  - **New trades**, chosen by settlement size: bakery, butcher, carpenter, mason, stable, warehouse,
    barn, well, market stalls. They are informational or decorative, and each is labelled honestly.

## 8. Save compatibility

- **Schema v9 → v10:** add the world's `encounters` and `structures`, and the character's vital
  state (death saves, stable, dead).
- **Structures** come from a once-only genesis after load, as `CommerceGenesis` does.
- **Encounters** default to none.
- **Armour data** is catalog data, so nothing is saved.
- **To be tested** from Phase 5 (v7), Phase 6 (v8) and Phase 7 (v9) fixtures.

## 9. Risks and plan

| Risk | Mitigation |
|---|---|
| AC change alters existing characters' numbers | Intended (provisional AC replaced); documented; tests updated with the reason |
| NPC death from combat cascades (shops, factions) | Reuse the existing `NpcSystem` death routine (refactored to a shared helper), never a new path |
| Combat state duplicating HP | The player's HP stays on the sheet (one source of truth); NPC and creature HP live on the combatant |
| Encounter determinism across save/load | Counter-based rolls (seed, index) stored in the encounter |
| UI scope | A functional encounter panel, simple placeholder markers on the ground, no animation work |
| Starting an encounter in-game when no encounter generator exists | A clearly labelled **practice encounter** from the settlement HUD: bandits at the edge of town, a nearby guard joining as an ally. Honest test content, not a quest system. |

**Implementation order:** as the brief's stages 1–7:
1. Rules, armour and the ADRs.
2. The combat core.
3. Resolution.
4. The lifecycle and AI.
5. Persistence and UI.
6. Structures.
7. Verification and documentation.
