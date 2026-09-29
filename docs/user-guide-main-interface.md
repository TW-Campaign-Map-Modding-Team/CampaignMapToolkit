# Campaign AI Map Editor — Main Interface Guide

---

## Overview

Campaign AI Map Editor (CAIME) is a painting application for creating campaign map data files used by Total War games. Think of it like Adobe Photoshop, but instead of painting colours for artistic purposes, you are painting *game data* — telling the game engine where roads are, where rivers flow, what climate each region has, and much more.

Each type of map data lives on its own **Layer**, just like layers in Photoshop. You pick a layer, pick a colour (called a **Swatch**), and paint onto the map. When you are done, the application converts your painted canvas into the binary files the game engine reads.

Supported games: **Rome 2, Attila, Thrones of Britannia, Warhammer, Warhammer 2, Warhammer 3, Three Kingdoms, Troy, Pharaoh,** and **Pharaoh Dynasties**.

---

## Table of Contents

1. [The Application Window at a Glance](#1-the-application-window-at-a-glance)
2. [The Title Bar](#2-the-title-bar)
3. [The Menu Bar](#3-the-menu-bar)
   - [File Menu](#file-menu)
   - [Edit Menu](#edit-menu)
   - [Process Menu](#process-menu)
   - [Tools Menu](#tools-menu)
   - [Settings Menu](#settings-menu)
   - [Help Menu](#help-menu)
4. [The Quick-Settings Bar](#4-the-quick-settings-bar)
5. [The Toolbar — Painting Tools](#5-the-toolbar--painting-tools)
6. [The Main Viewport (Map Canvas)](#6-the-main-viewport-map-canvas)
7. [The Sidebar](#7-the-sidebar)
   - [Minimap](#minimap)
   - [Swatches](#swatches)
   - [Actions](#actions)
   - [Layers](#layers)
8. [The Status Bar](#8-the-status-bar)
9. [Keyboard Shortcuts Reference](#9-keyboard-shortcuts-reference)
10. [Pro-Tips & Troubleshooting](#10-pro-tips--troubleshooting)

---

## 1. The Application Window at a Glance

The diagram below shows every region of the CAIME window. Each region is described in its own section of this guide.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  Campaign AI Map Editor                                        ▁   ❐   ✕    │◄─ Title Bar
├──────────────────────────────────────────────────────────────────────────────┤
│  File    Edit    Process    Tools    Settings    Help                         │◄─ Menu Bar
├────┬─────────────────────────────────────────────────────────────────────────┤
│    │ Brush Size: [──●──] 5  │  BG Opacity: [──●──] 50  │ [✕]  Source: [▼]  │◄─ Quick-Settings Bar
│    ├──────────────────────────────────────────────────────┬──────────────────┤
│ T  │                                                      │ ┌─ Minimap ────┐ │
│ o  │                                                      │ │              │ │
│ o  │                                                      │ │  [map thumb] │ │
│ l  │                                                      │ │              │ │
│ b  │              MAIN VIEWPORT                           │ └──────────────┘ │
│ a  │             (Map Canvas)                             │ ┌─ Swatches ───┐ │
│ r  │                                                      │ │ ■  Grass  ▼  │ │
│    │                                                      │ └──────────────┘ │◄─ Sidebar
│    │                                                      │ ┌─ Actions ────┐ │
│    │                                                      │ │[New][Ren][✕] │ │
│    │                                                      │ └──────────────┘ │
│    │                                                      │ ┌─ Layers ─────┐ │
│    │                                                      │ │☑ ● Ground    │ │
│    │                                                      │ │☑   Climates  │ │
│    │                                                      │ │☑   Regions   │ │
│    │                                                      │ │☑   Attrition │ │
│    │                                                      │ │☑   Roads     │ │
│    │                                                      │ │   ...        │ │
│    │                                                      │ └──────────────┘ │
├────┴──────────────────────────────────────────────────────┴──────────────────┤
│  Last log message here...                    X: 42   Y: 17   60 FPS   v1.0  │◄─ Status Bar
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. The Title Bar

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  Campaign AI Map Editor                                        ▁   ❐   ✕    │
└──────────────────────────────────────────────────────────────────────────────┘
```

The **Title Bar** sits at the very top of the window. It shows the application name and the three standard window control buttons on the right:

| Button | Symbol | Action |
|--------|--------|--------|
| Minimise | **▁** | Hides the window to the taskbar |
| Maximise / Restore | **❐** | Expands or returns the window to its previous size |
| Close | **✕** | Closes the application (you will be asked to save if there are unsaved changes) |

---

## 3. The Menu Bar

```
  File    Edit    Process    Tools    Settings    Help
```

The **Menu Bar** is the horizontal row of menus just below the title bar. Click any menu name to open its list of commands.

---

### File Menu

> **Go to: File**

| Menu Item | Shortcut | What It Does |
|-----------|----------|--------------|
| **Create new map** | Ctrl+N | Opens the *New Project* window to start a brand-new campaign map |
| **Open** | Ctrl+O | Opens Windows File Explorer so you can browse to and load an existing `.hex` project file |
| **Reload** | Ctrl+R | Discards any unsaved changes and reloads the project from its last saved state on disk |
| **Save** | Ctrl+S | Saves all your current painting work to the existing file |
| **Save as...** | Ctrl+Shift+S | Opens Windows File Explorer so you can save a copy with a new name or in a new folder |
| **Close** | Ctrl+X | Closes the current project without exiting the application |
| **Exit** | Ctrl+Q | Closes the application completely |

> **Tip:** If you try to **Exit** or **Close** without saving, a pop-up will appear asking *"Are you sure you want to quit without saving changes?"* Click **Yes** to discard changes, or **No** to go back and save first.

---

### Edit Menu

> **Go to: Edit**

| Menu Item | Shortcut | What It Does |
|-----------|----------|--------------|
| **Undo** | Ctrl+Z | Reverses your last painting action |
| **Redo** | Ctrl+Y | Re-applies an action you just undid |
| **Resize** | Ctrl+Shift+R | Opens the *Map Resize* window to change the map dimensions |
| **Rename map** | Ctrl+Shift+N | Opens a dialog to rename your campaign map |

---

### Process Menu

> **Go to: Process**

The **Process** menu converts your painted layers into the actual binary data files that Total War games can read. Run these when you are ready to export your work.

| Menu Item | What It Exports |
|-----------|-----------------|
| **Map Data** | The main map data file (ESF format) |
| **Pathfinding data** | Unit movement and pathfinding calculations |
| **Borders data** | Region border information |
| **Dynamic resources** | Dynamic resource placement data |
| **Trade Routes** | Trade route network data |
| **Lookup and Minimap images** | The lookup table image and minimap image shown in-game |

---

### Tools Menu

> **Go to: Tools**

| Menu Item | Shortcut | What It Does |
|-----------|----------|--------------|
| **Show Logger** | Ctrl+L | Opens the full Logger window with the complete message history |
| **Border Editor** | Ctrl+B | Opens the dedicated Border Editor tool |
| **Map Data Editor** | Ctrl+M | Opens the Map Data Editor tool |
| **Shader Resolution Corrector** | — | Patches the UI-overlay resolution baked into one or two compiled shaders (.fxc). Three Kingdoms projects only |
| **Import > Layer data** | — | Imports a previously exported layer image back into the project |
| **Export > SVG Borders** | — | Exports region borders as an SVG vector image |
| **Export > Baseline Tilemap image** | — | Generates a baseline tilemap reference image |
| **Export > Layer data** | — | Exports the currently active layer as an image file |
| **Validate > [Layer Name]** | — | Runs a check on the chosen layer and reports any issues in the Logger |

**Available Validation Checks:**

```
  Rivers       Town Slots    Roads        Bridges
  Beaches      Regions       Attritions   Climates
  Ground Types Impassable    Town Sprawl
```

After a validation run, a pop-up will tell you either:
- *"No issues have been found during [Layer] layer validation."* — all good!
- *"Validating [Layer] layer failed. See output logs for details."* — open the **Logger** (Ctrl+L) to read what went wrong.

---

### Settings Menu

> **Go to: Settings**

| Menu Item | Shortcut | What It Does |
|-----------|----------|--------------|
| **Preferences** | Ctrl+P | Opens the *Preferences* window where you can set your hex spacing, base game, and Assembly Kit path |

---

### Help Menu

> **Go to: Help**

| Menu Item | What It Does |
|-----------|--------------|
| **About** | Shows the application version and basic information |
| **License** | Displays the software licence |
| **EULA** | Displays the End User Licence Agreement |
| **Credits** | Lists the contributors to the project |
| **User Guides** | Opens these user guides online in your browser |
| **Discord support server** | Opens the community Discord server in your browser |

---

## 4. The Quick-Settings Bar

```
  Brush Size: [──────●──] 5   │  BG Opacity: [──────●──] 50  │ [✕ Clear]  │  Source: [▼ Layer Name]
```

The **Quick-Settings Bar** sits below the Menu Bar and provides fast access to the most commonly adjusted painting settings. It only appears when a project is open.

| Control | What It Does |
|---------|--------------|
| **Brush Size slider** | Drag left or right to make your brush smaller (1) or larger (10). The current size number is displayed next to the slider. |
| **BG Opacity slider** | Controls how visible the background reference image is (0 = invisible, 100 = fully visible). Only relevant when a background image is loaded. |
| **Clear background image button (✕)** | Removes the loaded background reference image from the viewport. |
| **Source dropdown** | Selects which layer is used as the source for the **Flood Fill** tool. For example, you can flood-fill the *Ground Types* layer using the shape of a *Regions* boundary. |

---

## 5. The Toolbar — Painting Tools

```
 ┌───┐
 │ M │  ← Pan
 │ Z │  ← Zoom
 │ B │  ← Brush
 │ F │  ← Flood Fill
 │ E │  ← Eraser
 │ I │  ← Background Image
 │ L │  ← Line
 │ P │  ← Color Picker
 │ ⚙ │  ← Properties
 └───┘
```

The **Toolbar** is the narrow vertical strip on the left side of the editor. It contains all of your painting tools. Click a tool icon (or press its hotkey) to activate it — only one tool can be active at a time.

| Icon | Hotkey | Tool Name | What It Does |
|------|--------|-----------|--------------|
| Pan icon | **M** | **Pan** | Click and drag to move the map around the viewport. This is the default navigation tool and does not paint anything. |
| Zoom icon | **Z** | **Zoom** | Scroll the mouse wheel (or click and drag) to zoom the map in or out. |
| Brush icon | **B** | **Brush** | Paints hexes with the currently selected Swatch colour. Click a hex or click-and-drag across multiple hexes. |
| Flood Fill icon | **F** | **Flood Fill** | Fills an entire connected area of the same colour with the selected Swatch. Works like the paint bucket in MS Paint. |
| Eraser icon | **E** | **Eraser** | Removes the painted data from hexes, returning them to blank/empty. |
| BackImg icon | **I** | **Background Image** | Loads a reference image (e.g. a hand-drawn map scan) behind the hex grid so you can trace over it. |
| Line icon | **L** | **Line** | Click to set a start point, then click again to paint a straight line of hexes between the two points. |
| ColorPicker icon | **P** | **Color Picker** | Click any hex to pick up its Swatch colour as the active painting colour. **Pro-tip:** Hold **Alt** on your keyboard to temporarily switch to the Color Picker at any time, then release **Alt** to return to your previous tool. |
| Properties icon | **⚙** | **Properties** | Opens a dialog with advanced settings for the current brush. |

> **How to know which tool is active?** The active tool's button appears highlighted/pressed in the Toolbar.

---

## 6. The Main Viewport (Map Canvas)

The large centre area of the application is the **Map Canvas** — this is where your campaign map is displayed as a grid of hexagonal tiles. Everything you paint appears here in real time.

```
 ┌─────────────────────────────────────────────────────┐
 │                                                     │
 │    ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡         │
 │  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡        │
 │    ⬡  ⬡  [painted] ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡          │
 │  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡        │
 │                                                     │
 └─────────────────────────────────────────────────────┘
```

**Navigating the map:**

| Action | Result |
|--------|--------|
| **Mouse wheel scroll** | Zoom in or out |
| **Middle mouse button drag** | Pan (scroll around) the map |
| **Select Pan tool (M) + left-click drag** | Pan the map |
| **Select Zoom tool (Z) + mouse wheel** | Zoom in or out |

**Painting on the map:**

1. Select a **Layer** from the Sidebar (see Section 7).
2. Select a **Swatch** (colour) from the Sidebar.
3. Select a **painting tool** from the Toolbar (e.g. **Brush**).
4. **Click** or **click-and-drag** on hexes in the viewport to paint them.

The status bar at the bottom always shows the current hex coordinates under your cursor (`X:` and `Y:`).

---

## 7. The Sidebar

The **Sidebar** is the 250-pixel-wide panel on the right side of the editor. It is divided into four collapsible sections. Click any section header to expand or collapse it.

```
 ┌─────────────────────────────────────┐
 │ ▼ Minimap                           │
 │ ┌───────────────────────────────┐   │
 │ │      [minimap thumbnail]      │   │
 │ │          [red frame]          │   │
 │ └───────────────────────────────┘   │
 ├─────────────────────────────────────┤
 │ ▼ Swatches                          │
 │  ■ Grassland                    ▼   │
 ├─────────────────────────────────────┤
 │ ▼ Actions                           │
 │  [New]   [Rename]   [Remove]        │
 ├─────────────────────────────────────┤
 │ ▼ Layers                            │
 │  ☑  ●  Ground Types                 │
 │  ☑      Climates                    │
 │  ☑      Regions                     │
 │  ☑      Attritions                  │
 │  ☑      Trade Routes                │
 │  ☑      Restrictions                │
 │  ☑      Beaches                     │
 │  ☑      Rivers                      │
 │  ☑      Bridges                     │
 │  ☑      Town Sprawl                 │
 │  ☑      Town Slots                  │
 │  ☑      Roads                       │
 │  ☑      Impassable                  │
 └─────────────────────────────────────┘
```

---

### Minimap

The **Minimap** is a thumbnail view of the entire campaign map. It lets you see the big picture even when the main viewport is zoomed in close.

- The **red rectangle** on the minimap shows you exactly which part of the map is currently visible in the main viewport.
- **Click anywhere on the minimap** to instantly jump the main viewport to that area.

---

### Swatches

The **Swatches** section lets you choose *what* you paint with — the colour or data value that will be applied to hexes when you use a painting tool.

```
  ■  [Colour Preview Box]    [▼  Swatch Name Dropdown]
```

- The coloured square on the left shows a preview of the currently selected swatch.
- Click the **dropdown** on the right to open a list of all available swatches for the currently active layer.
- Select a swatch from the list to make it the active painting colour.

> **Note:** The swatches available depend entirely on which **Layer** is selected. For example, the *Ground Types* layer has swatches like "Grassland" and "Desert", while the *Climates* layer has swatches like "Temperate" and "Arid".

---

### Actions

The **Actions** section shows buttons that perform operations on the active layer's swatches. The available buttons may change depending on which layer is selected.

| Button | What It Does |
|--------|--------------|
| **New** | Creates a new swatch entry for the active layer |
| **Rename** | Renames the currently selected swatch |
| **Remove** | Deletes the currently selected swatch |
| **Cleanup** | Removes any swatches that are not painted on any hex (cleans up unused entries) |

---

### Layers

The **Layers** section is one of the most important parts of the application. It works just like the Layers panel in Photoshop — each layer holds one specific type of map data, and you paint each layer independently.

```
  ☑  ●  Ground Types      ← Active layer (highlighted)
  ☑      Climates
  ☑      Regions
  ☒      Roads             ← Hidden layer (unchecked)
```

**Understanding the Layers List:**

| Element | What It Does |
|---------|--------------|
| **Checkbox (☑/☒)** | Toggles the layer's visibility on the map canvas. Uncheck to hide a layer without deleting it. |
| **Radio button (●)** | Marks the **active** layer — the one you are currently painting on. Only one layer can be active at a time. |
| **Layer name** | The name of the layer. The active layer's name appears highlighted. |

**How to switch layers:**

1. Find the layer you want to paint on in the **Layers** list.
2. Click the **radio button** next to its name to make it the active layer.
3. The **Swatches** section above will automatically update to show the swatches for that layer.
4. You can now paint on the map using your selected tool and swatch.

**How to re-arrange layers:**

Layers higher in the list are drawn on top. To change the order, drag a layer by the **⋮** dots on its right and drop it where the white line shows it will land, or right-click a row and choose **Move up**, **Move down**, **Move to top** or **Move to bottom**. The **Reset order** button under the list restores the built-in order. The order only affects what you see, and CAIME remembers it for each game in your preferences.

The **Layer Opacity** section, between **Actions** and **Layers**, has a slider for the active layer; lower it to let the layers below show through (right-click a row → **Opacity** for presets). Opacity is display only, and every layer starts at 100% when a project opens. See [Layer opacity](user-guide-layers-reference.md#layer-opacity).

**Standard layers** (available in all supported games, listed from top to bottom as shown in the panel):

| Layer Name | What It Controls |
|------------|-----------------|
| **Ground Types** | The terrain type of each hex (grass, desert, snow, etc.) |
| **Climates** | Climate zones across the map |
| **Attritions** | Areas that cause attrition (damage to armies over time) |
| **Regions** | Which political region each hex belongs to |
| **Beaches** | Coastal/beach hex locations |
| **Rivers** | River paths across the map |
| **Bridges** | Bridge crossing locations |
| **Town Sprawl** | The visual spread area around settlement hexes |
| **Town Slots** | The exact hexes where settlements can be placed |
| **Roads** | Road network paths |
| **Impassable** | Areas that cannot be entered by armies |

**Game-specific layers** (only appear when your project is set to the relevant game):

| Layer Name | Games |
|------------|-------|
| **Trade Routes** | Rome 2, Attila, Thrones of Britannia, Three Kingdoms |
| **Region Borders** | Attila and later |
| **Restrictions** | Attila and later |
| **Areas of Interest** | Warhammer 3, Three Kingdoms |

---

## 8. The Status Bar

```
  [Last log message text...]           X: 42   Y: 17   60 FPS   v1.0.0
```

The **Status Bar** is the thin ribbon running along the very bottom of the application window. It gives you at-a-glance information about the current state of the editor.

| Element | What It Shows |
|---------|---------------|
| **Last log message** | The most recent information, warning, or error message from the application. Click it to open the full **Logger** window and see the complete message history. |
| **X: [number]** | The horizontal column coordinate of the hex currently under your mouse cursor. |
| **Y: [number]** | The vertical row coordinate of the hex currently under your mouse cursor. |
| **[number] FPS** | The current rendering frame rate. Lower numbers can indicate the map is very large or your machine is under heavy load. |
| **v[version]** | The current version of the application. |

> **Tip:** If something unexpected happens, the **Last log message** in the status bar is the first place to look. Click on it to open the full Logger and read the complete details.

---

## 9. Keyboard Shortcuts Reference

### File & Project

| Shortcut | Action |
|----------|--------|
| **Ctrl+N** | Create new map |
| **Ctrl+O** | Open project |
| **Ctrl+R** | Reload project from disk |
| **Ctrl+S** | Save project |
| **Ctrl+Shift+S** | Save as (new name/location) |
| **Ctrl+X** | Close current project |
| **Ctrl+Q** | Exit application |

### Editing

| Shortcut | Action |
|----------|--------|
| **Ctrl+Z** | Undo last action |
| **Ctrl+Y** | Redo |
| **Ctrl+Shift+R** | Resize map |
| **Ctrl+Shift+N** | Rename campaign map |

### Tools

| Shortcut | Tool Activated |
|----------|----------------|
| **M** | Pan |
| **Z** | Zoom |
| **B** | Brush |
| **F** | Flood Fill |
| **E** | Eraser |
| **I** | Background Image |
| **L** | Line |
| **P** | Color Picker |
| **Hold Alt** | Temporarily switch to Color Picker (release to return to previous tool) |

### Windows & Panels

| Shortcut | Action |
|----------|--------|
| **Ctrl+L** | Open Logger window |
| **Ctrl+B** | Open Border Editor |
| **Ctrl+M** | Open Map Data Editor |
| **Ctrl+P** | Open Preferences |

---

## 10. Pro-Tips & Troubleshooting

- **If you accidentally paint over an area**, immediately press **Ctrl+Z** to undo. CAIME supports multiple undo steps, so you can press **Ctrl+Z** several times in a row to step back through your recent painting history.

- **If the "Save" option is greyed out** or doesn't seem to respond, check that you have an open project — use **File > Open** (Ctrl+O) to open a `.hex` file first. Similarly, if a message appears saying *"Missing Assembly Kit path"*, go to **Settings > Preferences** (Ctrl+P) and set the correct path to your game's Assembly Kit installation folder.

- **If the map canvas feels slow or laggy**, check the **FPS counter** in the Status Bar. Try hiding some layers (uncheck their visibility checkbox in the Layers panel) to reduce the number of things being drawn at once. You can re-enable them at any time — hiding a layer does not delete any painted data.

- **If a Process or Validation step fails**, click the **Last log message** text in the Status Bar (or press **Ctrl+L**) to open the Logger window. The detailed error messages listed there will tell you exactly which hexes or layers have issues that need to be fixed before exporting.

- **If you want to trace over a real map image**, use the **Background Image tool (I)** to load a reference picture. Then use the **BG Opacity slider** in the Quick-Settings Bar to make it semi-transparent so you can see your painted hexes on top of it. When you no longer need the reference image, click the **Clear background image (✕)** button to remove it without affecting any of your painted layers.

- **If your Flood Fill is not spreading the way you expect**, check the **Source** dropdown in the Quick-Settings Bar. This controls which layer defines the "boundary" for the flood fill. Setting it to the same layer you are painting will fill based on that layer's existing colours; setting it to a different layer (e.g. *Regions*) will use that layer's shape as the fill boundary instead.

---

*Guide version: 1.0 — Campaign AI Map Editor*
