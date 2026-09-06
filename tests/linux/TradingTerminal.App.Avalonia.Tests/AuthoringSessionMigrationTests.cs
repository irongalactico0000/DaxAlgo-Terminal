using System.Text.Json;
using FluentAssertions;
using TradingTerminal.App.Authoring;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class AuthoringSessionMigrationTests
{
    [Fact]
    public void Legacy_snapshot_without_lane_mode_defaults_to_four_ai_lanes()
    {
        var snapshot = Deserialize(new
        {
            StrategyId = "legacy",
            DisplayName = "Legacy strategy",
            Chat = Array.Empty<object>(),
            Thread = Array.Empty<object>(),
            Files = Array.Empty<object>(),
        });

        snapshot.GenerateCandidateFirst.Should().BeNull();
        snapshot.FourLaneGenerationEnabled.Should().BeTrue();
        snapshot.StrategyWorkspaceJson.Should().BeNull();
    }

    [Fact]
    public void Expert_mode_saved_by_the_legacy_ambiguous_toggle_migrates_to_four_lanes()
    {
        var snapshot = Deserialize(new
        {
            StrategyId = "legacy-expert",
            DisplayName = "Legacy expert strategy",
            Chat = Array.Empty<object>(),
            Thread = Array.Empty<object>(),
            Files = Array.Empty<object>(),
            GenerateCandidateFirst = false,
        });

        snapshot.AuthoringUxVersion.Should().Be(0);
        snapshot.FourLaneGenerationEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Explicit_lane_mode_from_the_redesigned_control_is_preserved(bool fourLaneEnabled)
    {
        var snapshot = Deserialize(new
        {
            StrategyId = "current",
            DisplayName = "Current strategy",
            Chat = Array.Empty<object>(),
            Thread = Array.Empty<object>(),
            Files = Array.Empty<object>(),
            GenerateCandidateFirst = fourLaneEnabled,
            AuthoringUxVersion = AuthoringSessionSnapshot.CurrentAuthoringUxVersion,
        });

        snapshot.FourLaneGenerationEnabled.Should().Be(fourLaneEnabled);
    }

    private static AuthoringSessionSnapshot Deserialize(object payload) =>
        JsonSerializer.Deserialize<AuthoringSessionSnapshot>(JsonSerializer.Serialize(payload))
        ?? throw new InvalidOperationException("The authoring-session fixture did not deserialize.");
}
