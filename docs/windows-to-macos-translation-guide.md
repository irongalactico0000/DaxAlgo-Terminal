# Windows/Visual Studio to macOS translation guide

## What is actually being translated

Visual Studio is the development environment, not the application language. DaxAlgo remains C# and
.NET 9 on macOS. The migration changes platform frameworks and operating-system services while
preserving product behavior and portable contracts.

Every Windows change must be assigned to one of four lanes before it enters the Mac repository:

1. **Portable unchanged** — domain models, algorithms, DTOs, validation, serialization, HTTP clients,
   and interfaces that target plain `net9.0` and do not reference Windows APIs.
2. **Framework adaptation** — the behavior is portable, but WPF types must be translated to Avalonia.
3. **Native substitution** — a Windows service such as DPAPI, Credential Manager, or Windows Service
   control requires a macOS implementation with the same security and lifecycle contract.
4. **Unsupported/gated** — Windows-only functionality stays visibly unavailable until a real macOS
   implementation exists. It must never silently degrade into an unsafe fallback.

## Concrete code mapping

| Windows public implementation | macOS implementation | Rule |
|---|---|---|
| `net9.0-windows`, `UseWPF`, `WinExe` | `net9.0`, Avalonia, `osx-arm64;osx-x64` | Portable libraries remain platform-neutral; only the app head owns Mac RIDs. |
| `System.Windows.FrameworkElement` | `Avalonia.Controls.Control` | Reimplement the visual boundary; do not add WPF compatibility shims. |
| `OnRender(DrawingContext)` | `Render(DrawingContext)` | Preserve immediate-mode behavior and operation bounds, then verify through Avalonia headless rendering. |
| `DependencyProperty` | `StyledProperty` or `DirectProperty` | Use `DirectProperty` for CLR-owned state; use `StyledProperty` when themes/styles must set it. |
| `ActualWidth` / `ActualHeight` | `Bounds.Width` / `Bounds.Height` | Test layout at explicit window sizes. |
| `VisualTreeHelper.GetDpi` | `TopLevel.RenderScaling` | Keep coordinates device-independent and expose scale through the SDK viewport. |
| WPF mouse events | Avalonia pointer events | Keep the latest pointer state; never expose UI-framework event types through SDK contracts. |
| WPF brushes, pens, paths, clips | Avalonia media equivalents | Preserve transforms, Y inversion, clipping, invalid-number rejection, and frame budgets. Remove WPF-only `Freeze()` calls. |
| `.xaml`, WPF resources and `Page` items | `.axaml`, Avalonia resources/styles | Translate behavior and binding semantics; do not mechanically rename markup. |
| WPF dispatcher | `Avalonia.Threading.Dispatcher.UIThread` behind the dispatcher seam | Core, SDK, and pipeline projects must not reference either UI framework. |
| WPF dialogs/windows | Avalonia windows and async owner-aware dialogs | Preserve product gates without blocking the Mac UI thread. |
| MahApps Metro | Avalonia Fluent theme plus DaxAlgo tokens | Preserve product hierarchy and states, not library-specific chrome. |
| AvalonEdit / ScottPlot.WPF | Avalonia-native package or DaxAlgo native control | WPF libraries cannot cross the boundary even when their APIs look portable. |

## Operating-system substitutions

| Windows service | macOS substitute | Acceptance condition |
|---|---|---|
| DPAPI / Windows Credential Manager | macOS Keychain through Security.framework | Secrets are never plaintext; missing Keychain access fails closed. |
| Authenticode trust | Apple code signing/notarization plus DaxAlgo hash/signature verification | Packages remain content-verified and app bundles pass signing checks. |
| Protected named pipes / Windows service | Owner-protected local IPC hosted by the app or `launchd` | Peer authentication and execution-secret protection pass before live orders are enabled. |
| Registry / Windows local-data assumptions | LocalApplicationData, Application Support, or preferences | Paths are centralized and migration-safe; no hard-coded Windows separators. |
| Job Objects | macOS process-group/lifecycle supervision | Sidecars terminate with the owning session. |
| Windows shell launch | Avalonia launcher/storage-provider seam or macOS workspace behavior | URLs/files open through an injectable boundary. |
| NinjaTrader desktop integration | Explicitly unavailable on macOS | The broker is disabled with a reason; no pretend connector. |

## Required sequence for every slice

1. Record Windows behavior, invariants, failure states, and public contracts.
2. Port and test plain C# contract/domain code first.
3. Introduce or reuse an OS/UI seam; keep platform types out of Core and SDK.
4. Implement the Avalonia/macOS adapter against that seam.
5. Run the same contract tests plus native headless/UI tests.
6. Compose it in the Mac dependency-injection root only after safety gates pass.
7. Enable navigation last. Live execution remains Paper-only until authorization, risk, IPC, and
   persistence tests are complete.

## Proven example: render surface

The SDK `IRenderSurface`, lifecycle, drawing routines, and visualizer contracts moved as portable C#.
The Windows `DrawingContextSurface` and `RenderSurfaceView` were reimplemented with Avalonia controls,
pointer input, render scaling, brushes, pens, paths, and clips. The Mac adapter preserves two-pass
panel layout, data transforms, Y inversion, semantic theme colors, invalid-number rejection,
exception isolation, and the 20,000-operation frame limit without exposing Avalonia to strategies.

This is the default pattern for the remaining catalog, Execution Console, login-mode gate, and
Extensions UI work.
