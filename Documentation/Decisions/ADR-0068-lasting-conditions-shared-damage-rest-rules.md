# ADR-0068: Lasting conditions on the sheet, one damage rule, and rest that depends on where you are

**Status.** Accepted, 2026-11-11 (Phase 14).

**Context.** The preflight audit found three things:
- Conditions existed only inside fights, and ended with them. Traps and hazards printed "you are poisoned" and applied nothing.
- Harm outside fights (`PlaceRules.Hurt`) bypassed resistance, immunity, vulnerability and temporary hit points.
- Rest was allowed anywhere: in foul air, beside a den, in a stranger's bedroom. A long rest also did nothing for exhaustion, because exhaustion never outlived a fight.

**Decision.**
1. **Lasting conditions** (`LastingConditionData` on `CharacterSheetData.Conditions`). Each records:
   - type;
   - exhaustion level;
   - source key and reason;
   - start, and end in absolute world minutes (−1 = until a remedy);
   - remedies (Lesser Restoration; a long rest per exhaustion level; an escape check);
   - escape DC and tries.

   The meanings are the SRD 5.2.1 definitions the combat engine already applies; nothing new was invented.
   - **Fight-only conditions** (grappled, prone, unconscious, incapacitated, invisible, charmed, stunned, paralysed, petrified) are never kept: they need a fight's context.
   - **Disease** is not modelled: the rules data has no disease definitions.
2. **No stacking.** Re-applying a condition keeps one instance (the later end wins, remedies combine). Exhaustion levels add, and level 6 kills.
3. **Time.** Conditions end when the world clock passes their end (`WorldSession.SetClock`, so every way time moves counts). A rest does not clear them: only exhaustion loses a level per long rest, as the SRD says.
4. **Remedies.**
   - Lesser Restoration cast outside a fight ends Poisoned, Blinded, Deafened or Paralyzed.
   - Restrained ends with a Break Free action: an Athletics or Acrobatics check against the source's DC, one minute per try. After three failed tries the character works free in ten minutes, so nothing can trap them for ever.
5. **Into and out of fights.**
   - When a fight starts, the sheet's lasting conditions become combat conditions with the same key, so the engine applies them (disadvantage, −2 per exhaustion level, speed 0 while restrained).
   - When it ends, lasting conditions removed during the fight are removed from the sheet, and exhaustion levels are written back.
6. **One damage rule.** `DamageRules.Adjust` (immunity, then resistance halved rounding down, then vulnerability doubled, each once) is used by `CombatEngine.ApplyDamage` and by `DamageRules.ApplyToSheet`. `ApplyToSheet` uses the sheet's own resistances and temporary hit points. Outside a fight the last hit point is kept: ADR-0064's fairness rule, now stated in the shared pipeline rather than in a separate trap function. `PlaceRules.Hurt` is gone. Traps, hazards, fire and falls all go through `PlaceHarm`, which:
   - rolls the dice;
   - makes the save with exhaustion and condition modes;
   - applies damage through `DamageRules`;
   - applies the lasting condition;
   - records the Chronicle event.
7. **Checks and saves outside fights** use the same modes as in fights:
   - poisoned or frightened: disadvantage on checks;
   - exhaustion: −2 per level;
   - restrained: disadvantage on Dexterity saves;
   - blinded: sight checks fail;
   - deafened: listening fails.
8. **Where rest is allowed** (`RestRules.Where`).

   | Where | Short rest | Long rest |
   |---|---|---|
   | Settlement street or lodging | Allowed | Allowed, safe |
   | Open country (map) | Allowed | Allowed (a camp) |
   | Place room: no hazard, no foes in sight, not restricted | Allowed | Allowed. If inhabitants still stand somewhere in the place, the night is interrupted: the nearest group finds the player, a fight starts in their room, and the long rest gives nothing. Deterministic per place, visit and room. |
   | Hazardous room, foes in sight, someone else's house, restricted room, garrison without leave | Refused, with the reason | Refused |

   The existing 24-hour rule and hit-dice limits are unchanged.

**Consequences.**
- A trap's poison is still there after a save and reload, shows in the HUD with what it does and how it ends, wears off with time, and gives disadvantage in the next fight.
- A dwarf's poison resistance now halves a poison needle.
- Resting cannot be used to skip dangers or to erase every condition.

**Alternatives considered.**
- *Conditions on `PlayerCharacterData`:* rejected. Named NPCs with sheets fight too, and the sheet is already the one source of truth for hit points.
- *Dying outside fights:* deferred. It needs rescue, carrying and recovery flows. The fairness floor stays, but it is now explicit and shared.
