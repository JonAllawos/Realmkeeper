# ADR-0017: Equipment is shown from world data with the outfit kit's own parts and procedural props

**Status.** Accepted, 2026-11-10 (Phase 5).

**Context.**
- Equipping must visibly change the character, in the street and in the Hub preview.
- The CC0 character packs provide two outfit sets (peasant, ranger) and two texture variants each.
- They have no weapons, shields or hats.
- The modular outfit parts in the source download are also present, as separate meshes, inside the
  outfit models already imported, so no new art import is needed.

**Decision.**

1. **The body is a function of world data.** `PlayerLook`, built from the hub snapshot (appearance
   plus the item in each slot), is the only input to `PlayerBodyBuilder`. It is rebuilt whenever the
   look's signature changes.
2. **Clothing is the outfit models' own parts.**
   - Each wearable item names its set, part and cloth variant (`outfit:Ranger:Body:Ranger3`).
   - The chest item decides the base outfit.
   - Parts of the other set are rebound onto the same skeleton by bone name.
   - Empty clothing slots show undyed underclothes, because there is no bare body fitted to the
     outfits.
   - A hood hides the hair.
3. **Weapons, shields and hats are procedural** (`ItemModels`): metre meshes with the kit's
   materials.
   - The same model is used on shop shelves and on the body.
   - Props are placed in the body's bind-pose frame and parented to bones, so they follow the
     animation:

   | Item | Where it goes |
   |---|---|
   | Hats | Head |
   | One-handed weapons | Right hand |
   | Shields | Back of the left forearm |
   | Long weapons | Slung across the back (there is no combat stance yet) |

4. **The same builder serves the street and the Hub preview.** The preview renders the same body on
   its own layer and stage, filmed into a render texture.

**Consequences.**
- A PlayMode test checks what the body shows (`PlayerBodyReport`): equipping a knife puts it in
  `hand_r`, taking off the tunic shows underclothes, a female frame rebuilds.
- Screenshots show the sword in hand in the street and in the preview.
- The look is limited by the art: two outfit families and simple prop shapes. Better art can
  replace `ItemModels` per item without changing the domain.

**Alternatives considered.**
- *Import the modular part FBX files separately.* Unnecessary: the outfit models already contain
  them.
- *Show equipment only in the UI.* Rejected by the brief: equipping must update the visible model.
