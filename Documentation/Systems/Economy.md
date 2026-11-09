# Settlement economy (Phase 2)

All values live in the simulation domain (`SettlementData.Economy`, `WorldData.TradeRoutes`,
`WorldData.Shipments`). The UI only reads query snapshots. All constants are in
`EconomyRules` (Simulation) and can be changed without touching systems. All arithmetic is
integer, and no system uses randomness.

## Resources

`food`, `timber`, `stone`, `iron`, `coin`. Every settlement has a storage capacity per
resource (by settlement type; for a Town: food 20,000, timber 4,000, stone 3,000, iron
1,500, coin 1,000,000). Stock never goes negative and never exceeds capacity. Production
that does not fit is recorded as **wasted**; unmet demand is recorded as **shortfall**.

## Daily order

| Order | System | What it does |
|---|---|---|
| 50 | `realmkeeper.labour` | Workforce = 50% of population. Staffs food sources first, to 115% of food demand (170% for agricultural settlements, 135% when agriculture is secondary; 100% when the granary is ≥ 75% full). Then other sources by allocation weight, capped by capacity, and finally spare workers back to food unless the granary is nearly full. |
| 100 | `realmkeeper.production` | Output = workers × rate per 100 workers × efficiency% × seasonal% (seasonal is 100% in Phase 2; the hook exists). Rates per 100 workers: farming 50, fishing 40, hunting 25, logging 40, quarrying 40, mining 30, trade 40 coin. |
| 150 | `realmkeeper.trade` | Arrivals, then departures along trade routes (below). Checks conservation and throws (rejecting the day) if any good appears or disappears outside recorded transit loss. |
| 200 | `realmkeeper.consumption` | Demand: food ⌈pop/10⌉, timber ⌈pop/80⌉, stone ⌊pop/500⌋, iron ⌊workers/300⌋, coin ⌊pop/100⌋. Consumes what is available and records the shortfall. Shortages: see below. |
| 300 | `realmkeeper.wellbeing` | Prosperity and stability move toward explainable targets, at most 1 point a day. |
| 400 | `realmkeeper.population` | Slow growth or emigration, carried in thousandths of a person. |
| 500 | `realmkeeper.chronicle` | Milestone events (ratings, population, treasury, production). |

## Production capacity and specialization (from terrain, at generation)

Capacity is the number of workers the land can employ, counted in cells around the settlement:

| Source | Capacity |
|---|---|
| Farming | 2.5 × plains/shore cells within 9, excluding the built-up core |
| Fishing | Water cells within 9, only if there are at least 10 |
| Hunting | Forest cells within 12 ÷ 4 |
| Logging | Forest cells within 16 |
| Quarrying | Hill + mountain cells within 16 |
| Mining | Mountain + hills ÷ 4 within 20, only if that ore count is at least 5 |
| Trade | Town 300, Village 60, Hamlet 15, + 10 per road |

The primary specialization has the highest output-value potential (Σ capacity × rate ×
resource value). It gets +25% efficiency and double allocation weight. A runner-up worth at
least a third of the primary is secondary (+10%, 1.5× weight). Allocation weights are stored
data, so manual editing later needs no change to the systems.

## Food and shortages

- **Food shortage** begins when reserves cover fewer than 15 days of demand and ends at 30
  days (hysteresis). Events: `FoodShortageBegan` and `FoodShortageEnded`.
- **Unmet food demand** is tolerated for 3 grace days (`FoodReservesExhausted`, reported at most
  once every 30 days). Then population falls by population / 200 per day (`StarvationBegan`).
  A single bad day never kills anyone.
- **Other resources** enter shortage after 3 consecutive days below 7 days of cover (or with any
  shortfall), and recover at 14 days of cover (`ResourceShortageBegan` and `ResourceShortageEnded`).
- **Days of food remaining** = reserves ÷ (demand − own production), shown only while in deficit.

## Trade

Routes come in pairs (A→B and B→A) along each generated road. Each route has a capacity
(the smaller settlement's: Town 240, Village 150, Hamlet 80), a travel time (road length
÷ 25 cells per day), a departure check every 2 days, and an efficiency (100 − 2 × travel
days, at least 85%) that applies to perishable food only.

On a departure check, if no shipment is already on the road, the route picks the most
urgent good. That is food while the destination is in food shortage; otherwise the good
with the largest missing share of the destination's target stock. The quantity is the
smallest of:
1. the origin's surplus above its reserve (45 days of food, 30 days of other goods);
2. the destination's need up to its target (60 days of food, 45 of other goods), minus goods already heading there;
3. route capacity;
4. destination free storage;
5. what the destination can pay for (food 2, timber and stone 3, iron 6 coin per unit).

Shipments under 5 units are not sent. Goods leave the origin at departure and live in the
shipment until arrival. On arrival, food loses the route's transit loss once. The
destination accepts what fits and pays the origin for it; a buyer never goes into debt. Goods
that do not fit wait on the road; after 5 days they travel back to the origin. Every delivery
is recorded on the route. The chronicle gets at most one delivery entry per route per 60 days.

Trade adds the value moved to both settlements' **trade activity**, which decays by 1/30 a day.

## Prosperity and stability

Factor scores, each 0–100 (`EconomyMath.Factors`):

- **food:** days of food cover ÷ 60 (capped at 40 in a food shortage, 0 while food demand goes unmet);
- **goods:** average cover of timber, stone and iron ÷ 30 days;
- **trade:** trade activity ÷ 600;
- **output:** last day's production value per 100 people ÷ 40;
- **employment:** workers ÷ workforce;
- **housing:** 100 up to 90% of housing capacity, falling to 0 at 120%;
- **coin:** treasury cover ÷ 60 days of upkeep.

Targets:

- **Prosperity target** = 30% food + 15% goods + 20% trade + 35% output − shortage penalty.
- **Stability target** = 35% food + 20% economic security ((goods + coin) / 2) + 15% housing + 20% prosperity + 10% employment − shortage penalty − starvation penalty (15).

The shortage penalty is 10 for a food shortage, 5 per other resource in shortage, and +2 per
day without food (up to 20). Each rating moves toward its target by at most 1 point a day.
The target is stored, so the inspector shows "rising toward" or "falling toward".

## Population

A fed settlement (no food shortage, no unmet demand) with stability ≥ 40 and below housing
capacity grows by (2.4% + prosperity/1000) a year. Stability below 25, or occupancy above
110%, makes people leave at 3% a year. Fractions carry over between days.

## Chronicle (structured events)

Every event has a stable ID, date, type, involved settlement IDs (primary first), key/value
facts, a severity (tone) and an importance. The simulation records facts only;
`EventFormatter` (Application) writes the sentences. Milestones are reported only when
something moved meaningfully: ratings by 10 points, population by 5%, the treasury or
lifetime production passing 1,000 / 2,500 / 5,000 / 10,000 …
