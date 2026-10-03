# Processing & Exporting Your Campaign Map

## Overview

Processing is the step that turns your painted hex map into the actual binary files that Total War games read at runtime. Think of it like a film developing process — you spend time painting and crafting your map in CAIME, then "develop" it into the final formats the game engine can use. Each process type reads specific painted layers from your map and outputs a different game file. Before any processing can happen, CAIME needs to know where your game's Assembly Kit is installed, because it reads game data directly from that folder.

Supported games: **Rome 2, Attila, Thrones of Britannia, Warhammer, Warhammer 2, Warhammer 3, Three Kingdoms, Troy, Pharaoh, Pharaoh Dynasties**.

> **Prefer the command line?** Every task in the **Process** menu can also be run from a terminal, without opening the editor — handy for scripting and batch processing. So can layer import and export, painting, and creating projects. See [Using CAIME from the Command Line (CLI)](user-guide-command-line-interface.md).

---

## Table of Contents

- [Part 1 — First-Time Setup: Connecting to Your Game's Assembly Kit](#part-1--first-time-setup-connecting-to-your-games-assembly-kit)
  - [What Is the Assembly Kit and Why Does CAIME Need It?](#what-is-the-assembly-kit-and-why-does-caime-need-it)
  - [How to Set the Assembly Kit Path](#how-to-set-the-assembly-kit-path)
  - [What CAIME Reads from the Assembly Kit](#what-caime-reads-from-the-assembly-kit)
- [Part 2 — The Process Menu: Generating Game Files](#part-2--the-process-menu-generating-game-files)
  - [Map Data](#21-map-data)
  - [Pathfinding Data](#22-pathfinding-data)
  - [Borders Data](#23-borders-data)
  - [Dynamic Resources](#24-dynamic-resources)
  - [Trade Routes](#25-trade-routes)
  - [Lookup and Minimap Images](#26-lookup-and-minimap-images)
- [Part 3 — The Tools › Validate Menu: Checking Your Work](#part-3--the-tools--validate-menu-checking-your-work)
  - [Rivers](#31-rivers)
  - [Roads](#32-roads)
  - [Bridges](#33-bridges)
  - [Beaches](#34-beaches)
  - [Regions](#35-regions)
  - [Town Slots](#36-town-slots)
  - [Attritions](#37-attritions)
  - [Climates](#38-climates)
  - [Ground Types](#39-ground-types)
  - [Impassable](#310-impassable)
  - [Town Sprawl](#311-town-sprawl)
- [Part 4 — The Tools › Export Menu: Saving Layers as Images](#part-4--the-tools--export-menu-saving-layers-as-images)
  - [SVG Borders](#41-svg-borders)
  - [Baseline Tilemap Image](#42-baseline-tilemap-image)
  - [Layer Data (Export Layers to Images)](#43-layer-data-export-layers-to-images)
- [Part 5 — Quick Reference: What Goes In, What Comes Out](#part-5--quick-reference-what-goes-in-what-comes-out)
- [Part 6 — Pro-Tips & Troubleshooting](#part-6--pro-tips--troubleshooting)

---

## Part 1 — First-Time Setup: Connecting to Your Game's Assembly Kit

### What Is the Assembly Kit and Why Does CAIME Need It?

The **Assembly Kit** (also called the Assembly Kit or "AKit") is Creative Assembly's official modding tool that ships with each Total War game on Steam. It contains a folder called `raw_data` that holds all of the game's definition tables — a structured list of every region name, every ground type, every climate zone, every road type, and more.

CAIME reads these tables every time you open a project. This is how it knows things like:

- Which region names are valid for your campaign map
- What movement costs each ground type has
- Which attrition effects are defined in the game
- What road thresholds trigger different road levels

> **Think of it this way:** the Assembly Kit database is the game's rulebook. CAIME is the referee who checks your painted map against that rulebook before allowing you to export anything.

Without a valid Assembly Kit path, CAIME cannot load a project or run any processing step.

---

### How to Set the Assembly Kit Path

```
┌─────────────────────────────────────────────────────────┐
│  Menu Bar                                               │
│  [ File ]  [ Edit ]  [ Process ]  [ Tools ]  [Settings]│
│                                                     ↑   │
│                                              Click here │
└─────────────────────────────────────────────────────────┘
         ↓
┌─────────────────────────┐
│  Settings               │
│  ┌─────────────────┐    │
│  │   Preferences   │ ←  │ Click this
│  └─────────────────┘    │
└─────────────────────────┘
```

1. Go to **Settings** in the top menu bar and click **Preferences**.

2. The **Preferences** window will open. It contains several sections:

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│  Preferences                                                                                                                │
│  ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────  │
│  Hex spacing:          [────●──────────────────────]                                                                        │
│  ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────  │
│  Base game:            [ Attila                    ▼]                                                                       │
│  Assembly kit path:    [C:\Program Files (x86)\Steam\SteamLibrary\steamapps\common\Total War Attila\assembly_kit] [Browse]  │
│  ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────  │
│  Log to file:          (●) Enabled    ( ) Disabled                                                                          │
│  Auto-save:            (●) Enabled    ( ) Disabled                                                                          │
│  Auto-backup:          (●) Enabled    ( ) Disabled                                                                          │
│  Map data config                                                                                                             │
│  auto-patch:           (●) Enabled    ( ) Disabled                                                                          │
│  ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────  │
│                                  [ Save ]  [ Cancel ]                                                                       │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

3. In the **Base game** dropdown, **select the game you are modding**. The path you set applies to the currently selected game — each game stores its own separate path.

4. In the **Assembly kit path** field, either type the path directly or click **Browse** to open a folder picker. Navigate to and select the root folder of your Assembly Kit installation for that game.

   > **The correct folder** is the one that contains subfolders named `binaries`, `raw_data`, and `working_data`. For example:
   > ```
   > C:\Program Files (x86)\Steam\SteamLibrary\steamapps\common\Total War Attila\assembly_kit
   > ```

5. Click **Save** to apply the change.

6. Repeat steps 3–5 for **each game** you intend to mod. CAIME remembers a separate path per game.

> **Important:** After changing the Assembly Kit path, close and re-open your project for the change to take effect.

---

### What CAIME Reads from the Assembly Kit

When you open a project, CAIME automatically reads these files from `{Assembly Kit path}\raw_data\db\`:

| File in Assembly Kit | What it provides to CAIME |
|---|---|
| `regions.xml` | The list of all region names and their map colours |
| `campaign_map_regions.xml` | Which regions belong to your campaign map |
| `campaign_ground_types.xml` | Ground type names and movement costs |
| `climates.xml` | Climate zone definitions |
| `campaign_map_attritions.xml` | Attrition effect definitions (terrain_land and terrain_sea only) |
| `campaign_map_roads.xml` | Road types and movement cost thresholds |
| `campaigns.xml` + `campaign_maps.xml` | Campaign structure (which campaign uses which map) |
| `region_to_province_junctions.xml` | Which province each region belongs to |
| `campaign_map_areas_of_interest.xml` | *(Three Kingdoms and Warhammer 3 only)* Areas of interest |

> **The region order matters.** CAIME loads land regions first, then sea regions, in the order they appear in `regions.xml`. Your painted region colours must correspond to the regions in that exact order. If you add or remove regions in the Assembly Kit database, you must reload your project so CAIME can re-synchronise.

---

## Part 2 — The Process Menu: Generating Game Files

Access all processing features from the **Process** menu in the top menu bar:

```
┌──────────────────────────────────────────────────────┐
│  [ File ]  [ Edit ]  [ Process ]  [ Tools ]          │
│                          │                           │
│                  ┌───────┴───────────────┐           │
│                  │ Map Data              │           │
│                  │ Pathfinding data      │           │
│                  │ Borders data          │           │
│                  │ Dynamic resources     │           │
│                  │ Trade Routes          │           │
│                  │ Lookup and Minimap    │           │
│                  │   images              │           │
│                  └───────────────────────┘           │
└──────────────────────────────────────────────────────┘
```

> **Always save your project first** (**File > Save** or **Ctrl+S**) before running any process. CAIME will refuse to run **Map Data** or **Dynamic Resources** if you have unsaved changes.

---

### 2.1 Map Data

**What it does:** Generates the `map_data.esf` file — the core settlement and region data file that defines how many building slots each settlement has, whether it has a port, and the layout of the campaign map's strategic layer. This step uses an external tool called **MapDataBuilder** that is included with CAIME.

**Special requirements (read carefully!):**

> **This process has strict folder requirements.** Your project file (`map.hex`) must be saved inside your Assembly Kit folder at this exact location:
> ```
> {Assembly Kit path}\raw_data\EmpireDesignData\campaign_maps\{your map name}\map.hex
> ```
> If your file is saved anywhere else, the process will be blocked and you will see an error in the Logger.

**How to use it:**

1. Make sure your project is saved at the required path (see above).

2. **Close the Assembly Kit application** (Tweak.AssemblyKit) if it is open. Running both at the same time will cause a conflict and the process will be blocked.

3. Go to **Process > Map Data**.

4. Processing runs automatically. The Logger will show output from the MapDataBuilder tool. Look for the message `Finished creating map` in the Logger to confirm success.

5. If **Map data config auto-patch** is enabled in Preferences, CAIME will automatically apply your Map Data Editor configuration after the file is generated (see the note below).

**What it reads:**
- Your painted map layers
- All Assembly Kit database tables
- The Assembly Kit `binaries` folder (MapDataBuilder uses this internally)
- *(Optional)* `caime_metadata.json` + the referenced `map_data_config.xml` (if auto-patch is enabled)

**Output file:** `{Assembly Kit path}\working_data\campaign_maps\{your map name}\map_data.esf`

> **About Map data config auto-patch:** If you have used the **Map Data Editor** tool (**Tools > Map Data Editor**, **Ctrl+M**) to customise building slot counts for your regions and saved that configuration, CAIME can automatically apply those customisations to the generated `map_data.esf` right after processing. Enable this in **Settings > Preferences** by setting **Map data config auto-patch** to **Enabled**.

---

### 2.2 Pathfinding Data

**What it does:** Calculates every movement rule on your map — how armies travel, which hexes are blocked, what bridges connect, how coast lines work, and how roads speed up movement. This is one of the most important files for a working campaign.

**How to use it:**

1. Go to **Process > Pathfinding data**.

2. Processing runs automatically. Watch the **Logger** panel (open it via **Tools > Show Logger** or **Ctrl+L**) to see progress. No additional windows appear.

3. When finished, a success message will appear in the Logger.

**What it reads:** Every painted layer contributes:

| Layer | What it contributes |
|---|---|
| Ground Types | Movement cost per hex |
| Regions | Which region each hex belongs to |
| Rivers | River crossing penalties |
| Roads | Road movement bonuses |
| Bridges | Bridge crossing connections |
| Beaches | Amphibious landing zones |
| Impassable | Hexes armies cannot enter |
| Town Slots | Settlement locations |
| Trade Routes | Trade route connectivity |

> **Tip:** Run all the validators under **Tools > Validate** before running Pathfinding. Errors in your bridges, rivers, or roads will produce incorrect pathfinding data.

**Output file:** `{Assembly Kit path}\working_data\campaign_maps\{your map name}\pathfinding.bin`

---

### 2.3 Borders Data

**What it does:** Reads every place where two regions meet on your map and writes the border geometry that the game draws on the campaign screen — the lines that divide one faction's territory from another's.

**How to use it:**

1. Go to **Process > Borders data**.

2. The **"Select borders to export"** window will open, showing a table of border type combinations:

```
┌───────────────────────────────────────────────────────────────┐
│  Select borders to export                                      │
│  ┌─────────────┬──────────────────────────────────────────┐   │
│  │ Border type │ Export  │ Notes                          │   │
│  ├─────────────┼─────────┼────────────────────────────────┤   │
│  │ Land–Land   │  ✓      │ Border between two land regions│   │
│  │ Land–Sea    │  ✓      │ Coastline border               │   │
│  │ Sea–Sea     │  ✗      │ (disabled by default)          │   │
│  │ Land–MI     │  ✓      │ ...                            │   │
│  │ ...         │  ...    │ ...                            │   │
│  └─────────────┴─────────┴────────────────────────────────┘   │
│                                                               │
│  [ Cancel ]        [ Reset ]        [ Export ]               │
└───────────────────────────────────────────────────────────────┘
```

   The columns tell you which type of border is between which types of areas:
   - **Land** = a normal land region
   - **Sea** = a sea region
   - **TI** = a theatre island (disconnected island region)
   - **MI** = a Mediterranean island (smaller island region)

3. Check or uncheck the border combinations you need. The defaults shown are the recommended ones for your selected game.

4. If you make changes and want to go back to defaults, click **Reset**.

5. Click **Export** to generate the file.

**What it reads:** The painted Regions layer.

**Output file:** `{Assembly Kit path}\working_data\campaign_maps\{your map name}\display\borders\borders.pbd`

---

### 2.4 Dynamic Resources

**What it does:** Generates the `dynamic_resources.esf` file, which defines the resource icons and positions that appear on the campaign map (things like food, wood, stone, and trade goods shown on the map overlay).

**Special requirements:** Same as Map Data — your project must be saved inside the Assembly Kit folder and the Assembly Kit application must be closed.

**How to use it:**

1. Make sure your project is saved at:
   ```
   {Assembly Kit path}\raw_data\EmpireDesignData\campaign_maps\{your map name}\map.hex
   ```

2. Close the Assembly Kit application if it is open.

3. Go to **Process > Dynamic resources**.

4. The Logger will show progress. Processing completes automatically.

**What it reads:** Assembly Kit database tables + painted map layers.

**Output file:** `{Assembly Kit path}\working_data\campaign_maps\{your map name}\dynamic_resources.esf`

---

### 2.5 Trade Routes

**What it does:** Calculates and exports all trade route paths across your map — both land and sea routes. This tells the game which hexes connect to form each tradeable route so that trade income and resource flows work correctly.

**How to use it:**

1. Go to **Process > Trade Routes**.

2. Processing runs automatically. The Logger will report statistics: how many land routes and sea routes were found, how many routes could not be resolved (unreachable), and how many spline segments were generated.

**What it reads:** The painted Trade Routes layer, Regions layer, and Sea/Land hex classification.

**Output file:** `{Assembly Kit path}\working_data\campaign_maps\{your map name}\trade_routes.ptd`

---

### 2.6 Lookup and Minimap Images

**What it does:** Generates the map images used on the campaign screen:
- The **lookup image** is a colour-coded map the game uses internally to identify regions at a glance
- The **minimap image** is the small overview image shown in the corner of the campaign screen

The output format depends on your game:
- **Warhammer, Warhammer 2, Warhammer 3, Troy, Three Kingdoms** → BMP format
- **All other games** → TGA format

**How to use it:**

1. Go to **Process > Lookup and Minimap images**.

2. Processing runs automatically and saves the files to the working data folder.

**What it reads:** All painted layers, primarily the Regions layer (each region's colour determines its zone in the lookup image).

**Output files:** Multiple TGA or BMP images saved to `{Assembly Kit path}\working_data\campaign_maps\{your map name}\`

---

## Part 3 — The Tools › Validate Menu: Checking Your Work

The Validate tools are like a spell-checker for your campaign map. They scan your painted layers for common mistakes before you try to export, so you can fix problems early. All results appear in the **Logger** (**Tools > Show Logger** or **Ctrl+L**).

There are three types of messages:
- **Error** (red) — a serious problem that will cause the export to fail or the game to crash. Must be fixed.
- **Warning** (yellow) — a likely mistake that may cause in-game issues. Strongly recommended to fix.
- **Info** (white) — a potential concern that is technically allowed but might be unintentional. Review these.

Access all validators from **Tools > Validate**:

```
┌──────────────────────────────────────────────────────┐
│  [ Tools ]                                           │
│       │                                              │
│  ┌────┴─────────────────────────────┐               │
│  │ Show Logger         Ctrl+L       │               │
│  │ Border Editor       Ctrl+B       │               │
│  │ Map Data Editor     Ctrl+M       │               │
│  │ Import ›                         │               │
│  │ Export ›                         │               │
│  │ Validate ›                       │ ← hover here  │
│  └───────────────────────────────┬──┘               │
│                                  │                  │
│                     ┌────────────┴────────┐         │
│                     │ Rivers              │         │
│                     │ Town slots          │         │
│                     │ Roads               │         │
│                     │ Bridges             │         │
│                     │ Beaches             │         │
│                     │ Regions             │         │
│                     │ Attritions          │         │
│                     │ Climates            │         │
│                     │ Ground types        │         │
│                     │ Impassable          │         │
│                     │ Town sprawl         │         │
│                     └─────────────────────┘         │
└──────────────────────────────────────────────────────┘
```

---

### 3.1 Rivers

**What it checks:** That every river hex is correctly connected to at least one neighbouring river hex, and that no river hex sits on sea.

**How to use it:** Go to **Tools > Validate > Rivers**.

**Common errors it catches:**

| Message in Logger | What it means | How to fix it |
|---|---|---|
| *"river flag is inconsistent with its river edge mask; it does not connect to a neighbouring river"* | A hex is marked as a river but none of its six neighbours are also rivers — it is completely isolated with no valid connection. | Either connect it to another river hex, or remove the river paint from that hex. |
| *"is both a river and sea"* | A river hex was painted on top of a sea hex. Rivers must only exist on land. | Remove the river paint from that hex, or change the hex to land. |
| *"has a river edge pointing off the map"* | A river runs along the edge of the map and points outside the map boundary. | Redirect the river so it does not flow off the map edge. |
| *"has a river edge pointing into a sea hex"* | A river hex is connected by edge to a neighbouring sea hex. | Ensure the river terminates on land, not at the coast. |
| *"is an isolated river hex (allowed, but maybe a mistake)"* | A single river hex has no other river neighbours. | If intentional, this can be ignored. Otherwise, connect or remove it. |

---

### 3.2 Roads

**What it checks:** That road hexes are placed on valid terrain and that every road connects to at least one other road hex.

**How to use it:** Go to **Tools > Validate > Roads**.

**Common errors it catches:**

| Message in Logger | What it means | How to fix it |
|---|---|---|
| *"is both a road and sea, and not a bridge"* | A road hex was painted on a sea hex without being designated as part of a bridge. | Remove the road paint from the sea hex, or mark the hex as a bridge if it is supposed to be one. |
| *"is both a road and cliff or beach, and is not leading to a bridge"* | A road runs onto a cliff or beach hex that is not a bridge cliff approach. | Remove the road, or correctly set up the bridge cliff approach (see Bridges). |
| *"is a road but connects to no other road (empty road edge mask)"* | A road hex is completely surrounded by non-road hexes. | Connect it to the road network or remove it. |
| *"is an isolated road hex (allowed, but maybe a mistake)"* | A road hex has no road neighbours (similar to above). | Connect or remove it. |
| *"creates a three-way intersection (allowed, but messy)"* | Three roads meet at a single hex junction. This is technically legal but can create visual and gameplay messiness. | Consider splitting the junction into two separate paths. |
| *"is both a road and impassable (allowed, but frowned upon?)"* | A road hex is also marked impassable. | Armies cannot travel this hex anyway, so the road paint is functionally useless here. |

---

### 3.3 Bridges

**What it checks:** That every bridge is correctly structured — it sits on sea hexes, connects two separate land sides, and has at least one **bridge-cliff** hex on the approach.

**How to use it:** Go to **Tools > Validate > Bridges**.

**Common errors it catches:**

| Message in Logger | What it means | How to fix it |
|---|---|---|
| *"is both a bridge and land"* | A bridge hex was accidentally painted on a land hex. Bridges must cross water. | Move the bridge paint to sea hexes only. |
| *"does not connect two separate land sides; it leads nowhere"* | The bridge does not link two distinct land areas. | Ensure the sea hexes of the bridge connect land on both sides. |
| *"has no bridge-cliff land hex on either side"* | Neither end of the bridge has a bridge-cliff hex. The bridge-cliff is a special land hex type that marks the road approach to the bridge. | Paint a bridge-cliff hex on the land immediately adjacent to where the bridge meets the shore on each side. |

---

### 3.4 Beaches

**What it checks:** That beach hexes are placed on land, adjacent to sea, and not overlapping cliffs.

**How to use it:** Go to **Tools > Validate > Beaches**.

**Common errors it catches:**

| Message in Logger | What it means | How to fix it |
|---|---|---|
| *"is both a beach and sea"* | A beach hex was painted on a sea hex. Beaches must be on land next to the water. | Move the beach paint to the land hexes along the coast. |
| *"is both a beach and a cliff"* | A single hex is marked as both a beach and a cliff. These are incompatible. | Remove one of the two — a hex can be a beach or a cliff, not both. |
| *"has no neighboring sea hex"* | A beach hex is surrounded by land on all six sides. Beaches must be adjacent to at least one sea hex. | Move the beach closer to the coast, or remove it. |
| *"is an isolated beach hex (allowed, but maybe a mistake)"* | A beach hex with no neighbouring beach hexes. | If intentional (a tiny cove), ignore it. Otherwise connect it to a beach chain. |

---

### 3.5 Regions

**What it checks:** That every hex has a region assigned, that the map's regions match the Assembly Kit database regions, and that no land region is split into disconnected blobs. It also warns if any sea region touches too many land regions (which can cause AI slowdowns).

**How to use it:** Go to **Tools > Validate > Regions**.

**Common errors it catches:**

| Message in Logger | What it means | How to fix it |
|---|---|---|
| *"There is a mismatch between database regions (count = X) and map.hex regions (count = Y)"* | The number of regions painted on your map does not match the number of regions defined in the Assembly Kit database. | Make sure your Assembly Kit database contains entries for exactly the same regions you have painted. Add or remove regions in both places until the counts match. |
| *"Region names mismatch. DB region key was X. Map.hex region key was Y."* | A region at a specific index has a different name in the database versus the map. The order of regions must be identical between the two. | Re-check the order of regions in your `regions.xml` and `campaign_map_regions.xml` files. |
| *"Hex has no region set"* | One or more hexes were left without a region assigned. Every single hex on the map must belong to a region. | Paint the region colour onto all unpainted hexes. |
| *"is split into X disconnected areas. This may lead to startpos processing issues."* | A land region is painted in two or more separate island-like blobs that do not connect. | Repaint the region so it forms one continuous area, or use separate regions for each island. |
| *"has X adjacent land regions, more than 9 means slowdown due to AI calculations, split it up!"* | A sea region borders too many different land regions. This causes the campaign AI to slow down significantly. | Split the sea region into two or more smaller sea regions. |
| *"has 0 passable region edge hexes. This may be valid and intentional but double check it."* | A region has no hexes on its border that armies can cross into from a neighbouring region. | Make sure the border hexes between this region and its neighbours are not all impassable. |
| *"does not have any hexes assigned"* | A region exists in the database but has no painted hexes on the map. | Either paint hexes for this region or remove it from the database. |

---

### 3.6 Town Slots

**What it checks:** That every settlement (town slot) is placed correctly inside a town sprawl area, that slots have the right size, and that port slots are adjacent to sea.

**How to use it:** Go to **Tools > Validate > Town slots**.

**Common errors it catches:**

| Message in Logger | What it means | How to fix it |
|---|---|---|
| *"Town Slot outside Town Sprawl (use Town Sprawl auto-generate to fix!)"* | A town slot hex was painted outside the town sprawl area. The town sprawl is the surrounding ring of hexes that marks the city's footprint. | Use the **Town Sprawl auto-generate** tool to automatically recalculate the correct sprawl, or manually paint the sprawl hexes around the slot. |
| *"Non-port Town Slot is on sea hex"* | A regular settlement slot (not a port) is placed on a sea hex. | Move the settlement onto land. |
| *"Town Slot is the wrong size"* | A main settlement slot has the wrong number of hexes. For Warhammer games, the main slot must be exactly 19 hexes (or 16 for a port settlement). | Repaint the slot to the correct hex count. |
| *"Town Slot needs at least one land hex with 6 adjacent same-slot hexes"* | No single hex in the slot is fully surrounded by other hexes of the same slot. The game requires at least one "centre" hex surrounded on all 6 sides. | Reshape the slot so that at least one hex has 6 same-slot neighbours. |
| *"Port Slot needs at least one hex in a sea region"* | A port slot exists but none of its hexes touch a sea region. | Paint the port slot so that at least one of its hexes is adjacent to a sea hex. |
| *"has a port slot but is not adjacent to any sea"* | A land region has a port slot painted, but the region does not have any coast hexes. | Remove the port slot or extend the region to reach the coast. |
| *"Town Sprawl overlaps region border"* | The town sprawl of one settlement crosses into an adjacent region's territory. | Resize or reposition the town sprawl so it stays within its own region. |
| *"is split into X disconnected clusters (duplicate town slot)"* | The same slot number appears in two separate locations within the same region. | Remove the duplicate. Each slot number can appear only once per region (as one connected cluster). |
| *"Town Slot has only X hex(es); a town slot needs at least 7"* | A slot is too small — it needs at least 7 hexes (the centre plus its 6 neighbours). | Expand the slot to at least 7 hexes. |
| *"has no Town Slots but is not fully impassable"* | A land region has passable hexes but no settlement. | Either add a settlement to the region, or mark all its hexes as impassable (for wasteland regions). |

---

### 3.7 Attritions

**What it checks:** That the Attritions layer data is internally consistent with the definitions loaded from the Assembly Kit database.

**How to use it:** Go to **Tools > Validate > Attritions**. Results appear in the Logger.

---

### 3.8 Climates

**What it checks:** That the Climates layer data is internally consistent with the climate definitions loaded from the Assembly Kit database.

**How to use it:** Go to **Tools > Validate > Climates**. Results appear in the Logger.

---

### 3.9 Ground Types

**What it checks:** That the Ground Types layer data is internally consistent with the ground type definitions loaded from the Assembly Kit database.

**How to use it:** Go to **Tools > Validate > Ground types**. Results appear in the Logger.

---

### 3.10 Impassable

**What it checks:** That the impassable layer has no one-hex-wide holes, and that town slots don't sit on impassable ground.

**How to use it:** Go to **Tools > Validate > Impassable**.

**Common errors it catches:**

| Message in Logger | What it means | How to fix it |
|---|---|---|
| *"has a town slot but is marked impassable"* | A settlement hex is also flagged impassable, which contradicts having a slot there. | Remove the impassable mark from the settlement's hexes, or move the slot. |
| *"is a passable hole fully enclosed by impassable hexes"* | A passable hex is completely boxed in by impassable neighbours, so units could never reach it. | Use **Plug Holes** in the Actions panel, or manually repaint the hex or its surroundings. |
| *"is an isolated impassable hex"* | A single impassable hex has no impassable neighbours. Allowed, but may be a stray mark. | Review whether this hex is meant to be impassable. |

---

### 3.11 Town Sprawl

**What it checks:** That each settlement's Town Sprawl footprint is a single blob that stays inside its own region and does not get pinched between hazard terrain.

**How to use it:** Go to **Tools > Validate > Town sprawl**.

**Common errors it catches:**

| Message in Logger | What it means | How to fix it |
|---|---|---|
| *"is sprawl but is also impassable, river or cliff terrain"* | A sprawl hex sits on terrain that shouldn't be buildable. | Remove the sprawl mark or the conflicting terrain type. |
| *"is part of a sprawl blob but belongs to a different region than the rest of the blob"* | A sprawl hex leaks into a neighbouring **land** region. Sprawl reaching over an adjacent **sea** region (for a port) is expected and not flagged. | Repaint the sprawl so it stays inside its own land region. |
| *"has its sprawl split across X separate blobs"* | One region's sprawl forms more than one disconnected patch. | Merge the patches into a single blob, or confirm each patch belongs to a genuinely different settlement. |
| *"contains no town slot hex"* (Info) | A sprawl blob has no settlement slot inside it. Some earlier games allow sprawl without a slot. | Add a town slot, or ignore if intentional for your game. |
| *"comes within 2 hexes of the sprawl blob ... without touching it directly"* | Impassable/beach/river/cliff terrain sits just outside the blob (2 hexes away) without ever bordering it. Sprawl must either directly touch such terrain or stay at least 3 hexes clear of it. | Extend the sprawl to touch the terrain, or move the sprawl (or the terrain) further apart. |
| *"borders X separate impassable/beach/river/cliff areas"* | The sprawl blob touches more than one separate patch of hazard terrain, pinching it into a bottleneck. | Reshape the sprawl or the surrounding terrain so only one hazard patch borders the blob. |

---

## Part 4 — The Tools › Export Menu: Saving Layers as Images

These tools save visual representations of your map layers as image files. They are primarily for inspection and debugging — useful for checking that your layers look correct before committing to a full processing run. Access them from **Tools > Export**.

---

### 4.1 SVG Borders

**What it does:** Exports the region border lines from your map as an **SVG vector file** — a format you can open in tools like Inkscape or Adobe Illustrator. This is useful for inspecting your borders, creating map art, or sharing a human-readable diagram of your region layout.

**How to use it:**

1. Go to **Tools > Export > SVG Borders**.

2. A Windows file save dialog will appear. Choose where to save the file and click **Save**.

3. The file is saved. Open it in any SVG viewer or vector editor.

**What it reads:** The Regions layer.

**Output:** An `.svg` file at the location you chose.

---

### 4.2 Baseline Tilemap Image

**What it does:** Exports a **PNG image** showing the tile map of your map — a visual snapshot of the roads, rivers, cliffs, and beaches as they are currently painted. This is useful as a reference image for artists working on the campaign terrain graphics.

**How to use it:**

1. Go to **Tools > Export > Baseline Tilemap image**.

2. A Windows file save dialog will appear. Choose where to save the `.png` file and click **Save**.

**What it reads:** Roads, Rivers, Cliffs, and Beaches layers.

**Output:** A `tile_map.png` file at the location you chose.

---

### 4.3 Layer Data (Export Layers to Images)

**What it does:** Exports one or more of your painted layers as image or binary files. This is useful for backups, sharing work-in-progress layers with others, or importing layers into a new project.

**How to use it:**

1. Go to **Tools > Export > Layer data**.

2. The **"Export layers to images"** window will open:

```
┌────────────────────────────────────────────────────────┐
│  Export layers to images                               │
│  ┌────────────────────────────────────┐ ┌───────────┐ │
│  │ ☑ Export ground types layer       │ │           │ │
│  │ ☑ Export rivers layer             │ │  [Mode ▼] │ │
│  │ ☑ Export climates layer           │ │           │ │
│  │ ☑ Export attritions layer         │ └───────────┘ │
│  │ ☑ Export regions layer            │               │
│  │ ☑ Export region borders layer     │               │
│  │ ☑ Export beaches layer            │               │
│  │ ☑ Export bridges layer            │               │
│  │ ☑ Export town sprawl layer        │               │
│  │ ☑ Export town slots layer         │               │
│  │ ☑ Export roads layer              │               │
│  │ ☑ Export trade routes layer       │ [ Export ]    │
│  │ ☑ Export impassable layer         │ [ Cancel ]    │
│  └────────────────────────────────────┘               │
└────────────────────────────────────────────────────────┘
```

3. **Check** the layers you want to export.

4. Use the **mode dropdown** on the right to choose the export format (image format options).

5. Click **Export**. A folder picker will appear — select the folder where you want the files saved.

6. All selected layers are exported as separate files into that folder.

---

## Part 5 — Quick Reference: What Goes In, What Comes Out

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  PROCESS / EXPORT          LAYERS / DATA READ          OUTPUT FILE          │
├─────────────────────────────────────────────────────────────────────────────┤
│  Map Data                  All layers + AKit DB +      map_data.esf         │
│                            map_data_config.xml (opt.)                       │
│                                                                             │
│  Pathfinding Data          Ground Types, Regions,      pathfinding.bin      │
│                            Rivers, Roads, Bridges,                          │
│                            Beaches, Impassable,                             │
│                            Town Slots, Trade Routes                         │
│                                                                             │
│  Borders Data              Regions                     borders.pbd          │
│                                                                             │
│  Dynamic Resources         All layers + AKit DB        dynamic_resources.esf│
│                                                                             │
│  Trade Routes              Trade Routes, Regions       trade_routes.ptd     │
│                                                                             │
│  Lookup & Minimap          All layers (esp. Regions)   lookup_*.tga / .bmp  │
│  Images                                                minimap_*.tga / .bmp │
│                                                                             │
│  SVG Borders               Regions                     *.svg (your choice)  │
│                                                                             │
│  Baseline Tilemap          Roads, Rivers,              tile_map.png         │
│                            Cliffs, Beaches             (your choice)        │
│                                                                             │
│  Layer Data Export         Any selected layers         Per-layer image files│
└─────────────────────────────────────────────────────────────────────────────┘
```

**All Process menu outputs go to:**
```
{Assembly Kit path}\working_data\campaign_maps\{your map name}\
```

---

## Part 6 — Pro-Tips & Troubleshooting

**Run validators before every export.**
The validators under **Tools > Validate** are fast and reveal problems early. A broken bridge or a region count mismatch will silently corrupt your exported files if not caught first. Make it a habit to run **Regions**, **Town Slots**, **Roads**, and **Bridges** before each export session.

**"Map Data Process denied" — what it means and how to fix it.**
If you see this message in the Logger, it means your project file is saved in the wrong location. For **Map Data** and **Dynamic Resources** to work, your project file must be named exactly `map.hex` and saved inside your Assembly Kit folder at `{Assembly Kit path}\raw_data\EmpireDesignData\campaign_maps\{your map name}\`. The file must also be saved (no unsaved changes) and the Assembly Kit application must be closed. Move the file there using **File > Save as...** and try again.

**"Mismatch between database regions and map.hex regions" — what it means.**
This error means the number of regions you have painted on the map does not match the number of regions defined in your Assembly Kit database files. This can happen if you add a new region in your database but forget to paint it on the map (or vice versa). Open your Assembly Kit's `regions.xml` and `campaign_map_regions.xml` and count how many regions belong to your campaign map. The count must exactly match the number of distinct region colours you have painted in CAIME. The order of land regions and sea regions must also match — land regions are indexed first, sea regions second.

**The Assembly Kit application (Tweak.AssemblyKit) must be closed before running Map Data or Dynamic Resources.**
These two processing steps use a background tool that accesses the same database files as the Assembly Kit editor. Running both at the same time causes a file conflict and the process will be blocked. Close the Assembly Kit completely, then run the process from CAIME.

**Sea regions that touch too many land regions cause campaign AI slowdowns.**
If the Regions validator warns that a sea region has more than 5 adjacent land regions, it is worth taking seriously. More than 9 adjacent land regions will noticeably slow down the campaign AI. Split large sea regions into two or more smaller sea regions to keep adjacency counts manageable.

**Warhammer / Warhammer 2 / Warhammer 3 town slots have different size rules.**
For these three games (and also Troy and Three Kingdoms), the validator applies relaxed rules — slots do not need to be exactly 19 hexes. If you are modding one of these games and get town slot size warnings, they may safely be ignored if you know your settlement layout is intentional.
