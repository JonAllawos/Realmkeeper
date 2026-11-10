# World time

Phase 5 gives the world a clock. The date (12 × 30-day calendar, ADR-0006) is unchanged; the world
now also has a **minute of the day**, `WorldData.MinuteOfDay` (0–1439), saved in schema v7.

## The model

- `WorldTime` (Domain, `Calendar/WorldTime.cs`): a date plus a minute. `Hour`, `Minute`,
  `AddMinutes`, `TotalMinutes`, `Clock(minute)` ("HH:MM").
- `DayPart`: Night, Morning, Midday, Afternoon, Evening. The ranges are data (`DayPartRules`):

  | Part | Starts |
  |---|---|
  | Morning | 06:00 |
  | Midday | 11:00 |
  | Afternoon | 13:00 |
  | Evening | 18:00 |
  | Night | 22:00 |

  `DayPartRules.Validate()` rejects ranges out of order.
- New worlds, and saves older than v7, start at **08:00** (`WorldTime.DefaultStartMinute`): a working
  morning, so shops are about to open and the light is good. Nothing about earlier saves says
  otherwise, so this is a default, not invented history.

## Advancing time

`WorldSession.Execute(AdvanceTimeCommand(minutes))`, 1 to 7 days' worth of minutes:

1. Each midnight crossed runs **one simulated day**: the same transactional, validated day as
   `AdvanceDaysCommand` (copy → simulate → validate → commit, ADR-0008). The daily systems are
   unchanged, so the economy, politics and the NPC engine keep their tested behaviour.
2. The minute is then set.
3. The player grows hungrier by the hour marks passed: −2 fed per hour, counted on absolute time.
   Many small steps therefore equal one large one exactly (a test compares 200 × 10 minutes with
   one 2000-minute step, byte for byte), and nothing hidden needs saving.
4. If a simulated day fails, the clock stops at that midnight.

The minute and hunger change in place (they cannot make the world invalid). Listeners get
`WorldChangeReason.TimeAdvanced`.

## Pace (ADR-0019)

`SimulationPlayback` has two paces:

| Where the player is | What a tick advances | Normal speed |
|---|---|---|
| On the realm map | whole days, as before; the time of day is left alone | 1 day per second |
| Walking a settlement (`LivingWorld = true`) | minutes | 2 game minutes per second (an hour in 30 s) |

Living-world speeds:

| Speed | Game minutes per second |
|---|---|
| Slow | 0.5 |
| Normal | 2 |
| Fast | 10 |
| Very fast | 60 |

- Normal is set so that a resident walking across town at 1.8 m/s arrives within the hour the
  world says they are there.
- A hitch is clamped to 120 minutes rather than replayed.
- In exploration the HUD shows the clock and has buttons for pause/play (P), speed, and "wait an
  hour" (T).

## Who uses the clock

- **People** (`NpcActivityPlan`, see `NPCs.md`): where everyone is, and what they do, at any minute.
- **Shops** (`Commerce.md`): opening hours; whether the keeper is at the counter.
- **Lighting** (`Exploration.md`):
  - sun path and golden hour;
  - moonlight;
  - sky, ambient light and haze;
  - lamps and lit windows.
- **The player**: hunger.
