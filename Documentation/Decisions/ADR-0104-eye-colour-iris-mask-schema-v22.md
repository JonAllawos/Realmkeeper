# ADR-0104: Eye colour through an iris mask (schema v22)

**Status.** Accepted, 2026-11-12 (Phase 20A).

**Context.** The CC0 Quaternius head paints iris, whites and the skin around the eye in one texture (`T_Eye_Brown.png`, 256²). A material tint would colour all three, which is why Phase 20 left eye colour out.

**Decision.**
1. **The iris mask.** `Tools/Art/make_iris_mask.py` derives an iris mask from the CC0 texture and writes `T_Eye_IrisMask.png` (under `Art/ThirdParty/Quaternius/UniversalBaseCharacters/Resources/RealmkeeperEyes/`).
   - **What it holds:** the texture's own colours, with the iris weight in alpha. The weight is high for saturated, warm pixels inside the measured iris disc; the pupil, highlight, whites and skin are 0.
   - **Licence:** a derivative of a CC0 work, made by the project. The script reproduces it byte for byte.
2. **Runtime.** `EyeColours.For(colour)` builds each colour's texture once from the readable mask. The iris keeps its own shading and takes the new hue. `HumanoidCharacter` gives the head's `M_Eyes` slots that texture through a property block, so the shared material is untouched.
3. **The palette.** `PlayerAppearanceOptions.EyePalette` has ten colours. Index 0, brown, is the texture as drawn and is never recoloured. Every species may use every colour. Generated residents keep 0, so no existing person's look changes.
4. **The data.** `PlayerAppearanceData.EyeColour` is validated by `PlayerAppearanceOptions.Check`, chosen in the creator's Appearance step, and part of the look's signature, so the body rebuilds only when it changes.
5. **Schema v22.** `MigrationV21ToV22` is structural and writes nothing:
   - an absent `eyeColour` reads as 0;
   - an absent turn flag `lastHitBoonUsed` (ADR-0103) reads as false.
6. **Fixed on the way.** The hub's copy of the player's appearance dropped height and build field by field, so the body in the world was always average while the creator showed the choice. The hub now clones the whole record.

**Consequences.** Earlier saves load with the eyes they had. The mask must stay readable: a test checks the meta. If it is missing, the eyes stay brown and a warning is logged.

**Alternatives.**
- A custom shader with a mask input: rejected, because it would add a shader variant to URP for one effect.
- Separate per-colour textures in the repository: rejected, because the runtime build is about 0.25 MB per colour used and costs nothing for unused ones.
