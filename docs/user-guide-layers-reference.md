# Layers Reference

## Overview

The **Layers** system in CAIME works like the layer system in image editors such as Photoshop or GIMP — each layer holds a different type of map data, and you can show or hide any of them independently. Think of each layer as a transparent sheet of tracing paper stacked on top of your map: the **Ground Types** sheet paints the terrain colours, the **Rivers** sheet draws water on top, the **Roads** sheet draws paths on top of that, and so on. You paint on one sheet at a time while all the others remain intact underneath.

Layers are the central concept in CAIME. Everything you paint, import, or validate belongs to a specific layer. Understanding how they work will make your modding workflow much faster and less error-prone.

---

## Table of Contents

- [The Layers Panel](#the-layers-panel)
- [Layer Actions (Quick Buttons)](#layer-actions-quick-buttons)
  - [All Layers — Swatches Buttons](#all-layers--swatches-buttons)
  - [Ground Types — Align Ground/Region Type & Validate](#ground-types--align-groundregion-type--validate)
  - [Impassable — Plug Holes & Validate](#impassable--plug-holes--validate)
  - [Town Sprawl — Align Town Sprawl/Slot & Validate](#town-sprawl--align-town-sprawlslot--validate)
  - [Region Borders — Auto-generate](#region-borders--auto-generate)
  - [Roads, Rivers, Bridges, Beaches, Town Slots — Validate](#roads-rivers-bridges-beaches-town-slots--validate)
- [Layer Reference Table](#layer-reference-table)
- [Importing Layer Data](#importing-layer-data)
- [Exporting Layer Data](#exporting-layer-data)
- [Validating a Layer](#validating-a-layer)
  - [Quick Validate](#quick-validate)
  - [Validate via the Menu](#validate-via-the-menu)
  - [Validator Reference](#validator-reference)
- [Pro-Tips & Troubleshooting](#pro-tips--troubleshooting)

---

## The Layers Panel

The **Layers** panel lives in the sidebar on the right side of the screen, inside a collapsible section labelled **Layers**. Click the **Layers** header to expand or collapse it. The diagram shows every layer in its built-in order; your own order may differ if you have [re-arranged the layers](#re-arranging-layers).

```
┌─────────────────────────────────┐
│  ▼ LAYERS                       │
│ ┌─────────────────────────────┐ │
│ │ ☑  ● Impassable           ⋮ │ │  ← Visible + Active (selected for painting)
│ │ ☐  ○ Trade Routes         ⋮ │ │  ← Hidden (Rome 2, Attila, Thrones of Britannia and Three Kingdoms only)
│ │ ☐  ○ Roads                ⋮ │ │
│ │ ☐  ○ Town Slots           ⋮ │ │
│ │ ☐  ○ Town Sprawl          ⋮ │ │
│ │ ☐  ○ Bridges              ⋮ │ │
│ │ ☐  ○ Rivers               ⋮ │ │
│ │ ☐  ○ Beaches              ⋮ │ │
│ │ ☐  ○ Restrictions         ⋮ │ │  (not shown for Rome 2)
│ │ ☐  ○ Region Borders       ⋮ │ │  (not shown for Rome 2)
│ │ ☐  ○ Areas Of Interest    ⋮ │ │  (Warhammer 3 / Three Kingdoms only)
│ │ ☑  ○ Regions         60%  ⋮ │ │  ← Opacity below 100%; the ⋮ dots are the drag handle
│ │ ☐  ○ Attritions           ⋮ │ │
│ │ ☐  ○ Climates             ⋮ │ │
│ │ ☑  ● Ground Types         ⋮ │ │  ← Default active layer on startup
│ └─────────────────────────────┘ │
│                  [Reset order]  │  ← Restores the built-in order
└─────────────────────────────────┘
```

### Each layer row has these controls:

| Control | What it does |
|---|---|
| **Checkbox** (left side) | Toggles the layer's **visibility** on the canvas. Check it to show the layer; uncheck to hide it. Layers higher in the list are drawn on top. |
| **Radio button / name** (middle) | Sets this layer as the **active layer** — the one you are currently painting on. Clicking anywhere on the row other than the eye icon and the **⋮** dots does the same. Only one layer can be active at a time. The active layer row turns light-coloured to distinguish it. |
| **Percentage** (right side, only when below 100%) | The layer's [opacity](#layer-opacity). |
| **Three dots ⋮** (right side) | The drag handle. Drag the dots to [re-arrange the layers](#re-arranging-layers); dragging anywhere else on the row does nothing. |

> **Important:** A layer does not need to be visible to be active. You can paint on a hidden layer. However, the painted colour will only appear on screen once you make the layer visible.

### Re-arranging layers

The order of the list is the drawing order, and you can change it at any time while you work:

- **Drag and drop** a layer by its **⋮** dots onto another row. While you drag, a thick white line marks where the layer will land: directly above the row under the cursor. To move a layer to the very bottom, drop it on the lower half of the last row; the line then appears below that row.
- **Right-click** a layer row for **Move up**, **Move down**, **Move to top** and **Move to bottom**.
- The **Reset order** button under the list (or **Right-click → Reset layer order**) puts every layer back in its built-in order. The button is greyed out while the layers are already in that order.

The canvas and the minimap redraw as soon as the order changes. Re-arranging only changes what is drawn on top; it never changes your map data or exported files.

The order is remembered in your preferences (`%AppData%\CampaignMapToolkit\Caime\preferences.json`), separately for each game, because each game has its own set of layers. It is restored the next time you open or reload a project for that game, including after restarting CAIME.

### Layer opacity

Every layer has an opacity from 0% to 100%. A layer below 100% lets the visible layers underneath it show through, so you can, for example, see your Regions tinted over your Ground Types without switching between them.

- **Layer Opacity panel:** the collapsible **Layer Opacity** section, between **Actions** and **Layers** in the sidebar, controls the active layer. It shows the layer's name, a slider and the current percentage. The canvas follows the slider while you drag it; the arrow keys move it 1% at a time and Page Up/Page Down 10%.
- **Presets:** **Right-click** any layer row, then **Opacity** → **100% (reset)**, **75%**, **50%** or **25%**.
- A layer below 100% shows its percentage at the right of its row. At **0%** its name is dimmed: the layer is still visible and you can still paint on it, but it adds nothing to the canvas until you raise its opacity.

How it behaves:

- With every layer at 100%, the canvas looks exactly as it did before opacity existed.
- Hiding a layer hides it whatever its opacity, and showing it again keeps its opacity. Moving a layer in the stack keeps its opacity too.
- Where only see-through layers colour a hex, they are blended over the dark canvas background.
- Painting, erasing, undo and redo show the blended colour straight away, including on a layer that sits under a see-through one.
- The minimap shows the same blended colours as the canvas.
- Opacity does **not** reveal the background image: the background image is drawn over the hex grid, so use its own opacity slider in the toolbar for that.
- Blended colours can look like a different swatch's colour. When you need to read exact colours, set the layer back to 100% or hide the layers above it.

Opacity only changes what is drawn. It never changes your map data, your project file or exported files, and it is not part of undo/redo. Unlike the layer order, it is not remembered: every layer starts at 100% each time you open or reload a project.

---

## Layer Actions (Quick Buttons)

When you select a layer, the **Actions** panel (also in the sidebar, above the Layers panel) updates to show buttons specific to that layer type. These are shortcuts for the most common operations on that layer.

---

### All Layers — Swatches Buttons

Most swatch-based layers (Regions, Attritions, Climates, Areas Of Interest) show a row of **swatch management** buttons in the Actions panel, plus a **Validate** button:

```
[ Create ]  [ Rename ]  [ Remove ]  [ Cleanup ]  [ Validate ]
```

| Button | What it does |
|---|---|
| **Create** | Adds a new colour swatch that you can paint with on this layer. |
| **Rename** | Renames the currently selected swatch in the Swatches dropdown. |
| **Remove** | Deletes the currently selected swatch. |
| **Cleanup** | Scans the layer and removes any swatches that are not used anywhere on the map. Useful to keep your swatch list tidy after heavy editing. |
| **Validate** | Runs the validator for the currently active layer (see [Validating a Layer](#validating-a-layer)). Areas Of Interest has no validator, so this button just reports that no validation is available for it. |

---

### Ground Types — Align Ground/Region Type & Validate

When the **Ground Types** layer is active, two additional buttons appear below the swatch buttons:

```
[ Align Ground/Region Type ]   [ Validate ]
```

**What it does:** **Align Ground/Region Type** automatically fills the entire Ground Types layer to match your Regions layer. Every hex that belongs to a land region is given a land ground type, and every hex that belongs to a sea region is given a sea ground type. This is a great starting point when you have finished painting your regions and need to quickly set up ground types before fine-tuning them manually. **Validate** runs the [Ground Types Validator](#ground-types-validator).

---

### Impassable — Plug Holes & Validate

When the **Impassable** layer is active, the Actions panel shows:

```
[ Validate ]   [ Plug Holes ]
```

**What it does:** **Plug Holes** scans the map for any single-hex-wide gaps in the impassable terrain and automatically fills them in. This prevents AI armies from slipping through tiny one-hex corridors that should be blocked by mountains or cliffs. **Validate** runs the [Impassable Validator](#impassable-validator).

---

### Town Sprawl — Align Town Sprawl/Slot & Validate

When the **Town Sprawl** layer is active, the Actions panel shows:

```
[ Align Town Sprawl/Slot ]   [ Validate ]
```

**What it does:** **Align Town Sprawl/Slot** places Town Sprawl automatically underneath every Town Slot hex on the map. For later games (Warhammer, Troy, Three Kingdoms), it also removes any Town Sprawl that is not sitting under a Town Slot hex, keeping the two layers perfectly in sync. **Validate** runs the [Town Sprawl Validator](#town-sprawl-validator).

---

### Region Borders — Auto-generate

When the **Region Borders** layer is active, the Actions panel shows:

```
[ Auto-generate ]
```

**What it does:** Automatically calculates and draws the region border outlines based on where different regions meet on the Regions layer. Region Borders has no validator, so no Validate button appears here.

---

### Roads, Rivers, Bridges, Beaches, Town Slots — Validate

These five layers have no layer-specific painting shortcut, so their Actions panel shows only:

```
[ Validate ]
```

**What it does:** Runs that layer's validator — see [Roads Validator](#roads-validator), [Rivers Validator](#rivers-validator), [Bridges Validator](#bridges-validator), [Beaches Validator](#beaches-validator), or [Town Slots Validator](#town-slots-validator).

---

## Layer Reference Table

The table below lists every available layer, what data it holds, and which games include it.

| Layer | What it paints | Available in |
|---|---|---|
| **Impassable** | Hexes that armies cannot enter (mountains, cliffs, deep water barriers). | All games |
| **Trade Routes** | The sea and land trade route network for the campaign. | Rome 2, Attila, Thrones of Britannia, Three Kingdoms |
| **Roads** | Road hexes that improve army movement speed. | All games |
| **Town Slots** | The settlement hexes inside each region (main slots, minor slots, and port slots). | All games |
| **Town Sprawl** | The surrounding area hexes that belong to a settlement cluster. | All games |
| **Bridges** | Bridge hexes placed over river or sea gaps to allow crossing. | All games |
| **Rivers** | River hexes that form the flowing water network on land. | All games |
| **Beaches** | Coastal landing hexes where amphibious armies can come ashore. | All games |
| **Region Borders** | The painted outline borders between neighbouring regions. | Attila, Thrones of Britannia, Warhammer, Warhammer 2, Warhammer 3, Three Kingdoms, Troy, Pharaoh, Pharaoh Dynasties |
| **Regions** | The coloured territory zones that define which faction controls which land and sea areas. | All games |
| **Attritions** | Areas that cause attrition damage to armies moving through them (e.g. desert heat, arctic cold). | All games |
| **Climates** | The climate zone assigned to each hex (affects army movement and campaign effects). | All games |
| **Ground Types** | The terrain type of each hex (grass, forest, desert, sea, etc.). This is the default active layer when you open a project. | All games |
| **Restrictions** | Hexes with movement restrictions (e.g. areas that certain unit types cannot enter). | Attila, Thrones of Britannia, Warhammer, Warhammer 2, Warhammer 3, Three Kingdoms, Troy, Pharaoh, Pharaoh Dynasties |
| **Areas Of Interest** | Special marked hexes used for unique in-game events or mechanics. | Warhammer 3, Three Kingdoms |

> Layers that do not apply to your selected game will not appear in the Layers panel.

---

## Importing Layer Data

You can load a previously exported layer file (`.hex_layer` binary) and apply it to any layer in your current project.

**Steps:**

1. Go to **Tools > Import > Layer data**.
2. The **Import layer data** window will appear.
3. Click **Browse...** to open a file browser and locate your `.hex_layer` file.
4. Once the file is loaded, the **Layer** dropdown becomes available. Use it to choose which layer in your project should receive the imported data.
5. Click **Confirm** to apply, or **Cancel** to discard.

> **Note:** The **Confirm** button is greyed out until a valid file has been selected. If it remains greyed out after browsing, make sure the selected file is a valid `.hex_layer` export from CAIME.

---

## Exporting Layer Data

You can export one or more layers as image files (for reference or sharing) or as binary `.hex_layer` files (for backup or re-importing later).

**Steps:**

1. Go to **Tools > Export > Layer data**.
2. The **Export layers to images** window will appear.
3. Check the box next to each layer you want to export:
   - Export ground types layer
   - Export rivers layer
   - Export climates layer
   - Export attritions layer
   - Export regions layer
   - Export region borders layer
   - Export beaches layer
   - Export bridges layer
   - Export town sprawl layer
   - Export town slots layer
   - Export roads layer
   - Export trade routes layer
   - Export impassable layer
4. Use the **Export Mode** dropdown to choose the output format:
   - **To image** — saves the layer as a PNG image you can open in any image editor.
   - **To binary** — saves the layer as a `.hex_layer` binary file that can be re-imported into CAIME.
5. Click **Export** to save the files, or **Cancel** to close without exporting.

---

## Validating a Layer

The validator checks your painted layer data for common mistakes — like rivers that end in the sea, roads that connect to nothing, or town slots that are too small. After the check finishes, a pop-up tells you whether everything is clean or if there are issues, and any problems are listed in the **Logger** window (open it with **Ctrl+L**).

### Quick Validate

Every layer that has a validator also has a **Validate** button in the **Actions** panel (the panel that shows layer-specific tools when a layer is selected in the **Layers** list). Click it to run that layer's validator immediately, without going through the **Tools** menu.

### Validate via the Menu

1. Make sure your project is open and painted.
2. Go to **Tools > Validate**.
3. Click the name of the layer you want to check.
4. A pop-up window will appear saying either:
   - **"No issues have been found during [Layer] layer validation."** — the layer is clean.
   - **"Validating [Layer] layer failed. See output logs for details."** — open the Logger (**Ctrl+L**) to read what went wrong.

---

### Validator Reference

Each validator checks for a specific set of rules. The table below explains what each one looks for and how to interpret the results.

---

#### Rivers Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| River flag does not match its connection to neighbours | Warning | A hex is marked as a river but is not connected to any adjacent river hex (or vice versa). | Make sure every river hex is adjacent to at least one other river hex, or remove the stray mark. |
| River hex is on a sea hex | Warning | A river is painted on top of a sea hex. | Rivers must be on land only. Repaint the hex as land ground type or remove the river mark. |
| River edge pointing off the map | Warning | A river hex at the map border has an edge pointing outside the map boundary. | Remove the river edge direction pointing off-map. |
| River edge pointing into a sea hex | Warning | A river hex has an edge that connects to a neighbouring sea hex. | Rivers must connect only to land hexes. |
| Isolated river hex | Info | A river hex has no river neighbours. This is allowed, but likely a mistake. | Connect it to a river network or remove it if unintentional. |

---

#### Roads Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| Road on impassable hex | Info | A road is painted on a hex also marked as impassable. This is technically allowed, but frowned upon. | Consider removing the road or the impassable mark. |
| Road on sea (not a bridge) | Warning | A road hex is on sea but is not part of a bridge. | Remove the road or convert it to a bridge. |
| Road on cliff or beach (not a bridge approach) | Warning | A road passes through a cliff or beach hex without being a bridge approach. | Add the bridge-cliff marking or reroute the road. |
| Road connects to nothing | Warning | A road hex has no connection to any other road in the edge mask. | Connect the road to another road hex or remove it. |
| Isolated road hex | Info | A road hex has no adjacent road neighbours. Allowed, but likely a mistake. | Connect it to a road network or remove it. |
| Three-way intersection | Info | A road hex has road neighbours on two directly adjacent sides, forming a T or Y junction. | This is allowed but can look messy in-game. Consider rerouting. |

---

#### Town Slots Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| Town slot index out of valid range | Error | A hex has a slot number that doesn't exist. | Repaint the hex with a valid slot index. |
| Town slot outside Town Sprawl | Warning | A town slot hex is not inside the Town Sprawl layer. | Run **Align Town Sprawl/Slot** in the Actions panel to fix this automatically. |
| Town slot is impassable | Info | A settlement hex is also marked impassable. Allowed but possibly a mistake. | Review whether the hex should be passable. |
| Non-port town slot on sea | Warning | A regular (non-port) settlement hex is in the sea. | Move the slot to a land hex, or change it to a port slot. |
| Town sprawl overlaps a region border | Warning | Two settlements from different regions have their sprawl hexes touching. | Shrink one or both sprawl areas so they don't cross the border. |
| Port slot without sea adjacency | Warning | A region has a port but none of its hexes border the sea. | Move the port to a region that touches the sea, or remove it. |
| Town slot wrong size | Warning | (Warhammer games only) A settlement cluster is not the required 19 or 16 hexes. | Resize the slot to meet the requirement. |
| Slot needs a fully-surrounded centre hex | Warning | (Classic games) No single hex within the slot has all 6 of its neighbours also in the same slot. | Ensure the settlement has a proper "core" hex surrounded by same-slot hexes. |
| Disconnected/duplicate slot clusters | Warning | The same slot index appears as two separate unconnected groups of hexes in one region. | Remove the duplicate cluster; each slot should be one continuous group. |
| Minimum slot size not met | Warning | (Classic games) A slot has fewer than 7 hexes total. | Expand the settlement to at least 7 hexes. |
| Region has no town slots but is not fully impassable | Warning | A passable land region has no settlement. | Add at least one town slot, or mark the entire region impassable if it is a wasteland. |

---

#### Bridges Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| Bridge on a land hex | Error | A bridge mark is placed on a land hex (bridges must span water). | Remove the bridge mark or change the hex to sea. |
| Bridge does not connect two separate land sides | Warning | The bridge leads to only one land bank, or nowhere. | Ensure the bridge has land hexes on both of its sides. |
| Bridge has no bridge-cliff hex | Warning | Neither bank of the bridge has the bridge-cliff land type marking. | Add the bridge-cliff ground type to the approach hexes on both sides. |

---

#### Beaches Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| Beach on a sea hex | Warning | A beach is painted on a hex flagged as sea. | Beaches must be on land. Change the hex to land or remove the beach mark. |
| Beach and cliff on same hex | Warning | A hex is marked as both beach and cliff simultaneously. | Remove one of the two marks — a hex cannot be both. |
| Beach with no neighbouring sea hex | Warning | A beach hex is completely surrounded by land. | Beaches must be adjacent to at least one sea hex. Move or repaint the beach. |
| Isolated beach hex | Info | A beach hex has no adjacent beach neighbours. Allowed, but possibly a mistake. | Connect it to a beach chain or remove it if unintentional. |

---

#### Regions Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| Region count mismatch between database and map | Error | The number of regions in the map does not match the database. | Add or remove regions in the database to match the map, or vice versa. |
| Region name mismatch | Error | A region in the map has a different internal name than the database entry at the same position. | Make sure the region names in the database and the map are in the same order. |
| Hex with no region assigned | Warning | A hex was not painted with any region colour. | Paint every hex with a region. No hex should be left blank. |
| Isolated region hex | Info | A hex belongs to a region but has no neighbouring hex in the same region. Allowed, but possibly a mistake. | Connect it to the region or remove it. |
| Sea region adjacent to more than 9 land regions | Warning | A sea region borders too many separate land regions, which causes AI slowdown. | Split the sea region into smaller sections. |
| Sea region adjacent to more than 5 land regions | Info | 5 is the recommended maximum for AI performance. | Consider splitting the sea region. |
| Empty region (zero hexes) | Warning | A named region in the database has no hexes assigned on the map. | Paint at least one hex for this region, or remove it from the database. |
| Non-contiguous land region | Warning | A land region's hexes form two or more disconnected islands. Sea regions may legitimately be split. | Merge the blobs or split the region into two separate regions. |
| Region has 0 passable region-edge hexes | Info | No hex along the region's border is passable, so armies may not be able to cross into it. | Check whether this is intentional (fully impassable region) or a painting mistake. |

---

#### Attritions Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| Attrition count mismatch between database and map | Error | The number of attrition types in the map does not match the database. | Synchronise the attrition list in the database with the map. |
| Attrition name mismatch | Error | An attrition type in the map has a different name than the database entry. | Ensure both lists are in the same order and use matching names. |
| Hex has an invalid attrition index | Error | A hex is painted with an attrition type that no longer exists. | Repaint the hex with a valid attrition type. |

---

#### Climates Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| Climate count mismatch between database and map | Error | The map references more or fewer climate types than the database defines. | Align the database climate list with the map. |
| Climate name mismatch | Error | A climate type in the map does not match the expected database name at that position. | Ensure the climate lists are in the same order. |
| Hex with no climate set | Warning | A hex has not been assigned any climate. | Paint every hex with a climate type. |
| Hex has an invalid climate index | Error | A hex references a climate that does not exist in the current list. | Repaint the hex with a valid climate. |

---

#### Ground Types Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| Hex has no ground type set | Warning | A hex is completely unpainted on the Ground Types layer. | Paint every hex with a ground type before exporting. |
| Hex has an invalid ground type index | Error | A hex references a ground type that no longer exists. | Repaint the hex with a valid ground type. |
| Sea ground type in a land region | Warning | A land-region hex is painted with a sea ground type (e.g. deep ocean colour). | Repaint it with a land ground type. |
| Land ground type in a sea region | Warning | A sea-region hex is painted with a land ground type. | Repaint it with a sea ground type. |
| Ground type contradicts the hex's land/sea flag | Warning | The hex is flagged as land but has a sea ground type, or vice versa. | Align the ground type with the hex's land/sea assignment. |
| Lone land hex surrounded entirely by sea | Warning | A single land hex is completely encircled by sea hexes, with no land neighbour. | This is usually a painting mistake. Remove the stray hex or add surrounding land. |
| Lone sea hex surrounded entirely by land | Info | A single sea hex (a tiny lake) is entirely inside land. Possibly intentional. | Remove it if it is not intentional. |
| Ground type count mismatch with database | Error | The total number of ground types in the map does not match the database. | Synchronise the ground type list in the database. |
| Ground type name or sea/land category mismatch | Error | A named ground type in the database does not match what the map expects at the same index. | Ensure the lists are in the same order and the sea/land categories match. |

---

#### Impassable Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| Town slot on impassable ground | Warning | A settlement hex is also marked impassable. | Remove the impassable mark from the slot's hexes, or relocate the slot. |
| Passable hole enclosed by impassable hexes | Warning | A passable hex is completely boxed in by impassable neighbours on every side, so units can never reach it. | Use **Plug Holes** in the Actions panel, or repaint the hex or its surroundings. |
| Isolated impassable hex | Info | An impassable hex has no impassable neighbours. Allowed, but may be a stray mark. | Review whether the hex is meant to be impassable. |

---

#### Town Sprawl Validator

| Issue | Severity | What it means | How to fix |
|---|---|---|---|
| Sprawl on impassable, river, or cliff terrain | Warning | A sprawl hex sits on terrain that shouldn't be buildable. | Remove the sprawl mark or the conflicting terrain. |
| Sprawl blob spans a different land region | Warning | A sprawl hex belongs to a different land region than the rest of its blob. Sprawl reaching over an adjacent **sea** region for a port is expected and not flagged. | Repaint the sprawl so it stays inside its own land region. |
| One region's sprawl split across multiple blobs | Warning | A single region's sprawl forms more than one disconnected patch. | Merge the patches, or confirm they really are separate settlements. |
| Sprawl blob has no town slot | Info | A sprawl blob contains no settlement slot hex at all. Some earlier games allow sprawl without a slot. | Add a town slot, or ignore if intentional for your game. |
| Hazard terrain within 2 hexes but not touching | Warning | Impassable/beach/river/cliff terrain comes within 2 hexes of the blob without ever bordering it directly. | Extend the sprawl to touch the terrain, or move them further apart (3+ hexes). |
| Sprawl blob bordered by more than one hazard patch | Warning | The blob touches two or more separate impassable/beach/river/cliff patches, pinching it into a bottleneck. | Reshape the sprawl or surrounding terrain so only one hazard patch borders the blob. |

---

## Pro-Tips & Troubleshooting

- **Always open the Logger before validating.** The pop-up dialog only tells you pass or fail — the Logger (**Ctrl+L**) gives you the exact hex coordinates and description of every issue found. Keep it open in a side window while you work.

- **Run validators in order, starting with Ground Types and Regions.** Many issues in other layers (Climates, Attritions, Town Slots) stem from problems in the foundational layers. Fixing those first often resolves cascading errors downstream.

- **If the Confirm button in the Import window is greyed out**, the selected file is not a valid CAIME layer export. Make sure you are selecting a `.hex_layer` file that was originally exported from CAIME using **Tools > Export > Layer data** in binary mode, not a plain image.

- **If the Validate menu items are greyed out**, you either do not have a project open, or your project has not been fully processed yet. Open a project file first, or check that the project loaded without errors.

- **Layers higher in the Layers panel are drawn on top of layers lower in the list.** For example, the Impassable layer starts at the very top, so making it visible will overlay it on everything else. Use the visibility checkboxes strategically to focus on one layer at a time without confusion, [re-arrange the layers](#re-arranging-layers) to bring the one you care about to the top, or lower a layer's [opacity](#layer-opacity) to see through it.
