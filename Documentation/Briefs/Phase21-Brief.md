PHASE 21 — ENVIRONMENTAL WORLD, LIVING SETTLEMENTS, FUNCTIONAL SHOPS, CRAFTING WORKSHOPS & REALISTIC INTERIORS

Project: Realmkeeper
Engine: Unity 6 / URP
Branch: feature/phase-0-1-foundation
Dependency: Phase 20A must be completed and locally committed before this phase begins.
Expected baseline: The Phase 20A commit, with Phase 20’s pushed implementation and all preceding phases preserved.

EXECUTION ORDER AND RULES

This prompt is supplied alongside Phase 20A. Do not execute both phases simultaneously.

1. Wait until Phase 20A has completed, been tested, documented, and committed locally.
2. Verify the actual resulting HEAD and Git status.
3. Then begin Phase 21.
4. Audit the existing systems before designing replacements.
5. Preserve the existing Domain/Simulation/Application/Infrastructure/Presentation architecture.
6. Keep the authoritative world simulation independent of Unity presentation.
7. Use stable IDs, deterministic generation, transactional commands, and existing save/migration conventions.
8. Do not build decorative objects that falsely appear functional.
9. Do not create a separate economy or inventory system for shops and crafting.
10. Do not introduce arbitrary setting canon, kingdom history, or named factions.
11. Do not silently discard or overwrite existing settlement layouts, buildings, shops, NPCs, or saved worlds.
12. Research assets and licenses before integrating them. Commercial-use and redistribution restrictions must be understood.
13. Do not push to origin. Commit locally only.
14. Do not claim manual human playtesting. Provide an owner playtest checklist.
15. Do not begin Phase 22 or invent additional phases. Stop after this phase and report.

⸻

1. THE OBJECTIVE

Realmkeeper now has a persistent world, six connected regions, settlements, factions, NPCs, companions, shops, crafting, items, exploration, quests, combat, and a redesigned first-pass interface.

However, the world still needs a major environmental and physical-world expansion.

The goal of Phase 21 is to make the world feel inhabited, geographically distinctive, environmentally responsive, and materially believable.

This is not simply an art pass.

The weather must exist in the simulation. Shops must contain real inventory. Displayed goods must correspond to actual stock. Workshops must use real recipes and materials. Buildings must have interiors that make sense for their purpose. NPCs must have reasons to be in those spaces and activities to perform there.

The player should be able to enter a settlement and recognize that people live and work there.

A blacksmith should look and function like a blacksmith. A leatherworker should have a working leather shop. A merchant should sell actual goods. A tavern should feel like a tavern. A house should look like someone’s home.

The same persistent world must remain authoritative across the strategic map, regional travel, settlement exploration, and building interiors.

2. PREFLIGHT: AUDIT THE ACTUAL WORLD

Before implementation, inspect the existing code and document the current state.

Create Reports/Phase21-Preflight-Audit.md.

Inspect at minimum:

2.1 World and geography

* World generation.
* Six-region generation.
* Region boundaries and terrain continuity.
* Terrain height, water, rivers, lakes, roads, vegetation, and forests.
* Climate or biome data, if any.
* Strategic-map presentation.
* Local settlement environments.
* World time, calendar, daily simulation, and weekly distant-region simulation.
* Existing environmental lighting and day/night transitions.

2.2 Settlement and building systems

* Settlement types and sizes.
* Districts and building placement.
* Building definitions and generated layouts.
* Existing structures and functional places.
* Interior generation and persistence.
* Doors, rooms, passages, furniture, and interactable objects.
* Existing NPC schedules and indoor movement.
* Companion and follower navigation.
* Current scene construction and asset loading.

2.3 Economy, shops, and crafting

* Item catalog and item instances.
* Ownership and inventory invariants.
* Personal storage.
* Merchant stock and till.
* Buying and selling.
* Merchant restocking.
* Item condition and provenance.
* Recipes and crafting jobs.
* Material gathering.
* Repairs and smith services.
* Existing crafting and inventory interfaces.

2.4 NPCs and simulation

* Daily routines.
* Hourly schedules.
* Work and leisure behavior.
* Settlement professions.
* Population and labor simulation.
* NPC relationships and dialogue.
* NPC movement and navigation.
* Simulation LOD.
* Existing environmental and faction reactions.

2.5 Persistence and architecture

* Current save schema and migration chain.
* Stable building, room, item, NPC, and location IDs.
* Deterministic generation.
* World snapshot and event log.
* Command validation and transactional mutation.
* Asset loading and import rules.

Identify what can be reused, what needs extending, and what requires new architecture.

Do not begin building a second version of an existing system.

3. ENVIRONMENTAL SIMULATION — WEATHER AND SEASONS

Create a persistent environmental system that is grounded in the world simulation.

3.1 Weather model

Implement a data-driven weather model supporting appropriate conditions such as:

* Clear.
* Partly cloudy.
* Overcast.
* Light rain.
* Heavy rain.
* Thunderstorm.
* Light snow.
* Heavy snow.
* Fog.
* Windy conditions.
* Heat or dry conditions where appropriate to the region.

Do not force every weather type into every region.

Weather must be appropriate to:

* Region.
* Season.
* Local climate.
* Elevation.
* Terrain.
* Nearby water.
* Current and recent weather conditions.

Weather transitions should be believable rather than randomly changing every hour.

Implement deterministic weather generation using the world’s seed and saved environmental state.

The same seed, world history, and simulation inputs should produce the same weather outcomes.

3.2 Regional weather

Weather must be regional, not a single global toggle.

Two distant regions may have different weather at the same time.

Weather should evolve coherently across adjacent regions. Avoid obvious artificial boundaries where rain abruptly stops at a region border.

Account for:

* Mountain elevations.
* Rain shadows where appropriate.
* Coastal or lakeside conditions where supported by geography.
* Forest and open-land differences.
* Seasonal temperature differences.

The simulation does not need a scientifically complete meteorological model, but it must be coherent and explainable.

3.3 Seasons and calendar

Audit the current calendar before defining seasonal rules.

If the existing world calendar lacks season support, implement a documented calendar model without breaking existing dates or saves.

Support:

* Spring.
* Summer.
* Autumn.
* Winter.

Seasonal changes should influence:

* Weather probabilities.
* Temperature.
* Daylight.
* Vegetation appearance.
* Crop cycles.
* Outdoor work.
* Food production.
* Travel conditions.
* NPC clothing choices where supported.

Do not simply recolor the world four times.

3.4 Environmental gameplay consequences

Weather should influence real gameplay where appropriate:

* Rain affects outdoor visibility and wet ground.
* Heavy rain may affect travel speed or road conditions where justified.
* Snow affects travel and outdoor work.
* Cold weather influences shelter, clothing, and exposure if the survival system supports it.
* Storms may interrupt exposed outdoor work.
* Fog changes visibility and exploration.
* Seasonal changes influence farming and food availability.
* NPC routines respond to severe weather.
* Caravans and road groups respond to dangerous conditions.

Do not introduce arbitrary penalties that make travel frustrating.

Use existing movement, travel, production, and NPC scheduling systems. Any new environmental effects must be visible, explainable, and testable.

If a survival mechanic such as temperature exposure does not exist, do not invent an intrusive survival system just to make weather appear meaningful. Document it as a possible later extension.

3.5 Visual weather

Implement visual presentation for weather in local exploration:

* Rain particles with appropriate density and distance.
* Snowfall.
* Fog and atmospheric depth.
* Cloud and sky changes.
* Wind response in trees, grass, flags, and suitable objects.
* Wet surfaces and puddles where appropriate.
* Snow accumulation where appropriate.
* Thunder and lightning for storms.
* Weather transitions rather than instant visual swaps.

Weather effects must not overwhelm performance or obscure gameplay.

The visual weather must reflect the actual saved regional weather state.

3.6 Lighting and time of day

Improve the existing day/night system.

Include:

* Dawn.
* Morning.
* Midday.
* Afternoon.
* Dusk.
* Night.

Improve:

* Sun direction and color.
* Skybox and ambient light.
* Shadow direction and intensity.
* Interior lighting.
* Street lamps and torches.
* Window lighting.
* Fog and atmospheric color.
* Weather-dependent lighting.

Interior lighting should make sense independently of outdoor brightness.

A lit forge should look like a forge at night. A dark house should not be fully illuminated simply because the global ambient light is bright.

Maintain readable character and environment visibility.

4. WORLD SCENERY AND REGIONAL IDENTITY

The strategic map and local exploration environments need more visual depth and geographic distinction.

4.1 Terrain and scenery

Audit current terrain and identify the most obvious visual limitations.

Improve:

* Hills and elevation transitions.
* Mountains and foothills.
* Cliffs and rock formations.
* Riverbanks and shorelines.
* Lakes and wetlands.
* Forest edges and density.
* Grasslands and meadows.
* Farmland.
* Roads, paths, bridges, and crossings.
* Natural landmarks.
* Ruins and wilderness locations.

Avoid repetitive terrain patterns and obvious procedural artifacts.

Terrain must remain navigable and compatible with existing location and travel systems.

4.2 Vegetation

Expand the existing vegetation library using properly licensed assets.

Support:

* Deciduous trees.
* Conifers.
* Bushes.
* Shrubs.
* Grass.
* Wildflowers.
* Reeds.
* Ferns.
* Crops.
* Orchard trees.
* Dead trees and fallen logs.
* Seasonal foliage.

Vegetation should reflect regional conditions, terrain, elevation, and season.

Use deterministic placement.

Avoid:

* Trees floating above terrain.
* Trees intersecting buildings.
* Excessive repeated models.
* Obvious grid patterns.
* Trees blocking essential doors and paths.
* Excessive draw calls and unnecessary high-detail models.

Implement sensible distance-based presentation and batching/instancing where appropriate.

4.3 Natural points of interest

Add environmental details that give the world recognizable places:

* Waterfalls where terrain supports them.
* Rock arches and formations.
* Woodland clearings.
* Ancient trees.
* Natural springs.
* Small ponds.
* Scenic overlooks.
* Abandoned roads.
* Ruined bridges.
* Old campsites.
* Wildlife habitats.

These must be generated from actual geography and persisted through the existing world generation and discovery architecture.

Do not scatter arbitrary landmarks without considering location context.

4.4 Same world, different views

The strategic map and local exploration must continue to represent the same world.

Do not create a separate scenic map or a disconnected exploration environment.

The same:

* Terrain.
* Roads.
* Rivers.
* Bridges.
* Buildings.
* Settlements.
* Weather.
* Discoveries.
* Structures.
* Relevant environmental states.

must correspond across the world and local views.

5. FULLY FUNCTIONAL SHOPS AND COMMERCIAL BUILDINGS

This is one of the most important deliverables.

Current shops need to become convincing physical businesses rather than a merchant interaction attached to an otherwise empty structure.

5.1 Shop types

Audit existing shop definitions and expand them using the existing economy and item catalog.

Support appropriate businesses such as:

* General goods merchant.
* Grocer and provisions shop.
* Butcher.
* Baker.
* Tavern and inn.
* Blacksmith.
* Weaponsmith.
* Armorer.
* Leatherworker.
* Tanner.
* Clothier and tailor.
* Herbalist or apothecary.
* Alchemist where the existing item and recipe systems support it.
* Carpenter.
* Fletcher.
* Jeweler.
* Bookseller or scribe where appropriate.
* Stable and animal trader if the existing world systems support it.

Do not force every settlement to contain every business.

Shop availability should depend on:

* Settlement size.
* Prosperity.
* Resources.
* Trade routes.
* Regional specialization.
* Existing buildings.
* Available labor.
* Faction and ownership context.

A hamlet should not have the same commercial infrastructure as a large town.

5.2 Real stock and inventory

Every shop must use actual item instances and the existing ownership system.

Requirements:

* Physical display goods correspond to real stock.
* Merchant UI inventory corresponds to the same stock.
* Buying removes the item from shop ownership and transfers it to the player.
* Selling transfers ownership to the shop.
* Unique items retain their identity and condition.
* Charges, quality, condition, curse state, provenance, and other item properties persist.
* No duplicate items.
* No phantom stock.
* No purchases from stale inventory.
* Stock changes through real deliveries, production, purchases, sales, and restocking.
* Merchant funds and tills remain consistent with the existing economy.

Do not make a visual inventory separate from the actual inventory.

5.3 Stock variety

Shops should have convincing, varied inventories.

For example:

General goods

* Rope.
* Torches.
* Lanterns.
* Backpacks.
* Tools.
* Basic clothing.
* Travel supplies.
* Containers.
* Common equipment.

Grocer

* Bread.
* Meat.
* Vegetables.
* Fruit.
* Preserved food.
* Cooking ingredients.

Blacksmith

* Weapons.
* Armor.
* Shields.
* Tools.
* Repair materials.
* Metal components.

Leatherworker

* Leather armor.
* Belts.
* Pouches.
* Bags.
* Gloves.
* Boots.
* Hides.
* Leatherworking supplies.

Clothier

* Shirts.
* Trousers.
* Cloaks.
* Robes.
* Hats.
* Gloves.
* Fabric.
* Thread.

Apothecary

* Herbs.
* Ingredients.
* Healing supplies.
* Supported consumables and recipes.

Tavern

* Prepared meals.
* Drinks supported by the existing item catalog.
* Provisions.
* Inn services where implemented.

Use the existing item catalog where possible. Create new items only when needed, with proper definitions, prices, weights, categories, icons, descriptions, and licensing provenance.

Do not flood shops with hundreds of meaningless copies.

5.4 Supply and demand

Stock should make economic sense.

Use:

* Settlement production.
* Merchant procurement.
* Trade caravans.
* Imports and exports.
* Local resource availability.
* Shop specialization.
* Existing economic conditions.
* Regional shortages and surpluses.

If a caravan carrying iron is lost, relevant shops should eventually feel that shortage.

If local farms produce a surplus of grain, food businesses should be able to acquire it.

Restocking must be deterministic, explainable, and persistent.

Do not create stock from nowhere on every restock cycle.

Where stock is generated as initial world setup, record its origin as initial stock rather than pretending it was produced later.

5.5 Physical shop displays

Build physical displays that match the actual merchant stock:

* Shelves.
* Counters.
* Cabinets.
* Weapon racks.
* Armor stands.
* Barrels.
* Crates.
* Food baskets.
* Hanging goods.
* Clothing displays.
* Tool racks.
* Ingredient containers.

The display should show a sensible representation of available stock.

It does not need to show every unit individually. A stack can be represented by a display group, but the display must not imply unavailable items are in stock.

Support interaction with:

* Shopkeeper.
* Display.
* Work counter.
* Repair station.
* Relevant service location.

The shopkeeper must be a persistent NPC from the existing settlement population.

5.6 Shop interiors

Each shop type should have a recognizable layout.

Examples:

* Blacksmith: forge, anvil, workbench, weapon rack, storage, coal/wood.
* Leatherworker: cutting table, hide racks, tools, tanning supplies, leather goods.
* Grocer: shelves, counters, produce baskets, storage.
* Baker: oven, preparation table, flour, shelves, bread display.
* Tavern: bar, seating, tables, kitchen, food storage.
* Tailor: fabric racks, sewing station, clothing displays.
* Apothecary: ingredient shelves, workbench, bottles, storage.

Do not use one generic interior with a different sign above the door.

6. FUNCTIONAL CRAFTING WORKSHOPS AND PRODUCTION STATIONS

Extend the existing crafting system rather than creating a second crafting system.

6.1 Stations

Implement appropriate functional stations, including:

Blacksmithing

* Forge.
* Anvil.
* Workbench.
* Quenching barrel or trough where appropriate.
* Weapon rack.
* Metal storage.
* Tool storage.

Leatherworking

* Cutting table.
* Leatherworking bench.
* Stitching and tooling station.
* Hide racks.
* Drying frames.
* Tanning vats where appropriate.
* Leather storage.

Carpentry

* Workbench.
* Sawing area.
* Tool rack.
* Lumber storage.
* Woodworking tools.

Tailoring

* Sewing station.
* Fabric table.
* Thread and material storage.
* Clothing displays.

Cooking and baking

* Cooking hearth.
* Oven.
* Preparation table.
* Pantry.
* Food storage.

Alchemy and herbalism

* Preparation bench.
* Mortar and pestle.
* Ingredient shelves.
* Supported brewing or mixing equipment.

Only implement station types for which the underlying recipes and materials can be supported.

6.2 Actual crafting interaction

The player must be able to:

* Find the station.
* Inspect it.
* Understand its purpose.
* See compatible recipes.
* Check required materials.
* Start a job.
* Spend or reserve the correct materials.
* Wait the required in-game time.
* Collect the finished item.
* Cancel or abandon a job according to clearly defined rules.

Use existing item ownership and crafting-job persistence.

Materials must not be duplicated or lost.

Station requirements:

* Some recipes require a specific station.
* Some recipes require tools.
* Some recipes require proficiency or skill.
* Some recipes require a suitable location or professional.
* Some jobs can be performed by the player.
* Some may require a qualified NPC.
* Recipe duration and output must be defined.

Use the existing rules engine for relevant skill checks.

6.3 Expand the recipe catalog

Audit the existing 11 recipes from Phase 11 and every recipe added afterward.

Create a meaningful progression of:

* Basic weapons.
* Metal tools.
* Weapon repairs.
* Armor repairs.
* Leather armor.
* Leather goods.
* Bags and pouches.
* Belts and straps.
* Clothing and cloth goods.
* Wooden tools and furniture.
* Cooking and food preparation.
* Supported alchemical items.

Do not add recipes with missing inputs or fake outputs.

Every recipe must be:

* Validated.
* Connected to real materials.
* Connected to appropriate stations.
* Connected to the correct item definition.
* Tested for ownership and persistence.
* Properly documented.

Do not turn every item into a crafting recipe merely to inflate the count.

6.4 NPC production

NPCs with relevant professions should use appropriate workstations.

Examples:

* Blacksmith works at the forge and anvil.
* Leatherworker works at leather stations.
* Baker uses the oven and preparation area.
* Tailor uses the sewing station.
* Carpenter uses the workbench.
* Cook works in the kitchen.

Use existing NPC schedules and simulation LOD.

Do not run expensive detailed animation or production simulation for every person in every region.

Work should have actual economic effects where the existing production model supports them.

A blacksmith should not create infinite stock simply by standing at an anvil.

7. BUILDINGS AND REALISTIC INTERIORS

The buildings currently provide a foundation, but their interiors need much more depth.

This section should receive substantial implementation effort.

7.1 Building purpose and interior templates

Create a data-driven interior architecture.

Support building types such as:

* Small and large homes.
* Wealthy homes.
* Poor homes.
* Inns and taverns.
* General stores.
* Workshops.
* Blacksmiths.
* Leatherworkers.
* Tailors.
* Bakeries.
* Butcher shops.
* Guardhouses.
* Barracks.
* Chapels.
* Temples.
* Guild halls.
* Noble halls.
* Warehouses.
* Farmhouses.
* Mills.
* Stables.
* Medical or healing buildings where appropriate.
* Libraries and record houses.

Each type should have its own layout rules, furniture requirements, functional objects, and atmosphere.

Use building size, wealth, profession, owner, settlement culture, and available resources to determine the interior.

7.2 Realistic furnishing

Interiors should feel occupied and purposeful.

Use appropriate:

* Tables.
* Chairs.
* Benches.
* Beds.
* Cabinets.
* Shelves.
* Chests.
* Desks.
* Rugs.
* Curtains.
* Storage containers.
* Fireplaces.
* Lamps.
* Candles.
* Wall hangings.
* Cooking equipment.
* Tools.
* Work surfaces.
* Decorative objects.
* Personal possessions.

Homes should have believable differences.

A poor household should not have the same furnishings as a prosperous merchant.

A guardhouse should not look like a tavern.

A chapel should not be furnished like a warehouse.

Do not fill rooms with random props simply to increase object count.

7.3 Interior realism and atmosphere

Implement:

* Appropriate lighting.
* Local light sources.
* Shadow and ambient-light behavior.
* Fire and fireplace presentation.
* Window placement and light.
* Room-specific materials.
* Floor and wall variation.
* Ceiling and roof treatment.
* Door placement.
* Furniture placement.
* Room connectivity.
* Storage and interactable objects.

The player should not see:

* Furniture floating.
* Props clipping through walls.
* Beds blocking doors.
* Stairs leading nowhere.
* Doors opening into furniture.
* Objects placed outside the building.
* Unreachable essential interactables.
* Repeated identical room layouts everywhere.

Use deterministic placement and validate generated layouts.

7.4 Persistent interior state

Preserve the existing persistent place model.

Opening a chest, moving an item, breaking a door, damaging a structure, discovering a secret, or changing a station must persist appropriately.

The generated building interior should not be rebuilt into a different layout every time the player enters.

Ensure save/load preserves:

* Interior identity.
* Layout seed.
* Room state.
* Door state.
* Furniture state where mutable.
* Container ownership.
* Shop inventory.
* Crafting jobs.
* Station availability.
* NPC assignments.
* Relevant environmental states.

7.5 NPCs inside buildings

Use existing schedules and movement systems.

Examples:

* Shopkeepers remain at or near their counters during working hours.
* Blacksmiths use forge and anvil locations.
* Leatherworkers use their stations.
* Tavern staff work behind the bar or in the kitchen.
* Residents sleep in their homes.
* Guards occupy guard posts.
* Priests use appropriate chapel locations.
* Merchants access storage and stock areas.

NPCs must not walk through walls or teleport between rooms as a normal behavior.

Use actual navigable paths and door state.

If a building is closed, locked, or inaccessible, NPC behavior should respect that.

The player should be able to see believable activity when entering a populated building.

8. SETTLEMENT LIFE AND ENVIRONMENTAL ACTIVITY

Expand the sense of life beyond static NPCs.

Implement appropriate activity such as:

* Merchants opening and closing shops.
* Customers visiting shops.
* Workers moving between workshops and storage.
* Tavern patrons eating and socializing.
* Residents using public spaces.
* Guards patrolling and standing watch.
* Farmers working fields.
* Caravans unloading goods.
* Craftspeople using their stations.
* People responding to severe weather.
* Seasonal activity changes.

NPC-to-NPC interaction should build on Phase 18’s overheard conversations and personality behavior.

Do not create random meaningless walking.

Each activity should have:

* A reason.
* A valid location.
* A schedule or trigger.
* A deterministic outcome where simulation state changes.
* Appropriate interruption behavior.
* Save/load persistence where relevant.

Use simulation LOD for distant populations.

Nearby important NPCs may have detailed movement and animation; distant population behavior should remain efficient.

9. ASSET RESEARCH, ART DIRECTION AND LICENSING

The existing art style is stylized fantasy. Preserve a coherent visual identity.

Research suitable assets for:

* Medieval buildings.
* Building interiors.
* Furniture.
* Market stalls.
* Shop displays.
* Blacksmithing.
* Leatherworking.
* Tools.
* Weapons and armor.
* Food and kitchen props.
* Vegetation.
* Terrain.
* Rocks.
* Water.
* Weather effects.
* Wildlife.
* Lighting and atmospheric effects.
* Character animations.

Review existing assets first:

* KayKit.
* Quaternius.
* Kenney.
* game-icons.net.
* Existing project assets.

Research additional legitimate sources where needed.

For every new asset:

* Verify the exact license.
* Verify commercial use.
* Verify modification rights.
* Verify redistribution restrictions.
* Record attribution requirements.
* Record the source and download/version information.
* Confirm compatibility with Unity 6 and URP.
* Confirm scale, materials, rigging, and performance suitability.

Prefer CC0 where practical.

CC BY assets are acceptable when attribution is properly maintained.

Do not:

* Use ripped game assets.
* Use extracted proprietary models.
* Assume free download means commercial permission.
* Use assets with unclear licenses.
* Commit assets whose license prohibits repository redistribution.
* Add large collections without measuring build and repository impact.

Update CREDITS.md and the asset registry.

9. VISUAL QUALITY AND UI INTEGRATION

Phase 19 was a first-pass UI overhaul, not a complete redesign of every interface.

Do not undo the established visual system.

Integrate Phase 21 features into the existing design language.

Add or improve:

* Weather and season indicators.
* Regional climate information.
* Shop browsing and stock status.
* Station interaction.
* Crafting and work orders.
* Interior inspection.
* Building and business information.
* NPC profession and work status.
* Relevant world and settlement information.

Shop and crafting interfaces should display:

* Actual available inventory.
* Prices.
* Item condition and quality.
* Required materials.
* Station requirements.
* Skill requirements.
* Job duration.
* Current work status.
* Collection and cancellation options.

Do not create separate UI screens that duplicate existing inventory, crafting, or merchant logic.

Review the Phase 19 backlog and preserve it for a dedicated future completion pass:

* Strategic map framing and inspector.
* Exploration HUD contextual interactions.
* Character and inventory detail.
* Crafting and storage redesign.
* Spellbook.
* Journal and discoveries.
* NPC, party, and faction screens.
* Combat presentation.

Phase 21 may improve interfaces needed for its own features, but do not claim the entire Phase 19 UI backlog is complete.

10. PERFORMANCE AND TECHNICAL REQUIREMENTS

The environmental and settlement improvements must not compromise the simulation.

Profile:

* World generation.
* Regional weather updates.
* Seasonal transitions.
* Terrain rendering.
* Vegetation rendering.
* Interior generation.
* Shop stock calculations.
* Shop restocking.
* NPC schedules.
* Nearby NPC movement.
* Interior pathfinding.
* Crafting and production.
* Save/load.

Requirements:

* No per-frame world simulation.
* No unnecessary object creation during routine updates.
* Avoid rebuilding interiors every frame or every time the player enters.
* Use pooling or instancing where justified.
* Use distance-based detail.
* Keep distant regions on the existing lower-frequency simulation schedule.
* Do not instantiate all interior props for every building in every region simultaneously.
* Avoid excessive real-time lights and shadow casters.
* Avoid expensive weather effects when the player cannot see them.
* Ensure weather state changes do not trigger unnecessary full-world rebuilds.

Provide actual measurements rather than claims that performance is acceptable.

Compare against a recorded baseline and explain any regressions.

11. SAVE SCHEMA, DETERMINISM AND MIGRATION

Audit the existing save schema before adding fields.

Any new persistent data must:

* Have stable IDs where appropriate.
* Use deterministic generation.
* Be versioned.
* Migrate older saves safely.
* Preserve all existing character, NPC, companion, faction, quest, place, item, and world data.

Test:

* Phase 20 v21 saves.
* Earlier supported schema migrations.
* New-world generation.
* Existing-world upgrades.
* Save/load while inside a building.
* Save/load during weather.
* Save/load with active crafting jobs.
* Save/load with shop stock changes.
* Save/load with changed doors, containers, and interiors.
* Save/load during regional travel.

The same seed and initial settings must reproduce the same generated settlement and interior content.

Do not reroll a player’s established world during migration.

12. TESTING AND ACCEPTANCE CRITERIA

Build meaningful automated tests for each new system.

Weather

* Deterministic weather generation.
* Regional variation.
* Seasonal transition.
* Save/load.
* Weather consequences.
* NPC response.
* Weather visual state matches simulation state.

Scenery

* Deterministic placement.
* Terrain compatibility.
* No floating or invalid props.
* Navigability.
* Region boundaries.
* Performance.

Shops

* Shop generation by settlement context.
* Real item ownership.
* Stock and till consistency.
* Purchase and sale.
* Item condition preservation.
* Restocking.
* Supply shortages.
* Caravans.
* Save/load.
* Physical display consistency.

Crafting

* Recipe validation.
* Material requirements.
* Station requirements.
* Skill requirements.
* Time requirements.
* Material reservation.
* Completion.
* Cancellation.
* No duplication or loss.
* Save/load.

Interiors

* Layout validity.
* Door connectivity.
* Room reachability.
* Functional furniture placement.
* Lighting.
* Shop/workstation availability.
* NPC navigation.
* Persistent state.
* Save/load.
* Determinism across seeds.

NPC activity

* Work schedules.
* Customer activity.
* Shopkeeper activity.
* Workshop activity.
* Weather responses.
* Door and access constraints.
* No duplicate NPC bodies.
* No wall clipping through navigation.
* Save/load.

Run:

* Full .NET Release suite.
* Full Unity EditMode suite.
* Full Unity PlayMode suite.
* Existing regression tests.
* Migration tests.
* Performance tests.

Do not report a full suite as passing if only targeted tests were run.

Do not silently relax test assertions or inflate timing thresholds to hide regressions.

If a test is flaky or inconclusive, investigate and document it.

13. SCREENSHOTS AND VISUAL VALIDATION

Capture actual Unity renders.

Required captures:

World and environment

* Clear midday.
* Sunrise or sunset.
* Night.
* Rain.
* Heavy weather.
* Snow or regionally appropriate winter conditions.
* Forest and open terrain.
* Water and mountain scenery.
* Seasonal differences.

Settlements

* Wide settlement overview.
* Market street.
* Fully stocked general shop.
* Grocer or baker.
* Blacksmith interior.
* Leatherworker interior.
* Tavern interior.
* Residential interior.
* Guardhouse or chapel.
* Workshop with NPC activity.

Gameplay

* Buying an actual displayed item.
* Selling an item.
* Checking a stock shortage.
* Using a crafting station.
* Starting a crafting job.
* Collecting a finished item.
* NPC using a workstation.
* NPC customer behavior.
* Save/load preserving changed stock and interior state.

Capture representative views at:

* 1920×1080.
* 1366×768.
* A compact aspect ratio.

Inspect screenshots individually.

Do not rely on screenshots alone to prove functionality.

14. OWNER PLAYTEST CHECKLIST

Provide a practical manual checklist for:

* Walking around the same settlement during different weather.
* Observing day/night and interior lighting.
* Visiting multiple shop types.
* Inspecting physical shop stock.
* Buying and selling actual displayed goods.
* Visiting a blacksmith and using the forge/anvil.
* Visiting a leatherworker and using the leatherworking station.
* Crafting an item.
* Saving and reloading while inside a building.
* Observing NPCs at work.
* Observing customers and settlement activity.
* Traveling between regions with different weather.
* Verifying the world remains coherent between strategic and local views.

Clearly label all human-only checks.

15. DOCUMENTATION AND GAME DESIGN BIBLE

Update the Game Design Bible to reflect the actual Phase 21 implementation.

Document:

* Environmental simulation.
* Weather.
* Seasons.
* Regional climate.
* Terrain and scenery.
* Lighting.
* Shop systems.
* Merchant inventory and supply.
* Crafting stations.
* Recipes.
* Interior generation.
* Building furnishing.
* NPC professions and work routines.
* Asset sources and licenses.
* Save schema.
* Known limitations.
* Deferred content.

Keep historical phase records intact.

Update the forward roadmap with outstanding work, but do not start future phases.

16. GIT AND COMPLETION

Before committing:

* Verify the actual branch and HEAD.
* Inspect git status.
* Inspect git diff.
* Confirm no unrelated user changes.
* Confirm ignored files remain untouched.
* Confirm local-only assets remain excluded.
* Confirm third-party asset licenses and attribution.
* Confirm all required tests and captures are accounted for.
* Confirm migrations are documented.

Commit locally with a descriptive Phase 21 message.

Do not push to origin.

17. FINAL REPORT

Provide a comprehensive report containing:

1. Starting and ending commit.
2. Preflight findings.
3. Weather and season systems.
4. Environmental and terrain improvements.
5. Vegetation and scenery.
6. Shop types and real inventory integration.
7. Merchant supply and restocking.
8. Physical shop displays.
9. Functional crafting stations.
10. Recipe and production additions.
11. Building and interior improvements.
12. NPC profession and activity behavior.
13. UI changes.
14. Asset sources and exact licenses.
15. Save schema and migrations.
16. Exact test results.
17. Performance measurements.
18. Screenshot inventory.
19. Owner playtest checklist.
20. Remaining limitations and deferred work.
21. Git status and local commit hash.
22. Explicit confirmation that no push occurred.

STOP after Phase 21. Do not begin Phase 22.