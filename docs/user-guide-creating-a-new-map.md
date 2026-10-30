# Creating a New Campaign Map from Scratch

---

## Overview

Creating a brand-new campaign map in CAIME is a two-part process, similar to starting a new painting project.  
**Part 1 — In RPFM, in your mod's pack:** You add your new map's rows to the database tables, so that CAIME (and eventually the game itself) knows which regions, roads, and other game data belong to it.  
**Part 2 — Inside CAIME:** You create an empty canvas — the **map file** — and give it a name, a target game, and a size, and tell CAIME which mod holds its rows. CAIME reads the database through RPFM each time the map opens, from the game's own packs with your mod layered over them, and fills your Swatches panel from it.

> **Using Dave?** CAIME doesn't read the Assembly Kit's database, so rows made in Dave only reach CAIME once they are in your mod's pack. See [Annex: The Assembly Kit](user-guide-assembly-kit.md#5-dave-and-the-assembly-kits-database).

---

## Table of Contents

1. [Before You Begin: Configuring RPFM and the Output Folder](#1-before-you-begin-configuring-rpfm-and-the-output-folder)
2. [Setting Up the Database in Your Mod](#2-setting-up-the-database-in-your-mod)
   - [regions](#regions)
   - [campaign_map_regions](#campaign_map_regions)
   - [campaigns](#campaigns)
   - [campaign_map_roads](#campaign_map_roads)
   - [campaign_map_playable_areas](#campaign_map_playable_areas)
   - [campaign_map_areas_of_interest (Three Kingdoms & Warhammer III only)](#campaign_map_areas_of_interest-three-kingdoms--warhammer-iii-only)
3. [Opening the Create New Map Dialog](#3-opening-the-create-new-map-dialog)
4. [Filling In the Map Details](#4-filling-in-the-map-details)
   - [Campaign Map Name](#campaign-map-name)
   - [Template](#template)
   - [Game](#game)
   - [Map Size](#map-size)
5. [What CAIME Creates for You](#5-what-caime-creates-for-you)
   - [Pointing the Project at Your Mod](#pointing-the-project-at-your-mod)
6. [Game-Specific Differences](#6-game-specific-differences)
   - [Rome II](#rome-ii)
   - [Attila & Thrones of Britannia](#attila--thrones-of-britannia)
   - [Warhammer, Warhammer II & Troy](#warhammer-warhammer-ii--troy)
   - [Three Kingdoms](#three-kingdoms)
   - [Warhammer III](#warhammer-iii)
   - [Pharaoh & Pharaoh Dynasties](#pharaoh--pharaoh-dynasties)
7. [The Big Picture: Order of Operations](#7-the-big-picture-order-of-operations)
8. [Pro-Tips & Troubleshooting](#8-pro-tips--troubleshooting)

---

## 1. Before You Begin: Configuring RPFM and the Output Folder

CAIME reads your game's database through **RPFM** (version 5.3 or later), and writes processed files to an **Output folder**. Before you create any map, set both. You only need to do this once.

**Steps:**

1. In RPFM, set the game's folder in RPFM's own settings, if you haven't already. RPFM finds the game's packs, and your mod, through it.
2. In CAIME, go to **Settings → Preferences** (or press **Ctrl+P**).

   The **Preferences** window will appear:

   ```
   ┌─ Preferences ────────────────────────────────────────────────────┐
   │                                                                   │
   │  Hex spacing:  [ ──●────────────────────── ] 0.00               │
   │  ─────────────────────────────────────────────────────────────   │
   │  Log to file:         ● Enabled    ○ Disabled                    │
   │  ...                                                             │
   │  ─────────────────────────────────────────────────────────────   │
   │  RPFM path:          [ C:\...\rpfm                   ] [Browse]  │
   │  Output folder:      [ C:\...\my_mod                 ] [Browse]  │
   │  ─────────────────────────────────────────────────────────────   │
   │  Base game:          [ Rome2                              ▼ ]    │
   │  Assembly kit path:  [                               ] [Browse]  │
   │  ...                                                             │
   │  ─────────────────────────────────────────────────────────────   │
   │                                              [ Save ] [ Cancel ] │
   └───────────────────────────────────────────────────────────────────┘
   ```

3. Click **Browse** next to **RPFM path** and select the folder that contains `rpfm_server.exe`.
4. Click **Browse** next to **Output folder** and select the folder processed files should go to. It can be your mod's own folder: files are laid out as in a pack.
5. Click **Save**. CAIME checks the RPFM folder before it saves.

> **Base game** and **Assembly kit path** are legacy settings; you can leave them alone. See [Annex: The Assembly Kit](user-guide-assembly-kit.md).

---

## 2. Setting Up the Database in Your Mod

CAIME reads each table from the game's own packs, with your mod's rows merged over them (see [Settings & Preferences](user-guide-settings-and-preferences.md#3-database-rpfm-and-output-folder)). So for a new map you only add the rows that are **specific to your campaign map**, to your mod's pack, in RPFM. Other tables (such as `campaign_ground_types` and `climates`) are global to the game and already contain all the data CAIME needs — no new rows are required in those.

Every row must use **exactly the same map name** you will type in the CAIME dialog in the next step. Save the pack in RPFM when you are done: CAIME reads the pack from disk.

The tables to add rows to for a new map are:

| Table | Why it needs editing |
|---|---|
| `regions` | Defines any new regions your map introduces, with their RGB colours |
| `campaign_map_regions` | Links your map name to each of its regions |
| `campaigns` | Links your campaign name to your map name (needed for road filtering) |
| `campaign_map_roads` | Road cost entries that reference campaigns using your map |
| `campaign_map_playable_areas` | Your map's playable area, which **Process → Map Data** builds `map_data.esf` from |
| `campaign_map_areas_of_interest` | **Three Kingdoms & Warhammer III only** — registers Areas of Interest for your map |

Each table is described in detail below.

---

### regions

**What it is:** The global list of all regions in the game. Each region has a unique name (key), an `is_sea` flag indicating whether it is a sea region, and an RGB colour. The RGB colour is **not** what CAIME shows in the Swatches panel — CAIME generates its own display colours for the painting interface automatically. Instead, these RGB values are used when generating the **lookup images** (the colour-coded map images the game engine reads to identify region boundaries).

**What to add:** One record per new region your map introduces. If you are reusing regions that already exist in the game (e.g., for a map replacement mod), those entries are already present and no changes are needed here. Fields you must fill for each new region:

| Field | What to enter |
|---|---|
| `key` | A unique internal name for this region (e.g., `my_map_region_gaul`) |
| `is_sea` | `0` for land regions, `1` for sea regions |
| `r`, `g`, `b` | A unique RGB colour (0–255 each) used in the game's lookup images |

> **Tip:** Make sure every region has a **unique RGB colour**. If two regions share the same colour, the game engine will not be able to tell them apart in the lookup images.

---

### campaign_map_regions

**What it is:** The link table between your specific map and its regions. Without entries here, CAIME will not load any regions for your map, even if they exist in `regions`.

**What to add:** One record per region, for each region that belongs to your map.

| Field | What to enter |
|---|---|
| `campaign_map` | The exact name of your map (must match what you will type in CAIME) |
| `region` | The `key` of the region from the `regions` table |

> **Pharaoh Dynasties only:** CAIME reads this table's `index` field. See [Pharaoh & Pharaoh Dynasties](#pharaoh--pharaoh-dynasties) below for details.

---

### campaigns

**What it is:** Lists all the playable campaigns in the game, each one linked to a campaign map.

**What to add:** If you are creating an entirely new campaign (not replacing an existing one), you will need a new entry that links your campaign name to your map name. If you are modifying or replacing the map of an existing campaign, no changes may be needed here.

CAIME reads this table to figure out which campaigns use your map. This information is then used to filter road costs in the next table.

---

### campaign_map_roads

**What it is:** Defines how much roads speed up unit movement on each campaign map, broken down per campaign. CAIME reads this to show road cost information in the editor.

**What to add:** One record per campaign that uses your map and per road type. The key fields are:

| Field | What to enter |
|---|---|
| `campaign` | The campaign name (from the `campaigns` table) that uses your map |
| `key` | A unique name for this road cost entry |
| `threshold` | The road density threshold at which this cost applies |
| `movement_cost` | The movement cost on roads |

---

### campaign_map_playable_areas

**What it is:** The area of each campaign map that is playable. **Process → Map Data** builds `map_data.esf` from your map's first row here, and stops with an error if there is none.

**What to add:** One record for your map. Its `index` and `maxx` go into the map data; the game also reads the row itself, so fill in its other columns as the game expects.

---

## 3. Opening the Create New Map Dialog

Once your mod has your map's rows, switch to CAIME and create the map file.

**Go to File → Create new map** in the menu bar, or press **Ctrl+N**.

```
┌─ Menu Bar ──────────────────────────────────────────────────────────┐
│  File    Edit    Process    Tools    Settings    Help                │
│  ├── Create new map    Ctrl+N   ◄── CLICK THIS                     │
│  ├── Open              Ctrl+O                                        │
│  ├── Reload            Ctrl+R                                        │
│  ├── Save              Ctrl+S                                        │
│  ├── Save as...        Ctrl+Shift+S                                  │
│  ├── Close             Ctrl+X                                        │
│  └── Exit              Ctrl+Q                                        │
└──────────────────────────────────────────────────────────────────────┘
```

The **Create new project** dialog will appear.

---

## 4. Filling In the Map Details

```
┌─ Create new project ──────────────────────────────────────────────┐
│                                                                    │
│  Campaign map name:  [_____________________________________]       │
│  ────────────────────────────────────────────────────────────      │
│  Template:           [ None                               ▼ ]     │
│  Game:               [ Rome2                              ▼ ]     │
│  Map size:           [  1016  ] x [  720  ]                       │
│  ────────────────────────────────────────────────────────────      │
│                                      [ Create ]  [ Cancel ]        │
└────────────────────────────────────────────────────────────────────┘
```

---

### Campaign Map Name

Type the internal name of your map in the **Campaign map name** field. This name:

- Must match **exactly** the name you used in your mod's tables in [Section 2](#2-setting-up-the-database-in-your-mod) above.
- Will become the name of the project folder created on disk (`Projects\YourMapName\`).
- Cannot be empty — CAIME will show an error and stop if you try to proceed without one.

> **Tip:** Use only letters, numbers, and underscores (e.g., `my_custom_map`). Avoid spaces or special characters, which the game's tables and file names may not accept.

When you click somewhere else after typing, CAIME will automatically clean up any disallowed characters in the name.

---

### Template

The **Template** dropdown lists any pre-built starting points stored in the `Templates` folder alongside CAIME.

| Choice | Meaning |
|---|---|
| **None** | Start with a completely blank map. You choose the game and size manually. |
| **A named template** | CAIME copies a pre-configured map from the Templates folder. The game and size are locked to whatever that template was designed for. |

If you select a template, the **Game** and **Map size** fields below it will be grayed out — the template already contains all that information.

---

### Game

The **Game** dropdown is only active when **Template** is set to **None**. Select the Total War game your map is for.

| Option in CAIME | Game |
|---|---|
| Rome2 | Total War: Rome II |
| Attila | Total War: Attila |
| Thrones_Of_Britannia | A Total War Saga: Thrones of Britannia |
| Warhammer | Total War: Warhammer |
| Warhammer2 | Total War: Warhammer II |
| Warhammer3 | Total War: Warhammer III |
| Three_Kingdoms | Total War: Three Kingdoms |
| Troy | A Total War Saga: Troy |
| Pharaoh | Total War: Pharaoh |
| Pharaoh_Dynasties | Total War: Pharaoh Dynasties |

> **Important:** Your choice here determines the internal binary format of the map file. You cannot change the game for a map after creation without starting over.

---

### Map Size

Enter the width (**x**) and height (**y**) of your map in hexes.

- Default starting values are **1016 × 720** hexes (similar to the standard game maps).
- **Width must be an even number.** Entering an odd width will show an error and prevent creation.
- Width or height cannot be zero.
- If your total hex count (width × height) exceeds **731,520 hexes**, CAIME will show a warning — this is above the recommended maximum. The map can still be created, but performance may suffer.

> **Tip:** If you are making a custom campaign rather than replacing a stock map, starting at `1016 × 720` gives you a canvas the same size as most standard Total War campaigns. You can always resize later using **Edit → Resize** (Ctrl+Shift+R).

Once you have filled in all fields, click **Create**.

- If a project folder with the same name already exists, CAIME will ask if you want to overwrite it. Click **Yes** to replace it, or **No** to cancel and pick a different name.

---

## 5. What CAIME Creates for You

After you click **Create**, CAIME creates the map file and opens it:

```
CAIME creates on your disk:
─────────────────────────────────────────────────────────────────────────
 [CAIME install folder]\
 └── Projects\
     └── YourMapName\
         └── map.hex          ◄── Your new (empty) map file
─────────────────────────────────────────────────────────────────────────
```

Processed files go to `campaign_maps\YourMapName\` in your **Output folder**, created when you first process the map.

The map file (`map.hex`) is completely empty at this stage. Every hex on the grid has:
- No region assigned
- No terrain type (ground type) assigned
- No climate assigned
- No attrition assigned
- No roads, bridges, rivers, or other features

CAIME reads the database through RPFM in the background as soon as the map opens; the log says when it is done. Until then you can look around the map, but not edit, save or process it.

### Pointing the Project at Your Mod

A new project has no mod set, so at first CAIME reads only the game's own packs, which don't have your map's rows. To add your mod:

1. Go to **Settings → RPFM Workflow** (only enabled while a project is open).
2. Type your mod's `.pack` file name, or click **Browse...** and pick the `.pack`.
3. Click **Save**.
4. Reload the map with **File → Reload** (Ctrl+R). CAIME reads the database again, with your mod's rows, and the Swatches panel fills with your regions.

CAIME remembers the mod for this project, in `caime_metadata.json` next to `map.hex`.

---

## 6. Game-Specific Differences

Different games support different subsets of painting layers and have different database requirements. Use the section for your game.

### Layer Support at a Glance

The following eleven layers are available in **every** supported game:
Impassable · Roads · Town Slots · Town Sprawl · Bridges · Rivers · Beaches · Regions · Attritions · Climates · Ground Types

The four layers below are **not universally available** — check the table to see which games include them:

| Layer | Rome II | Attila | Thrones | Warhammer | WH II | WH III | 3 Kingdoms | Troy | Pharaoh | Pharaoh Dynasties |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Trade Routes** | ✓ | ✓ | ✓ | — | — | — | ✓ | — | — | — |
| **Restrictions** | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Region Borders** | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Areas of Interest** | — | — | — | — | — | ✓ | ✓ | — | — | — |

---

### Rome II

| Detail | Value |
|---|---|
| **Map format version** | 13 |
| **Layers not available** | Restrictions, Region Borders, Areas of Interest |
| **Special DB requirement** | None |

Rome II has the simplest format. It is the only game that includes **Trade Routes** but lacks both **Restrictions** and **Region Borders**. No database tables beyond the standard set in [Section 2](#2-setting-up-the-database-in-your-mod) are required.

---

### Attila & Thrones of Britannia

| Detail | Value |
|---|---|
| **Map format version** | 15 |
| **Layers not available** | Areas of Interest |
| **Special DB requirement** | None |

Both games have the full layer set except Areas of Interest. No extra database tables are required beyond the standard set.

---

### Warhammer, Warhammer II & Troy

| Detail | Value |
|---|---|
| **Map format version** | 18 |
| **Layers not available** | Trade Routes, Areas of Interest |
| **Special DB requirement** | None |

These three games share the same map format version. **Trade Routes** is not available — the layer will not appear in the Layers panel at all. No extra database tables are required.

---

### Three Kingdoms

| Detail | Value |
|---|---|
| **Map format version** | 19 |
| **Layers not available** | None — all layers are available |
| **Special DB requirement** | `campaign_map_areas_of_interest` |

Three Kingdoms is the only game that supports every layer simultaneously, including both **Trade Routes** and **Areas of Interest**. Because Areas of Interest is active, you must add rows to one extra table in your mod:

#### campaign_map_areas_of_interest

**What it is:** Registers special named points on the map (e.g., landmarks, resource nodes) that can be assigned to individual hexes.

**What to add:** One record per area of interest you want on your map.

| Field | What to enter |
|---|---|
| `campaign_map` | The exact name of your map |
| `key` | A unique internal name for this area of interest |

Without entries in this table, the **Areas of Interest** layer in CAIME will be empty and you will not be able to assign any areas of interest to hexes.

---

### Warhammer III

| Detail | Value |
|---|---|
| **Map format version** | 20 |
| **Layers not available** | Trade Routes |
| **Special DB requirement** | `campaign_map_areas_of_interest` |

Warhammer III supports **Areas of Interest** but not **Trade Routes**. As with Three Kingdoms, you must add rows to `campaign_map_areas_of_interest` in your mod (same table structure described in the Three Kingdoms section above).

---

### Pharaoh & Pharaoh Dynasties

> Select whichever matches your map in the Game dropdown — **Pharaoh** or **Pharaoh_Dynasties**. They differ in how the map file stores regions.

| Detail | Pharaoh | Pharaoh Dynasties |
|---|---|---|
| **Map format version** | 18 (same as Warhammer group) | 20 |
| **Layers not available** | Trade Routes, Areas of Interest | Trade Routes, Areas of Interest |
| **Special DB requirement** | None | CAIME reads `index` in `campaign_map_regions` |
| **Special region handling** | None | Combined land+sea index (see below) |

Pharaoh Dynasties uses a unique system for storing regions in the binary map file. Instead of land regions and sea regions being stored separately (as in all other games), **all regions are stored together in a single combined list**, sorted by a numeric index.

This has two consequences:

**1. CAIME reads each region's `index` from `campaign_map_regions`.**

When adding your regions to your mod, each row needs an `index` value — a whole number starting from 1. CAIME checks them against the order stored in the binary file every time the map opens. If they don't match, the map still opens, but its database isn't loaded: it stays view-only, and can't be edited, saved or processed. The convention is: **land regions first, then sea regions**, each numbered sequentially with no gaps.

| Example | Region type | index |
|---|---|---|
| `my_map_region_egypt` | land | 1 |
| `my_map_region_nubia` | land | 2 |
| `my_map_region_nile` | land | 3 |
| `my_map_region_redcoast` | sea | 4 |
| `my_map_region_medsea` | sea | 5 |

**2. CAIME only reads these index values — it never writes to the database.**

When it saves the map, CAIME writes the regions in the order of their `index` values. If you add, remove, or reorder regions after initial setup, update the `index` values in your mod first, then reload the map in CAIME. CAIME will not correct or repair mismatched indices on its own.

```
How Pharaoh Dynasties region order works in CAIME:
──────────────────────────────────────────────────────────────────

  campaign_map_regions (your mod)       map.hex binary file
  ───────────────────────────────       ────────────────────
  my_map    egypt    index=1            Slot 0: egypt
  my_map    nubia    index=2     ─────► Slot 1: nubia
  my_map    nile     index=3            Slot 2: nile
  my_map    redcoast index=4            Slot 3: redcoast
  my_map    medsea   index=5            Slot 4: medsea

  CAIME reads these indices on load. The database is the source of truth.
```

---

## 7. The Big Picture: Order of Operations

Here is the complete recommended sequence for creating a brand new map from scratch:

```
STEP 1: In RPFM and CAIME (first time only)
─────────────────────────────────────────────────────────
RPFM: set the game's folder in RPFM's settings
CAIME: Settings → Preferences
  └─ Set RPFM path and Output folder → Save


STEP 2: In RPFM, in your mod's pack
─────────────────────────────────────────────────────────
Add your map's rows to these tables:
  ├── campaigns                  (link map to campaign)
  ├── regions                    (define each new region + RGB colour)
  ├── campaign_map_regions       (link regions to your map)
  ├── campaign_map_roads         (road costs per campaign)
  ├── campaign_map_playable_areas (needed to process Map Data)
  │
  └── IF Three Kingdoms or Warhammer III:
      └── campaign_map_areas_of_interest
Save the pack.


STEP 3: In CAIME
─────────────────────────────────────────────────────────
File → Create new map (Ctrl+N)
  ├── Type your Campaign map name  (MUST match your mod's rows)
  ├── Choose Template: None
  ├── Choose your Game
  ├── Set Map size
  └── Click Create
       │
       ▼
  CAIME creates Projects\YourMapName\map.hex and opens it

Settings → RPFM Workflow
  └─ Set your mod's .pack → Save
File → Reload (Ctrl+R)
       │
       ▼
  CAIME reads the database through RPFM
  Swatches panel populates with your regions, terrain types, etc.


STEP 4: Start painting!
─────────────────────────────────────────────────────────
Select a Layer in the Layers panel →
Select a Swatch → Paint on the map canvas
```

---

## 8. Pro-Tips & Troubleshooting

- **The Swatches panel is empty after creating my map.**
  Either the database hasn't been read yet, or it has no rows for your map. Check that your mod is set under **Settings → RPFM Workflow**, that you saved the pack in RPFM, and that the `campaign_map` column of your `campaign_map_regions` rows exactly matches what you typed in the CAIME dialog (case-sensitive). Then reload the map with **File → Reload** (Ctrl+R).

- **I can't paint, and Save and Processing stay greyed out, after opening the new map.**
  The database is read in the background after the map opens, so give it a moment. If it stays unavailable, the log says why — usually one of two things: (a) the RPFM path is not set in **Settings → Preferences**, or (b) one of the required tables (such as `regions` or `campaign_map_regions`) has no valid entries in your mod or the game's packs. Check both, then reload the map with **File → Reload** (Ctrl+R).

- **I typed an odd number for the map width and got an error.**
  Map widths must be even numbers (e.g., 512, 1016, 1024). This is a technical requirement of the hex grid layout. Simply increase or decrease your width by 1 to make it even.

- **CAIME says my Pharaoh Dynasties map's region indices don't match, and the map stays view-only.**
  The `index` values in your mod's `campaign_map_regions` no longer match the order stored in the `map.hex` binary file. In RPFM, correct the `index` values so that land regions are numbered first and sea regions follow, with no gaps, then reload the map in CAIME. If you are unsure what order the binary file expects, restore your last working `map.hex` from the auto-backup folder (`Projects\YourMapName\`) and compare.

- **I added or removed regions — how do I make CAIME reflect the change?**
  Always make region changes in your mod first: add or remove the rows in `regions` and `campaign_map_regions` in RPFM, save the pack, then switch to CAIME and reload the map with **File → Reload** (Ctrl+R). CAIME reads the database on load and will pick up whatever is in the saved pack. It never writes to the database, so changes made only inside CAIME's interface will be lost on the next reload.

- **I see a warning about exceeding 731,520 hexes.**
  This is a performance advisory, not a hard limit. If your map is very large you may notice slower painting and longer processing times. For most custom campaigns, the default 1016 × 720 canvas is more than enough.

- **Can I change the game type after creating the map?**
  No. The game type is baked into the binary map file at creation time and cannot be changed. If you selected the wrong game, you must create a new project. You can, however, copy individual layer data to the new project using **Tools → Import → Layer data**. This is a subject to change in future CAIME releases.
