# OpenSilver.Animations: design

This is the full specification of the library for every phase, as agreed with the product owner. Phase 2 (MoveAndResize, plus Bouncy and Wave) is built. Everything else here is the plan.

- How the built part works in detail, including the rules refined while testing, is in [architecture.md](architecture.md). Where this file and the code disagree on built behavior, the code and architecture.md are right.
- What is done, verified and next is in [roadmap.md](roadmap.md).
- The XAML.io "Animate" tab specified below is built later, in the XAML.io repo. Only its library side (`AnimationCatalog`, `AnimationDesign`) belongs here.

## Split

```mermaid
flowchart LR
    subgraph core [OpenSilver core - small PR]
        measure[UIElement.Measure] --> observer[LayoutObserver hooks]
        arrange[UIElement.Arrange] --> observer
        visibility[OnVisibilityChanged] --> observer
        detach[DetachVisualChildIfNotNull] --> observer
        passEnd["UpdateLayout finally"] --> observer
    end
    subgraph lib [OpenSilver.Animations library]
        observer --> collect["Collect rect changes and origins"]
        collect --> decide["Group by origin, apply rules"]
        decide --> batch["One JS batch per pass"]
        batch --> waapi["Web Animations"]
    end
    subgraph apps [Consumers]
        gallery[Gallery app] --> catalog["AnimationCatalog and AnimationDesign"]
        xamlio["XAML.io Animate tab - other repo, later"] --> catalog
        catalog --> lib
    end
```

## Core change 1: `LayoutObserver` (C#, about 60 lines)

It uses only public types, costs a single null check when no observer is registered, and doesn't change behavior.

```csharp
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class LayoutObserver
{
    public static IDisposable Register(LayoutObserver observer);

    protected internal virtual void OnMeasured(UIElement element, Size availableSize, bool sameAvailableSize, Size previousDesiredSize, bool isFirstMeasure) { }
    protected internal virtual void OnArranging(UIElement element, Rect finalRect, Rect previousFinalRect, bool isFirstArrange) { }
    protected internal virtual void OnArranged(UIElement element, Rect previousVisualRect, Rect visualRect, bool rendered) { }
    protected internal virtual void OnVisibilityChanged(UIElement element, Visibility oldValue, Visibility newValue) { }
    protected internal virtual void OnDetaching(UIElement child, UIElement parent) { }
    protected internal virtual void OnResponsiveValueChanged(FrameworkElement element, DependencyObject target, DependencyProperty property) { }
    protected internal virtual void OnLayoutPassCompleted() { }
}
```

Call sites, each a guarded one-liner:
- [UIElement.Layout.cs](src/Runtime/Runtime/System.Windows/UIElement.Layout.cs), `Measure`: after `_desiredSize = desiredSize;`. It passes `isCloseToPreviousMeasure` as `sameAvailableSize` and `prevSize` as `previousDesiredSize`.
- `Arrange`: before `ArrangeCore(finalRect)`, and after the `RenderLayout()` block. The previous rect comes from the existing `oldOffset`/`oldSize` locals.
- [UIElement.cs](src/Runtime/Runtime/System.Windows/UIElement.cs), `OnVisibilityChanged`: at the very end, after `UpdateIsVisibleCache()`. The library may re-enter here, and an earlier call would let the outer callback overwrite the caches with a stale value.
- [INTERNAL_VisualTreeManager.cs](src/Runtime/Runtime/Core/Rendering/INTERNAL_VisualTreeManager.cs), `DetachVisualChildIfNotNull`: before `InternalDetachVisual(child)`. The DOM still exists at that point: `RemoveFromDom` either queues the removal to `ProcessRenderQueue` or appends it to the same JS buffer, so it always runs after the library's clone call.
- [LayoutManager.cs](src/Runtime/Runtime/System.Windows/LayoutManager.cs), the `finally` block of `UpdateLayout`: before `UIRenderer.ProcessRenderQueue(true)`. The library's JS goes into the same flush.
- [ResponsiveExpression.cs](src/Runtime/Runtime/System.Windows/ResponsiveExpression.cs), `OnSourceElementSizeChanged`: after the expression is applied again, when it had a value and the new one differs. It passes the mentor (the element, or the owner of a ColumnDefinition, for example), the target and the property. This runs in the `SizeChanged` of the watched element, inside `UpdateLayout`, between two layout rounds.

Measure and arrange hooks run inside `Dispatcher.DisableProcessing()`. The library only records data there; anything that needs the dispatcher is posted with `BeginInvoke`.

## Core change 2: insertion that tolerates extra DOM nodes (JS, about 10 lines)

`osjs.createVisual` inserted with `parent.insertBefore(element, parent.children[index])`, the only index-based insertion in `cshtml5.js`. It now skips children marked `data-os-foreign` when resolving the index; an index past the end appends without scanning. The marked children are ghosts, text overlays and Magic move clones (phases 3, 5 and 7).

## Validate first (spike)

- **XAML.**
  - String presets on attached properties, through `[TypeConverter]` on the value types; the compiler's inspector detects `TypeConverterAttribute` on types.
  - Markup-extension parameters such as `Duration=0.3s`.
  - Preset objects declared as resources.
  - Style Setter values, which `Setter` resolves at runtime.
  - Fallback: type the affected properties as `object` and parse at runtime.
- **Browsers.**
  - `composite: 'add'` on `translate`.
  - `clip-path: inset(0)` in an animation overriding an inline `clip-path: path(...)`.
  - CSS geometry properties on `<rect>`/`<ellipse>` in Safari.
  - `linear()`; the fallback bakes the curve into keyframe offsets.
- **XAML.io.** The library loads on the design surface, and `DesignerProperties.IsInDesignTool` is true there.

## API principles

- **Naming.**
  - The package and namespace are `OpenSilver.Animations`. The attached properties live on the verb `Animate` (`anim:Animate.Appear="Fade"`), and the settings class is `AnimationSettings`.
  - "Animate" is understood by everyone; Keynote and Canva use it for exactly this. It reads as an instruction that matches the "When it ..." sections.
  - Being a verb, it isn't mistaken for a type of OpenSilver's Storyboard system (`System.Windows.Media.Animation`).
  - "Reduce motion" keeps its name, because that's the label of the operating system setting.
- **Named after what the developer sees.** Every property reads as "When it ...".
- **The XAML names and the designer labels match**:
  - `Appear` is "When it appears", `Disappear` is "When it disappears", and `MoveAndResize` is "When it moves or changes size".
  - `ItemAppear`, `ItemDisappear` and `ItemMoveAndResize` are "When an item appears / disappears / moves or changes size", and `ItemCascade` is "Cascade".
  - `TextChange` is "When its text changes", and `MagicMove` is "Magic move".
  - For parameters, the label is the XAML name split into words: `StartAfter` is "Start after", `OthersMove` is "Other elements move", `OnPageOpen` is "When the page opens", `ApplyTo` is "Apply to", and `AlsoOnWindowResize` is "Also when the browser window is resized". `From`, `To`, `Distance`, `Duration`, `Easing`, `Bounciness`, `Wave`, `Speed`, `Cursor` and `Travel` keep their names.
- **Off by default.** Choosing an effect is enough; every parameter has a good default.
- **Surrounding elements never overlap or jump.** When something animates, everything involved in the same change animates with it. That's not an option the developer has to understand.
- **Beginner vocabulary:** `From`/`To` (not Direction), `StartAfter` (not Delay), `Zoom` (not Scale), `Bouncy` (not Spring), `Cascade` (not Stagger), `Wave` (not Ripple).
- **One curve vocabulary everywhere:** `Smooth` (default, a soft ease-out), `Gentle` (ease-in-out), `Linear`, `Bouncy` (a spring, tuned by `Bounciness`).
- **Readable times.** Time values accept `300` (milliseconds), `300ms` or `0.3s`. The designer shows and writes seconds.

## Library API

The NuGet package is `OpenSilver.Animations`, described as "Easy animations for OpenSilver" and tagged `animation` and `transition`. The XML namespace is `xmlns:anim="http://opensilver.net/animations"` (declared with `XmlnsDefinition`; the `clr-namespace:OpenSilver.Animations;assembly=OpenSilver.Animations` form also works). All properties are attached on `Animate` and are not inherited. The value types are `Movement` (the base of `Smooth`, `Bouncy` and `None`), `Transition` (the base of the Appear and Disappear presets, with `Transition.None`), `AnimationTime` for durations and `Distance` for Slide. `From` and `To` take a `Side`, and `OthersMove` is an enum of the same name. The curves `Smooth`, `Gentle`, `Linear` and `Bouncy` are on the `Easings` class.

- The base of the presets is named `Transition`, not `Effect`: every element already has an `Effect` property (`UIElement.Effect`), which would hide the type in code-behind.
- `Distance` is a struct with a type converter, not a `double` with named values: the OpenSilver XAML compiler only accepts numbers for `double` properties.

```xml
<!-- When it appears / disappears -->
<StackPanel anim:Animate.Appear="{anim:Slide From=Bottom}" anim:Animate.Disappear="Fade" />

<!-- When it moves or changes size -->
<Button Content="{Binding SaveLabel}" anim:Animate.MoveAndResize="Smooth" />
<Border x:Name="SettingsForm" anim:Animate.MoveAndResize="{anim:Smooth ApplyTo=EverythingInside}" />

<!-- When its items change (Panel or ItemsControl) -->
<ListBox anim:Animate.ItemAppear="{anim:Slide From=Right}" anim:Animate.ItemDisappear="Fade"
         anim:Animate.ItemMoveAndResize="Smooth" anim:Animate.ItemCascade="0.04s" />
<WrapPanel anim:Animate.ItemMoveAndResize="{anim:Smooth AlsoOnWindowResize=True}" />

<!-- Text -->
<TextBlock Text="{Binding Count}" anim:Animate.TextChange="Roll" />
<TextBlock Text="Welcome" anim:Animate.Appear="{anim:Typewriter Speed=40, Cursor=True}" />

<!-- Magic move -->
<Border anim:Animate.MagicMove="SelectionPill" />
<Image anim:Animate.MagicMove="{Binding Id, StringFormat='photo-{0}'}" />

<!-- A reusable effect, and app-wide settings in App.xaml -->
<anim:Slide x:Key="Intro" From="Bottom" Duration="0.4s" />
<anim:AnimationSettings x:Key="AnimationSettings" Speed="1" />
```

```csharp
// Code-behind
Animate.SetAppear(card, "Fade");
Animate.Changes(() => details.Visibility = Visibility.Visible);
Animate.Skip(() => LoadInitialData());
```

- **Value forms.**
  - A preset name string: `"Fade"`, `"Smooth"`.
  - A preset object with parameters, as a markup extension (`{anim:Slide From=Bottom, Duration=0.4s}`) or as an element. Presets are classes deriving from `MarkupExtension` whose `ProvideValue` returns the preset itself, so they also work as resources (`{StaticResource Intro}`) and in Style Setters.
  - A parameter that doesn't apply to the chosen preset is ignored, with a warning in the debug log.
- **Animate.Appear and Animate.Disappear.**
  - **Presets:** `Fade`, `Slide`, `Zoom`, `Pop` (Appear only), `Wipe`, `Blur`, `Custom`, `None`. TextBlock only: `Typewriter` and `FadeLetters` (Appear), `Backspace` and `FadeLetters` (Disappear).
  - **Parameters**, with the presets that use them:
    - `From` (Appear) / `To` (Disappear): `Top`, `Bottom` (default), `Left`, `Right`. Used by Slide and Wipe.
    - `Distance`: `Short` (10 px), `Medium` (30 px, default), `Long` (100 px), or a number of pixels. Used by Slide.
    - `Duration`: 0.05 to 3 s, default `AnimationSettings.DefaultDuration` (0.25 s). All presets except Typewriter and Backspace. Pop, and any preset with `Easing=Bouncy`, default to twice `DefaultDuration`, so that the bounce has time to settle.
    - `StartAfter`: 0 to 10 s, default 0. All presets.
    - `Easing`: `Smooth` (default), `Gentle`, `Linear`, `Bouncy`, or an `IEasingFunction` resource. All presets except the text ones.
    - `Bounciness`: 0.1 to 1, default 0.5. Pop, and any preset with `Easing=Bouncy`.
    - Custom look: `Opacity` (0 to 1, default 0), `OffsetX`/`OffsetY` (px, default 0), `Zoom` (0 to 3, default 1), `Rotate` (degrees, default 0), `Blur` (px, default 0). Used by Custom. It's the start look for Appear and the end look for Disappear.
    - `Speed`: letters per second, 5 to 200, default 30. Typewriter and Backspace. `Cursor`: default false. Typewriter.
    - `OnPageOpen` (Appear only): `InPlace` (default), `PushOthers`, `Skip`.
    - `OthersMove`: `While` (default); `Before` for Appear only; `After` for Disappear only.
  - **At runtime, surrounding elements always move smoothly** to make room or to fill the space. They use the effect's `Duration` and `Easing`, except that a bouncy effect moves the others smoothly.
  - **OnPageOpen**: what happens when it appears together with its page or container (page load, a dialog opening, or inside a card that is itself appearing). `StartAfter` counts from the end of the layout pass that opened the page, when the effects of the rest of the page start.
    - `InPlace`: it animates in the space already reserved for it, after `StartAfter`.
    - `PushOthers`: the page opens without it, then it pushes the others aside after `StartAfter`. Its effect then plays at once. Under reduced motion, or when nothing animates, it acts like `InPlace`.
    - `Skip`: it simply shows, without its effect.
    - An element that appears on its own, after its page has opened, ignores `OnPageOpen`.
    - On a Disappear, `OnPageOpen` is ignored, with a warning.
  - **OthersMove**: when the surrounding elements move.
    - Appear: `While`, meaning the others make room while it appears; or `Before`, meaning they make room first and it appears in the empty space.
    - Disappear: `While`, meaning the others move into its place while it disappears; or `After`, meaning it disappears first and they move in afterwards.
    - When several elements appear or disappear in the same change and the same container, the first one gives the others their timing and its `OthersMove`. Each element still plays its own effect, with its own duration and curve.
  - **StartAfter at runtime** delays only the effect: the room opens at once, and the element stays invisible until its effect starts. Only `OnPageOpen=PushOthers` holds the element out of the layout (see the hold below).
    - On a Disappear, the others fill its place at once, and its copy waits before its effect.
    - With `OthersMove=Before`, the effect waits for the others' move, then for `StartAfter`. With `OthersMove=After`, the others wait for `StartAfter` and the effect.
    - It's divided by `AnimationSettings.Speed`, like durations, and it's clamped to 10 s.
  - **What the presets do.** `Fade` fades opacity. `Slide` fades while moving `Distance` from the `From` side. `Zoom` fades while zooming from 90%. `Pop` zooms from 50% with a bounce. `Wipe` reveals from the `From` edge without fading. `Blur` fades from an 8 px blur.
    - Used as a Disappear, Pop zooms out to 90% like Zoom, with a warning.
- **Animate.MoveAndResize**: `None` (default), `Smooth` or `Bouncy`.
  - **Parameters:**
    - `Duration`.
    - `Bounciness` (Bouncy only).
    - `Easing` (Smooth only): `Smooth`, `Gentle`, `Linear` or an `IEasingFunction`.
    - `Wave`: 0 to 1, default 0.
    - `ApplyTo`: `ThisElement` (default) or `EverythingInside`.
    - `AlsoOnWindowResize`: default false.
  - **ApplyTo=ThisElement.** Whenever this element moves or changes size, it animates. The exceptions are resizing the browser window, scrolling, dragging and typing (see "Never animates"). Everything that moves in the same change animates with it, even if the change started elsewhere.
  - **ApplyTo=EverythingInside.** Exactly as if every element inside, at any depth (including templates and items created later), had `MoveAndResize` with the same settings. Two additions:
    - Elements inside that appear or disappear without their own effect get an automatic one: Wipe when they push others or the others fill their place, Fade otherwise. It follows the curve of the sizes, so that the wiped part keeps up with the elements that move. Only the topmost element gets it, never on page open.
    - `Appear=None` or `Disappear=None` turns it off for an element. An element that becomes Hidden keeps its space, so it gets no automatic effect. At most 40 elements get an automatic Disappear in one change; the others vanish at once.
    - Lists inside track their items by data item, as with `ItemMoveAndResize`.

    Nothing is copied to the elements inside: the area is found by a lookup when a change happens (see "Areas").
    - A nested `EverythingInside` starts a new area with its own settings.
    - `None` on a descendant carves it and everything inside it out of the area: changes starting there never animate, including the ones that push others.
    - A descendant with `ThisElement` doesn't carve anything out. It gives its own timing to the changes that move or resize it, including the ones that start inside it, because the nearest setting wins (see "One timing per group").
  - **AlsoOnWindowResize.** Normally, changes caused by resizing the browser window never animate, because animating a whole page while its edge is dragged feels laggy.
    - With this flag, only the elements that opted in animate, together with their content; everything else snaps to the new size, and brief overlaps are accepted.
    - Each resize event retargets from the current on-screen position, so elements keep following the window edge.
    - A `{Responsive}` value that switches when the window, or the element it watches, crosses a breakpoint animates without this flag, like a click (see "Window resizes" in "When things animate").
- **Items** (Panel or ItemsControl). Items are the containers of an ItemsControl, or the children of a Panel. Setting the properties on the items panel inside an ItemsControl's template also works.
  - `Animate.ItemAppear` and `Animate.ItemDisappear`: the same presets and parameters as Appear/Disappear, except the text ones. `OnPageOpen` accepts `InPlace` or `Skip`.
  - `Animate.ItemMoveAndResize`: `Smooth`, `Bouncy` or `None`, with the same parameters as `MoveAndResize` except `ApplyTo`. Each item moves and resizes smoothly, including when the list is re-sorted, filtered or refreshed.
  - `Animate.ItemCascade`: the time between items that appear or disappear together, from 0 to 0.3 s (default 0, all together). The total is capped at 0.6 s.
  - An item's own `Appear`, `Disappear` or `MoveAndResize` wins.
- **Animate.TextChange** (TextBlock), used when `Text` changes while the TextBlock is visible:
  - `Roll`: `From=Bottom` (default) or `Top`, plus `Duration`.
  - `CrossFade`, `Scramble`: `Duration`.
  - `Retype`: `Speed`.
  - `CountUp`: `Duration`. For numbers only; other text uses CrossFade.
- **Magic move.**
  - `Animate.MagicMove`: an id, which can be bound. Or `{anim:MagicMove Id=..., Travel=Automatic|Above|Behind, Duration=..., Easing=...}`.
  - `Animate.MagicMoveByName` (bool) on a Frame or ContentControl: elements with the same `x:Name` in the old and new content morph into each other.
- **Globals.**
  - `AnimationSettings`:
    - `IsEnabled` (true) and `RespectReducedMotion` (true).
    - `Speed` (1; 2 means twice as fast, 0.5 half as fast).
    - `DefaultDuration` (0.25 s).
    - `LogForDebugging` (false). It logs why each change animated or not, in plain language (for example "DetailsPanel appeared; 5 elements moved with it; timing from its Fade, 0.25 s"). The same messages are raised as the static `AnimationSettings.Logged` event, whether or not `LogForDebugging` is on.
      - Only changes that involve a setting (or `Animate.Changes`) are explained. A repeated snap message is logged at most once per second, and window-resize messages at most once per second.
      - Changes that start in scroll bars or scroll viewer parts, or right after an element appeared, snap silently.
      - Messages name an element from the page rather than a part of a control template when they can, and the timing source names the element that carries the setting.
    - `SimulateReducedMotion` (false). Behaves as if the device asked for reduced motion; the gallery's "Reduce motion" switch sets it.
    - `ShowWhatAnimates` (false). A debugging overlay that outlines the elements of each animated group while they animate, with the origin highlighted.
  - Set them in code (`AnimationSettings.Current`), or declare `<anim:AnimationSettings x:Key="AnimationSettings" .../>` in the App.xaml resources, which is read at startup.
  - `Animate.Changes(Action, duration?)` animates every change made in the action, even without any property set. `Animate.Skip(Action)` makes those changes instant.
    - Both apply to the layout pass that contains the changes made in the action.
    - The library calls `UpdateLayout()` right after the action when it isn't already inside a layout pass.
- **Reduced motion.** When the user prefers reduced motion and `RespectReducedMotion` is on:
  - moves and resizes snap;
  - appear and disappear effects become a 0.15 s fade;
  - text effects show the final text;
  - Magic move becomes a cross-fade in place.
- **No compatibility layer.** The Showcase's own `OpenSilver.Animations` project will be removed. The Showcase is ported to this API.

## Designer panel (XAML.io)

This section is the specification for the XAML.io repo. It is not implemented in this plan. The library side it depends on (`AnimationCatalog`, `AnimationDesign`) is, because the gallery playgrounds use it too.

### Where it lives

- **An Animate tab** next to Properties and Events. Its tooltip is "Animations: how it appears, disappears, moves and changes".
- **Selection.**
  - **One element:** its sections.
  - **Several elements:** only the sections that apply to all of them. Different values show "Mixed", and an edit applies to all of them.
  - **No selection, or the page root:** the page view (page intro timeline and app animation settings).
- **Design mode is passive.** The library sees `DesignerProperties.IsInDesignTool` and never animates by itself on the canvas, so editing never lags or replays intros. Held elements are shown, and text shows its final value. Only the Play buttons animate.
- **First use.** The first time an animation value is set, XAML.io adds the `OpenSilver.Animations` package reference and the `xmlns:anim` declaration on the root element.

### Tab layout, top to bottom

1. **"What do you want to animate?"** A row of recipe buttons. Once the element has an animation value, it collapses to a single "Recipes" button. Recipes that don't apply are greyed, with the reason as a tooltip.
   - **"Show or hide it with an effect":** `Appear=Fade` and `Disappear=Fade`; opens both sections.
   - **"Animate its size and position changes":** `MoveAndResize=Smooth`.
   - **"Animate every change inside it":** `MoveAndResize={anim:Smooth ApplyTo=EverythingInside}`.
   - **"Animate list items":** on a Panel or ItemsControl, `ItemAppear=Fade`, `ItemDisappear=Fade` and `ItemMoveAndResize=Smooth`. On anything else it shows "Select the list first" and a "Select the nearest list" button.
   - **"Animate text changes"** (TextBlock): `TextChange=Roll` when the current text is a number, `CrossFade` otherwise.
   - **"Page intro":** `Appear={anim:Slide From=Bottom}` on the selected elements, with Start after spread by 0.1 s in reading order; opens the page intro timeline.
   - **"Move between two elements (Magic move)":** sets `MagicMove` to an id suggested from `x:Name`, and shows "Give the same id to the other element".
2. **Sections**, in this order, each shown only when it applies:
   - "When it appears" and "When it disappears": any element.
   - "When it moves or changes size": any FrameworkElement.
   - "When its items change": Panel or ItemsControl.
   - "When its text changes": TextBlock.
   - "Magic move": any FrameworkElement.
3. **Footer:** a "Preview as someone who prefers less motion" toggle.

### Section header

- Each header shows the title, a status, a Play button and a Reset button.
  - The status is "Off", or the short summary, for example "Slide from bottom, 0.25 s".
  - Reset removes every attribute of that section from the XAML.
- Sections with no value are collapsed. Sections with a value, or a value from a style or an area, are expanded.

### Controls shared by all sections

- **Effect gallery.**
  - Tiles with an icon and a name, "None" first.
  - Hovering a tile plays a looping one-second preview on a sample card inside the tile; the real element isn't touched.
  - Clicking a tile selects it. Presets that don't apply to this element type aren't shown.
- **Only the fields that the chosen effect uses** appear under the gallery, in this order:
  - direction ("From" or "To");
  - Distance;
  - Duration;
  - Start after;
  - Easing;
  - Bounciness;
  - the effect-specific fields.
- **Direction picker:** four arrows around a square, labelled "From" in Appear and "To" in Disappear.
- **Distance:** Short, Medium, Long, or Custom, which reveals a pixel box from 0 to 1000.
- **Time fields:** a slider plus a number box in seconds.
  - Typing `300ms` is accepted and converted to seconds.
  - Out-of-range values are clamped, with a short message.
  - Ranges: Duration 0.05 to 3 s in 0.05 s steps; Start after 0 to 10 s in 0.05 s steps; Cascade 0 to 0.3 s in 0.01 s steps.
- **Easing:** four buttons with curve icons (Smooth, Gentle, Linear, Bouncy), under the line "How it speeds up and slows down". Choosing Bouncy reveals the Bounciness slider, labelled "A little" to "A lot".
- **Example line:** grey text under each field, listed per section below.
- **Advanced:** an expander at the bottom of a section, closed by default. It opens by itself when it holds a non-default value.
- **Values that don't apply.**
  - A field that doesn't apply to the chosen effect is hidden, unless the XAML sets it. In that case it's shown disabled, with "No effect with this effect" and a Remove link.
  - The same applies to a wrong `OthersMove` value, a wrong `OnPageOpen` for items, and `ApplyTo` on items.
- **Values the panel can't edit:** bindings (except the Magic move id), a `StaticResource` preset, an `IEasingFunction`.
  - The field shows "Set in XAML: {StaticResource Intro}" with "Edit in XAML" and "Replace" buttons.
  - A resource preset also shows its resolved values, read-only, with "Edit the resource".
- **Values from a style:** "(from style PrimaryButton)" with a link to the style. Editing creates a local value, with the note "This overrides the style".
- **Undo:** every edit is one undo step. The XAML updates immediately, and editing the XAML updates the panel.

### "When it appears"

- **Gallery:** None, Fade, Slide, Zoom, Pop, Wipe, Blur, Custom. For a TextBlock, also Typewriter and Fade letters.
- **Fields per effect:**
  - Fade, Zoom, Blur: Duration, Start after, Easing.
  - Slide: From, Distance, Duration, Start after, Easing.
  - Pop: Duration, Start after, Bounciness.
  - Wipe: From, Duration, Start after, Easing.
  - Custom: "Start look" (Opacity %, Move X px, Move Y px, Zoom %, Rotate °, Blur px), then Duration, Start after, Easing.
  - Typewriter: Speed (letters per second), "Show a blinking cursor", Start after.
  - Fade letters: Duration, Start after.
- **"Other elements move"** radio:
  - "While it appears" (default);
  - "Before it appears (it then appears in the empty space)".

  It's shown only when `AnimationDesign.WouldPushOthers(element)` is true, meaning that hiding the element moves something in the current layout.
- **"When the page opens"** radio:
  - "Play the effect in its place" (default);
  - "Show it after the page has opened, pushing the others aside";
  - "Just show it, without the effect".

  Example line: "The page opens when it loads, when a dialog opens, or when a container around it appears."
- **Example lines:**
  - Start after: "Counted from when the page opens, or from when it's shown."
  - Section: "It appears when the page opens, when it's made visible, or when it's added."

### "When it disappears"

- **Gallery:** None, Fade, Slide, Zoom, Wipe, Blur, Custom ("End look"). For a TextBlock, also Backspace and Fade letters.
- **Fields:** the same as for appearing, with "To" instead of "From". Backspace has Speed.
- **"Other elements move"** radio, shown when the element pushes something:
  - "While it disappears" (default);
  - "After it has disappeared".
- **Example line:** "It disappears when it's hidden (Visibility Collapsed or Hidden) or removed."

### "When it moves or changes size"

- **Choice:** a segmented control with "Don't animate", "Smooth" and "Bouncy".
  - Outside an area, "Don't animate" removes the attribute.
  - Inside an area, it becomes "Exclude from the area" and writes `None`.
- **Fields:**
  - Duration.
  - Bounciness (Bouncy only).
  - "Also when the browser window is resized". Example: "For example, tiles that glide to their new rows while the window edge is dragged."
- **Advanced:**
  - **"Apply to"**: "This element only" (default) or "This element and everything inside it". Example: "Everything inside: any change in it animates, including items and elements that appear."
  - **Wave**: a slider from Off to Strong (0 to 1). Example: "Farther elements start a bit later, like a wave."
  - **Easing** (Smooth only): Smooth, Gentle or Linear.
- **Example line:** "For example when its text, content or size changes, or when an element next to it appears, disappears or grows."
- **Inside an area**, when the element has no value of its own:
  - The section shows "Smooth (from SettingsForm)", with a link that selects the area.
  - Two buttons: "Change for this element" creates a local value; "Exclude this element" writes `None`.

### "When its items change"

- **Three rows**, each with its own gallery or segmented control:
  - "When an item appears": the Appear gallery without text presets. Its "When the page opens" radio has only "Play the effect in its place" and "Just show it".
  - "When an item disappears": the Disappear gallery without text presets.
  - "When an item moves or changes size": "Don't animate", "Smooth" or "Bouncy", with Duration, Bounciness and "Also when the browser window is resized". Advanced holds Wave and Easing.
- **"Cascade: time between items"**, shown when an appear or disappear effect is set. Example: "0 means all together."
- **"Other elements move"** on the appear and disappear rows: the same radios as above.
- **Example line** under "moves or changes size": "For example when the list is sorted or filtered, or when an item above it appears, disappears or grows."

### "When its text changes" (TextBlock)

- **Gallery:** None, Roll, Cross-fade, Scramble, Retype, Count up.
- **Fields:**
  - Roll: "From" (Bottom or Top) and Duration.
  - Cross-fade, Scramble and Count up: Duration.
  - Retype: Speed.
- **Example line:** "For example when a bound value changes. Count up needs a number."

### "Magic move"

- **Id** text box, with suggestions from the ids already used on the page.
  - Under it: "Used by 3 elements", with "Select them".
  - A bound id shows "Bound to Id (photo-{0})" and "Edit binding".
- **Travel:** "Automatic" (default), "Above everything" or "Behind other elements". Under it, the current result, for example "Automatic: behind, because it's a background".
- **Duration and Easing.**
- **On a Frame or ContentControl**, a checkbox "Move elements with the same name between pages" (`MagicMoveByName`).
- **Example line:** "When this element disappears and another element with the same id appears, it travels to the new place."

### Summary sentence

Each section shows a plain-language sentence under it, generated by `AnimationDesign.Describe`. The same text is used for the header status and the canvas badge tooltip. Examples:
- Appear: "Slides in from the bottom in 0.3 s, 0.2 s after the page opens. The elements below slide down to make room."
- Disappear: "Fades out in 0.25 s. The elements below move up after it has disappeared."
- Moves: "Moves and resizes smoothly in 0.25 s, also when the window is resized. Everything inside it too."
- Items: "New items slide in from the right, one every 0.04 s. Removed items fade out. Items glide to their new places."
- Text: "When the text changes, the characters roll up in 0.25 s."
- Magic move: "Travels from the other 'SelectionPill' element to here, behind the other elements, in 0.25 s."

### Hints

A hint is an info line inside its section, with an optional action button and "Don't show again". Hints never block anything.
- **MoveAndResize on an item container or inside an ItemTemplate:** "To animate list items, use 'When its items change' on the list." [Select the list]
- **Item properties on an items panel inside an ItemsControl:** "You can also set this on the list itself." [Move to the list]
- **Start after on elements inside an ItemTemplate:** "To make items appear one after another, use Cascade on the list." [Select the list]
- **Everything inside on the page root, or on a Frame's page:** "Every size change on the page will animate. Consider a smaller area."
- **Everything inside with "Also when the browser window is resized":** "The whole area will follow the window edge with a delay, which can feel laggy. Prefer turning it on only for the items that should glide."
- **The same MoveAndResize settings as the surrounding area:** "Already animated by SettingsForm. You can remove this." [Remove]
- **"Show it after the page has opened" on an element that pushes nothing:** "Nothing moves around it, so it will simply appear after the page."
- **Blur on an element larger than a quarter of the page:** "Blur can be slow on large areas. Consider Fade."
- **A TextBox or PasswordBox with MoveAndResize:** "Typing never animates; other size changes do."
- **Text effects on a TextBlock with rich inlines:** "Rich text uses Cross-fade."
- **Roll on text that wraps to several lines:** "Roll works on one line; Cross-fade is used otherwise."
- **A Magic move id used by one element only:** "No other element uses this id yet."
- **Magic move inside a template whose states use Opacity instead of Visibility:** "Magic move needs this element to be hidden with Visibility, but its template uses Opacity." [Show the state]
- **An element inside a popup or dialog, with an area set outside it:** "Dialogs and dropdowns are separate areas. Set it on the dialog." [Select the dialog]

### Page view (no selection, or the page root)

- **Page intro timeline.**
  - One row per element with an Appear effect that plays when the page opens. Elements inside templates aren't listed.
  - Rows are ordered by Start after. Each is labelled with its `x:Name`, or its type plus a text snippet.
  - A bar starts at Start after and is as long as Duration. Dragging the bar changes Start after; dragging its right edge changes Duration. It snaps to 0.05 s; Shift disables snapping.
  - Rows with "Show it after the page has opened" get a push icon. Rows with "Just show it" are greyed.
  - "Spread evenly" sets Start after to row index x 0.1 s for the selected rows.
  - "Play page intro" replays the page open on the canvas.
- **App animation settings.** This edits the `AnimationSettings` resource in App.xaml and creates it on first edit:
  - "Turn on animations" (IsEnabled);
  - "Respect 'reduce motion' on the user's device";
  - "Speed" (0.5x to 2x);
  - "Default duration";
  - "Log why things animate (for debugging)".

### Canvas

- **Badges.** A small animation icon at the top-right of each element that has an animation value.
  - Hovering shows the summary; clicking selects the element and opens the Animate tab.
  - A canvas toolbar toggle, "Show animation badges", is on by default.
- **Area outline.** When the selected element is an area, or is inside one, the area gets a dashed outline.
- **Magic move links.** When the selected element has a Magic move id, dotted lines join it to the other elements with the same id.

### Previews

All previews run on the live canvas through `AnimationDesign.Play(element, section)`. Afterwards the exact prior state is restored, without animation. Any edit or canvas click stops a preview. The reduced-motion toggle applies to previews.
- **Appear:** hide the element instantly, then show it with its effect; the neighbors react.
- **Disappear:** hide it with its effect, then show it again instantly after 0.8 s.
- **Moves or changes size:** grow the element by 25% (at least 40 px), wait 0.6 s, then shrink it back, both animated. For an area, the same is done to its first child.
- **Items:** hide the second visible item and show it again. With no items, it shows "Add items (or sample data) to preview".
- **Text change:**
  - numbers go from the value minus 1 to the value (Count up counts from 0);
  - other text goes from the text without its last word to the full text.
- **Magic move:** when the id belongs to a template of a RadioButton, TabItem, ListBoxItem or ToggleButton, select the next sibling item, then restore. Otherwise, animate a clone between two visible elements with the same id. Otherwise, show "Preview needs two elements with this id".
- **Page intro:** re-create the page's visual tree and play the page open.

### What the library provides to XAML.io

- **AnimationCatalog:**
  - sections (id, title, applicability);
  - presets (id, label, icon id, tile-preview id, applicable element types);
  - parameters (XAML name, label, type, unit, range, step, default, which presets use it, example line, visibility rule).
- **AnimationDesign:**
  - `Parse` and `Write` for attribute values. Writing always produces the shortest form:
    - the plain string when every parameter is default;
    - otherwise the markup extension with only the non-default parameters, in catalog order;
    - times in seconds with the `s` suffix.
  - `GetValues(element)`: per section, the value and where it comes from (local, style, area or XAML-only).
  - `Describe`, `GetHints`, `WouldPushOthers`, `GetArea`, `GetMagicMoveGroup`, `Play`, `PlayPageIntro`, `StopPreview`.
- **Localization.** All labels, example lines and hint texts come from the catalog, so they can be translated.

## Gallery app: "OpenSilver.Animations library: a guided tour"

A standalone OpenSilver app in the library repo that tells the story of the system, from the simplest thing to the most advanced one. The name keeps the dot, and says "library", so that it isn't mistaken for OpenSilver's storyboard animations. The logo, in the top bar and as the browser tab's icon (`favicon.svg`), is a rounded box that moves to the right, leaving a fading trail. Each chapter introduces one idea, and every idea comes with something to click. It is also the manual test suite: every acceptance scenario below lives in a chapter.

### Every chapter page

- **Explanation.** Two to four sentences in plain language, using the same words as the API and the future designer.
- **Live demo.** The demo, with its action buttons ("Show details", "Add item", "Sort") and a Replay button.
- **Playground.** A compact panel next to the demo, generated from `AnimationCatalog`, with the same labels, ranges and example lines as the future designer. Changing a value applies it to the demo immediately. It is not the XAML.io tab, just a small reuse of the same metadata, which also validates the catalog early.
- **Live XAML.** The attributes produced by `AnimationDesign.Write` for the current playground values, with a Copy button.
- **Summary.** The sentence from `AnimationDesign.Describe`.
- **"What just happened".** The last messages from `AnimationSettings.Logged`, so the reader sees why things moved.
- **"Good to know".** One tip or pitfall.
- **"What should happen".** A collapsible box with the chapter's acceptance lines, for manual testing. Its header is a two-sided chevron that points right when closed and down when open, so it reads as an expander, not as an option.
- **Navigation.** Previous and Next.

### Global toolbar

- **Slow motion:** `Speed` 0.25.
- **Show what animates:** `ShowWhatAnimates`.
- **Reduce motion:** simulates the user preference.
- **Replay page:** replays the current chapter's page open.
- **Chapters sidebar:** progress, and one URL per chapter for deep links.
- All the options are off at start.

### Phones and tablets

The tour works from a phone to a desktop, with the breakpoints of `ResponsiveThreshold.Default` (960 and 1280 px):

- **Desktop:** the chapters, the page and the log are three columns; the options are in the top bar.
- **Tablet:** the log becomes a drawer, opened by a Log button with a dot for new messages.
- **Phone:** the chapters and the options become a drawer too, opened by a menu button. Margins, fonts and the demos' sizes are smaller, so that every demo fits and still animates.

### Chapters

1. **Your first animation.** `Appear` and `Disappear` set to Fade on a details panel toggled by a button. The key idea: nothing jumps, and the elements below slide by themselves.
2. **Pick an effect.** Every Appear and Disappear preset, with From, Distance, Duration, Start after and Easing in the playground. Demo: a validation message under a field.
3. **When the others move.** `OthersMove` While, Before and After, side by side. Demo: an accordion.
4. **Page intros.** `StartAfter` and `OnPageOpen` (InPlace, PushOthers, Skip), with Replay page. The title uses Typewriter.
5. **Smooth moves and resizes.** `MoveAndResize` on:
   - a "Save" to "Saving..." button;
   - a badge;
   - a collapsible sidebar;
   - a ChildWindow going from 300 to 600;
   - a card whose image loads;
   - a search box that widens on focus.

   The intro suggests turning on Show what animates, to see that everything involved moves together.
6. **Lists.** `ItemAppear`, `ItemDisappear`, `ItemMoveAndResize` and `ItemCascade`. Demos:
   - a to-do list (add and remove);
   - toasts;
   - sort and filter;
   - drag to reorder;
   - a streaming chat;
   - a virtualized list where scrolling plays no effects.
7. **Everything inside.** `ApplyTo=EverythingInside` on a settings form; a nested `None` that carves out a chart; the area outline; the popup boundary. Demo: a responsive layout that switches from 2 columns to 1 with a button.
8. **When the window or a panel is resized.** A WrapPanel of tiles with and without `AlsoOnWindowResize`, with the instruction "resize your browser window". A profile panel resized with a GridSplitter, whose content switches layout with `{Responsive ElementName=...}` while it's dragged. Cards that switch from a row to a column with the window.
9. **Text that moves.** Typewriter, Roll on a counter, Count up, Scramble and Retype, plus a live clock.
10. **Bouncy and Wave.** A tab underline with Bouncy; a column of cards pushed by a growing card, with a Wave slider.
11. **Magic move.** A segmented control with a selection pill, a tab underline, a list-to-detail photo, and a wizard with `MagicMoveByName`.
12. **From code.** `Animate.Changes`, `Animate.Skip`, `AnimationSettings` (Speed, DefaultDuration, the App.xaml resource) and reduced motion.
13. **What never animates, and why.** Live proof that the following stay instant:
    - scrolling;
    - dragging a GridSplitter or a Slider;
    - typing;
    - window resizing, except the values that switch with `{Responsive}` (the shapes switch size at 960 px).

    Also the performance budget, and the known limitations shown live (text re-wrapping, `Path` shapes).
14. **Putting it together.** A link to the Showcase port as the real-world example.

### When chapters are built

Each chapter is added in the phase that implements its feature, so the gallery grows with the library and each phase ends with a working, testable story:
- Phase 2: chapter 5, and the parts of chapter 13 about scrolling, dragging, typing and window resize.
- Phase 3: chapters 1 to 4, 6, 7, 8 and 14.
- Phase 4: the playgrounds, the live XAML and the summaries.
- Phase 5: chapter 9.
- Phase 6: chapter 10.
- Phase 7: chapter 11.
- Chapter 12 and the rest of chapter 13 are completed along the way.

Chapters 5, 7, 8, 10, 12 and 13 are built (chapter 10 early, with Bouncy and Wave); the others show a "coming soon" page. Their exact status is in [roadmap.md](roadmap.md).

## Acceptance scenarios (covered by the gallery chapters)

Each line is what the developer sets, and what must happen.

- **Show more details in a card:** `Appear=Fade` on the details panel. The card grows and the content below slides down.
- **Validation message under a field:** `Appear` Slide from Top, `Disappear=Fade`. The fields below slide.
- **Button label "Save" to "Saving...":** `MoveAndResize=Smooth` on the button. It widens, and the toolbar buttons to its right slide.
- **Badge "3" to "12":** `MoveAndResize=Smooth` and `TextChange=Roll`.
- **Collapsible sidebar (Width 240 to 64):** `MoveAndResize=Smooth` on the sidebar. The content area resizes with it.
- **ChildWindow Width 300 to 600:** `MoveAndResize=Smooth` on the ChildWindow. It grows and re-centers, and its stretched content grows in sync without overflowing.
- **Tab underline** (one element whose Grid.Column changes): `MoveAndResize=Bouncy`.
- **Bar chart** (Width bound to data): `MoveAndResize=Smooth` in the bar template.
- **Image loads, card grows:** `MoveAndResize=Smooth` on the card.
- **Search box widens on focus:** `MoveAndResize=Smooth` on the TextBox. Typing inside it never animates.
- **AI chat, streaming answer:** `ItemMoveAndResize=Smooth` and `ItemAppear` Slide from Bottom on the message list. The growing bubble pushes smoothly while text streams in.
- **Toasts:** on the toast StackPanel, `ItemAppear` Slide from Right, `ItemDisappear=Fade`, `ItemMoveAndResize=Smooth`.
- **Sortable, filterable list:** `ItemMoveAndResize=Smooth`. Items glide to their new places, also with recycling virtualization.
- **Drag to reorder:** `ItemMoveAndResize=Smooth`. While dragging, the other items glide out of the way; the dragged item follows the pointer without lag.
- **GridSplitter and Slider drags:** nothing lags behind the pointer, even inside an `EverythingInside` area.
- **Scrolling a virtualized ListBox with ItemAppear:** no effects play while scrolling.
- **WrapPanel of tiles that re-flow when the browser window is resized:** `ItemMoveAndResize` Smooth with `AlsoOnWindowResize=True` on the WrapPanel. Tiles glide to their new rows while the edge is dragged; the rest of the page snaps.
- **Accordion that expands its content only after the space has opened:** `Appear` Fade with `OthersMove=Before`.
- **Responsive page** (breakpoint switches from 2 columns to 1): `MoveAndResize` Smooth with `ApplyTo=EverythingInside` on the page root. The rearrangement animates; the window resize itself doesn't.
- **A side panel whose content switches layout while its edge is dragged:** `Orientation="{Responsive ElementName=Panel, Threshold=220, ...}"` on its content, and `ApplyTo=EverythingInside` on the panel. The edge follows the pointer; at the breakpoint, the content glides into its new layout while the drag goes on.
- **Settings form with toggles that show sub-options:** `ApplyTo=EverythingInside` on the form.
- **Page intro:** `Appear` with `StartAfter` on each element; the subtitle has `OnPageOpen=PushOthers`; the title uses `Typewriter`.
- **Selection pill across radio buttons:** `MagicMove=SelectionPill` on the template background. Travel is automatic, and resolves to Behind, whichever direction it travels.
- **Wizard pages:** `MagicMoveByName` on the Frame.
- **List to detail photo:** a bound `MagicMove` id on the thumbnail and on the detail image.

## When things animate (library logic)

- **Origins** (where a change starts):
  - An element re-measured with the same available size, on its first measure of the pass. A later measure in the same pass is the layout settling (for example a scroll viewer measuring again after showing a scroll bar) and doesn't count. It also doesn't count when one of its children was re-measured with the same available size in this pass and changed its desired size; then it's only carrying that child's change upward.
    - Children re-measured with a new available size are consequences, not causes. Example: when a ChildWindow's Width changes, its template's `ContentRoot` is re-measured at the new size, so the ChildWindow itself is the origin.
    - Implementation: when a child reports "same available size, desired size changed", its layout parent goes into a per-pass set. An element in that set isn't an origin.
  - An element re-arranged with an unchanged final rect because something inside it invalidated its arrange (for example Canvas.Left, or an alignment change).
  - An element appearing or disappearing.
  - An element where a `{Responsive}` value switched, and its parent.
- **Collecting.** During arrange the library keeps a stack and records, for each re-arranged element, its old and new rectangles (relative and absolute) and its origin.
  - The old rectangle is the one before the first change in this `UpdateLayout` call, so changes made in `SizeChanged` handlers (second loop) animate from what was on screen.
  - An element is affected if it's on the origin's ancestor chain, or if its parent is affected and its slot changed.
  - Elements arranged directly from the arrange queue get their absolute position by walking up the visual offsets.
- **Areas (how `EverythingInside` reaches the elements inside).** It is not an inherited property. When a change needs a decision, the library walks up the visual parents (`VisualTreeHelper.GetParent`) from the element in question. It stops at the first element whose `MoveAndResize` is `EverythingInside` (inside that area) or `None` (carved out), and passes over `ThisElement` settings.
  - Templates, items created later, and content swapped in later (Frame navigation, ContentControl content, DataTemplates) are covered automatically, because they're in the visual tree when the change happens.
  - Changing the setting at runtime takes effect on the next change.
  - Nothing is stored per element. Results are memoized per pass for every element on the walked path, so each element is visited at most once per pass. When no `EverythingInside` is set anywhere, the walk is skipped.
  - Popups are boundaries: `GetParent` returns null at a popup's root. A dropdown, tooltip, context menu or ChildWindow opened from inside the area isn't part of it. Set the property inside it instead; for example on the ChildWindow, which is its popup's `Child`.
  - The lookup is used both for origins ("does this change start inside an area?") and for affected elements ("does this element count as having `MoveAndResize`?"). So a change that starts outside the area but moves or resizes something inside it also animates.
- **Deciding, per origin at `OnLayoutPassCompleted`.** The whole group (the origin chain plus every affected element) animates when any of these holds:
  - it starts inside an `EverythingInside` area;
  - it moves or resizes an element with `MoveAndResize`, or an item of a list with `ItemMoveAndResize`;
  - it's a runtime appearance or disappearance of an element that has an effect (its own, an item effect, or an automatic one).
- **One timing per group** (Duration and curve), which keeps the containment guarantee. **The nearest setting wins:** going up from where the change starts, the timing comes from the first of:
  - the setting of the element where the change starts, even when its own box stays the same: an effect gives its Duration and Easing, and a bouncy effect moves the others smoothly;
  - an ancestor with `EverythingInside`;
  - an ancestor with another setting that moved or changed size in the same change. An ancestor that stays in place doesn't count: its setting is about its own box.

  When several changes start in the same group, the setting found the fewest levels up wins. Otherwise the timing comes from the nearest affected element with a setting (fewest tree hops, then document order), and otherwise from the defaults.
- **Page open.** An element appears together with its container when it isn't the topmost element appearing in that pass. Such appearances follow `OnPageOpen` and never push others unless it's `PushOthers`. A container that is shown at runtime is a page that opens, for the elements inside it.
- **Items re-sorted, filtered or refreshed.** The library keeps, for lists with item animations, a map from each data item to its rectangle from the previous completed pass. It records each container's item when it's first arranged, using `ItemContainerGenerator.ItemFromContainer` (public).
  - Containers that are removed, created or recycled in the same pass glide from their item's old rectangle to its new one, instead of running the item disappear and appear effects.
  - Collection changes are observed on `ItemsControl.Items` (`INotifyCollectionChanged`).
- **Never animates:**
  - **Window resizes** (new constraints on the roots), except for the elements that set `AlsoOnWindowResize` and their content. The rest of that group snaps.
    - A pass is a window-resize pass when `Application.Current.Host.Content` has a different size than at the previous pass. Such a pass has no origins at all: scroll bar parts and other elements that measure again would otherwise look like changes, and their group would climb to the root and take the whole page with it.
    - Groups that follow the window never join their containers, even when an element is arranged again later in the same pass.
    - **`{Responsive}` switches** are the exception, in a window resize and during a drag. A switch is a change, like a click: what was arranged before it follows the resize, and the rest of the pass only has the switches as origins. Each setting found going up from a switch animates what is inside it, with its own timing and its own message; the rest snaps with the resize. While the resize goes on, their animations go on too, and follow the new sizes.
  - **Settling after appearing.** For 0.5 s after an element is first arranged (a page, a dialog, new items), changes that start inside it snap silently: scroll bars showing up, fonts and images loading. An element released by `OnPageOpen=PushOthers` is the exception: it pushes the others even with a short `StartAfter`.
  - **Scroll chrome.** Changes that start in a scroll bar or in a part of a scroll viewer's template snap silently; they're consequences of a content change.
  - **Scrolling.** An arrange-only change of an `IScrollInfo` whose offset changed (OpenSilver scrolls by arranging, through `ScrollContentPresenter`). Items that virtualization realizes or removes in that pass get no effects either.
  - **Dragging.** A dragging state is on while a pointer button is held and the pointer has moved more than 4 px. A small JS pointer tracker notifies C# only when a drag starts and ends. During a drag, everything snaps except list items that move because the list order changed (drag-to-reorder), and `{Responsive}` switches. The element holding the mouse capture, and everything inside it, always snaps.
  - **Typing.** Changes that start inside the content of the focused text box (`FocusManager.GetFocusedElement()`). The text box's own Width and similar properties still animate.
  - Reduced motion (see the API), design mode, `AnimationSettings.IsEnabled = false`, `Animate.Skip`, and origins under `MoveAndResize=None`.
- **Performance:**
  - **Fast path.** An element with no animation value, outside any area and outside a group, costs one hash lookup per hook, with no allocations.
  - **Only changed rectangles** get animated. Children riding inside a moving parent don't get animations of their own.
  - **Off-screen culling.** An element is culled when both its old and new absolute rectangles are outside the window. Absolute rectangles include the offsets of `IScrollInfo` ancestors.
  - **Budget.** If more than about 300 elements remain in a group after culling, the whole group snaps (and the log says so), rather than risking overlaps.
  - **Bulk appearances** animate only the items in view, with the cascade capped at 0.6 s.

## Rendering: Web Animations

- **Moves and resizes.**
  - Position animates `translate` from the delta (old minus new) back to 0, with `composite: 'add'`, so it combines with a running effect or a previous move.
  - Size animates `width` and `height`.
  - The core's inline `left/top/width/height` are already the final values and show when the animation ends.
  - Start delays use `fill: 'backwards'`.
- **One JS call per pass**, addressing elements by `Interop.GetDiv(e).Uid`, with a shared start time: the next frame, not the time of the frame the call runs in, which can be long gone after a big layout pass. It goes into the same flush as the core's arrange writes. The core removes `uielement-unarranged` in `osjs.arrange`, earlier in the same batch.
- **Curves.** They're passed as CSS `linear()`. On browsers without it, the curve is baked into keyframe offsets.
- **Containment guarantee.** All elements of a group share start time, duration and curve, and every edge is interpolated with the same progress value. Being inside the parent is a set of linear inequalities, so a child that is inside its parent before and after stays inside at every frame.
  - For layouts that are linear in the parent size (stretch, alignment, margins, star columns without Min/Max), the in-between frames equal the real layout at that size.
  - For non-linear ones (wrap re-flow, Min/Max kicking in), elements glide from the old place to the new one, still inside the parent, and siblings may cross paths briefly.
  - Wave delays never apply to the origin's descendants. Elements inside a delayed member share its delay, but the containers of the origin pass nothing down, so the siblings of the origin each get their own delay. Delays are divided by `Speed`.
- **Interruptions.** A new animation of an element that is already animating starts from its computed current `translate`, `width` and `height`, so continuous updates such as streaming text stay fluid. The library tracks running animations in C#; a non-animated arrange of such an element cancels its animation.
- **Clips.** Layout clips are written as `clip-path: path(...)` in pixels (`EnsureClip` and `SetClipPath`), so they don't follow a resizing box on their own.
  - When an element's clip is exactly its own box (`LayoutInformation.GetLayoutClip` is the rectangle of its RenderSize, with no `Clip` set), the animation holds `clip-path: inset(0)`, which follows the animated box. `ScrollContentPresenter` and `ClipToBounds` are like that.
  - Other clips snap to their final value.
- **Shapes.** The core writes `Rectangle` and `Ellipse` SVG geometry straight to its final size, and `.opensilver-shape` is `overflow: visible`.
  - For shapes that fill their box, the geometry differs from the box by a constant stroke inset. So the library animates the SVG geometry CSS properties (`x/y/width/height`, `rx/ry/cx/cy`) from the final values plus the box delta, in the same animation.
  - Where a browser lacks these properties, the geometry snaps. `Path` geometry always snaps.
- **Effects** animate only `opacity`, `translate`, `scale`, `rotate`, `filter`, `clip-path` and a temporary `z-index`. They never touch dependency properties.
  - Opacity ends at the element's own opacity.
  - `filter` keyframes keep the element's existing filter and add `blur()`.
  - Every element has `transform-origin: 0 0`, so Zoom, Pop and Rotate add a compensating `translate` to pivot around the center. Rotation is sampled in several keyframes, because the compensation isn't linear.
- **Stacking.** Every element div is `position: absolute; z-index: 0`, which makes it a stacking context painted in DOM order. Raising an element's `z-index` above 0 therefore puts it above its siblings. Nothing can pass behind an element outside its own parent, which is why Magic move uses clones.

## Appear and disappear mechanics

- **Triggers.**
  - Appear: first arrange after attach, and Collapsed/Hidden to Visible.
  - Disappear: detach of a visible element, and Visible to Collapsed/Hidden.
  - Only the topmost element appearing or disappearing gets automatic effects.
  - Explicit Appear effects always run, also inside an element that appears: they play with their container, without making room.
  - Explicit Disappear effects run for the topmost element that disappears only. The elements inside it are part of its ghost: the core notifies the detach of the removed element, not of its descendants, and a collapsed parent hides them without changing their own Visibility.
  - The hooks ignore the Visibility changes made by the library's own holds.
  - **Until the layout pass.** An element that becomes Visible again with an explicit Appear, or inside an area, is kept invisible by the script from the Visibility change to the batch, so that it never shows for a frame before its effect. Hidden to Visible needs no layout pass, so its effect plays at once.
- **Start delays.** `StartAfter` with the `backwards` fill keeps the element hidden until its effect starts.
- **The hold (validated against the runtime code).** It is used for `OnPageOpen=PushOthers` only, even with no delay. A runtime appearance with `StartAfter` doesn't use it: the room opens at once, and only the effect waits.
  - **Mechanism.** A `Storyboard` with one `ObjectAnimationUsingKeyFrames` (a `DiscreteObjectKeyFrame` at `KeyTime=0`, value `Collapsed`), targeted with `Storyboard.SetTarget` and `new PropertyPath(UIElement.VisibilityProperty)`. Its `Duration` is zero, not `Forever`: it holds its value at the end (`HoldEnd`), and its clock is completed, so it asks for no frames, and an element that is never added doesn't keep a clock running.
  - **Apply it synchronously.** `Begin()` followed immediately by `SeekAlignedToLastTick(TimeSpan.Zero)`. A plain `Begin()` is too late for templates created during Measure, changes in `SizeChanged` handlers, and explicit `UpdateLayout()` calls.
  - **Trigger points.** For page open, the hold starts from the property-changed callback, when the element isn't loaded yet; an Appear set on an element already on the page is for its next appearance. When the container appears at runtime, it starts re-entrantly from the `OnVisibilityChanged` hook; this is safe because the store commits the value before notifying, and both DOM class writes land in the same batch. There's no hold when a global snap reason applies, except dragging.
  - **Page open or not.** At the end of the first layout pass after `Loaded`, the nearest ancestor arranged in that pass decides: if it appeared, the element opened with its page. Otherwise it was added on its own: it's released at once, and appears like any element added at runtime.
  - **Release.** `StartAfter` after the end of that pass. Releases less than 20 ms apart happen together, in the same layout pass. It is posted with `Dispatcher.BeginInvoke`, or a `DispatcherTimer`, and done with `Stop()`. The app's own Visibility value wins at release. A container hidden and shown again before the release starts the wait again.
  - **Side effect to document.** When a hold starts on an element that was just shown, `IsVisibleChanged` fires true, then false, then true.
- **OthersMove=Before / After.**
  - For Appear, the element's effect gets a delay equal to the others' move duration, plus its `StartAfter`.
  - For Disappear, the rest of the group (neighbors and containers) gets a delay equal to the ghost's `StartAfter` and duration.
- **Stacking.** While an appear effect pushes others, the element is raised above its siblings by a `z-index` inside the animation.
- **Ghosts.**
  - **Capture.** At `OnDetaching`, or in the `OnVisibilityChanged` hook, the library deep-clones the element's DOM.
    - It strips ids, sets `pointer-events: none` and `data-os-foreign`, and copies canvas bitmaps.
    - It removes the `uielement-collapsed` class (`display: none !important`, which no animation can override) and the `uielement-hidden` class.
    - The clone stays in the parent at the element's old DOM position, so its stacking among siblings doesn't change, and at its last arranged rectangle.
  - **Starting look.** The clone starts from the element's computed look at capture time (opacity, translate, scale), so an element that disappears while still appearing doesn't jump.
  - **Animate.** It animates at `OnLayoutPassCompleted`, then is removed. An element that becomes Hidden keeps its space, so there is no layout pass: its ghost plays at once. A ghost that no batch plays is removed after 1.5 s.
  - **Toggling back.** If the element appears again while its ghost is animating, the ghost is removed and the appear effect starts from the ghost's current look.
  - **Fidelity limits.** Video, audio, iframes and plugins become an empty box of the same size in the clone, so that they don't load or play again, and the ghost fades out in 0.15 s instead of playing its effect. An element that is itself a video or an iframe gets no ghost.

## Bouncy and Wave

- **Bouncy.** A damped-spring curve sampled into CSS `linear()`. Only moves bounce; sizes use Smooth with the same duration, to avoid wobbly content. The containment guarantee can be exceeded by the bounce amount.
- **Wave.** Each affected element starts later the farther it is from where the change started.
  - The distance is Euclidean between absolute rectangles: `delay = Wave x 200 ms x min(1, distance / 600 px)`.
  - The bounce also decreases with distance.
  - Descendants share their nearest delayed ancestor's delay, and the origin's descendants get none.

## Magic move

- **Pairing.**
  - An element with id K disappears and another with K appears in the same pass. Or the disappearing one comes first and the appearing one follows within 300 ms; meanwhile the disappearing one's ghost stays frozen. If nothing arrives, it plays its normal disappear effect.
  - When several candidates exist, the pair with the closest common ancestor wins, so templates can share ids safely.
  - A pair replaces both elements' Appear and Disappear effects. Unpaired elements run their normal effects.
  - Moving the same element to a new parent in one pass also pairs with itself.
- **Geometry.** Measured in JS (`getBoundingClientRect`), which includes transforms, scrolling and in-flight animations.
- **Travel.** A travelling layer (a clone of the old element plus a clone of the new one, cross-fading) moves from the old rectangle to the new one. The real new element stays invisible until the layer arrives; then the layer is removed.
  - `Above`: the layer goes in an overlay at the top of the window root, which escapes clipping.
  - `Behind`: the layer is inserted as the first child of the common ancestor's div. It's painted above the ancestor's own background but below all its children, whichever direction it travels.
  - `Automatic`: Behind when the element is the bottom-most child covering its container (a background); otherwise Above.
- **Look.** For identical elements the cross-fade looks exactly like a move, so there's no option. Images are resized by scaling; everything else by width and height.
- **Templates.** Visibility changes from the VisualStateManager are detected. Templates whose states use Opacity need to switch to Visibility (the designer hints this).
- **MagicMoveByName.** When the content is swapped, the library collects the named elements of the old content (walking its visual tree) and captures their clones before removal. It pairs them with the named elements of the new content through `FindName`. The old content itself runs a fade.

## Text animations

- **Detecting changes.** `TextBlock.Text` is mirrored through a private attached property binding, which gives old and new values.
- **Overlay.** A library overlay (`data-os-foreign`) inside the TextBlock's div renders the animated characters.
  - During the effect, the TextBlock's div gets `visibility: hidden` and the overlay `visibility: visible`.
  - Measurement is unaffected: `measureTextBlock` receives its HTML from C#.
  - Inlines inserted by `createVisual` skip the overlay (core change 2).
- **Effects.**
  - Typewriter, FadeLetters and Backspace use spans that only animate opacity, so wrapping matches exactly.
  - Roll uses a character diff, and works on single-line text only in v1. It's clipped by the TextBlock's `overflow: clip`.
  - Rich inlines fall back to CrossFade.

## Showcase parity (reference demo)

- **Straight swaps.** The effects map to `Appear` with `StartAfter`.
- **AnimatedContentControl** becomes `OnPageOpen=PushOthers`.
- **Source-code pane.** The `PropertyAnimator` row code becomes `MoveAndResize` Smooth on the pane's row content.
- **Known differences:**
  - The neighbors move with the effect's duration, instead of 1.5 times it.
  - Text re-wraps live while its container animates.
  - Appearing elements sit at their final position rather than centered in the growing slot.

## Known limitations

- `Path` geometry (and Rectangle/Ellipse where browsers lack CSS geometry properties) snaps inside animated boxes.
- Text wraps at the current box width during the animation, clipped to its box by the core's `overflow: clip`. The interpolated height is normally enough, because the needed height shrinks roughly as 1/width, which lies below the straight line between the two sizes. Near a line-count step, the last line can be cut for a few frames.
- Clips other than the element's own box snap to their final value.
- Anything positioned by app code from `ActualWidth` or `SizeChanged` jumps to its final value. `TransformToVisual` and `ActualWidth` report final values immediately.
- The `RenderTransformOrigin` offset snaps.
- Ghost clones don't carry the animations running inside them; inner elements show their final look.
- Storyboards that animate layout properties of animated elements get smoothed twice.
- If the app's own storyboard animates Visibility during a hold, the app wins.
- In the Simulator, JS batches can arrive a frame apart.

## Phases

- **Phase 0:** spike (the "Validate first" list).
- **Phase 1:** core PR.
- **Phase 2:** MoveAndResize (Smooth) with the grouping rules, the exclusions, the performance guards and the diagnostics; the gallery app shell with chapter 5.
- **Phase 3:** Appear/Disappear, OnPageOpen and OthersMove, and Items, with their gallery chapters; then the Showcase port.
- **Phase 4:** `AnimationCatalog` and `AnimationDesign` (library side), and the gallery playgrounds built on them.
- **Phase 5:** text.
- **Phase 6:** Bouncy and Wave (done early, together with phase 2).
- **Phase 7:** Magic move.
- **Later:** Hover, ContentTransition.

Not in these phases: the XAML.io Animate tab, which is built in the XAML.io repo from the "Designer panel" specification and the library's `AnimationCatalog`/`AnimationDesign`.
