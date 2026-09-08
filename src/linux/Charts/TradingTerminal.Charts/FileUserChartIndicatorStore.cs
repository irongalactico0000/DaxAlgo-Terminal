using System.IO;
using System.Text.Json;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.Charts;

/// <summary>
/// Loads user-authored chart indicators from LocalAppData. Missing files seed a documented sample
/// so users can copy/edit without hunting for schema docs.
/// </summary>
public sealed class FileUserChartIndicatorStore
{
    private readonly string _filePath;

    public FileUserChartIndicatorStore(string? filePath = null)
    {
        _filePath = filePath ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DaxAlgo Terminal",
            "user-indicators.json");
    }

    public string FilePath => _filePath;

    public IReadOnlyList<UserChartIndicatorDefinitionV1> LoadOrSeed()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, SampleJson);
                return UserChartIndicatorCatalogV1.Parse(SampleJson);
            }

            return UserChartIndicatorCatalogV1.Parse(File.ReadAllText(_filePath));
        }
        catch
        {
            return Array.Empty<UserChartIndicatorDefinitionV1>();
        }
    }

    public const string SampleJson =
        """
        {
          "indicators": [
            {
              "id": "sma-200",
              "displayName": "SMA 200",
              "kind": "sma",
              "period": 200,
              "alias": "sma200"
            },
            {
              "id": "ema-9",
              "displayName": "EMA 9",
              "kind": "ema",
              "period": 9,
              "alias": "ema9"
            },
            {
              "id": "rsi-7",
              "displayName": "RSI 7",
              "kind": "rsi",
              "period": 7,
              "alias": "rsi7"
            }
          ]
        }
        """;
}
