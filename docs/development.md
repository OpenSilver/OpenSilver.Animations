# Development: build, run and test

## Where things are

| Path | What it is |
| --- | --- |
| `C:\VSProjects\UserwareSource\OpenSilver` | The OpenSilver repository, on branch `feat/internalhooks`. It holds the core changes (see [architecture.md](architecture.md)). |
| `C:\VSProjects\UserwareSource\OpenSilver\OpenSilver.Animations` | This repository (remote `github.com/OpenSilver/OpenSilver.Animations`). It sits inside the OpenSilver folder but is a separate git repository. |
| `C:\VSProjects\UserwareSource\OpenSilver\OpenSilver.Themes.Modern` | Another library repository, the model for this repository's layout and build scripts. |
| `C:\VSProjects\UserwareSource\OpenSilver\OpenSilver.Samples.Showcase` | The Showcase, ported to this library on branch `feat/use-new-animations-library` (see "Run the Showcase on the local library"). Its own animation project is removed there. |

The three nested folders show up as untracked in the OpenSilver repository. They must never be committed into it.

## Prerequisites

- **.NET SDK 10** (10.0.401 was used).
- **Visual Studio's `msbuild` in the PATH** for the `.bat` scripts, for example from a Developer PowerShell. The OpenSilver build scripts need it.
- The OpenSilver repository on branch `feat/internalhooks`.

## The OpenSilver packages with the hooks

The library needs `OpenSilver.InternalHooks`, an internal class of OpenSilver that it sees through `InternalsVisibleTo("OpenSilver.Animations")`. It is not in a published OpenSilver package yet, so OpenSilver is built locally:

```bat
build\Build_Local_OpenSilver.bat C:\VSProjects\UserwareSource\OpenSilver
```

- It builds the `OpenSilver`, `OpenSilver.WebAssembly` and `OpenSilver.Simulator` packages with version `3.4.0-preview-animations-local`; a second argument gives another version.
- It copies them to `build\local-packages`, which `NuGet.Config` lists as a source. The `.nupkg` files are git-ignored.
- The version never changes between local builds, so the script also deletes the cached copies of that version under `%USERPROFILE%\.nuget\packages`.
- It takes a few minutes. Run it again after every change in the OpenSilver repository, then rebuild this solution.

`Directory.Build.props` sets `OpenSilverVersion` (default `3.4.0-preview-animations-local`) and `LangVersion` 12. Every project uses `$(OpenSilverVersion)`. The samples pin it exactly (`[$(OpenSilverVersion)]`), and the library uses it as a minimum.

## Build and pack

```powershell
cd C:\VSProjects\UserwareSource\OpenSilver\OpenSilver.Animations
dotnet build OpenSilver.Animations.slnx -c Release
```

- The expected result is 0 errors.
- The only warnings are NU1902/NU1903 security advisories for `System.Security.Cryptography.Xml` and `System.Security.Cryptography.Pkcs`. They come with the OpenSilver Simulator and WebAssembly packages, not from this repository.

To make the package:

```bat
build\Build_NuGet.bat 0.1.0-preview 3.4.0-preview-animations-local
```

The arguments are the OpenSilver.Animations version and the OpenSilver version, which defaults to the first. The package goes to `build\output` (git-ignored). For a quick check without msbuild, run `dotnet pack src\OpenSilver.Animations\OpenSilver.Animations.csproj -c Release -o artifacts\test-pack`, then delete `artifacts`.

The package contains `lib/netstandard2.0/OpenSilver.Animations.dll` and its XML documentation; the script is embedded in the dll. `icon.png` comes from `build/`, and the license is MIT.

**A local package for an app.** To try the library in an app before it's published, like the Showcase:

```bat
build\Build_Local_Package.bat
```

- It packs `OpenSilver.Animations` `0.1.0-preview-local` for OpenSilver `3.4.0-preview-animations-local` into `build\local-packages`, next to the OpenSilver packages. The two arguments give other versions.
- The version never changes, so it also deletes the cached copy under `%USERPROFILE%\.nuget\packages`. Rebuild the app after each run.

## Run the Showcase on the local library

The Showcase (`OpenSilver.Samples.Showcase`, branch `feat/use-new-animations-library`) uses the local packages:

- its `nuget.config` lists `../OpenSilver.Animations/build/local-packages` as a source;
- its `Directory.Build.props` sets `OpenSilverVersion` to `3.4.0-preview-animations-local` and `OpenSilverAnimationsVersion` to `0.1.0-preview-local`, with the versions of the themes, the ControlsKit and Blazor;
- the C#, F# and VB projects reference `OpenSilver.Animations` with `$(OpenSilverAnimationsVersion)`.

After `Build_Local_OpenSilver.bat` and `Build_Local_Package.bat`:

```powershell
cd C:\VSProjects\UserwareSource\OpenSilver\OpenSilver.Samples.Showcase
dotnet build src\OpenSilver.Showcase.Browser\OpenSilver.Showcase.Browser.csproj -c Debug
dotnet run --project src\OpenSilver.Showcase.Browser\OpenSilver.Showcase.Browser.csproj --no-build --urls http://localhost:55622
```

- The C# build goes to `bin\Debug_cs`. The expected result is 0 errors; the OS0003 warnings say that the ControlsKit was built for an older OpenSilver.
- Check that the build took the new package: the `OpenSilver.Animations.dll` in `bin\Debug_cs\net8.0\wwwroot\_framework` has the time of the last `Build_Local_Package.bat`.
- What to look at: the Welcome page intro (the particles, the title that overshoots, the subtitle and the card that push the others without drifting), the menu that slides in from the left after 4.5 s, the "Fork me" and "Suggest" buttons, the hovers (Nudge), "View source" (the pane slides up from the bottom of the window), and the menu on a small screen.

**CI.** `.github/workflows/build-package.yml` is started by hand (`workflow_dispatch`).

- It takes an `opensilver-version` input, which must be a published OpenSilver that includes `InternalHooks`.
- It builds the version `0.1.0-preview-<date>-<sha>` with `Build_NuGet.bat`, and pushes it to the OpenSilver MyGet feed with the `MYGET_TOKEN` secret.
- It only runs in the `OpenSilver` organization.

## Run the gallery

The gallery is three projects:

- `samples/OpenSilver.Animations.Gallery`: the app, `netstandard2.0`;
- `samples/OpenSilver.Animations.Gallery.Browser`: the WebAssembly host, `net10.0`;
- `samples/OpenSilver.Animations.Gallery.Simulator`: the desktop Simulator, `net10.0-windows`, started by `SimulatorLauncher.Start<App>()`.

In a browser:

```powershell
dotnet build OpenSilver.Animations.slnx -c Debug
dotnet run --project samples\OpenSilver.Animations.Gallery.Browser\OpenSilver.Animations.Gallery.Browser.csproj -c Debug --no-build --urls http://localhost:55620
```

- Wait for "Now listening", then open `http://localhost:55620/`. Each chapter has its own URL, for example `http://localhost:55620/#/chapter/5`.
- **Restart the server after every build**, then reload the page.
- To judge smoothness, use `-c Release`: the Debug build is slower, and the first frames of an animation can stutter there.
- **Stop the server** by killing its `dotnet` processes; they exit with code -1, which is expected:

  ```powershell
  Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -match 'Gallery.Browser' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
  ```

The gallery has four options, all off at start:

- **Slow motion** sets `Speed` to 0.25.
- **Show what animates** sets `ShowWhatAnimates`.
- **Reduce motion** sets `SimulateReducedMotion`.
- **Replay page** opens the chapter again.

The console functions, like `osAnimations.slowMotion(4)`, change the same settings, and the switches follow them, except Slow motion.

The log, "What just happened", shows the `Logged` messages. Each demo has a "What should happen" expander with its acceptance lines.

The shell is responsive (see "The gallery chapters" in [roadmap.md](roadmap.md)). From 1280 px, the options are in the top bar and the log is on the right. From 960 px, the log is a drawer opened by the Log button. Below that, the options and the chapters are in a drawer opened by the menu button.

- **The breakpoints** are `ResponsiveThreshold.Default`, set in `App`. The XAML uses `{Responsive Mobile=..., Tablet=...}` values (the Desktop value falls back to Tablet).
- **Code** uses `Shell.WindowSize`: `WindowSize.IsPhone` for sizes that differ on a phone, as in chapters 8, 12 and 13.
- **Measure the window, not the page.** `{Responsive}` compares the width of the `Window`, which draws a 1 px border around the page, so the page is 2 px narrower. Code that compared the page's width chose the phone layout at 960 and 961 px while the XAML chose the tablet one.

## Testing in the browser as an agent

The agent's browser tools (`cursor-ide-browser`) drive the gallery. This is how phase 2 was verified.

### Workflow

1. Start the server (see above) and wait for "Now listening".
2. List the tabs, `browser_navigate` to `http://localhost:55620/#/chapter/N` if needed, and `browser_lock` the tab.
3. Register the helper scripts below once per tab, with `browser_cdp` `Page.addScriptToEvaluateOnNewDocument` (`{ "source": "..." }`), after `Page.enable`: without it, the scripts don't run. Then reload.
4. **Reload with `Runtime.evaluate` `location.reload()`.** Navigating to a URL that only differs by its hash doesn't reload the app. To go to a chapter and reload: `location.hash = '#/chapter/13'; location.reload()`.
5. Wait about 8 seconds for the app to start, then run the test as an async `Runtime.evaluate` with `awaitPromise: true` and `returnByValue: true`. Return JSON strings.
6. Use `browser_take_screenshot` to look. Screenshots are scaled down: compare `innerWidth` with the screenshot width before using screenshot coordinates. It's easier to find elements by text with the helpers.
7. `browser_lock` with `unlock` when done, and stop the server.

### Simulating a window resize

```json
{ "method": "Emulation.setDeviceMetricsOverride", "params": { "width": 1600, "height": 950, "deviceScaleFactor": 1, "mobile": false } }
```

Then change `width`, for example to 1350 and back. It takes effect after a short delay, and the size can survive a reload. Finish with `Emulation.clearDeviceMetricsOverride`.

### Helper scripts

**Console capture.** Every `console.log`, `warn` and `error` goes into `window.__logs`. The library's messages start with `[OpenSilver.Animations]` when `LogForDebugging` is on; the gallery leaves it off and shows the `Logged` messages in its log.

```js
window.__logs = [];
for (const k of ['log', 'warn', 'error']) {
  const o = console[k].bind(console);
  console[k] = (...a) => { try { window.__logs.push(k + ': ' + a.map(x => (x && x.stack) ? x.stack : String(x)).join(' ')); } catch {} o(...a); };
}
```

**Pointer capture for synthetic events.** OpenSilver captures the pointer with `setPointerCapture`, which throws for untrusted events. This patch fakes capture, for tests only. Buttons need it too: they capture the mouse when pressed, so without it no click from a script or from `browser_click` clicks. With it, a Slider thumb drag worked once. For a GridSplitter, use the patch of "Dragging a GridSplitter" below instead.

```js
(() => {
  const cap = new Set();
  window.__captured = cap;
  Element.prototype.setPointerCapture = function () { cap.add(this); };
  Element.prototype.hasPointerCapture = function () { return cap.has(this); };
  Element.prototype.releasePointerCapture = function () { cap.delete(this); };
})();
```

**Test helpers (`window.__t`) and the animation recorder (`window.__anims`).** Every `element.animate` call is recorded: id, class, animated keys, first keyframe, duration, delay, composite and easing.

```js
(() => {
  const wait = ms => new Promise(r => setTimeout(r, ms));
  window.__anims = [];
  const origAnimate = Element.prototype.animate;
  Element.prototype.animate = function (k, o) {
    try {
      const last = Array.isArray(k) ? k[k.length - 1] : k;
      window.__anims.push({
        id: this.id || this.tagName,
        cls: (typeof this.className === 'string' ? this.className : '').replace('opensilver-uielement ', '').slice(0, 40),
        keys: Object.keys(last || {}).filter(x => x !== 'offset').join(','),
        first: JSON.stringify(Array.isArray(k) ? k[0] : k).slice(0, 120),
        d: o && o.duration, delay: o && o.delay, comp: o && o.composite, easing: o && o.easing && String(o.easing).slice(0, 40),
      });
    } catch (e) {}
    return origAnimate.call(this, k, o);
  };
  const pointer = { bubbles: true, cancelable: true, composed: true, pointerId: 1, isPrimary: true, pointerType: 'mouse', view: window };
  const captured = () => (window.__captured && window.__captured.size ? [...window.__captured][0] : null);
  window.__t = {
    wait,
    // Elements whose whole text is t, in DOM order (outermost first).
    all(t) { return [...document.querySelectorAll('.opensilver-uielement')].filter(e => e.textContent.trim() === t && e.getBoundingClientRect().width > 0); },
    find(t, n) { return this.all(t)[n || 0]; },
    findDeep(t, n) { const a = this.all(t); return a[a.length - 1 - (n || 0)]; },
    rect(e) { const b = e.getBoundingClientRect(); return [Math.round(b.x), Math.round(b.y), Math.round(b.width), Math.round(b.height)]; },
    center(t, n) { const e = this.findDeep(t, n); if (!e) return null; const b = e.getBoundingClientRect(); return [Math.round(b.x + b.width / 2), Math.round(b.y + b.height / 2)]; },
    tapAt(x, y) {
      const target = document.elementFromPoint(x, y);
      const o = { ...pointer, clientX: x, clientY: y };
      target.dispatchEvent(new PointerEvent('pointerover', o));
      target.dispatchEvent(new PointerEvent('pointermove', o));
      target.dispatchEvent(new PointerEvent('pointerdown', { ...o, button: 0, buttons: 1 }));
      target.dispatchEvent(new MouseEvent('mousedown', { ...o, button: 0, buttons: 1 }));
      target.dispatchEvent(new PointerEvent('pointerup', { ...o, button: 0, buttons: 0 }));
      target.dispatchEvent(new MouseEvent('mouseup', { ...o, button: 0, buttons: 0 }));
      target.dispatchEvent(new MouseEvent('click', { ...o, button: 0 }));
      return target.id;
    },
    // Takes the text of the element to click, not the element.
    tap(t, n) { const c = this.center(t, n); return c ? this.tapAt(c[0], c[1]) : 'not found: ' + t; },
    // Moves go to the element that captured the pointer, if any, as in a real browser.
    async drag(x0, y0, x1, y1, steps) {
      const target = document.elementFromPoint(x0, y0);
      target.dispatchEvent(new PointerEvent('pointerdown', { ...pointer, clientX: x0, clientY: y0, button: 0, buttons: 1 }));
      target.dispatchEvent(new MouseEvent('mousedown', { ...pointer, clientX: x0, clientY: y0, button: 0, buttons: 1 }));
      for (let i = 1; i <= steps; i++) {
        const x = x0 + (x1 - x0) * i / steps, y = y0 + (y1 - y0) * i / steps;
        const tgt = captured() || document.elementFromPoint(x, y) || target;
        tgt.dispatchEvent(new PointerEvent('pointermove', { ...pointer, clientX: x, clientY: y, button: -1, buttons: 1 }));
        tgt.dispatchEvent(new MouseEvent('mousemove', { ...pointer, clientX: x, clientY: y, button: 0, buttons: 1 }));
        await wait(30);
      }
      const tgt = captured() || document.elementFromPoint(x1, y1) || target;
      tgt.dispatchEvent(new PointerEvent('pointerup', { ...pointer, clientX: x1, clientY: y1, button: 0, buttons: 0 }));
      tgt.dispatchEvent(new MouseEvent('mouseup', { ...pointer, clientX: x1, clientY: y1, button: 0, buttons: 0 }));
    },
    wheel(dy, x, y) {
      x = x || innerWidth / 2; y = y || innerHeight / 2;
      document.elementFromPoint(x, y).dispatchEvent(new WheelEvent('wheel', { bubbles: true, cancelable: true, deltaY: dy, deltaMode: 0, clientX: x, clientY: y }));
    },
    // Scrolls the chapter until the element with this text is in view.
    async scrollTo(text, top) {
      top = top || 150;
      for (let i = 0; i < 60; i++) {
        const c = this.center(text);
        if (c && c[1] > top && c[1] < innerHeight - 150) return c;
        this.wheel(c && c[1] < top ? -200 : 200);
        await wait(120);
      }
      return this.center(text);
    },
    // The texts of the log ("What just happened") when it's on the right, top to bottom, without the times.
    panel() {
      return [...document.querySelectorAll('.opensilver-textblock')]
        .map(e => [e, e.getBoundingClientRect()])
        .filter(([e, b]) => b.x > innerWidth - 330 && b.y > 120 && b.width > 0)
        .sort((a, b) => a[1].y - b[1].y)
        .map(([e]) => e.textContent)
        .filter(s => !/^\d\d:\d\d:\d\d$/.test(s));
    },
    // Runs action, then records the rects of els at every frame for ms milliseconds.
    async sample(els, ms, action) {
      const samples = []; const t0 = performance.now(); let done = false;
      const tick = () => { samples.push([Math.round(performance.now() - t0), ...els.map(e => e ? this.rect(e) : null)]); if (!done) requestAnimationFrame(tick); };
      window.__anims = [];
      const r = action(); tick(); await wait(ms); done = true;
      return { r, samples };
    },
  };
})();
```

The `panel` cut-off (`innerWidth - 330`) and the drags following the captured element are changes made while writing these helpers down: the versions used during testing had a fixed `x > 1100` (for a 1600 px window) and a patch applied at runtime. Check them on first use.

### Example: click and read what happened

```js
(async () => {
  __anims.length = 0;
  const r = __t.tap('Save');
  await __t.wait(500);
  return JSON.stringify({ r, animations: __anims.length, panel: __t.panel().slice(0, 4) });
})()
```

On chapter 5 this gives 10 animations, or 20 with Show what animates on (10 moves plus 10 outlines). The newest message is "TextBlock "Saving..." changed size; 10 elements animate together (Smooth, 0.25 s, from the MoveAndResize of SaveButton)."

### Testing a phone

```json
{ "method": "Emulation.setDeviceMetricsOverride", "params": { "width": 390, "height": 844, "deviceScaleFactor": 2, "mobile": true } }
```

Two more helpers were added at runtime for the responsive checks. In a narrow window, off-screen copies of a text (in a closed drawer, for example) are easy to hit, so `vis` keeps the matches that are on screen, and `center` uses it:

```js
__t.vis = function (s) { return this.all(s).filter(e => { const b = e.getBoundingClientRect(); return b.right > 0 && b.left < innerWidth && b.bottom > 0 && b.top < innerHeight; }); };
__t.center = function (s, n) { const a = this.vis(s); const e = a[a.length - 1 - (n || 0)]; if (!e) return null; const b = e.getBoundingClientRect(); return [Math.round(b.x + b.width / 2), Math.round(b.y + b.height / 2)]; };
// The elements wider than the window: [] means that nothing overflows.
__t.overflow = () => [...document.querySelectorAll('.opensilver-uielement')].filter(e => { const b = e.getBoundingClientRect(); return b.width > 0 && b.right > innerWidth + 1 && getComputedStyle(e).display !== 'none'; }).slice(0, 8).map(e => e.id + ' ' + (e.textContent || '').trim().slice(0, 30) + ' ' + JSON.stringify(__t.rect(e)));
```

The menu button is at about (22, 28): `__t.tapAt(22, 28)` opens the drawer.

### Dragging a GridSplitter

This drove the profile panel of chapter 8 across its `{Responsive}` breakpoint. The capture patch keeps the real capture when it works, and raises `lostpointercapture` on release, which OpenSilver needs to end the drag:

```js
const P = Element.prototype;
const set = P.setPointerCapture, has = P.hasPointerCapture, rel = P.releasePointerCapture;
P.setPointerCapture = function (id) { try { set.call(this, id); } catch { this.__cap = id; } };
P.hasPointerCapture = function (id) { return this.__cap === id || has.call(this, id); };
P.releasePointerCapture = function (id) {
  if (this.__cap === id) { this.__cap = undefined; this.dispatchEvent(new PointerEvent('lostpointercapture', { pointerId: id, bubbles: true })); return; }
  try { rel.call(this, id); } catch {}
};
```

Then dispatch every event on the splitter's div (`[class*="GridSplitter"]`, Debug builds only), with the same `pointerId`:

1. `pointerover`, then `pointerdown` and `mousedown` with `button: 0, buttons: 1`.
2. One `pointermove` per animation frame (`await` a `requestAnimationFrame` between them), with `button: -1, buttons: 1` and a growing `clientX`. Record positions in the same loop.
3. `pointerup` and `mouseup` with `buttons: 0`.

### Hovering and pressing

`browser_mouse_click_xy` dispatches synthetic events on the element under the point only: `pointerenter` never reaches its ancestors, so the element that reacts, usually an outer div, never gets it. Simulate the mouse instead, as chapter 12 was tested:

1. Keep the chain of the element under the pointer (`document.elementFromPoint`, then its ancestors).
2. On each move, dispatch `pointerout` on the old target, `pointerleave` on each element left (non-bubbling), `pointerover` on the new target, `pointerenter` on each element entered, from the outside in (non-bubbling), then a bubbling `pointermove` on the target. Wait 16 to 30 ms between moves, so that Nudge can measure a speed.
3. Press with a bubbling `pointerdown` on the target, `buttons: 1`, and release with `pointerup`, `buttons: 0`. `pointerType: 'touch'` checks that hover doesn't play.
4. Read `getComputedStyle(element).scale`, `translate` and `rotate` on the element that reacts, and `getAnimations()`.

Scroll with the page's ScrollViewer (wheel events), never with `scrollIntoView`: it scrolls the OpenSilver root out of the window, and `elementFromPoint` then finds nothing.

### Recording frames and batches

- **Frames.** A `requestAnimationFrame` loop that pushes `[time, rects..., animation count]` into a `window` array, stopped by time rather than by a number of frames, so that it's still running when the next tool call resizes the window. Keep only the frames that differ from the previous one before returning them.
- **Batches.** `globalThis.osAnimations` is frozen, but the global itself can be replaced by a wrapper whose `run` records the batch, then calls the real one. That's how the shapes of chapter 14 turned out to be animated, but ended before they were painted (see "Start time" in [architecture.md](architecture.md)).
- **Batches from the first frame**, for page intros. Register an init script (`Page.addScriptToEvaluateOnNewDocument`) that defines `globalThis.osAnimations` as an accessor: its setter receives the real object from `animations.js` and stores a frozen wrapper whose `run` and `hold` push copies of their arguments into `window` arrays before calling the real ones. When the page opens, changes below the fold are culled: emulate a tall window (1600 × 2600) to see their moves.
- **Messages below 1280 px.** The log is then a closed drawer, and new entries only show in the DOM once it is opened. Open it with the Log button, or test messages at 1280 px or more.

### Testing pitfalls

- **The log column in the DOM.** New entries are inserted at the top on screen, but their DOM nodes are appended (see "DOM order" in [architecture.md](architecture.md)). Read it with `panel()`, which sorts by position, not with `innerText`.
- **Finding elements.** `find` matches the whole text of an element, and the first match can be an outer element. `findDeep`, which `tap` uses, takes the innermost one. If text is ambiguous, find elements by position or by DOM structure.
- **Type-name classes** such as `[class*="Controls.Slider"]` exist only in Debug builds of the gallery.
- **Interrupting an animation** in a Debug build can look like a jump. Measure it with `sample` before concluding: in chapter 5, the dialog's second animation started from 556.7 px, exactly where the first one was.
- **The emulated window size** sometimes survives a reload. Clear it at the end.
- **Stuck at 97% or 99% after a rebuild.** The browser can keep a broken cache entry for one of the `_framework` files and load it with `force-cache`. A file can also keep its fingerprinted name while its content changes, as `.pdb` files do in Debug builds: the console then says "Failed to fetch", because the cached copy doesn't match its integrity hash. Turn the cache off with `Network.enable`, then `Network.setCacheDisabled` (`{ "cacheDisabled": true }`), and navigate to the page with a new query string, like `?r=2`. Turn the cache back on at the end. By hand, a hard reload with the cache disabled in the developer tools does the same.
- **Clicks with `browser_mouse_click_xy`** need a screenshot just before, and its coordinates are scaled by the image size, not the page drawn in it: x is `viewportX × imageWidth / innerWidth`, y the same with the heights. The tool reports the viewport position it clicked: check it.
- **Registering the helper script twice** on a tab (for example in two sessions) wraps `element.animate` twice, so `__anims` records each call twice. Check with a test `div` before counting.
- **One frame per second.** When the browser panel isn't on screen in the editor, the tab can paint once per second while `document.visibilityState` still says `visible`, and `Page.bringToFront` doesn't help. Measure it first with a short `requestAnimationFrame` loop. At that rate, `startAtNextFrame` restarts animations up to a second late, so timings and mid-animation samples are meaningless: check the batches and the final state instead, pause the animations (below), or ask the user to show the panel. A click that builds new content can also block the page for most of a second, so a frame loop stopped by a timer may record a single frame.
- **Pausing a Magic move**, which doesn't need frames. After the click, poll every 10 ms until the layers (`[data-os-layer]`) and their animations exist (`document.getAnimations()` whose `effect.target` is inside a layer), `pause()` them all, then set each `currentTime` to a fraction of its duration and read the layers and their copies. A travel ends 1 s after its duration even when paused: to keep it on screen for a screenshot, wrap `window.setTimeout` so that delays from 1 to 10 s get 60 s more, which also slows `__t.wait` with such delays, and restore it with the animations finished afterwards.
- **Pausing effects and moves**, which start too quickly to catch by polling. Wrap `Element.prototype.animate` so that, while a flag is set, each new animation with the duration you want (250 ms by default) is paused, gets its `currentTime`, and gets a `startTime` property of its own whose setter does nothing: `startAtNextFrame` sets `startTime`, which would make a paused animation play again. Afterwards, `delete` that property and call `finish()`. A paused effect doesn't show in `browser_take_screenshot`: use `Page.captureScreenshot`. `Animation.setPlaybackRate` from the DevTools protocol doesn't work for this either: it slows the document timeline, while `startAtNextFrame` takes `performance.now()`, so the animations start seconds late. The speed buttons of the Animations panel of the developer tools do the same.
- **What a frame really shows.** A `requestAnimationFrame` recorder can run before the library's own callbacks of the same frame (`startAtNextFrame`), and see a later state than the one painted. To see what is painted, observe a hidden 1 px element with a `ResizeObserver` and change its width in each frame: the observer runs after every callback, just before the paint. Record from the click, not only while a travel's layer exists.
- **A click that focuses.** A script's `pointerdown` doesn't focus a text box the way a real click does, and OpenSilver handles the two differently. To click like a hand, call `focus()` on the `textarea` right after `mousedown`, then send `pointerup`, `mouseup` and `click` about 100 ms later. XAML.io's AI card showed for a frame only that way.
- **Slow motion in an app without a switch**, like XAML.io: type `osAnimations.slowMotion(4)` in the console, and `osAnimations.slowMotion(1)` to go back. It sets `AnimationSettings.Current.Speed`, so it slows everything, the library's safety timers included, until the page is reloaded. `osAnimations.showWhatAnimates(true)`, `logForDebugging(true)`, `simulateReducedMotion(true)` and `isEnabled(false)` work the same way (see "Globals" in [design.md](design.md)). From a test script, the answers are `console.log` calls, made before the function returns.
- **Zoomed screenshots.** `browser_take_screenshot` with a `ref` gave a page without the travelling layers, and full screenshots are scaled down. `Page.captureScreenshot` with a `clip` (`x`, `y`, `width`, `height` and `scale`, such as 3) is saved to a JSON file: decode its `data` field from base64 into a `.png` (`[Convert]::FromBase64String` in PowerShell), then read that image. The first screenshot after a pause can still show an older frame: take a second one.
