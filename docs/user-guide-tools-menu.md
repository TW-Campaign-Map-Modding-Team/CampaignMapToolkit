# The Tools Menu

## Overview

The **Tools** menu is your modder's workbench — a collection of specialist editors, import/export utilities, and quality-control checks that sit alongside the main painting workflow. Think of it like the "professional tools" drawer in a workshop: most of the time you're painting with brushes, but when you need to fine-tune a border line, review a log of warnings, or make sure your town slots are correctly sized before shipping, everything you need is here.

---

## Table of Contents

- [Show Logger](#1-show-logger)
- [Border Editor](#2-border-editor)
- [Map Data Editor](#3-map-data-editor)
- [Shader Resolution Corrector](#4-shader-resolution-corrector)
- [Import → Layer Data](#5-import--layer-data)
- [Import → map_data.esf](#6-import--map_dataesf)
- [Export → SVG Borders](#7-export--svg-borders)
- [Export → Baseline Tilemap Image](#8-export--baseline-tilemap-image)
- [Export → Layer Data](#9-export--layer-data)
- [Validate → Rivers](#10-validate--rivers)
- [Validate → Town Slots](#11-validate--town-slots)
- [Validate → Roads](#12-validate--roads)
- [Validate → Bridges](#13-validate--bridges)
- [Validate → Beaches](#14-validate--beaches)
- [Validate → Regions](#15-validate--regions)
- [Validate → Attritions](#16-validate--attritions)
- [Validate → Climates](#17-validate--climates)
- [Validate → Ground Types](#18-validate--ground-types)
- [Validate → Impassable](#19-validate--impassable)
- [Validate → Town Sprawl](#20-validate--town-sprawl)
- [Pro-Tips & Troubleshooting](#pro-tips--troubleshooting)

---

## How to Use It

### 1. Show Logger

**Keyboard shortcut:** `Ctrl + L`

The Logger is a live message history window — like the "Output" panel in a video editor. Every time the application processes data, exports files, or detects a problem in a validator, it writes a timestamped message here. Messages are colour-coded by severity (info, warning, error).

**Steps:**

1. Click **Tools** in the top menu bar.
2. Click **Show Logger**.
3. The **Logger messages stack** window will open.
4. Read through the list of messages. Warnings and errors appear in different colours to help you spot issues quickly.
5. To copy messages elsewhere (e.g. to paste into a bug report), select one or more lines and press **Ctrl+C**, or right-click for a **Copy selected** / **Copy all** context menu. The **Copy all** button at the bottom of the window copies the entire log regardless of selection.
6. To clear old messages from the list, click the **Clear** button at the bottom of the window.
7. The window can stay open in the background while you continue working — it updates automatically.

> **Tip:** Always open the Logger before and after running any of the Validate or Process operations. If something went wrong, the reason will be written here.

---

### 2. Border Editor

**Keyboard shortcut:** `Ctrl + B`

> **Note:** This option is only available when a project is open with borders data ready to export.

The Border Editor is a precision tool for manually fine-tuning the invisible lines that the game uses to separate regions from each other. Think of region borders like the stitching on a quilt — the Border Editor lets you move individual stitching points by exact pixel coordinates. It opens in its own separate window.

```
┌────────────────────────────────────────────────────────────────────┐
│  Border editor  │ File │ Border part │ Border point │ Visualization │
├────────────────┬──────────────────────────┬────────────────────────┤
│ Source region  │                          │  Border point coords   │
│ [Select] [Clr] │                          │  X: [____] Y: [____]  │
│ ■ Region name  │   Visualization canvas   │                        │
│ ⇅ (swap)       │   (map preview)          │  Colour legend:        │
│ Target region  │                          │  Border: ■ [change]   │
│ [Select] [Clr] │                          │  Current point: ■     │
│ ■ Region name  │                          │  Other parts: ■       │
│                │                          │  Complementary: ■     │
│ Border part    │                          │  Source region: ■     │
│ ┌────────────┐ │                          │  Target region: ■     │
│ │ part 0 ... │ │                          │                        │
│ │ part 1 ... │ │                          │  [Status message]      │
│ └────────────┘ │                          │                        │
└────────────────┴──────────────────────────┴────────────────────────┘
```

**Loading an existing borders file:**

1. Click **Tools** → **Border Editor** (or press `Ctrl + B`).
2. The Border Editor window opens. The status bar at the bottom-right will read *"You have to load a PBD file first!"* in red.
3. Inside the Border Editor, click **File** → **Load**.
4. A Windows file picker will appear — navigate to your `.pbd` file and click **Open**.
5. The status bar will change to *"Loaded PBD file successfully"* in white when done.

**Selecting the two regions whose border you want to edit:**

6. Under **Source region**, click **Select**.
7. A region selection panel will appear — pick the first region (e.g. the region on the left side of the border).
8. Under **Target region**, click **Select** and pick the second region.
9. A colour swatch and the region name will appear next to each label confirming your selection.
10. To swap Source and Target quickly, click the small **⇅ invert** button between the two selectors.

**Working with Border Parts:**

A single border between two regions can be made up of several separate segments called *border parts* (e.g. a river might split the border into two separate stretches).

11. Once both regions are selected, the **Border part** list on the left will fill with the existing parts.
12. Click a part in the list to select it — the visualization canvas in the middle will highlight that part.
13. To add a new part, click **Border part** → **Add** in the top menu of the Border Editor.
14. To delete the selected part, click **Border part** → **Delete**.
15. To reverse the direction of the border (swap which side is "from" and which is "to"), click **Border part** → **Change border facing**.

**Working with Border Points:**

Each border part is made of individual coordinate points that trace the line across the map.

16. With a border part selected, the **Border point** list on the right side fills with all the points in that part (shown as `Border point 0 (X|Y)`).
17. Click a point in the list to select it — its X and Y coordinates appear in the top-right input boxes.
18. To **move** a point, type new values into the **X** and **Y** boxes, or use the small **▲ ▼** arrow buttons next to each field to nudge the point by one unit at a time.
19. To **insert** a new point, click **Border point** → **Insert**. If no point is selected, the new point is inserted before the first existing one. If a point is selected, the new point is inserted immediately after it.
20. To **delete** a point, click **Border point** → **Delete**.
21. To deselect the current point without deleting it, click **Border point** → **Unselect**.

**Refreshing the preview:**

22. If the visualization canvas looks out of date after edits, click **Visualization** → **Refresh**.
23. The colour swatches in the bottom-right panel show what each element looks like in the preview. Click any **Change colour** button to adjust these preview colours — this only affects how things look in the editor, not the actual game data.

**Saving your work:**

24. When finished, click **File** → **Save** inside the Border Editor.
25. A Windows file picker will appear — choose a location and filename, then click **Save**.
26. The status bar will confirm *"Saved PBD file successfully"*.
27. To close the Border Editor, click **File** → **Exit** or press the **✕** button in the top-right corner of the window.

---

### 3. Map Data Editor

**Keyboard shortcut:** `Ctrl + M`

The Map Data Editor is a targeted editor for the game's `map_data.esf` file. Specifically, it lets you control how many **settlement building slots** each region has, and whether a region has a port slot. This is important for Rome 2 and similar games where you need to duplicate settlement slot entries to increase a region's building capacity.

> **Note:** This tool is only supported for certain Total War titles (Rome 2, Attila, Thrones of Britannia). If you open a file from a game that does not require slot duplication (e.g. Warhammer 3), the editor will show an error message and refuse to load the file.

```
┌───────────────────────────────────────────────┐
│ Map Data Editor                               │
├───────────────────────────────────────────────┤
│ File: [path/to/map_data.esf    ]  [Browse]   │
│       [Apply config file] [Create config file]│
├───────────────────────────────────────────────┤
│  region_name_1    Slots: [3 ▲▼]  Port: [✓]  │
│  region_name_2    Slots: [2 ▲▼]  Port: [ ]  │
│  region_name_3    Slots: [4 ▲▼]  Port: [✓]  │
│  ...                                          │
├───────────────────────────────────────────────┤
│                              [Save changes]   │
└───────────────────────────────────────────────┘
```

**Steps:**

1. Click **Tools** → **Map Data Editor** (or press `Ctrl + M`).
2. The Map Data Editor window opens.
3. Click **Browse** next to the **File:** field.
4. A Windows file picker will open — navigate to your `map_data.esf` file and click **Open**.
5. The list in the centre of the window will populate with all the regions that have settlement information.
6. For each region, you can change the **number of slots** (building queue slots) using the spinner control next to it.
7. The **Port** checkbox indicates whether the region has a port slot. This is read from the file automatically.
8. When you are happy with your changes, click **Save changes**.
9. A confirmation dialog will ask *"Do you confirm you want to save the changes?"* — click **Yes** to write the file.

**Using config files (optional — saves time on repeat edits):**

10. After loading a `map_data.esf` and adjusting slots, click **Create config file** to save your slot configuration as a reusable `.json` file.
11. On a future session, load the same `map_data.esf`, then click **Apply config file** and select the saved `.json` to restore all your slot settings in one click.

---

### 4. Shader Resolution Corrector

> **Note:** Only available for Total War: Three Kingdoms projects.

Patches the campaign UI-overlay resolution that is baked directly into a compiled shader (`.fxc`), so you can change it without a runtime injector. The overlay has two variants — a normal one and an `enable_shadows` one, used depending on the player's shadow setting — so this tool can patch both in a single run.

**Steps:**

1. Click **Tools** → **Shader Resolution Corrector**. The **New** width/height fields are pre-filled from the currently open map's size, but can be edited.
2. Click **Browse...** next to **Shader file** and select the compiled shader to patch.
3. The **Current** width/height fields are filled in automatically from the shader if a resolution can be detected — otherwise, enter them yourself.
4. *(Optional)* Click **Browse...** next to **Shader file (enable_shadows)** to add the second variant. Use **Clear** to remove it again.
5. Check the **New** width and height — these apply to both files. All four fields only accept whole numbers.
6. Click **Browse...** next to **Save patched shader(s) to** and choose the destination folder. Each patched file keeps its original name so it can be repacked in place.
7. Click **Run**.
8. A confirmation message reports, for each file, how many times each value was replaced and where it was saved.

> **Tip:** If you only provide one shader file, remember its sibling variant still needs the same change for it to work in every case — come back and patch it too before repacking.

---

### 5. Import → Layer Data

> **Note:** Available only when a project with pathfinding data is open.

This tool lets you bring in a previously exported layer binary file (`.hex_layer`) and apply it to the current map. It is the counterpart of **Export → Layer Data** — useful for transferring a layer you painted in one project into another, or for reverting a layer to a backed-up state.

**Steps:**

1. Click **Tools** → **Import** → **Layer data**.
2. The **Import layer data** window will appear.
3. Click **Browse...** to open a file picker.
4. Navigate to the `.hex_layer` file you want to import and click **Open**.
   - The application will automatically detect what type of layer the file contains (e.g. Rivers, Climates, Roads) and pre-select it in the **Layer** drop-down.
   - If the file resolution does not match your current map size, an error will appear in the Logger and the import will be cancelled.
5. If needed, use the **Layer** drop-down to manually choose which layer you want to overwrite with this data.
6. Click **Confirm** to apply the import, or **Cancel** to close without making any changes.

---

### 6. Import → map_data.esf

> **Note:** Available whenever a project can be opened. Works with every supported game.

Creates a new project from a `map_data.esf` by rebuilding the `map.hex` it was made from — useful when you have a map's `map_data.esf` but not its `map.hex`. The current project is closed first.

**Steps:**

1. Click **Tools** → **Import** → **map_data.esf**.
2. Navigate to the `map_data.esf` file and click **Open**. The file picker only shows files named `map_data.esf`.
   - Warhammer III and Three Kingdoms files are recognised on their own. The other games share file layouts (Rome II, Attila and Thrones of Britannia share one; Warhammer, Warhammer II, Troy and Pharaoh share another), so CAIME asks which game the file is from. Pick the right one: the game decides the `map.hex` version and how **Process → Map Data** writes the file.
3. The new project opens. The map is named after the folder the file is in.

See [Starting from a map_data.esf](user-guide-modifying-an-existing-map.md#starting-from-a-map_dataesf) for what the import recovers and what it leaves empty.

---

### 7. Export → SVG Borders

> **Note:** Available only when a project with borders data ready to export is open.

Exports your map's region borders as an **SVG vector image** file. SVG files can be opened in any vector graphics application (such as Inkscape or Adobe Illustrator) for visualisation, documentation, or artistic reference purposes.

**Steps:**

1. Click **Tools** → **Export** → **SVG Borders**.
2. A Windows file picker will appear — choose a save location and filename (the `.svg` extension will be used automatically).
3. Click **Save**. The file is generated and written to disk immediately, with a confirmation message in the Logger.

---

### 8. Export → Baseline Tilemap Image

> **Note:** Available only when a project is open.

Generates a **Baseline Tilemap** image — a flat colour image of the map that shows which ground type each hex belongs to, useful as a visual reference layer when creating the game's artistic map textures.

**Steps:**

1. Click **Tools** → **Export** → **Baseline Tilemap image**.
2. A Windows file picker will appear — choose a save location and filename.
3. Click **Save**. The image is written to disk and a confirmation appears in the Logger.

---

### 9. Export → Layer Data

> **Note:** Available only when lookup/minimap generation is enabled for your project.

Exports one or more of your painted layers as raw binary files (`.hex_layer`) or as image snapshots (PNG). This is useful for backing up your work, transferring layers between projects, or sharing individual layers with collaborators.

```
┌────────────────────────────────────────────────┐
│  Export layers to images                       │
├─────────────────────────────────┬──────────────┤
│ ☑ Export ground types layer     │  [PNG ▼]    │
│ ☑ Export rivers layer           │             │
│ ☐ Export climates layer         │             │
│ ☐ Export attritions layer       │             │
│ ☑ Export regions layer          │             │
│ ☐ Export region borders layer   │             │
│ ☐ Export beaches layer          │             │
│ ☐ Export bridges layer          │             │
│ ☐ Export town sprawl layer      │             │
│ ☐ Export town slots layer       │             │
│ ☐ Export roads layer            │             │
│ ☐ Export trade routes layer     │             │
│ ☐ Export impassable layer       │             │
├─────────────────────────────────┤             │
│                    [Export] [Cancel]           │
└────────────────────────────────────────────────┘
```

**Steps:**

1. Click **Tools** → **Export** → **Layer data**.
2. The **Export layers to images** window will open.
3. Tick the checkbox next to each layer you want to export. You can select as many as you need.
   - **Ground types** — terrain category painted on each hex
   - **Rivers** — river hexes
   - **Climates** — climate zones
   - **Attritions** — attrition zones
   - **Regions** — region ownership of each hex
   - **Region borders** — the border line layer
   - **Beaches** — beach hexes
   - **Bridges** — bridge hexes
   - **Town sprawl** — the town sprawl footprint
   - **Town slots** — individual settlement slot hexes
   - **Roads** — road hexes
   - **Trade routes** — trade route hexes
   - **Impassable** — impassable (no-go) hexes
4. Use the **format drop-down** on the right to choose the export format if applicable.
5. Click **Export**.
6. A Windows folder picker will appear — choose where to save the files.
7. Click **Cancel** to close the window without exporting.

---

## The Validate Submenu

The **Validate** submenu contains eleven independent checks, each scanning a different layer of your map for common mistakes. Think of them like spell-checkers for your map data — they do not modify anything, they only report problems. After every check, a pop-up will tell you whether the validation passed or failed, and detailed messages are written to the **Logger** (see [Show Logger](#1-show-logger)).

> **Tip:** Run validators in the order you painted your layers — ground types first, then rivers, roads, regions, town slots, etc. Fix any errors before running the next one, since some issues cascade into others.

---

### 10. Validate → Rivers

Checks every river hex on your map for consistency problems.

**Steps:**

1. Click **Tools** → **Validate** → **Rivers**.
2. Wait a moment while the map is scanned.
3. A dialog will appear:
   - **"No issues have been found during Rivers layer validation."** — you are good.
   - **"Validating Rivers layer failed. See output logs for details."** — open the Logger to read the specific warnings.
4. Common issues reported:
   - A river hex that does not connect to any neighbouring river hex (isolated river).
   - A river hex that is also flagged as sea (river-on-sea conflict).
   - A river hex whose edge connections point off the map or into a sea hex.

---

### 11. Validate → Town Slots

Checks the placement and size of every settlement slot (the cluster of hexes that represents a city or port).

**Steps:**

1. Click **Tools** → **Validate** → **Town slots**.
2. Wait for the scan to complete.
3. Common issues reported:
   - A town slot hex that is outside the painted town sprawl area.
   - A town slot with too few hexes (for most games, a slot needs at least 7 hexes).
   - A non-port town slot sitting on a sea hex.
   - A port slot in a region that does not touch any sea hex.
   - Town sprawl from two different regions overlapping at a border.
   - A region with no town slots but with passable land hexes (likely a missing slot).
   - A settlement slot that is split into two disconnected clusters (duplicate slot error).

---

### 12. Validate → Roads

Checks every road hex for placement and connectivity issues.

**Steps:**

1. Click **Tools** → **Validate** → **Roads**.
2. Common issues reported:
   - A road hex on sea (without being a bridge) — roads should stay on land.
   - A road hex on a cliff or beach that is not part of a bridge approach.
   - A road hex that is flagged as impassable (technically allowed, but usually a mistake).
   - A road hex that connects to no other road hex (isolated road, dead end).
   - A road hex creating a three-way intersection (allowed, but noted as a layout concern).

---

### 13. Validate → Bridges

Checks every bridge structure on the map.

**Steps:**

1. Click **Tools** → **Validate** → **Bridges**.
2. Common issues reported:
   - A bridge hex that is incorrectly flagged as land (bridges must sit on sea hexes).
   - A bridge that only connects to one land bank instead of two (a bridge that leads nowhere).
   - A bridge that has no "bridge-cliff" approach hex on either side (the transitional hex where road meets bridge).

---

### 14. Validate → Beaches

Checks every beach hex for correct placement.

**Steps:**

1. Click **Tools** → **Validate** → **Beaches**.
2. Common issues reported:
   - A beach hex that is also flagged as sea (beach must be land).
   - A beach hex that is also flagged as a cliff (beach and cliff conflict).
   - A beach hex with no adjacent sea hex (a beach that does not touch water).
   - An isolated beach hex (a single beach hex not connected to any other beach — allowed, but may be a mistake).

---

### 15. Validate → Regions

This is the most comprehensive validator. It checks the overall health and consistency of your region painting.

**Steps:**

1. Click **Tools** → **Validate** → **Regions**.
2. This check takes longer on large maps. Common issues reported:
   - A hex with no region assigned to it at all.
   - A mismatch between the number of regions in your project's database and the number painted on the map.
   - A region name in the database that does not match what is stored in the map file.
   - An isolated region hex — a lone hex of one region surrounded by hexes of another.
   - A land region that is split into two or more disconnected areas (a split region can cause serious startpos processing bugs — the two halves should be joined or made into separate regions).
   - A sea region with more than 9 adjacent land regions (this causes AI slowdown; consider splitting the sea region).
   - A region with zero passable border edge hexes (armies cannot enter or leave — double-check this).

---

### 16. Validate → Attritions

Checks that the attrition zone data painted on the map is consistent with the game's database.

**Steps:**

1. Click **Tools** → **Validate** → **Attritions**.
2. Common issues reported:
   - The number of attrition zones in the database does not match the number stored in the map file.
   - An attrition zone name in the database does not match what is stored in the map file (a name mismatch).
   - A hex assigned to an attrition zone index that is out of range (e.g. pointing to zone 5 when only 4 zones exist).

---

### 17. Validate → Climates

Checks that the climate zone data is consistent with the game's database.

**Steps:**

1. Click **Tools** → **Validate** → **Climates**.
2. Common issues reported:
   - The number of climates in the database does not match the number in the map file.
   - A climate name mismatch between the database and the map file.
   - A hex with no climate assigned at all.
   - A hex assigned to a climate index that is out of range.

---

### 18. Validate → Ground Types

Checks the terrain type (ground type) painted on every hex for consistency errors.

**Steps:**

1. Click **Tools** → **Validate** → **Ground types**.
2. Common issues reported:
   - A hex with no ground type assigned at all.
   - A hex assigned to a ground type index that is out of range.
   - A sea-category ground type used on a land-region hex (e.g. "ocean floor" painted over a land territory).
   - A land-category ground type used on a sea-region hex (e.g. "forest" painted over open water).
   - A hex flagged as land terrain but sitting inside a sea region, or vice versa.
   - A lone land hex completely surrounded by sea hexes (usually a stray painting mistake).
   - A mismatch between the ground type names in the database and those stored in the map file.

---

### 19. Validate → Impassable

Checks the impassable (no-go) layer for holes and inconsistencies.

**Steps:**

1. Click **Tools** → **Validate** → **Impassable**.
2. Common issues reported:
   - A town slot hex that is also marked impassable (a settlement cannot sit on impassable ground).
   - A passable hex that is completely boxed in by impassable neighbours on every side (a "hole" that units cannot walk into or out of).
   - An isolated impassable hex surrounded entirely by passable hexes (allowed, but may be a stray painting mistake).

---

### 20. Validate → Town Sprawl

Checks that every settlement's Town Sprawl footprint is a single, well-formed blob that stays inside its own region and terrain.

**Steps:**

1. Click **Tools** → **Validate** → **Town sprawl**.
2. Common issues reported:
   - A sprawl hex that is also flagged as impassable.
   - A sprawl hex belonging to a different (land) region than the rest of its sprawl blob — port sprawl reaching over an adjacent sea region is expected and not flagged.
   - A single region's sprawl split across more than one disconnected blob.
   - A sprawl blob with no town slot hex inside it at all (allowed in some games, but may be a mistake).
   - Impassable/beach/river/cliff terrain that comes within 2 hexes of a sprawl blob without ever touching it directly.
   - A sprawl blob bordering more than one separate impassable/beach/river/cliff area — this pinches the blob into a bottleneck and is not allowed.

---

## Pro-Tips & Troubleshooting

- **Run validators before every Process operation.** The validators are fast and catch problems that would otherwise result in a corrupt or broken export. Make it a habit to validate Rivers, Roads, Regions, and Town Slots before clicking anything in the **Process** menu.

- **The Logger is your first stop when anything fails.** If a validator shows *"failed — see output logs"*, open **Tools → Show Logger** immediately. Each problem is listed with the exact hex coordinates (column, row), so you can navigate directly to the problematic location on the map canvas.

- **The Border Editor requires a loaded PBD file before any editing is possible.** If the status bar shows red text saying *"You have to load a PBD file first!"*, use **File → Load** inside the Border Editor window — not the main application's File menu.

- **The Map Data Editor only supports specific games.** If you see an error message saying the feature is *"not supported"* after opening a `map_data.esf`, the file is from a game (such as Warhammer 3) where slot duplication is handled differently. The Map Data Editor is designed for Rome 2, Attila, and Thrones of Britannia.

- **Import → Layer Data will refuse files with mismatched map dimensions.** If you get an import error, make sure the `.hex_layer` file was exported from a map with the exact same width and height as your current project. Layer files from different-sized maps are not compatible.
