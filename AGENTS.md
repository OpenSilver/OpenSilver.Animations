# OpenSilver.Animations: notes for AI agents

OpenSilver.Animations is a beginner-friendly animation library for [OpenSilver](https://opensilver.net). You set what an element does "when it moves or changes size", and later "when it appears", "when it disappears", and so on. The library animates it, and everything that moves because of the same change, without storyboards:

```xml
<Button Content="Save" anim:Animate.MoveAndResize="Smooth" />
```

The repository also holds the gallery, "OpenSilver.Animations library: a guided tour", which teaches the library in 14 chapters and is the manual test suite.

Read these before working:

| File | Read it for |
| --- | --- |
| [docs/roadmap.md](docs/roadmap.md) | What is done, verified and next, and the decisions already made. Start here. |
| [docs/architecture.md](docs/architecture.md) | How the built part works: the OpenSilver hooks, the engine rules, the JavaScript, the public API. |
| [docs/development.md](docs/development.md) | Building OpenSilver locally, building and packing, running the gallery, testing in the browser with the helper scripts. |
| [docs/design.md](docs/design.md) | The full specification of every phase, including the XAML.io designer tab, which is built elsewhere. |

## Working agreement with the product owner

- **Never commit, in any repository.** When the work is done, say so and list what changed; the product owner commits. This applies to this repository and to the OpenSilver repository (branch `feat/layout-observer`).
- **Beginners first.** Names read as "When it ...". Parameters use plain words (`StartAfter`, not Delay; `Bouncy`, not Spring; `Wave`, not Ripple). Messages and gallery text are plain, complete sentences.
- **Keep OpenSilver changes minimal**: hooks only, with no behavior change. Everything else goes in this library.
- **The hooks are internal.** `LayoutObserver` is visible to this library only, through `InternalsVisibleTo("OpenSilver.Animations")`, as the OpenSilver owner asked while the API is not stable. Don't make it public, and keep the assembly name `OpenSilver.Animations`.
- **No compatibility layer** with the Showcase's old animation project, which will be removed.
- **No XAML.io designer tab for now**, but keep `design.md`'s designer section in mind when shaping the API (`AnimationCatalog` and `AnimationDesign` come in phase 4).
- **Ask** when a decision is the product owner's to make.
- **Keep the docs current.** When you change behavior, update `docs/architecture.md` and `docs/roadmap.md`, and `docs/design.md` when the specification itself changes. `docs/design.md` replaces the original Cursor plan (`smooth_layout_transitions_88836246.plan.md` in the user's `.cursor/plans` folder).

## Repository layout

```text
OpenSilver.Animations.slnx       solution: the library and the three gallery projects
Directory.Build.props            OpenSilverVersion (3.4.0-preview-animations-local by default), LangVersion 12
NuGet.Config                     MyGet OpenSilver feed + build/local-packages
.github/workflows/               build-package.yml: manual CI that packs and pushes to MyGet
build/                           Build_NuGet.bat, Build_Local_OpenSilver.bat, icon.png, local-packages/
src/OpenSilver.Animations/       the library (netstandard2.0)
  Animate.cs, Movement.cs, AnimationTime.cs, Easings.cs, AnimationSettings.cs, AssemblyInfo.cs
  Engine/                        LayoutAnimator (the logic), Timing, BatchBuilder, BrowserBridge, ElementNames
  Scripts/animations.js          embedded; plays the batches with Web Animations
samples/
  OpenSilver.Animations.Gallery/            the tour (netstandard2.0): Shell/, Controls/, Chapters/, Images/
  OpenSilver.Animations.Gallery.Browser/    WebAssembly host (net10.0), http://localhost:55620
  OpenSilver.Animations.Gallery.Simulator/  desktop Simulator (net10.0-windows)
docs/                            the documentation listed above
```

This repository is a separate git repository, nested in the OpenSilver folder (`C:\VSProjects\UserwareSource\OpenSilver\OpenSilver.Animations`). `OpenSilver.Themes.Modern`, next to it, was the model for its layout.

## Quick commands

Run them from the repository root:

```powershell
# After any change in the OpenSilver repository (needs msbuild in the PATH)
build\Build_Local_OpenSilver.bat C:\VSProjects\UserwareSource\OpenSilver

# Build (expect 0 errors; NU1902/NU1903 advisories come from OpenSilver's packages)
dotnet build OpenSilver.Animations.slnx -c Release

# Run the gallery in a browser (restart it after every build)
dotnet run --project samples\OpenSilver.Animations.Gallery.Browser\OpenSilver.Animations.Gallery.Browser.csproj -c Release --no-build --urls http://localhost:55620

# Pack
build\Build_NuGet.bat 0.1.0-preview 3.4.0-preview-animations-local
```

## Code conventions

- **C#.** `netstandard2.0`, C# 12, nullable reference types off.
  - Every public member has an XML doc comment written for beginners; the package ships the XML file.
  - Engine types are `internal` and live under `OpenSilver.Animations.Engine`.
- **Comments.** Only write a comment for a rule or constraint the code can't show, in plain sentences. Match the density and tone of the surrounding code.
- **The hot path.** The measure and arrange hooks of `LayoutObserver` run for every element of every layout pass, inside `Dispatcher.DisableProcessing()`.
  - Only record data there, with no allocations and no tree walks.
  - Decide in `OnLayoutPassCompleted`, which runs once after the pass, just before the render queue is flushed. Post anything that needs the dispatcher with `BeginInvoke`.
- **Visual only.** Never change a dependency property to animate. The core writes the final layout, and the library animates on top of it with Web Animations, in the same JavaScript flush.
- **Messages.** Snap and animate messages go through `LogSnap` and `_messages`. They name elements with `ElementNames.Of`, and explain only what would otherwise have animated. Silent reasons are listed in `IsSilent`.
- **Testing.** Verify behavior in the gallery, in the browser (see development.md), and add the scenario to a chapter's "What should happen" lines when it's user-visible.
