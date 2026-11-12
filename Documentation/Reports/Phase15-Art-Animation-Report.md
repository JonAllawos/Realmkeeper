# Phase 15 (art addendum): character models, animation and combat presentation

Date: 2026-11-12. Branch `feature/phase-0-1-foundation`, on top of `4324084`. Committed locally together with Phase 15 (see `Phase15-Completion-Report.md`); **not pushed**.

**Scope.** This covers the "Additional Phase 15 requirements: art assets, animations and combat presentation". The main Phase 15 brief (companions, parties) has not been supplied, so the parts that depend on it are **not done**:
- companions using locomotion while following;
- animated party members fighting alongside the player.

The shared animation system below is what they would use.

## A. Asset audit (verified in Unity, `Realmkeeper.Editor.ArtAudit`)

| Asset | Location | Licence | State verified |
|---|---|---|---|
| KayKit Skeletons 1.1 | `Art/ThirdParty/KayKit/Skeletons/` | CC0 | Present. The Warrior, Minion, Rogue and Mage import as **valid humanoid avatars**, with bones `hand.l/r` and `handslot.l/r`. They are 1.05–1.34 m tall as made. 13 accessories import as static props (no rig). |
| KayKit Character Animations 1.1 | `Art/ThirdParty/KayKit/CharacterAnimations/Rig_Medium/` | CC0 | 5 files, valid humanoid avatars, 83 clips. Every clip was marked non-looping; this is fixed below. |
| Universal Animation Library | `Art/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx` | CC0 | 43 clips, humanoid, valid. It includes `Sword_Attack`, `Punch_Jab`, `Hit_Chest`, `Hit_Head`, `Death01`, `Spell_Simple_Shoot`, `Pistol_Shoot`, `Interact`, `Jump_Start`, `Idle_Loop`, `Walk_Loop` and `Jog_Fwd_Loop` (the earlier list was accurate and is a subset). |
| Quaternius Bestiary (local only) | `Art/ThirdParty/QuaterniusBestiary/` | QAL 1.0 | On this machine only. The Imp (1.78 m) and the Puglin (1.10 m) have valid humanoid avatars, bones `hand_l/r`, and textures. Git-ignored; see H. |
| Ultimate Monsters | — | — | **Not installed** (the earlier Google Drive download failed). Nothing assumes it. |
| Import rules | `Editor/ArtImportPostprocessor.cs` | — | Version 3 kept. The KayKit accessories are still rigless (the import shows no "Invalid Avatar Rig" errors), and the animation sets still import their clips. |

## B. Character models in fights

Bodies are chosen in `CombatantBodies` (Presentation) by what the creature is and carries. Statistics, behaviour and rules are untouched.

| Creature | Body | Held | Notes |
|---|---|---|---|
| skeleton (shortsword, shortbow) | KayKit **Skeleton_Warrior** (2 in 3) or **Skeleton_Rogue** (1 in 3, per combatant), scaled ×1.3 | `Skeleton_Blade` in `handslot.r` (its shortsword) | Different skeletons look different |
| zombie (slam) | KayKit **Skeleton_Minion**, ×1.3, with a sickly green tint (material property block; no material copies) | nothing (it fights with slams) | The brief's "weaker undead" |
| goblin | **Puglin**, from the local Bestiary when installed | — | A small, armed dungeon raider, the closest fit. Without the local Bestiary: the placeholder |
| bandit, highwayman, bandit captain, cultist, cult fanatic, veteran, guard, acolyte, commoner, named people | The towns' Quaternius humanoid bodies, with a deterministic appearance per combatant | their equipment as the body shows it | |
| beasts and anything else | the placeholder (capsule or box), as before | — | |

**Not used, and why:**
- **Skeleton_Mage:** no undead caster exists among the creatures.
- **Imp:** no imp creature exists; relabelling it would be arbitrary.
- **The shortbow:** KayKit has no bow, so a skeleton's shortbow isn't shown. Its bow attacks still play the bow motion.

**Fallbacks:**
- A missing catalog, model or prop returns null, and the marker keeps its placeholder shape.
- Props attach only if both the prop and the slot bone exist.

## C. Animation architecture

**One shared set-up**, built by `Editor/CharacterAnimationBuilder` (idempotent; menu *Realmkeeper ▸ Build Character Animation*):

| Asset | Contents |
|---|---|
| `Art/Characters/Combatant.controller` | Locomotion: a speed blend tree (idle 0, walk 1.5, run 4.5 m/s). One-shot states: Attack1H, Attack2H, AttackUnarmed, AttackRanged (crossbow, thrown), AttackBow, Cast, Block, Hit, Interact, Jump, each returning to locomotion. Death enters from any state when `Dead` is set, and leaves only when `Dead` is cleared. No one-shot can start while dead. |
| `SkeletonCombatant.overrideController` | The skeletons' own idle, walk and death (KayKit Special set) |
| `PersonCombatant.overrideController` | The Universal Animation Library clips made for the Quaternius bodies: idle, walk, jog, sword attack, punch, pistol shot (crossbow and thrown), spellcast, hit, death, interact, jump. 2H, bow and block have no library clip and stay KayKit (retargeted). |
| Loop flags | The importer now loops the KayKit idles, walks, runs, blocking and aim idles |

**At runtime:**
- `CombatantAnimator` is the single interface for every body. Parameter hashes are computed once; only parameters the controller actually has are written. Every motion is recorded, for tests.
- **Death holds:** once dead, `Play` refuses and pending triggers are cleared.
- **Driven by real events only:** `ExplorationController.ShowCombatFeedback` reads each new combat log entry once, by its sequence number:

| Log entry | Motion |
|---|---|
| Attack | chosen from the weapon the line names and the **combatants' squares** (more than 5 ft apart, or a bow, crossbow, javelin or dart = ranged; slam, bite, claw, punch = unarmed; great-, maul, glaive, halberd, pike, quarterstaff = two-handed; otherwise one-handed) |
| Spell, only when the engine cast it | Cast on the caster; that caster's spell-attack lines in the same batch don't also swing a weapon |
| Applied damage | Hit on the victim |
| Down or death | Dead |
| A miss against a shield-bearer | Block |

- **Walking:** markers walk their squares, and their speed drives locomotion; they return to idle when they stop. The player's speed is measured from its body, as before.
- **The player's body** switches to the people controller for a fight and back to its exploration controller afterwards.
- **No replay:** refreshing the panels, opening the log or grid, inspecting a combatant, or focusing the camera replays nothing (tested).

**Interruption rules (tested where marked):**
- A rejected spell plays nothing: only cast log entries trigger Cast (tested: choosing a spell to aim plays nothing).
- Hits are one-shots that return to locomotion.
- Nothing plays over death, and idle never replaces it (tested after 30 frames).
- Animation never changes a result: motions are chosen after the engine has decided, and no animation event changes state.

**Two bugs found and fixed during verification:**
1. Melee attacks played the ranged motion. The range came from where the bodies were drawn mid-walk; it now comes from the engine's squares. A test now checks that an adjacent sword is never ranged.
2. A spell's cast entry, which can name no target, played nothing. A test now checks it.

## D. Exploration

- `Locomotion.controller` (the player and the towns' people) gains Jump and Interact states, using the Universal Animation Library's `Jump_Start` and `Interact`. The player's body jumps when the player jumps, and reaches out when pressing E on something. Tested by animator state.
- **Changed behaviour:** free exploration movement is no longer slowed by load, hunger or species pace. `Player.SpeedMultiplier` is now always 1. The hub still shows the rules' speed and why, and fights still use the rules' speed.
  - Before, Phases 5 and 11 slowed exploration walking by carried weight and hunger. The addendum requires that walking outside fights is not restricted by them.
  - Tested: carrying 480 lb, the hub's speed factor falls below 1 while free movement stays at 1.
- **Companion following:** not done (see Scope). Freed captives in places already walk with the people's locomotion animation (`PlaceWalker` and the driver).

## E. Rig and equipment validation

| Check | Result |
|---|---|
| Avatars | All characters and animation files are valid humanoids (audit) |
| KayKit clips on KayKit and Bestiary bodies | Correct (captures 05, 06, 08, 09; the staged Puglin in 02) |
| KayKit clips on the Quaternius bodies | **Poor:** the people stood with arms held out stiffly (seen in a capture). As the brief requires, the rig-appropriate set is used instead: the Universal Animation Library through `PersonCombatant`. Re-captured afterwards: natural stance (capture 03). |
| Root motion | Off on every combat body (`applyRootMotion = false`). The engine places bodies, and walking is driven by the movement code, so clips never move them. |
| Foot sliding | Walk-blend thresholds match the marker walk speed (3.2 m/s moves at the walk-to-run blend). Not measured frame by frame; small sliding may remain. |
| Weapons | The blade sits in `handslot.r` (tested by hierarchy; visible in captures 05 and 06). Orientation is the pack's own slot alignment, unadjusted. |
| Scale | Skeletons ×1.3 (≈1.37–1.74 m); the Puglin ×1 (1.10 m, a small goblin) |
| Rendering | KayKit uses its atlas through standard import. The Puglin uses a local URP Lit material (base colour and normal map), created in the ignored folder. |

## F. Performance

Windowed editor, 1707×875, town scene (`Phase15ArtCaptureTests.Capture_Phase15Performance`):

| Measure | Result |
|---|---|
| Town, no extra bodies | 7.08 ms/frame |
| With 36 animated bodies (24 skeletons and zombies, 12 people; walking, and attacking every 40 frames) | 8.05 ms/frame, about +1 ms |
| Building the 36 bodies | 22 ms in total |

- **Resource use:** the controllers are shared assets (one combat controller and two override controllers); no animator is created per frame; parameter hashes are static; animation assets are never loaded during play; the zombie tint uses property blocks.
- **Repository size:** no new packs; no GLTF/OBJ copies, previews or shortcuts.

## G. Visual verification (all automated captures; no manual play-through was done by me)

In `Documentation/Screenshots/Phase15/` (JPEG), with captions in `p15_capture_log.txt`:

| Shot | Shows |
|---|---|
| 01 | The player's body in its jump state, in town |
| 02 | **Staged:** this machine's Puglin beside the placeholder a clone without the Bestiary shows (`Create` returned null) |
| 03 | A practice fight: the bandits, cultist and guard in humanoid bodies, standing naturally |
| 04 `caster_spell` | The Cult Fanatic in its cast state (Hold Person) |
| 04 `player_spell` | A wizard player in the cast state (Fire Bolt) |
| 05 | The crypt: Skeleton_Warrior with its blade, and Skeleton_Minion as the zombie |
| 06 | A skeleton's one-handed attack (a miss, "Miss" floating) |
| 07 | The player's one-handed attack |
| 08 | A skeleton's hit state |
| 09 | A skeleton's death, held |

**Not captured:**
- **Recordings:** no video was made.
- **Companion locomotion and party members fighting:** not built (see Scope).
- **A skeleton's ranged shot:** the crypt fight was all melee.
- **A clone without the Bestiary:** shown only staged (02) and tested (`WithoutTheLocalBestiary_BodiesFallBack_WithoutErrors`).
- **Crypt lighting:** the crypt is dark, so the skeletons are visible but small in 05 and 06.

**Owner check before relying on it:** these show states and poses at a moment. Smoothness of transitions, foot sliding and weapon angle are best judged by playing (*Practice fight (F)* in town, or the crypt).

## H. Git safeguards

- `QuaterniusBestiary/` and its `.meta` stay git-ignored. `git status --ignored` shows them as ignored, including the locally generated `Resources/RealmkeeperLocalCreatureModels.asset` and `Materials/M_Puglin.mat`.
- No committed asset references a Bestiary GUID: all 21 GUIDs were checked against the catalog, controllers, runtime, editor and scenes.
- The runtime finds local models only through `Resources.Load` of an asset inside the ignored folder. On a clone, the load returns null: no missing references, no compile dependency, no exception (tested with `LocalCreatureModels.ForceMissing`).
- Nothing downloaded; no new third-party assets; `CREDITS.md` unchanged (no new assets).

## Tests

| Suite | Result |
|---|---|
| .NET | 691 / 691 passed, 0 warnings |
| Unity compile | 0 errors, 0 warnings |
| Unity PlayMode | 129 tests: **104 passed, 0 failed, 25 skipped** (capture tests) |
| Unity EditMode | **691 / 691 passed**, 0 compile warnings |

**New: `Phase15ArtPlayModeTests` (7):**
- the dead fight in their bodies with what they carry, and their motions really play: model per creature, blade in the slot, humanoid avatar, idle state; hit, attack and the player's attack observed in animator states; adjacent swords never ranged; no replay on refresh; death holds;
- people fight in humanoid bodies, and the player's exploration controller comes back;
- a wizard's Fire Bolt plays the cast state, only when cast;
- exploration jump and interact play, and walking is never slowed by load;
- without the local Bestiary, bodies fall back without errors;
- the motion follows the weapon named and the range.

**New captures:** `Phase15ArtCaptureTests` (4 capture tests, including the performance measurement).

## Files

| Kind | Files |
|---|---|
| New runtime | `CombatantAnimator.cs`, `CombatantBodies.cs`, `LocalCreatureModels.cs`, `ExplorationController.Animation.cs` |
| Changed runtime | `ArtCatalog.cs` (creatures, props, three controllers), `ExplorationController.Combat.cs` (real bodies, death state, `FaceCamera` target), `ExplorationController.CombatWorld.cs` (motions from the log, walking speed, ranged from squares, cast entries), `HumanoidCharacter.cs` (Jump and Interact on the driver), `PlayerCharacter.cs` (jump motion), `ExplorationController.cs` (interact motion), `ExplorationController.Living.cs` (free movement unrestricted), `Application/CombatQueries.cs` (`CombatantView.CreatureId`) |
| Editor | `ArtAudit.cs`, `CharacterAnimationBuilder.cs` |
| Assets | `Art/Characters/Combatant.controller`, `SkeletonCombatant.overrideController`, `PersonCombatant.overrideController`; `Locomotion.controller` (Jump, Interact); `RealmkeeperArtCatalog.asset`; KayKit animation `.fbx.meta` (loop flags) |
| Tests | `Phase15ArtPlayModeTests.cs`, `Phase15ArtCaptureTests.cs` |
| Docs | this report, `Documentation/Screenshots/Phase15/` |

## Known limitations

- **Two-handed, bow and block on human bodies** are KayKit clips retargeted onto the Quaternius rig; the library has no equivalent. They may look less natural than the rest.
- **No hand-adjusted weapon angles** for the skeletons, no bow prop, and the player's equipment display is unchanged.
- **Placeholders remain** for beasts, ogres and trolls.
- **Companions and party** are not done (no brief yet).
- **No video recordings.**

## Next steps

1. With the main Phase 15 brief: companions and party members use `CombatantBodies` and `CombatantAnimator` (people controller), following with the measured-speed locomotion they already get.
2. Hand-tune prop rotations per slot, and add bow and quiver props if a CC0 source exists.
3. Bodies for beasts and giants from a licence-checked CC0 pack (none is installed).
