# Status and roadmap

Last updated: October 28, 2026, after chapter 15, Putting it together.

## Phases


| Phase | Content                                                                                            | Status                                                                                                                          |
| ----- | -------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| 0     | Spike: validate the risky parts                                                                    | Partly done (see below).                                                                                                        |
| 1     | OpenSilver core changes: `LayoutObserver` and the insertion fix                                    | Done: commit `547adac2` on branch `feat/layout-observer`, then the `{Responsive}` hook (`OnResponsiveValueChanged`), then `LayoutObserver` made internal, with `InternalsVisibleTo("OpenSilver.Animations")`, and its unused parts removed. No PR yet. |
| 2     | `MoveAndResize` (Smooth), grouping, exclusions, performance guards, diagnostics; the gallery shell | Done. The product owner tested it; the first round of feedback is addressed and verified in Chromium (see "Verified").          |
| 3     | `Appear`/`Disappear`, `OnPageOpen`, `OthersMove`, the `Item...` properties, `Hover` and `Press`; then the Showcase port | Built, in four steps, then the port and chapter 15; the product owner reviews them. Step 1 is done: `Appear`/`Disappear` with their presets, `OthersMove`, the automatic disappear inside areas, and chapters 1 to 3. Step 2 is done: `StartAfter` and `OnPageOpen`, and chapter 4. Step 3 is done: `ItemAppear`, `ItemDisappear`, `ItemMoveAndResize` and `ItemCascade`, chapter 6, and the chapter 8 tiles. Step 4 is done: `Hover` and `Press` with `Grow`, `Lift`, `Shrink` and `Nudge`, and chapter 12. The Showcase port is done, not committed yet, with what it needed to look as before: the classic curves and `Overshoot`, `OthersDuration`, `Fade=False`, distances in percent, `Zoom` sizes, and elements that stay where they end. Chapter 15 is built: a small copy of the Showcase's window, and how each of its animations is made. |
| 4     | `AnimationCatalog` and `AnimationDesign`, and the gallery playgrounds built on them                | Not started.                                                                                                                    |
| 5     | Text effects (`TextChange`, Typewriter, ...)                                                       | Not started.                                                                                                                    |
| 6     | Bouncy and Wave                                                                                    | Done early, with phase 2.                                                                                                       |
| 7     | Magic move                                                                                         | Not started.                                                                                                                    |
| Later | ContentTransition, custom curves for MoveAndResize                                                 | Not started.                                                                                                                    |


The XAML.io "Animate" tab is not part of these phases: it's built later in the XAML.io repository, from the "Designer panel" section of [design.md](design.md).

### The spike (phase 0)


| Item                                                       | Result                                                                                              |
| ---------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| String presets through a `TypeConverter` on the value type | Works (`anim:Animate.MoveAndResize="Smooth"`).                                                      |
| Markup-extension parameters, including readable times      | Works (`{anim:Smooth ApplyTo=EverythingInside, Wave=0.8, Duration=0.4s, AlsoOnWindowResize=True}`). |
| Presets as resources and in Style Setters                  | Not tested yet.                                                                                     |
| `composite: 'add'` on `translate`                          | Works in Chromium.                                                                                  |
| `clip-path: inset(0px)` over the core's clip               | Works in Chromium (the collapsible sidebar has `ClipToBounds`).                                     |
| SVG geometry CSS properties on `rect`/`ellipse`            | Work in Chromium. Safari not tested; without them, the geometry snaps.                              |
| `linear()` and its fallback                                | `linear()` works in Chromium. The fallback (baked keyframes) is written but not tested.             |
| Loading on the XAML.io design surface                      | Not tested. The engine doesn't start when `DesignerProperties.IsInDesignTool` is true.              |




## To-do list of the plan


| Id                | Content                                                                | Status                                                                                                                                                                                                                                                                                                              |
| ----------------- | ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| spike             | See above                                                              | In progress                                                                                                                                                                                                                                                                                                         |
| core-observer     | `LayoutObserver` and its call sites, including `{Responsive}` switches | Done                                                                                                                                                                                                                                                                                                                |
| core-js           | `createVisual` skips `data-os-foreign` children                        | Done                                                                                                                                                                                                                                                                                                                |
| lib-api           | Public API                                                             | `MoveAndResize`, `Appear`, `Disappear` (presets, `From`/`To`, `Distance` in pixels or percent, `Fade`, `Duration`, `OthersDuration`, `Easing`, `Bounciness`, `OthersMove`, `StartAfter`, `OnPageOpen`), the `Item...` properties, `Hover` and `Press` (`Grow`, `Lift`, `Shrink`, `Nudge`), `Changes`, `Skip`, `AnimationSettings` and the curves (with `EaseOut`, `EaseIn`, `EaseInOut` and `Overshoot`) are done. `TextChange` and `MagicMove` are pending.                      |
| lib-causality     | Origins, groups, areas, decisions, exclusions, guards                  | Done, including the rule that changes never cross `None`, `{Responsive}` switches that animate during a resize, the drag-to-reorder exception (the element holding the mouse capture snaps, the other items glide), and no effects for items that virtualization realizes or removes. |
| lib-render        | Web Animations rendering                                               | Moves, sizes, clips, shapes, interruptions, effects with center compensation, ghosts, and the automatic wipe or fade of elements appearing or disappearing inside an area are done.                                                                                                                                 |
| lib-appear        | Appear, Disappear, ghosts, holds, items                                | Done: effects, ghosts, `OthersMove`, `StartAfter`, `OnPageOpen` and its hold (a Storyboard on `Visibility`), the automatic disappear inside areas, the hold until the layout pass, and the items: their effects, cascades, and containers paired by data item.                                              |
| lib-reactions     | Hover and Press                                                        | Done: the presets, hover and press that add up, the hover kept while the pointer is where the element rests, Nudge from the speed of the mouse, disabled elements, touch, `Speed` and reduced motion.                                                                                                              |
| lib-diagnostics   | `ShowWhatAnimates`, `Logged`                                           | Done                                                                                                                                                                                                                                                                                                                |
| designer-metadata | `AnimationCatalog`, `AnimationDesign`                                  | Pending                                                                                                                                                                                                                                                                                                             |
| gallery-app       | The guided tour                                                        | In progress (see below)                                                                                                                                                                                                                                                                                             |
| showcase-port     | The Showcase on the new API                                            | Done, not committed: branch `feat/use-new-animations-library` of the Showcase, rebased on `develop`. Its animation project is removed; the C# app is checked in the browser (see "The Showcase port"). The F# and VB projects were already broken on `develop`, so their changes weren't compiled.                    |
| lib-text          | Text effects                                                           | Pending                                                                                                                                                                                                                                                                                                             |
| lib-bouncy        | Bouncy and Wave                                                        | Done                                                                                                                                                                                                                                                                                                                |
| lib-magicmove     | Magic move                                                             | Pending                                                                                                                                                                                                                                                                                                             |
| later             | ContentTransition                                                      | Pending                                                                                                                                                                                                                                                                                                             |




## The gallery chapters


| Chapter                                  | Status                                                                                                                                                                                                                                                                                                                                                                   |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Welcome                                  | Built: the options explained, "Start with chapter 1".                                                                                                                                                                                                                                                                                                                    |
| 1. Your first animation                  | Built: an order card whose details fade in while the rows below slide down, and notes added and removed, which fade in and out.                                                                                                                                                                                                                                         |
| 2. Pick an effect                        | Built: every preset, with Side, Distance (Medium, Long, 50% and 100% of the card), Size, Fade, Duration, the duration of the text below (`OthersDuration`) and eight curves, each one a tile with its drawing (`EasingCurve`, from the library's own `Ease`, on the same scale), on a card that is hidden and shown; a validation message under an email field (`{anim:Slide From=Top, Distance=Short}`).                                                                                                                                                                                            |
| 3. When the others move                  | Built: While, Before and After side by side; an accordion whose sections wipe in from the top and roll up toward their header.                                                                                                                                                                                                                                          |
| 4. Page intros                           | Built: its own title is a page intro, which Replay page plays again; a profile that opens in steps of 0.1 s; InPlace, PushOthers and Skip side by side on three inboxes; an order whose details panel opens like a page, and a gift message whose effect waits 0.3 s while the room opens at once. The title will use Typewriter in phase 5.                         |
| 5. Smooth moves and resizes              | Built: Save button, badge, sidebar, ChildWindow (`ResizableDialog`, which stays centered), photo card, search box.                                                                                                                                                                                                                                                       |
| 6. Lists                                 | Built: a to-do list (add, add 3 with a cascade, remove, clear the done ones); toasts that slide in from the right, at most three; products sorted, shuffled and filtered in a WrapPanel; steps reordered by dragging; a streaming chat whose growing answer pushes what is below it, and whose oldest question and answer fade out at the third question; a virtualized ListBox of 300 orders (add, move to the top, remove), where scrolling plays nothing. |
| 7. Everything inside                     | Built: grid from 2 columns to 1, settings form whose details wipe in and out, dashboard with the chart carved out by `None`.                                                                                                                                                                                                                                             |
| 8. When the window or a panel is resized | Built: tiles with and without `AlsoOnWindowResize`, with `ItemMoveAndResize` on the WrapPanel; a profile panel resized with a GridSplitter, whose content switches at 220 px with `{Responsive ElementName=ProfilePanel}`, also from code; three step cards that switch from a row to a column with the window, at 1100 px. |
| 9. Text that moves                       | Coming soon (phase 5)                                                                                                                                                                                                                                                                                                                                                    |
| 10. Bouncy and Wave                      | Built: bouncy tab underline with Bounciness and Duration sliders, a growing card with a Wave slider.                                                                                                                                                                                                                                                                     |
| 11. Magic move                           | Coming soon (phase 7)                                                                                                                                                                                                                                                                                                                                                    |
| 12. Hover and press                      | Built: buttons that grow and shrink, lift, only shrink, or grow with a bounce; cards that lift; icons that the mouse nudges, with Distance and Tilt sliders; a button that doesn't react while a switch disables it.                                                                                                                                                     |
| 13. From code                            | Built: `Animate.Changes` (default and 0.6 s), `Animate.Skip`, `AnimationSettings` (Speed, DefaultDuration, IsEnabled), reset when leaving.                                                                                                                                                                                                                               |
| 14. What never animates, and why         | Built: scrolling, GridSplitter, Slider, typing, window resize (links to chapter 8, with `{Responsive}` as the exception), the 300-element budget, wrapping text, shapes (which switch size with `{Responsive}` at 960 px).                                                                                                                                               |
| 15. Putting it together                  | Built: a small copy of the Showcase's window (`MiniShowcase`), with the names and values of its XAML: the welcome intro, the tabs, the menu that pushes the page after 4.5 s, Nudge, two pretend samples (Buttons loads in 0.3 s, Data grid in 2.5 s with the loading popup), the source code pane, and Phone size, where the menu becomes a drawer with its quicker effect. Then each animation with its code and links to its chapters, and links to the live Showcase and its source code. |


Not built yet in the chapters: the playgrounds, the live XAML with Copy, and the summary sentences, which come with phase 4. Each demo has a "What should happen" expander (a chevron, closed by default) with its acceptance lines.

**The logo and the title of the top bar** go back to the welcome page.

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
- **Chapter 13.**
  - Direct changes jump; `Animate.Changes` glides in 0.25 s, or 0.6 s with a duration.
  - `Skip` logs its message.
  - Speed 0.5 makes it last about 0.48 s, and the log says "0.5 s"; a duration of 0.3 with speed 0.5 lasts 0.6 s.
  - With IsEnabled off, it snaps with a message.
  - Reset works, and the settings are reset when leaving the chapter.
- **Chapter 14.**
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
  - At 390 px nothing is wider than the window in chapters 2, 5, 7, 8, 10, 13 and 14, and their demos still animate (sidebar 150 to 64 px, search box, dialog at 12 px from each side, settings form, dashboard, boxes of chapters 13 and 14).



### `{Responsive}` switches

Checked in the Debug build, at 1300 × 800 unless stated otherwise:

- **Chapter 8, the profile panel**, dragged from 170 to 280 px with synthetic pointer events (see development.md).
  - The edge and the rest of the page follow the pointer in the same frame. Animations that were running when the drag started are cancelled.
  - At 220 px, the content glides into its new layout in about 14 frames while the drag goes on (the name moves 60 px right and 51 px up), then follows the edge again. The log says "{Responsive} switched the Orientation of StackPanel; 17 elements animate together (Smooth, 0.25 s, from the EverythingInside of ProfilePanel)."
  - "Switch the layout from code" glides in 0.25 s.
- **Chapter 8, the step cards**: crossing 1100 px both ways, the row becomes a column and back with an animation, which goes on through the next resize steps. The log says "...3 elements animate together (Smooth, 0.25 s, from the MoveAndResize of StackPanel)." Crossing 1280 px, where only the shell switches, writes no message.
- **Chapter 14, the shapes**, crossing 960 px both ways between 940 and 980 px, where the shell switches to the phone layout too (73 switches in one pass): the three boxes resize between 50 and 80 px and their gaps change, in 0.25 s. The rest of the page follows the window at once.
- **Start time.** That pass takes about 0.4 s and ends late in a frame, so before the fix of `startAtNextFrame` the shapes' animations ended before they were painted. Clicks and reveals still play in full with it: the chapter 8 switch button, and the settings form of chapter 7, both ways.
- **Each switch with its own setting**, at 1000 × 900 then 940, with the shapes in view: the batch has four timings, one for each of RectangleBox, EllipseBox, PathBox and WrapBox, and the log has four messages, like "{Responsive} switched the Width of EllipseBox; 2 elements animate together (Smooth, 0.25 s, from the MoveAndResize of EllipseBox)." Before, the whole page shared one timing and one message, which named AutoWidthBox. Back at 1000 px, the boxes glide from 50 to 80 px and WrapBox from 260 to 520 px in about 0.24 s. The chapter 8 step cards still glide at 1100 px, with the same message as before.



### Appear and Disappear (phase 3, step 1)

Checked in the Debug build, at 1050 × 900 and 390 × 844. The agent's browser was painting one frame per second during these tests, because its panel was not on screen. So the checks read the batches sent to `animations.js` (timings, delays, looks) and the final state, rather than timing the frames.

- **Chapter 1.**
  - The details fade in over 0.25 s while the payment and delivery rows slide down 89 px with the same timing; hiding them fades out a ghost while the rows slide back. Five quick clicks hand over each time: a fade-out starts from where the fade-in was (0.72), and the next fade-in from where the ghost was. It ends visible, with no ghost left.
  - A new note fades in and the counter and button slide down 60 px; a removed note's ghost fades out while the notes after it slide up. The two first notes fade in when the page opens.
  - Under reduced motion, the details fade in and out in 0.15 s and the rows snap, with "it fades in quickly, because reduced motion is simulated".
- **Chapter 2.**
  - Every preset, both ways: Fade, Slide (30 px from each side), Zoom (from 90 %, around the center), Pop (from 50 %, 0.5 s spring; it disappears with Zoom), Wipe (edges 1 to 4), Blur (8 px) and Custom (12 rotation keyframes). The card always ends at opacity 1 with no ghost left.
  - Duration 0.5 s and 1 s, Gentle, Linear, and Bouncy (a 0.5 s spring for the card, Smooth 0.5 s for the text below).
  - The Side row fades in and out with the rows below moving: "SideLabel appeared; it fades in, SideChoices fades in, and 21 elements animate together".
  - The validation message slides in from 10 px above while the button moves down 24 px, and fades out when the address is completed.
- **Chapter 3.**
  - While: the message and the rows move together. Before: the rows move, then the message fades in after 0.25 s. After: on hide, the ghost fades, then the rows move after 0.25 s. The log says "then" in the right order.
  - The accordion: the new section wipes in from its top while the headers below slide, and the old one rolls up toward its header in the same change ("Body1 appeared; it wipes in, Body0 wipes out, and 17 elements animate together"). Quick clicks end with one section open and no ghost.
- **Chapter 7.** The settings form's details wipe in from the top and wipe out toward the top with the size curve ("…and EmailDetails wipes out"). Quick toggles of the three options end right, with nothing running. Under reduced motion, everything snaps and the ghost is removed at once.
- **Media in a ghost.** With an iframe added by script to chapter 1's details, hiding them makes a ghost with an empty 50 × 20 box in its place, which fades out in 0.15 s and is then removed. The real iframe stays where it was.
- **Regressions.** Chapters 5 (Save, badge, sidebar), 7 (columns), 8 (switch from code) and 14 (rows, word, width, shapes) animate and log as before.
- **At 390 px**, chapters 1, 2 and 3 fit the window.

### StartAfter and OnPageOpen (phase 3, step 2)

Checked in the Debug build, at 1600 × 950, at 1600 × 2600 (to have the inboxes on screen when the page opens) and at 390 × 844, by reading the batches and the final state. Some checks used temporary values, reverted afterwards.

- **Chapter 4, page open.** One batch has the 12 effects of the page, each delayed by its `StartAfter` (0 to 600 ms). The Skip banner plays nothing. The PushOthers banner is held, then a second batch, 632 ms later, has its effect with no delay, raised, and the three messages sliding down. Before the fix, the wait counted from `Loaded`, which comes about 300 ms before the end of the first pass in the Debug build, so it was released too early.
- **Off screen**, when the page opens with the inboxes below the fold, the messages snap and only the banner's effect plays: the group is culled like any other.
- **Open the inboxes again** gives the same two batches (600 and 603 ms apart). With a temporary `StartAfter=0.2s`, the release came 233 ms after the inboxes opened, inside the 500 ms settling window, and the messages still slid down.
- **Reduce motion**: no hold; the InPlace and PushOthers banners both fade in quickly (0.15 s) after their `StartAfter`. **Slow motion** (speed 0.25): the waits and durations are four times longer (the InPlace banner waits 2.4 s).
- **The order.** The details panel wipes in and its lines slide in at 100, 200 and 300 ms. The gift message waits 300 ms while 15 elements move at once, and fades out at once while they move back.
- **Temporary values.**
  - `Disappear="{anim:Fade StartAfter=0.3s}"` on the gift message: the ghost waits 300 ms while the others move at once ("it fades out after 0.3 s"). Shown again while its ghost waited, it ends visible.
  - In chapter 3, `StartAfter=0.2s`: with Before, the effect starts at 450 ms (the 250 ms move, then the wait); with After, the rows wait 450 ms.
  - A PushOthers line in the details panel, shown at runtime: it is held, then released 322 ms later, pushing 17 elements. Shown, hidden and shown again quickly, it was first released 77 ms after the last opening, because the held line was skipped as collapsed; after the fix, 334 ms. Hidden before its release, it stays hidden, and appears normally the next time.
- **Regressions.** Chapters 1 (Add a note), 2 (the card, Sign up), 3 (the columns, the accordion), 5 (Save: 10 elements) and 7 (columns) animate and log as before, with no console errors.
- **At 390 px**, chapter 4 fits the window.

### Lists (phase 3, step 3)

Checked in the Debug build, at about 1024 × 576 and 1600 × 950, by reading the batches and the final state.

- **The to-do list.** A new task slides in from 10 px above while the tasks below and the buttons move down. "Add 3" cascades at 0, 50 and 100 ms: "Item "Reply to Sam" of Tasks appeared; it slides in, 2 other items follow, one every 0.05 s, and 20 elements animate together (…, from the ItemAppear of Tasks)". A removed task fades out while the others slide up; "Clear the done ones" fades them out in a cascade. When the page opens, only the to-do list is said to cascade.
- **Toasts.** Each one slides in from the right, the others glide 65 px down, and it fades out after 3 s or on a click. A fourth toast makes the oldest one fade out at once.
- **Sort and filter.** "By price", "By name" and "Shuffle" make every tile that changes place glide: "The items of Products changed order; 8 elements animate together (…, from the ItemMoveAndResize of Products)". Every move continues from the old container's ghost, so a sort during a sort doesn't jump, and no copy is left. The filter fades the tiles out, and back in. Sampled at every frame, from the click to the batch (about 100 ms): the copies show all the tiles with their names, for the three buttons and the checkbox, and the three tiles filtered out fade with their names. Before the fix, those frames showed no tile at all.
- **Drag to reorder**, with synthetic pointer events. The neighbors glide out of the way, the dragged step follows the pointer exactly (904 px for 904 px), dragging back and forth continues from the ghosts, and the order is right at the end. The message counts only what animates: "1 element animates".
- **The streaming chat.** The question slides in and the content below moves down 43 px. Each new line of the answer pushes it 18 to 19 px: "TextBlock "OpenSilver runs your XAM…" changed size; 20 elements animate together (…, from the ItemMoveAndResize of Chat)". At the third question, the oldest question and answer fade out, the rest moves up 162 px, and the new question slides in. A click while an answer streams is ignored. "Start over" (a `Clear()`) fades the three messages out with their text, and the greeting slides in again.
- **The long list.** Scrolling plays nothing: its batches only remove the copies of the rows it removed, in the same pass. Scrolling, "Add an order", "Move to the top" and "Remove" make no refresh copies, and neither do the to-do list and its removals. "Add an order" fades the new order in and slides 7 rows down, and the row pushed out at the bottom gets no copy. "Move to the top" glides the order 184 px. "Remove" fades it out, the rows below slide up, and the row realized at the bottom fades in.
- **Chapter 8, with `ItemMoveAndResize`.** Going from 1600 to 1350 px, the 11 tiles of FollowingTiles that move glide, with one timing in the batch, and SnappingTiles snaps: "The window was resized; the items of FollowingTiles follow it (Smooth, 0.25 s, from the ItemMoveAndResize of FollowingTiles)." After quick resizes, nothing is left running or offset. Clicking a tile of SnappingTiles widens it from 150 to 312 px while the tiles after it glide, and back.
- **Regressions.** Chapters 1 (details, note), 2 (card, sign up), 3 (new message), 4 (details), 5 (Save, sidebar), 7 (columns, Refresh with the carve-out), 8 (switch from code), 10 (tabs), 13 (`Animate.Changes`) and 14 (rows, word, shapes) animate and log as before, with no console errors.

### Hover and press (phase 3, step 4)

Checked in the Debug build, with the mouse simulated by script (see "Hovering and pressing" in development.md), by reading the computed `scale`, `translate` and `rotate` of the elements and their running animations.

- **Buttons.** "Buy now" grows to 1.05 around its center (translated by −2.18 and −0.93 px), and goes back to rest when the pointer leaves. The button next to it doesn't move. Pressed while hovered, it's at about 1.008 (1.05 × 0.96), and back at 1.05 when released. A quick click dips and comes back. "Cancel", with Press only, is at 0.96 while pressed. The bouncy one goes to 1.134 at 140 ms, 1.088 and 1.104, and settles at 1.1 in 0.5 s; when the pointer leaves, it goes to 0.966, 1.012 and 0.996, then 1. These were read by stepping the animation's time, because the tab painted once per second. Before the reaction spring, it only peaked at 1.109, which the product owner barely saw.
- **Cards.** A card lifts 6 px. It stays up while the pointer moves in the strip at its bottom that it rose from, and comes down when the pointer goes below it.
- **Nudge.** A fast sweep pushes an icon about 2.85 px, turns it 0.59 degrees, overshoots back by 0.78 px, and ends in about 320 ms. A slow sweep pushes it about 0.12 px. A click on the Distance slider at 4 sends the five icons their new Nudge.
- **Disabled.** With the switch off, Send doesn't react to the pointer; switched on again, it grows.
- **Settings.** Slow motion makes the grow last about 1 s. Reduce motion plays nothing.
- **Touch.** No hover; a press shrinks to 0.96, then is released.
- **Renumbered chapters.** Chapters 13, 14 and 15 have the right numbers and navigation, and chapter 10's Next goes to chapter 12.
- **Regressions.** Chapter 1 (the details appear and disappear, with 28 moves) and chapter 14 (a GridSplitter drag of 80 px, with no batch during the drag), with no console errors.

### The Showcase port

Checked in the C# Debug build of the Showcase, at 1600 × 950 and 700 × 900, by recording every `element.animate` call (keyframes, duration, delay and easing) and looking at the final layout. The product owner had compared it with the old library and listed what differed; each item was matched:

- **The Welcome intro.** The particles zoom from 0.5 with EaseOut over 1 s. The title zooms from 0.5 with Overshoot and Bounciness 1 (a `linear()` curve), and fades in on its own with EaseOut, so its opacity never goes past 1. The subtitle zooms in 0.25 s with EaseInOut while the others move in 0.375 s. The card zooms around its center in 1 s while the others move in 1.5 s with EaseInOut. Before, it grew from its bottom-right corner: the Grid around it re-centers what it holds as it grows, and the card now moves against that (−111.5, −154 px) so that it stays where it ends.
- **The menu** slides in from the left by half its width (−120 px) and fades in, over 1 s with EaseOut, after 4.5 s, raised above the page, while the page moves aside over 1.5 s.
- **"Fork me" and "Suggest"** slide down by half their height (−18.5 and −28.5 px) over 0.5 s with EaseOut, after 2 s and 2.3 s. The background fades in over 1 s after 1 s.
- **"View source."** The pane slides up from below by its whole height (493.5 px), with no fade, over 0.5 s with EaseOut, while the page shrinks from 992 to 493.5 px. The page stays above the GridSplitter until it has shrunk, so the splitter doesn't show on top of it; it can then be dragged. Closing it, the page grows back with EaseIn, and the iframe's copy fades out in 0.15 s (see "Known limitations").
- **A small screen** (700 × 900): the menu slides in over 0.5 s with EaseOut, the menu button moves 240 px over 0.75 s, and the page moves at once, as with the old library.
- **Chapter 2 of the gallery**, with the new rows: Distance 100% hides the card with a copy that slides 120 px (its height) with Overshoot in 0.25 s, while the text below moves over 1 s with EaseOut; the log says "(EaseOut, 1 s, from the Disappear of SampleCard)". Shown again with Fade=No, it slides back with no opacity animation. The XAML line reads `{anim:Slide Distance=100%, Fade=False, OthersDuration=1s, Easing=Overshoot}`.

### Chapter 15

Checked in the Debug build, at 1767 × 994 and 390 × 844, by recording every `element.animate` call and reading opacities, with clicks simulated by script:

- **The intro** has the Showcase's timeline: the background fades in after 1 s, the dots and the title zoom in after 0.5 s (the title with Overshoot), the tabs slide down after 2 s and 2.3 s, the subtitle pushes after 1.5 s (0.375 s for the others) and the card after 2.7 s (1.5 s), and the menu slides in after 4.5 s while 15 elements move over 1.5 s. The log names `MenuContainer`, `WelcomeSubtitle` and `WelcomeCard`, as in the Showcase.
- **The loading popup.** Buttons loads in 0.3 s: the popup stays at opacity 0 and is hidden before its turn. Data grid loads in 2.5 s: the popup zooms in with Overshoot after 1 s, and is gone when the sample shows.
- **The source code pane** slides up by its height (207.5 px), with no fade, while the page shrinks from 420 to 207.5 px. Closing it slides it down with EaseIn. The menu scrolls while the pane is open.
- **Phone size.** The copy narrows from 828 to 340 px over 0.5 s, with the fade of the menu button. The button slides the menu in over 0.5 s, and moves 150 px over 0.75 s; clicking the page closes the menu at once. Off again, the copy widens and the menu pushes the page aside. Turned on 2 s after the copy opens, and with the copy opened at phone size, the menu never comes by itself and the log has no message for it.
- **Links.** "Chapter 13: From code" opens chapter 13, and chapter 14's Next goes to chapter 15.
- **At 390 px** the copy is at phone size, and the Phone size switch is hidden. No console errors.

## Not verified yet

- The Showcase's loading popup itself: it's hard to open on purpose. Its effect was checked in the copy of chapter 15 (see "Chapter 15").
- The links to the live Showcase and its source code, which open a new tab.
- The Showcase in F# and VB, the MauiHybrid app and the Simulator.

- Phase 3, steps 1 to 3, in a browser that paints normally: the agent's checks read the batches, not the frames.
- A refreshed `ListBox`, which empties its containers by removing their content first: the library handles it, but no gallery demo refreshes one.
- Items: a `ListBox` with recycling virtualization (`VirtualizationMode=Recycling`), `ObservableCollection.Move` in a virtualized list that scrolls at the same time, nested lists, `ItemAppear` with `OnPageOpen=Skip`, and drags by hand on a touch screen.
- `Visibility.Hidden` (no gallery demo uses it, also with `StartAfter`), the warnings of `CheckEffect`, and ghosts of a real `MediaElement` or `WebBrowser` (only an iframe added by script was tried).
- `OnPageOpen=PushOthers` in a template created during Measure, in a `ChildWindow`, and on an element whose Appear is set in a Style.
- Drags by hand, including the GridSplitter of chapter 14, which was only dragged with synthetic pointer events. They start a GridSplitter's capture only with `setPointerCapture` patched, as in development.md.
- Hover and press with a real mouse, pen and finger: chapter 12 was tested with simulated pointer events only. How Nudge feels by hand.
- Reactions in Safari and Firefox (`animation.persist()`, and `scale` and `rotate` with `composite: 'add'`), set in a Style or as a resource, and inside a `ChildWindow` or a `Popup`.
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

- **Wrapping text** wraps at the current box width during a width animation, so words hop between lines. Chapter 14 shows it.
- `Path` **geometry** snaps inside an animated box, and so do Rectangle and Ellipse on browsers without the SVG geometry CSS properties.
- **Clips** other than the element's own box snap to their final value.
- **App code** that positions elements from `ActualWidth` or `SizeChanged` jumps, because those report final values at once.
- **The** `RenderTransformOrigin` **offset** snaps.
- **Storyboards** that animate layout properties of animated elements are smoothed twice.
- **Elements that appear or disappear without an effect.** Inside an area with `ApplyTo=EverythingInside`, they wipe in from the side they push the others away from, or fade in when they push nothing, and wipe or fade out the same way. Elsewhere they appear and disappear at once, and only the others move.
- **Ghosts are copies.** What disappears is a copy of the element as it was: nothing inside it changes while it fades out. Explicit Disappear effects inside it don't play on their own. Media and frames in it are empty, and it fades out in 0.15 s. That's why the Showcase's source code pane fades out quickly instead of sliding down: playing the Disappear on the real element was proposed, and not chosen for now.
- **One effect gives the timing.** When several elements appear or disappear in the same change and container, the others move with the timing and the `OthersMove` of the first one found.
- **One frame at the old size.** When a click makes an element Visible again, the core shows it in the DOM during the click itself (releasing the mouse capture runs the queued JavaScript), but arranges it only in the next layout pass, one dispatcher tick later. The browser often paints a frame in between, where the element has the box it had before it was collapsed and the elements around it haven't moved yet. The library hides it until that pass when it has an Appear or is inside an area. Elsewhere, it needs a fix in the core: a pending layout pass should always run before the browser paints.
- **New items settle too.** For 0.5 s after an item appears, changes that start inside it snap, like for any element that appears. A chat answer that grows as soon as it's added snaps for that time; chapter 6 waits 0.7 s with "…" before the words come.
- **Equal items.** Items are told apart with `Equals`. With the same string twice in a list, removing one plays no disappear effect while the other stays, and a sort can make one glide from the other's place while the other appears like a new item.
- **Paired moves carry the position only.** An item shown again in a new container while its old one was still moving goes on from where it was on screen, but its size and opacity at that moment are not carried over.
- **Layout clips move with their element.** When an element whose layout clip isn't its own box animates, like an items panel that overflows its list, its clip moves with it. That's why the chapter 6 chat removes its oldest messages instead of scrolling.
- **Reactions need something to hit.** An element without a `Background` only reacts while the pointer is over its content, because the browser doesn't hit the rest of its box.
- **Appear effects hide reactions while they play.** An effect with an offset, a zoom or a rotation replaces the element's hover or press until it ends. A ghost is a copy of the element at rest, so an element that disappears while hovered starts from its rest size.
- **A disabled button doesn't always look disabled.** In the PrimaryButton style of the gallery, a button disabled while the mouse is elsewhere keeps its normal look, even without reactions. That's an OpenSilver issue; chapter 12 says that the button doesn't react rather than showing it dimmed.
- **The Simulator:** JavaScript batches can arrive a frame apart.



## Decisions, and why

- **Names:** `Animate`**,** `OpenSilver.Animations`**,** `AnimationSettings`**.** "Animate" is understood by everyone, and it reads as an instruction that matches the "When it ..." sections. "Motion" was considered and rejected, because it would be less understood. "Reduce motion" keeps its name, because that's the operating-system setting.
- **Beginner vocabulary everywhere:** `From`/`To`, `StartAfter`, `Zoom`, `Bouncy`, `Cascade`, `Wave`, and one curve vocabulary (`Smooth`, `Gentle`, `Linear`, `Bouncy`, then `EaseOut`, `EaseIn`, `EaseInOut` and `Overshoot`). Messages are plain sentences that name elements by `x:Name`.
- **The classic curves keep their CSS names**, chosen by the product owner for the Showcase port: `EaseOut`, `EaseIn` and `EaseInOut` (the cubic curves of the old library), and `Overshoot`, its `BackEase`, which goes past the end once and comes back, with `Bounciness` for how far. Designers know these names; `Smooth` stays the default and the first one the gallery shows.
- **A fade never bounces or overshoots.** With Pop, Bouncy and Overshoot, the opacity plays on its own curve (Smooth for the bounces, EaseOut for Overshoot), so the element never gets more opaque than it is, and doesn't flicker. The movement keeps its bounce.
- **The others never bounce or overshoot either**: with Pop and Bouncy they move Smooth, and with Overshoot EaseOut, so that the content around the element doesn't wobble.
- **`OthersDuration`** gives the others their own duration, as the old library's page intros did (the Welcome card zooms in 1 s while the page makes room in 1.5 s). Without it, they keep the effect's timing.
- **Distances in percent are for Slide only**, like `Distance=50%`: it's a fraction of the element's size, so the old library's "slide by half the width" reads the same. Lift and Nudge move by pixels: with a percent, they keep their default distance and write a warning.
- **`Fade=False` on Slide and Zoom** makes an element slide or zoom in at full opacity, like the source code pane of the Showcase, which slides up from the bottom of the window as a drawer would.
- **An element that appears stays where it ends.** A Grid or a centered StackPanel re-centers what it holds as it grows, so a zoom around the center drifted toward a corner. The element now gets a move against the re-centering of the containers it makes grow (`k` in the batch, see architecture.md).
- **The Showcase keeps its page above the GridSplitter** (`Canvas.ZIndex="1"` on the page), instead of fading the splitter in: the source pane slides up under the page and the splitter, which shows in the gap as the page shrinks. That's a Showcase choice, with no library change.
- **No compatibility layer.** The Showcase's own animation library will be removed, and the Showcase ported to this API (phase 3).
- **No XAML.io designer tab for now.** It's specified in design.md, and built later in the XAML.io repository.
- **The gallery is a progressive storytelling tour.** Chapters are added in the phase that builds their feature.
- **Chapter 15 is a small copy of the Showcase, with a walkthrough**, chosen by the product owner, rather than a link only: the reader tries each animation on the page that explains it. The links to the live Showcase and its code say that they use this library from its next version.
- **Minimal OpenSilver changes:** only hooks and the insertion fix, with no behavior change. The rest stays in the library.
- **The hooks are internal for now**, as the OpenSilver owner asked: `LayoutObserver` is visible to OpenSilver.Animations only, through `InternalsVisibleTo`, so that OpenSilver doesn't commit to an API that isn't stable yet. What the library didn't use was removed at the same time: the `availableSize` parameter of `OnMeasured`, and unregistering (the library registers once, for the lifetime of the app). Every other hook and parameter is used.
- **Window resizes never animate by default.** Following a window edge with a delay feels laggy; elements opt in with `AlsoOnWindowResize`.
- **A** `{Responsive}` **switch is a change, like a click**, agreed with the product owner. It animates with the nearest setting, even while the window or a panel is resized; the rest of the resize still snaps. `AlsoOnWindowResize` keeps its name and meaning: following the resize itself. When several switches happen in the same resize, each one uses the timing and the message of its own nearest setting, also agreed with the product owner.
- `ThisElement` **means the element's own box.** Its setting gives the timing to changes that move or resize it, including changes that start inside it, like a longer label. An ancestor that stays in place doesn't make changes inside it animate. That's what `EverythingInside` is for.
- **The nearest setting wins**, agreed with the product owner after the first round of feedback. Going up from where a change starts, the first `EverythingInside`, or the first other setting on an element that moves or resizes, gives the timing. Before, an area won over a nearer `ThisElement` setting, and the "Good to know" of chapter 7 needed two hard bullets to explain the difference.
- **Silent snaps** for scroll chrome and for the 500 ms after an element appears. They're consequences, not things the developer did.
- **Only the first measure of a pass makes an origin**, and a window-resize pass has no origins except `{Responsive}` switches. These rules came from testing: without them, a window resize animated or snapped the whole page.
- **Bouncy only bounces moves**; sizes change smoothly, so content doesn't wobble.
- **Changes never cross** `None`**.** A change inside an element with `MoveAndResize=None` doesn't go up to the elements around it, and a change outside doesn't go into it. So what the carved-out element pushes snaps with it, even when another change animates in the same pass.
- **Automatic reveals inside areas.** In an `EverythingInside` area, an element that appears takes part in the change: a wipe that keeps up with the elements it pushes reads better than an element that overlaps them until they have moved.
- **Show what animates is off by default**, in every chapter, as the product owner asked. The reader turns the outlines on when they want them; chapter 5 suggests it.
- **The presets derive from** `Transition`**, not** `Effect`, chosen by the product owner. Every element has a `UIElement.Effect` property, so a type named `Effect` didn't compile in code-behind without its namespace.
- `Distance` **is a struct**, with `Short`, `Medium` and `Long` and a converter. As a `double`, `Distance=Short` didn't compile: the OpenSilver XAML compiler only accepts numbers for `double` properties.
- **Pop and Bouncy effects last twice the default duration**, like `Bouncy` moves, so that the bounce settles. The elements around them move Smooth, and their fade is Smooth too (see below).
- `StartAfter` **at runtime delays only the effect**, chosen by the product owner: the room opens at once, and the element appears in it later. Only `OnPageOpen=PushOthers` holds the element out of the layout.
- `StartAfter` **counts from the end of the layout pass that opened the page**, for every `OnPageOpen` value. That's when the effects of the page start, so an InPlace and a PushOthers element with the same `StartAfter` start together.
- **A container shown at runtime is a page that opens** for the elements inside it, as design.md says: their `StartAfter` and `OnPageOpen` apply, as in the details panel of chapter 4.
- **The PushOthers hold lasts no time** (`Duration` zero instead of `Forever`): it keeps its value without asking for frames, and an element that is never added keeps no clock running.
- **A released PushOthers element is not settling**: it pushes the others even with a `StartAfter` shorter than the 500 ms settling time.
- **Items are paired by data item, without listening to the collection.** design.md planned a `CollectionChanged` subscription. Pairing at the first arrange of a container covers sorts, filters, `Reset`, `Move`, a new `ItemsSource` and recycling the same way, because OpenSilver always detaches the containers it removes and creates their DOM again with a new id.
- **A paired item goes on from where its old container was on screen**, through that container's copy. Without it, a second sort during the first one made the tiles jump back to where the first sort had sent them.
- **A refreshed list is copied before it's emptied.** On a `Reset`, OpenSilver clears the `DataContext` of every container before its panel removes them, and the new containers are shown at the next pass, so the list was empty for a few frames. Changing that order in OpenSilver would be a behavior change, so the library listens to `DataContextChanged` on the containers it tracks (still not to the collection), and copies the containers on screen when the first one is emptied.
- **What holds the mouse capture snaps, with its containers.** During a drag to reorder, the capture is usually on an element inside the item, while the list moves the item's container. So the elements that contain the captured one snap too, and the dragged item follows the pointer while the others glide.
- **`ItemMoveAndResize=None` carves each item out**, like `MoveAndResize=None` on it, so the same word means the same thing on an item and on an element.
- **Virtualized removals play nothing.** A container removed while its item is still in the list was scrolled or virtualized out of view; it plays no disappear effect. A `VirtualizingStackPanel` tells its new offset only after it removes its containers, so this is decided at the end of the pass.
- **Items are named by their text, or by their data item.** Messages say `Item "Buy milk" of Tasks`. After a `Reset`, the containers have lost their text, so the data item's `ToString` names them, unless it's only the type name. The gallery's data classes override `ToString`.
- **The chat removes its oldest question and answer** at the third question, instead of scrolling. An items panel taller than its list is clipped by OpenSilver's layout clip, which would move with the animated panel (see "Known limitations").
- **At most three toasts.** A fourth one makes the oldest one fade out at once, which also shows ItemDisappear and the others gliding in the same change.
- **The chat's button isn't disabled while an answer streams.** It ignores clicks instead: when a button is enabled again with the mouse over it, OpenSilver keeps its Disabled look until the mouse leaves. That's an OpenSilver issue, unrelated to the library.
- **Chapter 8's tiles use `ItemMoveAndResize`**, as the acceptance scenario of design.md says, instead of `ApplyTo=EverythingInside` on the WrapPanel. Each tile that follows the window makes its own group, and the message names the list: "the items of FollowingTiles follow it".
- **`Animate.Hover` and `Animate.Press`, with the base class `Reaction`**, chosen by the product owner, with four presets: `Grow`, `Lift`, `Shrink` and `Nudge`. They were built together, in a chapter of their own (12), and the chapters after it were renumbered.
- **Reactions play in the browser**, on pointer events, without the layout engine. A hover changes nothing in the layout, and a round trip through C# on every pointer event would lag. C# only sends the reactions, the settings, and whether the element is enabled.
- **Reactions play on top**, with `composite: 'add'`, like the moves. The Showcase's `SpringEffect` replaced the element's `RenderTransform`, which broke any transform the app had set.
- **Hover and Press add up**, so that a button that grows on hover and shrinks when pressed dips from its hovered size, and comes back to it when released.
- **Bouncy springs more on Hover and Press.** With the spring of moves, a bouncy Grow overshot by less than a pixel, and the product owner barely noticed it. A higher default Bounciness wasn't enough, because that spring always dies out by the end of the duration: less damping made it ring about 10 times a second. Reactions use their own spring, looser and settling to 2% of the change, with the same `Bounciness` parameter and duration.
- **Leaving counts from where the element rests.** Otherwise a lifted card, or a button that shrinks, leaves the pointer outside it, comes back under it, and flickers.
- **Nudge is for Hover only**: a press has no direction. It keeps the `SpringEffect` defaults (3 px, 2 degrees, 0.4 s, the same spring back), so the Showcase looks the same after the port. It doesn't use `Easing`, because its curve is its spring.
- **No hover on touch screens.** A finger's `pointerenter` comes with the press, so a hover would play with every tap.



## Next steps

1. **The product owner tests chapter 12** with a real mouse and a touch screen.
2. **The OpenSilver PR.** Open the PR from `feat/layout-observer`, and get a published preview package. Then set the default `OpenSilverVersion` in `Directory.Build.props` to it, and use it in CI.
3. **The end of phase 3.** The product owner reviews the Showcase port and chapter 15, and commits them. The port replaced `SpringEffect` with `Hover=Nudge`, `Animation.OnAppear` with `Appear`, `AnimatedContentControl` with `OnPageOpen=PushOthers`, and `PropertyAnimator` with `MoveAndResize`.
4. **Phase 4:** `AnimationCatalog` and `AnimationDesign`, then the playgrounds, the live XAML and the summaries in every chapter.
5. **Phase 5** (text, chapter 9), then **phase 7** (Magic move, chapter 11).

