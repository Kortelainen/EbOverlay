# EbOverlay

A lightweight, always-on-top Windows desktop overlay inspired by the aesthetic of Cowboy Bebop's Ed.
Ambient system information and a hand-drawn sprite companion — present but not intrusive.

---

## Philosophy

> "Ambient intelligence" — the overlay should feel like the OS *has a soul*, not like a widget app running on top of it.

Always running. Low noise. Low resource use. Reacts to the system, not the user.

---

## Display Zones

```
┌─────────────────────────────────────────────────────────────┐
│  [TOP-LEFT]              [TOP-CENTER]            [TOP-RIGHT] │
│  Active App Name         —                       Clock       │
│                                                              │
│                                                              │
│                                                  [MID-RIGHT] │
│                                                  Sprite zone │
│                                                              │
│                                                              │
│  [BOT-LEFT]                                  [BOT-RIGHT]    │
│  Net ↑↓ metrics          —                   CPU / RAM bar  │
└─────────────────────────────────────────────────────────────┘
```

All zones are sparse — anchored at screen edges, not filled.

---

## Feature Set

### 1. Active App Display (top-left)
- Hook `SetWinEventHook` → `EVENT_SYSTEM_FOREGROUND`
- Show focused app name in monospace, Ed-aesthetic font
- Fade-in on change, hold ~4s, fade-out
- Optional: show small app icon glyph

### 2. Clock (top-right)
- Minimal `HH:MM` or `HH:MM:SS`
- Optional glitch frame on minute rollover

### 3. System Metrics (bottom-right)
- CPU % and RAM used/total
- Animated bar fill, polled every 2s via `PerformanceCounter`

### 4. Network Metrics (bottom-left)
- Upload ↑ / Download ↓ in KB/s or MB/s
- Hides when idle, appears when traffic detected
- Polled via `NetworkInterface.GetAllNetworkInterfaces()`

### 5. Sprite / Character Zone (mid-right)
- Hand-drawn sprite sheet (provided separately)
- States: **idle** (slow blink/breathe), **active** (CPU spike or app switch), **sleep** (after 10min no input)
- Rendered via `WriteableBitmap`, frame-stepped — intentional low FPS (4–8) for pixel art feel

### 6. Ambient Scanline Layer (optional)
- ~3–5% opacity animated scanline across full screen
- Gives the desktop a subtle "CRT screen" feel
- Toggleable in config — first to cut if too distracting

---

## Window-Aware Sprite (M7)

Instead of being pinned to a fixed screen corner, the sprite floats along the edges of the active foreground window — like Ed crawling around the frame of whatever you're working in.

### How it tracks the window

- `GetWindowRect(hwnd)` returns the exact pixel rect of the foreground window
- `SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE)` fires in real time when the window moves or resizes — no polling needed
- The sprite's canvas position is recalculated from the window rect on every location event

### Movement path

The sprite walks a clockwise (or random-wandering) path along the four edges of the window rect:

```
  ┌──────────────────────────────┐
  │  top edge →→→→→→→→→→→→→→→→  │
  │                           ↓  │
  │  ←←←←←←←←←←←←←←← bot edge  │
  ↑                              │
  │                   right edge ↓
```

Speed and direction can react to system events (CPU spike = frantic scurry, idle = slow drift).

### Simulating "behind the window" — edge clipping

The overlay always sits above other windows, so the sprite cannot truly go behind. Instead a `RectangleGeometry` clip is applied to the sprite that shrinks as the sprite crosses the window border, making it appear to slide underneath the frame:

```
  Window border
       │
  ████░░    ← sprite half-clipped, looks like it's behind the chrome
       │
```

As the sprite rounds a corner and "emerges" on the other side, the clip expands back to full. The effect is convincing at normal sprite sizes (64–96px).

### Window drag interaction

If the user drags the window while the sprite is on it, the sprite moves with the window in real time (same location event). Optional: add a small "lag" lerp so the sprite appears to be pulled along rather than snapping.

### Fallback

When no window is focused (desktop, fullscreen, etc.) the sprite returns to its default pinned corner and idles there until a window is foregrounded again.

### Technical notes

- Uses `WinEventHook` with `EVENT_OBJECT_LOCATIONCHANGE` + `EVENT_SYSTEM_FOREGROUND` — no polling
- Clip geometry: WPF `UIElement.Clip` with a `RectangleGeometry` updated on each sprite position tick
- Path walking: simple parametric position along perimeter (0.0–4.0 maps to 4 edges), updated by a `DispatcherTimer` at 30 FPS
- Two-layer overlay (true z-order behind window) is possible but deferred — clipping achieves 90% of the visual at 10% of the complexity

---

## Keyboard Control — ALT Layer

Holding **ALT** reveals a translucent hint overlay showing what each numpad key does in the current context. Release ALT to dismiss it. No ALT held = overlay is fully passive and click-through.

### Numpad = Screen Grid

The numpad layout maps 1:1 to screen regions:

```
┌─────────────────────────────────────────────────────────────┐
│  [7] top-left            [8] top-center        [9] top-right │
│                                                              │
│  [4] mid-left            [5] CENTER            [6] mid-right │
│                                                              │
│  [1] bot-left            [2] bot-center        [3] bot-right │
└─────────────────────────────────────────────────────────────┘

  Numpad:       7  8  9
                4  5  6
                1  2  3
```

### Bindings

| Key | Zone | Action |
|-----|------|--------|
| ALT+1 | Bottom-left | Cycle views: Net ↑↓ → IP info → hidden |
| ALT+2 | Bottom-center | Cycle views: (reserved / future) |
| ALT+3 | Bottom-right | Cycle views: CPU+RAM → CPU only → RAM only → hidden |
| ALT+4 | Mid-left | Cycle views: (reserved / future) |
| ALT+5 | Global | Toggle show/hide entire overlay |
| ALT+6 | Mid-right | Toggle show/hide sprite |
| ALT+7 | Top-left | Cycle views: App name → App name + icon → hidden |
| ALT+8 | Top-center | Cycle views: (reserved / future) |
| ALT+9 | Top-right | Cycle views: Clock HH:MM → HH:MM:SS → hidden |

### ALT Hint Overlay

While ALT is held, a minimal dim panel fades in showing:
- Which zones are currently visible (lit) vs hidden (dim)
- The key number anchored near each screen corner it controls
- Disappears instantly on ALT release — no lingering chrome

### Design Notes

- ALT is chosen because it rarely conflicts with foreground apps (most apps don't use bare ALT+numpad)
- "Cycle" means each press steps through the mode list in order, wrapping around
- `settings.json` persists the last selected mode per zone across restarts
- Reserved zones (2, 4, 8) are intentional — space for future views without redesigning the scheme

---

## Behavioral Rules

| Trigger | Response |
|---------|----------|
| App focus changes | App name fades in, sprite perks up (1 cycle) |
| CPU > 70% | Sprite switches to active state |
| No input > 10 min | Sprite enters sleep, metrics dim to 30% opacity |
| Input resumes | Everything fades back in |
| Fullscreen app detected | Overlay hides entirely |
| Network burst > 1 MB/s | Net indicator highlights briefly |

---

## Performance Budget

| Component | CPU (idle) | RAM |
|-----------|-----------|-----|
| Overlay process total | < 0.5% | < 60 MB |
| Sprite animation | < 0.1% | — |
| Metrics polling | < 0.1% | — |
| Render loop | 30 FPS cap, drops to 10 FPS when idle | — |

---

## Tech Stack

- **Language**: C# (.NET 8)
- **UI**: WPF
- **Rendering**: `WriteableBitmap` for sprites, WPF canvas for text/bars
- **System hooks**: Win32 `SetWinEventHook`, `PerformanceCounter`, `NetworkInterface`
- **Transparency**: `WS_EX_LAYERED` + `WS_EX_TRANSPARENT` (click-through)

---

## Project Structure

```
EbOverlay/
├── README.md
└── src/
    └── EbOverlay/
        ├── App.xaml
        ├── App.xaml.cs
        ├── OverlayWindow.xaml
        ├── OverlayWindow.xaml.cs
        ├── Zones/
        │   ├── AppNameZone.cs       # M2
        │   ├── ClockZone.cs         # M2
        │   ├── MetricsZone.cs       # M3
        │   └── SpriteZone.cs        # M4
        ├── Hooks/
        │   ├── WindowHook.cs        # M2 — SetWinEventHook wrapper
        │   ├── FullscreenDetector.cs # M1
        │   ├── KeyboardHook.cs      # M8 — global low-level keyboard hook
        │   └── WindowLocationHook.cs # M7 — EVENT_OBJECT_LOCATIONCHANGE watcher
        ├── Services/
        │   ├── SystemMetrics.cs     # M3
        │   ├── NetworkMetrics.cs    # M3
        │   ├── SpritePathController.cs # M7 — perimeter walking + clip geometry
        │   └── ZoneStateManager.cs  # M8 — tracks mode per zone, persists to settings.json
        ├── Sprites/                 # Drop sprite sheets here
        └── Config/
            └── settings.json        # Opacity, zones on/off, poll rates
```

---

## Milestones

| # | Goal | Status |
|---|------|--------|
| M1 | Transparent click-through window, always on top, hides on fullscreen | ✅ Done |
| M2 | App name hook + clock rendering | ✅ Done |
| M3 | System + network metrics live | ⬜ |
| M4 | Sprite zone with idle animation | ⬜ |
| M5 | Behavioral states wired to triggers | ⬜ |
| M6 | Scanline layer + config file | ⬜ |
| M7 | Window-aware sprite — floats around active window edges, clips at border | ⬜ |
| M8 | ALT layer — hint overlay + numpad zone cycling | ⬜ |

---

## Running

```bash
cd src/EbOverlay
dotnet run
```

Requires .NET 8 SDK. Targets Windows only (`net8.0-windows`).
