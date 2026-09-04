# DaxAlgo.FootprintTransformer — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/AI/DaxAlgo.FootprintTransformer/FootprintModelContract.cs
```cs
    5: public const int MetadataSchemaVersion = 2;
    6: public const int LookbackBars = 64;
    7: public const int HorizonBars = 8;
    8: public const int MaximumRows = 1_024;
    9: public const int RowFeatureCount = 11;
   10: public const int BarFeatureCount = 23;
   11: public const int TargetCount = 7;
   12: public const int QuantileCount = 3;
   13: public const double RowSize = 2.5;
   14: public const double PriceScaleTicks = 64.0;
   15: public const double GapScaleTicks = 32.0;
   16: public const double LogVolumeScale = 4.0;
   17: public const double CumulativeDeltaScale = 4.0;
   18: public const double ImbalanceRatio = 3.0;
   19: public const double QuantityScale = 1_000.0;
   20: public const string ModelKind = "fdt-n-footprint-distribution-transformer";
   21: public const string RuntimeStatus = "shadow-only-research";
   22: public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
   24: public static readonly string[] RowFeatureNames =
   39: public static readonly string[] BarFeatureNames =
   66: public static readonly string[] TargetNames =
```

## src/linux/AI/DaxAlgo.FootprintTransformer/FootprintTransformerEncoding.cs
```cs
   13: public static EncodedFootprintWindow Encode(FootprintForecastRequest request)
  266: public double TotalVolume => BuyVolume + SellVolume;
  267: public double Delta => BuyVolume - SellVolume;
```

## src/linux/AI/DaxAlgo.FootprintTransformer/FootprintTransformerForecastProvider.cs
```cs
    7: public sealed class FootprintTransformerForecastProvider : IFootprintForecastProvider, IDisposable
   13: public FootprintTransformerForecastProvider()
   30: public Task<FootprintForecastResult> ForecastAsync(
   51: public void Dispose()
```

## src/linux/AI/DaxAlgo.FootprintTransformer/OnnxFootprintInferenceSession.cs
```cs
   14: public OnnxFootprintInferenceSession(byte[] modelBytes)
   29: public float[] Run(FootprintInferenceInput input, CancellationToken cancellationToken)
   67: public void Dispose() => _session.Dispose();
  140: public static LoadedFootprintModel? TryLoad()
```
