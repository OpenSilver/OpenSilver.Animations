# Proposal: clearer scopes for moves, resizes and boundaries

**Status: proposal, not implemented.** Written on November 2, 2026, after a brainstorm with the product owner. The library was at version 0.2.0, published on NuGet. Breaking changes are accepted for this revision. Nothing in this document has been coded yet.

This document is meant to be enough to resume the work in a new chat, without the brainstorm's history. It gives:

- the context, and how the engine decides today;
- the problems found, with the examples that showed them;
- the decisions already made by the product owner;
- the proposed API, and its rules in four sentences, with their precise meaning;
- worked examples, which are also the acceptance tests;
- an implementation plan, file by file;
- everything else that has to change: docs, gallery, XAML.io, the AI agent's skills;
- the open questions, and the alternatives that were rejected, with the reasons.

Before implementing, also read [AGENTS.md](../AGENTS.md) (the working agreement: never commit, beginners first, keep the docs current), [architecture.md](architecture.md) (sections "One layout pass, step by step", "Decide", "Areas" and "Elements that appear and disappear") and [development.md](development.md) (build, gallery, browser testing).

## 1. Context

### Where things are

| What | Where |
| --- | --- |
| The library | `C:\VSProjects\UserwareSource\OpenSilver\OpenSilver.Animations`, its own git repository, branch `master`, version 0.2.0. |
| The engine | `src/OpenSilver.Animations/Engine/LayoutAnimator.cs` (about 4,400 lines), with `LayoutAnimator.MagicMove.cs`, `Reactions.cs`, `TextChanges.cs`, `PageOpenHolds.cs`, `Timing.cs`, `BatchBuilder.cs`, `BrowserBridge.cs`, and `Scripts/animations.js`. |
| The public API | `src/OpenSilver.Animations/Animate.cs` (the attached properties), `Movement.cs` (`Smooth`, `Bouncy`, `None`, `ApplyTo`, `AnimationScope`), `Transition.cs`, `TextTransition.cs`, `Reaction.cs`, `MagicMove.cs`, `AnimationSettings.cs`. |
| The gallery | `samples/OpenSilver.Animations.Gallery` (the tour and manual test suite), run at http://localhost:55620. |
| XAML.io | `C:\VSProjects\UserwareSource\OpenSilver\Userware.XamlDesigner`, branch `feat/animations`. It references the published 0.2.0 package. Its Animations panel is in `src/Userware.OnlineIde/Animations/` (`AnimationCatalog.cs`, `AnimationsPanel.xaml` and `.xaml.cs`, `AnimationDrawing.cs`). |
| The AI agent's skills | `C:\VSProjects\UserwareSource\OpenSilver\Userware.AiAgent`, branch `feat/add-opensilver-animations-skills`, folder `skills/opensilver/`. |

### The API today, in short

- **Element settings**, attached on `Animate`: `Appear`, `Disappear`, `MoveAndResize`, `TextChange`, `Hover`, `Press`, `MagicMove`, `MagicMoveByName`.
- **Item settings**, on a Panel or an ItemsControl, for each of its items: `ItemsAppear`, `ItemsDisappear`, `ItemsMoveAndResize`, `ItemsCascade`. An item's own setting wins.
- **`MoveAndResize` values:** `Smooth`, `Bouncy` or `None`, with the parameters `Duration`, `Easing`, `Bounciness`, `Wave`, `AlsoOnWindowResize` and `ApplyTo`.
  - `ApplyTo=ThisElement`, the default: the element animates whenever it moves or changes size.
  - `ApplyTo=EverythingInside`: "exactly as if every element inside, at any depth, had `MoveAndResize` with the same settings". It includes the element itself. It adds automatic effects: elements inside that appear or disappear without an effect of their own wipe or fade. This is called an **area** in the code.
  - `MoveAndResize=None` on an element inside an area **carves it out**: changes that start in it or inside it never animate, and a change never climbs out of it.
- **Code:** `Animate.Changes(action, duration?)` animates every change made in the action, and `Animate.Skip(action)` makes them instant. `AnimationSettings.IsEnabled` turns everything off, app-wide. `Animate.PlayHover` and `Animate.PlayPress` play a reaction once, for previews.

### How the engine decides today

These terms are the ones used in the code and in architecture.md, and in the rest of this document.

- **A change** is one thing that changes in a layout pass, plus everything it pushes. An element appears, its text gets longer, its `Width` or `Grid.Row` is set, and so on.
- **The origin** is where a change starts. The measure hook finds it: the element re-measured with the same available size, whose desired size changed, and that isn't only carrying a child's change upward. An element that appears or disappears is a change of its parent: the parent is the origin.
- **The group** is the origin, the ancestors that were arranged again because of it, and every element whose slot changed because of them. In the code, a Change group climbs from each record to its parent's record (union-find, in `OnArranged`). Two changes in the same layout pass that climb to a common ancestor merge into one group.
- **Members** are the elements of the group whose rectangle changed. Their content rides along without animations of its own.
- **Deciding** (`Decide`, at the end of the pass). The whole group animates, with one duration and one curve, when one of these gives a timing:
  1. `Animate.Changes`;
  2. an effect of an element that appears or disappears (`bucket.Effect`);
  3. the nearest setting going up from an origin (`FindNearestMovement`): the origin's own setting, or an ancestor with `EverythingInside`, or an ancestor with another setting that is a member. The walk stops at `None` and at a popup's root;
  4. **the member with a setting that is the fewest tree hops away from the origin** (`FindNearestMemberMovement`).

  Otherwise the group snaps. Point 4 is the source of the "contagion" described below.
- **One timing per group** is what keeps the containment guarantee: everything shares the start, the duration and the curve, so a child that is inside its parent before and after the change stays inside it at every frame.
- **Never animates:** window resizes (except with `AlsoOnWindowResize`), the 0.5 s after an element appears (settling), scroll chrome, scrolling, dragging, typing, reduced motion, the designer, `IsEnabled` false, `Animate.Skip`, and origins under `None`.
- **Performance:** an element with no setting, outside any area and outside a group, costs one hash lookup per hook. Only changed rectangles get animations. Elements outside the window are culled, and a group with more than about 300 elements left snaps.

### What the brainstorm clarified about today's behavior

These answers came out of the brainstorm, and are worth keeping because they explain the problems below.

- **An `Appear` doesn't make the whole UI animate.** Only what that appearance pushes glides: the elements around it, the containers that grow, and what those push in turn. Inside a container with a fixed size, nothing outside moves. In a `StackPanel` near the top of the page, everything below it glides.
- **`Appear` and `MoveAndResize` overlap.** Both are triggers for the same mechanism: "everything this change touches glides together". `Appear` fires when the element appears. `MoveAndResize` fires when anything moves or resizes the element, for any reason, including changes that start elsewhere.
- **A control can move without its own content changing.** A centered Button whose label grows moves its left edge by half the growth. An element moves when something above it grows, when its `Margin`, `Canvas.Left`, `Grid.Row` or alignment changes, or when its parent is resized.
- **Move and resize can't be two separate properties.** An element's move and its resize are one animation of its box, with one duration and one curve. With `Move="None"` and `Resize="Smooth"`, a centered button whose label grows would jump halfway, then grow to the right. With two different curves, the edges of the box would follow different curves, and the content would slide around in its own box.
- **A timer elsewhere is usually not affected.** Its change is a separate change, and is instant unless it touches a setting itself. Two exceptions: if it lands in the same layout pass and climbs to a common ancestor, it merges with the animated group. If it moves an element that is still gliding, that element's animation is cancelled and it jumps to its final place.

## 2. The problems

1. **Contagion.** A setting on one element makes changes that start elsewhere glide, but only the changes that happen to move that element. Example: a `StackPanel` with children A, B and C, from top to bottom, where only B has `MoveAndResize="Smooth"`.
   - A gets taller: B is pushed down, so the whole change glides, including A, C, the StackPanel and what is below it.
   - C gets taller: A and B don't move. Nothing with a setting is touched, so C and what is below it jump.

   Reading the XAML, nobody can predict that.
2. **Two ways to animate the layout, with a subtle difference.** `MoveAndResize` on an element, and `ApplyTo=EverythingInside` on a container. Setting it on one child and setting it on the panel give different results, as above. The name `EverythingInside` also doesn't say that it includes the element itself.
3. **No encapsulation for component authors.** A ComboBox author who makes a glyph `Pop` in its template can't keep that animation from making the app's page glide, if the glyph changes the ComboBox's size. The author wants a guarantee that the animations of the control never affect the rest of the app, for peace of mind and separation of concerns.
4. **Excluding part of the tree uses `MoveAndResize="None"`,** which has a hidden extra meaning (carve-out), only covers moves and resizes, and doesn't beat the settings inside the carved-out element. XAML.io's `AnimationDrawing` uses it to keep its previews out of the panel's area.
5. **TextChange is a special case.** A counter that rolls to a wider number needs an extra `MoveAndResize` on the TextBlock or its container for its neighbors to follow. Nobody would guess it.
6. **The timing rule takes several cases to explain** (points 1 to 4 of "Deciding" above).

## 3. Decisions already made

- **The benefits are accepted:** no contagion, encapsulation, an explicit off switch, TextChange pushing its neighbors, and rules that fit in four sentences. A sixth one, a name without "Move" for size changes, was dropped with the next decision.
- **Keep the names `MoveAndResize` and `ItemsMoveAndResize`.** Since move and resize can't be separated (see above), a new name like `LayoutChange` brings nothing.
- **"Items", not "Children",** for the settings of a Panel or an ItemsControl. That is already the case.
- **A boundary property, `anim:Animate.IsAnimationScope="True"`.** It follows the WPF pattern of `Grid.IsSharedSizeScope` and `FocusManager.IsFocusScope`.
- **A separate off switch, `anim:Animate.DisableAllAnimations="True"`.** The product owner suggested this name "or something similar", and expects it to be rarely used. It is on the `Animate` class, like the other properties.
- **Overlaps are acceptable in the new cases** (an element that glides alone, the edge of a scope). Apps already get them with `RenderTransform` animations.
- **Breaking changes are fine.** No compatibility layer.

## 4. The proposed API

### Element settings: unchanged

`Appear`, `Disappear`, `MoveAndResize`, `TextChange`, `Hover`, `Press`, `MagicMove` and `MagicMoveByName` keep their names, values and parameters. What changes is the meaning of `MoveAndResize` on a pushed element (rule 4) and of `MoveAndResize="None"` (see below).

### Item settings: unchanged

`ItemsAppear`, `ItemsDisappear`, `ItemsMoveAndResize` and `ItemsCascade` keep their names and meaning. `ItemsMoveAndResize` covers items that move and items that resize, because in a list it is rarely only one of the two.

### The region: `ApplyTo=ThisAndEverythingInside` (proposed rename, to confirm)

```xml
<Border anim:Animate.MoveAndResize="{anim:Smooth ApplyTo=ThisAndEverythingInside}">
```

- It means the same as today's `EverythingInside`: as if the element itself and every element inside it, at any depth, including templates and items created later, had `MoveAndResize` with these settings. It also gives the automatic wipe or fade to the elements inside that appear or disappear without an effect of their own.
- The new name says that the element itself is included, which the product owner found unclear. The values of `AnimationScope` become `ThisElement` (the default) and `ThisAndEverythingInside`.
- It's meant for a region, like a form, a card or a settings page. On the app's root, every change of the app would glide. That isn't a performance problem, since the cost depends on how many elements change, not on how many there are, but it changes the feel of the whole app.

### New: `IsAnimationScope`

```xml
<!-- In the ComboBox template -->
<Grid x:Name="Root" anim:Animate.IsAnimationScope="True">
    <Path x:Name="Glyph" anim:Animate.Appear="Pop"/>
</Grid>
```

"Animations that start inside this element stay inside it."

- **The changes that start inside it animate only inside it.** The element's own box, and everything around it, take their new place and size at once, exactly as they would without the library. So the host of a component is never affected by the component's animations.
- **The element's own settings work as usual.** For example, an `Appear` that the app sets on the ComboBox control itself. Only the changes that start *inside* the scope element are contained. Note that in a template, the property is on the template's root, which is inside the control.
- **A change from outside that pushes or resizes the scope element still carries it,** like any element it pushes, and what is inside follows that change.
- **The only side effect is visual.** When the inside is still shrinking while the box has already taken its smaller size, the inside can overlap a neighbor for a moment, like a `RenderTransform` animation. Optionally, the engine could clip the inside to its box while it animates; that only touches the component's own elements (open question 7).
- **Content that the app places in the control,** like the selected item of a ComboBox shown through its `ContentPresenter`, is inside the scope too: from outside, the control is one box (open question 5).

### New: `DisableAllAnimations`

```xml
<vendor:Chart anim:Animate.DisableAllAnimations="True"/>
```

"No animation plays in this element or inside it, whatever is set inside."

- It turns off every kind: moves and resizes, appear and disappear effects (own, item and automatic), text changes, hover and press, Magic moves, and page-open holds.
- **It wins from above**, like `IsEnabled` for input: a disabled panel disables its children even when they say `IsEnabled="True"`. It beats every setting inside, including a vendor's.
- **A change that starts inside never animates,** and what it pushes outside jumps with it. A change never climbs out of it, so it never merges with a change outside in the same pass. That's the lesson of chapter 7's chart (see "A change never crosses None" in architecture.md).
- **A change from outside still carries it** (proposed, open question 9): it moves and resizes with that change like any element the change pushes, and what is inside follows.
- It replaces today's carve-out with `MoveAndResize="None"`.

### The code API

- `Animate.GetIsAnimationScope` and `SetIsAnimationScope`, `Animate.GetDisableAllAnimations` and `SetDisableAllAnimations`, like the other attached properties.
- `Animate.Changes`, `Animate.Skip`, `Animate.PlayHover`, `Animate.PlayPress` and `AnimationSettings` don't change.

### What changes meaning

- **`MoveAndResize="None"` becomes local.** It no longer carves out the subtree.
  - It means: "this element doesn't give a timing". When the search for a timing, going up from where a change started, finds `None` first, the change is instant. That's how today's `FindNearestMovement` already treats `None` (it stops the walk).
  - It's also how an element opts out of a region: rule 1 makes its own `None` beat the region.
  - It doesn't stop changes that start in its children: they find their own setting, or the region's, first.
  - When an animated change carries the element, it follows like any pushed element.
  - To turn off a whole subtree, use `DisableAllAnimations`.
- **`ItemsMoveAndResize="None"`** means the same for each item.
- **A setting on a pushed element no longer makes the change that pushed it glide** (rule 4).
- **`TextChange` gives a timing to the change of its TextBlock's size,** like an appear effect does, so the neighbors follow without `MoveAndResize` (benefit 5).

## 5. The rules, in four sentences

1. **The closest setting wins:** an element's own setting, then the Items setting of the list or panel it is in, then the nearest `MoveAndResize` with `ApplyTo=ThisAndEverythingInside` above it or on it.
2. **`DisableAllAnimations` turns off every animation of an element and of everything inside it,** whatever is set inside.
3. **When a change animates, it plays with the setting found closest to where it started, and everything it pushes follows with that timing,** up to an animation scope, or wherever the layout stops the push.
4. **A setting decides how its own element changes; it never makes the changes of other elements animate.**

### Rule 1, precisely

For a given element and a given kind of setting (appear, disappear, move and resize, text change, hover, press), the effective setting is the first one found among:

1. the element's own value, including `None`;
2. the matching Items setting of the list it is an item of: its visual parent is a Panel with item settings, or the items panel of an ItemsControl that has them (today's `GetListOf`);
3. the nearest region above the element or on it. A region is a `MoveAndResize` with `ApplyTo=ThisAndEverythingInside`. For appear and disappear, the region gives the automatic wipe or fade. A `ThisElement` setting on an ancestor is passed over: it is about the ancestor's own box.

If none is found, the element has no setting of that kind. This is today's `GetOwnMovement` followed by `GetArea`, without the carve-out. Like a local value in WPF, which beats a style or an inherited value, an element's own `MoveAndResize="Bouncy"` beats the region on the same element.

### Rule 2, precisely

- An element is disabled when it, or any ancestor up to a popup's root, has `DisableAllAnimations="True"`.
- An origin in a disabled element is excluded in `Decide`, with a message.
- A disabled element doesn't play its own or automatic effects, text changes, reactions or Magic moves.
- The group doesn't climb from a disabled element to its parent.

### Rule 3, precisely

- **The timing search.** Going up from each origin, look at the origin itself, even when its own box doesn't change, then at each ancestor that is a member of the change. That means only ancestors that moved or changed size, not ancestors that stayed in place. The first effective setting found (rule 1) gives the timing of the whole change.
  - An appear or disappear effect, and a text change, count as the setting of the element where the change starts. They come first, as `bucket.Effect` does today.
  - The search stops at a popup's root, and after an `IsAnimationScope` element. The scope element's own region setting still counts for what's inside it.
  - When several origins are in the same group, the setting found the fewest levels up wins. On a tie, an element of the page beats a part of a control template. This is today's rule.
- **What it pushes follows.** Every member of the group animates with that timing, whatever its own setting. Their own settings only matter for changes that start at them, or when an unanimated change moves them (rule 4).
- **Where the push stops.** A push stops where the layout stops it: a container with a fixed size, a `ScrollViewer` (only its scroll bar changes), or a popup. It also stops at an `IsAnimationScope` element. The group doesn't climb from the scope element to its parent, the scope element's own box snaps, and its parent and everything outside snap too.
- **`Animate.Changes`** still forces a timing on everything changed in its action.

### Rule 4, precisely

- **When a group finds no timing,** each member that has an effective `MoveAndResize` (rule 1) other than `None` glides alone, with its own setting. This is called a **solo glide**. Only the outermost such members glide, and their content rides along. Everything else in the group snaps.
- **A solo glide goes from where the element was on screen to where it is now.** Its absolute rectangles are used, because its parent may have jumped. It may overlap a neighbor for a moment.
- **Solo glides only replace the "no setting found" case.** A group that snaps for a reason from the "never animates" list (typing, scroll chrome, settling, disabled, window resize, dragging, reduced motion, the designer, `IsEnabled` false, `Animate.Skip`) has no solo glides either.
- **This removes the member fallback (`FindNearestMemberMovement`) from the decision.** It may still be used to find the `Wave` of the others when an effect pushes them, which is about how they move, not whether.

## 6. Worked examples

These are the expected behaviors, and the acceptance tests of the implementation. Each one should become a gallery scenario or a "What should happen" line.

1. **The StackPanel with A, B and C, where only B has `MoveAndResize="Smooth"`.**
   - A gets taller with no setting: the change finds no timing. B glides alone, from its old place down to its new one. A, C and everything else jump. Today, everything glides.
   - C gets taller: everything jumps, as today.
   - B's own content grows: B is the first element with a setting going up from its content, so the change glides with B's setting, and C and everything below follow.
   - With `ItemsMoveAndResize="Smooth"` on the StackPanel instead, all three cases glide: each child gets the setting through rule 1.
2. **A button with `MoveAndResize="Smooth"` whose label gets longer.** The TextBlock inside has no setting, so going up, the button is the first element with one. The button grows smoothly, and the label next to it follows with the same timing, even if that label has its own `Bouncy`.
3. **A Border with a region `Smooth` and its own `MoveAndResize="Bouncy"`** (open question 2).
   - Its own `Width` is set: the Border is the origin, its own setting wins over the region (rule 1), and the change bounces.
   - Its content grows: the content gets `Smooth` from the region and is the first setting found, so the change is smooth, and the Border follows smoothly.
4. **One element of a Grid with `MoveAndResize="Smooth"` whose `Grid.Row` changes.** It is the origin, with its own setting: it glides to its new row. The other children of the Grid don't move, so nothing else animates. If its move changes the height of `Auto` rows, what those push follows with its timing.
5. **The ComboBox glyph with `IsAnimationScope` on the template root.** The glyph pops.
   - If the ComboBox's size stays the same, nothing else happens.
   - If the glyph makes the ComboBox wider, the ComboBox's box and the app's page take their new place at once, and only the inside of the template glides.
   - The app's `Appear` on the ComboBox control itself still plays, and pushes the page smoothly, as usual.
6. **A vendor chart with `DisableAllAnimations`.**
   - Its bars change on Refresh: they jump, its own `Hover` and `TextChange` don't play, and if the chart's size changes, what it pushes outside jumps.
   - A panel above it appears with `Appear="Fade"`: the chart is pushed down and glides with the panel's timing, like any pushed element.
7. **A counter with `TextChange="Roll"` that goes from 99 to 100.** The digits roll, the TextBlock gets wider, and the label next to it glides with the text change's timing, with no `MoveAndResize` anywhere.
8. **A details panel with `Appear="Fade"` in a page.**
   - What it pushes glides: the elements below it, the containers that grow, and what those push.
   - Inside a container with a fixed height, nothing outside moves.
   - Inside a `ScrollViewer`, only its scroll bar changes outside.
9. **A child with `Appear="{anim:Slide From=Right}"` inside a region.** Its own effect wins over the automatic wipe (rule 1). The others make room with the slide's timing (rule 3).
10. **A child with `Appear="None"` inside a region.** It appears at once. Whether the others still make room smoothly is open question 3. The recommendation is yes, as today.
11. **Nested regions.** An inner region with `Bouncy` inside an outer `Smooth` one. The changes that start inside the inner region bounce, and what they push in the outer region follows with the same Bouncy timing. The changes that start in the outer region, outside the inner one, are smooth.
12. **Chapter 7's dashboard.** A region on the dashboard, and `DisableAllAnimations` on the chart instead of today's `MoveAndResize="None"`. The numbers animate, the bars snap, and on every Refresh the bars' change never joins the numbers' change.
13. **XAML.io's Animations panel.**
    - `AnimationDrawing` uses `DisableAllAnimations` instead of `MoveAndResize="None"`.
    - The panel's root keeps its region (`ApplyTo=ThisAndEverythingInside`).
    - The "More effects" link keeps `Disappear="None"`, so that it goes at once instead of fading over the new tiles.
14. **A timer that changes an element elsewhere** during a glide. Its change is separate, and is instant unless it touches a setting. If it moves an element that is still gliding, that element's animation is cancelled, as today. A possible improvement, outside this proposal: go on from where it is on screen instead of jumping.

## 7. What changes for apps, compared with 0.2.0

- **Contagion is gone.** A `MoveAndResize` on a pushed element only makes that element glide, not the whole change. Apps that relied on it, for example a `Bouncy` underline that made a whole Grid's rearrangement glide, need the setting on the element where the change starts, on the container's items, or a region.
- **A change that starts outside a region and pushes elements inside it** no longer makes the whole group glide. Those elements glide alone (rule 4), and the outside snaps.
- **`EverythingInside` is renamed** `ThisAndEverythingInside`, if confirmed.
- **The carve-out with `MoveAndResize="None"` is replaced by `DisableAllAnimations`.** `None` stays, with its local meaning.
- **New: `IsAnimationScope` and `DisableAllAnimations`.**
- **`TextChange` pushes its neighbors smoothly** without `MoveAndResize`. Apps that set both keep working.
- **The containment guarantee** ("surrounding elements never overlap or jump", in design.md's principles) still holds inside one animated change. It no longer holds for solo glides and at the edge of a scope, which the product owner accepted.

This is a breaking release: the next version would be 0.3.0.

## 8. Implementation plan

The steps below follow the code as it is at version 0.2.0. Line numbers are approximate. Keep the hot path rules of AGENTS.md: record only in the hooks, with no allocations and no tree walks, and decide in `OnLayoutPassCompleted`.

### `Animate.cs`

- Register `IsAnimationScopeProperty` and `DisableAllAnimationsProperty` (bool, default false, not inherited, like the others), with `Get` and `Set` methods and beginner XML docs.
- Their changed callbacks set fast-path flags, like `AnyAreas` today: for example `LayoutAnimator.AnyScopes` and `AnyDisabled`. When both are false, the new walks are skipped.
- `DisableAllAnimations` also needs to reach the browser for reactions. Hover and press play in `animations.js`, from `BrowserBridge.React`. A simple way: set a data attribute on the element's div (and on attach), and have the script check `element.closest('[data-os-anim-off]')` before a reaction plays. That avoids sending every descendant again when an ancestor changes. To verify against how divs are created and reattached.

### `Movement.cs`

- Rename `AnimationScope.EverythingInside` to `ThisAndEverythingInside`, if confirmed, and update the XML docs of `ApplyTo` and `AnimationScope`.
- Update the doc of `Movement.None`: it no longer "excludes this element and everything inside it". It means the element gives no timing, and beats a region on this element.

### `Engine/LayoutAnimator.cs`

- **Climbing (`OnArranged`, around line 700).** Today, a group doesn't climb between an element and its parent when either has `None` (`HasNone`).
  - Remove the `None` condition, since `None` no longer carves out.
  - Don't climb from an element with `DisableAllAnimations` to its parent, or from an `IsAnimationScope` element to its parent.
  - Mark the scope element's record so that its own box snaps, while its descendants animate relative to it. A flag in `RecordFlags` would do.
  - To verify: the members inside animate with deltas relative to their parents, so they glide inside a box that snapped.
- **Areas (`GetArea`, `IsInsideArea`, `HasNone`, around lines 2577 to 2640).**
  - `GetArea` stops treating `None` as a carve-out. It passes over every `ThisElement` setting, `None` included, and finds the nearest region.
  - Add a cached lookup for "is disabled" (walk up to the first `DisableAllAnimations`, memoized per pass like `_areaCache`), and one for "nearest scope". `IsInsideArea` and `HasNone` follow.
- **`Decide` (around line 1957).**
  - Replace the carved-out exclusion with the disabled one: "the change started inside Chart, which has DisableAllAnimations".
  - Keep the effect first. Add the text change as an effect: an origin that is a TextBlock with a `TextChange` whose text changed in this pass (`_textChanges`, see "Text" in architecture.md) gives the timing. Use the text change's `Duration` for Roll and CrossFade. For Scramble, CountUp and Retype, whose durations are long or depend on the letters, `DefaultDuration` (open question 8).
  - Keep `FindNearestMovement` (around line 2230), with two changes. The origin's own setting is its effective setting from rule 1, which already includes the region. The walk stops after a scope element, as well as at a popup's root.
  - Remove the fallback to `FindNearestMemberMovement` as a reason to animate. When no timing is found, return the snap reason, and let `ProcessGroup` play the solo glides.
- **Solo glides (`ProcessGroup` and `Emit`).** When a group snaps because no setting gave a timing, pick the outermost members with an effective `MoveAndResize` other than `None`. Emit a move for each one, with `Timing.FromMovement(itsSetting, settings, itsOwnDistance)`, from its old absolute rectangle to its new one. Snap the rest. Write one message per glide, or one for several.
- **Wave.** `Decide` uses the nearest member's `Wave` for the others of an effect. That can stay: it's about how they move.
- **Appearances and disappearances.** The holds (`OnVisibilityChanged`, around line 717), the captures (`CaptureDisappearing`, around line 903) and the automatic reveals skip disabled elements. A scope doesn't change them: an effect inside a scope plays, and the others inside make room. Only the scope's box and the outside don't follow.
- **`InvolvesSettings` and the messages** (`Describe`, `DescribeSource`, `LogSnap`). Examples to adapt:
  - "The change started inside Chart, which has DisableAllAnimations; it happened at once."
  - "Glyph appeared; it pops in, and 3 elements inside ComboBox animate together. ComboBox and the elements around it took their new place at once, because ComboBox's template is an animation scope (IsAnimationScope)."
  - "UnderlineBar was moved by a change that doesn't animate; it glides alone (Bouncy, 0.6 s, from its MoveAndResize)."
  - `DescribeSource` says "the ThisAndEverythingInside of X" instead of "the EverythingInside of X".
- **Show what animates** could outline scope elements, and draw solo glides in another color.

### Other engine files

- **`LayoutAnimator.MagicMove.cs`** (around lines 98 and 125, where `_skip` and `IsEnabled` are checked): no departure and no arrival in a disabled element.
- **`Reactions.cs`:** no reaction in a disabled element. Check it in the script, with the attribute above, or in `Send`.
- **`TextChanges.cs` and `HoldOldText`:** a disabled TextBlock shows its new text at once, with no message, or with a message like the snap reasons.
- **`PageOpenHolds.cs`:** no hold in a disabled element (`StartHold`).
- **`Scripts/animations.js`:** the reaction check. Solo glides use the existing move entries.

### Docs of the library

- **README.md:**
  - "Moves and resizes", "Everything inside" and "Parameters of MoveAndResize" get the new name and rules.
  - A new section for scopes and turning animations off, aimed at component authors.
  - "Text" no longer needs `MoveAndResize` for the neighbors.
  - "Status and limitations" gets the overlaps of solo glides and scopes.
- **design.md:**
  - "API principles": amend "Surrounding elements never overlap or jump" (within one animated change).
  - The `Animate.MoveAndResize` part of "Library API".
  - "When things animate (library logic)": Areas, Deciding, and One timing per group.
  - The XAML.io designer section.
- **architecture.md:** "A change never crosses None", "Decide", "Areas", "Elements that appear and disappear", "Text", and the public API list.
- **roadmap.md:**
  - this revision as a phase, with its verified scenarios;
  - the decisions of section 3 in "Decisions, and why";
  - the rejected alternatives of section 10 in the same section.
- **AGENTS.md:** the example at the top still works. Link this document from the table while the work is in progress, and replace it by the updated docs at the end.

### Gallery

The files that use `MoveAndResize`, `ApplyTo` or `EverythingInside` today:

- `Chapters/MovesAndResizesChapter.xaml` and `.xaml.cs`;
- `Chapters/MoreMovesAndResizesChapter.xaml` and `.xaml.cs` (the chart with `MoveAndResize="None"`, and its snippet);
- `Chapters/NeverAnimatesChapter.xaml`;
- `Chapters/WindowResizeChapter.xaml`;
- `Chapters/ListsChapter.xaml` and `.xaml.cs`;
- `Chapters/FromCodeChapter.xaml`;
- `Chapters/TextChapter.xaml` and `.xaml.cs` (the badge's Border has `MoveAndResize` only for the roll's neighbors);
- `Chapters/MagicMoveChapter.xaml`;
- `Chapters/HoverAndPressChapter.xaml`;
- `Chapters/ResizableDialog.xaml`;
- `MainPage.xaml`;
- `Shell/Tour.cs`;
- `Demos/DeployDemo.xaml` (a `MoveAndResize="None"` around line 479).

Add scenarios for the new behaviors:

- the StackPanel A, B, C, with the setting on B, then on the panel's items;
- a small templated control with a glyph and `IsAnimationScope`, with a switch to compare with and without it;
- `DisableAllAnimations` on a part with its own hover and text change;
- a solo glide.

### XAML.io (`Userware.XamlDesigner`, branch `feat/animations`)

- **`AnimationCatalog.cs`:** around line 527, the switch "Also animate what's inside" writes `ApplyTo` with `EverythingInside`; it writes the new value. Around lines 639 and 670 are the `MoveAndResize` and `ItemsMoveAndResize` entries. Decide with the product owner whether the panel shows `IsAnimationScope` and `DisableAllAnimations`; they are meant for component authors and rare cases, so probably not.
- **`AnimationsPanel.xaml`,** around line 480: the root StackPanel's `{anim:Smooth ApplyTo=EverythingInside}`.
- **`AnimationDrawing.cs`,** around line 49: `SetMoveAndResize(this, "None")` becomes `SetDisableAllAnimations(this, true)`.
- **`tests/manual/IDE/Animations.md`:** it mentions `EverythingInside`.
- **The package reference:** XAML.io references the published package, so it needs the new version once it's published, or a local package to test (see development.md).

### The AI agent's skills (`Userware.AiAgent`, branch `feat/add-opensilver-animations-skills`)

These files mention `MoveAndResize` or `EverythingInside`:

- `skills/opensilver/reference/opensilver-animations.md`;
- in `skills/opensilver/examples/`: `todo-list.md`, `ROADMAP.md`, `master-detail-datagrid.md`, `login-form.md`, `form-with-validation.md`, `dashboard.md`, `crud-rest-api.md`.

Their CONTRIBUTING notes still refer to version 0.1.0.

### Verifying

- Build the library with 0 errors and 0 warnings, and run the gallery (AGENTS.md "Quick commands").
- Check every example of section 6 in the gallery, in the browser, with `osAnimations.logForDebugging(true)` and the batches (development.md "Recording frames and batches").
- Regressions: chapter 1 (the details), chapter 5 (Save, 10 elements), chapter 7 (the dashboard, several Refreshes), chapter 9 (the badge and the message), chapter 11 (Magic moves), chapter 14 (the GridSplitter drag).
- No console errors in any chapter.

## 9. Open questions for the product owner

1. **The region's name.** `ApplyTo=ThisAndEverythingInside`, as proposed, or a separate property? Earlier candidates were `Changes`, `Inside`, `Everything` and `Subtree`. The product owner found `Changes` unclear about including the element itself.
2. **The Border of example 3.** Should an element's own setting beat the region setting that its content inherits, when the change starts in the content? The proposal says no: the change plays with the first setting found going up from where it started.
3. **`Appear="None"` and `Disappear="None"` inside a region.** Keep today's meaning, where only the element is instant and the others still make room smoothly (recommended, less migration)? Or make the whole change instant?
4. **`MoveAndResize="None"` with its local meaning** (section 4): confirm.
5. **Does `IsAnimationScope` include content that the app places in the control,** through its `ContentPresenter`? Recommended: yes.
6. **The scope element's own settings** work as usual, and only what starts inside is contained (section 4): confirm.
7. **Should a scope clip its inside while it animates,** to avoid the brief overlap when it shrinks? It only touches the component's elements, but it also cuts shadows and glows for that time.
8. **The timing that a text change gives its neighbors** when the effect is long: CountUp and Scramble last 1.2 s by default, and Retype depends on the letters.
9. **Should `DisableAllAnimations` also stop the element from being carried by a change from outside?** Proposed: no, it's carried, because it only governs what starts inside. Making it jump would create overlaps that the app didn't ask for.
10. **The names `IsAnimationScope` and `DisableAllAnimations`.** These are the product owner's choices; confirm before coding, and check how they read in the XAML.io panel if they're shown there.

## 10. Alternatives considered and rejected

These came up in the brainstorm. They are listed so that they aren't proposed again without new arguments.

- **A control's template is a closed box by default,** with `Contain="False"` for the app to open it, or the boundary at the page. The product owner found it "not self-explanatory at all", and the defaults and exceptions made the model harder to explain.
- **At a boundary, make room at once when growing, and close the gap only after the inside is done when shrinking.** That delays at the DOM level a change that is already done in the visual tree, so it still affects the host and defeats the isolation.
- **Ownership by XAML file or by assembly** (a library's animations only move its own elements). The XAML file is too fine: an app made of UserControls would stop gliding. The assembly is hard to detect reliably.
- **Splitting `MoveAndResize` into `Move` and `Resize`.** It's one animation of the box. Two values would contradict each other (see section 1).
- **Renaming `MoveAndResize` to `LayoutChange`.** Without the split, it brings nothing, and the name stays.
- **`AnimationsInside="Continue|Contained|None"`,** an enum modeled on `KeyboardNavigation.TabNavigation`. The product owner preferred `IsAnimationScope` plus a separate off switch.
- **`IsEnabled` as the off switch.** It's confusing with `UIElement.IsEnabled`, and reads as if the `IsEnabled` property were animated.
- **`Skip="True"` and `StayInside="True"`.** Not clear enough.
- **"Children" instead of "Items".** The product owner prefers "Items".

## 11. Other ideas from the brainstorm, parked

These are independent of this revision, and can be discussed later.

- **A metadata API.** XAML.io's `AnimationCatalog.cs` (about 850 lines) repeats the library's presets, defaults and ranges. For example, Pop's 0.6 s default, the 0.1 to 1 range of `Bounciness`, and which parameters each effect uses. A descriptor API in the library would be one source of truth for XAML.io, the AI agent's skills and the docs, and could warn when a parameter doesn't apply.
- **A general preview call,** like `Animate.Preview(element, moment)`, instead of `PlayHover` and `PlayPress` only. XAML.io previews the other moments by re-rendering the element.
- **Switching between views,** like the two views of XAML.io's Animations panel, or the steps of a wizard. Today, each view needs its own `Appear` and `Disappear` with mirrored sides, and shorter disappear durations so that they don't overlap. A container-level setting could take the direction from the order of the views.
- **An element that appears independently of its parent.** A child of a parent that slides in moves with it. That's why XAML.io's Play button had to leave the moment's view to pop on its own. Something like `{anim:Pop WithParent=False}` would avoid restructuring the layout, but would need to counter-animate the parent's transform.
- **The settling rule:** for 0.5 s after an element appears, changes inside it snap. It's invisible in the API, and surprised us while testing XAML.io's panel. Document it better, or make it a setting.
- **Magic move uses two properties,** `MagicMove` (an id) and `MagicMoveByName` (a bool). One property with a "ByName" value would be simpler.
- **One-word switches,** like `ItemsMoveAndResize="Smooth"` also giving default appear and disappear effects to the items.
- **A scoped speed,** applied by a parent to everything inside it like `DisableAllAnimations`, to slow down or speed up a vendor control's animations without turning them off.
- **Going on from where an element is on screen** when an unanimated change arranges an element that is still gliding, instead of cancelling its animation.

## 12. Glossary

- **Change:** one thing that changes in a layout pass, plus everything it pushes.
- **Origin:** where a change starts. For an element that appears or disappears, its parent.
- **Group:** the origin, the ancestors arranged again because of it, and the elements whose slot changed because of them. Changes in the same pass that climb to a common ancestor share a group.
- **Member:** an element of the group whose rectangle changed.
- **Pushed:** an element whose place or size changed because of a change that started elsewhere.
- **Timing:** the duration and curve that a whole change plays with.
- **Effective setting:** the setting found by rule 1 for an element.
- **Region:** an element whose `MoveAndResize` has `ApplyTo=ThisAndEverythingInside`, and everything inside it. Called an "area" in today's code.
- **Animation scope:** an element with `IsAnimationScope`. The changes that start inside it stay inside it.
- **Disabled:** an element with `DisableAllAnimations`, or inside one.
- **Solo glide:** a member with its own setting that glides alone, because the change that moved it doesn't animate.
