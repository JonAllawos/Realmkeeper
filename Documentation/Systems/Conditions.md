# Lasting conditions, harm and rest (Phase 14)

Decision record: ADR-0068. Code:
- `Domain/Rpg/LastingConditions.cs`
- `Domain/Combat/DamageRules.cs`
- `Application/Places/PlaceHarm.cs`
- `Application/Places/RestRules.cs`
- `Application/SpellCommands.cs` (rest)
- `Exploration/Presentation/ExplorationController.Phase14.cs` (HUD)

## Lasting conditions

A condition that outlasts a fight is stored on the character sheet (`CharacterSheetData.Conditions`). Each one records:
- **type**, and a **level** (exhaustion only, 1–6);
- **source**: a stable key such as `trap:poison_needle` (applying the same source again refreshes the condition, it never stacks);
- **reason** in words;
- the absolute minute it was **applied**, and the minute it **expires** (−1 means it lasts until a remedy);
- its **remedies**, and an **escape DC** with the **tries** used so far.

**How a condition ends:**
- **Time.** It is removed when the clock passes its expiry (`WorldSession.SetClock` → `LastingConditions.Expire`). The HUD reports what wore off.
- **Lesser Restoration** (`ConditionRemedy.Restoration`) ends Blinded, Deafened, Paralyzed or Poisoned. It can be cast outside a fight, from the conditions panel.
- **A long rest** (`LongRestLevel`) removes one level of exhaustion. Nothing else ends because of a rest; the rest summary lists what is "Still:" in force.
- **Escape** (`Escape`): Athletics or Acrobatics against the DC, one minute per try. After `EscapeTries` (3) failures the holder works free anyway in `WorkFreeMinutes` (10), so nobody is held for ever.

**What a condition does:**
- The same effects as in a fight. Checks and saves made outside fights (`PlaceRules.Check`, `PlaceRules.Save`) apply disadvantage, and exhaustion's −2 per level.
- Restrained: no moving from room to room, no resting, and no leaving the place.
- Blinded: no reading.

**Into and out of fights:**
- At the start of a fight, lasting conditions become fight conditions, carrying their `LastingKey`.
- When the fight ends they come back to the sheet with any change, without being duplicated.
- Fight-only conditions (prone, grappled) never stay on the sheet; the validator refuses them there.

**The HUD:** the panel `xk-conditions` shows each condition with its reason, what it does and how it ends. It also offers **Break free** and **Cast Lesser Restoration** where they apply, lists who follows or waits and why, and says whether this room is a place to rest.

## One rule for harm

`DamageRules.Adjust` applies:
- resistance (halved, rounded down), vulnerability (doubled) and immunity;
- then temporary hit points first.

The combat engine uses it, and so does all harm outside fights (traps, hazards, fire, a fall from a bridge), through `DamageRules.ApplyToSheet`. Outside a fight, harm never takes the last hit point. Only a fight can drop the player to 0 (the fairness rule from Phase 13, now in one place). `PlaceHarm.Apply` adds the save, the condition and the Chronicle entry (`HarmTaken`).

## Rest depends on where you are

`RestRules.Check` decides:

| Where | Result |
|---|---|
| A settlement | Lodging: safe. |
| Open country on the map | A camp. |
| A room in a place | Allowed if the room has no live hazard, is nobody's private room, and has no foes in sight. Otherwise refused, with the reason. |
| Someone's house | Refused: find lodging. |
| Any place, while restrained | Refused: break free first. |

If inhabitants who have not been dealt with remain anywhere in the place, a long rest there is **broken**:
- half the night passes;
- then that group finds the sleeper (`InterruptRestCommand`) and the fight starts in the player's room;
- nothing is recovered.
