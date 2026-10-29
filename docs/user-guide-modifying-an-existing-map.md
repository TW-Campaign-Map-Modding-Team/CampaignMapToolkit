# Modifying an Existing Campaign Map

---

## Overview

Modifying an existing campaign map in CAIME is like editing a layer-based painting in a professional image editor. Some changes are purely about what you paint on the canvas — CAIME updates the map file and you re-process the outputs. Other changes also require you to update the **database** (your Assembly Kit's data tables edited in Dave), because the game engine and the startpos tool need the database to accurately reflect what is on the map.

This guide explains every type of modification, tells you exactly which database tables need to change, and specifies which processing steps to run afterward — including all game-specific differences you need to be aware of.

---

## Table of Contents

1. [The Two-Part Change System](#1-the-two-part-change-system)
2. [Quick Reference: What Each Modification Requires](#2-quick-reference-what-each-modification-requires)
3. [Detailed Walkthrough by Modification Type](#3-detailed-walkthrough-by-modification-type)
   - [Repainting Terrain, Climate, and Attrition (existing swatches)](#repainting-terrain-climate-and-attrition-existing-swatches)
   - [Adding, Renaming, or Removing a Terrain Type](#adding-renaming-or-removing-a-terrain-type)
   - [Adding, Renaming, or Removing a Climate](#adding-renaming-or-removing-a-climate)
   - [Adding, Renaming, or Removing an Attrition Type](#adding-renaming-or-removing-an-attrition-type)
   - [Impassable and Restrictions Changes](#impassable-and-restrictions-changes)
   - [Region Boundary Changes (Same Regions, New Shapes)](#region-boundary-changes-same-regions-new-shapes)
   - [Adding a New Region](#adding-a-new-region)
   - [Removing a Region](#removing-a-region)
   - [Renaming a Region](#renaming-a-region)
   - [Adding or Moving Settlement Slots](#adding-or-moving-settlement-slots)
   - [River Changes](#river-changes)
   - [Road Changes](#road-changes)
   - [Beach Changes](#beach-changes)
   - [Bridge Changes](#bridge-changes)
   - [Trade Route Changes](#trade-route-changes)
   - [Adding, Renaming, or Removing an Area of Interest](#areas-of-interest-changes)
   - [Resizing the Map](#resizing-the-map)
   - [Starting from a map_data.esf](#starting-from-a-map_dataesf)
4. [After Modifying: What to Re-Process](#4-after-modifying-what-to-re-process)
5. [Running Validators Before Processing](#5-running-validators-before-processing)
6. [Game-Specific Differences](#6-game-specific-differences)
   - [Rome II](#rome-ii)
   - [Attila](#attila)
   - [Thrones of Britannia](#thrones-of-britannia)
   - [Warhammer & Warhammer II](#warhammer--warhammer-ii)
   - [Warhammer III](#warhammer-iii)
   - [Three Kingdoms](#three-kingdoms)
   - [Troy](#troy)
   - [Pharaoh & Pharaoh Dynasties](#pharaoh--pharaoh-dynasties)
7. [Recommended Order of Operations](#7-recommended-order-of-operations)
8. [Pro-Tips & Troubleshooting](#8-pro-tips--troubleshooting)

---

## 1. The Two-Part Change System

Every modification you make falls into one of two categories:

```
┌─────────────────────────────────────────────────────────────────────┐
│        CATEGORY A — Repaint hexes with existing swatches            │
│                                                                      │
│  Paint in CAIME → Save → Re-process outputs → Done                  │
│                                                                      │
│  Applies whenever you are assigning swatches that ALREADY EXIST     │
│  to hexes: moving region borders, repainting terrain, changing      │
│  climates or attritions on hexes, painting roads, rivers, beaches,  │
│  bridges, impassable areas, restrictions, town sprawl, town slots.  │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│     CATEGORY B — Create, rename, or remove a swatch type            │
│                                                                      │
│  Update Dave (database) ↔ Use the Actions panel in CAIME →          │
│  Save → Re-process outputs → Done                                    │
│                                                                      │
│  The rule: if a layer shows Create / Rename / Remove buttons in     │
│  its Actions panel, then adding, renaming, or removing a swatch     │
│  for that layer ALWAYS requires a matching database change.          │
│                                                                      │
│  Layers with Create / Rename / Remove buttons:                      │
│    Ground Types, Climates, Attritions, Regions, Areas of Interest   │
└─────────────────────────────────────────────────────────────────────┘
```

> **The golden rule:** CAIME reads the database when you open or reload a map. It never writes to the database automatically. If your database and your painted map get out of sync, the validators will flag the errors — and the startpos tool will likely fail.

---

## 2. Quick Reference: What Each Modification Requires

**Repainting** means assigning an existing swatch to hexes — no database change is ever needed for that alone. **Swatch management** means using the **Create**, **Rename**, or **Remove** buttons in the **Actions** panel — that always requires a matching database change.

| What you're doing | Database change? | Which tables | Re-process steps |
|---|---|---|---|
| Repaint any layer with existing swatches | No | — | Depends on layer (see Section 3) |
| **Ground Types** — Create / Rename / Remove a type | **Yes** | `campaign_ground_types` | Pathfinding, Map Data |
| **Climates** — Create / Rename / Remove a climate | **Yes** | `climates` | Map Data |
| **Attritions** — Create / Rename / Remove an attrition | **Yes** | `campaign_map_attritions` | Map Data |
| **Regions** — Create a new region (swatch) | **Yes** | `regions`, `campaign_map_regions`, `region_to_province_junctions` | All outputs |
| **Regions** — Rename a region (swatch) | **Yes** | `regions`, `campaign_map_regions`, `region_to_province_junctions` | All outputs |
| **Regions** — Remove a region (swatch) | **Yes** | `regions`, `campaign_map_regions`, `region_to_province_junctions` | All outputs |
| **Areas of Interest** — Create / Rename / Remove | **Yes** | `campaign_map_areas_of_interest` | Map Data |
| Impassable, Restrictions, Rivers, Roads¹, Beaches, Bridges, Trade Routes, Town Slots, Town Sprawl, Region Borders | No | — | Depends on layer (see Section 3) |
| Resize map | No | — | All outputs |

¹ Road cost values live in `campaign_map_roads`. Painting road hexes needs no database change. Only add a row to `campaign_map_roads` if you are introducing an entirely new road cost entry for a campaign.

---

## 3. Detailed Walkthrough by Modification Type

---

### Repainting Terrain, Climate, and Attrition (existing swatches)

**What you're doing:** Switching hexes from one existing swatch to another — for example, changing a hex from "plains" to "forest", or from one climate zone to another. The set of available swatches does not change; you are only reassigning hexes.

**Database changes required:** None.

**Steps:**

1. Open your map in CAIME (**File → Open**, Ctrl+O).
2. In the **Layers** panel on the right, select the layer you want to edit (**Ground Types**, **Climates**, or **Attritions**).
3. Select the desired swatch from the **Swatches** dropdown.
4. Paint on the map canvas.
5. Save (**File → Save**, Ctrl+S).
6. Re-process: **Process → Pathfinding data** and **Process → Map Data** (Ground Types and Attritions); **Process → Map Data** only (Climates).

> **Important:** Ground type mismatches between the database and the map file will be caught by the validator. Run **Tools → Validate → Ground Types** after major terrain edits to confirm everything lines up.

---

### Adding, Renaming, or Removing a Terrain Type

**What you're doing:** Using the **Create**, **Rename**, or **Remove** buttons in the **Actions** panel while the **Ground Types** layer is active. This changes the list of terrain types that exists in the map file and must be mirrored in the database.

**Database table:** `campaign_ground_types` (global — applies to all maps of that game).

**To add a new terrain type:**

1. In Dave, add a new row to `campaign_ground_types`:

| Field | What to enter |
|---|---|
| `type` | A unique internal name for the terrain (e.g., `my_mod_terrain_ashfield`) |
| `is_sea` | `0` for land terrain, `1` for sea terrain |
| `movement_cost` | The movement cost for armies crossing this terrain |

2. Save in Dave.
3. In CAIME, with the **Ground Types** layer active, click **Create** in the **Actions** panel and type the same name you used in Dave.
4. The new swatch appears — paint it onto the relevant hexes.
5. Save (**Ctrl+S**) and re-process **Process → Pathfinding data** and **Process → Map Data**.

**To rename a terrain type:**

1. Rename the row in `campaign_ground_types` in Dave (change the `type` field).
2. Save in Dave, then reload the map in CAIME (**File → Reload**, Ctrl+R).
3. With the **Ground Types** layer active, select the swatch in the **Swatches** panel, then click **Rename** in the **Actions** panel and enter the new name.
4. Save and re-process.

**To remove a terrain type:**

1. Make sure no hexes are still painted with that terrain type (use the swatch's colour to identify them, then repaint them with an alternative swatch).
2. In CAIME, select the swatch and click **Remove** in the **Actions** panel. Alternatively, click **Cleanup** to automatically remove all swatches that are not used by any hex.
3. Remove the corresponding row from `campaign_ground_types` in Dave.
4. Save and re-process.

---

### Adding, Renaming, or Removing a Climate

**What you're doing:** Using the **Create**, **Rename**, or **Remove** buttons in the **Actions** panel while the **Climates** layer is active.

**Database table:** `climates` (global — applies to all maps of that game).

**To add a new climate:**

1. In Dave, add a new row to `climates`:

| Field | What to enter |
|---|---|
| `climate_type` | A unique internal name for the climate (e.g., `my_mod_climate_volcanic`) |

2. Save in Dave.
3. In CAIME, with the **Climates** layer active, click **Create** in the **Actions** panel and enter the matching name.
4. Paint the new climate swatch onto the relevant hexes.
5. Save (**Ctrl+S**) and re-process **Process → Map Data**.

**To rename or remove a climate:** Follow the same pattern as for terrain types above, using the `climate_type` field in the `climates` table.

---

### Adding, Renaming, or Removing an Attrition Type

**What you're doing:** Using the **Create**, **Rename**, or **Remove** buttons in the **Actions** panel while the **Attritions** layer is active.

**Database table:** `campaign_map_attritions`. Note that CAIME only loads rows where the `type` field is `terrain_land` or `terrain_sea` — only rows of those types are available as paintable swatches.

**To add a new attrition type:**

1. In Dave, add a new row to `campaign_map_attritions`:

| Field | What to enter |
|---|---|
| `key` | A unique internal name for the attrition (e.g., `my_mod_attrition_lava`) |
| `type` | Must be `terrain_land` or `terrain_sea` for CAIME to load it |

2. Save in Dave, then reload the map in CAIME (**File → Reload**, Ctrl+R).
3. With the **Attritions** layer active, click **Create** in the **Actions** panel and enter the matching key name.
4. Paint the attrition onto the relevant hexes.
5. Save (**Ctrl+S**) and re-process **Process → Map Data**.

**To rename or remove an attrition type:** Follow the same pattern as for terrain types above, using the `key` field in the `campaign_map_attritions` table.

---

### Impassable and Restrictions Changes

**What you're doing in CAIME:**
- Painting the **Impassable** layer to block army movement through certain hexes
- Painting **Restrictions** levels (Attila and later games only) to create graduated movement restrictions that can be unlocked via lua scripts

**Database changes required:** None.

**Steps:**

1. Select the **Impassable** (or **Restrictions**) layer in the **Layers** panel.
2. Select your swatch and paint the hexes you want to mark.
3. Save (**Ctrl+S**) and re-process **Process → Pathfinding data** and **Process → Map Data**.

> **Restrictions** is only available for Attila, Thrones of Britannia, Warhammer I/II/III, Three Kingdoms, Troy, and Pharaoh. Rome II does not support this layer.

---

### Region Boundary Changes (Same Regions, New Shapes)

**What you're doing:** Repainting which region colour covers which hexes — for example, expanding a region to the north and shrinking an adjacent region to compensate. The regions themselves still exist; you are only changing their shapes.

**Database changes required:** None (as long as the same regions still exist and no region is completely removed).

**Steps:**

1. Select the **Regions** layer in the **Layers** panel.
2. Choose the region swatch you want to expand and paint over the hexes you want to reassign.
3. Make sure every hex on the map still has a region assigned — no hex should be left without one. Use **Tools → Validate → Regions** to check.
4. Save (**Ctrl+S**).
5. Re-process: **Process → Borders data** (not needed for Troy or Pharaoh), **Process → Pathfinding data**, **Process → Map Data**, and **Process → Lookup and Minimap images**.

> **Watch for split regions.** If repainting disconnects a region's hexes into two isolated blobs (e.g., an island and a mainland both in the same region), the Regions validator will flag this as a startpos risk. Regions should ideally be contiguous.

---

### Adding a New Region

Adding a region is a Category B change — you must update the database **first**, then paint in CAIME.

**Steps in Dave (the Assembly Kit database editor):**

**1. Add the region definition to `regions`:**

| Field | What to enter |
|---|---|
| `key` | A unique internal name (e.g., `my_map_region_galia`) |
| `is_sea` | `0` for land, `1` for sea |
| `r`, `g`, `b` | A unique RGB colour (0–255). No two regions may share the same colour. |

**2. Add a link entry to `campaign_map_regions`:**

| Field | What to enter |
|---|---|
| `campaign_map` | Your map's exact internal name |
| `region` | The `key` you just added in `regions` |

> **Pharaoh only:** This table also has an `index` field. See [Pharaoh & Pharaoh Dynasties](#pharaoh--pharaoh-dynasties) for the extra steps required.

**3. Add a province link in `region_to_province_junctions`:**

| Field | What to enter |
|---|---|
| `region` | The `key` of your new region |
| `province` | The internal name of the province this region belongs to |

If you are creating a brand-new province at the same time, you may also need to add it to the `provinces` table (check your game's schema in Dave).

**Steps in CAIME:**

4. Save all changes in Dave.
5. In CAIME, reload the map: **File → Reload** (Ctrl+R). The new region swatch will now appear in the **Swatches** panel under the **Regions** layer.
6. Select the **Regions** layer and paint your new region onto the map.
7. Also paint **Town Sprawl** and at least one **Town Slot** within the new region (unless it is intentionally a wasteland with no settlement).
8. Run **Tools → Validate → Regions** and **Tools → Validate → Town Slots** to catch any issues.
9. Save (**Ctrl+S**).
10. Re-process all outputs: Borders (if applicable), Pathfinding, Trade Routes (if applicable), Map Data, Lookup and Minimap images.

---

### Removing a Region

Removing a region is a Category B change. You must repaint all the hexes that previously belonged to the deleted region **before** removing it from Dave, so that no hex is left without a region.

**Steps in CAIME (first):**

1. Select the **Regions** layer.
2. Repaint all hexes that currently show the region you want to remove. Assign them to adjacent regions using their swatches.
3. Remove any **Town Sprawl** and **Town Slot** hexes that were in the deleted region (use the Eraser tool on those layers).
4. Run **Tools → Validate → Regions** to confirm no unassigned hexes remain.
5. Save (**Ctrl+S**).

**Steps in Dave (second):**

6. In `campaign_map_regions`, delete the row where `campaign_map` = your map name and `region` = the region you are removing.
7. In `region_to_province_junctions`, delete any rows where `region` = the deleted region's key.
8. In `regions`, delete the row with the matching `key` — **only** if this region is not used by any other map. If it is shared with other campaign maps, leave it there.

> **Pharaoh only:** After removing a region, you must renumber the remaining `index` values in `campaign_map_regions` so they are sequential with no gaps. See [Pharaoh & Pharaoh Dynasties](#pharaoh--pharaoh-dynasties).

9. Reload the map in CAIME (**File → Reload**).
10. Re-process all outputs.

---

### Renaming a Region

Renaming a region means changing its internal `key`. This is a pure database operation — the painted hexes do not need to change, but every table that references the old name must be updated.

**Steps in Dave:**

1. In `regions`, change the `key` field of the region to the new name.
2. In `campaign_map_regions`, update the `region` field for all rows that referenced the old name.
3. In `region_to_province_junctions`, update the `region` field for all rows that referenced the old name.
4. If the region key appears in any other tables in your mod (settlement tables, script references, etc.), update those as well — CAIME does not manage those tables.

**Steps in CAIME:**

5. Reload the map (**File → Reload**).
6. The renamed swatch will appear in the **Swatches** panel. No repainting is needed.
7. Save (**Ctrl+S**) and re-process all outputs.

> **Do not just change the colour** in the `regions` table and leave the key the same — that only changes how the game generates the lookup image, not the region identity. Always rename the key if you want a different region name.

---

### Adding or Moving Settlement Slots

Town slots (the hexes where settlements appear) and the surrounding town sprawl are painted directly in CAIME — no database changes are required.

**Steps:**

1. Select the **Town Sprawl** layer and paint a cluster of hexes around where you want the settlement to be.
2. Select the **Town Slots** layer and paint the settlement slot hexes inside the sprawl. Each slot index (visible in the swatch list) corresponds to a settlement tier or slot number.
3. Run **Tools → Validate → Town Slots** to confirm the slot is valid.

**Slot size rules by game:**

| Game | Validator size checks | Port slot |
|---|---|---|
| Rome II, Attila, Thrones | Minimum 7 hexes per slot; at least one hex must be fully surrounded by 6 hexes of the same slot | Must touch a sea hex |
| Warhammer I, II, III | Main slot (slot 0) must be exactly **19 hexes** (inland) or **16 hexes** (port); 7-hex minimum and shape checks do NOT apply | Must touch a sea hex |
| Three Kingdoms, Troy, Pharaoh | No size or shape enforcement | Must touch a sea hex |

4. Save (**Ctrl+S**) and re-process **Process → Pathfinding data** and **Process → Map Data**.

---

### River Changes

Rivers are painted directly in CAIME — no database changes are required.

**Steps:**

1. Select the **Rivers** layer.
2. Paint river hexes along the path you want. Rivers should connect to each other and flow toward the sea or the map edge. Isolated river hexes are allowed but will produce a validator warning.
3. Run **Tools → Validate → Rivers** to check for disconnected or sea-adjacent river edges.
4. Save (**Ctrl+S**) and re-process **Process → Pathfinding data** and **Process → Map Data**.

> Rivers cannot overlap sea hexes. If a river appears to reach the coast, it must terminate on a land hex adjacent to the sea — not on the sea hex itself.

---

### Road Changes

Road hexes are painted in CAIME. The road **cost values** (how much movement roads grant) are stored in the `campaign_map_roads` database table, but this table only needs editing if you are introducing a new road type or a new campaign that uses this map.

**Steps:**

1. Select the **Roads** layer.
2. Paint road hexes. Roads should form continuous paths — a single isolated road hex will pass validation with a warning but will not generate useful connections.
3. Run **Tools → Validate → Roads** to check for disconnected or sea road hexes.
4. Save (**Ctrl+S**) and re-process **Process → Pathfinding data** and **Process → Map Data**.

**If you need to add a new road cost entry:**

- In Dave, add a row to `campaign_map_roads` with the campaign name, a unique key, a threshold value, and a movement cost.

---

### Beach Changes

Beaches mark the transition between land and sea for amphibious landings. They are painted directly in CAIME.

**Steps:**

1. Select the **Beaches** layer.
2. Paint beach hexes along coastlines. Every beach hex must be adjacent to at least one sea hex.
   - CAIME only lets you paint beach onto coastal hexes — a land hex with at least one sea neighbour. Painting over inland hexes simply leaves them unchanged, so you can drag a brush along the coast without smearing beach inland. Erasing beach is never restricted.
3. Run **Tools → Validate → Beaches** to confirm no beaches are on sea hexes or inland without a sea neighbour.
4. Save (**Ctrl+S**) and re-process **Process → Pathfinding data** and **Process → Map Data**.

---

### Bridge Changes

Bridges allow armies to cross rivers. They are painted directly in CAIME.

**Steps:**

1. Select the **Bridges** layer.
2. Paint bridge hexes across a river path.
3. Run **Tools → Validate → Bridges** to check bridge validity.
4. Save (**Ctrl+S**) and re-process **Process → Pathfinding data** and **Process → Map Data**.

---

### Trade Route Changes

> **Applies to:** Rome II, Attila, Thrones of Britannia, Three Kingdoms only. This layer is not available for Warhammer I/II/III, Troy, or Pharaoh.

Trade routes define the sea and land trade connections between regions. They are painted directly in CAIME — no database changes are needed.

**Steps:**

1. Select the **Trade Routes** layer.
2. Paint trade route hexes connecting the regions you want to link.
3. Save (**Ctrl+S**) and re-process: **Process → Trade Routes**.

---

### Areas of Interest Changes

> **Applies to:** Three Kingdoms and Warhammer III only.

Areas of Interest are special named locations on the map (landmarks, resource sites, etc.). This layer has **Create**, **Rename**, and **Remove** buttons in its **Actions** panel, so all swatch management requires a matching database change.

**Database table:** `campaign_map_areas_of_interest` (map-specific — rows are filtered by your map name).

**To add a new Area of Interest:**

1. In Dave, add a new row to `campaign_map_areas_of_interest`:

| Field | What to enter |
|---|---|
| `campaign_map` | Your map's exact internal name |
| `key` | A unique name for this area of interest |

2. Save in Dave, then reload the map in CAIME (**File → Reload**, Ctrl+R).
3. With the **Areas of Interest** layer active, click **Create** in the **Actions** panel and enter the matching key name.
4. Paint the area of interest onto the appropriate hex.
5. Save (**Ctrl+S**) and re-process **Process → Map Data**.

**To rename or remove an Area of Interest:** Update the `key` in `campaign_map_areas_of_interest` in Dave first, reload in CAIME, then use the **Rename** or **Remove** button in the **Actions** panel.

---

### Resizing the Map

You can grow or shrink the map canvas using **Edit → Resize** (Ctrl+Shift+R). This does not require database changes, but the newly revealed hexes will be empty and must be painted before processing.

```
┌─ Resize dialog ────────────────────────────────────────────────────┐
│                                                                     │
│  New map size:   [ 1200 ] x [  800  ]                              │
│  ─────────────────────────────────────────────────────────────     │
│  Padding:  Right [  0  ]  Left [  0  ]  Top [  0  ]  Bottom [ 0 ] │
│  ─────────────────────────────────────────────────────────────     │
│                                               [ OK ]  [ Cancel ]   │
└─────────────────────────────────────────────────────────────────────┘
```

**Steps:**

1. Go to **Edit → Resize** (Ctrl+Shift+R).
2. Enter the new width and height. Width **must** be an even number.
3. Use the **Padding** fields to push existing content away from the edges if you are adding canvas space on one side.
4. Click **OK**.
5. Paint all newly exposed hexes — they must have at least a region and a ground type assigned.
6. Save (**Ctrl+S**) and re-process all outputs.

> If you also need to update the `campaign_map_playable_areas` table in Dave (which defines the camera-visible and playable region boundaries), do so before re-processing Map Data.

---

### Starting from a map_data.esf

If you have a map's `map_data.esf` but not its `map.hex` — for example a map from another mod's pack — use **Tools → Import → map_data.esf** and pick the file. Every supported game's `map_data.esf` can be imported; when the file's layout is shared by several games, CAIME asks which one it is from. CAIME rebuilds the `map.hex` and opens it as a new project in `Projects\[MapName]\`. The map is named after the folder the file is in, so extract the file with its `campaign_maps\[MapName]\` folder, or rename the map afterwards with **Edit → Rename map**.

The import recovers regions and their colours, ground types, climates, attritions, areas of interest (in the games that have them), roads, rivers, land and sea, beaches, cliffs, bridges, impassable hexes, town slots (every building slot, in the games that have more than a main and a port slot), town sprawl around settlements, and campaign masks with the hexes each one masks. Region borders are recalculated. Processing **Map Data** from the imported map writes the same `map_data.esf` back. Two things are not in `map_data.esf` and start empty:

| Layer | After import |
|---|---|
| **Restrictions** | Empty. Repaint them before you process pathfinding data |
| **Trade Routes** | Empty. Repaint them in the games that use them |

Some things are inferred, because `map_data.esf` does not record them directly. None of them changes the `map_data.esf` you process from the map:

- **Impassable coast.** Whether an impassable hex on the coast is a beach or a cliff is not stored. CAIME makes it a beach when it touches a passable beach hex, and a cliff otherwise. This matches CA's own maps on about 97–99% of such hexes.
- **Land and sea regions.** `map.hex` lists land regions before sea regions, and `map_data.esf` keeps that order but not where the sea regions start. CAIME starts them after the last region that has a settlement or land ground. A sea region listed before that point is imported as a land region; move it in the region list if it matters to you.
- **Town sprawl away from settlements.** Only the sprawl that belongs to a settlement is in `map_data.esf`; sprawl hexes no settlement reaches are not imported.
- **Sea ground types.** `map_data.esf` keeps land and sea ground types in one list. CAIME splits it between the last ground type under passable land and the first under passable sea. Ground types found only under impassable hexes, such as an impassable lake's, go on whichever side explains the cliffs: a land hex is a cliff exactly when it touches sea. On CA's maps this gives the same split as their `map.hex`. If it is wrong for your map, **Tools → Validate → Ground types** reports each ground type the database marks as sea.

---

## 4. After Modifying: What to Re-Process

After painting and saving your changes, you need to tell CAIME to re-generate the binary output files. Use the **Process** menu for each relevant step.

```
┌─────────────────────────────────────────────────────────────────────┐
│  Processing steps and when to run them                              │
│                                                                     │
│  Process → Borders data          ← After region changes            │
│  (Not needed for Troy or Pharaoh)                                   │
│                                                                     │
│  Process → Pathfinding data      ← After roads, rivers, bridges,   │
│                                    beaches, terrain, impassable,    │
│                                    restrictions, slots              │
│                                                                     │
│  Process → Trade Routes          ← After trade route painting       │
│  (Rome 2, Attila, Thrones, Three Kingdoms only)                     │
│                                                                     │
│  Process → Map Data              ← After any map change            │
│  ⚠ The map file must be saved to:                                  │
│    [AKit]\raw_data\EmpireDesignData\campaign_maps\[MapName]\        │
│                                                                     │
│  Process → Dynamic resources     ← Only if resource placement      │
│                                    was modified externally          │
│                                                                     │
│  Process → Lookup and Minimap    ← After region boundary or         │
│            images                  region colour changes            │
└─────────────────────────────────────────────────────────────────────┘
```

**Where the output files land:**

All processed files are written to your Assembly Kit's working data folder:
```
[Assembly Kit]\
└── working_data\
    └── campaign_maps\
        └── [YourMapName]\
            ├── display\
            │   └── borders\
            │       └── borders.pbd          (Borders data)
            ├── pathfinding.ppd               (Pathfinding data)
            ├── trade_routes.ptd              (Trade Routes, where applicable)
            └── map_data.esf                  (Map Data)
```

> **Map Data requires the project to be in the correct location.** For **Process → Map Data** to work, your `map.hex` file must be saved directly inside the Assembly Kit at:
> ```
> [AKit]\raw_data\EmpireDesignData\campaign_maps\[YourMapName]\map.hex
> ```
> If your project is stored somewhere else (e.g., in the CAIME `Projects` folder), use **File → Save as...** (Ctrl+Shift+S) to save a copy to the Assembly Kit location, then re-open it from there before processing Map Data.

---

## 5. Running Validators Before Processing

CAIME includes built-in validators that check the most common causes of processing failure. Run these from the **Tools → Validate** sub-menu before triggering any processing step.

```
┌─ Menu Bar ───────────────────────────────────────────────────────────┐
│  File   Edit   Process   Tools   Settings   Help                     │
│                          ├── Show Logger       Ctrl+L                │
│                          ├── Border Editor     Ctrl+B                │
│                          ├── Map Data Editor   Ctrl+M                │
│                          ├── Shader Resolution Corrector              │
│                          ├── Import                                   │
│                          ├── Export                                   │
│                          └── Validate ──► Rivers                     │
│                                          ├── Town Slots              │
│                                          ├── Roads                   │
│                                          ├── Bridges                 │
│                                          ├── Beaches                 │
│                                          ├── Regions        ◄ Run   │
│                                          ├── Attritions      these   │
│                                          ├── Climates        before  │
│                                          ├── Ground Types   export!  │
│                                          ├── Impassable             │
│                                          └── Town Sprawl             │
└──────────────────────────────────────────────────────────────────────┘
```

**What each validator checks:**

| Validator | Key things it catches |
|---|---|
| **Regions** | Region count matches database (number of region definitions in map file vs. database); region names match in order; no unassigned hexes; non-contiguous land regions; sea regions adjacent to too many land regions (AI slowdown) |
| **Ground Types** | Database ground type count and names match the map file; sea ground type on a land region hex (and vice versa) |
| **Town Slots** | Slot size rules by game; slots outside of sprawl; non-port slots on sea hexes; port slots with no sea neighbour; regions with passable hexes but no slot |
| **Roads** | Roads on sea (non-bridge); disconnected road hexes; three-way intersections |
| **Rivers** | River flag matches edge mask; rivers on sea; river edges pointing off-map or into sea |
| **Beaches** | Beaches on sea hexes; beaches with no sea neighbour; beach overlapping cliff |
| **Bridges** | Bridge validity rules |
| **Attritions** | Attrition index validity |
| **Climates** | Climate index validity |
| **Impassable** | Town slots on impassable ground; passable holes fully enclosed by impassable hexes; isolated impassable hexes |
| **Town Sprawl** | Sprawl blobs staying inside one land region (sea spillover for ports is allowed); one blob per region; sprawl on hazard terrain; sprawl pinched between two separate hazard patches |

> **Error vs. Warning vs. Info:** Errors (shown in red in the Logger) are problems that will likely break the game or startpos. Warnings (orange) are strong recommendations. Info messages (blue) are things worth checking but not necessarily wrong. Fix all errors before processing.

To view validator output, open the Logger panel with **Tools → Show Logger** (Ctrl+L).

---

## 6. Game-Specific Differences

---

### Rome II

| What is different | Detail |
|---|---|
| **Trade Routes** | Supported — paint the Trade Routes layer and run **Process → Trade Routes** |
| **Borders** | `borders.pbd` is active and used by the game |
| **Restrictions layer** | Not available |
| **Areas of Interest** | Not available |
| **Town slot size** | Not strictly enforced by the validator |
| **Pharaoh region index** | Not used |

**Database tables needed for region changes:**
- `regions` (add/remove/rename)
- `campaign_map_regions` (link regions to map)
- `region_to_province_junctions` (link regions to provinces)

---

### Attila

| What is different | Detail |
|---|---|
| **Trade Routes** | Supported — paint the Trade Routes layer and run **Process → Trade Routes** |
| **Borders** | `borders.pbd` is active and used by the game |
| **Restrictions layer** | Available — paint Restrictions to create movement penalty zones |
| **Areas of Interest** | Not available |
| **Town slot size** | Not strictly enforced by the validator |
| **Pharaoh region index** | Not used |

**Database tables needed for region changes:** Same as Rome II.

---

### Thrones of Britannia

| What is different | Detail |
|---|---|
| **Trade Routes** | Supported — paint the Trade Routes layer and run **Process → Trade Routes** |
| **Borders** | `borders.pbd` is active and used by the game |
| **Restrictions layer** | Available |
| **Areas of Interest** | Not available |
| **Town slot size** | Not strictly enforced by the validator |
| **Pharaoh region index** | Not used |

**Database tables needed for region changes:** Same as Rome II.

---

### Warhammer & Warhammer II

| What is different | Detail |
|---|---|
| **Trade Routes** | **Not supported** — the Trade Routes layer is not available for these games |
| **Borders** | `borders.pbd` is active and used by the game |
| **Restrictions layer** | Available |
| **Areas of Interest** | Not available |
| **Town slot size** | **Strictly enforced:** main settlement must be exactly 19 hexes (inland) or 16 hexes (port) |
| **Pharaoh region index** | Not used |

**Database tables needed for region changes:** Same as Rome II.

> **Town slot size is enforced by the validator.** If you move or repaint a settlement slot, count your hexes carefully before processing — the wrong size will be flagged as an error.

---

### Warhammer III

| What is different | Detail |
|---|---|
| **Trade Routes** | **Not supported** |
| **Borders** | `borders.pbd` is active and used by the game |
| **Restrictions layer** | Available |
| **Areas of Interest** | **Supported** — requires `campaign_map_areas_of_interest` database entries |
| **Town slot size** | Smaller or non-uniform slots are allowed (size not strictly enforced) |
| **Pharaoh region index** | Not used |

**Database tables needed for region changes:** Same as Rome II.

**Additional database table for Areas of Interest changes:**

| Table | What to do |
|---|---|
| `campaign_map_areas_of_interest` | Add a row per new area of interest (`campaign_map` + `key`) |

---

### Three Kingdoms

| What is different | Detail |
|---|---|
| **Trade Routes** | **Supported** — paint the Trade Routes layer and run **Process → Trade Routes** |
| **Borders** | `borders.pbd` is active and used by the game |
| **Restrictions layer** | Available |
| **Areas of Interest** | **Supported** — requires `campaign_map_areas_of_interest` database entries |
| **Town slot size** | Smaller or non-uniform slots are allowed (size not strictly enforced) |
| **Pharaoh region index** | Not used |

**Database tables needed for region changes:** Same as Rome II.

**Additional database table for Areas of Interest changes:** Same as Warhammer III above.

---

### Troy

| What is different | Detail |
|---|---|
| **Trade Routes** | **Not supported** |
| **Borders** | **`borders.pbd` has NO effect.** Borders are rendered by the game engine directly from `map_data.esf`. Running **Process → Borders data** will show a notice but the output file will not change anything visible in-game. |
| **Restrictions layer** | Available |
| **Areas of Interest** | Not available |
| **Town slot size** | Smaller or non-uniform slots are allowed (size not strictly enforced) |
| **Pharaoh region index** | Not used |

> **For Troy:** Do not rely on `borders.pbd` to control region border visuals. Region borders in-game are derived automatically from the `map_data.esf` file — focus on getting **Process → Map Data** correct.

**Database tables needed for region changes:** Same as Rome II.

---

### Pharaoh & Pharaoh Dynasties

> Pharaoh Dynasties uses the same map format as base Pharaoh and behaves identically in CAIME. Select whichever matches your map in the game dropdown — **Pharaoh** or **Pharaoh Dynasties**.

| What is different | Detail |
|---|---|
| **Trade Routes** | **Not supported** |
| **Borders** | **`borders.pbd` has NO effect.** Same as Troy — borders come from `map_data.esf`. |
| **Restrictions layer** | Available |
| **Areas of Interest** | Not available |
| **Town slot size** | Smaller or non-uniform slots are allowed |
| **Pharaoh region index** | **Yes — extra steps required for every region change** |

**The Pharaoh region index system:**

Pharaoh stores all regions (land and sea together) in a single combined list sorted by an `index` number. This index is stored in the `campaign_map_regions` table in Dave and must be kept in sync with the binary map file.

Every time you **add, remove, or reorder** regions for a Pharaoh map, you must:

1. Update the `index` values in `campaign_map_regions` in Dave.
2. The convention is: **land regions first (1, 2, 3…), then sea regions**, all numbered sequentially with no gaps.
3. Reload the map in CAIME after updating the indices. CAIME reads these on load and will refuse to open the map if the indices do not match the binary file.

**Example — adding a new land region to an existing Pharaoh map:**

```
Before adding:                          After adding "region_sinai" (land):
─────────────────────────────────       ──────────────────────────────────────
region        is_sea   index            region           is_sea   index
──────────    ──────   ─────            ─────────────    ──────   ─────
region_egypt  land     1                region_egypt     land     1
region_nubia  land     2                region_nubia     land     2
region_nile   land     3                region_nile      land     3
region_coast  sea      4      ──────►   region_sinai     land     4   ← NEW
region_medsea sea      5                region_coast     sea      5   ← updated
                                        region_medsea    sea      6   ← updated
```

The sea region indices must shift up to make room for the new land region. Every `index` value must be updated, not just the new one.

> **CAIME does not write back to Dave.** You must make all index corrections manually in Dave, then reload the map. If you open a Pharaoh map and CAIME reports a region index mismatch, your `index` values in Dave do not match the binary map file — correct them in Dave and retry.

---

## 7. Recommended Order of Operations

```
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 1: Plan your changes                                          │
│  ─────────────────────────────────────────────────────────────────  │
│  Identify whether your changes are Category A (paint only) or      │
│  Category B (paint + database). Refer to Section 2.                │
└─────────────────────────────────────────────────────────────────────┘
            │
            ▼ (Category B changes only)
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 2: Update the database in Dave                                │
│  ─────────────────────────────────────────────────────────────────  │
│  Add/remove/rename rows in:                                         │
│  ├── regions                                                         │
│  ├── campaign_map_regions                                            │
│  ├── region_to_province_junctions                                    │
│  └── campaign_map_areas_of_interest (3K / WH3 only)                 │
│                                                                      │
│  Pharaoh: also update index values in campaign_map_regions          │
│  Save all changes in Dave.                                           │
└─────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 3: Open or reload the map in CAIME                            │
│  ─────────────────────────────────────────────────────────────────  │
│  File → Open (Ctrl+O)   — if not already open                      │
│  File → Reload (Ctrl+R) — if already open; picks up DB changes     │
└─────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 4: Paint your changes                                         │
│  ─────────────────────────────────────────────────────────────────  │
│  Select the appropriate layer in the Layers panel                   │
│  Choose a swatch → Paint on the canvas                              │
└─────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 5: Validate                                                   │
│  ─────────────────────────────────────────────────────────────────  │
│  Tools → Validate → Regions                                         │
│  Tools → Validate → Ground Types                                    │
│  Tools → Validate → Town Slots                                      │
│  + any other validators relevant to your change                     │
│  Fix all errors (red) before continuing.                            │
└─────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 6: Save                                                       │
│  ─────────────────────────────────────────────────────────────────  │
│  File → Save (Ctrl+S)                                               │
│  If processing Map Data: also save to the Assembly Kit location     │
│  using File → Save as... (Ctrl+Shift+S)                             │
└─────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 7: Re-process outputs                                         │
│  ─────────────────────────────────────────────────────────────────  │
│  Process → Borders data          (not Troy or Pharaoh)             │
│  Process → Pathfinding data                                         │
│  Process → Trade Routes          (Rome 2, Attila, Thrones, 3K)     │
│  Process → Map Data                                                 │
│  Process → Lookup and Minimap images                                │
└─────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 8: Regenerate startpos                                        │
│  ─────────────────────────────────────────────────────────────────  │
│  Run the Assembly Kit's startpos tool (BOB / Tweak.AssemblyKit).   │
│  This is outside CAIME — refer to the Assembly Kit documentation.   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 8. Pro-Tips & Troubleshooting

- **"The Regions validator reports a count mismatch between the database and the map."**
  This means the number of regions in your database (in Dave) and the number painted in the map file do not match. Open Dave and count the rows in `campaign_map_regions` that belong to your map. Compare that count to how many distinct region swatches are painted on the map. Add or remove database rows accordingly, save in Dave, then use **File → Reload** (Ctrl+R) in CAIME.

- **"A region appears in the Swatches panel but its hexes are never painted on the map."**
  The Regions validator will flag this region as having zero hexes assigned and warn that it may cause startpos processing issues. Either paint some hexes with that region's swatch, or remove it from `campaign_map_regions` in Dave if the region should not be part of this map.

- **"I moved a land region's boundary and now the Regions validator says it is split into disconnected areas."**
  A land region whose hexes form two isolated blobs (e.g., an island and a mainland both belonging to the same region) is a known startpos bug. Sea regions can legitimately be split (e.g., a sea region that wraps around a peninsula), but land regions should be contiguous. Repaint one of the isolated blobs to belong to a different region.

- **"My Pharaoh map opens fine, but after I added a region the map won't open anymore."**
  CAIME verifies the `index` values in `campaign_map_regions` against the binary map file on every load. If the index values are out of sync, the load is rejected. Open Dave, fix the index values in `campaign_map_regions` (land regions first, sequential, no gaps), save, and try opening the map again. If you need to know what order the binary file expects, restore an earlier backup from your `Projects\[MapName]\` folder and compare.

- **"A sea region is adjacent to more than 9 land regions and the Regions validator is flagging it."**
  This is a soft limit for AI pathfinding performance — a sea region touching more than 9 land regions causes a significant slowdown in the game's AI calculations **when the campaign features horde factions**. Split the sea region into two smaller sea regions. The validator treats more than 5 adjacencies as an informational note and more than 9 as an error.

- **"I re-processed Borders data for a Troy or Pharaoh map but nothing changed in-game."**
  This is expected. Troy and Pharaoh render region borders directly from `map_data.esf`, not from `borders.pbd`. The `borders.pbd` file has no effect for these games. Focus on getting Map Data processed correctly instead.
  