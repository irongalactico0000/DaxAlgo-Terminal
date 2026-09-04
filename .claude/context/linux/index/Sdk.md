# macOS index / Sdk

Generated from source fingerprint `e91d50e75733`. macOS/Avalonia source only.

| File | LOC | Tree | Project | Role | Public surface | Purpose |
|---|---:|---|---|---|---|---|
| `src/linux/Sdk/DaxAlgo.Package/DaxPackage.cs` | 398 | linux | DaxAlgo.Package | product | Y | Extension for a strategy submission. |
| `src/linux/Sdk/DaxAlgo.Package/DaxPackageManifestCodec.cs` | 147 | linux | DaxAlgo.Package | product | Y | Serialises the manifest as canonical JSON — properties in a fixed order, |
| `src/linux/Sdk/DaxAlgo.Package/DaxPackageModels.cs` | 149 | linux | DaxAlgo.Package | product | Y | Publishes trading signals. Ships as |
| `src/linux/Sdk/DaxAlgo.Package/ExtensionsPackageInspection.cs` | 104 | linux | DaxAlgo.Package | product | Y | Extensions (Strategy Manager) open-package gate shared by every edition. |
| `src/linux/Sdk/DaxAlgo.Package/StrategyBuilderPackageBridge.cs` | 135 | linux | DaxAlgo.Package | product | Y | Reads the confirmed-input digest from a provenance payload, or null if absent/malformed. |
| `src/linux/Sdk/DaxAlgo.Sdk/AuthoredPlugin.cs` | 255 | linux | DaxAlgo.Sdk | product | Y | The author wrote a complete hand-written window: metadata, a view-model, a view. |
| `src/linux/Sdk/DaxAlgo.Sdk/Drawing/Candles.cs` | 94 | linux | DaxAlgo.Sdk | product | Y | How a candle series is drawn. |
| `src/linux/Sdk/DaxAlgo.Sdk/Drawing/Footprint.cs` | 262 | linux | DaxAlgo.Sdk | product | Y | How a volume footprint is drawn. |
| `src/linux/Sdk/DaxAlgo.Sdk/Drawing/Ladder.cs` | 156 | linux | DaxAlgo.Sdk | product | Y | How a depth ladder is drawn. |
| `src/linux/Sdk/DaxAlgo.Sdk/Drawing/Plot.cs` | 173 | linux | DaxAlgo.Sdk | product | Y | An inclusive numeric range, and the arithmetic every drawing routine repeats. |
| `src/linux/Sdk/DaxAlgo.Sdk/IAuthoredDrawingManifest.cs` | 12 | linux | DaxAlgo.Sdk | product | Y | Stable layer type ids in the same order as the reviewed drawing |
| `src/linux/Sdk/DaxAlgo.Sdk/IPluginRegistrar.cs` | 33 | linux | DaxAlgo.Sdk | product | Y | The host service collection the plugin registers its strategy / view / |
| `src/linux/Sdk/DaxAlgo.Sdk/IRenderSurface.cs` | 182 | linux | DaxAlgo.Sdk | product | Y | What a panel is for. The host picks chrome, gutters and default |
| `src/linux/Sdk/DaxAlgo.Sdk/IStrategyEngineFactory.cs` | 26 | linux | DaxAlgo.Sdk | product | Y | Declarative tunables used by live editors, backtests, and optimizers. |
| `src/linux/Sdk/DaxAlgo.Sdk/IStrategyKernel.cs` | 56 | linux | DaxAlgo.Sdk | product | Y | The declarative launch-time parameter schema. |
| `src/linux/Sdk/DaxAlgo.Sdk/IStrategyLifecycle.cs` | 51 | linux | DaxAlgo.Sdk | product | Y | Host-facing lifecycle for one sandboxed strategy session. |
| `src/linux/Sdk/DaxAlgo.Sdk/IStrategyPlugin.cs` | 28 | linux | DaxAlgo.Sdk | product | Y | Human-readable plugin name (logging + the future marketplace UI). |
| `src/linux/Sdk/DaxAlgo.Sdk/IVisualizer.cs` | 56 | linux | DaxAlgo.Sdk | product | Y | The declarative launch-time parameter schema. |
| `src/linux/Sdk/DaxAlgo.Sdk/NullRenderSurface.cs` | 69 | linux | DaxAlgo.Sdk | product | Y | The shared instance. It holds no state, so one is enough. |
| `src/linux/Sdk/DaxAlgo.Sdk/SandboxContexts.cs` | 239 | linux | DaxAlgo.Sdk | product | Y | The complete canonical instrument set visible to this strategy or visualizer. |
| `src/linux/Sdk/DaxAlgo.Sdk/SdkInfo.cs` | 18 | linux | DaxAlgo.Sdk | product | Y | Semantic version of this SDK build. Bump on any breaking change to |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/CanonicalJson.cs` | 59 | linux | DaxAlgo.Strategy.Bundle | product | Y | Minimal JSON string/number encoding with a frozen escape algorithm. It deliberately avoids |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/DaxStrategyBundle.cs` | 354 | linux | DaxAlgo.Strategy.Bundle | product | Y | Creates and verifies passive .daxstrategy archives without loading any payload assembly. |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleArchive.cs` | 403 | linux | DaxAlgo.Strategy.Bundle | product | Y | DSSE PAE domain-separates the payload type and both byte lengths from the |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleEnginePolicy.cs` | 256 | linux | DaxAlgo.Strategy.Bundle | product | Y | Validates the manifest-named factory from metadata without loading strategy code. |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleExternalAssemblyPolicy.cs` | 249 | linux | DaxAlgo.Strategy.Bundle | product | Y | Frozen v1 list of assemblies supplied by the .NET 9 Windows shared |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleLimitOptions.cs` | 60 | linux | DaxAlgo.Strategy.Bundle | product | Y |  |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleManifestCodec.cs` | 625 | linux | DaxAlgo.Strategy.Bundle | product | Y |  |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleModels.cs` | 230 | linux | DaxAlgo.Strategy.Bundle | product | Y | A repeatable source for one payload. The bundle packer owns and disposes |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundlePath.cs` | 93 | linux | DaxAlgo.Strategy.Bundle | product | Y |  |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundlePayloadPolicy.cs` | 485 | linux | DaxAlgo.Strategy.Bundle | product | Y | Validates bundle payload shape as metadata only. This is a format and |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleRuntimePolicy.cs` | 80 | linux | DaxAlgo.Strategy.Bundle | product | Y | The frozen v1 framework/host assembly allowlist used by graph and runtime resolution. |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleSemanticVersion.cs` | 85 | linux | DaxAlgo.Strategy.Bundle | product | Y |  |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleStore.cs` | 708 | linux | DaxAlgo.Strategy.Bundle | product | Y | Atomically makes one already-installed evidence selection active. |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleStoreJson.cs` | 263 | linux | DaxAlgo.Strategy.Bundle | product | Y |  |
| `src/linux/Sdk/DaxAlgo.Strategy.Bundle/StrategyBundleStoreModels.cs` | 105 | linux | DaxAlgo.Strategy.Bundle | product | Y | Controls whether an installed strategy may be unsigned. |
