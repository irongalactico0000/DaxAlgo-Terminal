# TradingTerminal.StrategyComposer — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/UI/TradingTerminal.StrategyComposer/AuthoredStrategyViewComposer.cs
```cs
   14: public sealed class AuthoredStrategyViewComposer(IServiceProvider services) : IAuthoredStrategyViewComposer
   17: public object ComposeView(ITradingStrategy descriptor) => new ComposedStrategyView(descriptor, services);
   21: public static class StrategyComposerServiceCollectionExtensions
   28: public static IServiceCollection AddStrategyViewComposer(this IServiceCollection services)
```

## src/linux/UI/TradingTerminal.StrategyComposer/ComposedStrategyView.axaml.cs
```cs
   28: public partial class ComposedStrategyView : UserControl, IDisposable
   42: public ComposedStrategyView()
   48: public ComposedStrategyView(ITradingStrategy descriptor, IServiceProvider services)
  104: public IReadOnlyList<Control> Panels { get; private set; } = [];
  415: public void Dispose()
```

## src/linux/UI/TradingTerminal.StrategyComposer/EmbeddedOrderBookPanel.axaml.cs
```cs
   11: public partial class EmbeddedOrderBookPanel : UserControl, IEmbeddedPausable, IDisposable
   15: public EmbeddedOrderBookPanel() => InitializeComponent();
   17: public void SetPaused(bool paused)
   34: public void Dispose() => ClearFreeze();
```

## src/linux/UI/TradingTerminal.StrategyComposer/EmbeddedVolumeFootprintPanel.axaml.cs
```cs
   11: public partial class EmbeddedVolumeFootprintPanel : UserControl, IEmbeddedPausable, IDisposable
   15: public EmbeddedVolumeFootprintPanel() => InitializeComponent();
   17: public void SetPaused(bool paused)
   34: public void Dispose() => ClearFreeze();
```

## src/linux/UI/TradingTerminal.StrategyComposer/IEmbeddedPausable.cs
```cs
    4: public interface IEmbeddedPausable
    6:     void SetPaused(bool paused);
```
