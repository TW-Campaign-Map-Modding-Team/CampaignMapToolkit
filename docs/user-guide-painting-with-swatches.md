# Campaign AI Map Editor — Painting with Swatches

---

## Overview

Painting in CAIME works like a specialised version of Photoshop's brush and bucket tools — but instead of choosing a colour from a colour wheel, you choose a **Swatch**. A Swatch is a named, preset tile of data (for example, *"Grassland"*, *"Temperate"*, or *"Main Settlement"*) that tells the game engine what each hex on the map means. When you paint a hex with a Swatch, you are not just colouring it — you are writing game data into it.

Each Swatch belongs to a specific **Layer**, and you can only see and use the Swatches for whichever Layer is currently active. Think of it like a colouring book where each chapter uses a completely different set of crayons: the *Ground Types* chapter has terrain crayons, the *Climates* chapter has climate crayons, and so on.

---

## Table of Contents

1. [Understanding the Three Ingredients](#1-understanding-the-three-ingredients)
2. [Step 1 — Choose Your Layer](#2-step-1--choose-your-layer)
3. [Step 2 — Choose Your Swatch](#3-step-2--choose-your-swatch)
4. [Step 3 — Choose Your Tool and Paint](#4-step-3--choose-your-tool-and-paint)
   - [Brush Tool](#brush-tool-hotkey-b)
   - [Flood Fill Tool](#flood-fill-tool-hotkey-f)
   - [Eraser Tool](#eraser-tool-hotkey-e)
   - [Line Tool](#line-tool-hotkey-l)
   - [Color Picker Tool](#color-picker-tool-hotkey-p)
   - [Background Image Tool](#background-image-tool-hotkey-i)
5. [Triangle Warning — Roads, Rivers & Trade Routes](#5-triangle-warning--roads-rivers--trade-routes)
6. [Adjusting Brush Size](#6-adjusting-brush-size)
7. [Managing Swatches (Create, Rename, Remove)](#7-managing-swatches-create-rename-remove)
8. [Undoing and Redoing Painting Actions](#8-undoing-and-redoing-painting-actions)
9. [Pro-Tips & Troubleshooting](#9-pro-tips--troubleshooting)

---

## 1. Understanding the Three Ingredients

Every painting action in CAIME requires three things working together. Think of it as a recipe:

```
  ┌─────────────────────────────────────────────────────┐
  │                                                     │
  │   LAYER          +   SWATCH        +   TOOL         │
  │  (the canvas)       (the paint)       (the brush)   │
  │                                                     │
  │   Which type of        What value         How you   │
  │   data are you         are you            apply it  │
  │   editing?             painting?          to hexes  │
  │                                                     │
  │   e.g.                 e.g.               e.g.      │
  │   Ground Types         Grassland          Brush     │
  │                                                     │
  └─────────────────────────────────────────────────────┘
              ▼            ▼             ▼
              └────────────┴─────────────┘
                           │
                    Paint on hex grid
```

All three are shown together in the interface at the same time:

```
 ┌──── Sidebar (right panel) ──────────┐  ┌──── Toolbar (left strip) ──┐
 │                                     │  │                            │
 │ ▼ Swatches                          │  │   M   Pan                  │
 │  ■ Grassland                  ▼     │  │   Z   Zoom                 │
 │                                     │  │  [B]  Brush  ← active      │
 │ ▼ Layers                            │  │   F   Flood Fill           │
 │  ☑  ●  Ground Types  ← active       │  │   E   Eraser               │
 │  ☑      Climates                    │  │   L   Line                 │
 │  ☑      Regions                     │  │   P   Color Picker         │
 └─────────────────────────────────────┘  └────────────────────────────┘
```

---

## 2. Step 1 — Choose Your Layer

The **Layers** section is at the bottom of the right-hand Sidebar. It lists every type of data your map can hold.

1. Find the layer you want to edit in the **Layers** list.
2. Click the **round radio button (●)** to the left of its name to make it the active layer.
   - The layer name will become highlighted to confirm it is active.
   - The **Swatches** section at the top of the Sidebar will instantly update to show that layer's swatches.

```
 ┌─ Layers ─────────────────────────────┐
 │                                      │
 │  ☑  ●  Ground Types   ← click ● here │
 │  ☑      Climates                     │
 │  ☑      Regions                      │
 │  ☑      Attritions                   │
 │  ☑      Roads                        │
 │  ...                                 │
 └──────────────────────────────────────┘
```

> **Note:** Hiding a layer (unchecking the ☑ box) only hides it visually. You cannot paint on a hidden layer — make sure the layer's checkbox is checked before painting.

---

## 3. Step 2 — Choose Your Swatch

The **Swatches** section is near the top of the right-hand Sidebar.

1. Look at the **coloured square** — it shows the currently active Swatch colour.
2. Click the **dropdown arrow (▼)** to open the full list of Swatches for the active layer.
3. Click any Swatch name in the list to select it.
   - The coloured square updates to show the new selection.
   - Any hexes you paint next will receive that Swatch's data value.

```
 ┌─ Swatches ────────────────────────────────┐
 │                                           │
 │   ■ [colour preview]  Grassland      ▼   │◄── click ▼ to open list
 │                                           │
 │                       ┌────────────────┐  │
 │                       │ Grassland      │  │
 │                       │ Desert         │  │
 │                       │ Snow           │  │
 │                       │ Mud            │  │
 │                       │ Steppe         │  │
 │                       └────────────────┘  │
 └───────────────────────────────────────────┘
```

> **What Swatches are available?** It depends entirely on which Layer is active. Here are some examples:

| Active Layer | Example Swatches |
|---|---|
| **Ground Types** | Grassland, Desert, Snow, Mud, Steppe, Wetland… |
| **Climates** | Temperate, Arid, Tropical, Tundra… |
| **Regions** | Your named regions (e.g. "Gaul", "Egypt") |
| **Attritions** | Named attrition zones |
| **Roads / Rivers / Trade Routes** | Paint (add) or Erase (remove) — these are on/off layers. **See [Section 5](#5-triangle-warning--roads-rivers--trade-routes) for a critical rule about triangle junctions.** |
| **Bridges / Beaches / Impassable / Town Sprawl** | Paint (add) or Erase (remove) — these are on/off layers |
| **Town Slots** | Main Settlement, Port, Slot 2, Slot 3… |
| **Trade Routes** | Trade route markers |
| **Restrictions** | Restriction level values |
| **Campaign Masks** | Masked — paint (mask) or Erase (unmask) hexes of the mask chosen in the Actions panel |

---

## 4. Step 3 — Choose Your Tool and Paint

Once your Layer and Swatch are set, select a painting tool from the **Toolbar** on the left side of the screen.

---

### Brush Tool (Hotkey: B)

The most common tool. Click or drag to paint hexes with the selected Swatch.

**How to use:**

1. Press **B** on your keyboard (or click the Brush icon in the Toolbar).
2. Move your mouse over the map — you will see a circular cursor showing the brush area.
3. **Click** a single hex to paint it.
4. **Click and drag** across the map to paint a continuous stroke across many hexes.
5. Release the mouse button to finish the stroke.

```
  Brush size 1              Brush size 3
  (single hex)              (wider area)

       ⬡                   ⬡  ⬡  ⬡
                          ⬡  ⬡  ⬡  ⬡
                            ⬡  ⬡  ⬡

  ● = painted hex         ● = painted hex
```

> The Brush paints all hexes whose centre falls within the circular brush area. Use the **Brush Size** slider (see [Section 5](#5-adjusting-brush-size)) to control how many hexes are covered in one stroke.

---

### Flood Fill Tool (Hotkey: F)

Works like the paint-bucket tool in MS Paint. Click one hex and the fill automatically spreads outward across all connected hexes that share the same value — stopping at boundaries.

**How to use:**

1. Press **F** on your keyboard (or click the Flood Fill icon).
2. **Click once** on any hex to trigger the fill. Do not drag — the fill applies instantly on click.

**The Source dropdown matters:**

The **Source** dropdown in the Quick-Settings Bar at the top of the screen controls which layer defines the "boundary" for the flood fill. This lets you fill the shape of one layer using another layer as a guide.

```
  ┌─────── Quick-Settings Bar ──────────────────────────────────┐
  │  Brush Size: [...] 5  │  BG Opacity: [...] 50  │  Source: [▼ Regions]  │
  └──────────────────────────────────────────────────────────────┘
```

**Example:** You have a *Regions* layer already painted with political regions. You want to flood-fill the *Climates* layer so that the entire Gaul region gets the "Temperate" climate.

1. Set the active layer to **Climates**.
2. Select the **Temperate** Swatch.
3. Set the **Source** dropdown to **Regions**.
4. Select the **Flood Fill** tool and click any hex inside the Gaul region.
5. The fill spreads across all hexes that belong to Gaul (as defined by the Regions layer) and paints them "Temperate".

> **Supported source layers for Flood Fill:** Ground Types, Climates, Attritions, Regions, Impassable, Roads, Trade Routes, Restrictions, Campaign Masks.

---

### Eraser Tool (Hotkey: E)

Removes painted data from hexes, resetting them to their blank/empty state. Works exactly like the Brush tool — you click and drag — but instead of applying a Swatch it clears the data.

**How to use:**

1. Press **E** on your keyboard (or click the Eraser icon).
2. **Click or drag** across hexes to clear their data.
3. Release the mouse to finish.

> The Eraser respects the active layer — it only clears data on the currently active layer. Data on all other layers is untouched.

---

### Line Tool (Hotkey: L)

Paints a straight line of hexes between two points. Useful for painting roads, rivers, or trade routes quickly across long distances.

**How to use:**

1. Press **L** on your keyboard (or click the Line icon).
2. **Click once** on the starting hex. The hex will highlight in **red** to mark the start point.
3. **Move your mouse** toward your destination — the hexes along the shortest path highlight in **yellow** as a live preview.
4. **Click again** on the destination hex. The entire line is painted with the selected Swatch.

```
  Before release:           After release:
  (preview colours)         (final paint)

   [R] = Red (start)         ● ● ● ● ●
   [Y] = Yellow (path)
   [R] = Red (end)

  [R]──[Y]──[Y]──[Y]──[R]   ● ● ● ● ●
```

> **Note:** If any hex along the path cannot be painted (for example, it is outside the map boundary), it will highlight in a different colour to warn you before you commit.

---

### Color Picker Tool (Hotkey: P)

Picks up the Swatch from any hex you click and makes it the active Swatch. Useful when you want to match an existing area without having to find the right Swatch manually in the dropdown.

**How to use:**

1. Press **P** on your keyboard (or click the Color Picker icon).
2. **Click any hex** on the map.
3. The **Swatches** section in the Sidebar instantly updates to show the Swatch that was applied to that hex.
4. Switch back to your preferred painting tool (e.g. press **B** for Brush) — you are now set up to paint with the same Swatch.

**Shortcut — hold Alt to pick without switching tools:**

> While using the Brush, Flood Fill, Eraser, or Line tool, hold **Alt** on your keyboard to temporarily switch to the Color Picker. Hover over any hex to sample its Swatch, then release **Alt** to instantly return to the tool you were using before. This means you never have to leave your painting tool to sample a colour.

---

### Background Image Tool (Hotkey: I)

Loads a reference image — such as a scanned hand-drawn map, a screenshot from the game, or an exported tilemap — and pins it behind the hex grid so you can trace over it while painting. Think of it like taping a sketch underneath tracing paper: the image guides your hand without becoming part of the final output.

> **Important:** The background image is for visual reference only. It is never saved into the project file and is never exported into any game data. It simply helps you align your painting.

---

**Loading a background image:**

1. Press **I** on your keyboard (or click the **Background Image** icon in the Toolbar).
2. The Windows file browser opens immediately.
3. Navigate to your reference image. Supported formats: **JPG, JPEG, BMP, TIFF, PNG**.
4. Select the file and click **Open**.
5. The image appears behind the hex grid, filling the entire map area. The application automatically returns you to whichever tool you were using before.

```
  Before loading:                After loading:

  ┌─────────────────────────┐    ┌─────────────────────────┐
  │   ⬡  ⬡  ⬡  ⬡  ⬡  ⬡    │    │  ░░░░░░░░░░░░░░░░░░░░░  │ ← reference
  │  ⬡  ⬡  ⬡  ⬡  ⬡  ⬡     │    │  ░⬡  ⬡  ⬡  ⬡  ⬡  ⬡░   │   image
  │   ⬡  ⬡  ⬡  ⬡  ⬡  ⬡    │    │  ░░⬡  ⬡  ⬡  ⬡  ⬡  ⬡░  │   showing
  │  (blank grey canvas)    │    │  ░░░░░░░░░░░░░░░░░░░░░  │   through
  └─────────────────────────┘    └─────────────────────────┘
```

---

**Adjusting how visible the image is (Opacity):**

Once an image is loaded, the **Image opacity** slider in the Quick-Settings Bar becomes active. Use it to blend the image behind your painted hexes so neither overwhelms the other.

```
  ┌──── Quick-Settings Bar ─────────────────────────────────────────────┐
  │  Brush Size: [...] 5  │  Image opacity: [────●────] 50  │ [✕]  ... │
  └──────────────────────────────────────────────────────────────────────┘
                                        ↑
                            drag to control transparency
```

| Opacity value | What you see |
|---|---|
| **100** (default) | Image fully visible — hexes are drawn on top of it |
| **50** | Image at half strength — good balance for tracing |
| **20–30** | Image barely visible — helpful for a subtle guide while painting |
| **0** | Image invisible (but still loaded — raise the slider to see it again) |

You can also adjust the slider with the **left and right arrow keys** on your keyboard for fine-grained control.

---

**Recommended workflow — tracing a reference map:**

This is the most common use case. Say you have an image of the real-world geography you want to reproduce as a campaign map.

```
  ┌──────────────────────────────────────────────────────────────┐
  │  STEP-BY-STEP: Trace a reference image                       │
  │                                                              │
  │  1. Press I → load your reference image                      │
  │  2. Set Image opacity to ~50 so hexes are still readable     │
  │  3. Set active layer to Ground Types (or whichever layer     │
  │     you want to start with)                                  │
  │  4. Select a Swatch (e.g. Grassland)                         │
  │  5. Press B for Brush and paint over the areas shown         │
  │     in the reference image                                   │
  │  6. Switch swatches and repeat for each terrain type         │
  │  7. When a layer is done, lower opacity further or           │
  │     press ✕ to clear the image and check your work          │
  └──────────────────────────────────────────────────────────────┘
```

**Tip — use a game export as your reference:** Go to **Tools > Export > Baseline Tilemap image** to export the current tilemap of an existing map, then load that export as a background image. This lets you paint a new map while precisely aligning it to the original game grid.

---

**Removing the background image:**

Click the **✕ (Remove background image)** button in the Quick-Settings Bar. The image is cleared immediately and the opacity slider resets to 100 and becomes greyed out until you load another image.

> The ✕ button is only clickable when an image is loaded — it is greyed out otherwise.

---

## 5. Triangle Warning — Roads, Rivers & Trade Routes

> **This section applies only when painting on the Roads, Rivers, or Trade Routes layers.**

```
  ╔═══════════════════════════════════════════════════════════════════════╗
  ║                                                                       ║
  ║   /!\  CRITICAL WARNING: DO NOT PAINT TRIANGLES                       ║
  ║                                                                       ║
  ║   When painting Roads, Rivers, or Trade Routes, never connect         ║
  ║   three hexes so that all three are directly adjacent to each other.  ║
  ║   This creates a closed three-hex loop that can cause INFINITE        ║
  ║   PROCESSING during export and UNPREDICTABLE BEHAVIOUR in-game.       ║
  ║                                                                       ║
  ╚═══════════════════════════════════════════════════════════════════════╝
```

### What is a triangle?

A triangle occurs when three painted hexes each share an edge with the other two, forming a closed loop. Every hex is a neighbour of the other two — there is no dead end, only an endless circle.

```
  AVOID — Triangle (closed loop):        SAFE — Open junction (T-shape):

           [A]                                    [A]
          /   \                                    |
        [B] — [C]                           [B] — [C]

   A→B, B→C, and A→C are all          C→B and A→C exist,
   connected. This closes a loop.      but A→B does NOT.
   There is no exit point.             The path has a dead end at A.
```

The left example is dangerous: starting at A, you can travel to B, then C, then back to A — forever. The right example is safe: a traveller reaching A from C has nowhere to go but back.

### Why is this dangerous?

When you export Roads, Rivers, or Trade Routes via the **Process** menu, the tool traces every painted path to build a network graph. A triangle creates a cycle with no exit, which can cause the tracer to loop endlessly — **hanging the export indefinitely**. Even if the export somehow completes, the game engine may misread the looped network and produce broken road visuals, invisible rivers, or non-functional trade routes in-game.

### How to spot a triangle before it happens

When your brush or line is about to connect two hexes that are already linked to each other through a third painted hex, you are about to close a triangle. A simple check:

```
  Before adding hex [X], ask:
  "Are any two of [X]'s painted neighbours also connected to each other?"

  Example — adding [X] would create a triangle:

      Already painted:    [A] — [B]

      About to add:       [X] adjacent to both [A] and [B]
                          → [X]–[A]–[B]–[X] = closed loop

      Do NOT paint [X] here. Route around it instead.
```

### How to fix an accidental triangle

1. Press **Ctrl+Z** immediately to undo the last stroke. Keep pressing until the closed loop is broken.
2. If the triangle spans several old strokes that you do not want to fully undo, activate the **Eraser (E)** and remove just the one hex that closes the loop — any one of the three connecting hexes is enough to break the cycle.

```
  Step 1: Spot the triangle         Step 2: Erase one connection

         [A]                                [A]
        /   \            →                   |
      [B] — [C]                        [B]   [C]

                               Erased A–B. The loop is broken.
                               A is now a dead end — safe to export.
```

> **Rule of thumb:** At any fork or crossing in your Roads, Rivers, or Trade Routes painting, no more than two of the hexes surrounding a junction should be painted AND touching each other. If you see three painted hexes that are all neighbours of each other, erase one of the three connections.

---

## 6. Adjusting Brush Size

The Brush and Eraser tools both have a size setting that controls how many hexes they cover in one click. A larger brush covers more hexes at once.

**Two ways to change the brush size:**

**Option A — Quick-Settings Bar slider** (fastest):

```
  ┌──── Quick-Settings Bar ─────────────────────────────┐
  │  Brush Size: [────●────] 5                          │
  └──────────────────────────────────────────────────────┘
```

1. Find the **Brush Size** slider in the Quick-Settings Bar at the top of the screen.
2. **Drag the slider** left to make the brush smaller, right to make it larger.
3. The number next to the slider shows the current size (range: **1 to 10**).

> The slider is only visible when a painting tool (Brush or Eraser) is active.

**Option B — Properties window** (for precise control):

1. Click the **Properties (⚙)** button at the bottom of the Toolbar.
   - The Properties button only becomes clickable when the Brush or Eraser is active.
2. A small **Brush Properties** window appears, always floating on top.

```
  ┌─ Brush Properties ─────────────────┐
  │                                    │
  │  Brush Size:                       │
  │  [1 ──────────────────────────10]  │
  │         ▲ drag slider              │
  │                                    │
  │  [ Apply ]          [ Cancel ]     │
  └────────────────────────────────────┘
```

3. **Drag the slider** to your desired size (1–10).
4. Click **Apply** to confirm, or **Cancel** to close without changes.

**Visual guide — what each size looks like on the hex grid:**

```
  Size 1      Size 2       Size 3          Size 4
  (1 hex)   (7 hexes)   (19 hexes)      (37 hexes)

    ●           ●           ●  ●           ●  ●  ●
              ● ● ●       ●  ●  ●        ●  ●  ●  ●
                ●         ●  ●  ●  ●    ●  ●  ●  ●  ●
                            ●  ●  ●      ●  ●  ●  ●
                               ●           ●  ●  ●
```

> A circular cursor on screen always shows you the exact area that will be painted before you click.

---

## 7. Managing Swatches (Create, Rename, Remove)

The **Actions** section in the Sidebar lets you add, rename, and remove Swatches. The buttons that appear depend on which Layer is active.

```
 ┌─ Actions ──────────────────────────────┐
 │                                        │
 │   [ New ]   [ Rename ]   [ Remove ]    │
 │                                        │
 └────────────────────────────────────────┘
```

> **Which layers allow custom Swatches?** Regions, Ground Types, Attritions, Climates, and Areas of Interest. Layers like Rivers, Roads, and Bridges use fixed on/off swatches and do not support creating new ones.

---

### Creating a New Swatch

1. Make sure the correct layer is **active** (e.g. *Regions*).
2. Click the **New** button in the Actions section.
3. A **Create New Swatch** window appears.

```
  ┌─ Create New Region ────────────────────────────────┐
  │                                                    │
  │  Name:  [ ________________________ ]               │
  │                                                    │
  │  Is sea?   ●  Yes    ○  No                         │
  │                                                    │
  │              [ Confirm ]   [ Cancel ]              │
  └────────────────────────────────────────────────────┘
```

4. Type a **name** for your new Swatch (e.g. *"Gaul"* or *"Egyptian Desert"*).
5. If the layer supports it (e.g. *Regions*), toggle **Is sea? Yes/No** to indicate whether this Swatch represents a sea or land area. The application will automatically pick an appropriate colour.
6. Click **Confirm** to create the Swatch. It will appear in the Swatch dropdown and is ready to paint with immediately.

---

### Renaming a Swatch

1. Select the Swatch you want to rename from the **Swatch dropdown**.
2. Click **Rename** in the Actions section.
3. A **Rename Swatch** window appears.

```
  ┌─ Rename Region ────────────────────────────────────┐
  │                                                    │
  │  Old name:  [ Gaul              ]  (read-only)    │
  │  New name:  [ _________________ ]                  │
  │                                                    │
  │              [ Confirm ]   [ Cancel ]              │
  └────────────────────────────────────────────────────┘
```

4. The **Old name** field shows the current name and cannot be edited.
5. Type the new name in the **New name** field.
6. Click **Confirm**. The Swatch is renamed everywhere — any hexes already painted with it are unaffected.

---

### Removing a Swatch

1. Select the Swatch you want to delete from the **Swatch dropdown**.
2. Click **Remove** in the Actions section.
3. The Swatch is removed from the list.

> **Caution:** Removing a Swatch does not automatically clear the hexes that were painted with it. Use the **Eraser** or paint over those hexes with a different Swatch before removing one, or use **Cleanup** (if available for the active layer) to tidy up unused entries automatically.

---

### Changing a Swatch's Colour

On supported layers, you can change the display colour of an existing Swatch:

1. Select the Swatch from the dropdown.
2. Click the **Change Colour** button (in the Actions section, when available).
3. A **colour picker** window appears.

```
  ┌─ Change Colour ────────────────────────────────────┐
  │                                                    │
  │  New colour:                                       │
  │  ┌──────────────────────────────────────────────┐  │
  │  │           [colour picker control]            │  │
  │  └──────────────────────────────────────────────┘  │
  │                                                    │
  │              [ Confirm ]   [ Cancel ]              │
  └────────────────────────────────────────────────────┘
```

4. Pick the desired colour and click **Confirm**.

---

## 8. Undoing and Redoing Painting Actions

Every stroke, flood fill, line, or erase operation is recorded and can be stepped back freely.

| Action | Shortcut | What It Does |
|---|---|---|
| **Undo** | **Ctrl+Z** | Reverses the last painting action and restores the hexes to their previous state |
| **Redo** | **Ctrl+Y** | Re-applies an action you have just undone |

> Each complete mouse gesture (press → drag → release) counts as one undo step. A single long drag across 50 hexes can be undone in one press of **Ctrl+Z**.

---

## 9. Pro-Tips & Troubleshooting

- **If a Roads, Rivers, or Trade Routes export hangs or freezes**, there are two common causes. The most likely one — especially for Roads — is an **infinite segment loop**: any closed ring of painted hexes that has no dead-end endpoint, regardless of size. A ring road that loops back on itself with no branch terminating elsewhere is a classic example. The export's segmentation pass traces paths between endpoints; if it finds a ring with no endpoints at all, it cycles forever. The second cause is a **triangle** (three mutually adjacent hexes all painted, forming the smallest possible closed loop — see [Section 5](#5-triangle-warning--roads-rivers--trade-routes)). A triangle is itself a type of infinite loop, just the smallest possible one. To fix either: cancel the export, trace along the painted path until you find where it closes back on itself, and use the **Eraser (E)** to cut the ring at any one point — creating a dead end that lets the export complete.

- **If you are painting but nothing is appearing on the map**, the most common cause is that the active layer is **hidden**. Check the **Layers** panel in the Sidebar and make sure the **checkbox (☑)** next to your active layer is ticked. A layer must be visible for painting to work. If it is already visible and still not responding, verify that the **radio button (●)** marks the correct layer as active.

- **If your Flood Fill spreads into the wrong area or stops too early**, check the **Source** dropdown in the Quick-Settings Bar. If Source is set to a different layer than the one you are painting, the fill uses that other layer's shape as its boundary — which may not match what you expect. To fill based only on the layer you are painting, set the Source dropdown to match your active layer.

- **If you want to sample an existing colour and paint more of it**, avoid searching through the dropdown manually. Instead, hold **Alt** to temporarily activate the Color Picker, click any hex that has the Swatch you want, then release **Alt** to return to your brush. The sampled Swatch is now set and ready to paint.

- **If a Swatch name is rejected when creating or renaming**, it means a Swatch with that name already exists on the layer. Each Swatch must have a unique name — choose a different name and try again.

- **For painting long, straight roads or rivers**, the **Line tool (L)** is significantly faster than brushing freehand. Set your active layer (e.g. *Roads*), select the **Road** Swatch, press **L**, click your start hex, move to the destination and click again. You can chain multiple line segments end-to-end.

- **If the circular brush cursor seems too small or too large compared to the hexes on screen**, this is normal — the cursor automatically scales with your current zoom level to always show the correct coverage area. Zoom in for fine detail work with small brushes; zoom out to paint large areas quickly with a large brush.

- **If a background image fails to load**, check that the file is one of the supported formats (JPG, JPEG, BMP, TIFF, or PNG) and that the file is not corrupted or locked by another program. If the image loads but looks blurry or distorted, this is because the image resolution does not match your map's hex grid — the image is always stretched to fill the entire map area. For best alignment, export a reference image at the exact map resolution using **Tools > Export > Baseline Tilemap image** and use that as your background.

- **If you cannot tell your painted hexes apart from the background image**, adjust the **Image opacity** slider in the Quick-Settings Bar. Lowering it to around 30–40 makes the reference image fade back while your painting stays fully visible. If you need to temporarily hide the image to inspect your work clearly, drag the slider all the way to 0 — the image stays loaded and you can raise it again at any time without reloading the file.

---

*Guide version: 1.0 — Campaign AI Map Editor*
