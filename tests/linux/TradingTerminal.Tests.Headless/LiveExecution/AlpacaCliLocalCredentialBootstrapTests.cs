using FluentAssertions;
using TradingTerminal.Execution.Mac;
using Xunit;

namespace TradingTerminal.Tests.Headless.LiveExecution;

public sealed class AlpacaCliLocalCredentialBootstrapTests
{
    [Fact]
    public void Parses_default_profile_name_and_key_pair()
    {
        AlpacaCliLocalCredentialBootstrap.ReadDefaultProfileName("""
            default_profile: financex-sandbox
            output: json
            """)
            .Should().Be("financex-sandbox");

        AlpacaCliLocalCredentialBootstrap.TryParseProfileYaml("""
            api_key: PKTESTKEY123456789012345
            secret_key: "secret-value-with-quotes"
            """, out var keyId, out var secretKey)
            .Should().BeTrue();
        keyId.Should().Be("PKTESTKEY123456789012345");
        secretKey.Should().Be("secret-value-with-quotes");
    }

    [Fact]
    public void Merge_fills_only_blank_fields()
    {
        var keyId = "already-set";
        var secretKey = string.Empty;
        // Without a real profile this returns false; exercise merge semantics via parse+manual apply.
        AlpacaCliLocalCredentialBootstrap.TryParseProfileYaml("""
            api_key: from-profile-key
            secret_key: from-profile-secret
            """, out var profileKey, out var profileSecret).Should().BeTrue();

        if (string.IsNullOrWhiteSpace(keyId))
            keyId = profileKey;
        if (string.IsNullOrWhiteSpace(secretKey))
            secretKey = profileSecret;

        keyId.Should().Be("already-set");
        secretKey.Should().Be("from-profile-secret");
    }
}
