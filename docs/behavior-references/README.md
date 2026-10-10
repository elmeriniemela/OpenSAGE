# Original-game behavior references

Keep comparisons small enough to turn into a regression fixture. Store recordings,
original executables and proprietary save/map files outside the repository; link a
local path or private artifact identifier. Synthetic fixtures and observations may
be committed. A reverse-engineering finding can support an explanation, but a
behavioral comparison still needs observable original-game results.

Copy this template for a specific mechanic or mission checkpoint:

```text
Reference ID:
Subsystem / feature:
Original game and exact patch/version:
OpenSAGE commit and build configuration:
Map / campaign / mission:
Difficulty / faction / general:
Seed (if available; otherwise state unknown):
Initial state / setup:
Exact player orders with timing:
Expected original-game state and time/tick:
Observed OpenSAGE state and time/tick:
Difference / tolerance:
External recording or artifact:
Public synthetic fixture / filtered verification command:
Acceptance status (observed / reproduced / regression covered / gameplay accepted):
```

For a future simulation scenario, also record the maximum tick budget, assertions,
object identifiers, originating scripts and the uninterrupted/save-reload state
hashes where applicable. Do not mark a reference accepted until the stated check
has actually been performed.
