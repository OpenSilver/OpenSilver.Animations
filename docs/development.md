# Development: build, run and test

## Where things are

| Path | What it is |
| --- | --- |
| `C:\VSProjects\UserwareSource\OpenSilver` | The OpenSilver repository, on branch `feat/layout-observer`. It holds the core changes (see [architecture.md](architecture.md)), in commit `547adac2`. |
| `C:\VSProjects\UserwareSource\OpenSilver\OpenSilver.Animations` | This repository (remote `github.com/OpenSilver/OpenSilver.Animations`). It sits inside the OpenSilver folder but is a separate git repository. |
| `C:\VSProjects\UserwareSource\OpenSilver\OpenSilver.Themes.Modern` | Another library repository, the model for this repository's layout and build scripts. |
| `C:\VSProjects\UserwareSource\OpenSilver\OpenSilver.Samples.Showcase` | The Showcase, to be ported to this library in phase 3. Its own animation project will be removed. |

The three nested folders show up as untracked in the OpenSilver repository. They must never be committed into it.

## Prerequisites

- **.NET SDK 10** (10.0.401 was used).
- **Visual Studio's `msbuild` in the PATH** for the `.bat` scripts, for example from a Developer PowerShell. The OpenSilver build scripts need it.
- The OpenSilver repository on branch `feat/layout-observer`.

## The OpenSilver packages with the hooks

The library needs `OpenSilver.LayoutObserver`, which is not in a published OpenSilver package yet. So OpenSilver is built locally:

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

**CI.** `.github/workflows/build-package.yml` is started by hand (`workflow_dispatch`).

- It takes an `opensilver-version` input, which must be a published OpenSilver that includes `LayoutObserver`.
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
3. Register the helper scripts below once per tab, with `browser_cdp` `Page.addScriptToEvaluateOnNewDocument` (`{ "source": "..." }`). Then reload.
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

**Pointer capture for synthetic events.** OpenSilver captures the pointer with `setPointerCapture`, which throws for untrusted events. This patch fakes capture, for tests only. With it, a Slider thumb drag worked once. The GridSplitter (through `DragValidator`) never captured with synthetic events, so it needs a manual check.

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

### Testing pitfalls

- **The log column in the DOM.** New entries are inserted at the top on screen, but their DOM nodes are appended (see "DOM order" in [architecture.md](architecture.md)). Read it with `panel()`, which sorts by position, not with `innerText`.
- **Finding elements.** `find` matches the whole text of an element, and the first match can be an outer element. `findDeep`, which `tap` uses, takes the innermost one. If text is ambiguous, find elements by position or by DOM structure.
- **Type-name classes** such as `[class*="Controls.Slider"]` exist only in Debug builds of the gallery.
- **Interrupting an animation** in a Debug build can look like a jump. Measure it with `sample` before concluding: in chapter 5, the dialog's second animation started from 556.7 px, exactly where the first one was.
- **The emulated window size** sometimes survives a reload. Clear it at the end.
- **Registering the helper script twice** on a tab (for example in two sessions) wraps `element.animate` twice, so `__anims` records each call twice. Check with a test `div` before counting.
