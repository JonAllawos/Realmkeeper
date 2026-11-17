KITBASH3D TRIAL SCENE (evaluation only)

Project: Realmkeeper
Engine: Unity 6 (6000.0.47f1) / URP
Branch: feature/phase-0-1-foundation (or a branch the owner names)
Roadmap place: outside the approved M1–M9 order. It informs M8 (visual polish) and can run whenever the owner chooses; it does not replace M1.2.
Companion brief: Visual-Quality-Pass-Brief.md. The two answer the same question ("how much better can the game look, and at what cost?") from different directions, and their reports are read side by side.

Status: written by Claude 2026-11-16 at the owner's request. Not started. Begins only on the owner's go-ahead, after the owner has bought a kit (Claude does not buy anything).

⸻

PRIMARY OBJECTIVE

Find out, with screenshots and numbers, whether KitBash3D art can live in Realmkeeper:
- Does it look right beside the current art, or only on its own?
- What does it cost in draw calls, triangles, memory, disk and frame time against the agreed budget?
- How much work is it to bring one kit into the shape the game needs (URP materials, LODs, colliders)?

The deliverable is a decision, not a feature. Nothing from the trial reaches the game's scenes or the build list.

⸻

WHAT IS ALREADY DECIDED (do not reopen)

- Budget (Graphics Pass, owner 2026-11-10): 60 fps at 1080p on a GTX 1660-class GPU, at most 1,500 draw calls in a town.
- Art direction: warm stylized realism. The current town art is the Quaternius CC0 kits plus AmbientCG textures; the Graphics Pass measured at most 955 draw calls and 2.76 M triangles in a town.
- URP only. No pipeline change.
- Owned and licence-clear assets first; every licence recorded in an asset register.

⸻

RULES

1. Evaluation only. No change to the layout code, the building builders, the simulation, saves or any test.
2. The trial lives in its own scene, Assets/Realmkeeper/Scenes/Trials/KitBash3DTrial.unity, which is not in the build list.
3. The kit's files are not committed until the owner decides (see §1). Only the trial scene, the scripts that build it, and the report are committed.
4. Do not claim visual quality without screenshots, and do not claim development-build numbers that were not measured.
5. Preserve the uncommitted M_LampGlow.mat keyword, the Unity MCP lines in Packages/, and the untracked Assets/InitTestScene* scenes.
6. Never run the .NET and Unity suites in parallel. Ask before running a full suite.
7. Commit locally only after verification. Do not push. Stop after the report.

⸻

0. OWNER STEPS BEFORE CLAUDE STARTS

- Choose one kit whose subject is close to a Realmkeeper town (medieval or fantasy buildings). A kit that is far off-subject tells us little.
- Read the kit's current licence. Record in the report, in your words: game use allowed, redistribution of the source files forbidden or not, seat or team limits.
- Download it in its FBX (or Unity) format and put it outside the repo, for example C:\Dev\KitBash3D\<kit>. Tell Claude the path.

⸻

1. WHERE THE FILES GO (the repo question)

The repo has no Git LFS and already carries about 526 MB of third-party art. One KitBash3D kit can be several gigabytes of textures, and its licence may not allow the raw files to be stored where others can read them.

- During the trial: import into Assets/Realmkeeper/Art/ThirdParty/KitBash3D/, and add that folder to .gitignore (with its .meta) before the first import, so nothing is staged by accident.
- In the report: the kit's size on disk raw, after import, and after the texture limits in §3, and a recommendation for adoption: Git LFS, keeping it outside git with a setup step, or reduced textures only.

⸻

2. THE TRIAL SCENE

Build it with an editor script (Realmkeeper ▸ Trials ▸ Build KitBash3D Trial), so it can be rebuilt after reimports:
- Left half: a street of the existing town art, made by the existing builders from a fixed seed and layout.
- Right half: the same street footprint dressed with KitBash3D pieces placed by hand or by the script, at the same building widths, heights and spacing.
- The same lighting as town exploration: the exploration atmosphere, fog, sun angle and post-processing profile.
- Fixed cameras matching the Graphics Pass views where they make sense: gate, street, building, plaza, overview. Add one camera that sees both halves together.

⸻

3. BRINGING THE KIT INTO SHAPE

Record the time and the steps each item takes; that effort is part of the answer.
- Materials: convert to URP Lit, keeping albedo, normal, and roughness/metallic (packed into URP's mask layout). Note anything that does not convert (special shaders, decals, emissives).
- Scale and pivots: in metres, standing on the ground, matching a 1.8 m character.
- Textures: try 4K, 2K and 1K limits, and record the visible difference at 1080p and the memory saved.
- LODs: use the kit's LODs if it has any. If not, try two reductions made outside Unity (for example Blender's Decimate) for the two heaviest pieces only, and report how long that took. Unity 6.0 has no built-in mesh LOD generation.
- Colliders: box colliders per piece, as the Graphics Pass rule asks, not mesh colliders.
- Batching: static batching or GPU instancing for repeated pieces, and the material count before and after merging shared materials.

⸻

4. MEASUREMENTS

For each camera, for the existing art and for KitBash3D (raw, then after §3):
- draw calls, set-pass calls, triangles, renderers in view;
- texture and mesh memory;
- frame mean and p95 in the editor; and in a Windows development build through the existing -rkPerfProbe, if the probe can be pointed at the trial scene with a small addition (otherwise say it was not measured);
- import time and size on disk.

Compare against the 1,500 draw-call budget. Do not estimate GTX 1660 numbers as if measured; label any scaling as an estimate.

⸻

5. VISUAL EVIDENCE

- Matching screenshots per camera, at 1920×1080 and 2560×1440, in Documentation/Screenshots/KitBash3D-Trial/.
- One pair that puts both styles in the same frame, which is the real test of mixing.
- The same views under the overcast and rain weather, since the weather tints and wets materials.

⸻

6. REPORT AND RECOMMENDATION

Documentation/Reports/KitBash3D-Trial-Report.md with:
- the kit, its licence summary, its sizes;
- the measurements table and the screenshots;
- the effort per piece to bring it into shape;
- one recommendation, with its reason:
  (a) adopt KitBash3D as the town style everywhere (and what that would replace and cost);
  (b) use it for a few hero pieces only (a castle, a cathedral) if they sit well beside the current art;
  (c) do not adopt.
- what the owner should look at before deciding.

Also add the kit to an asset register (Documentation/Assets/) whatever the result, so the licence is on file.

⸻

STOP

Stop after the report. Do not dress the game's real towns, do not change any builder, and do not start the visual-quality pass.
