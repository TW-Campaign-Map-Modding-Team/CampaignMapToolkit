# Getting Started with Campaign AI Map Editor (CAIME)

---

## Overview

Welcome to **Campaign AI Map Editor (CAIME)** — a painting tool for creating campaign map data files used by Total War games. Think of it like Adobe Photoshop, but instead of painting colours for art, you are painting *game data* directly onto a hexagonal map — telling the engine where rivers flow, what terrain the army trudges through, and much more.

Each type of map data lives on its own **Layer** (Ground Types, Climates, Roads, Rivers, and so on), exactly like layers in Photoshop. You pick a layer, pick a colour called a **Swatch**, brush it onto the map, and when you are done, CAIME converts your painted canvas into the binary files the game engine reads. No coding required.

Supported games: **Rome 2, Attila, Thrones of Britannia, Warhammer, Warhammer 2, Warhammer 3, Three Kingdoms, Troy, Pharaoh,** and **Pharaoh Dynasties**.

---

## Table of Contents

1. [First Launch — Accepting the EULA](#1-first-launch--accepting-the-eula)
2. [The Application Window at a Glance](#2-the-application-window-at-a-glance)
3. [Step 1 — Configure Your Settings](#3-step-1--configure-your-settings)
4. [Step 2 — Create a New Map or Open an Existing One](#4-step-2--create-a-new-map-or-open-an-existing-one)
   - [Creating a New Map](#creating-a-new-map)
   - [Opening an Existing Map](#opening-an-existing-map)
5. [Step 3 — Paint Your Map](#5-step-3--paint-your-map)
6. [Step 4 — Save Your Work](#6-step-4--save-your-work)
7. [Auto-Save and Auto-Backup](#7-auto-save-and-auto-backup)
   - [What is Auto-Save?](#what-is-auto-save)
   - [What is Auto-Backup?](#what-is-auto-backup)
   - [Recovering from Auto-Saves and Backups](#recovering-from-auto-saves-and-backups)
8. [Step 5 — Export for the Game](#8-step-5--export-for-the-game)
9. [Your Complete Workflow at a Glance](#9-your-complete-workflow-at-a-glance)
10. [Where to Go Next](#10-where-to-go-next)
11. [Pro-Tips & Troubleshooting](#11-pro-tips--troubleshooting)

---

## 1. First Launch — Accepting the EULA

The very first time you open CAIME, a **License Agreement** window appears before the main editor loads.

```
┌──────────────────────────────────────────┐
│          End User License Agreement      │
│                                          │
│  Please read the license agreement       │
│  before using Campaign AI Map Editor.    │
│                                          │
│  [ .... license text .... ]              │
│                                          │
│              [ I Accept ]  [ ✕ Close ]  │
└──────────────────────────────────────────┘
```

- **Click I Accept** to agree and continue to the main window.  
- If you close the window without accepting, the application will shut down.  

> You will only see this window on the very first launch. After you accept, it will not appear again.

If a newer version of CAIME has been installed since your last run, a **Changelog** window will also appear briefly to show you what is new. You can read it or close it — it does not block your work.

---

## 2. The Application Window at a Glance

After accepting the EULA, the main editor window opens. Here is what you will see:

```
┌─────────────────────────────────────────────────────────────────────┐
│  [icon]  Campaign AI Map Editor        ▁  ❐  ✕                     │
│─────────────────────────────────────────────────────────────────────│
│  File   Edit   Process   Tools   Settings   Help                    │  ← Menu Bar
│─────────────────────────────────────────────────────────────────────│
│         │  Brush size: [━━●━━━]   Opacity: [━━━━━●]   [Clear]      │  ← Quick Settings Bar
│  [🖌]   │─────────────────────────────────────────────────────────  │
│  [⬛]   │                                         │  [Minimap]      │
│  [╱ ]   │                                         │─────────────────│
│  [💧]   │         MAP CANVAS                      │  [Swatches]     │
│  [✚]   │         (Hexagonal Grid)                │─────────────────│
│  [✚]   │                                         │  [Actions]      │
│         │                                         │─────────────────│
│  [⚙]   │                                         │  [Layers]       │
│─────────────────────────────────────────────────────────────────────│
│  Logger: Ready.                                                      │  ← Status Bar
└─────────────────────────────────────────────────────────────────────┘
   ↑                ↑                                 ↑
 Toolbar        Map Canvas                         Sidebar
(left strip)   (centre)                          (right panel)
```

| Area | What it does |
|---|---|
| **Menu Bar** | All major operations — File, Edit, Process, Tools, Settings, Help |
| **Quick Settings Bar** | Adjust brush size, image overlay opacity, and flood-fill source layer |
| **Toolbar** (left strip) | Switch between painting tools (Brush, Flood Fill, Line, Color Picker) |
| **Map Canvas** (centre) | The hexagonal map you paint on |
| **Sidebar** (right panel) | Minimap, Swatches (colours), Actions, Layer Opacity, and Layers |
| **Status Bar** (bottom) | Shows the result message of the last action performed |

> **When you first open CAIME with no project loaded**, most menu items and toolbar buttons are greyed out. This is normal — they unlock as soon as a map is open.

---

## 3. Step 1 — Configure Your Settings

Before creating or opening a map, you need to tell CAIME where your game's **Assembly Kit** is installed. The Assembly Kit is a set of official modding tools provided by Creative Assembly alongside each game, and CAIME reads game data from it (region names, terrain types, road types, etc.) to populate your Swatches.

1. Go to **Settings** in the menu bar and click **Preferences** (or press **Ctrl+P**).  
   The **Preferences** window opens.

```
┌────────────────────────────────────────────────┐
│  Preferences                                   │
│                                                │
│  Hex spacing:        [━━━●━━━━━━━] 0.05        │
│  ─────────────────────────────────────────     │
│  Base game:          [ Warhammer3       ▼]     │
│  Assembly kit path:  [C:\WH3_AssKit\  ] [Browse]│
│  ─────────────────────────────────────────     │
│  Log to file:        ● Enabled  ○ Disabled     │
│  Auto-save:          ● Enabled  ○ Disabled     │
│  Auto-backup:        ● Enabled  ○ Disabled     │
│  Map data config                               │
│  auto-patch:         ● Enabled  ○ Disabled     │
│  ─────────────────────────────────────────     │
│                          [ Save ] [ Cancel ]   │
└────────────────────────────────────────────────┘
```

2. In the **Base game** dropdown, select the Total War game you are modding.  
3. Next to **Assembly kit path**, click **Browse** and navigate to the folder where your game's Assembly Kit is installed.  
   - This is typically found in your Steam library under `steamapps\common\[Game Name] Assembly Kit\`.  
4. Click **Save** to apply your settings.

> If you work on maps for multiple games, open **Preferences** again any time you switch games and update the **Base game** and **Assembly kit path** accordingly.

---

## 4. Step 2 — Create a New Map or Open an Existing One

### Creating a New Map

1. Go to **File** in the menu bar and click **Create new map** (or press **Ctrl+N**).  
   The **Create new project** dialog opens.

```
┌──────────────────────────────────────────┐
│  Create new project                      │
│                                          │
│  Campaign map name:  [my_campaign_map  ] │
│  ─────────────────────────────────────   │
│  Template:           [ None          ▼]  │
│  Game:               [ Warhammer3    ▼]  │
│  Map size:           [ 1016 ] x [ 720 ]  │
│  ─────────────────────────────────────   │
│                    [ Create ] [ Cancel ] │
└──────────────────────────────────────────┘
```

2. In the **Campaign map name** field, type a unique name for your map (for example `my_rome_map`). Use only letters, numbers, and underscores — no spaces.  
3. In the **Template** dropdown, choose:
   - **None** — Start from a completely blank canvas (you will also need to choose a **Game** and **Map size** below).
   - **A template name** — Pre-fills the canvas with starter data. When a template is selected, the **Game** and **Map size** fields are locked, because they are defined by the template.  
4. If you chose **None** as the template:
   - Select your target game from the **Game** dropdown.
   - Enter your desired map dimensions in the **Map size** fields (width × height in hexes). The default of `1016 x 720` is a good starting size for most maps. Note that the **width must be an even number**.
5. Click **Create**.

CAIME will create your project files and open the map, ready to paint.

> **Important for new maps:** Before clicking Create, make sure you have already registered your map in the game's database using the Assembly Kit's **Dave** database editor. CAIME reads region names, road types, and other data from the database the moment the map opens — if the database has not been set up yet, your Swatches will be empty. See the **Creating a New Campaign Map** guide for full database setup instructions.

---

### Opening an Existing Map

1. Go to **File** in the menu bar and click **Open** (or press **Ctrl+O**).  
   A Windows file browser window opens.
2. Navigate to your map's project folder and select the **map.hex** file inside it.
3. Click **Open**.

CAIME loads the map and all its layer data. The Swatches and Layers panels on the right side will populate automatically.

---

## 5. Step 3 — Paint Your Map

Once a map is open, the editor is fully unlocked. The workflow is:

**Select a Layer → Select a Swatch → Paint on the Canvas**

```
  SIDEBAR (right)            MAP CANVAS (centre)
  ┌─────────────────┐        
  │  Layers         │        
  │  ☑ ● Ground Typ │   →    [Paint hex cells with the active Swatch]
  │  ☑   Climates   │        
  │  ☑   Regions    │        
  │  ─────────────  │        
  │  Swatches       │        
  │  ┌───┐  [Grass ▼]        
  │  │   │               ← currently active swatch colour
  │  └───┘               
  └─────────────────┘        
```

1. In the **Layers** section of the sidebar (bottom-right), click the name of the layer you want to paint — for example **Ground Types**. A radio button next to it will indicate it is active.
2. In the **Swatches** section of the sidebar (middle-right), use the dropdown to pick the value you want to paint with (for example "Grass Plains" or "Forest").  
   The square preview above the dropdown updates to show the selected swatch colour.
3. Select a painting tool from the **Toolbar** on the left:

   | Button | Tool | Best used for |
   |---|---|---|
   | Brush icon | **Brush** | Painting freely over individual hexes |
   | Fill icon | **Flood Fill** | Instantly filling a large connected area with one swatch |
   | Line icon | **Line** | Drawing straight lines across the map |
   | Eyedropper icon | **Color Picker** | Sampling the swatch already painted on a hex (also activated by holding **Alt**) |

4. **Click and drag** on the map canvas to paint. Hexes under your cursor will be filled with the selected swatch.
5. Use the **Brush size** slider in the Quick Settings Bar (top centre) to make your brush larger or smaller (range: 1–10).

> **Undo and Redo** are always available under **Edit > Undo** (**Ctrl+Z**) and **Edit > Redo** (**Ctrl+Y**).

---

## 6. Step 4 — Save Your Work

- Press **Ctrl+S** (or go to **File > Save**) at any time to save your progress into the project's `.hex` file.
- Use **File > Save as...** (**Ctrl+Shift+S**) to save a copy of the current map under a new name.

> **Auto-save** is enabled by default (configurable in **Settings > Preferences**). Even so, it is good practice to save manually before exporting or closing.

---

## 7. Auto-Save and Auto-Backup

### What is Auto-Save?

**Auto-save** automatically saves your map work at regular intervals (every 5 minutes by default) without requiring you to manually press **Ctrl+S**. This helps protect against accidental data loss due to crashes, unexpected shutdowns, or user error.

- **How it works:** CAIME silently saves to a special autosave file named `{your_map_name}_autosave.hex` in your project folder every 5 minutes while a map is open.
- **Where files are stored:** In the same directory as your main `map.hex` file.
- **Enable/Disable:** Use **Settings > Preferences** and toggle the **Auto-save** checkbox.

> Auto-save files are *separate* from your main map file. They do not overwrite your manually saved work — they exist in parallel as a safety net.

### What is Auto-Backup?

**Auto-backup** creates timestamped snapshots of your entire project folder at regular intervals (every 10 minutes by default). Each backup captures the complete state of your map at that moment, allowing you to revert to a previous version if needed.

- **How it works:** CAIME automatically creates a full copy of your project files every 10 minutes while a map is open. Each backup is named with a timestamp: `{your_map_name}_YYYY_MM_DD_HH_mm_backup.hex`
- **Where files are stored:** In a `backups\` subfolder within your project directory.
- **Enable/Disable:** Use **Settings > Preferences** and toggle the **Auto-backup** checkbox.
- **Multiple backups:** Previous backups are kept, so you can potentially revert to multiple previous versions depending on how long ago you want to go back.

### Recovering from Auto-Saves and Backups

#### Scenario 1: CAIME Crashed or Closed Unexpectedly

1. Open CAIME again and go to **File > Open**.
2. Navigate to your project folder and look for:
   - A file named `{map_name}_autosave.hex` — this is your auto-save from 5 minutes ago.
3. Select the auto-save file and click **Open**.
4. Once the file opens, optionally save it as your main map using **File > Save as...** to make it your working file, or copy the changes you want to keep back into your original `map.hex`.

#### Scenario 2: You Made Changes You Want to Undo (Multiple Hours or Days Ago)

1. Open your project folder and navigate to the `backups\` subfolder inside it.
2. You will see multiple backup files with timestamps in their names (e.g., `my_map_YYYY_MM_DD_HH_mm_backup.hex`).
3. Choose the backup closest to the point in time you want to recover to, based on the timestamp in the filename.
4. In CAIME, go to **File > Open** and select the backup file.
5. Review the map to confirm it is the version you wanted.
6. Use **File > Save as...** to save it as a new copy, or **File > Save** to overwrite your current map after confirming this is the right version.

#### Scenario 3: Accidental Edits in the Current Session

If you accidentally painted over something just a few minutes ago within the same session, use **Ctrl+Z** to undo instead of loading a backup. CAIME has unlimited undo depth — you can undo all the way back to when you opened the file.

---

## 8. Step 5 — Export for the Game

When you are satisfied with your painted map, you need to convert it into the binary files the game engine actually reads. These are generated from the **Process** menu.

Go to **Process** in the menu bar. The available options are:

| Process option | What it generates |
|---|---|
| **Borders data** | Region border pathfinding files |
| **Pathfinding data** | AI movement and passability data |
| **Map Data** | The main `map_data.esf` game file |
| **Dynamic resources** | Resource overlay data |
| **Trade Routes** | Trade route data (where applicable) |
| **Lookup and Minimap images** | The lookup images used in-game to identify region boundaries on radar maps |

Run each export step relevant to your changes. A result message will appear in the **Logger** strip at the bottom of the window.

> You do not need to run all exports every time — only the ones covering the layers you have changed.

---

## 9. Your Complete Workflow at a Glance

```
  [First time only]
        │
        ▼
  Settings > Preferences
  → Set Base game
  → Set Assembly kit path
  → Save
        │
        ▼
  File > Create new map   ──OR──   File > Open
        │
        ▼
  Select a Layer (Layers panel, right)
        │
        ▼
  Select a Swatch (Swatches panel, right)
        │
        ▼
  Choose a Tool (Toolbar, left)
        │
        ▼
  Paint on the Map Canvas
        │
        ▼
  Ctrl+S  →  Save
        │
        ▼
  Process > [Export step]
        │
        ▼
  Binary files ready for the game mod!
```

---

## 10. Where to Go Next

Now that you are set up and familiar with the basics, explore the full feature guides for each part of the workflow:

| Guide | What it covers |
|---|---|
| [Main Interface Guide](user-guide-main-interface.md) | Detailed breakdown of every panel, button, and menu item |
| [Creating a New Campaign Map](user-guide-creating-a-new-map.md) | Database setup in Dave, templates, and game-specific notes |
| [Painting with Swatches](user-guide-painting-with-swatches.md) | Swatches in depth, creating custom swatches, managing colours |
| [Toolbar & Painting Tools](user-guide-toolbar-painting-tools.md) | Each painting tool explained in detail |
| [Settings & Preferences](user-guide-settings-and-preferences.md) | All Preferences options and what they do |
| [Tools Menu](user-guide-tools-menu.md) | Logger, Border Editor, Map Data Editor, Shader Resolution Corrector, Import/Export/Validate |
| [Modifying an Existing Map](user-guide-modifying-an-existing-map.md) | Loading, editing, and re-exporting an existing map |
| [Processing & Exporting](user-guide-processing-and-exporting.md) | All Process menu export steps explained in detail |

---

## 11. Pro-Tips & Troubleshooting

- **Swatches are empty after creating a map.**  
  This means CAIME could not find your game's database. Go to **Settings > Preferences**, confirm the **Assembly kit path** points to the correct folder for your game, and then **File > Reload** the map. Also make sure you have set up your map's entries in Dave before creating the CAIME project (see the [Creating a New Campaign Map](user-guide-creating-a-new-map.md) guide).

- **Map size width was rejected.**  
  The map width must always be an **even number** (e.g. 1016, 512, 2048). If you enter an odd number, CAIME will show an error and will not create the map. Simply add or subtract 1 to make it even.

- **Performance slows down on very large maps.**  
  If your map dimensions result in more than ~731,000 hexes (e.g. wider than ~1016 at full height), CAIME will warn you before creating the project. This is not a hard block, but painting and exporting will be noticeably slower. Start with smaller dimensions and resize later via **Edit > Resize** (**Ctrl+Shift+R**) if you need a bigger canvas.

- **Most menu items are greyed out.**  
  This is expected when no project is loaded. Open or create a map first, and the full menu will unlock.

- **Accidentally painted the wrong layer?**  
  Press **Ctrl+Z** to undo. You can undo as many steps as you need without limit, right back to when you opened the file.

- **CAIME crashed or was closed unexpectedly.**  
  If **Auto-save** was enabled in **Preferences**, CAIME may have saved a backup automatically. Check **File > Open** and look inside your project folder for backup files.
