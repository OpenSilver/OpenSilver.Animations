# Architecture: how the built part works

This file describes the code as it is: phase 2 (MoveAndResize, with Bouncy and Wave). The full specification, including the phases not built yet, is in [design.md](design.md).

## The two halves

The library needs to see every layout pass, so OpenSilver gets a small extensibility hook. Everything else lives in this repository.

```mermaid
flowchart LR
    subgraph core ["OpenSilver core (branch feat/motion)"]
        measure["UIElement.Measure"] --> observer["LayoutObserver"]
        arrange["UIElement.Arrange"] --> observer
        visibility["OnVisibilityChanged"] --> observer
        detach["DetachVisualChildIfNotNull"] --> observer
        passEnd["UpdateLayout finally"] --> observer
    end
    subgraph lib ["OpenSilver.Animations"]
        observer --> animator["LayoutAnimator: records, origins, groups"]
        animator --> decide["Decide per group: animate or snap"]
        decide --> batch["BatchBuilder: one JS call per pass"]
        batch --> js["animations.js: Web Animations"]
    end
```

## The OpenSilver core changes

They are on branch `feat/motion` of the OpenSilver repository (`C:\VSProjects\UserwareSource\OpenSilver`), not committed yet. They don't change any behavior when no observer is registered: each call site is one null check.

| File (under `src/Runtime/`) | Change |
| --- | --- |
| `Runtime/OpenSilver/LayoutObserver.cs` | New public abstract class with virtual no-op methods. `Register(observer)` returns an `IDisposable` that unregisters it. Observers are kept in a copy-on-write array. |
| `Runtime/System.Windows/UIElement.Layout.cs` | `NotifyMeasured` in `Measure`, `NotifyArranging` and `NotifyArranged` in `Arrange`. |
| `Runtime/System.Windows/UIElement.cs` | `NotifyVisibilityChanged` at the very end of `OnVisibilityChanged`. |
| `Runtime/Core/Rendering/INTERNAL_VisualTreeManager.cs` | `NotifyDetaching` in `DetachVisualChildIfNotNull`, before `InternalDetachVisual`. |
| `Runtime/System.Windows/LayoutManager.cs` | `NotifyLayoutPassCompleted` in the `finally` block of `UpdateLayout`, before `UIRenderer.ProcessRenderQueue(true)`. |
| `Runtime/Runtime.OpenSilver.csproj` | Compiles `LayoutObserver.cs`. |
| `Scripts/cshtml5.js` | `osjs.createVisual` skips children marked `data-os-foreign` when it resolves an insertion index. |

What each hook guarantees:

- **`OnMeasured(element, availableSize, sameAvailableSize, previousDesiredSize, isFirstMeasure)`** is called only when the element was really measured, right after its new `DesiredSize` is stored.
  - Collapsed elements return earlier and are never reported.
  - Neither are elements whose measure is still valid for the same size.
- **`OnArranging(element, finalRect, previousFinalRect, isFirstArrange)`** is called only when the arrange is invalid or the final rect changed, just before `ArrangeCore`. An element whose parent arranges it again in the same rect, without its arrange being invalid, is not reported.
- **`OnArranged(element, previousVisualRect, visualRect, rendered)`** is called after `RenderLayout`.
  - The rects are relative to the visual parent: `VisualOffset` plus `RenderSize`, before and after.
  - `rendered` is false for elements whose box is not written to the DOM. These are the elements with `BypassLayoutPolicies`: `Window`, `PopupRoot`, `WindowHost` and taskbar items.
  - It is not in a `finally` block, so an exception during an arrange leaves an `OnArranging` without its pair. The pass still ends with `OnLayoutPassCompleted`, which resets the library.
- **`OnVisibilityChanged`** and **`OnDetaching`** are not used by the library yet; phase 3 (Appear and Disappear) needs them. When `OnDetaching` is called, the DOM of the element still exists, and its removal runs after any JavaScript the observer queues.
- **`OnLayoutPassCompleted`** is called once per `UpdateLayout`, before the pending JavaScript is flushed. So JavaScript queued there with `ExecuteJavaScriptVoidAsync` runs in the same flush as the layout's own writes: the browser never paints the new layout without its animations.
- The measure and arrange hooks run inside `Dispatcher.DisableProcessing()`. Observers only record data there. Anything that needs the dispatcher, such as raising the `Logged` event, is posted with `BeginInvoke`.

The library repository gets these changes through locally built OpenSilver packages; see [development.md](development.md).

## Files of the library

| File (under `src/OpenSilver.Animations/`) | Role |
| --- | --- |
| `Animate.cs` | The public entry point: the `MoveAndResize` attached property, `Animate.Changes` and `Animate.Skip`. |
| `Movement.cs` | `Movement` (abstract, a `MarkupExtension`), `Smooth`, `Bouncy`, `Movement.None`, `MovementConverter`, the `AnimationScope` enum. |
| `AnimationTime.cs` | A readable duration (`0.3s`, `300ms`, `300`), with its type converter. |
| `Easings.cs` | The curves `Smooth`, `Gentle`, `Linear` and `Bouncy`, and `EasingConverter`. The internal `CubicBezierEasing`, `LinearEasing` and `SpringEasing` classes. |
| `AnimationSettings.cs` | App-wide settings, the `Logged` event and `AnimationLoggedEventArgs`. |
| `AssemblyInfo.cs` | `XmlnsDefinition` and `XmlnsPrefix` for `http://opensilver.net/animations` with the prefix `anim`. |
| `Engine/LayoutAnimator.cs` | The `LayoutObserver`. It collects the pass, groups, decides, culls, emits and logs. Most of the logic is here. |
| `Engine/Timing.cs` | The duration and curve of a group, and their JSON form. |
| `Engine/BatchBuilder.cs` | Builds the JavaScript object sent once per pass. |
| `Engine/BrowserBridge.cs` | Loads `animations.js` and runs the batch. It keeps `IsDragging` and `PrefersReducedMotion` up to date through JavaScript callbacks. |
| `Engine/ElementNames.cs` | Names elements in messages: their `x:Name`, or their type and a bit of their text. |
| `Scripts/animations.js` | Embedded resource (`OpenSilver.Animations.animations.js`). It plays the batch with Web Animations, and tracks dragging and reduced motion. |

## Public API (built)

```xml
<UserControl xmlns:anim="http://opensilver.net/animations">
    <Button Content="Save" anim:Animate.MoveAndResize="Smooth" />
    <Border anim:Animate.MoveAndResize="{anim:Smooth Duration=0.4s, Easing=Gentle}" />
    <Border anim:Animate.MoveAndResize="{anim:Bouncy Bounciness=0.7}" />
    <StackPanel anim:Animate.MoveAndResize="{anim:Smooth ApplyTo=EverythingInside, Wave=0.8}" />
    <WrapPanel anim:Animate.MoveAndResize="{anim:Smooth ApplyTo=EverythingInside, AlsoOnWindowResize=True}" />
    <Grid anim:Animate.MoveAndResize="None" />  <!-- carved out of an area -->
</UserControl>
```

```csharp
Animate.SetMoveAndResize(card, new Smooth { Duration = AnimationTime.FromSeconds(0.4) });
Animate.SetMoveAndResize(card, "Smooth");                  // implicit conversion from string
Animate.Changes(() => Details.Visibility = Visibility.Collapsed);          // animates everything
Animate.Changes(() => Box.Width = 400, AnimationTime.FromSeconds(0.6));
Animate.Skip(() => RestoreLayout());                       // nothing animates
AnimationSettings.Current.Speed = 0.5;
AnimationSettings.Logged += (s, e) => Debug.WriteLine(e.Message);
```

- **`Animate.MoveAndResize`** (attached, type `Movement`, not inherited).
  - Setting it starts the engine: it registers the observer and loads the script, once. The engine never starts in a designer (`DesignerProperties.IsInDesignTool`).
  - A value with `ApplyTo=EverythingInside`, or `None`, turns on the area lookup (`LayoutAnimator.AnyAreas`). Until then, ancestors are never searched.
- **`Movement` parameters.**
  - `Duration` (`AnimationTime?`): clamped to 0.05 to 3 s when used. The default is `AnimationSettings.DefaultDuration`, doubled for `Bouncy`.
  - `Easing` (`IEasingFunction`, names through `EasingConverter`): used by `Smooth`.
  - `Bounciness` (0.1 to 1, default 0.5): used by `Bouncy`, and by `Smooth` with `Easing=Bouncy`.
  - `Wave` (0 to 1, default 0).
  - `ApplyTo` (`ThisElement` or `EverythingInside`).
  - `AlsoOnWindowResize` (default false).
- **Value forms.** A `Movement` is a `MarkupExtension` whose `ProvideValue` returns itself. So it works as a string (`"Smooth"`, through `MovementConverter`), as a markup extension with parameters, as an element, and as a resource. `Movement.Parse` accepts `Smooth`, `Bouncy`, `None` and the empty string.
- **`Animate.Changes(action, duration?)`** first runs `UpdateLayout()`, so that pending layout is attributed to earlier changes. Then it runs the action and `UpdateLayout()` again, inside a scope where every change animates, even without settings, with the Smooth curve.
- **`Animate.Skip(action)`** does the same with a scope where nothing animates.
- **Nested scopes:** the inner one applies until it exits, then the outer one applies again.
- **`AnimationSettings`**:
  - `Current` is read lazily. The first time, it comes from the application resource with the key `"AnimationSettings"`, if there is one (the gallery declares `<anim:AnimationSettings x:Key="AnimationSettings" />` in App.xaml).
  - Properties: `IsEnabled`, `RespectReducedMotion`, `SimulateReducedMotion`, `Speed` (0.05 to 20), `DefaultDuration` (0.25 s), `LogForDebugging` and `ShowWhatAnimates`.
  - It implements `INotifyPropertyChanged`, so the gallery's toolbar binds to it.
  - The static `Logged` event carries the same messages as `LogForDebugging`, raised with `BeginInvoke` after the pass.
- **Curves.**
  - `Easings.Smooth` is `cubic-bezier(0.2, 0, 0, 1)`, the default.
  - `Easings.Gentle` is `cubic-bezier(0.45, 0, 0.55, 1)`.
  - `Easings.Linear`.
  - `Easings.Bouncy` is a damped spring. Its damping ratio is `1 - 0.8 × Bounciness`, and it is within 0.1% of the target at the end of the duration.
  - Any other `IEasingFunction` is sampled into 33 points.

## One layout pass, step by step

`LayoutAnimator` keeps per-pass state in reused arrays and dictionaries, and clears them at the end of every pass (`Reset`). The terms below are used everywhere in the code and in the messages.

### 1. Measure: finding where a change started

`OnMeasured` ignores first measures. For the others:

- `_measured` records every element measured in the pass. A second measure of the same element in the same pass is "measured again".
- **A scrolled element is ignored.** This is an `IScrollInfo` whose offsets changed since the last check (`IsScrolled`, which stores offsets in a `ConditionalWeakTable` and caches the answer per pass).
- **A different available size means the element is a consequence**, not where the change started, so it is ignored.
- **Carriers.** When the desired size changed, the layout parent goes into `_carriers`. A carrier is only passing its child's change upward.
- **Measure origins.** An element measured with the same available size, for the first time in the pass, that is not a carrier, goes into `_measureOrigins`.
  - Example: a Button whose label got longer. The TextBlock is the origin, and the Button and its panel are carriers.
  - Example: a Border whose `Width` was set. The Border is the origin; its parent is a carrier.
  - Only the first measure counts, because a later measure in the same pass is the layout settling: a ScrollViewer measures its content again after showing a scroll bar.

### 2. Arrange: records, stack and groups

`OnArranging` pushes a **record** on a stack. The record holds the element, its parent record (the element below it on the stack), its old and new rects, flags and a group id.

- **Appearing elements.** An element arranged for the first time gets `FirstArrange`. Its descendants arranged for the first time in the same pass get no record at all: everything inside an element that appears, appears with it. The time it appeared is stored in `_appeared`, for the settling rule below.
- **Scrolled elements** get `Scrolled`.
- **Origins** get `Origin`: a measure origin, or an element arranged again in the same rect without being measured (an arrange-only change, such as `Grid.Column` or an alignment). A window-resize pass has no origins at all (see below).

`OnArranged` pops the record.

- It keeps the first old rect of the pass and the last new rect.
- It sets `NotRendered` when the rect changed but nothing was written (`rendered` false). Such elements are never animated.
- An origin creates a **Change group**.
- A Change group then joins the parent record's group (union-find: `NewGroup`, `Find`, `Union`). So a change climbs through every ancestor that was arranged again. A WindowResize group never climbs.

When the stack is empty, the arrange session is over. `EndArrangeSession` walks the session's records in arrange order (pre-order):

- A record without a group takes its parent's group, unless the parent is scrolled. So everything arranged by a member of a group belongs to it.
- A record still without a group whose rect changed, and whose effective movement has `AlsoOnWindowResize`, starts a **WindowResize group**.
- When a Change group and a WindowResize group merge, the Change group wins: the change keeps its own timing.

### 3. End of the pass: deciding per group

`OnLayoutPassCompleted` prunes `_appeared`, and does nothing more if there are no records and no scope. Otherwise `CompletePass` runs, inside a `try`/`catch` that writes any exception to the console and never breaks the app.

1. **Global snap reason**, checked first. If any of these holds, every group snaps:
   - `Animate.Skip`;
   - `IsEnabled` is false;
   - a designer;
   - reduced motion, real (the `prefers-reduced-motion` media query, if `RespectReducedMotion`) or simulated (`SimulateReducedMotion`);
   - `BrowserBridge.IsDragging`.
2. **Buckets.** For each record that changed or is an origin, except appearing ones:
   - without a group, a change snaps;
   - otherwise it goes into its group's bucket, as an origin and/or a member (a member is a record whose rect changed).
3. **`ProcessGroup`** for each bucket that has members:
   - **A WindowResize group** (no origins) animates with the timing of the element that opted in.
   - **A Change group** goes through `Decide`, described below.
   - **Culling and budget** (`CullAndCount`). A member is left out when both its old and new absolute rects are outside the window, with a margin of 100 px. If more than 300 members remain, the whole group snaps. If none remain, nothing happens.
   - **`Emit`** adds the moves to the batch, or **`Snap`** cancels the running animations of the members.
4. **The batch** is sent with one `ExecuteJavaScriptVoidAsync` call: moves, cancels and outlines.
5. **Messages** are published (see "Messages").

### Decide: does a Change group animate, and with which timing?

It goes through the origins. When not inside `Animate.Changes`, an origin is excluded for any of these reasons, in this order:

- **Typing.** The origin is strictly inside the focused `TextBox`, `PasswordBox` or `RichTextBox`. The text box itself growing is not excluded: it is a member, not the origin.
- **Scroll chrome** (silent). The origin is a `ScrollBar` or inside one, up to 8 levels up, or its `TemplatedParent` is a `ScrollViewer`. These parts only change as a consequence of the content.
- **Settling** (silent). The origin, or one of its ancestors, appeared less than 500 ms ago. Right after a page, a dialog or new items appear, scroll bars show up and fonts and images load.
- **Carved out.** The origin's area is `None`.

If every origin is excluded, the group snaps with the last reason. Otherwise the group animates when one of these gives a timing, checked in this order:

1. `Animate.Changes`: its duration, or the default, with the Smooth curve.
2. The origin's own setting. It counts even when the origin's own box doesn't change. That's needed for a ChildWindow, whose `Width` change resizes its template while the ChildWindow itself keeps the page size.
3. The origin's area (`EverythingInside` on an ancestor).
4. **The nearest ancestor that has a setting and is a member** (`FindAncestorMovement` with `IsMember`). An ancestor that stays in place doesn't count, because `ThisElement` is about its own box. Example: the Save button resizes because its label grew, so the button's setting gives the timing.
5. **The member with an effective setting that is the fewest tree hops away** from the origin (`FindNearestMemberMovement`, through the records' parent chains). Example: a tab underline with `Bouncy` moves because its Grid rearranged its columns.

Otherwise it snaps: "no element involved has MoveAndResize".

The origin named in messages, and used for the wave, is the origin that has its own setting, if any. Otherwise it is the first eligible origin, preferring an element of the page over a part of a control template (`IsTemplatePart`: `TemplatedParent != null`).

### Areas

`GetArea(element)` walks up `VisualTreeHelper.GetParent` until it finds one of:

- `EverythingInside`: the element is inside that area, with its movement;
- `None`: the element is carved out;
- the root: no area.

`ThisElement` settings are passed over. Results are memoized per pass for every element on the walked path (`_areaCache`), so each element is visited at most once per pass. Popups are boundaries, because `GetParent` returns null at a popup's root.

`GetEffectiveMovement(element)` is the element's own setting (null for `None`), or else its area's movement. Members use it, so a change that starts outside an area but moves something inside it animates.

### Window-resize passes

`IsWindowResizePass` compares `Application.Current.Host.Content.ActualWidth`/`ActualHeight` with the size seen at the previous pass. The Host's ResizeObserver updates it before the layout pass runs. It is evaluated lazily, once per pass, from `OnArranging`.

In such a pass there are no origins: everything follows the window. Only the elements that set `AlsoOnWindowResize`, and their content, form WindowResize groups. Without this rule, the scroll bar parts that measure again in a resize pass would look like changes. Their group would climb to the root and snap the whole page, or animate it.

### Wave

When the timing has `Wave > 0`, each member gets a delay.

- **The formula** is `Wave × 200 ms × min(1, distance / 600 px)`, divided by `Speed`. The distance is between the absolute rects of the member and the origin: 0 when they overlap.
- **Delays are inherited.** A member inside another member shares that member's delay. The origins and their descendants start at once.
- **The origin's ancestors pass nothing down** (`_chainStamp`), so the elements next to the origin each get their own delay.
- `_waveStamp` changes for each emitted group, so that stamps don't leak between groups.

### Absolute positions

`ComputeAbsolute` adds the record rects along the parent chain. For the root of an arrange session, it adds `TransformToVisual(null)` of its visual parent. Scroll offsets are included, because a `ScrollContentPresenter` arranges its content at the negative offset. Positions are computed only for culling and the wave, and memoized with `_stamp`.

### Running animations and cancels

`_runningUntil` remembers, for each animated element, when its animation ends (delay plus duration plus 50 ms).

- When such an element changes again and snaps, its animation is cancelled in the same batch, so it doesn't fight the new position.
- Entries are removed on detach, and swept every 2 s.
- In the browser, a new animation on an element that is still animating starts from where the element is on screen.

### Messages

Messages are produced only when `LogForDebugging` is on or `Logged` has handlers.

- **An animated group:** "TextBlock "Saving..." changed size; 10 elements animate together (Smooth, 0.25 s, from the MoveAndResize of SaveButton)."
- **A window-resize group:** "The window was resized; FollowingTiles follows it (Smooth, 0.25 s, from the AlsoOnWindowResize of FollowingTiles)." At most one per second.
- **A snap:** "SliderBar changed size; nothing animated because something is being dragged."
  - Only snaps that would otherwise have animated are explained: inside `Animate.Changes`, or when an origin or a member has an effective setting (`InvolvesSettings`).
  - Scroll chrome and settling snap silently.
  - The same message is written at most once per second (typing and dragging repeat it).
- The snap reasons are: `Animate.Skip was used`, `AnimationSettings.IsEnabled is false`, `the app runs in a designer`, `the device asks for reduced motion`, `reduced motion is simulated (AnimationSettings.SimulateReducedMotion)`, `something is being dragged`, `it came from resizing the window or from outside the elements that have settings`, `the change came from typing in X`, `the change started inside an element with MoveAndResize=None`, `no element involved has MoveAndResize`, and `more than 300 elements would animate at once`.
- `LogForDebugging` writes to the console with the prefix `[OpenSilver.Animations]`. The `Logged` event is raised with `Dispatcher.BeginInvoke` after the pass, so handlers can change the page. A handler that changes an animated element starts a new pass. Avoid logging into a list that is inside an animated area, or each message would cause another one.

### Constants

These are at the top of `LayoutAnimator`:

| Constant | Value | Meaning |
| --- | --- | --- |
| `MaxAnimatedElements` | 300 | The budget per group, after culling. |
| `MaxWaveDelayMs` | 200 | The delay at full wave and full distance. |
| `WaveDistance` | 600 | The distance (px) that gets the full delay. |
| `CullMargin` | 100 | The margin (px) around the window for culling. |
| `Tolerance` | 0.05 | Two lengths closer than this (px) are equal. |
| `SettleMs` | 500 | How long, after appearing, changes count as settling. |

## The browser side (`Scripts/animations.js`)

`osAnimations.init(dragStarted, dragEnded, reducedMotionOn, reducedMotionOff)` runs once and installs:

- **The drag tracker.** Capture listeners on `window` for `pointerdown`, `pointermove`, `pointerup` and `pointercancel`. A drag starts when a button is held and the pointer moved more than 4 px.
- **The reduced-motion listener**, on `matchMedia('(prefers-reduced-motion: reduce)')`.

`osAnimations.run(batch)` plays one pass. The batch is a JavaScript object literal built by `BatchBuilder`:

```text
{ t: [ { d: durationMs, e?: cssEasing, s?: [samples], se?: sizeEasing } ],
  m: [ [id, timingIndex, delayMs, dx, dy, oldWidth, oldHeight, newWidth, newHeight, flags] ],
  c: [ ids to cancel ],
  o: [ [id, durationMs, delayMs, isOrigin] ] }
```

- **Elements** are found by the id of their outer div (`Interop.GetDiv(element).Uid`).
- **Moves.** The core has already written the final `left`/`top`/`width`/`height`. The library only animates on top of them, and never touches a dependency property.
  - Position: `translate` from `(dx, dy)` to 0 with `composite: 'add'`.
  - Size: `width`/`height` from the old size to the new one.
  - Delays use `fill: 'backwards'`, so a delayed element shows its old place until it starts.
- **Flags.**
  - `1` (`ClipOwnBox`): the element's clip is exactly its own box, as with `ClipToBounds` or a `ScrollContentPresenter`. The size animation holds `clip-path: inset(0px)` so that the clip follows the animated box.
  - `2` (`Rectangle`) and `4` (`Ellipse`): a shape with `Stretch=Fill`. Its SVG geometry (`width`/`height`, or `rx`/`ry`/`cx`/`cy`) is animated too, where the browser supports these CSS properties. `Path` geometry always snaps.
- **Curves.** A CSS easing (`cubic-bezier`, `linear`), or samples. Samples play as CSS `linear(...)` where supported, and are otherwise baked into keyframe offsets. `Bouncy` moves use spring samples, and its sizes use the Smooth cubic-bezier (`se`), so content doesn't wobble.
- **Interruptions.** Running animations are kept per element in a `WeakMap`. A new animation reads the eased progress of the previous one (`getComputedTiming().progress`) and starts from the current offset and size, then cancels the previous one.
- **Outlines** (`ShowWhatAnimates`) animate `outline` on the element: orange for the origin, pink for members. They last at least 700 ms.

## Things to know before changing the engine

- **The hot path.** The hooks are called for every measured and arranged element of every pass. Keep them to set and dictionary operations, with no allocations and no tree walks. Tree walks (areas, typing, settling, scroll chrome, ancestors) only happen in `CompletePass`, for origins and members.
- **Final values are reported at once.** `ActualWidth`, `TransformToVisual` and `SizeChanged` report final values immediately. Anything that app code positions from them jumps to its final place.
- **ChildWindow.** While it's open, its `Width` reads as the width of the page (`UpdateOverlaySize`). Code that toggles it must keep its own state, as `ResizableDialog` does.
- **DOM order.** Children created through `osjs.createLayout`, such as the containers of an `ItemsControl`, are always appended. So DOM order can differ from visual order, for example in the gallery's log, where new entries are inserted at the top. That's existing OpenSilver behavior. Don't rely on DOM order.
- **Element classes.** In Debug builds of an app (`Features.DOM.AssignClass`), each element's div also has its type name as a CSS class, for example `System.Windows.Controls.Slider`. Release builds don't have them.
- **The Simulator.** JavaScript batches can arrive a frame apart there, so an animation can start one frame late.
