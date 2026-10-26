# Status and roadmap

Last updated: October 26, 2026, after the `{Responsive}` switches.

## Phases

| Phase | Content | Status |
| --- | --- | --- |
| 0 | Spike: validate the risky parts | Partly done (see below). |
| 1 | OpenSilver core changes: `LayoutObserver` and the insertion fix | Done: commit `547adac2` on branch `feat/layout-observer`, then the `{Responsive}` hook (`OnResponsiveValueChanged`). No PR yet. |
| 2 | `MoveAndResize` (Smooth), grouping, exclusions, performance guards, diagnostics; the gallery shell | Done. The product owner tested it; the first round of feedback is addressed and verified in Chromium (see "Verified"). |
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
| core-observer | `LayoutObserver` and its call sites, including `{Responsive}` switches | Done |
| core-js | `createVisual` skips `data-os-foreign` children | Done |
| lib-api | Public API | `MoveAndResize`, `Changes`, `Skip`, `AnimationSettings` and the curves are done. `Appear`, `Disappear`, the `Item...` properties, `TextChange` and `MagicMove` are pending. |
| lib-causality | Origins, groups, areas, decisions, exclusions, guards | Done for `MoveAndResize`, including the rule that changes never cross `None`, and `{Responsive}` switches that animate during a resize. Pending: the drag-to-reorder exception, effects on virtualized items, and the rule that the element holding the mouse capture snaps (today everything snaps during a drag). |
| lib-render | Web Animations rendering | Moves, sizes, clips, shapes, interruptions, and the automatic wipe or fade of elements appearing inside an area are done. Effects with center compensation are pending (phase 3). |
| lib-appear | Appear, Disappear, ghosts, holds, items | Pending. Done early: the automatic reveal inside areas, and a hold that hides an element shown again inside an area until its layout pass. |
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
| Welcome | Built: the options explained, "Start with chapter 5". |
| 1. Your first animation | Coming soon (phase 3) |
| 2. Pick an effect | Coming soon (phase 3) |
| 3. When the others move | Coming soon (phase 3) |
| 4. Page intros | Coming soon (phase 3) |
| 5. Smooth moves and resizes | Built: Save button, badge, sidebar, ChildWindow (`ResizableDialog`, which stays centered), photo card, search box. |
| 6. Lists | Coming soon (phase 3) |
| 7. Everything inside | Built: grid from 2 columns to 1, settings form whose details wipe in, dashboard with the chart carved out by `None`. |
| 8. When the window or a panel is resized | Built: tiles with and without `AlsoOnWindowResize` (they use `ApplyTo=EverythingInside` on the WrapPanel until `ItemMoveAndResize` exists); a profile panel resized with a GridSplitter, whose content switches at 220 px with `{Responsive ElementName=ProfilePanel}`, also from code; three step cards that switch from a row to a column with the window, at 1100 px. |
| 9. Text that moves | Coming soon (phase 5) |
| 10. Bouncy and Wave | Built: bouncy tab underline with Bounciness and Duration sliders, a growing card with a Wave slider. |
| 11. Magic move | Coming soon (phase 7) |
| 12. From code | Built: `Animate.Changes` (default and 0.6 s), `Animate.Skip`, `AnimationSettings` (Speed, DefaultDuration, IsEnabled), reset when leaving. |
| 13. What never animates, and why | Built: scrolling, GridSplitter, Slider, typing, window resize (links to chapter 8, with `{Responsive}` as the exception), the 300-element budget, wrapping text, shapes (which switch size with `{Responsive}` at 960 px). |
| 14. Putting it together | Coming soon (after the Showcase port) |

Not built yet in the chapters: the playgrounds, the live XAML with Copy, and the summary sentences, which come with phase 4. Each demo has a "What should happen" expander (a chevron, closed by default) with its acceptance lines.

**The shell is responsive**, with the breakpoints set in `App` (`ResponsiveThreshold.Default = new ResponsiveThreshold(960, 1280)`):

- **Desktop** (1280 px and wider): the chapters on the left, the options in the top bar, and the log on the right.
- **Tablet** (960 to 1279 px): the log becomes a drawer, opened with the Log button of the top bar. A dot on the button says that new messages arrived.
- **Phone** (below 960 px): the chapters and the options become a drawer too, opened with the menu button. The chapters use smaller margins, and their demos fit the screen (tested at 390 px).

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

### After the first round of feedback

Checked in the Debug build:

- **Shell.**
  - Show what animates is off by default.
  - From the root URL, "Start with chapter 5" opens a page that has its scroll bar and scrolls.
  - The "What should happen" chevron points right when closed and down when open.
- **Chapter 5.**
  - The dark sidebar narrows with the content next to it (about 250 ms) instead of snapping.
  - While the dialog narrows, its buttons and text box are no longer cut by the final size.
  - The dialog stays centered while it widens and narrows: 650 to 950 px becomes 500 to 1100 px in a 1600 px window. Before, it kept its left edge, which is what a ChildWindow does by itself.
- **Chapter 7.**
  - The details of an option wipe in from the top, at their final place, and the options below move down with the same timing. Turning on the first option and then the third gives the same result.
  - "Leave the chart out" works on every Refresh: the bars jump, the numbers resize smoothly, and "nothing animated because the change started inside Chart" is logged each time. With the switch off, the bars glide.
- **The nearest setting wins**, checked with a temporary `Bouncy` on the Visitors card of the chapter 7 dashboard. When Refresh resized that card, the log said "VisitorsText changed size; 12 elements animate together (Bouncy, 0.5 s, from the MoveAndResize of VisitorsCard)". When it didn't, the dashboard's Smooth applied. The messages of chapters 5, 7 and 10 stayed the same, since no demo has a `ThisElement` setting inside an area.
- **Responsive shell**, with emulated sizes: 390 × 844 (phone, touch), 959, 960 and 961 (the phone/tablet boundary), 1100 (tablet), and 1600 (desktop).
  - The menu drawer, with the options and the chapters, closes when a chapter is chosen or the dimmed page is tapped.
  - The log drawer opens from the Log button and closes with Close or a tap on the dimmed page. Its unread dot shows when a message arrives while it's closed.
  - Resizing with a drawer open goes to the right layout: the menu closes above 960 px, and the log returns beside the page at 1280 px.
  - At 390 px nothing is wider than the window in chapters 2, 5, 7, 8, 10, 12 and 13, and their demos still animate (sidebar 150 to 64 px, search box, dialog at 12 px from each side, settings form, dashboard, boxes of chapters 12 and 13).

### `{Responsive}` switches

Checked in the Debug build, at 1300 × 800 unless stated otherwise:

- **Chapter 8, the profile panel**, dragged from 170 to 280 px with synthetic pointer events (see development.md).
  - The edge and the rest of the page follow the pointer in the same frame. Animations that were running when the drag started are cancelled.
  - At 220 px, the content glides into its new layout in about 14 frames while the drag goes on (the name moves 60 px right and 51 px up), then follows the edge again. The log says "{Responsive} switched the Orientation of StackPanel; 17 elements animate together (Smooth, 0.25 s, from the EverythingInside of ProfilePanel)."
  - "Switch the layout from code" glides in 0.25 s.
- **Chapter 8, the step cards**: crossing 1100 px both ways, the row becomes a column and back with an animation, which goes on through the next resize steps. The log says "...3 elements animate together (Smooth, 0.25 s, from the MoveAndResize of StackPanel)." Crossing 1280 px, where only the shell switches, writes no message.
- **Chapter 13, the shapes**, crossing 960 px both ways between 940 and 980 px, where the shell switches to the phone layout too (73 switches in one pass): the three boxes resize between 50 and 80 px and their gaps change, in 0.25 s. The rest of the page follows the window at once.
- **Start time.** That pass takes about 0.4 s and ends late in a frame, so before the fix of `startAtNextFrame` the shapes' animations ended before they were painted. Clicks and reveals still play in full with it: the chapter 8 switch button, and the settings form of chapter 7, both ways.

## Not verified yet

- The GridSplitter drag in chapter 13, and drags by hand in general. Synthetic pointer events start a GridSplitter's capture only with `setPointerCapture` patched, as in development.md.
- The Simulator, by hand.
- Safari and Firefox: SVG geometry properties, the `linear()` fallback, `composite: 'add'`.
- Touch devices: the drag tracker relies on `pointercancel` when the browser starts scrolling.
- The gallery on a real phone and tablet. The phone layout was only checked with an emulated screen.
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
- **Elements that appear.** Inside an area with `ApplyTo=EverythingInside`, they wipe in from the side they push the others away from, or fade in when they push nothing. Elsewhere they appear in their place at once, and only the others move. Elements that disappear vanish at once. Effects of the app's own choosing, for appearing and disappearing, come with phase 3.
- **Switches of the same pass share one timing and one message.** The timing is the nearest setting found among them, and the message names the switch it came from. In a window resize the whole page is arranged again, so every switch of the page is in the same group: at 960 px in chapter 13, the message names AutoWidthBox, while the shapes animate too. With different settings, a switch would animate with the timing of another one.
- **One frame at the old size.** When a click makes an element Visible again, the core shows it in the DOM during the click itself (releasing the mouse capture runs the queued JavaScript), but arranges it only in the next layout pass, one dispatcher tick later. The browser often paints a frame in between, where the element has the box it had before it was collapsed and the elements around it haven't moved yet. Inside an area, the library hides it until that pass. Elsewhere, it needs a fix in the core: a pending layout pass should always run before the browser paints.
- **The Simulator:** JavaScript batches can arrive a frame apart.

## Decisions, and why

- **Names: `Animate`, `OpenSilver.Animations`, `AnimationSettings`.** "Animate" is understood by everyone, and it reads as an instruction that matches the "When it ..." sections. "Motion" was considered and rejected, because it would be less understood. "Reduce motion" keeps its name, because that's the operating-system setting.
- **Beginner vocabulary everywhere:** `From`/`To`, `StartAfter`, `Zoom`, `Bouncy`, `Cascade`, `Wave`, and one curve vocabulary (`Smooth`, `Gentle`, `Linear`, `Bouncy`). Messages are plain sentences that name elements by `x:Name`.
- **No compatibility layer.** The Showcase's own animation library will be removed, and the Showcase ported to this API (phase 3).
- **No XAML.io designer tab for now.** It's specified in design.md, and built later in the XAML.io repository.
- **The gallery is a progressive storytelling tour.** Chapters are added in the phase that builds their feature.
- **Minimal OpenSilver changes:** only hooks and the insertion fix, with no behavior change. The rest stays in the library.
- **Window resizes never animate by default.** Following a window edge with a delay feels laggy; elements opt in with `AlsoOnWindowResize`.
- **A `{Responsive}` switch is a change, like a click**, agreed with the product owner. It animates with the nearest setting, even while the window or a panel is resized; the rest of the resize still snaps. `AlsoOnWindowResize` keeps its name and meaning: following the resize itself.
- **`ThisElement` means the element's own box.** Its setting gives the timing to changes that move or resize it, including changes that start inside it, like a longer label. An ancestor that stays in place doesn't make changes inside it animate. That's what `EverythingInside` is for.
- **The nearest setting wins**, agreed with the product owner after the first round of feedback. Going up from where a change starts, the first `EverythingInside`, or the first other setting on an element that moves or resizes, gives the timing. Before, an area won over a nearer `ThisElement` setting, and the "Good to know" of chapter 7 needed two hard bullets to explain the difference.
- **Silent snaps** for scroll chrome and for the 500 ms after an element appears. They're consequences, not things the developer did.
- **Only the first measure of a pass makes an origin**, and a window-resize pass has no origins except `{Responsive}` switches. These rules came from testing: without them, a window resize animated or snapped the whole page.
- **Bouncy only bounces moves**; sizes change smoothly, so content doesn't wobble.
- **Changes never cross `None`.** A change inside an element with `MoveAndResize=None` doesn't go up to the elements around it, and a change outside doesn't go into it. So what the carved-out element pushes snaps with it, even when another change animates in the same pass.
- **Automatic reveals inside areas.** In an `EverythingInside` area, an element that appears takes part in the change: a wipe that keeps up with the elements it pushes reads better than an element that overlaps them until they have moved.
- **Show what animates is off by default**, in every chapter, as the product owner asked. The reader turns the outlines on when they want them; chapter 5 suggests it.

## Next steps

1. **The product owner tests the fixes of the first round of feedback**, the "nearest setting wins" rule, and the `{Responsive}` switches of chapters 8 and 13, by dragging the panel and resizing the window by hand.
2. **The OpenSilver PR.** Open the PR from `feat/layout-observer`, and get a published preview package. Then set the default `OpenSilverVersion` in `Directory.Build.props` to it, and use it in CI.
3. **Phase 3: Appear and Disappear**, specified in design.md under "Library API", "Appear and disappear mechanics" and "Items".
   - The core hooks it needs already exist: `OnVisibilityChanged`, `OnDetaching`, and the `data-os-foreign` insertion fix for ghosts.
   - New pieces:
     - effect presets (Fade, Slide, Zoom, Pop, Wipe, Blur, Custom) with center compensation;
     - ghosts cloned at detach or collapse;
     - the hold (a Storyboard on `Visibility`, applied synchronously). A simpler hold exists already, for elements shown again inside an area: `animations.js` keeps them at opacity 0 until the batch of their layout pass;
     - `OnPageOpen` and `OthersMove`;
     - the item properties, with containers paired by data item.
   - Gallery chapters 1 to 4 and 6. After that, the Showcase port and chapter 14.
4. **Phase 4:** `AnimationCatalog` and `AnimationDesign`, then the playgrounds, the live XAML and the summaries in every chapter.
5. **Phase 5** (text, chapter 9), then **phase 7** (Magic move, chapter 11).
