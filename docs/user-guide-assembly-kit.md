# Annex: The Assembly Kit (Legacy & Advanced)

---

## Overview

> **You don't need the Assembly Kit to use CAIME.** CAIME reads the database through RPFM (see [Settings & Preferences](user-guide-settings-and-preferences.md#3-database-rpfm-and-output-folder)) and writes every game file itself. This annex covers the few places where CAIME can still use an Assembly Kit, for those who want them.

The **Assembly Kit** is Creative Assembly's official modding toolset, shipped with most Total War games on Steam. Older versions of CAIME read their database from it and wrote their output into it. CAIME now uses it in only three places, all of them optional:

| Use | What it needs | Section |
|---|---|---|
| A fallback place for processed files, when no **Output folder** is set | The **Assembly kit path** | [Output fallback](#3-output-fallback-working_data) |
| **Assembly Kit Integration**: build `map_data.esf` and `dynamic_resources.esf` with the Assembly Kit's own builder | The **Assembly kit path** | [Assembly Kit Integration](#4-assembly-kit-integration) |
| A source of raw map files (`map.hex`, `trees.png`, ...) for templates | Nothing in CAIME; you copy the files | [Creating and Sharing Map Templates](user-guide-creating-a-template.md) |

CAIME never reads the Assembly Kit's database. The only thing it ever writes into the Assembly Kit is processed files, and only under the output fallback.

---

### Table of Contents

1. [Base Game](#1-base-game)
2. [Setting the Assembly Kit Path](#2-setting-the-assembly-kit-path)
3. [Output Fallback: working_data](#3-output-fallback-working_data)
4. [Assembly Kit Integration](#4-assembly-kit-integration)
5. [Dave and the Assembly Kit's Database](#5-dave-and-the-assembly-kits-database)
6. [Troubleshooting](#6-troubleshooting)

---

## 1. Base Game

The **Base game** dropdown in **Settings > Preferences** picks which game's **Assembly kit path** the field below it shows and edits. CAIME remembers a separate path for each game. It has no other effect: CAIME takes each map's game from the map itself.

Opening a project sets **Base game** to that project's game, so the field shows the path that project would use.

---

## 2. Setting the Assembly Kit Path

1. Open **Settings > Preferences** (`Ctrl + P`).
2. In **Base game**, select the game whose Assembly Kit you want to set.
3. Next to **Assembly kit path**, click **Browse**.
4. Select the game's `assembly_kit` folder, the one with the `raw_data` and `working_data` sub-folders. It is usually inside the game's Steam folder, for example:
   ```
   C:\Program Files (x86)\Steam\steamapps\common\Total War WARHAMMER III\assembly_kit
   ```
5. Click **Save**.

Repeat for every game whose Assembly Kit you want CAIME to use.

---

## 3. Output Fallback: working_data

Processed files normally go to `campaign_maps\{your map name}\` inside the **Output folder**. With no **Output folder** set, they go to the Assembly Kit instead:

```
[Assembly kit path]\working_data\campaign_maps\{your map name}\
```

Preferences won't **Save** with neither an **Output folder** nor an **Assembly kit path** for the selected game, because CAIME would have nowhere to write.

---

## 4. Assembly Kit Integration

An optional add-on, **Disabled** by default. Normally CAIME writes `map_data.esf` and `dynamic_resources.esf` itself. When **Assembly Kit Integration** is **Enabled** in **Settings > Preferences**, **Process → Map Data** and **Process → Dynamic Resources** run the Assembly Kit's own builder instead, through the `MapDataBuilder` tool that ships with CAIME. Use it to compare CAIME's output with Creative Assembly's, or as a fallback if CAIME's writer rejects a map.

- **It needs the Assembly kit path** for the project's game (see [Section 2](#2-setting-the-assembly-kit-path)). Only the Assembly Kit's `binaries` are used.
- **It needs the database read through RPFM**, as normal processing does. CAIME writes the tables the builder reads to a temporary folder, made from your map's first row in `campaign_map_playable_areas`. It copies your saved `map.hex` there too, and, for Dynamic Resources, `dynamic_resources.png` and `dynamic_resources_database.xml` from the project folder. Nothing in the Assembly Kit is changed, and your project can be saved anywhere.
- The result goes to the same place as CAIME's own output.

The builder's own messages, and its log files, appear in the Logger.

---

## 5. Dave and the Assembly Kit's Database

**Dave** is the Assembly Kit's database editor. CAIME doesn't read the Assembly Kit's database, so rows you add or change in Dave don't reach CAIME on their own. CAIME sees them only once they are in your mod's pack, which it reads through RPFM.

The other guides describe database changes in RPFM, made directly in your mod's pack. If you prefer Dave, make the change there, get it into your mod's pack, and then reload the map in CAIME (**File → Reload**, `Ctrl + R`).

---

## 6. Troubleshooting

- **"Please, provide an output folder or a path to the assembly_kit folder!" — Save button does nothing.**
  CAIME needs somewhere to write processed files. Set an **Output folder** (recommended), or an **Assembly kit path** for the selected **Base game**, then click **Save** again.

- **"No output folder, and no assembly kit path for *&lt;game&gt;*, is configured." when processing.**
  The project's game has no Assembly kit path, and no **Output folder** is set. Set an **Output folder** in **Settings > Preferences**.

- **I switched games and the Assembly kit path changed.**
  This is by design: CAIME stores a separate path for each game, and the field shows the path for whichever **Base game** is selected.

- **I cannot find my assembly_kit folder.**
  In Steam, right-click your Total War game in your Library, select **Properties > Installed Files > Browse**, and look for the `assembly_kit` folder. Not every game ships with an Assembly Kit; check the game's modding wiki for how to get it.
