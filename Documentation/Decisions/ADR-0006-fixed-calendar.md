# ADR-0006: Fixed 360-day calendar

**Decision.** 12 months × 30 days, 7-day weeks, years starting at 1, with original month
names (Deepwinter … Longnight). Advance week = 7 days; advance month = 30 days. The default
start date is 1 Seedtide 412.

**Consequences.** Date arithmetic is exact integer maths (`ToDayNumber` / `FromDayNumber`).
There are no leap rules and no time-zone or culture dependence. "Advance month" always moves
the date by exactly one calendar month.

**Alternatives.** The Gregorian calendar via `DateTime`: carries culture and time-zone
behaviour that has no meaning in a fantasy world.
