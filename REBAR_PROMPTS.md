# Rebar Placement Prompt Guide

Best prompts for placing reinforcement in Revit beams, columns, walls, and floors using the AI connector.

## Important: Always Start with Discovery

Before placing any rebar, the AI needs to understand the host element and available bar types. Always begin with these queries:

```
Get the rebar bar types available in this model
```
```
Get the rebar hook types available
```
```
Get the host rebar info for element [ID] — I need its dimensions, start/end points, and cover settings
```

---

## Beam Reinforcement

### Simple Beam — Longitudinal + Stirrups

```
I have a concrete beam (element ID 12345). Please reinforce it with:
- 4 x T16 bottom longitudinal bars with 25mm cover
- 2 x T12 top hanger bars
- T8 stirrups at 150mm spacing along the full beam length

First get the beam's geometry and cover settings using get_host_rebar_info,
then get available bar types and hook types,
then place the rebar calculating correct positions from the beam dimensions minus cover.
```

### Beam with Hooks

```
Add bottom reinforcement to beam [ID] using T20 bars with 135-degree hooks at both ends.
Place 3 bars at the bottom with 30mm side cover and 25mm bottom cover.
Distribute them evenly across the beam width.
```

### Beam Stirrup Zones

```
For beam [ID], place stirrups in three zones:
- End zones (first and last 1/4 of span): T10 stirrups at 100mm spacing
- Middle zone (center half of span): T10 stirrups at 200mm spacing
Use get_host_rebar_info first to calculate the zone lengths from the beam span.
```

---

## Column Reinforcement

### Standard Column

```
Reinforce column [ID] with:
- 8 x T20 longitudinal bars arranged around the perimeter with 40mm cover
- T10 ties at 200mm spacing along the column height

First check the column dimensions using get_host_rebar_info,
then calculate bar positions: 3 bars per face of a rectangular column,
minus cover on each side. Place ties as stirrups with the column cross-section
dimensions minus cover for width and height.
```

### Circular Column

```
Column [ID] is circular. Place 8 x T25 longitudinal bars evenly spaced
around the circumference at 50mm cover from the outer face.
Add T10 circular ties at 150mm spacing.
Start by getting the column geometry to find the diameter.
```

### Column with Close Spacing at Ends

```
For column [ID], use two tie spacing zones:
- Lap zone (bottom 600mm and top 600mm): T10 ties at 100mm centers
- Mid zone: T10 ties at 250mm centers
Get the column height first, then calculate zone boundaries.
```

---

## Wall Reinforcement

### Standard Wall — Horizontal + Vertical

```
Reinforce wall [ID] with:
- Horizontal bars: T12 at 200mm spacing on both faces (near and far face)
- Vertical bars: T12 at 200mm spacing on both faces
- Cover: 25mm on each face

Steps:
1. Get wall geometry (get_host_rebar_info) — I need wall length, height, and thickness
2. Get bar types (get_rebar_bar_types)
3. Place near-face horizontal bars (normal pointing inward from one face)
4. Place far-face horizontal bars (normal pointing inward from opposite face)
5. Place near-face vertical bars
6. Place far-face vertical bars
Use MaxSpacing layout rule with 200mm (0.656 ft) spacing.
```

### Shear Wall with Boundary Elements

```
Wall [ID] is a shear wall. Reinforce with:
- Standard zone (middle): T12 horizontal @ 200mm + T12 vertical @ 200mm on both faces
- Boundary elements (first and last 600mm of wall length):
  T16 vertical @ 100mm spacing + T10 ties at 150mm spacing
Get wall dimensions first to calculate boundary element extents.
```

---

## Floor / Slab Reinforcement

### Simple Slab — Two-Way

```
Reinforce floor slab [ID] with two-way reinforcement:
- Bottom layer 1 (X-direction): T12 at 150mm spacing
- Bottom layer 2 (Y-direction): T12 at 150mm spacing
- Top layer at supports: T12 at 150mm spacing (extend 1/4 span from each support)

Get the slab geometry first to determine span directions and dimensions.
Bottom layer 1 sits at the very bottom (cover distance up from soffit).
Bottom layer 2 sits on top of layer 1 (cover + bar diameter of layer 1).
```

### One-Way Slab

```
Floor slab [ID] spans in the X-direction. Place:
- Main bottom bars: T16 at 175mm spacing in X-direction
- Distribution bars: T10 at 300mm spacing in Y-direction (on top of main bars)
- Top bars over supports: T16 at 175mm spacing, extending 0.3 x span from each end
Calculate cover positions from slab thickness minus cover.
```

### Slab with Drop Panels

```
For flat slab [ID], reinforce with:
- Column strip bottom: T16 at 125mm both ways
- Middle strip bottom: T12 at 200mm both ways
- Column strip top: T20 at 125mm both ways (over column)
- Middle strip top: T12 at 250mm both ways
Start by getting slab geometry and identify column positions.
```

---

## Advanced Prompts

### Check Existing Rebar Before Adding

```
Show me what rebar is already placed in beam [ID] — bar types, quantities, and spacing.
Then tell me what's missing compared to a standard design with 4T20 bottom, 2T12 top, and T8 stirrups at 150.
```

### Copy Rebar Pattern to Similar Elements

```
Beam [ID1] has the correct rebar design. Get its rebar details.
Now place the same rebar pattern in beams [ID2], [ID3], and [ID4],
adjusting bar lengths for each beam's actual span.
```

### Modify Existing Rebar Spacing

```
Change the stirrup spacing in beam [ID] from 200mm to 150mm.
First get the existing rebar, find the stirrup set, then use set_rebar_layout
with MaxSpacing rule at 0.492 ft (150mm).
```

### Set Cover Before Placing Rebar

```
Set the rebar cover on beam [ID] to:
- Top cover: 25mm
- Bottom cover: 30mm
- Side cover: 25mm
First get available cover types with get_rebar_cover_types,
then apply the closest matching cover types.
```

---

## Unit Conversion Reference

When prompting, you can use mm — the AI will convert to feet:

| mm | feet |
|----|------|
| 8mm bar | T8 |
| 10mm bar | T10 |
| 12mm bar | T12 |
| 16mm bar | T16 |
| 20mm bar | T20 |
| 25mm bar | T25 |
| 32mm bar | T32 |
| 100mm spacing | 0.328 ft |
| 150mm spacing | 0.492 ft |
| 200mm spacing | 0.656 ft |
| 250mm spacing | 0.820 ft |
| 300mm spacing | 0.984 ft |
| 25mm cover | 0.082 ft |
| 30mm cover | 0.098 ft |
| 40mm cover | 0.131 ft |
| 50mm cover | 0.164 ft |

---

## Workflow Summary

```
Step 1: get_rebar_bar_types          → find bar type IDs for T8, T12, T16, etc.
Step 2: get_rebar_hook_types         → find hook IDs (if needed)
Step 3: get_host_rebar_info {hostId} → get host dimensions, cover, start/end points
Step 4: get_rebar_cover_types        → find/set cover (if needed)
Step 5: place_rebar / place_stirrups → place reinforcement with calculated positions
Step 6: set_rebar_layout             → adjust distribution if needed
Step 7: get_rebar_in_host            → verify placement
```
