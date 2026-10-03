# Creating a New Campaign Map from Scratch

---

## Overview

Creating a brand-new campaign map in CAIME is a two-part process, similar to starting a new painting project.  
**Part 1 — In Dave (your Assembly Kit database editor):** You register your new map in the game's database tables so that CAIME (and eventually the game itself) knows which regions, terrain types, climates, and other game data belong to it.  
**Part 2 — Inside CAIME:** You create an empty canvas — the **map file** — and give it a name, a target game, and a size. CAIME reads the database the moment the map opens, so by doing Part 1 first, your Swatches panel will be fully populated and ready to paint straight away.

---

## Table of Contents

1. [Before You Begin: Configuring Assembly Kit Paths](#1-before-you-begin-configuring-assembly-kit-paths)
2. [Setting Up the Database in Dave](#2-setting-up-the-database-in-dave)
   - [campaign_maps](#campaign_maps)
   - [regions](#regions)
   - [campaign_map_regions](#campaign_map_regions)
   - [campaigns](#campaigns)
   - [campaign_map_roads](#campaign_map_roads)
   - [campaign_map_areas_of_interest (Three Kingdoms & Warhammer III only)](#campaign_map_areas_of_interest-three-kingdoms--warhammer-iii-only)
3. [Opening the Create New Map Dialog](#3-opening-the-create-new-map-dialog)
4. [Filling In the Map Details](#4-filling-in-the-map-details)
   - [Campaign Map Name](#campaign-map-name)
   - [Template](#template)
   - [Game](#game)
   - [Map Size](#map-size)
5. [What CAIME Creates for You](#5-what-caime-creates-for-you)
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

## 1. Before You Begin: Configuring Assembly Kit Paths

CAIME reads your game's database directly from your Assembly Kit installation. Before you create any map, you must tell CAIME where each game's Assembly Kit is installed on your computer. You only need to do this once per game.

**Steps:**

1. In CAIME, go to **Settings → Preferences** (or press **Ctrl+P**).

   The **Preferences** window will appear:

   ```
   ┌─ Preferences ────────────────────────────────────────────────────┐
   │                                                                   │
   │  Hex spacing:  [ ──●────────────────────── ] 0.00               │
   │  ─────────────────────────────────────────────────────────────   │
   │  Base game:          [ Rome2                              ▼ ]    │
   │  Assembly kit path:  [ C:\assembly_kit\...           ] [Browse]  │
   │  ─────────────────────────────────────────────────────────────   │
   │  Log to file:         ● Enabled    ○ Disabled                    │
   │  Auto-save:           ● Enabled    ○ Disabled                    │
   │  Auto-backup:         ● Enabled    ○ Disabled                    │
   │  Map data config auto-patch: ○ Enabled  ● Disabled               │
   │  ─────────────────────────────────────────────────────────────   │
   │                                              [ Save ] [ Cancel ] │
   └───────────────────────────────────────────────────────────────────┘
   ```

2. In the **Base game** dropdown, select the game you want to configure.
3. Click **Browse** next to **Assembly kit path** and navigate to the root folder of that game's Assembly Kit installation (the folder that contains `raw_data` and `working_data` sub-folders).
4. Click **Save**.
5. Repeat steps 2–4 for every game you plan to work with.

> **Important:** Each game has its own separate Assembly Kit installation and its own path entry. You must configure the correct path for the game you choose when creating your map, or CAIME will fail to open the map.

---

## 2. Setting Up the Database in Dave

Before creating the map file in CAIME, open **Dave** — the official database editor bundled with the Assembly Kit, specifically designed for editing these tables. The entries must use **exactly the same map name** you will type in the CAIME dialog in the next step.

The tables you need to open and edit in Dave are the ones that hold records **specific to your campaign map name**. Other tables (such as `campaign_ground_types` and `climates`) are global to the game and already contain all the data CAIME needs — no new rows are required in those.

The tables to edit for a new map are:

| Table | Why it needs editing |
|---|---|
| `campaign_maps` | Registers your map so the game and CAIME know it exists |
| `regions` | Defines any new regions your map introduces, with their RGB colours |
| `campaign_map_regions` | Links your map name to each of its regions |
| `campaigns` | Links your campaign name to your map name (needed for road filtering) |
| `campaign_map_roads` | Road cost entries that reference campaigns using your map |
| `campaign_map_areas_of_interest` | **Three Kingdoms & Warhammer III only** — registers Areas of Interest for your map |

Each table is described in detail below.

---

### campaign_maps

**What it is:** The master registry of campaign maps. The game and CAIME both use this to know that your map exists.

**What to add:** One new record with your map's name. Each game's schema is slightly different, but at minimum you need a field pointing to your map name (often called `campaign_map` or similar) and a field pointing to the campaign it belongs to.

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

> **Pharaoh & Pharaoh Dynasties only:** This table has an extra `index` field. See [Pharaoh & Pharaoh Dynasties](#pharaoh--pharaoh-dynasties) below for details.

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

## 3. Opening the Create New Map Dialog

Once the database is set up in Dave, switch to CAIME and create the map file.

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

- Must match **exactly** the name you used in the Dave database tables in [Section 2](#2-setting-up-the-database-in-dave) above.
- Will become the name of the project folder created on disk (`Projects\YourMapName\`).
- Cannot be empty — CAIME will show an error and stop if you try to proceed without one.

> **Tip:** Use only letters, numbers, and underscores (e.g., `my_custom_map`). Avoid spaces or special characters to prevent issues with the Assembly Kit.

When you click somewhere else after typing, CAIME will automatically clean up any disallowed characters in the name.

---

### Template

The **Template** dropdown lists any pre-built starting points stored in the `Templates` folder alongside CAIME.

| Choice | Meaning |
|---|---|
| **None** | Start with a completely blank map. You choose the game and size manually. |
| **A named template** | CAIME copies a pre-configured map from the Templates folder and renames it to your **Campaign map name**. The game and size are locked to whatever that template was designed for. |

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

After you click **Create**, CAIME automatically does the following:

```
CAIME creates on your disk:
─────────────────────────────────────────────────────────────────────────
 [CAIME install folder]\
 └── Projects\
     └── YourMapName\
         └── map.hex          ◄── Your new (empty) map file
─────────────────────────────────────────────────────────────────────────

CAIME also creates (if they don't exist already) output folders in your
Assembly Kit, ready for when you process the map later:

 [Your Assembly Kit path]\
 └── working_data\
     └── campaign_maps\
         └── YourMapName\     ◄── Where processed files will be exported
             └── debug\       ◄── Where debug images will go
─────────────────────────────────────────────────────────────────────────
```

The map file (`map.hex`) is completely empty at this stage. Every hex on the grid has:
- No region assigned
- No terrain type (ground type) assigned
- No climate assigned
- No attrition assigned
- No roads, bridges, rivers, or other features

CAIME immediately reads the Assembly Kit database after creating the file. Because you already set up the database entries in Dave, the Swatches panel will populate with your regions and terrain types and you can begin painting straight away.

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

Rome II has the simplest format. It is the only game that includes **Trade Routes** but lacks both **Restrictions** and **Region Borders**. No database tables beyond the standard set in [Section 2](#2-setting-up-the-database-in-dave) are required.

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

Three Kingdoms is the only game that supports every layer simultaneously, including both **Trade Routes** and **Areas of Interest**. Because Areas of Interest is active, you must populate one extra table in Dave before opening the map:

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

Warhammer III supports **Areas of Interest** but not **Trade Routes**. As with Three Kingdoms, you must add entries to `campaign_map_areas_of_interest` in Dave before opening the map (same table structure described in the Three Kingdoms section above).

---

### Pharaoh & Pharaoh Dynasties

> Pharaoh Dynasties shares the same map format as Pharaoh and behaves identically in CAIME. Select whichever matches your map in the Game dropdown — **Pharaoh** or **Pharaoh_Dynasties**.

| Detail | Value |
|---|---|
| **Map format version** | 18 (same as Warhammer group) |
| **Layers not available** | Trade Routes, Areas of Interest |
| **Special DB requirement** | Extra `index` field in `campaign_map_regions` |
| **Special region handling** | Combined land+sea index (see below) |

Pharaoh uses a unique system for storing regions in the binary map file. Instead of land regions and sea regions being stored separately (as in all other games), **all regions are stored together in a single combined list**, sorted by a numeric index.

This has two consequences:

**1. The `campaign_map_regions` table for Pharaoh has an extra `index` field.**

When adding your regions in Dave, each entry needs an `index` value — a whole number starting from 1. The index values **must be correct before you open the map in CAIME**, because CAIME reads them on load and will refuse to open the map if the index values do not match the order stored in the binary file. The convention is: **land regions first, then sea regions**, each numbered sequentially with no gaps.

| Example | Region type | index |
|---|---|---|
| `my_map_region_egypt` | land | 1 |
| `my_map_region_nubia` | land | 2 |
| `my_map_region_nile` | land | 3 |
| `my_map_region_redcoast` | sea | 4 |
| `my_map_region_medsea` | sea | 5 |

**2. CAIME only reads these index values — it never writes back to Dave.**

If you add, remove, or reorder regions after initial setup, you must update the `index` values in Dave first, then reload the map in CAIME. CAIME will not correct or repair mismatched indices on its own.

```
How Pharaoh region order works in CAIME:
──────────────────────────────────────────────────────────────────

  campaign_map_regions (Dave)           map.hex binary file
  ───────────────────────────           ────────────────────
  my_map    egypt    index=1            Slot 0: egypt
  my_map    nubia    index=2     ─────► Slot 1: nubia
  my_map    nile     index=3            Slot 2: nile
  my_map    redcoast index=4            Slot 3: redcoast
  my_map    medsea   index=5            Slot 4: medsea

  CAIME reads these indices on load. Dave is the source of truth.
```

---

## 7. The Big Picture: Order of Operations

Here is the complete recommended sequence for creating a brand new map from scratch:

```
STEP 1: In CAIME
─────────────────────────────────────────────────────────
Settings → Preferences
  └─ For each game: set Assembly kit path → Save


STEP 2: In Dave (Assembly Kit database editor)
─────────────────────────────────────────────────────────
Add your map name to these tables:
  ├── campaign_maps              (register the map)
  ├── campaigns                  (link map to campaign)
  ├── regions                    (define each new region + RGB colour)
  ├── campaign_map_regions       (link regions to your map)
  ├── campaign_map_roads         (road costs per campaign)
  │
  └── IF Three Kingdoms or Warhammer III:
      └── campaign_map_areas_of_interest


STEP 3: In CAIME
─────────────────────────────────────────────────────────
File → Create new map (Ctrl+N)
  ├── Type your Campaign map name  (MUST match the Dave entries)
  ├── Choose Template: None
  ├── Choose your Game
  ├── Set Map size
  └── Click Create
       │
       ▼
  CAIME creates Projects\YourMapName\map.hex
  CAIME reads the Assembly Kit database
  Swatches panel populates with your regions, terrain types, etc.


STEP 4: Start painting!
─────────────────────────────────────────────────────────
Select a Layer in the Layers panel →
Select a Swatch → Paint on the map canvas
```

---

## 8. Pro-Tips & Troubleshooting

- **The Swatches panel is empty after creating my map.**
  This means CAIME could not find any entries for your map in the Assembly Kit database. Open Dave and confirm that your map name in the `campaign_map` column of `campaign_map_regions` exactly matches what you typed in the CAIME dialog (case-sensitive). Then reload the map with **File → Reload** (Ctrl+R).

- **CAIME shows a "failed to load" error when opening the new map.**
  This usually means one of two things: (a) the Assembly Kit path for your game is not set in **Settings → Preferences**, or (b) one of the required tables (such as `regions` or `campaign_map_regions`) has no valid entries in the Assembly Kit's `raw_data\db\` folder. Check both before retrying.

- **I typed an odd number for the map width and got an error.**
  Map widths must be even numbers (e.g., 512, 1016, 1024). This is a technical requirement of the hex grid layout. Simply increase or decrease your width by 1 to make it even.

- **My Pharaoh map will not open — CAIME says the region indices are mismatched.**
  The `index` values in `campaign_map_regions` in Dave no longer match the order stored in the `map.hex` binary file. Open Dave, correct the `index` values so that land regions are numbered first and sea regions follow, with no gaps, then try opening the map again in CAIME. If you are unsure what order the binary file expects, restore your last working `map.hex` from the auto-backup folder (`Projects\YourMapName\`) and compare.

- **I added or removed regions — how do I make CAIME reflect the change?**
  Always make region changes in Dave first: add or remove the rows in `regions` and `campaign_map_regions`, then switch to CAIME and reload the map with **File → Reload** (Ctrl+R). CAIME reads the database on load and will pick up whatever is currently in Dave. It never writes back to the database, so changes made only inside CAIME's interface will be lost on the next reload.

- **I see a warning about exceeding 731,520 hexes.**
  This is a performance advisory, not a hard limit. If your map is very large you may notice slower painting and longer processing times. For most custom campaigns, the default 1016 × 720 canvas is more than enough.

- **Can I change the game type after creating the map?**
  No. The game type is baked into the binary map file at creation time and cannot be changed. If you selected the wrong game, you must create a new project. You can, however, copy individual layer data to the new project using **Tools → Import → Layer data**. This is a subject to change in future CAIME releases.
