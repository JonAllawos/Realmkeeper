# ADR-0010: Third-party art: CC0 only, used subset in the repo, licences on file

**Status.** Accepted, 2026-11-10 (graphics pass).

**Context.** The exploration view was built from Unity primitives. The graphics pass needs real
buildings, characters, animation and ground materials. The owner chose a mix of procedural
assembly and licence-clear art ("Mix"), allowed any package with an ADR, ruled out purchases
in this pass, and chose to commit only the art that is used.

**Decision.**

1. **Only CC0 1.0 art is incorporated.** Every pack's licence is read from its own download
   (or, for ambientCG, from the site's licence page) before the files enter the repo, and a
   copy of the licence sits next to the files.
2. **Sources** (all CC0 1.0):
   - Quaternius *Medieval Village MegaKit* (Standard): modular walls, roofs, doors, windows, props.
   - Quaternius *Universal Base Characters* (Standard): heads, eyes, brows, hair, beards.
   - Quaternius *Modular Character Outfits – Fantasy* (Standard): clothed bodies (peasant, ranger).
   - Quaternius *Universal Animation Library* (Standard): humanoid clips.
   - ambientCG: Grass004, Ground037, Ground054, PavingStones070, Planks021, Wood049.
3. **Only the used subset is committed**, under `Assets/Realmkeeper/Art/ThirdParty/<Author>/<Pack>`:
   about 80 MB. Textures are re-encoded at 1024 px (outfit colour maps at 2048), and roughness
   and ORM maps are not used. Original downloads are not committed.
4. **The inventory is the record.** `Documentation/Art/AssetInventory.md` lists every pack,
   its URL, licence, what was taken and how it was modified.
5. **Purchases are recommend-only.** A paid asset may be suggested in a report, with its
   price and licence, but nothing is bought and nothing depends on one.
6. **No packages were needed.** Animation uses the built-in Animator with Mecanim humanoid
   retargeting, and rendering stays URP Lit. No ProBuilder, Cinemachine, Shader Graph or
   Addressables were added.

**Consequences.** The art is free to ship and modify. The look is consistent because the packs
come from one artist. The Standard tiers are limited: two outfits per body frame, a few
hairstyles, and no turn-in-place clip (see ADR-0012). An upgrade to the paid Source tiers
would need the owner's decision and a licence record.

**Alternatives considered.** Making the art by hand: too slow for this pass. Paid kits:
ruled out by the owner for this pass. Committing whole packs: about 600 MB for little gain.
