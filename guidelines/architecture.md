# Architecture

## MVVM

CAIME follows Model-View-ViewModel:

- **Model** — data. `CAIME/Models/`, `CAIME/Classes/MapHex/`, and the
  `Classes/` subfolders that represent map data and file formats.
- **View** — UI. `CAIME/Views/`, XAML and its code-behind. A view displays
  state and forwards input; it does not contain business logic.
- **ViewModel** — business logic. `CAIME/ViewModels/`. A view model exposes the
  state and commands a view binds to, and contains no direct UI code.

Keep logic out of code-behind and out of models. If a view's code-behind is
making a decision rather than wiring up the view, that decision belongs in a
view model.

## KISS and SOLID

Prefer the simplest design that solves the problem in front of you. Do not
add abstraction, configurability, or indirection for a need you don't have
yet.

Within that, follow SOLID:

- **Single responsibility** — a class has one reason to change. A tool, a
  generator, an exporter, a view model: each does one job.
- **Open/closed** — extend behaviour by adding a new tool, painter, or
  generator, not by branching existing ones on type checks.
- **Liskov substitution** — a subclass must honour the contract its base type
  promises. If it can't, it shouldn't be a subclass.
- **Interface segregation** — depend on the narrow interface you actually use,
  not a broad one that drags in members you don't need.
- **Dependency inversion** — depend on an abstraction (an interface, a base
  class) rather than reaching into a concrete implementation, where a real
  seam is needed.

These are tools for keeping change local and cheap, not a checklist to satisfy
for its own sake. Do not apply a pattern that adds indirection nobody needs;
see [Coding style](coding-style.md) on leaving code outside your change alone.

## Design patterns

Reach for a design pattern when it is the well-known, best fit for the problem
in front of you — not by default, and not because a class could theoretically
grow a hierarchy of options someday. A pattern is justified by a real problem
it solves today, not by a speculative future one.

Patterns already earning their place in this codebase:

- **Command** — `RelayCommand` and `ToolCommands` wrap a UI action as an
  object, which is what lets `ICommand` bindings and undo/redo work the same
  way for every action.
- **Strategy** — the `Tool` hierarchy in `CAIME/Models/Editor/Tools/`, the
  painters in `CAIME/Classes/Editor/Painters/`, and the generators in
  `CAIME/Classes/Exporters/Pathfinding/Generators/` each vary one algorithm
  behind a shared shape, so the editor and the exporter can pick one without
  knowing its details.
- **MVVM's own Observer** — data binding between a view and a view model is
  the Observer pattern; see [MVVM](#mvvm) above. Nothing further is needed to
  get change notification working.

Before adding a new one, check whether it solves a problem you actually have:

- Adding a pattern to a class with one implementation and no planned second
  one is premature abstraction — plain code is simpler and just as correct.
- A `Factory` is worth it when construction itself is a decision (which
  exporter for which game, which tool for which hotkey); it is not worth it
  for `new` on a single concrete type.
- A `Singleton` hides a dependency instead of declaring it, and makes testing
  harder. Prefer passing the instance in.
- If a pattern's abstraction and its benefit are getting hard to explain in
  a sentence, that is a sign it doesn't fit — see KISS above.

## Data layout in hot paths

The processing unit walks every hex, tile, and region in a map, often more
than once per export. In that code:

- Favour cache-friendly, contiguous data layouts — arrays and structs laid out
  for sequential access — over collections of small heap-allocated objects.
- Avoid unnecessary allocation, boxing, and indirection inside a hot loop.
- This applies to hot paths: exporters, generators, and anything that iterates
  the full hex grid. It is not a mandate to micro-optimise code that runs
  once, like startup or a dialog handler.

Changes here are still bound by
[Output compatibility](output-compatibility.md): a faster layout must produce
the same bytes.

## Components

CAIME decomposes into these components. A change belongs to the component it
touches; avoid reaching across components except through their existing
seams.

| Component | Responsibility | Where |
|---|---|---|
| Editor | Hosts the editing experience: tools, sidebar, viewport, and the windows that tie them together. | `CAIME/Models/Editor/`, `CAIME/ViewModels/Editor/`, `CAIME/Views/Windows/` |
| Processing unit | Exports map data to game-ready files: `pathfinding.ppd`, `map_data.esf`, trade routes, borders, and the rest. | `CAIME/Classes/Exporters/` |
| Hex grid rendering / Viewport | Renders the hex grid and handles viewport brushes: pan, zoom, and camera. | `CAIME/Models/HexMap/`, viewport tools in `CAIME/Models/Editor/Tools/` |
| Painting unit | Painting and erasing brushes, and swatches. | `CAIME/Classes/Editor/Painters/`, `CAIME/Models/Editor/Tools/` |
| Project management unit | The map hex file, its database, undo/redo, saves, and backups. | `CAIME/Classes/MapHex/`, `CAIME/Models/Database/`, `CAIME/Classes/Common/UndoRedoManager.cs` |
| Logging unit | In-app logging and the logger window. | `CAIME/ViewModels/Common/LoggerViewModel.cs`, `CAIME/Views/Controls/Logger.xaml`, `CAIME/Views/Windows/LoggerWindow.xaml` |
| Settings | User preferences and settings. | `CAIME/ViewModels/Windows/` (preferences), `CAIME/Views/Windows/` |
| Help unit | EULA, About, and credits. | `CAIME/Views/Windows/` |
| Helpers | Cross-cutting utility code shared by the components above. | `CAIME/Classes/Common/` |

Where a folder doesn't yet match this table, treat the table as the target,
not as license to move unrelated code while making an unrelated change.
