# Art assets: KayKit (CC0) installed, Bestiary kept local

Date: 2026-11-12. Branch `feature/phase-0-1-foundation`. The CC0 packs and the import rules are committed locally as `d975302`, which is **not pushed**; the branch is 1 commit ahead of `origin`. This report file is not committed yet.

## Summary

| Pack | Source | Licence | In the repository? | Status |
|---|---|---|---|---|
| KayKit Character Pack: Skeletons 1.1 (free) | kaylousberg.itch.io/kaykit-skeletons | CC0 1.0 | Yes | Installed; imports cleanly |
| KayKit Character Animations 1.1 (free) | kaylousberg.itch.io/kaykit-character-animations | CC0 1.0 | Yes | Installed, 5 of 8 sets; imports cleanly |
| Quaternius Bestiary – Dungeon Monsters Kit (free Standard) | quaternius.itch.io/bestiary-dungeon-monsters-kit | Quaternius Asset License (QAL) 1.0 | **No: git-ignored, this machine only** | Installed locally; 2 of 7 monsters in the free tier |
| Quaternius Ultimate Monsters | Google Drive, linked from quaternius.com | CC0 1.0 | — | **Not installed:** Drive refused the downloads |

**Nothing is used in game code yet.** Fights still show the placeholder capsules and boxes.

## KayKit: Skeletons 1.1 (CC0)

**Checks:**
- **Licence:** read from the pack's own `License.txt`: Creative Commons Zero, free for personal, educational and commercial use. Credit is optional; it is given in `CREDITS.md` anyway.
- **Download:** the free version, 8.2 MB zip, fetched into an isolated scratch folder and listed before anything was extracted. It contained no executables, scripts or symbolic links.

**Installed in `Assets/Realmkeeper/Art/ThirdParty/KayKit/Skeletons/`:**

| Folder | Files | Size |
|---|---|---|
| `Characters/` | `Skeleton_Warrior.fbx`, `Skeleton_Minion.fbx`, `Skeleton_Rogue.fbx`, `Skeleton_Mage.fbx` (rigged) | 1.9 MB |
| `Accessories/` | 13 props from the pack's Unity-ready folder: axe, blade, crossbow, quiver, staff, shields (large A/B, small A/B), arrows (whole, half, broken, broken half) | 0.5 MB |
| (root) | `skeleton_texture.png` (one shared gradient atlas, placed here so the characters and props both find it), `License.txt` | — |

**Not installed:** GLTF and OBJ copies, the pack's duplicate animation files (the animation pack below has them), the preview images, and three shortcut files (Discord, More, Patreon).

## KayKit: Character Animations 1.1 (CC0)

**Checks:**
- **Licence:** the pack's own `License.txt` says CC0.
- **Download:** the free version, 14.9 MB zip; same checks as above.

**Installed in `Assets/Realmkeeper/Art/ThirdParty/KayKit/CharacterAnimations/` (13 MB):**

| File | What it holds | Size |
|---|---|---|
| `Rig_Medium/Rig_Medium_General.fbx` | Idle, hit, death, spawn, interact | 2.4 MB |
| `Rig_Medium/Rig_Medium_MovementBasic.fbx` | Walk, run, jump | 1.8 MB |
| `Rig_Medium/Rig_Medium_CombatMelee.fbx` | One-handed, two-handed, unarmed, dual wield, block | 3.3 MB |
| `Rig_Medium/Rig_Medium_CombatRanged.fbx` | Bow, crossbow, throw, spellcasting | 3.1 MB |
| `Rig_Medium/Rig_Medium_Special.fbx` | Skeleton-specific idle, walk, hit, death, spawn | 2.5 MB |
| `License.txt` | — | — |

**Left out, to keep the repository smaller:** MovementAdvanced, Simulation and Tools (about 9 MB together; easy to add later), the Rig_Large animations (for KayKit's larger characters, which are in paid tiers), and the GLTF copies.

## Import rules (`Assets/Realmkeeper/Editor/ArtImportPostprocessor.cs`)

The project's import rule treated every unrecognised model under `Art/ThirdParty` as a humanoid body with no animation. On the first import this produced "Invalid Avatar Rig" errors for all 13 KayKit props, and it would have dropped the clips from the animation files. Two rules were added, and the version was bumped from 2 to 3 so Unity re-imports the art:

| Path | New setting |
|---|---|
| `…/KayKit/…/Accessories/` | No rig, no animation, standard materials (static props held in a hand) |
| `…/KayKit/CharacterAnimations/` | Humanoid rig, avatar from the file, animation imported, no materials |

The KayKit characters keep the existing default: humanoid, with an avatar created from the file. Because the characters and the animation sets are both humanoid, KayKit's melee and ranged clips can also be retargeted onto the current Quaternius player body.

## Quaternius Bestiary (QAL): local only

**What came in:**
- **Licence:** Quaternius Asset License 1.0, read on quaternius.com/license.html and in the pack's `License_Standard.txt`.
- **What it allows:** use in personal, educational and commercial products, with no credit required.
- **What it forbids (section 3a):** redistributing the asset files themselves, in any form. To respect that, the files are never committed.
- **The free tier is only part of the kit:** the store page describes 7 monsters, but the free Standard zip (47 MB) contains **2: the Imp and the Puglin.** The licence file says all 7 and the colour variations are in the paid Source version on quaternius.com.

**Installed in `Assets/Realmkeeper/Art/ThirdParty/QuaterniusBestiary/` (22 MB, this machine only):**
- `Models/Imp.fbx` and `Models/Puglin.fbx`;
- `Textures/`, for each monster: 3 colour variants, normal, ORM and emissive maps;
- `License.txt`.

The Godot/Unreal GLB exports, the Unreal normal maps and the preview images were left out.

**Kept out of git:** `.gitignore` excludes the folder and its `.meta`, with a comment giving the reason. `git check-ignore` confirms both paths, and neither appears in the commit.

**Credit:** `CREDITS.md` has a row marking Bestiary as local only, with where to download it.

**Consequence:** another machine that clones the repository will not have these two models. Any later code that uses them must fall back to the placeholder shapes when they are missing.

## Quaternius Ultimate Monsters: not installed

The pack (CC0, 50 animated monsters) is hosted on Google Drive. All 53 requested files came back as Drive's "Quota exceeded: too many users have viewed or downloaded this file recently" page instead of models. Nothing from it was kept; the error pages were deleted. A later attempt, or a manual download in a browser, may work.

## Verification

| Check | Result |
|---|---|
| Unity import (batch) | KayKit and Bestiary models, animations and textures import with no errors |
| Pre-existing warning | `Quaternius/ModularCharacterOutfitsFantasy/Models/Female_Peasant.fbx`: "Required human bone 'Head' not found". This outfit was already there; it was reported only because the version bump re-imported all third-party art |
| Unity compile | 0 errors, 0 warnings |
| Unity PlayMode (after the KayKit import, before Bestiary) | 119 tests: 98 passed, 0 failed, 21 skipped (capture tests) |
| `.gitignore` | Bestiary folder and `.meta` ignored, confirmed with `git check-ignore` |

**What was not re-run after Bestiary was added:** the .NET and EditMode suites. No code changed after the import rules, and Bestiary itself is only art files.

## Other changes in this period

- **Pushed earlier:** Phase 14 (`242fde2`) and the combat overhaul (`08c222e`) are on GitHub (`cda9da0..08c222e`).
- **Out of date:** the Phase 14 and combat overhaul reports, and the roadmap, still say "not pushed". That wording is due for correction in the next documentation update.
- **Unchanged:** the owner's `ProjectSettings/GraphicsSettings.asset` change is still unstaged.

## Commit

`d975302` "Art: KayKit Skeletons and Character Animations (CC0); import rules; Bestiary kept local". 59 files, including Unity `.meta` files: the KayKit packs, `ArtImportPostprocessor.cs`, `CREDITS.md` and `.gitignore`. Not pushed.

## Suggested next steps

1. **Put the skeletons into fights:** a KayKit skeleton body for skeleton and zombie creatures in place of the capsules, driven by the Special set, with the props from Accessories attached to the hand bones.
2. **Use the Bestiary models for goblin-like creatures** where they are present, with the placeholder as the fallback.
3. **Wire attack, hit and death animations** from the KayKit and Universal Animation Library clips into the Animator, keyed to the combat log as the procedural feedback already is.
4. **Optionally:** retry Ultimate Monsters, or buy the Bestiary Source tier for all 7 monsters.
