# Phase 27: performance

**Conditions.**
- Unity 6000.0.47f1 editor, **batch mode**, `Phase27PlayModeTests`.
- **No development or player build was made.** None of these figures are player-build numbers.
- Bramhaven, the largest test town (1,644 inhabitants, 71 buildings), at noon, standing by the square.

## Population tiers

| Measure | Result |
|---|---|
| Planned out at noon (abstract tier) | 100 unnamed + 6 watch |
| Shown (detailed tier) | 32 (the cap) within 70 m |
| At 22:00 | 13 out, 8 shown |
| Plan for the hour (routes for everyone) | 23–38 ms, once an hour (game time) and on arrival |
| Townsfolk update per frame | 0.12 ms mean, 0.25 ms worst (positions of the shown, every 0.3 s nearest selection) |
| Figure build | 1.4 ms worst each; at most two each 0.3 s |
| Figures built in the first minute | 41 (cache 48; reused per person) |

## Frame time by the square at noon (240 frames each)

| | Mean | Worst |
|---|---|---|
| With townsfolk | 1.82–1.83 ms | 14.2–18.8 ms (varies run to run) |
| Without (switched off) | 1.22 ms | 1.8–2.4 ms |

**The worst frame.** The townsfolk update itself never exceeded 0.25 ms, and figure builds 1.4 ms. The worst-frame difference is most likely the 32 extra animated, skinned figures being rendered and animated in the editor, and was not profiled further. **The owner accepted this worst-frame figure (2026-11-14).**

**Earlier runs, for comparison:**
- With six builds per tick: 12.6–14.3 ms worst.
- Mean: 1.35–1.61 ms with townsfolk, against 1.08–1.21 ms without.

## Other measures

| Measure | Result |
|---|---|
| Entering the town (layout, build with dressing and herds, residents, first frames) | about 1.65–1.70 s (editor) |
| Dressing | 58 items planned, 70 objects (banners back to back), 42 with colliders |
| Animals | 39 in the town's pastures; each wakes within 90 m of the camera and checks once a second |
| Allocated memory after the test | about 1.1 GB (editor process; not comparable to a build) |

## Not measured

- A development or player build.
- Draw calls and GPU time.
- The interior generator's time (it runs once per building, on first entry).
- The smallest and medium settlements' frame times. Their planned counts are 1–45 people, below the largest town's.
- The local map refresh with townsfolk (townsfolk are not on the map).
