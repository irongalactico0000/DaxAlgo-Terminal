using System.Text.RegularExpressions;
using TradingTerminal.Execution.Alpaca;

namespace TradingTerminal.Execution.Mac;

/// <summary>
/// Optional developer bootstrap: when Alpaca KeyId/SecretKey are empty, fill them from the Alpaca
/// CLI profile under <c>~/.config/alpaca</c>. Shipped operator UX stays the Execution Console Brokers
/// form — this only seeds in-process options so a local machine with a CLI profile can Connect
/// without retyping. Never logs secret material.
/// </summary>
public static class AlpacaCliLocalCredentialBootstrap
{
    private static readonly Regex YamlScalar = new(
        @"^\s*(?<key>api_key|secret_key|api-key|secret-key)\s*:\s*(?<value>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Fills blank <see cref="AlpacaExecutionOptions"/> credentials from the default CLI profile.
    /// </summary>
    public static bool TryApplyMissing(AlpacaExecutionOptions options, out string profileName)
    {
        ArgumentNullException.ThrowIfNull(options);
        var keyId = options.KeyId ?? string.Empty;
        var secretKey = options.SecretKey ?? string.Empty;
        if (!TryMergeMissing(ref keyId, ref secretKey, out profileName))
            return false;
        options.KeyId = keyId;
        options.SecretKey = secretKey;
        return true;
    }

    /// <summary>
    /// Merges CLI credentials into blank fields only. Returns false when both fields are already set
    /// or no usable profile exists.
    /// </summary>
    public static bool TryMergeMissing(ref string keyId, ref string secretKey, out string profileName)
    {
        profileName = string.Empty;
        var needKey = string.IsNullOrWhiteSpace(keyId);
        var needSecret = string.IsNullOrWhiteSpace(secretKey);
        if (!needKey && !needSecret)
            return false;

        if (!TryReadDefaultProfile(out profileName, out var profileKey, out var profileSecret))
            return false;

        var applied = false;
        if (needKey && !string.IsNullOrWhiteSpace(profileKey))
        {
            keyId = profileKey;
            applied = true;
        }
        if (needSecret && !string.IsNullOrWhiteSpace(profileSecret))
        {
            secretKey = profileSecret;
            applied = true;
        }
        return applied;
    }

    /// <summary>Reads the default Alpaca CLI profile credentials from the user config directory.</summary>
    public static bool TryReadDefaultProfile(out string profileName, out string keyId, out string secretKey)
    {
        profileName = string.Empty;
        keyId = string.Empty;
        secretKey = string.Empty;

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "alpaca");
        var configPath = Path.Combine(root, "config.yaml");
        if (!File.Exists(configPath))
            return false;

        profileName = ReadDefaultProfileName(File.ReadAllText(configPath)) ?? "default";
        var profilePath = Path.Combine(root, "profiles", profileName + ".yaml");
        if (!File.Exists(profilePath))
            return false;

        return TryParseProfileYaml(File.ReadAllText(profilePath), out keyId, out secretKey);
    }

    internal static string? ReadDefaultProfileName(string configYaml)
    {
        foreach (var raw in configYaml.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            const string prefix = "default_profile:";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var value = line[prefix.Length..].Trim().Trim('"', '\'');
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        return null;
    }

    internal static bool TryParseProfileYaml(string yaml, out string keyId, out string secretKey)
    {
        keyId = string.Empty;
        secretKey = string.Empty;
        foreach (var raw in yaml.Split('\n'))
        {
            var match = YamlScalar.Match(raw);
            if (!match.Success)
                continue;
            var value = match.Groups["value"].Value.Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(value))
                continue;
            var key = match.Groups["key"].Value;
            if (key.Contains("secret", StringComparison.OrdinalIgnoreCase))
                secretKey = value;
            else
                keyId = value;
        }
        return !string.IsNullOrWhiteSpace(keyId) && !string.IsNullOrWhiteSpace(secretKey);
    }
}
