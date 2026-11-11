# Phase 6 pre-flight: Character Creation & Core RPG Foundation

Written 2026-11-10, before any Phase 6 code.

## Repository

| Check | Result |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| HEAD | `1f37bb6` Phase 5 (present in the history) |
| Origin | `277bd48`; the branch is 1 commit ahead and **not pushed** |
| Uncommitted | `ProjectSettings/GraphicsSettings.asset` only |
| Unity editor | Closed, so batch runs are possible |
| Save schema | v7 |
| Baseline | .NET 346, Unity EditMode 346, PlayMode 57 (Phase 5 report) |

**The uncommitted GraphicsSettings change.** It is Unity re-serialising three references onto one
line each. The GUIDs and values are identical, so it is formatting only. It is left untouched and
will stay out of the Phase 6 commit, as in Phase 5.

The repository matches the expected baseline.

## Design authority

- The **Game Design Bible** was supplied with this phase's brief. It is not yet in the repository,
  so it will be added as `Documentation/Product/GameDesignBible.md` with an implementation-status
  section.
- **A conflict to note, not to resolve here:**
  - `Documentation/Product/ProductFoundation.md` (binding) describes "a persistent fantasy world
    simulation and exploration platform".
  - The Bible describes a full RPG with combat, magic and companions.
  - The two agree on persistence, determinism, local play and optional AI. The RPG scope extends
    the product definition.
  - Phase 6 implements only what this phase's brief authorises. Reconciling the two documents is
    an owner decision, recorded here and in the report.

## What exists and will be extended

| Area | State | Phase 6 use |
|---|---|---|
| `PlayerCharacterData` | Persistent player: ID, name, location, pose; Phase 5 adds species text ("Human"), age, free-text background ("Wanderer"), description, appearance, purse, fed, inventory, equipment, `IsOutfitted` | Extended with one RPG sub-record (`Rpg`). No parallel player. |
| Appearance | `PlayerAppearanceData`, limited to renderable options; `PlayerBodyBuilder`; `CharacterPreview` (render texture, turn and zoom) | Reused in the creator. A species body configuration contract is added. |
| Items | `ItemCatalog` (30 definitions), stacks with one owner, 8 slots, equip rules in `EquipHandler` | Extended with the class and background gear the starting kits need (weapons, one armour, tools, focuses). The equip logic is reused. |
| Commands | Typed, validated, atomic (`CommandContext.Edit` → whole-world validation → commit) | New `CreateCharacterCommand`, a single transaction |
| Validation | `WorldValidator` (+ `.Commerce`) | New `.Character` rules |
| Persistence | DTOs, mapper partials, `MigrationV6ToV7`, post-load genesis | Schema v8 and `MigrationV7ToV8` |
| Player Hub | 8 sections; the Character section is a short profile | Becomes the character sheet; the 8 sections stay |
| Chronicle | Structured event types (last: `PlayerSale` = 51) | Adds `PlayerCharacterCreated` |

**The Phase 5 starting kit.** New worlds and migrated saves give the player traveller's clothes, a
knife, bread and 150 coin. Phase 6 keeps this as **the clothes on your back** for everyone. Class
and background starting equipment is *added* once, at creation, so nothing is duplicated and the
Phase 5 tests keep their meaning.

## Licensing

Verified on 2026-11-10.

- **SRD 5.2.1** is released by Wizards of the Coast under **CC BY 4.0**. Use requires a fixed
  attribution statement and no other attribution to Wizards. "Compatible with fifth edition" is the
  permitted description. "Dungeons & Dragons" is a trademark and is not used in the product.
- **SRD 5.1** is also under CC BY 4.0.
- **Species coverage:**
  - SRD 5.2.1: Dragonborn, Dwarf, Elf (High and Wood lineages), Gnome, Goliath, Halfling, Human,
    Orc, Tiefling.
  - SRD 5.1: Half-Orc.
  - **Not in any SRD:** Aasimar, which Wizards' SRD page names as excluded. It is defined as
    **original Realmkeeper content** (its own text and mechanics), and the name is flagged for the
    owner's IP review.
- **Background coverage:**
  - SRD 5.2.1: Acolyte, Criminal, Sage (= Scholar), Soldier.
  - The other eight (Noble, Artisan, Merchant, Outlander, Sailor, Entertainer, Hermit, Folk Hero)
    are **original Realmkeeper definitions**.
- **Classes:** all twelve are in both SRDs. Phase 6 uses only level-one mechanics (hit die,
  proficiencies, level-one features and resources), written in Realmkeeper's own words.
- **Not copied:** published lore, setting names, subclasses beyond references, spells and artwork.
  All descriptions and lore are original.
- **Provenance:** every definition records its source: `SRD 5.2.1`, `SRD 5.1` or
  `Realmkeeper original`.

## Decisions

1. **One player.** RPG data is a sub-record of `PlayerCharacterData`. Derived values are computed,
   never stored:
   - ability modifiers;
   - skill and save bonuses;
   - proficiency bonus;
   - maximum hit points;
   - armour class;
   - speed;
   - proficiencies and languages.
2. **No new assemblies.**
   - Rules live in the engine-free Domain (`Realmkeeper.Domain.Rpg`).
   - Creation lives in Application.
   - The UI lives in the existing exploration presentation.
   - The current boundaries hold all of it cleanly, and a new assembly would add references
     without adding isolation.
3. **The rules configuration is 5e-compatible with species ability bonuses (the SRD 5.1 style).**
   - Point buy is 27 points over 8–15; the standard array is 15/14/13/12/10/8.
   - The modifier is `floor((score − 10) / 2)` and the proficiency bonus is +2 at level 1.
   - Level-1 hit points are the hit die's maximum plus the Constitution modifier, at least 1.
   - SRD 5.2.1's background-based increases are recorded as an alternative configuration, not
     implemented.
4. **Typed effects.** Species traits, background features and class features are lists of typed
   effects (`RuleEffect`): proficiencies, senses, resistances, speed, hit points, unarmoured
   defence, resources, choices, and combat rules kept as data for Phase 8. One evaluator applies
   them. There are no per-species conditionals.
5. **Appearance.**
   - Only Human has finished art.
   - Every other species is offered with an **honest placeholder**: the human body at the species'
     height, plainly labelled "Placeholder body: final model pending".
   - The data contract (body set, head set, height, features) is in place for the visual
     production phase.
6. **Existing saves.**
   - v7 players migrate to `CreationState = Legacy`: nothing assigned, nothing invented, and
     identity, looks, belongings, purse and location unchanged.
   - New worlds start `Pending`.
   - Both are prompted to create their character.
   - Creation grants the class and background kit **once** (`StartingEquipmentGranted`) and cannot
     be repeated.
7. **Where creation happens.** The creator opens on entering a settlement while creation is pending
   or legacy, and from the Hub's Character section. It reuses the existing preview and window
   styling.

   **Changed during implementation.** An automatically opening, input-capturing window would
   interrupt every existing exploration flow, including 54 PlayMode tests that walk, trade and
   talk on entry. The creator is therefore **offered, not forced**:
   - a HUD button, "Create character (C)", and the C key;
   - a banner in the Hub's Character section.

   Pending and legacy players play normally until they choose. See `Systems/CharacterCreation.md`.
