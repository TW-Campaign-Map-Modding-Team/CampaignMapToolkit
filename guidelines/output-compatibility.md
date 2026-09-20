# Output compatibility

Everything that turns map data into game-ready files — pathfinding, borders,
trade routes, `map_data`, and every other exporter — must produce output byte
for byte identical to the reference output in `TestData/`. The game parses
these files by offset; a single byte out of place is a broken map, not a
cosmetic defect.

- No change may alter exporter output unless changing that output is the
  explicit point of the change.
- Refactoring, optimisation, and cleanup in this code must be output-preserving.
  Faster is welcome; different is not.
- A change that deliberately alters output must say so, explain why the previous
  output was wrong, and update the reference data in `TestData/` in the same
  change.
- Where the exporters reproduce a quirk of the original implementation, that
  quirk is the specification. Do not "fix" it because it looks wrong.
- Run the integration tests before opening a pull request. They are the only
  thing standing between a refactor and a corrupted campaign map.
