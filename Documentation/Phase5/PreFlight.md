# Phase 5 pre-flight: Living World, Player Hub & Commerce

Written 2026-11-10, before any Phase 5 code.

## Repository

| | |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| HEAD | `277bd48` Phase 4, equal to `origin/feature/phase-0-1-foundation` after a fetch |
| Working tree | Clean |
| Unity editor | Closed, so batch-mode runs are possible |
| Save schema | v6 |

## What exists

| Area | State |
|---|---|
| World clock | `WorldData.CurrentSimulationDate` holds days only (12 × 30-day calendar, ADR-0006). `SimulationPlayback` issues `AdvanceDaysCommand` at whole-day boundaries (Normal = 1 s per day). The daily systems run inside one copy → validate → commit transaction. There is no hour. |
| Player | `PlayerCharacterData` is persistent and in the domain: ID, name, location kind, settlement, region, pose, perspective and last settlement. Commands move it. It has no appearance, inventory, equipment or currency. The body is a fixed ranger built by `NpcAppearanceResolver.Player()` (graphics pass). |
| Items, inventory, currency | None for the player. Settlements hold aggregate resources (food, timber, stone, iron, coin) in `ResourceInventory`; coin is the settlement treasury. Named people have personal savings (`NpcCondition.Wealth`). |
| Shops | None. Locations of kind Shop and Workshop exist on layout sites: `house-0` and `house-1` in towns and villages, and the granary. Shopkeeper candidates exist: Merchant at the shop, Smith at the smithy, Innkeeper at the tavern, Labourer or Farmer at the granary. |
| NPC schedules | A weekly pattern of one location per day, plus need overrides. Movement is daily and authoritative. Exploration places people at their site; they idly wander and do not walk between places. |
| Exploration | `PlayerCharacter` (CharacterController), a camera rig with two perspectives, `ExplorationAtmosphere` (fixed sun), and the modular builder. Only the tavern is enterable. |
| Character assets | Four complete outfits (Peasant and Ranger, male and female) with variant textures; heads; 8 hair and beard pieces. The downloaded pack also has modular parts (body, arms, legs, feet, hood, pauldrons), not yet imported. There are no weapon or shield models in any pack. |

## Conflicts and decisions

1. **The day is the simulation's unit.** Time of day is added as a minute-of-day on `WorldData`. Daily systems still run once per midnight, in the same transaction. Within the day, NPC activity and location are a deterministic function of (schedule, minute), so nothing extra needs storing.
2. **One owner per item.** Items are definitions (data) plus stacks in exactly one container: the player's inventory or equipment, or a shop's stock. Settlement resources stay aggregate. Shop restocking is the only bridge: it consumes aggregate materials and pays the treasury.
3. **Shopkeepers are existing NPCs.** The shop's till is the shopkeeper's own savings (`NpcCondition.Wealth`), so no second money pool exists.
4. **Equipment must be renderable.** Clothing slots map to the imported modular outfit parts. Weapons and shields use simple procedural models, since the CC0 packs have none. Nothing is equippable that cannot be shown.
5. **Playback speed.** A day of 1 s cannot show a day/night cycle. Playback advances minutes; Normal becomes 1 game hour per 2 s, and Fast and VeryFast keep the strategic pace usable.

## Implementation order

Clock (domain, playback, lighting) → items, inventory, equipment → shops, pricing, restock, buy/sell, reputation → hourly NPC activities and visible movement → Player Hub and shop UI, enterable shops → schema v7 → tests, performance, screenshots, docs.
