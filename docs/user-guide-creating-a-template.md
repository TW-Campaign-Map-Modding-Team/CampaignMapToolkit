# Creating and Sharing Map Templates

---

## Overview

A **template** in CAIME is a set of raw campaign map files taken directly from the game's Assembly Kit — the very same files Creative Assembly's developers used internally to produce the finished, game-ready campaign maps you see in Total War titles. Think of them as the original blueprints: unopinionated, unmodified source material that you can open, paint over, and export without having to reconstruct anything from scratch. Templates can also come from the modding community, where experienced modders package their own pre-built map bases for others to build on.

When someone picks a template from the **New Project** dialog, CAIME copies all of its files into the new project folder and opens it immediately — the game type, map dimensions, regions, terrain, and supporting processing files are all already in place.

---

## Table of Contents

1. [Understanding the Templates Folder](#1-understanding-the-templates-folder)
2. [What Goes Inside a Template Folder](#2-what-goes-inside-a-template-folder)
   - [Required: map.hex](#required-maphex)
   - [Recommended: trees.png](#recommended-treespng)
   - [Recommended: tree_database.xml](#recommended-tree_databasexml)
   - [Recommended: dynamic_resources.png](#recommended-dynamic_resourcespng)
   - [Recommended: dynamic_resources_database.xml](#recommended-dynamic_resources_databasexml)
3. [Step-by-Step: Building Your Template](#3-step-by-step-building-your-template)
4. [Using Your Template in CAIME](#4-using-your-template-in-caime)
5. [Sharing Templates with Others](#5-sharing-templates-with-others)
6. [Pro-Tips & Troubleshooting](#6-pro-tips--troubleshooting)

---

## 1. Understanding the Templates Folder

CAIME ships with a folder called `Templates` in its distribution package. This folder lives **alongside** (at the same level as) the main `CAIME` folder, not inside it.

```
CampaignMapToolkit\              ← the root of your CAIME installation
│
├── CAIME\                       ← the application lives here (CAIME.exe)
│   └── Release\
│       └── CAIME.exe
│
├── Projects\                    ← your own saved project files live here
│
├── Templates\                   ← custom templates live here  ← YOU WORK HERE
│   ├── main_rome_map\           ← example built-in template (Rome 2)
│   ├── main_attila_map\         ← example built-in template (Attila)
│   └── your_custom_template\   ← a template you create
│
└── Tools\                       ← internal processing tools (do not modify)
```

> **How CAIME finds the Templates folder:** When CAIME opens the **New Project** dialog, it automatically scans the `Templates` folder and lists every subfolder it finds as an option in the **Template** dropdown. There is nothing to configure — you just need to create your subfolder in the right place.

---

## 2. What Goes Inside a Template Folder

When a user selects your template in the **New Project** dialog and clicks **Create**, CAIME copies every file that sits directly inside your template folder into the new project. It does **not** copy subfolders — only the files at the top level of your template folder.

Here is what each file does and whether it is required.

---

### Required: map.hex

```
your_custom_template\
└── map.hex    ← REQUIRED — without this, the template will not work
```

This is the CAIME project file itself. It contains:
- The target **game** (Rome 2, Attila, Warhammer, etc.) — this is locked in when the template is chosen
- The **map dimensions** (width × height in hexes) — also locked in when the template is chosen
- All of the **painted layer data** (regions, terrain, roads, rivers, town slots, etc.)
- The **region colour table** used to display regions in the editor

Without `map.hex`, the template cannot be opened and will produce an error.

---

### Recommended: trees.png

```
your_custom_template\
├── map.hex
└── trees.png    ← RECOMMENDED — needed for Map Data processing
```

This is a PNG image that defines **where trees are placed** on the campaign map. It is used by the Assembly Kit's internal `BOB` tool when you run process terrain / tilemap or trees directly. Without it, the campaign map will appear visually with no trees (if its a new map).

> **For Rome 2, Attila, Warhammer I/II/III, Three Kingdoms, and Troy maps:** Always include `trees.png`. For Pharaoh maps it is less critical, but including it is still good practice.

---

### Recommended: tree_database.xml

```
your_custom_template\
├── map.hex
├── trees.png
└── tree_database.xml    ← RECOMMENDED — rules for tree types
```

An XML file that defines which tree species appear across the map (e.g., pine, oak, palm) and the rules for how they are distributed. Used alongside `trees.png`.

---

### Recommended: dynamic_resources.png

```
your_custom_template\
├── map.hex
├── trees.png
├── tree_database.xml
└── dynamic_resources.png    ← RECOMMENDED — resource icon positions
```

A PNG image painting where farmland tiles appear on the campaign map as a region's buildings level up: each colour is one building level from `dynamic_resources_database.xml`. Used when you run **Process → Dynamic Resources**.

> **Not needed for Three Kingdoms maps:** Three Kingdoms handles dynamic resources differently and does not use this file.

---

### Recommended: dynamic_resources_database.xml

```
your_custom_template\
├── map.hex
├── trees.png
├── tree_database.xml
├── dynamic_resources.png
└── dynamic_resources_database.xml    ← RECOMMENDED — links resources to positions
```

An XML file that links each coloured region in `dynamic_resources.png` to a specific in-game resource type. Used during **Process → Dynamic Resources**.

---

### Complete template folder at a glance

```
your_custom_template\
├── map.hex                          ← REQUIRED
├── trees.png                        ← Strongly recommended
├── tree_database.xml                ← Strongly recommended
├── dynamic_resources.png            ← Recommended (most games)
└── dynamic_resources_database.xml   ← Recommended (most games)
```

---

## 3. Step-by-Step: Building Your Template

---

**Step 1 — Obtain your map.hex**

The `map.hex` file in a template is almost always sourced directly from the Assembly Kit — it is the raw campaign map file that Creative Assembly produced during the game's development. There are two common origins:

- **From the Assembly Kit:** Navigate to the Assembly Kit's `raw_data` folder and locate the `map.hex` file for the campaign map you want to use as a base (see Step 2 for the exact path). This is the primary and recommended source — it is the authoritative, unmodified file for that map.

- **From a modder-made base:** Some experienced modders release their own pre-built `map.hex` files (custom regions, modified terrain, etc.) as community templates. These are packaged the same way and dropped into the `Templates` folder like any other.

---

**Step 2 — Gather the supporting files**

If you are building a template based on an official game map, the supporting files (`trees.png`, `tree_database.xml`, `dynamic_resources.png`, `dynamic_resources_database.xml`) are typically found in the Assembly Kit at:

```
[Assembly Kit path]\raw_data\EmpireDesignData\campaign_maps\[MapName]\
```

Copy those files from there. They do not need to be modified for your template — they just need to be present so that when a user creates a project from your template and then runs processing, the tools can find the files they need.

---

**Step 3 — Create your template subfolder**

Open **Windows File Explorer** and navigate to your CAIME installation root (the folder that contains the `CAIME`, `Projects`, `Templates`, and `Tools` subfolders).

Open the **`Templates`** folder.

```
 📁 CampaignMapToolkit\
  ├── 📁 CAIME\
  ├── 📁 Projects\
  ├── 📁 Templates\   ← open this
  └── 📁 Tools\
```

Inside `Templates`, **create a new folder**. The name you give this folder becomes the name that appears in CAIME's **Template** dropdown — choose something clear and descriptive.

```
Rules for naming your template folder:
────────────────────────────────────────
  ✓  my_rome2_template
  ✓  attila_barbarian_invasion_base
  ✓  wh3_custom_chaos_map
  ✗  my template (spaces can cause issues)
  ✗  My Template! (special characters can cause issues)

Recommended: use only letters, numbers, and underscores.
```

---

**Step 4 — Copy your files into the template folder**

Copy your prepared files directly into the new folder you just created. Do **not** create any subfolders inside it — CAIME only copies top-level files.

```
 📁 Templates\
  └── 📁 my_rome2_template\         ← your new folder
       ├── 📄 map.hex               ← copied from your CAIME project
       ├── 🖼 trees.png             ← copied from Assembly Kit
       ├── 📄 tree_database.xml     ← copied from Assembly Kit
       ├── 🖼 dynamic_resources.png ← copied from Assembly Kit
       └── 📄 dynamic_resources_database.xml
```

---

**Step 5 — Verify your template folder**

Before testing, do a quick check:

```
Template folder checklist
────────────────────────────────────────────────────────────────
 ☑  map.hex is present at the top level of the folder
 ☑  No caime_metadata.json file is in the folder
 ☑  No subfolders exist inside the template folder
 ☑  The folder name uses only letters, numbers, and underscores
```

---

## 4. Using Your Template in CAIME

Once your template folder is in place, you do not need to restart CAIME — the **Template** dropdown reads the `Templates` folder fresh each time you open the **New Project** dialog.

**Steps:**

1. In CAIME, go to **File → Create new map** (or press **Ctrl+N**).

   The **Create new project** dialog will appear:

   ```
   ┌─ Create new project ──────────────────────────────────────────────┐
   │                                                                    │
   │  Campaign map name:  [_____________________________________]       │
   │  ────────────────────────────────────────────────────────────      │
   │  Template:           [ None                               ▼ ]     │ ← click here
   │  Game:               [ Rome2                              ▼ ]     │
   │  Map size:           [  1016  ] x [  720  ]                       │
   │  ────────────────────────────────────────────────────────────      │
   │                                      [ Create ]  [ Cancel ]        │
   └────────────────────────────────────────────────────────────────────┘
   ```

2. In the **Campaign map name** field, type the internal name for your new map. This name must match your Assembly Kit database entries exactly (see the [Creating a New Map](user-guide-creating-a-new-map.md) guide for database setup details).

3. Click the **Template** dropdown and select your template from the list.

   ```
   Template: [ None                         ▼ ]
             ┌────────────────────────────────┐
             │ None                           │ ← blank canvas
             │ my_rome2_template              │ ← your template
             │ main_attila_map                │
             │ main_rome_map                  │
             └────────────────────────────────┘
   ```

4. Notice that as soon as you select a template, the **Game** and **Map size** fields become **greyed out**:

   ```
   ┌─ Create new project ──────────────────────────────────────────────┐
   │                                                                    │
   │  Campaign map name:  [ my_new_campaign        ]                   │
   │  ────────────────────────────────────────────────────────────      │
   │  Template:           [ my_rome2_template       ▼ ]  ← selected   │
   │  Game:               [ Rome2                   ▼ ]  ← locked     │
   │  Map size:           [  1016  ] x [  720  ]         ← locked     │
   │  ────────────────────────────────────────────────────────────      │
   │                                      [ Create ]  [ Cancel ]        │
   └────────────────────────────────────────────────────────────────────┘
   ```

   These fields are locked because the game type and map dimensions are already embedded inside the template's `map.hex` file — they cannot be overridden.

5. Click **Create**.

   CAIME will:
   - Create a new project folder at `Projects\[YourMapName]\`
   - Copy every file from your template folder into that project folder
   - Rename none of the files — they are copied as-is
   - Open the copied `map.hex` and connect to your Assembly Kit database

   Your new project will open with all of the template's layer data already visible and ready to edit.

---

## 5. Sharing Templates with Others

To share a template:

1. Navigate to your `Templates` folder.
2. **Right-click** your template subfolder and select **Send to → Compressed (zipped) folder** (or use any archive tool like 7-Zip or WinRAR).
3. Send the resulting `.zip` file to your collaborator.

The recipient should:
1. Extract the `.zip` file.
2. Move the extracted folder into their own `Templates` folder (inside their CAIME installation).
3. Open the **New Project** dialog — the template will appear in the **Template** dropdown immediately.

```
CAIME installation template sharing workflow:
────────────────────────────────────────────────────────────────────
  You (template author)                    Recipient
  ──────────────────────                   ─────────────────────────
  Templates\
  └── my_rome2_template\                   Templates\
       ├── map.hex             → .zip →    └── my_rome2_template\
       ├── trees.png           →  →             ├── map.hex
       ├── tree_database.xml   →  →             ├── trees.png
       └── ...                              └── ...
                                            (now appears in dropdown)
```

---

## 6. Pro-Tips & Troubleshooting

- **My template does not appear in the dropdown.**
  Check that your template folder is directly inside the `Templates` folder — not inside another subfolder within it. The path should be `Templates\your_template_name\map.hex`, not `Templates\some_other_folder\your_template_name\map.hex`. Also check the folder name: avoid spaces and special characters.

- **I created a project from my template but the Swatches panel is empty.**
  The Swatches panel is populated from your Assembly Kit database, not from the template files. A template does not carry database entries with it — only the painted layer data from `map.hex`. The recipient must have the correct Assembly Kit installed and set up in **Settings → Preferences** (Ctrl+P), and the required database entries (regions, ground types, climates, etc.) must exist in that Assembly Kit before opening the project. Refer to the [Creating a New Map](user-guide-creating-a-new-map.md) guide for the database setup steps.

- **Process → Map Data or Process → Dynamic Resources fails after I used the template.**
  This usually means `trees.png`, `tree_database.xml`, `dynamic_resources.png`, or `dynamic_resources_database.xml` is missing from the project folder (and therefore was not in the template). Check that all five files are present in your template folder, then delete the project, re-create it from the template, and run the process again. You can also copy the missing files directly into the project folder at `Projects\[MapName]\` without re-creating the project from scratch.

- **Do not include `caime_metadata.json` in your template.**
  If you have used the Map Data Editor on your map before making it a template, CAIME will have created a `caime_metadata.json` file in your project folder. This file stores an absolute path to a `map_data_config.xml` file on your own machine. If you include it in your template and another user creates a project from it, CAIME will try to read the config file from a path that does not exist on their machine, and Map Data auto-patching will silently fail. Always check your template folder and remove `caime_metadata.json` before distributing.

---

*Guide version: 1.0 — Campaign AI Map Editor*
