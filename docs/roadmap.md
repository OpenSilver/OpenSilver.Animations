# Status and roadmap

Last updated: October 30, 2026, after the gallery's chapters were reordered, with what shows the library best first. Before that, October 29, 2026, after the first animations in XAML.io's own interface (the AI input and the side panels), which brought early arrivals, the hook for popups that close, copies of shadow roots, frames that change size with their box in a Magic move, and an element that arrives hidden from the first frame, also when a popup lays it out before its partner leaves. Then functions of the browser console for the settings, like `osAnimations.slowMotion(4)`. Then a text box that opens into a popup in chapter 11, with the copies of a closing popup's Magic moves shown until they travel, and demo snippets that follow the options of their demos. Then OpenSilver's hooks were merged, and the library builds on the first MyGet preview that has them. Then the gallery's shell got its own animations and the Modern theme, an element hidden right after its container appeared goes at once, and `Wave` goes up to 2. Then the others move in a wave also when an element appears with an effect, and new animations wait at their start for a frame that comes on time, so that a long task right after them doesn't play them twice.

## Phases


| Phase | Content                                                                                            | Status                                                                                                                          |
| ----- | -------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| 0     | Spike: validate the risky parts                                                                    | Partly done (see below).                                                                                                        |
| 1     | OpenSilver core changes: `InternalHooks` and the insertion fix                                     | Done: commit `547adac2` on branch `feat/internalhooks` (first called `feat/layout-observer`), then the `{Responsive}` hook (`OnResponsiveValueChanged`), then the hooks made internal, with `InternalsVisibleTo("OpenSilver.Animations")`, and their unused parts removed, then `LayoutObserver` renamed `InternalHooks` in the PR review. Then `OnDetaching` for a popup that closes, in `PopupRoot.Close`. Merged into `develop` as `38ba1092` ([#1112](https://github.com/OpenSilver/OpenSilver/pull/1112)), and published on MyGet in OpenSilver `3.4.0-preview-2026-10-29-113513-38ba1092`, which the library uses. |
| 2     | `MoveAndResize` (Smooth), grouping, exclusions, performance guards, diagnostics; the gallery shell | Done. The product owner tested it; the first round of feedback is addressed and verified in Chromium (see "Verified").          |
| 3     | `Appear`/`Disappear`, `OnPageOpen`, `OthersMove`, the `Items...` properties, `Hover` and `Press`; then the Showcase port | Built, in four steps, then the port and chapter 15; the product owner reviews them. Step 1 is done: `Appear`/`Disappear` with their presets, `OthersMove`, the automatic disappear inside areas, and chapters 1 to 3. Step 2 is done: `StartAfter` and `OnPageOpen`, and chapter 4. Step 3 is done: `ItemsAppear`, `ItemsDisappear`, `ItemsMoveAndResize` and `ItemsCascade`, chapter 6, and the chapter 8 tiles. Step 4 is done: `Hover` and `Press` with `Grow`, `Lift`, `Shrink` and `Nudge`, and chapter 12. The Showcase port is done, not committed yet, with what it needed to look as before: the classic curves and `Overshoot`, `OthersDuration`, `Fade=False`, distances in percent, `Zoom` sizes, and elements that stay where they end. Chapter 15 is built: a small copy of the Showcase's window, and how each of its animations is made. |
| 4     | `AnimationCatalog` and `AnimationDesign`, and the gallery playgrounds built on them                | Not started.                                                                                                                    |
| 5     | Text effects (`TextChange`, Typewriter, ...)                                                       | Built, not committed yet; the product owner reviews it. `TextChange` with `Roll`, `CrossFade`, `Scramble`, `Retype` and `CountUp`; the Appear and Disappear presets `Typewriter`, `Backspace` and `FadeLetters`; chapter 9, and the typed title of chapter 4. |
| 6     | Bouncy and Wave                                                                                    | Done early, with phase 2.                                                                                                       |
| 7     | Magic move                                                                                         | Built, not committed yet; the product owner reviews it. `MagicMove` (an id, or `{anim:MagicMove}` with `Travel`, `Duration` and `Easing`) and `MagicMoveByName`, and chapter 11. |
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
| core-observer     | `InternalHooks` and its call sites, including `{Responsive}` switches  | Done                                                                                                                                                                                                                                                                                                                |
| core-js           | `createVisual` skips `data-os-foreign` children                        | Done                                                                                                                                                                                                                                                                                                                |
| lib-api           | Public API                                                             | `MoveAndResize`, `Appear`, `Disappear` (presets, `From`/`To`, `Distance` in pixels or percent, `Fade`, `Duration`, `OthersDuration`, `Easing`, `Bounciness`, `OthersMove`, `StartAfter`, `OnPageOpen`), the `Items...` properties, `Hover` and `Press` (`Grow`, `Lift`, `Shrink`, `Nudge`), `TextChange` (`Roll`, `CrossFade`, `Scramble`, `Retype`, `CountUp`), the text presets (`Typewriter`, `Backspace`, `FadeLetters`), `MagicMove` (`Id`, `Travel`, `Duration`, `Easing`) and `MagicMoveByName`, `Changes`, `Skip`, `AnimationSettings` and the curves (with `EaseOut`, `EaseIn`, `EaseInOut` and `Overshoot`) are done. |
| lib-causality     | Origins, groups, areas, decisions, exclusions, guards                  | Done, including the rule that changes never cross `None`, `{Responsive}` switches that animate during a resize, the drag-to-reorder exception (the element holding the mouse capture snaps, the other items glide), and no effects for items that virtualization realizes or removes. |
| lib-render        | Web Animations rendering                                               | Moves, sizes, clips, shapes, interruptions, effects with center compensation, ghosts, and the automatic wipe or fade of elements appearing or disappearing inside an area are done.                                                                                                                                 |
| lib-appear        | Appear, Disappear, ghosts, holds, items                                | Done: effects, ghosts, `OthersMove`, `StartAfter`, `OnPageOpen` and its hold (a Storyboard on `Visibility`), the automatic disappear inside areas, the hold until the layout pass, and the items: their effects, cascades, and containers paired by data item.                                              |
| lib-reactions     | Hover and Press                                                        | Done: the presets, hover and press that add up, the hover kept while the pointer is where the element rests, Nudge from the speed of the mouse, disabled elements, touch, `Speed` and reduced motion.                                                                                                              |
| lib-diagnostics   | `ShowWhatAnimates`, `Logged`                                           | Done                                                                                                                                                                                                                                                                                                                |
| designer-metadata | `AnimationCatalog`, `AnimationDesign`                                  | Pending                                                                                                                                                                                                                                                                                                             |
| gallery-app       | The guided tour                                                        | In progress (see below)                                                                                                                                                                                                                                                                                             |
| showcase-port     | The Showcase on the new API                                            | Done, not committed: branch `feat/use-new-animations-library` of the Showcase, rebased on `develop`. Its animation project is removed; the C# app is checked in the browser (see "The Showcase port"). The F# and VB projects were already broken on `develop`, so their changes weren't compiled.                    |
| lib-text          | Text effects                                                           | Done, not committed: the text changes and their hold until the layout pass, the overlay, the letters of Appear and Disappear, the cursor (with `BlinkFirst`, also on Backspace and Retype), the caps for long texts, reduced motion and the messages.                                                                                                             |
| lib-bouncy        | Bouncy and Wave                                                        | Done                                                                                                                                                                                                                                                                                                                |
| lib-magicmove     | Magic move                                                             | Done, not committed: pairing in the same pass or within 0.3 s, the same element first and then the nearest one; copies of the elements inside a container that disappears; Above and Behind; the duration by distance; the cross-fade, with copies that add up, corners and borders that change, frames that change size with the box, content that zooms with its letters, is held where the box moves the least and is cut off where it doesn't fit, and texts that travel by their letters and take turns; SVG gradients kept in the copies; waiting for an image's size; interruptions; `MagicMoveByName`; reduced motion, `ShowWhatAnimates` and the messages. |
| later             | ContentTransition                                                      | Pending                                                                                                                                                                                                                                                                                                             |




## The gallery chapters


| Chapter                                  | Status                                                                                                                                                                                                                                                                                                                                                                   |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Welcome | Built: the options explained, "Start with chapter 1", and that the first three chapters show the main ideas in a few minutes. |
| 1. Your first animation | Built: an order card whose details fade in while the rows below slide down, and notes added and removed, which fade in and out. |
| 2. Magic move | Built: a wizard of three steps in a ContentControl with `MagicMoveByName`, whose avatar, heading and plan travel between the steps; a text box that opens into a card in a Popup, like XAML.io's AI input, and back when the popup closes (outside click, Escape or Send); toppings that travel between two lists, which make room with `ItemsMoveAndResize`; a segmented control (Day, Week, Month, Year to date) whose selection pill slides behind the labels; six photos that open into a detail view, the photo and its title travelling, and back. The photos are generated (`Images/`). |
| 3. Smooth moves and resizes | Built: Save button, badge; under "Everything inside a container", a settings form whose details wipe in and out, and a growing card with a Wave slider; under "Layouts that switch while you drag", a profile panel resized with a GridSplitter, whose content switches at 220 px with `{Responsive ElementName=ProfilePanel}`, also from code. |
| 4. Page intros | Built: its own title is a page intro, which Replay page plays again; a profile that opens in steps of 0.1 s; an order whose details panel opens like a page, and a gift message whose effect waits 0.3 s while the room opens at once; InPlace, PushOthers and Skip side by side on three inboxes. The title is typed in (Typewriter). |
| 5. Hover and press | Built: icons that the mouse nudges, with Distance and Tilt sliders; buttons that grow and shrink, lift, only shrink, or grow with a bounce; cards that lift; a button that doesn't react while a switch disables it. |
| 6. Lists | Built: a to-do list (add, add 3 with a cascade, remove, clear the done ones); products sorted, shuffled and filtered in a WrapPanel; steps reordered by dragging; toasts that slide in from the right, at most three; a streaming chat whose growing answer pushes what is below it, and whose oldest question and answer fade out at the third question; a virtualized ListBox of 300 orders (add, move to the top, remove), where scrolling plays nothing. |
| 7. More moves and resizes | Built: grid from 2 columns to 1, bouncy tab underline with Bounciness and Duration sliders, sidebar, ChildWindow (`ResizableDialog`, which stays centered), photo card, search box, dashboard with the chart carved out by `None`. |
| 8. Text that moves | Built: a title typed with a cursor; an Inbox badge that rolls (Add one, Add 10, Remove one, with From Automatic, Bottom or Top) inside a Border with MoveAndResize; a tagline that is retyped, cross-faded and scrambled; a message typed in and erased with Backspace, or faded letter by letter, with Speed, Cursor and Blink first choices; a dashboard whose three numbers count up on Refresh; a clock that rolls each second, started by a button. |
| 9. Pick an effect | Built: every preset, with Side, Distance (Medium, Long, 50% and 100% of the card), Size, Fade, Duration, the duration of the text below (`OthersDuration`) and eight curves, each one a tile with its drawing (`EasingCurve`, from the library's own `Ease`, on the same scale), on a card that is hidden and shown. Each effect is a tile too, with a still drawing that plays an exaggerated loop on hover, with Storyboards (`EffectPreview`). Each change of an option, or a click on the chosen one, plays the card again: it disappears, then appears. The duration starts at 0.5 s. On a stage at least 600 pixels wide, the options are on the left and the card on the right; on a narrower one, the card is below the effects; a validation message under an email field (`{anim:Slide From=Top, Distance=Short}`). |
| 10. Putting it together | Built: a small copy of the Showcase's window (`MiniShowcase`), with the names and values of its XAML: the welcome intro, the tabs, the menu that pushes the page after 4.5 s, Nudge, two pretend samples (Buttons loads in 0.3 s, Data grid in 2.5 s with the loading popup), the source code pane, and Phone size, where the menu becomes a drawer with its quicker effect. It also tries three ideas that the Showcase doesn't have yet: each page shows at once, under the loading popup, and the rows of Data grid fade in when they are loaded; the menu slides out on small screens; and the pages have a transition. Then each animation with its code and links to its chapters, and links to the live Showcase and its source code. |
| 11. When the others move | Built: an accordion whose sections wipe in from the top and roll up toward their header; While, Before and After side by side. |
| 12. When the window is resized | Built: tiles with and without `AlsoOnWindowResize`, with `ItemsMoveAndResize` on the WrapPanel; three step cards that switch from a row to a column with the window, at 1100 px. |
| 13. From code | Built: `Animate.Changes` (default and 0.6 s), `Animate.Skip`, `AnimationSettings` (Speed, DefaultDuration, IsEnabled), and the functions of the browser console. The controls show the changes made from the console, and only their own changes are reset when leaving. |
| 14. What never animates, and why | Built: scrolling, GridSplitter, Slider, typing, window resize (links to chapter 12, with `{Responsive}` as the exception), the 300-element budget, wrapping text, shapes (which switch size with `{Responsive}` at 960 px). |


**The order of the chapters** puts what shows the library best first, so that a visitor who reads only the first three chapters sees Magic move, the moves and resizes, and the elements around a change that follow it. Within a chapter too, the most striking demos come first, and the side-by-side comparisons last. The chapters were renumbered on October 30, 2026, at the product owner's request, and their pages are named after their topic (`MagicMoveChapter.xaml`), so that the numbers are only in `Tour`, the `UriMapper` of `MainPage`, and each page's overline and `ChapterNavigation`. The rest of this file was written before, with the old numbers: 1 Your first animation, 2 Pick an effect, 3 When the others move, 4 Page intros, 5 Smooth moves and resizes, 6 Lists, 7 Everything inside, 8 When the window or a panel is resized, 9 Text that moves, 10 Bouncy and Wave, 11 Magic move, 12 Hover and press, 13 From code, 14 What never animates, 15 Putting it together. The new chapter 3 has the button and the badge of the old 5, the settings form of the old 7, the wave of the old 10 and the resized panel of the old 8; the new chapter 7 has the rest of the old 5, 7 and 10.

The snippet of a demo with options follows them, without the default values: in chapter 9, the Wave slider of chapter 3, the carve-out switch and the Bouncy sliders of chapter 7, the badge and the letters of chapter 8, Nudge and the Send switch of chapter 5, and the settings of chapter 13, which also show the changes made from the console (`Controls/Snippet.cs` writes the values). Not built yet in the chapters: the playgrounds, a Copy button, and the summary sentences, which come with phase 4. Each demo has a "What should happen" expander (a chevron, closed by default) with its acceptance lines.

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
  - **The console functions**, in the Release build. Each of the 21 calls tried answered with one sentence, before it returned. `slowMotion(4)` made the Speed label 0.25 and the box's move and resize 1000 ms. `slowMotion(100)` said "20 times slower, the most they can", and `slowMotion(0)` and `slowMotion('fast')` said what to type.
    - `showWhatAnimates(true)` and `simulateReducedMotion(true)` turned on the toolbar's switches, and `isEnabled(false)` turned off the chapter's switch, with "nothing animated because AnimationSettings.IsEnabled is false" in the log. `logForDebugging(true)` wrote the box's message to the console.
    - Slow motion stayed after going to chapter 12 and back. After the chapter's own switch was used, leaving it reset the speed, as before.
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

- **The to-do list.** A new task slides in from 10 px above while the tasks below and the buttons move down. "Add 3" cascades at 0, 50 and 100 ms: "Item "Reply to Sam" of Tasks appeared; it slides in, 2 other items follow, one every 0.05 s, and 20 elements animate together (…, from the ItemsAppear of Tasks)". A removed task fades out while the others slide up; "Clear the done ones" fades them out in a cascade. When the page opens, only the to-do list is said to cascade.
- **Toasts.** Each one slides in from the right, the others glide 65 px down, and it fades out after 3 s or on a click. A fourth toast makes the oldest one fade out at once.
- **Sort and filter.** "By price", "By name" and "Shuffle" make every tile that changes place glide: "The items of Products changed order; 8 elements animate together (…, from the ItemsMoveAndResize of Products)". Every move continues from the old container's ghost, so a sort during a sort doesn't jump, and no copy is left. The filter fades the tiles out, and back in. Sampled at every frame, from the click to the batch (about 100 ms): the copies show all the tiles with their names, for the three buttons and the checkbox, and the three tiles filtered out fade with their names. Before the fix, those frames showed no tile at all.
- **Drag to reorder**, with synthetic pointer events. The neighbors glide out of the way, the dragged step follows the pointer exactly (904 px for 904 px), dragging back and forth continues from the ghosts, and the order is right at the end. The message counts only what animates: "1 element animates".
- **The streaming chat.** The question slides in and the content below moves down 43 px. Each new line of the answer pushes it 18 to 19 px: "TextBlock "OpenSilver runs your XAM…" changed size; 20 elements animate together (…, from the ItemsMoveAndResize of Chat)". At the third question, the oldest question and answer fade out, the rest moves up 162 px, and the new question slides in. A click while an answer streams is ignored. "Start over" (a `Clear()`) fades the three messages out with their text, and the greeting slides in again.
- **The long list.** Scrolling plays nothing: its batches only remove the copies of the rows it removed, in the same pass. Scrolling, "Add an order", "Move to the top" and "Remove" make no refresh copies, and neither do the to-do list and its removals. "Add an order" fades the new order in and slides 7 rows down, and the row pushed out at the bottom gets no copy. "Move to the top" glides the order 184 px. "Remove" fades it out, the rows below slide up, and the row realized at the bottom fades in.
- **Chapter 8, with `ItemsMoveAndResize`.** Going from 1600 to 1350 px, the 11 tiles of FollowingTiles that move glide, with one timing in the batch, and SnappingTiles snaps: "The window was resized; the items of FollowingTiles follow it (Smooth, 0.25 s, from the ItemsMoveAndResize of FollowingTiles)." After quick resizes, nothing is left running or offset. Clicking a tile of SnappingTiles widens it from 150 to 312 px while the tiles after it glide, and back.
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
- **Later, the three ideas that the Showcase doesn't have yet** (now chapter 10, checked in the Release build at 1400 × 1000). Data grid shows at once with an empty table, the popup zooms in over it after 1 s, and the rows fade in at 2.5 s, in a space of their own, so that nothing moves. Each page slides up a little while the one before fades out; the welcome page opens without its transition (`OnPageOpen=Skip`), and the log says so. At phone size, the menu slides out to the left when the switch is turned on and when an item is clicked.

### Text (phase 5)

Checked in the Debug build, at 1767 × 994 and 390 × 844, by recording the batches, the text holds and every frame of the overlays, with clicks simulated by script:

- **The badge.** From 9 to 10, the 1 rises from below into a new column while the 9 rolls up into a 0, in 0.25 s, and the badge widens from 22 to 28 px with the Border's MoveAndResize: "The text of BadgeCount changed; it rolls up (0.25 s, from its TextChange)." From 10 to 9 the digits come from above ("rolls down"). With From set to Top, 39 to 40 comes from above; with Bottom, 40 to 39 from below.
- **Quick clicks.** Four clicks on Add 10, 60 ms apart, roll 8, 18, 28, 38 and 48: each roll keeps playing until the next one starts, the count never goes back, and nothing stays still in between. Before the fix, the hold jumped to the end of the running roll and showed it still for about 85 ms.
- **The dashboard.** Refresh counts the three numbers in 1 s, slowing down at the end, and keeps their formats ("$7,770", "2,095", "2.5%"). A second Refresh after 0.4 s goes on from where the count is (14,799, then 14,886), with no jump.
- **The clock** rolls only the digits that change, every second, and stops when the page is left.
- **The tagline.** Retype erases "the web" and types "desktops" at 30 letters per second; CrossFade blends in 0.25 s; Scramble shows random letters where the word changes, which settle from left to right in 1 s, while "Build apps for " stays. Two clicks 180 ms apart: Retype goes on erasing from what it shows, then types "phones", and all three end on "phones".
- **The message.** Typewriter types the 45 letters in 1.5 s, the cursor following them; then the cursor blinks twice and fades out by 3.5 s. The text below makes room in 0.25 s while the first letters are typed. The first frame shows no letter: before the fix, the whole message showed for one frame. Backspace erases from the last letter in 1.5 s, then the text below moves up ("it is erased letter by letter, then 24 elements animate together"). FadeLetters fades the letters in and out in 0.5 s while the text below moves.
- **Chapter 4.** Its title is typed in after 0.1 s, one letter every 33 ms.
- **The cursor, after the feedback** (sampled every 50 ms, and measured against the letters on screen):
  - The chapter 9 title: nothing shows during its 0.1 s StartAfter. Then the cursor is on at the first letter's place, goes off at 0.5 s, and the first letter comes with it at 0.93 s. It follows the 15 letters to 240 px, then blinks twice and fades out by 3.4 s. It is 3.2 px wide for the 32 px title, in the color of the text: "it is typed in after 0.1 s (30 letters per second, after its cursor blinks for 0.8 s, from its Appear)". Slow motion stretches the blink to 3.2 s.
  - The message: the cursor is 1.6 px wide for 16 px text, and stays within a pixel of the last letter, while typing and while Backspace erases on the ghost, also on the second line at 390 px. Backspace's cursor starts after the last letter and moves back with each letter. With Blink first at 1.5 s, it blinks there for 1.5 s (0.4 s on, 0.4 s off) before erasing, and the text below waits until the ghost is gone, at 3 s.
  - The choices send what they say: Default (Typewriter 0.8 s, Backspace none), None, 1.5 s on both, and Cursor No (no cursor and no wait, without a warning).
  - Retype: the cursor follows "the web" back to "Build apps for ", then "desktops", blinks twice and fades out. The old text and the new one never show together. Two clicks 200 ms apart: the second change starts from "Build apps for p", with one cursor, and ends on the real text.
  - Chapter 4's title gets the default cursor and its 0.8 s blink. No console errors.
- **Reduce motion.** The badge changes at once: "The text of BadgeCount changed at once, because reduced motion is simulated (AnimationSettings.SimulateReducedMotion)." The message fades in and out quickly, without letters.
- **Slow motion.** Retype at 7.5 letters per second, CrossFade 1 s, Scramble 4 s, typing 133 ms per letter, and the cursor blinks for 8 s.
- **At 390 px**, nothing in chapter 9 is wider than the window, and the message is typed on two lines.
- **Regressions.** Chapters 1, 2 and 5 (Save: 10 elements, with the same message) animate and log as before, with no console errors.

### Magic move (phase 7)

Checked in the Debug build, at about 1766 × 994, by recording every `element.animate` call, reading the travelling layers at each frame, and pausing the animations halfway for screenshots. Clicks were simulated by script, with the pointer capture patch of development.md: without it, no button clicks.

- **The pill.** From Week to Month, it travels behind the labels (inside their StackPanel), from x = 500 to 571 in 0.25 s: "Pill of RadioButton "Month" appeared as Pill of RadioButton "Week" disappeared, with the same Magic move "SelectionPill"; it travels from where that one was, behind the elements around it (Smooth, 0.25 s for the distance)." Slow motion makes it 1 s, and the message says so. Quick clicks stop the first trip where it is, and the next one goes on from there, with no jump.
- **The toppings.** A chip travels above the page to the other list in about 0.34 s while the chips around it make room. Three quick clicks travel together. Only the empty overlay div stays in the page afterwards.
- **The photos.** A thumbnail opens into the detail view, the photo and its title travelling together, and back. Before the fix, the detail image had no size yet in that pass, and its copy shrank toward a line; now both layers wait, then leave together at 188 ms and end at the detail image (480 × 320) and its title. Going back, the other thumbnails show at once (opacity 1 by 153 ms).
- **The wizard.** Next and Back work: the avatar and the heading travel, and the plan from step 2 to step 3, while the rest cross-fades: "The content of WizardHost changed (MagicMoveByName): 2 elements with the same x:Name in both travel to their new places, like Avatar (…), while the old content fades out and the new one fades in (…)." From step 2 to step 3, the old heading is gone at 211 ms of 311 and the new one starts at about 150 ms, so they take turns.
- **Corners, content and letters** (after the product owner's feedback on the avatar, the photo titles and `CornerRadius`). Read on the paused travels, at their start, halfway and at their end, and in zoomed screenshots taken halfway:
  - The avatar's "AM" is in the same place in both copies, within 0.3 px, from 56 to 36 px and from 36 to 88 px, and ends on the real one. Its corners go from 28 to 18 px and from 18 to 44 px, half its size all the way, so it stays a circle with `CornerRadius="999"`.
  - The plan card becomes the pill: corners from 8 to 12 px, the 2 px border gone, the colors in between, and its "Pro" from 15 to 13 px, from the card's left edge (x = 679) to the "Pro" of "Pro plan" (x = 632). The first try zoomed the content as much as the box, which made that "Pro" 6 px, and the next one centered it on the card's StackPanel, which fills the card.
  - The photo title "Dunes" has its two copies on top of each other at every point (60.5 × 28.4 px at a third of the way, at the same place, against 37 × 17.5 px at the start), with opacities that add up to 1 (0.25 and 0.75). Paused there, it reads as one SemiBold title, no bolder.
  - The pill, whose corners don't change, and the toppings, whose text stays in place in its chip, travel as before. Five quick clicks on the pill, and Open, Back and Open again on a photo within 0.3 s, end with nothing left in the page, nothing running and no console errors. Reduce motion still cross-fades in place in 0.15 s, with no layer.
- **Reduce motion.** Each pair cross-fades in place in 0.15 s: "…; they cross-fade in place quickly, because reduced motion is simulated (AnimationSettings.SimulateReducedMotion)."
- **Show what animates** outlines the element that arrives.
- **Regressions.** Chapter 1 (the details appear and disappear), with no console errors in any chapter tried.

### XAML.io's own interface

Checked in XAML.io's Debug build, on the local OpenSilver and library packages, at 1600 × 900. The library's messages went to the console (`LogForDebugging`, turned on for the test only), every `element.animate` call was recorded, and buttons were clicked with the pointer capture patch of development.md. Halfway frames were taken with `Page.captureScreenshot`, with each new 250 ms animation paused as it was created and its `startTime` kept from changing.

- **The AI input.** Clicking the pill opens the popup's card: "GlobalAiPopupRoot appeared as AIControlBase disappeared, with the same Magic move "GlobalAiInput"; it travels from where that one was, above the page (Smooth, 0.25 s for the distance)." The layer grows from 819 × 26 to 819 × 173. Clicking outside closes it, and the card goes back into the pill the same way. Nothing stays in the page, nothing keeps running, and there are no console errors. Before the fixes, opening played the card's own Appear, because the popup is laid out as it opens, before the binding to its `IsOpen` collapses the pill. Closing played nothing, because OpenSilver said nothing when a popup closed. Halfway, the page shows faintly through the layer, because the pill's background is partly transparent.
- **The AI input's frame.** Tested by hand, the travel showed the content a moment above the input, and its gradient border didn't follow the box. That border is drawn by children of `GradientBorderControl`: a Border with the gradient background, and an SVG rectangle for the outline, which paints with a gradient it refers to by id. The rectangle counted as content, so the content filled the box at both ends and was centered on the way, 75 px above the input's top. The copies had no outline either, because their ids were removed. Now the copies keep their gradients, the Border and the rectangle change size with the layer, and the content keeps its top and is cut off at the frame's edges. Paused at several points, both ways: one gradient frame between the input and the card, whose top stays where the input's top is, and the content inside it moves by about 18 px right and 14 px up. The card ends at full opacity, and there are no console errors.
- **The card for one frame.** Then, by hand, the opening showed more than it should for a moment. Recorded frame by frame, the real card was visible, at its full size, in the first frame of the travel. The animation that hides the element that arrives only filled forwards, and in that frame it was still before its start (`startAtNextFrame`). With `fill: 'both'`, the card stays hidden in all 14 frames of the travel. Closing hid the same flash under the card's copy. Slowed 4 times with `osAnimations.slowMotion(4)`, opening and closing last 1 s (61 and 62 frames), with the card hidden in every frame.
- **The card before its travel.** By hand, the whole card still showed for a moment, before the travel. Those recordings had only looked at the frames of the travel, from a script's click. A real click also focuses the text box, which opens the popup inside OpenSilver's mousedown handling. Recorded that way, and just before each paint (from a `ResizeObserver`), one frame showed the whole card next to the pill, 20 ms before the travel. The card's arrangement was already on the page, but its hold, the pill's collapse and the travel were still in OpenSilver's queue of JavaScript, which ran after that paint. Now the hold of an early arrival runs at once (`HoldNow`). Three opens with the focus, one from a script and one slowed 4 times show the card hidden in that frame, the pill or its copy always on screen, and the card hidden through the travel. Two closes are unchanged, and there are no console errors.
- **The side panels.** My Solutions and the Solution Explorer slide out when they are collapsed, and slide in when they are docked again from the popup of their strip: "LeftPanelContainer disappeared; it slides out, and 54 elements animate together (Smooth, 0.25 s, from the Disappear of LeftPanelContainer)", then "appeared; it slides in", and the same for SolutionExplorer. The document area moves by 181 px, and its width changes with it. The strip that replaces a panel shows at once, above the end of the panel's copy.
- **Shadow roots.** The copy of the Solution Explorer had no icons: they are `SvgImage` controls, an `HtmlPresenter` with `UseShadowDom`, and `cloneNode` leaves shadow roots out. Paused at 8% of the slide, the copy now shows them.
- **Regressions.** Gallery chapter 11 after these changes: the pill, a photo opened and closed, the wizard both ways, and the toppings both ways. Chapter 1: the details. Nothing stays in the page but the empty overlay, and there are no console errors.
- **Regressions after the frame**, in a Release build of the gallery: the pill (from 79 to 110 px wide), a topping, a photo opened and closed, and the wizard both ways and with Start over. Paused halfway, the avatar's "AM" stays centered in its circle, and the plan card's frame shrinks into the pill's while their contents cross-fade. No layer stays and there are no console errors.

### Chapter 11's popup, and snippets that follow their options

Checked in a Release build of the gallery, at 1600 × 900, recording just before each paint (from a `ResizeObserver`, see development.md), with clicks that also focus the text box.

- **Opening.** The card enters the page hidden while the text box still shows, then the text box's copy grows into it, and the card shows when it lands. No frame shows the card before its travel, or neither of them. The cursor is then in the card's text box. "Border appeared as another disappeared, with the same Magic move "AskBox"; it travels from where that one was, above the page (…)."
- **Closing.** OpenSilver removes the popup from the page at once, during the click, and the card's copy was only kept aside until the travel, at the next layout pass: two frames showed neither the card nor the text box. Now that copy shows where the card was from the frame the popup leaves until the travel takes it, 26 ms later. Closing with a click outside, Escape or Send all shrink the card back; what was typed in the card is in the text box, and Send empties it. Opening again works each time.
- **Regressions.** A photo of chapter 11 opens and closes with its two layers. Nothing stays in the page but the empty overlay, and there are no console errors.
- **The snippets.** The carve-out switch of chapter 7 (`None` on the chart, or not), Roll's From in chapter 9 (`{anim:Roll From=Bottom}`, `Top`, or `Roll`), its letters (`Speed=15`, `BlinkFirst=1.5s` on both, `BlinkFirst=0s` on Typewriter only, `Cursor=False`, FadeLetters, and back to `Typewriter` and `Backspace`), the Bouncy sliders of chapter 10 (`Bounciness=0.7, Duration=0.6s`), its Wave slider (from 0.8 to 0, where `Wave` is left out), Nudge in chapter 12 (`Distance=4, Tilt=3`), its Send switch (`IsEnabled="False"`), and chapter 13's settings (`Speed="1.25"`, `IsEnabled="False"`, then back to the defaults from the console).

### OpenSilver from MyGet

Checked on OpenSilver `3.4.0-preview-2026-10-29-113513-38ba1092`, the first MyGet preview with the merged hooks, in a Release build of the gallery at 1600 × 900.

- **The build.** The solution builds with 0 errors and 0 warnings: the security advisories of the local packages are gone. The overrides had to become `protected override`, since the hooks are now `protected`.
- **Moves.** Save in chapter 5 gives the same 10 animations and message as before.
- **Chapter 11's popup.** It opens with the cursor in the card, and closes with a click outside, Escape and Send. In the task where OpenSilver removes the popup, the card's copy is already shown, while the text box is still collapsed; the travel takes the copy about 35 ms later, and the text box is hidden until the copy lands. Nothing is left in the page, and there are no console errors.
- **Internals.** The check of development.md finds only `PopupRoot` besides `InternalHooks`.
- **XAML.io**, on the same OpenSilver and the library repacked for it. Its solution builds with 0 errors, its formatting check passes, and the AI input opens with the cursor in the card and closes twice without errors. When the popup leaves the page, the card's copy is already travelling, in the same task.

## Not verified yet

- The gallery's animated shell, its Modern theme and loading indicator, the elements hidden right after their container appeared, and `Wave` up to 2: built, and being checked by the product owner (see "The shell's own animations" in [design.md](design.md)).
- The wave of the elements pushed by an element that appears with an effect, like the gallery's log, and the start of new animations at a frame that comes on time (see "Start time" in [architecture.md](architecture.md)): built, and being checked by the product owner. Traced once when the gallery opens: the side panels waited at their start through the 0.3 s that the first page takes to load, then slid in over 15 frames.
- Chapter 2's new "Every effect": the effect tiles, the replay on each change and on a click on the chosen option, and the two layouts. Checked once: the tiles are still until the mouse enters them, then Zoom, Blur and Wipe play their loops after half a second and are still again when it leaves; a click on Slide, then on Slide again, makes the card slide out and back in each time, within 1.3 s at 0.5 s; the wide layout has no gap between the effects and the other options, and at 420 pixels the card is below the effects. Being checked by the product owner.
- The Showcase's loading popup itself: it's hard to open on purpose. Its effect was checked in the copy of chapter 15 (see "Chapter 15").
- The links to the live Showcase and its source code, which open a new tab.
- The Showcase in F# and VB, the MauiHybrid app and the Simulator.
- Magic move: `MagicMoveByName` on a `Frame` (only a ContentControl was tried), `Travel`, `Duration` and `Easing` set by hand (Bouncy), an element moved to another parent (the chips are new elements), an element that disappears without a partner, a swap whose new content never comes, ids shared by the templates of two lists, travels inside a scrolled view or a `ChildWindow`, Safari and Firefox, and the Simulator.
- Text: a TextBlock with several runs, Roll on text that wraps, text effects in Safari and Firefox, `CountUp` with other cultures, the warnings of `CheckEffect` and `CheckTextChange`, the text presets on the children of a Panel, a message shown again while Backspace erases it, and a text that changes while its TextBlock is collapsed.

- Phase 3, steps 1 to 3, in a browser that paints normally: the agent's checks read the batches, not the frames.
- A refreshed `ListBox`, which empties its containers by removing their content first: the library handles it, but no gallery demo refreshes one.
- Items: a `ListBox` with recycling virtualization (`VirtualizationMode=Recycling`), `ObservableCollection.Move` in a virtualized list that scrolls at the same time, nested lists, `ItemsAppear` with `OnPageOpen=Skip`, and drags by hand on a touch screen.
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
- XAML.io's own interface: sending from the AI card while signed in (the card should disappear after its 0.3 s wait, since the pill is collapsed by then), Escape, switching solution while the card is open, the phone and tablet layouts, and a window narrower than 1024 px, which collapses the panels.
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
- **A message hidden while it's being typed** shows its letters not yet typed, then is erased from its last letter: the ghost is a copy of the real text. With the 0.8 s `BlinkFirst` of Typewriter, this also happens when it's hidden while its cursor blinks first.
- **The cursor is placed to the whole pixel**, from the letters' `offsetLeft` and `offsetWidth`: it can be up to a pixel off the letter. In right-to-left text, it still goes on the right of each letter.
- **The speed in the messages** of Typewriter and Backspace is the one set, also for a long text that goes faster because of the caps.
- **The TextBlock gets its new size at once** when its text changes. What is around it moves smoothly only with `MoveAndResize`, as on the chapter 9 badge.
- **The content of a box travels as one picture.** In a Magic move, what is inside a box moves and zooms together. When the old and the new contents differ, like the plan card's two lines and the pill's label in chapter 11, they cross-fade on the way, rather than each part going to its new place.
- **A Magic move that stops halfway** and goes on from there keeps the corners and the zoom of its content where they stopped.
- **Some frames zoom.** The frame of a box that is scaled on screen, like an element in a Viewbox, zooms with what is inside it rather than changing its size, and so does a frame drawn by a `Path`, or by a shape in a browser without CSS geometry properties.
- **Without `plus-lighter`**, in browsers that can't add the two copies up, a Magic move cross-fades as before: the old copy stays until halfway, so a text that fades into itself looks a little bolder there.
- **A Magic move without a partner is late.** An element with a Magic move that disappears and finds no partner plays its Disappear 0.3 s later, and the elements around it don't wait for it: they move with their own settings, or snap.
- **An element that appears before its partner leaves** is hidden only when the partner leaves. If that happens in a later task, after the browser painted, the element can show in its place for a frame first. In XAML.io's AI input, both happen in the same task.
- **Some elements don't travel.** An element inside a container that disappears travels only if it is in view; otherwise its partner plays its own Appear. A `Hidden` element doesn't travel either: it keeps its space.
- **A popup that closes** plays only the Magic moves inside it. A Disappear on its content doesn't play: its copy would be removed with the popup. The rest of its content is gone at once, while the copies of its Magic moves wait for their travel.
- **A travel that waits for a size** measures its end after the wait, including what the other animations of the pass have moved by then.
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
- **The hooks are internal for now**, as the OpenSilver owner asked: `InternalHooks` is visible to OpenSilver.Animations only, through `InternalsVisibleTo`, so that OpenSilver doesn't commit to an API that isn't stable yet. It was renamed from `LayoutObserver` in the PR review, because it reports more than layout: visibility changes, detached elements and `{Responsive}` switches. What the library didn't use was removed at the same time: the `availableSize` parameter of `OnMeasured`, and unregistering (the library registers once, for the lifetime of the app). Before the merge, the reviewer added `Unregister` back, made `Register` public and the hook methods `protected`. Every other hook and parameter is used.
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
- **The list properties are plural:** `ItemsAppear`, `ItemsDisappear`, `ItemsMoveAndResize` and `ItemsCascade`, as the product owner decided, because they say what the items of a list do. They were `ItemAppear`, `ItemDisappear`, `ItemMoveAndResize` and `ItemCascade`, like XAML's `ItemTemplate`, until the review of phase 7.
- **`ItemsMoveAndResize=None` carves each item out**, like `MoveAndResize=None` on it, so the same word means the same thing on an item and on an element.
- **Virtualized removals play nothing.** A container removed while its item is still in the list was scrolled or virtualized out of view; it plays no disappear effect. A `VirtualizingStackPanel` tells its new offset only after it removes its containers, so this is decided at the end of the pass.
- **Items are named by their text, or by their data item.** Messages say `Item "Buy milk" of Tasks`. After a `Reset`, the containers have lost their text, so the data item's `ToString` names them, unless it's only the type name. The gallery's data classes override `ToString`.
- **The chat removes its oldest question and answer** at the third question, instead of scrolling. An items panel taller than its list is clipped by OpenSilver's layout clip, which would move with the animated panel (see "Known limitations").
- **At most three toasts.** A fourth one makes the oldest one fade out at once, which also shows ItemsDisappear and the others gliding in the same change.
- **The chat's button isn't disabled while an answer streams.** It ignores clicks instead: when a button is enabled again with the mouse over it, OpenSilver keeps its Disabled look until the mouse leaves. That's an OpenSilver issue, unrelated to the library.
- **Chapter 8's tiles use `ItemsMoveAndResize`**, as the acceptance scenario of design.md says, instead of `ApplyTo=EverythingInside` on the WrapPanel. Each tile that follows the window makes its own group, and the message names the list: "the items of FollowingTiles follow it".
- **`Animate.Hover` and `Animate.Press`, with the base class `Reaction`**, chosen by the product owner, with four presets: `Grow`, `Lift`, `Shrink` and `Nudge`. They were built together, in a chapter of their own (12), and the chapters after it were renumbered.
- **Reactions play in the browser**, on pointer events, without the layout engine. A hover changes nothing in the layout, and a round trip through C# on every pointer event would lag. C# only sends the reactions, the settings, and whether the element is enabled.
- **Reactions play on top**, with `composite: 'add'`, like the moves. The Showcase's `SpringEffect` replaced the element's `RenderTransform`, which broke any transform the app had set.
- **Hover and Press add up**, so that a button that grows on hover and shrinks when pressed dips from its hovered size, and comes back to it when released.
- **Bouncy springs more on Hover and Press.** With the spring of moves, a bouncy Grow overshot by less than a pixel, and the product owner barely noticed it. A higher default Bounciness wasn't enough, because that spring always dies out by the end of the duration: less damping made it ring about 10 times a second. Reactions use their own spring, looser and settling to 2% of the change, with the same `Bounciness` parameter and duration.
- **Leaving counts from where the element rests.** Otherwise a lifted card, or a button that shrinks, leaves the pointer outside it, comes back under it, and flickers.
- **Nudge is for Hover only**: a press has no direction. It keeps the `SpringEffect` defaults (3 px, 2 degrees, 0.4 s, the same spring back), so the Showcase looks the same after the port. It doesn't use `Easing`, because its curve is its spring.
- **No hover on touch screens.** A finger's `pointerenter` comes with the press, so a hover would play with every tap.
- **`TextChange` takes a `TextTransition`**, with the presets `Roll`, `CrossFade`, `Scramble`, `Retype` and `CountUp`, chosen by the product owner.
- **Roll chooses its side**, chosen by the product owner: from the bottom when the number grows, from the top when it shrinks, and from the bottom for text. `From` still forces a side.
- **The cursor blinks a few more times, then fades away**, chosen by the product owner: twice in about 2 s after the last letter.
- **A cursor by default on Typewriter, Backspace and Retype**, chosen by the product owner after trying chapter 9; `Cursor=False` turns it off. Backspace's cursor follows the erasing, and Retype's the erasing and the typing, then it blinks twice and fades away like Typewriter's.
- **`BlinkFirst`**, named by the product owner: how long the cursor blinks before the letters change, 0 to 10 s. Typewriter defaults to 0.8 s, so the reader sees the cursor blink once before typing; Backspace and Retype default to 0, without a lag, also chosen by the product owner. `StartAfter` is unchanged: nothing shows during it. Without a cursor, there is no wait.
- **The cursor is a tenth of the font size wide**, after the product owner found it too thin on the title. It was `0.08em`, but the em of the overlay is the TextBlock's div, while OpenSilver puts the font size on the Run, so the cursor was about 1 px wide at any size. It now takes the font size and the color of the first letter.
- **Backspace erases first, then the others close the gap** (`OthersMove=After` by default), chosen by the product owner.
- **The smaller defaults**, announced to the product owner: Roll and CrossFade last `DefaultDuration`, Scramble and CountUp four times that, FadeLetters twice. CountUp keeps the format of the new text, and counts from the old number or from 0. Retype keeps the beginning both texts share. Rich text cross-fades.
- **The old text is held until the layout pass.** OpenSilver writes the new text to the page before the library hears of it, so the script shows the old text in an overlay at once. The overlay is out of the flow because OpenSilver measures a TextBlock by copying its HTML; design.md said that measurement was unaffected for another reason, which was wrong.
- **An effect that is interrupted keeps playing until the change plays**, then the change starts from what it shows. Jumping to its end, or stopping where it was, showed a still frame while the pass was coming.
- **The text presets play on the children of a Panel** that are TextBlocks, as item effects. design.md had excluded them from items; on the items of an ItemsControl, which are containers, they fade with a warning.
- **The chapter 9 clock starts from a button**, because it writes a message every second in the log.
- **A Magic move lasts longer for a longer trip**, chosen by the product owner: `DefaultDuration` up to 150 px, growing to 1.8 times that at 900 px (0.25 s to 0.45 s with the defaults), so a long trip doesn't look rushed. `Duration` replaces it.
- **A Magic move is twice as long, with a stronger curve**, chosen by the product owner on October 30, 2026: twice `DefaultDuration` up to 150 px, growing to 3.6 times at 900 px (0.5 s to 0.9 s with the defaults), and a much stronger EaseInOut, `cubic-bezier(0.85, 0, 0.15, 1)`: very slow at the start and the end, and very fast in between. Bouncy keeps the durations it had (it was already twice as long). The curve is internal for now (`Easings.MagicMove`, "Strong EaseInOut" in the messages); whether to make it a public easing is open. The gallery keeps its hover background quick, with `Duration=0.25s, Easing=Smooth`.
- **Texts scale on the way, boxes change their width and height**, chosen by the product owner. A text that changed size by width and height would wrap differently on the way; a box that scaled would stretch its content.
- **With `MagicMoveByName`, the new content fades in**, chosen by the product owner, unless it has its own Appear; the old content fades out. Both last as long as the longest travel.
- **The chapter 11 photos are generated**, chosen by the product owner: six small pictures in the gallery's `Images` folder.
- **Behind needs two backgrounds.** `Automatic` travels Behind only when both elements are the first of several children and cover their parent, like a selection pill. Anything else travels Above, which never gets cut by a container that clips.
- **The parameters of the element that appears win**, or else those of the one that disappeared, so setting them on one of the two is enough.
- **An element waits 0.3 s for its partner**, then plays its own Disappear. That covers the apps that remove the old element first and add the new one at the next tick.
- **Names in control templates don't count for `MagicMoveByName`.** They are the same in every control (`Pill`, `ContentPresenter`), so they would pair parts of unrelated controls.
- **The travels of a pass wait together for a size.** The detail image of chapter 11 has no size until it loads; travelling at once made its copy shrink to a line, and making only the photo wait separated it from its title.
- **Two different texts take turns**: the old one is gone by 60% of the way and the new one starts at 40%. Cross-faded at the same time, the letters of the two headings of the wizard mixed into a blur.
- **Corners and borders change on the way; colors don't need to.** The product owner asked for `CornerRadius`, colors and other properties. Corners and borders change the shape, so both copies animate them, and the two copies keep the same shape. The two copies then add up (`plus-lighter`) instead of covering each other: two identical shapes that cross-fade that way change color exactly like a color animation, and so do gradients, images and shadows, which couldn't be animated one by one.
- **What is inside a box zooms as much as its letters change size**, not as much as the box: the avatar's "AM" goes from 20 to 13 px with its circle, and the plan card's "Pro" from 15 to 13 px while the card shrinks into a pill. Zooming as much as the box made that "Pro" 6 px. Without letters, it zooms as much as the content does.
- **A text travels by its letters, not by its box.** The photo titles are wide boxes with a short word in them: their copies were scaled by the boxes' heights and placed by the boxes, so the two words didn't cover each other, and the old one, still opaque, made the title look bolder. The same text at both ends also stretches a little, so that its copies match exactly.
- **A popup that closes tells the library**, chosen by the product owner: `PopupRoot.Close` calls `OnDetaching` for the element that holds the popup's content, with the `PopupRoot` as the parent, in the OpenSilver PR. Without it, XAML.io's AI card couldn't go back into its pill.
- **Early arrivals pair.** An element with a Magic move that appears without a partner can still pair until the end of the task, because nothing was painted yet. A popup is laid out as it opens, before a binding to its `IsOpen` collapses the element it replaces. Waiting longer would delay the Appear of every element with a Magic move. When its partner leaves, it is hidden at once, not through OpenSilver's queue of JavaScript, which can run after a paint.
- **A closing popup's Magic moves show their copies until they travel.** OpenSilver removes a popup from the page at once, so the copies of the elements inside it, kept aside as for any container that disappears, are shown above the page where the elements were, until the next layout pass makes them travel or removes them. Otherwise the page showed neither the card nor the element it goes back to for a few frames. A popup's content has no copy of its own to hold them (see "Known limitations").
- **Copies include shadow roots**, except those with media, so that an `HtmlPresenter` with `UseShadowDom`, like XAML.io's icons, isn't empty while it disappears or travels.
- **The frame of a box changes size with it; the rest travels as one picture.** In a control's template, the Border and the shapes that draw its outline fill the box. Zoomed with the content, the AI input's gradient border grew and moved like a picture instead of following the box. Only the panels and the plain shapes within 4 px of the box's sides count: what is inside them, like a card's texts, still travels as one picture.
- **Content that is centered in both boxes is held where the box moves the least.** Centered on the way, the content of the AI input went 75 px above it, while the card opened below. A badge that grows around its middle keeps its label centered.
- **Chapter 11 has toppings instead of a tab underline**, because chapter 10 already has the underline, and it's one element that moves, which `MoveAndResize` does. The toppings show what only Magic move does: an element that goes from one list to another.
- **Functions of the browser console for the settings**, chosen by the product owner: `osAnimations.slowMotion(4)` (how many times slower), and one function per switch, named after its property. They work in every app, published ones too, and last until the page is reloaded. The speed controls of the developer tools slow the page's timeline but not `performance.now()`, so the animations started late, and the library's safety timers ended them early.



## Next steps

1. **The product owner tests chapter 5 (Hover and press)** with a real mouse and a touch screen.
2. **Replace the internal `PopupRoot` check**, which is temporary. `DepartInside` checks `parent is PopupRoot`, an internal OpenSilver type, to tell a popup that closes (see "Other internals, for now" in architecture.md). It has to go before the hooks become public: OpenSilver could say it in the hooks, for example with a parameter of `OnDetaching` or a hook of its own, or the library could find a public way. The product owner chooses.
3. **The end of phase 3.** The product owner reviews the Showcase port and chapter 10 (Putting it together), and commits them. The port replaced `SpringEffect` with `Hover=Nudge`, `Animation.OnAppear` with `Appear`, `AnimatedContentControl` with `OnPageOpen=PushOthers`, and `PropertyAnimator` with `MoveAndResize`.
4. **Phase 4:** `AnimationCatalog` and `AnimationDesign`, then the playgrounds, the live XAML and the summaries in every chapter.
5. **The end of phase 5.** The product owner reviews the text effects and chapter 8 (Text that moves), and commits them.
6. **The end of phase 7.** The product owner reviews Magic move and chapter 2, and commits them.
7. **XAML.io's own interface.** The product owner tests the AI input and the side panels. XAML.io takes OpenSilver from MyGet, and the library from its local package (`Build_Local_Package.bat`, listed by its `NuGet.Config` for `OpenSilver.Animations` only) until the library is published. Then the next batches: status texts, things that appear, menus and dialogs, sliding selections, lists, and hover and press.
8. **Later:** ContentTransition.

