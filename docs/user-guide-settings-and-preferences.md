# Campaign AI Map Editor — Settings & Preferences Guide

---

## Settings & Preferences

### Overview

The Preferences window is CAIME's control panel — the one place where you configure everything about how the application behaves before you start working on your map. Think of it like the "Options" menu in a video game: you visit it once at the start, set everything up the way you like, and then it stays out of your way while you work. The two most important settings here are the **RPFM path**, which CAIME reads your game's database through, and the **Output folder**, where it writes the files the game engine reads.

---

### Table of Contents

1. [Opening Preferences](#1-opening-preferences)
2. [The Preferences Window at a Glance](#2-the-preferences-window-at-a-glance)
3. [Database (RPFM) and Output Folder](#3-database-rpfm-and-output-folder)
4. [Hex Spacing](#4-hex-spacing)
5. [Log to File](#5-log-to-file)
6. [Auto-Save](#6-auto-save)
7. [Auto-Backup](#7-auto-backup)
8. [Map Data Config Auto-Patch](#8-map-data-config-auto-patch)
9. [Assembly Kit Settings (Legacy)](#9-assembly-kit-settings-legacy)
10. [Saving or Discarding Your Changes](#10-saving-or-discarding-your-changes)
11. [Pro-Tips & Troubleshooting](#11-pro-tips--troubleshooting)

---

## 1. Opening Preferences

To open the Preferences window:

- Go to **Settings** in the menu bar, then click **Preferences**.

> **Keyboard shortcut:** `Ctrl + P`

The Preferences window will appear in the centre of your screen.

---

## 2. The Preferences Window at a Glance

The diagram below maps every control in the Preferences window to its purpose.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  Preferences                                                                 │
├──────────────────────────────────────────────────────────────────────────────┤
│  Hex spacing:   0.00  [────────●────────────────────────────]                │◄─ Visual gap between hex cells
├──────────────────────────────────────────────────────────────────────────────┤
│  Log to file:                        ● Enabled   ○ Disabled                  │◄─ Save the log to a file on disk
│  Auto-save:                          ● Enabled   ○ Disabled                  │◄─ Save your project automatically
│  Auto-backup:                        ● Enabled   ○ Disabled                  │◄─ Keep automatic backup copies
│  Auto-backups to keep:       [0                                     ]        │◄─ 0 keeps every backup
│  Map data config auto-patch:         ○ Enabled   ● Disabled                  │◄─ Auto-patch map_data.esf on export
├──────────────────────────────────────────────────────────────────────────────┤
│  RPFM path:                  [C:\...\rpfm                  ] [Browse]        │◄─ RPFM, which reads the database
│  Output folder:              [C:\...\my_mod                ] [Browse]        │◄─ Where processed files go
├──────────────────────────────────────────────────────────────────────────────┤
│  Base game:                  [ Rome 2                               ▼]       │◄─ Legacy: whose AK path is shown
│  Assembly kit path:          [C:\...\assembly_kit          ] [Browse]        │◄─ Legacy: see Section 9
│  Assembly Kit Integration:   ○ Enabled   ● Disabled                          │◄─ Legacy: see Section 9
├──────────────────────────────────────────────────────────────────────────────┤
│                                              [  Save  ] [Cancel]             │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Database (RPFM) and Output Folder

CAIME reads its database tables through **RPFM**, from the game's own packs and your campaign's mod. Your packs and your database are never modified — the workflow is read-only.

The map opens straight away and can be viewed at once (panned and zoomed); the tables are read in the background. Until they have been read, the map can't be edited or saved: the toolbar, the sidebar, **Edit → Resize** and **Rename map**, **File → Save** and **Tools → Import → Layer data** are greyed out. So are the steps that need the database: **Process → Map Data**, **Pathfinding data** and **Lookup and Minimap images**, **Tools → Export → SVG Borders**, and the **Regions**, **Attritions**, **Climates** and **Ground types** validators. The log says when the database has been read. If it can't be read, an error message says why, and the map stays view-only for the session.

Each required table is **merged** from every source that has a piece of it, the same way the game itself combines table fragments — CAIME does not pick one winning source per table:

1. **Your campaign's mod, if you've set one for this project, and every mod it depends on.** RPFM reads the mod's own dependency list and loads those mods too — and theirs in turn — so naming the mod at the top is enough. Any fragments of a required table found in them are merged in. Entirely optional — see below.
2. **The game's own packs.** Fill in whatever the mods don't provide — for example, if they only override `campaigns`/`campaign_map_regions`, ground types and everything else still come from the game, live from the actual install. CAIME reads them through RPFM, from the game folder set in RPFM's own settings, so you never have to work out which `.pack` holds a game's tables.

Every required table must be found in one of these. If RPFM can't find one, CAIME names the missing table, and the map stays view-only for the session.

When two sources both define a row for the same primary key (e.g. both a mod and the game have an entry for the same region), the conflict is resolved the same way the game resolves it: by the name of the table *fragment* each row came from (e.g. `db/campaign_map_regions_tables/data__` vs. `.../!!!my_mod`) — the earlier-sorting fragment name wins, which is why a `!!!`-prefixed fragment name (a common modding convention) beats the plain `data__` fragment the game and most exports use. This has nothing to do with what the `.pack` file is called or the order mods are listed in — only the fragment's own name inside the pack. If a mod and the game happen to use the *identical* fragment name, the mod wins; if your mod and one of the mods it depends on both have a fragment at the identical path, your mod's copy is the one used.

**Requirements:**

- **RPFM installation, version 5.3 or later.** Set the **RPFM path** to the folder containing `rpfm_server.exe`. CAIME won't let you **Save** preferences without it, and validates the folder (by connecting to its RPFM server) before saving. CAIME talks to RPFM through that server: if RPFM is already running it uses the same server in a session of its own, without touching the packs you have open there; otherwise it starts the server in the background, and the server exits by itself once CAIME is done with it.
- **The game's folder set in RPFM.** RPFM finds the game's packs, and your mod, through the game folder in RPFM's own settings — the same one RPFM itself needs to work with the game. If it isn't set, CAIME tells you so once the project is open.
- **Without an RPFM path**, a project opens with no database, view-only. CAIME uses RPFM's schema for every table; the one field it needs that no pack carries, `regions.is_sea`, comes from your map's own land and sea regions.

**Output folder.** Processed files go to `campaign_maps\{your map name}\` inside the **Output folder**, laid out as in a pack, so the folder can be your mod's own. If you leave it empty, CAIME falls back to the Assembly Kit (see Section 9), and Preferences won't **Save** with neither set.

**Processing `map_data.esf`.** CAIME builds `map_data.esf` from your map's first row in `campaign_map_playable_areas` — from your mod or the game — so it must have one. Rome 2 stores the row's `maxy`, which no game's packs carry: CAIME works it out from `maxx` and the map's size in hexes.

**Optional: your campaign's mod, set per project, not in Preferences.** This is the mod containing *this specific campaign's* own data (its region list, campaign definition, etc.). It's entirely optional: a project without one simply reads everything from the game. Set it any time via **Settings > RPFM Workflow** in the menu bar (only enabled while a project is open): type the mod's `.pack` file name, or **Browse...** to pick the `.pack` from disk — either way only the file name is kept. Leave the field empty to go back to the game's tables only. The change takes effect the next time the project is opened, so reload it with **File > Reload** (`Ctrl + R`).

RPFM looks the name up the same way it finds a mod's dependencies: first in the game's `data` folder, then in the Steam Workshop download folder, and uses the first copy it finds. So a local copy you're working on in `data` always wins over the published Workshop version, and a project keeps working wherever the mod is installed — a teammate can open it with the mod subscribed rather than copied to the same place. The name must match the file name exactly, capitalisation included. If your campaign is spread over several packs, name the one that lists the others as dependencies (the pack's dependency list, which you edit in RPFM); the others are then read with it.

The project records the name in its `caime_metadata.json` companion file (next to `map.hex`) — opening a project never prompts for it, since it isn't required. Setting a mod makes opening the project take a few seconds longer, since RPFM loads it the same way it loads a pack's dependencies.

> **Supported games:** Rome 2, Attila, Thrones of Britannia, Warhammer 1/2/3, Three Kingdoms, Troy, Pharaoh and Pharaoh Dynasties.

---

## 4. Hex Spacing

The **Hex spacing** slider controls the visual gap between the hex cells drawn on your map canvas. This is a purely visual setting and has no effect on the output files the game reads.

```
  Minimum (0.00)           Maximum (0.20)
  Hexes touch each other   Hexes have a visible gap between them
  [●────────────────────]  [─────────────────────●]
```

**How to adjust it:**

1. Open **Settings > Preferences**.
2. Drag the **Hex spacing** slider left or right. The current value is shown as a number next to the label.
   - `0.00` — no gap; hex cells are drawn touching edge-to-edge (default).
   - `0.20` — maximum gap; hex cells have a clearly visible space between them.
3. Click **Save**.

> **When to use this:** If you find it hard to see individual hex boundaries while painting, increasing the spacing slightly can make it easier to click on specific cells with precision tools.

---

## 5. Log to File

The **Log to file** setting controls whether CAIME writes its activity log to a file on your computer's hard drive, in addition to displaying it in the Logger panel inside the application.

| Option | What it does |
|--------|-------------|
| **Enabled** (default) | A log file is saved to disk. Useful for reporting bugs or diagnosing export errors after the fact. |
| **Disabled** | Log output is shown in the Logger panel only and is lost when you close CAIME. |

**How to change it:**

1. Open **Settings > Preferences**.
2. Under **Log to file**, click **Enabled** or **Disabled**.
3. Click **Save**.

> The log file is saved to your Windows AppData folder. If you are reporting a problem to the CAIME team, having this enabled means you can attach the log file to your report.

---

## 6. Auto-Save

When **Auto-save** is enabled, CAIME automatically saves your project to disk as you work, without you needing to press `Ctrl + S` manually.

| Option | What it does |
|--------|-------------|
| **Enabled** (default) | Your project is saved automatically in the background. |
| **Disabled** | You must save manually using **File > Save** (`Ctrl + S`). |

**How to change it:**

1. Open **Settings > Preferences**.
2. Under **Auto-save**, click **Enabled** or **Disabled**.
3. Click **Save**.

> **Recommendation:** Keep this enabled. An unexpected crash will not lose your work if auto-save is on.

Like any save, auto-save waits until the project's database has been read (see Section 3).

---

## 7. Auto-Backup

When **Auto-backup** is enabled, CAIME automatically creates backup copies of your project file as you work. This is separate from auto-save — backups give you a rollback point even if you save a mistake.

| Option | What it does |
|--------|-------------|
| **Enabled** (default) | CAIME creates backup copies of your project automatically. |
| **Disabled** | No backup copies are created. |

**How to change it:**

1. Open **Settings > Preferences**.
2. Under **Auto-backup**, click **Enabled** or **Disabled**.
3. Click **Save**.

> **Recommendation:** Keep this enabled, especially when making large changes to a map. If you accidentally paint over a large area and do not notice until later, a backup lets you recover your work.

### How many backups to keep

Each backup is a full copy of your `map.hex`, written into a `backups` folder next to your project. On a large map that is several megabytes every time, so a long session can use a surprising amount of disk.

**Auto-backups to keep** controls how many of the newest backups CAIME holds on to. After each new backup is written, anything older than that count is deleted.

| Value | What it does |
|-------|-------------|
| **0** (default) | Keep every backup. Nothing is ever deleted — this is how CAIME has always behaved, and stays the default. |
| **1 or more** | Keep that many of the newest backups and delete the rest. For example, `10` with the default 10-minute interval keeps roughly the last 100 minutes of history. |

**How to change it:**

1. Open **Settings > Preferences**.
2. Type a number into **Auto-backups to keep**.
3. Click **Save**.

> **Note:** Only the files CAIME wrote as auto-backups (named `..._backup.hex`) are pruned. Anything else you put in the `backups` folder is left alone.

---

## 8. Map Data Config Auto-Patch

This is an advanced setting for users who use the **Map Data Editor** to configure town definitions (such as the number of building slots and whether a settlement has a port).

When enabled, every time you run **Process > Map Data**, CAIME will automatically apply your saved map data configuration to the exported `map_data.esf` file — without you having to do it as a separate step.

> **How it works behind the scenes:** When you save a map data config in the Map Data Editor, CAIME creates a small companion file called `caime_metadata.json` next to your project's `map.hex` file. When Auto-patch is enabled, CAIME reads that companion file after every export and applies the configuration automatically.

| Option | What it does |
|--------|-------------|
| **Enabled** | After a successful Map Data export, CAIME automatically patches `map_data.esf` using your saved config. |
| **Disabled** (default) | No automatic patching. You apply the config manually from the Map Data Editor. |

**How to enable it:**

1. Open **Settings > Preferences**.
2. Under **Map data config auto-patch**, click **Enabled**.
3. Click **Save**.

> **Before enabling this:** Make sure you have already used the Map Data Editor to create and apply a config file at least once. CAIME needs the `caime_metadata.json` companion file to exist — it is created automatically when you save in the Map Data Editor after applying a config.

---

## 9. Assembly Kit Settings (Legacy)

You can leave these alone: CAIME needs no Assembly Kit. They are covered in full in [Annex: The Assembly Kit](user-guide-assembly-kit.md).

| Setting | What it does |
|--------|-------------|
| **Base game** | Picks which game's **Assembly kit path** the field below it shows and edits. It has no other effect; CAIME takes each map's game from the map itself. |
| **Assembly kit path** | The game's `assembly_kit` folder. With no **Output folder** set, processed files go to its `working_data\campaign_maps\{your map name}\`. **Assembly Kit Integration** needs it too. |
| **Assembly Kit Integration** | **Disabled** by default. When **Enabled**, **Process → Map Data** and **Process → Dynamic Resources** run the Assembly Kit's own builder instead of CAIME's writers. |

---

## 10. Saving or Discarding Your Changes

At the bottom of the Preferences window you will find two buttons:

| Button | What it does |
|--------|-------------|
| **Save** | Validates your settings, saves them to disk, and closes the window. Your preferences are remembered the next time you open CAIME. |
| **Cancel** | Discards any changes you made in this session and restores everything to the last saved state. The window closes without saving. |

**Your preferences are stored in:**
```
C:\Users\[YourName]\AppData\Roaming\CampaignMapToolkit\Caime\preferences.json
```
You do not normally need to open or edit this file manually — CAIME manages it for you.

---

## 11. Pro-Tips & Troubleshooting

- **"Please, provide an output folder or a path to the assembly_kit folder!" — Save button does nothing.**
  CAIME needs somewhere to write processed files. Click **Browse** next to **Output folder**, select the folder they should go to, and click **Save** again. (An **Assembly kit path** also works; see [Annex: The Assembly Kit](user-guide-assembly-kit.md).)

- **"Please, provide a path to the RPFM folder!" — Save button does nothing.**
  CAIME reads the database through RPFM, so it needs an **RPFM path**. Click **Browse** next to it, select the folder containing `rpfm_server.exe`, and then click **Save** again.

- **"The provided RPFM installation path is not valid" when saving.**
  The selected folder does not contain a working `rpfm_server.exe`. Point the **RPFM path** at the folder where you installed RPFM 5.3 or later (the one containing `rpfm_server.exe`); older versions of RPFM speak a different protocol, which CAIME does not use. Invalid paths are never stored.

- **The map opened, but I can't paint, and Save is greyed out.**
  The database is still being read in the background. Wait for the log to say it has been read. If it couldn't be read, an error message said why (usually one of the messages below, or no RPFM path): fix that, then reopen the project.

- **"RPFM has no schema for this game" when opening a project.**
  RPFM needs its schema for a game to read that game's tables, and downloads it the first time you use the game in RPFM. Open RPFM, select the game, then open the project in CAIME again.

- **"RPFM failed to open &lt;game&gt;'s packs ... Check that the game's folder is set in RPFM's settings" when opening a project.**
  RPFM could not find the game's packs, usually because its settings have no folder for the game. Open RPFM, set the game's folder in its settings, then open the project in CAIME again.

- **"RPFM found no &lt;table&gt; table in &lt;game&gt;'s packs or the project's mod" when opening a project.**
  RPFM read the game's packs from the game folder set in its settings and one of the tables CAIME needs wasn't in them. Check that the folder points at the game's actual, complete install.

- **"campaign_map_playable_areas has no row for &lt;map&gt;" when processing `map_data.esf`.**
  `map_data.esf` is built from your map's row in `campaign_map_playable_areas`, and neither your mod nor the game has one. Add one for the map to your mod. Its `index` and `maxx` go into the map data (and, for Rome 2, its `maxy`); the game also reads the row itself, so fill in its other columns as the game expects.

- **"RPFM found no mod named &lt;name&gt;" in the log.**
  RPFM looked for that `.pack` in the game's `data` folder and its Steam Workshop folder and didn't find it, so the project opened without it. Check the name in **Settings > RPFM Workflow** — it must match the file name exactly, capitalisation included — and that the mod is installed or subscribed.

- **I want this project to use a mod, but nothing ever asks me for one.**
  That's expected — the mod is optional and never prompted for automatically. Set it yourself via **Settings > RPFM Workflow** in the menu bar (only enabled while a project is open).

- **I accidentally clicked Save with wrong settings — how do I undo?**
  Close Preferences without saving by pressing `Escape` or clicking **Cancel** *before* you click **Save**. Once you click **Save**, the changes are written to disk immediately. If you already saved, simply re-open Preferences (`Ctrl + P`), correct the setting, and click **Save** again.
