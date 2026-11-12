# Phase 19A: Screen-to-asset matrix

**Key.**
- **GI**: game-icons.net icons (CC BY 3.0).
- **KB**: Kenney Fantasy UI Borders (CC0).
- **KC**: Kenney Cartography Pack (CC0).
- **PH**: Poly Haven leather or linen grain (CC0).
- **Font**: the chosen typeface (see the Visual Direction document).
- **Tokens**: the shared USS variables proposed in Direction A.

**"Architecture"** says whether the screen exists and can take the assets:
- **Ready**: the screen exists in UI Toolkit and the assets apply through USS.
- **Build**: the screen does not exist yet.
- **Adapt**: the screen needs code changes, e.g. textured drawing in `generateVisualContent`.

## Screens that exist today

The code was traced on 2026-11-12. The atlas is drawn with `Painter2D` in `RealmkeeperHud.Atlas.cs`. The hub, combat, dialogue, trade, creator and advancement screens are C#-built `VisualElement` windows in `Exploration/Presentation`.

| Screen | Where it lives | Assets that would improve it | Architecture |
|---|---|---|---|
| Strategic world map (3D) + sidebar | `RealmkeeperHud.cs`, `.uxml`, `RealmkeeperHud.uss`; `WorldView`, `MapIndicatorLayer` | GI for the sidebar groups (settlement, faction, people, events), time controls (hourglass, pause and play) and indicator markers. Tokens; KB corner frame on the inspector. | Ready (UI). Markers in 3D need world-space sprites or uGUI (ADR-0001 allows uGUI for world labels): Adapt. |
| Settlement inspector | `RealmkeeperHud.Places/People/Politics/Structures.cs` | GI stat icons (population, food, prosperity, stability, trade, production); meters from tokens; small sparkline charts drawn with `Painter2D` | Ready |
| World atlas | `RealmkeeperHud.Atlas.cs` (Painter2D) | KC parchment base, KC town, castle and village symbols per settlement size, KC mountain and tree stamps for region terrain, KC compass rose; GI layer-chip icons; hatched unknown land | Adapt: textured quads need `MeshGenerationContext.Allocate(..., texture)`, or child `VisualElement`s with background images |
| Event feed ("Recent events") | `RealmkeeperHud.cs` | GI category icons (trade, war, famine, weather, crime) | Ready |
| Settlement exploration HUD | `ExplorationHud.cs`, `.uxml`, `.uss` | GI condition, hunger and party icons; a prompt plate with key cap; a compass strip (tokens only) | Ready |
| Interaction prompts | `ExplorationController.*` → `ExplorationHud` | GI verb icons (talk, trade, open, inspect, lockpick, steal) | Ready |
| Player hub: Character | `PlayerHubWindow.cs` | GI ability and skill icons; PH grain; KB frame; Font (serif headings) | Ready |
| Hub: Inventory / Equipment / Weapons / Clothing / Storage / Currency | `PlayerHubWindow.Items.cs` | GI item-category icons in 40 px slots; KB slot frame; GI rarity and ownership badges | Ready |
| Hub: Crafting & work | `PlayerHubWindow.Items.cs` | GI tool and material icons | Ready |
| Hub: Spells & rest | `PlayerHubWindow.Spells.cs` | GI magic-school icons, slot pips (tokens), concentration icon | Ready |
| Hub: Party | `PlayerHubWindow.Party.cs` | GI order icons (follow, wait, guard); meters | Ready |
| Hub: Journal (quests, rumours, discoveries, decisions, events, places, crimes, reputation, campaign) | `PlayerHubWindow.Journal.cs`, `.Discoveries.cs`, `.Campaign.cs` | Parchment page (KC texture) for the reading pane; GI category glyphs; Font | Ready |
| Character advancement (level-up) | `CharacterAdvancementWindow.cs` | GI class-feature icons; tokens | Ready. Progression is level-based choices, not a tree: no skill-tree art. |
| Character creator | `CharacterCreatorWindow.cs` | Tokens to replace the white Unity text fields; GI ability icons | Ready. A new creator is out of scope (Phase 20). |
| Dialogue | `DialogueWindow.cs` | Font; GI check glyphs (persuasion, insight, intimidation); KB divider | Ready |
| Trade | `TradeWindow.cs` | Item slots as in the inventory; GI coin | Ready |
| Combat HUD | `CombatWindow*.cs` | GI action, bonus and reaction pips and attack, spell, dash and disengage icons; GI condition icons; turn-order cards | Ready |
| Faction and political management | `RealmkeeperHud.Politics.cs`; `PoliticalOverlayLayer` | GI faction-kind icons (crown, guild, temple, free folk); coloured banner chips; relation graph drawn in Painter2D | Ready / Adapt (graph) |
| NPC inspection | `RealmkeeperHud.People.cs`; `ResidentView` in exploration | GI occupation icons; relationship meter | Ready |
| Credits | Hub ▸ Info (`Credits.cs`) | Must list the GI authors once icons ship | Ready |

## Screens in the brief that do not exist yet

| Screen | Finding | Assets |
|---|---|---|
| Main menu / title | There is none. `RealmkeeperBootstrap` starts a world directly (`_worldName`, `_seed`). | A KC parchment or 3D settlement backdrop; Font for the title; KB frame. Build. |
| World creation and world selection | No UI. Saves exist (F5, Save/Load in the top bar). | Tokens and cards. Build, on the existing save and seed commands only. |
| Campaign selection | It exists inside the hub's Campaign section (Phase 17 capture `p17_06`), not as an entry screen. | Cards; GI campaign glyph. Adapt. |
| Loading screen | None; world generation is synchronous | KC parchment plus a tip line. Build only if there is a load wait to cover. |
| Settings and accessibility | None in the game (speed is in the top bar) | GI cog; Toggle, Slider and Dropdown restyled through tokens. Build, with real settings only (resolution, fullscreen, UI scale through `PanelSettings.scale`, text size). |
| Skill tree | Not applicable. SRD progression has no tree. | None |

## Priority
1. **Tokens and shared controls.** These affect every screen.
2. **Icons (GI).**
3. **Atlas (KC).**
4. **Hub, combat and dialogue.**
5. **New entry screens** (main menu, world selection, settings).
6. **Textures and decoration.**
