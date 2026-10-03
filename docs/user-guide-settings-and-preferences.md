# Campaign AI Map Editor — Settings & Preferences Guide

---

## Settings & Preferences

### Overview

The Preferences window is CAIME's control panel — the one place where you configure everything about how the application behaves before you start working on your map. Think of it like the "Options" menu in a video game: you visit it once at the start, set everything up the way you like, and then it stays out of your way while you work. The most important setting here is the **Assembly Kit Path**, which tells CAIME where your game's modding tools are installed so it can produce files the game engine can read.

> **Prefer the command line?** Every setting in this window can also be read and changed with `CAIME.exe config` — see [Changing Settings](user-guide-command-line-interface.md#changing-settings).

---

### Table of Contents

1. [Opening Preferences](#1-opening-preferences)
2. [The Preferences Window at a Glance](#2-the-preferences-window-at-a-glance)
3. [Setting Your Base Game](#3-setting-your-base-game)
4. [Setting the Assembly Kit Path](#4-setting-the-assembly-kit-path)
5. [Hex Spacing](#5-hex-spacing)
6. [Log to File](#6-log-to-file)
7. [Auto-Save](#7-auto-save)
8. [Auto-Backup](#8-auto-backup)
9. [Map Data Config Auto-Patch](#9-map-data-config-auto-patch)
10. [Database Source (Assembly Kit or RPFM)](#10-database-source-assembly-kit-or-rpfm)
11. [Saving or Discarding Your Changes](#11-saving-or-discarding-your-changes)
12. [Pro-Tips & Troubleshooting](#12-pro-tips--troubleshooting)

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
│  Base game:                  [ Rome 2                               ▼]       │◄─ The game you are modding
│  Assembly kit path:          [C:\...\assembly_kit          ] [Browse]        │◄─ Path to the game's mod tools
├──────────────────────────────────────────────────────────────────────────────┤
│  Log to file:                        ● Enabled   ○ Disabled                  │◄─ Save the log to a file on disk
│  Auto-save:                          ● Enabled   ○ Disabled                  │◄─ Save your project automatically
│  Auto-backup:                        ● Enabled   ○ Disabled                  │◄─ Keep automatic backup copies
│  Auto-backups to keep:       [0                                     ]        │◄─ 0 keeps every backup
│  Map data config auto-patch:         ○ Enabled   ● Disabled                  │◄─ Auto-patch map_data.esf on export
├──────────────────────────────────────────────────────────────────────────────┤
│  Database source:            [ Assembly Kit                         ▼]       │◄─ Where tables are loaded from
│  RPFM path:                  [C:\...\rpfm                  ] [Browse]        │◄─ Only shown when RPFM is selected
│  Vanilla pack:                [C:\...\data\db.pack          ] [Browse]        │◄─ Per-game; required when source is RPFM
├──────────────────────────────────────────────────────────────────────────────┤
│                                              [  Save  ] [Cancel]             │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Setting Your Base Game

The **Base game** dropdown tells CAIME which Total War game you are creating a mod for. This is a critical first step because the assembly kit path (see the next section) is saved separately for each game — switching games here automatically loads the path you previously set for that game.

**How to set it:**

1. Open **Settings > Preferences**.
2. Click the **Base game** dropdown.
3. Select your game from the list:
   - Rome 2
   - Attila
   - Thrones of Britannia
   - Warhammer
   - Warhammer 2
   - Warhammer 3
   - Three Kingdoms
   - Troy
   - Pharaoh
   - Pharaoh Dynasties
4. The **Assembly kit path** field below will update automatically to show the path you previously saved for that game (or be empty if you have not set one yet).

> **Important:** You must select the correct base game before clicking **Save**. If you process map data with the wrong game selected, the output files will not be compatible with your target game.

---

## 4. Setting the Assembly Kit Path

The Assembly Kit is a folder that Creative Assembly provides with its modding tools. CAIME needs to know where this folder is on your computer so it can read game-specific data files and generate the correct output.

**How to set it:**

1. Open **Settings > Preferences**.
2. Make sure the correct **Base game** is selected first (see Section 3).
3. Next to **Assembly kit path**, click **Browse**.
4. A folder picker window will appear. Navigate to and select your `assembly_kit` folder for that game. It is usually found inside the game's Steam directory, for example:
   ```
   C:\Program Files (x86)\Steam\steamapps\common\Total War WARHAMMER III\assembly_kit
   ```
5. The path will appear in the text box.
6. Click **Save**.

> **Tip:** CAIME remembers a separate assembly kit path for each game. If you mod multiple Total War games, you only need to set this once per game — after that, switching the **Base game** dropdown will automatically load the correct path.

---

## 5. Hex Spacing

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

## 6. Log to File

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

## 7. Auto-Save

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

---

## 8. Auto-Backup

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

## 9. Map Data Config Auto-Patch

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

## 10. Database Source (Assembly Kit or RPFM)

By default CAIME loads its database tables directly from your **Assembly Kit**. This is the standard workflow and requires no extra setup beyond the Assembly Kit path.

If your tables live in an **RPFM** `.pack` file instead, you can switch the **Database source** to **RPFM**. CAIME then temporarily extracts the required tables into your Assembly Kit, uses them to open the project, and restores your Assembly Kit to exactly its original state once the project is closed. Your pack, your database, and your Assembly Kit files are never modified — the workflow is read-only.

Each required table is **merged** from every source that has a piece of it, the same way the game itself combines table fragments — CAIME does not pick one winning source per table:

1. **Your modded `.pack` files, if you've set any for this project.** Every one of them is checked for every required table, and any fragments found are merged in. Entirely optional — see below.
2. **Your configured vanilla pack.** Fills in whatever your modded packs don't provide — for example, if your packs only override `campaigns`/`campaign_map_regions`, ground types and everything else still come from vanilla, live from the actual game install. This is a per-game setting you point at yourself (see below) — CAIME does not guess which file this is, since it isn't a stable filename: it's `data.pack` for some games, split across several `data1.pack`/`data2.pack`/... files for others, and has even changed within a single game's lifetime (Warhammer 3 moved its DB tables from `data.pack` into `db.pack` after release).
3. **The Assembly Kit's own bundled copy.** Used for any table found in none of the packs above (unchanged from the non-RPFM workflow).

When two sources both define a row for the same primary key (e.g. both a mod and vanilla have an entry for the same region), the conflict is resolved the same way the game resolves it: by the name of the table *fragment* each row came from (e.g. `db/campaign_map_regions_tables/data__` vs. `.../!!!my_mod`) — the earlier-sorting fragment name wins, which is why a `!!!`-prefixed fragment name (a common modding convention) beats the plain `data__` fragment vanilla and most exports use. This has nothing to do with which `.pack` file the fragment came from or the order it's listed in below — only the fragment's own name inside the pack. If a mod and the vanilla pack happen to use the *identical* fragment name, the mod wins regardless of what either file is called.

| Option | What it does |
|--------|-------------|
| **Assembly Kit** (default) | Loads tables straight from the Assembly Kit, exactly as before. |
| **RPFM** | Prepares tables from your configured vanilla pack (and your modded `.pack` files, if this project has any), then loads them through the normal Assembly Kit path. |

**Requirements when using RPFM:**

- **RPFM installation.** Set the **RPFM path** to the folder containing `rpfm_cli.exe`. Required the moment you switch the Database source to RPFM — CAIME won't let you **Save** preferences without it, and validates the folder (by running `rpfm_cli.exe help`) before saving.
- **A vanilla pack, per game.** Set via the **Vanilla pack** field, which appears next to **RPFM path** once the Database source is set to RPFM — pick the **Base game** you want to configure first, since this is stored per game like the Assembly Kit path. Point it at the `.pack` file that actually contains that game's DB tables (open the game's `data` folder in RPFM if you're not sure which one it is - `pack list` on each candidate is the reliable way to check). **This is required, not optional**, for whichever game is selected when you hit **Save** — CAIME blocks saving preferences without it, the same way it blocks opening a project without it: it's the actual data source RPFM exists to read from, so an RPFM session can't do anything meaningful without it.
- **The Assembly Kit path is still required** — the RPFM workflow reuses the Assembly Kit's table schemas, and is the final fallback for any table not found in either pack.

**Optional: modded `.pack` files, set per project, not in Preferences.** These are the pack(s) containing *this specific campaign's* own data (its region list, campaign definition, etc.) — different from the vanilla pack above, which is shared across every project for that game. It's entirely optional: a project with none set simply reads everything from the vanilla pack. Manage the list any time via **Settings > RPFM Workflow** in the menu bar (only enabled while a project is open) — **Add** or **Remove** `.pack` files as needed; every pack listed contributes to every table equally, so there is nothing to reorder. The list is always shown sorted alphabetically by pack file name, and that order only matters in the rare case where two packs in this list each define a table fragment with the exact same name — the one whose pack file name sorts first then wins that tiebreak. Each project records its own list in its `caime_metadata.json` companion file (next to `map.hex`) — opening a project never prompts for this automatically, since it isn't required.

> **Takes effect on next open:** The Database source can be changed at any time, including while a project is open. Changing it has no effect on a project that's already open — only the next project you open reads the new setting.

> **Supported games:** Rome 2, Attila, Thrones of Britannia, Warhammer 1/2/3, Three Kingdoms, Troy, Pharaoh and Pharaoh Dynasties.

---

## 11. Saving or Discarding Your Changes

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

## 12. Pro-Tips & Troubleshooting

- **"Please, provide a path to the assembly_kit folder!" — Save button does nothing.**
  This warning appears if you click **Save** while the Assembly kit path field is empty. CAIME requires a valid path to function. Click **Browse**, select your game's `assembly_kit` folder, and then click **Save** again.

- **"The database source is set to RPFM, but no RPFM installation path is provided" — Save button does nothing.**
  You switched **Database source** to **RPFM** without setting a **RPFM path**. Either provide one or switch the source back to **Assembly Kit**.

- **"The provided RPFM installation path is not valid" when saving.**
  The selected folder does not contain a working `rpfm_cli.exe`. Point the **RPFM path** at the folder where you extracted RPFM (the one containing `rpfm_cli.exe`). Invalid paths are never stored.

- **"The database source is set to RPFM, but no vanilla pack is set for &lt;game&gt;" — Save button does nothing.**
  With **Database source** set to **RPFM**, a vanilla pack is required for whichever game is currently selected in the **Base game** dropdown — CAIME won't let you save preferences without it (and won't let a project for that game open, for the same reason: it's the actual data RPFM reads from, unlike the modded pack, which is optional). Select that game in **Base game**, **Browse** to the `.pack` file containing its DB tables under **Vanilla pack**, and **Save** again. If you don't use RPFM for that particular game, switch **Database source** back to **Assembly Kit** instead.

- **I want this project to use a modded pack, but nothing ever asks me for one.**
  That's expected — modded packs are optional and never prompted for automatically. Add one or more yourself via **Settings > RPFM Workflow** in the menu bar (only enabled while a project is open).

- **I switched games and the Assembly kit path changed unexpectedly.**
  This is by design. CAIME stores a separate assembly kit path for each game. When you change the **Base game** dropdown, the path field updates to show whichever path you previously saved for that game. Simply set the correct path and click **Save** again.

- **I accidentally clicked Save with wrong settings — how do I undo?**
  Close Preferences without saving by pressing `Escape` or clicking **Cancel** *before* you click **Save**. Once you click **Save**, the changes are written to disk immediately. If you already saved, simply re-open Preferences (`Ctrl + P`), correct the setting, and click **Save** again.

- **I cannot find my assembly_kit folder.**
  Open Steam, right-click your Total War game in your Library, select **Properties > Local Files > Browse**, and look for the `assembly_kit` subfolder inside the game's installation directory. Not all games ship with an assembly kit — check the game's modding wiki for instructions on how to download it.
