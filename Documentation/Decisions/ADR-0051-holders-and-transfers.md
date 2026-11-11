# ADR-0051: Every instance has one holder; transfers are atomic commands; weight limits the pack

**Status.** Accepted, 2026-11-11 (Phase 11).

**Context.**
- Items had two places to be: the pack and a slot. Loot went straight into the pack. The pack's limit was an arbitrary 40 stacks.
- Phase 5 had its own pace curve (25 kg) beside the SRD carrying capacity the character sheet used.

**Decision.**
1. **Holders.** An instance is held by exactly one of these:
   - the pack;
   - an equipment slot;
   - a container (`ContainerData`: the player's chest, a cache, a loot pile, a settlement store, a camp);
   - a shop's particular goods (`ShopData.Goods`);
   - work in progress (`WorkJobData.Held`).
   `WorldValidator` rejects any stack ID that appears twice across all holders.
2. **Containers have keys.** Each container has a stable key (`chest:{settlement}`, `cache:{settlement}`, `loot:encounter:{id}`), a kind, access rules (player access, accepts deposits), coin, an optional capacity, and the key of what filled it. They are saved and never regenerated.
3. **Transfers are commands.** The transfer commands are `TakeFromContainer`, `PutInContainer`, `TakeAllFromContainer`, `MoveContainerCoin` and `OpenContainer`.
   - Each edits the working copy. A refusal changes nothing: wrong settlement, no deposits, too heavy, bound or attuned.
   - Take-all is all or nothing.
   - Taking part of a stack makes a new instance; the rest keeps its ID.
4. **Weight is the limit.** Carrying capacity is Strength × 15 lb, doubled by Powerful Build (SRD 5.2.1).
   - Over capacity, Speed is 5 ft. `CharacterRules.SpeedBreakdown` and the exploration pace both read it.
   - Past twice capacity nothing more can be picked up (`PackLimitGrams`).
   - The Phase 5 pace curve is gone.
   - A 200-stack safeguard remains only against weightless clutter.
5. **Genesis.** `ItemGenesis.Ensure` gives each settlement the player's chest (at the tavern), a cache filled once from `old_cache`, and its gathering sites. It is deterministic and runs once, also for old saves.

**Consequences.**
- No duplication or loss across holders. Fault-injection tests prove the validator catches a stack held twice.
- A weak character really is limited by what they carry.

**Alternatives considered.**
- *A stack-count limit:* arbitrary, and unrelated to the rules.
- *Loot straight into the pack:* overflow had to be silently dropped or refused, and choice was lost.
