# macOS index / Charts

Generated from source fingerprint `e91d50e75733`. macOS/Avalonia source only.

| File | LOC | Tree | Project | Role | Public surface | Purpose |
|---|---:|---|---|---|---|---|
| `src/linux/Charts/TradingTerminal.BubbleChart/BubbleChartServiceCollectionExtensions.cs` | 16 | linux | TradingTerminal.BubbleChart | product | Y | DI registration for the experimental Volume Bubble Line chart. Transient so each |
| `src/linux/Charts/TradingTerminal.BubbleChart/BubbleChartViewModel.cs` | 530 | linux | TradingTerminal.BubbleChart | product | Y | One executed-trade bubble: time, price, volume, side (+1 buy / −1 sell |
| `src/linux/Charts/TradingTerminal.BubbleChart/BubbleChartWindow.axaml.cs` | 80 | linux | TradingTerminal.BubbleChart | product | Y | Native Avalonia host for the copied Professional bubble-heatmap view model. The view |
| `src/linux/Charts/TradingTerminal.BubbleChart/BubbleChartWindow.axaml` | 232 | linux | TradingTerminal.BubbleChart | product | N | UI |
| `src/linux/Charts/TradingTerminal.BubbleChart/HeatmapBubbleSurface.cs` | 400 | linux | TradingTerminal.BubbleChart | product | Y | Native Avalonia renderer for the Professional bubble heatmap. It preserves the Windows |
| `src/linux/Charts/TradingTerminal.Charts/AvaloniaInstrumentTagsConverter.cs` | 75 | linux | TradingTerminal.Charts | product | Y | Native-Avalonia projection of the Windows instrument picker's broker, asset and data pills. |
| `src/linux/Charts/TradingTerminal.Charts/ChartsPanel.axaml.cs` | 189 | linux | TradingTerminal.Charts | product | Y | Avalonia host for the reusable chart VM and native renderer. It preserves |
| `src/linux/Charts/TradingTerminal.Charts/ChartsPanel.axaml` | 157 | linux | TradingTerminal.Charts | product | N | UI |
| `src/linux/Charts/TradingTerminal.Charts/ChartsPanelFeatures.cs` | 53 | linux | TradingTerminal.Charts | product | Y | Symbol/timeframe selectors, presets, pause/export, the ? help and the ⚙ rail toggle. |
| `src/linux/Charts/TradingTerminal.Charts/ChartsServiceCollectionExtensions.cs` | 15 | linux | TradingTerminal.Charts | product | Y | DI registration for the TradingView-style Charts tool. Transient so each open gets |
| `src/linux/Charts/TradingTerminal.Charts/ChartsViewModel.cs` | 560 | linux | TradingTerminal.Charts | product | Y | Non-null when this view-model lives inside a strategy window rather than the |
| `src/linux/Charts/TradingTerminal.Charts/ChartsWindow.axaml.cs` | 12 | linux | TradingTerminal.Charts | product | Y | Standalone native-Avalonia host around . The shell owns and disposes the |
| `src/linux/Charts/TradingTerminal.Charts/ChartsWindow.axaml` | 12 | linux | TradingTerminal.Charts | product | N | UI |
| `src/linux/Charts/TradingTerminal.Charts/NativeChartSurface.cs` | 605 | linux | TradingTerminal.Charts | product | Y | Splices a forming candle exactly as Lightweight Charts' |
| `src/linux/Charts/TradingTerminal.Heatmap/AvaloniaUi/BookmapHeatmapAvaloniaWindow.axaml.cs` | 13 | linux | TradingTerminal.Heatmap | product | Y | Avalonia (cross-platform) view for the Bookmap + VolBook tool — net9.0-leg counterpart |
| `src/linux/Charts/TradingTerminal.Heatmap/AvaloniaUi/BookmapHeatmapAvaloniaWindow.axaml` | 46 | linux | TradingTerminal.Heatmap | product | N | UI |
| `src/linux/Charts/TradingTerminal.Heatmap/BookmapHeatmapViewModel.cs` | 395 | linux | TradingTerminal.Heatmap | product | Y | How many time columns are visible at once (the scrolling window width). |
| `src/linux/Charts/TradingTerminal.Heatmap/BookmapHeatmapWindow.xaml.cs` | 41 | linux | TradingTerminal.Heatmap | product | Y | Hosts the combined Bookmap + VolBook view. Pure presentation: it binds the |
| `src/linux/Charts/TradingTerminal.Heatmap/BookmapHeatmapWindow.xaml` | 161 | linux | TradingTerminal.Heatmap | product | N | UI |
| `src/linux/Charts/TradingTerminal.Heatmap/BookmapSurface.cs` | 724 | linux | TradingTerminal.Heatmap | product | Y | Called when the VM's buffers change — rebuild the cached data layer. |
| `src/linux/Charts/TradingTerminal.Heatmap/HeatmapServiceCollectionExtensions.cs` | 17 | linux | TradingTerminal.Heatmap | product | Y | DI registration for the Heatmap surface — the single combined |
| `src/linux/Charts/TradingTerminal.Heatmap/SingleInstrumentHeatmapViewModelBase.cs` | 250 | linux | TradingTerminal.Heatmap | product | Y | Redraw cadence — decoupled from the data feed so a fast book/tape |
| `src/linux/Charts/TradingTerminal.OrderBook/AvaloniaUi/OrderBookAvaloniaWindow.axaml.cs` | 11 | linux | TradingTerminal.OrderBook | product | Y | Avalonia (cross-platform) view for the Order Book — net9.0-leg counterpart to the |
| `src/linux/Charts/TradingTerminal.OrderBook/AvaloniaUi/OrderBookAvaloniaWindow.axaml` | 69 | linux | TradingTerminal.OrderBook | product | N | UI |
| `src/linux/Charts/TradingTerminal.OrderBook/OrderBookModels.cs` | 41 | linux | TradingTerminal.OrderBook | product | Y | One display row of the ladder. |
| `src/linux/Charts/TradingTerminal.OrderBook/OrderBookServiceCollectionExtensions.cs` | 17 | linux | TradingTerminal.OrderBook | product | Y | DI registration for the standalone Order Book tool. Transient so each open |
| `src/linux/Charts/TradingTerminal.OrderBook/OrderBookViewModel.cs` | 636 | linux | TradingTerminal.OrderBook | product | Y | Cap on how many instruments the picker shows at once (the broker |
| `src/linux/Charts/TradingTerminal.OrderBook/OrderBookWindow.xaml.cs` | 313 | linux | TradingTerminal.OrderBook | product | Y | Hosts the standalone Order Book window. Pure view: the owns the |
| `src/linux/Charts/TradingTerminal.OrderBook/OrderBookWindow.xaml` | 300 | linux | TradingTerminal.OrderBook | product | N | UI |
| `src/linux/Charts/TradingTerminal.SurfaceLab/AxisConfigViewModel.cs` | 137 | linux | TradingTerminal.SurfaceLab | product | Y | True for Z/Color — shows the custom-formula bar under the dropdown. |
| `src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceLabConverters.cs` | 13 | linux | TradingTerminal.SurfaceLab | product | Y |  |
| `src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceLabServiceCollectionExtensions.cs` | 15 | linux | TradingTerminal.SurfaceLab | product | Y | DI registration for the native macOS 3D Surface Lab. Transient registration gives |
| `src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceLabViewModel.cs` | 773 | linux | TradingTerminal.SurfaceLab | product | Y | Display pause: the live rebuild tick is gated; pumps keep filling the |
| `src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceLabWindow.axaml.cs` | 126 | linux | TradingTerminal.SurfaceLab | product | Y | Native Avalonia host for the copied Surface Lab view model. The view |
| `src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceLabWindow.axaml` | 371 | linux | TradingTerminal.SurfaceLab | product | N | UI |
| `src/linux/Charts/TradingTerminal.SurfaceLab/SurfacePlot3D.cs` | 470 | linux | TradingTerminal.SurfaceLab | product | Y | Projection is cached across slice moves. Only a new surface, camera gesture, |
| `src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceSlicePlot.cs` | 139 | linux | TradingTerminal.SurfaceLab | product | Y | Small native Avalonia cross-section plot used by both Surface Lab slice viewers. |
| `src/linux/Charts/TradingTerminal.VolumeFootprint/AvaloniaUi/VolumeFootprintAvaloniaWindow.axaml.cs` | 12 | linux | TradingTerminal.VolumeFootprint | product | Y | Avalonia (cross-platform) view for the Volume Footprint — net9.0-leg counterpart to the |
| `src/linux/Charts/TradingTerminal.VolumeFootprint/AvaloniaUi/VolumeFootprintAvaloniaWindow.axaml` | 45 | linux | TradingTerminal.VolumeFootprint | product | N | UI |
| `src/linux/Charts/TradingTerminal.VolumeFootprint/VolumeFootprintModels.cs` | 131 | linux | TradingTerminal.VolumeFootprint | product | Y | Which POC series an overlay fit curve belongs to (drives the brush |
| `src/linux/Charts/TradingTerminal.VolumeFootprint/VolumeFootprintServiceCollectionExtensions.cs` | 17 | linux | TradingTerminal.VolumeFootprint | product | Y | DI registration for the Volume Footprint tool. Transient so each open gets |
| `src/linux/Charts/TradingTerminal.VolumeFootprint/VolumeFootprintViewModel.cs` | 739 | linux | TradingTerminal.VolumeFootprint | product | Y | Which brokers actually wire a native trade tape today (see the cube |
| `src/linux/Charts/TradingTerminal.VolumeFootprint/VolumeFootprintWindow.xaml.cs` | 677 | linux | TradingTerminal.VolumeFootprint | product | Y | Draws the connector lines for the total / buy / sell points-of-control |
| `src/linux/Charts/TradingTerminal.VolumeFootprint/VolumeFootprintWindow.xaml` | 326 | linux | TradingTerminal.VolumeFootprint | product | N | UI |
