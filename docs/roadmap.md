# Status and roadmap

Last updated: October 23, 2026, at the end of phase 2.

## Phases

| Phase | Content | Status |
| --- | --- | --- |
| 0 | Spike: validate the risky parts | Partly done (see below). |
| 1 | OpenSilver core changes: `LayoutObserver` and the insertion fix | Done on branch `feat/motion`, not committed, no PR yet. |
| 2 | `MoveAndResize` (Smooth), grouping, exclusions, performance guards, diagnostics; the gallery shell | Done. Verified in Chromium; waiting for the product owner's testing. |
| 3 | `Appear`/`Disappear`, `OnPageOpen`, `OthersMove`, the `Item...` properties; then the Showcase port | Not started. |
| 4 | `AnimationCatalog` and `AnimationDesign`, and the gallery playgrounds built on them | Not started. |
| 5 | Text effects (`TextChange`, Typewriter, ...) | Not started. |
| 6 | Bouncy and Wave | Done early, with phase 2. |
| 7 | Magic move | Not started. |
| Later | Hover, ContentTransition, custom curves for MoveAndResize | Not started. |

The XAML.io "Animate" tab is not part of these phases: it's built later in the XAML.io repository, from the "Designer panel" section of [design.md](design.md).

### The spike (phase 0)

| Item | Result |
| --- | --- |
| String presets through a `TypeConverter` on the value type | Works (`anim:Animate.MoveAndResize="Smooth"`). |
| Markup-extension parameters, including readable times | Works (`{anim:Smooth ApplyTo=EverythingInside, Wave=0.8, Duration=0.4s, AlsoOnWindowResize=True}`). |
| Presets as resources and in Style Setters | Not tested yet. |
| `composite: 'add'` on `translate` | Works in Chromium. |
| `clip-path: inset(0px)` over the core's clip | Works in Chromium (the collapsible sidebar has `ClipToBounds`). |
| SVG geometry CSS properties on `rect`/`ellipse` | Work in Chromium. Safari not tested; without them, the geometry snaps. |
| `linear()` and its fallback | `linear()` works in Chromium. The fallback (baked keyframes) is written but not tested. |
| Loading on the XAML.io design surface | Not tested. The engine doesn't start when `DesignerProperties.IsInDesignTool` is true. |

## To-do list of the plan

| Id | Content | Status |
| --- | --- | --- |
| spike | See above | In progress |
| core-observer | `LayoutObserver` and its call sites | Done |
| core-js | `createVisual` skips `data-os-foreign` children | Done |
| lib-api | Public API | `MoveAndResize`, `Changes`, `Skip`, `AnimationSettings` and the curves are done. `Appear`, `Disappear`, the `Item...` properties, `TextChange` and `MagicMove` are pending. |
| lib-causality | Origins, groups, areas, decisions, exclusions, guards | Done for `MoveAndResize`. Pending: the drag-to-reorder exception, effects on virtualized items, and the rule that the element holding the mouse capture snaps (today everything snaps during a drag). |
| lib-render | Web Animations rendering | Moves, sizes, clips, shapes and interruptions are done. Effects with center compensation are pending (phase 3). |
| lib-appear | Appear, Disappear, ghosts, holds, items | Pending |
| lib-diagnostics | `ShowWhatAnimates`, `Logged` | Done |
| designer-metadata | `AnimationCatalog`, `AnimationDesign` | Pending |
| gallery-app | The guided tour | In progress (see below) |
| showcase-port | The Showcase on the new API | Pending |
| lib-text | Text effects | Pending |
| lib-bouncy | Bouncy and Wave | Done |
| lib-magicmove | Magic move | Pending |
| later | Hover, ContentTransition | Pending |

## The gallery chapters

| Chapter | Status |
| --- | --- |
| Welcome | Built: the toolbar explained, "Start with chapter 5". |
| 1. Your first animation | Coming soon (phase 3) |
| 2. Pick an effect | Coming soon (phase 3) |
| 3. When the others move | Coming soon (phase 3) |
| 4. Page intros | Coming soon (phase 3) |
| 5. Smooth moves and resizes | Built: Save button, badge, sidebar, ChildWindow (`ResizableDialog`), photo card, search box. Show what animates is turned on the first time. |
| 6. Lists | Coming soon (phase 3) |
| 7. Everything inside | Built: responsive grid (2 columns to 1), settings form, dashboard with the chart carved out by `None`. |
| 8. Following the browser window | Built: tiles with and without `AlsoOnWindowResize`. Uses `ApplyTo=EverythingInside` on the WrapPanel until `ItemMoveAndResize` exists. |
| 9. Text that moves | Coming soon (phase 5) |
| 10. Bouncy and Wave | Built: bouncy tab underline with Bounciness and Duration sliders, a growing card with a Wave slider. |
| 11. Magic move | Coming soon (phase 7) |
| 12. From code | Built: `Animate.Changes` (default and 0.6 s), `Animate.Skip`, `AnimationSettings` (Speed, DefaultDuration, IsEnabled), reset when leaving. |
| 13. What never animates, and why | Built: scrolling, GridSplitter, Slider, typing, window resize (links to chapter 8), the 300-element budget, wrapping text, shapes. |
| 14. Putting it together | Coming soon (after the Showcase port) |

Not built yet in the chapters: the playgrounds, the live XAML with Copy, and the summary sentences, which come with phase 4. Chapter pages have a "What should happen" switch with their acceptance lines.

## Verified

This was checked in Chromium, through the agent's browser, with the Debug build unless stated otherwise:

- **Chapter 5.**
  - The dialog widens and narrows (300 to 600). Interrupting it is continuous: the second animation starts from 556.7 px, where the first one was.
  - After the review, in the Release build: the Save button (10 elements, timing from SaveButton) and the badge.
- **Chapter 7:** the column switch, the settings form, and the carve-out.
- **Chapter 8.**
  - With the window going from 1600 to 1350 px wide, only `FollowingTiles` and its tiles animate (12 animations), and the message is logged.
  - The rest of the page snaps, with no stray messages.
- **Chapter 10.**
  - Bouncy overshoots by 15 px at Bounciness 0.5, and by 54 px at 1.
  - The wave delays range from 2 to 160 ms.
- **Chapter 12.**
  - Direct changes jump; `Animate.Changes` glides in 0.25 s, or 0.6 s with a duration.
  - `Skip` logs its message.
  - Speed 0.5 makes it last about 0.48 s, and the log says "0.5 s"; a duration of 0.3 with speed 0.5 lasts 0.6 s.
  - With IsEnabled off, it snaps with a message.
  - Reset works, and the settings are reset when leaving the chapter.
- **Chapter 13.**
  - Scrolling stays instant. Taller rows animate (37 elements).
  - A click on the slider track glides; a slider drag (tested once) snaps with "something is being dragged".
  - Typing snaps with "the change came from typing in AutoWidthBox"; "Add a word from code" glides.
  - 100 tiles grow smoothly (116 elements). With 400 tiles it snaps with "more than 300 elements would animate at once".
  - Wrapping text and the shapes animate (rect and ellipse geometry).
- **Build:** Release build of the solution with 0 errors. The package `OpenSilver.Animations.0.1.0.nupkg` (44 KB) packs.

## Not verified yet

- The GridSplitter drag in chapter 13. Synthetic pointer events never start its capture, so it needs a check by hand.
- The Simulator, by hand.
- Safari and Firefox: SVG geometry properties, the `linear()` fallback, `composite: 'add'`.
- Touch devices: the drag tracker relies on `pointercancel` when the browser starts scrolling.
- Presets declared as resources or in Style Setters.
- The XAML.io design surface.
- After the review on October 23, only chapter 5 was re-run in the browser. The review changes are:
  - `ThisElement` ancestors must have moved to give their timing;
  - nested scopes;
  - `Skip` starts the engine;
  - roots positioned by the browser are never animated.

## Known limitations of what's built

- **Wrapping text** wraps at the current box width during a width animation, so words hop between lines. Chapter 13 shows it.
- **`Path` geometry** snaps inside an animated box, and so do Rectangle and Ellipse on browsers without the SVG geometry CSS properties.
- **Clips** other than the element's own box snap to their final value.
- **App code** that positions elements from `ActualWidth` or `SizeChanged` jumps, because those report final values at once.
- **The `RenderTransformOrigin` offset** snaps.
- **Storyboards** that animate layout properties of animated elements are smoothed twice.
- **Elements shown for the first time** appear in their place and only the others move. Effects come with phase 3.
- **The Simulator:** JavaScript batches can arrive a frame apart.

## Decisions, and why

- **Names: `Animate`, `OpenSilver.Animations`, `AnimationSettings`.** "Animate" is understood by everyone, and it reads as an instruction that matches the "When it ..." sections. "Motion" was considered and rejected, because it would be less understood. "Reduce motion" keeps its name, because that's the operating-system setting.
- **Beginner vocabulary everywhere:** `From`/`To`, `StartAfter`, `Zoom`, `Bouncy`, `Cascade`, `Wave`, and one curve vocabulary (`Smooth`, `Gentle`, `Linear`, `Bouncy`). Messages are plain sentences that name elements by `x:Name`.
- **No compatibility layer.** The Showcase's own animation library will be removed, and the Showcase ported to this API (phase 3).
- **No XAML.io designer tab for now.** It's specified in design.md, and built later in the XAML.io repository.
- **The gallery is a progressive storytelling tour.** Chapters are added in the phase that builds their feature.
- **Minimal OpenSilver changes:** only hooks and the insertion fix, with no behavior change. The rest stays in the library.
- **Window resizes never animate by default.** Following a window edge with a delay feels laggy; elements opt in with `AlsoOnWindowResize`.
- **`ThisElement` means the element's own box.** Its setting gives the timing to changes that move or resize it, including changes that start inside it, like a longer label. An ancestor that stays in place doesn't make changes inside it animate. That's what `EverythingInside` is for.
- **Silent snaps** for scroll chrome and for the 500 ms after an element appears. They're consequences, not things the developer did.
- **Only the first measure of a pass makes an origin**, and a window-resize pass has no origins. These rules came from testing: without them, a window resize animated or snapped the whole page.
- **Bouncy only bounces moves**; sizes change smoothly, so content doesn't wobble.

## Next steps

1. **The product owner tests phase 2.** Then fix what comes out of it.
2. **The OpenSilver PR.** Commit `feat/motion` (the product owner commits), open the PR, and get a published preview package. Then set the default `OpenSilverVersion` in `Directory.Build.props` to it, and use it in CI.
3. **Phase 3: Appear and Disappear**, specified in design.md under "Library API", "Appear and disappear mechanics" and "Items".
   - The core hooks it needs already exist: `OnVisibilityChanged`, `OnDetaching`, and the `data-os-foreign` insertion fix for ghosts.
   - New pieces:
     - effect presets (Fade, Slide, Zoom, Pop, Wipe, Blur, Custom) with center compensation;
     - ghosts cloned at detach or collapse;
     - the hold (a Storyboard on `Visibility`, applied synchronously);
     - `OnPageOpen` and `OthersMove`;
     - the item properties, with containers paired by data item.
   - Gallery chapters 1 to 4 and 6. After that, the Showcase port and chapter 14.
4. **Phase 4:** `AnimationCatalog` and `AnimationDesign`, then the playgrounds, the live XAML and the summaries in every chapter.
5. **Phase 5** (text, chapter 9), then **phase 7** (Magic move, chapter 11).
