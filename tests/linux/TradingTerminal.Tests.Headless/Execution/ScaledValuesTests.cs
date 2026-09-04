using FluentAssertions;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradingTerminal.Core.Execution;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class ScaledValuesTests
{
    [Fact]
    public void Four_value_types_preserve_coefficient_scale_and_validity()
    {
        new ScaledQuantity(1, 3).Should().Be(new ScaledQuantity(1, 3));
        new ScaledPrice(100_125, 3).IsValid.Should().BeTrue();
        new ScaledMoney(-25, 2).IsValid.Should().BeTrue();
        new ScaledRatio(1, 18).IsValid.Should().BeTrue();
        new ScaledQuantity(1, 19).IsValid.Should().BeFalse();
        new ScaledPrice(1, 19).IsValid.Should().BeFalse();
        new ScaledMoney(1, 19).IsValid.Should().BeFalse();
        new ScaledRatio(1, 19).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(12_000, 3, true, 12)]
    [InlineData(-12_000, 3, true, -12)]
    [InlineData(12_001, 3, false, 0)]
    [InlineData(1, 19, false, 0)]
    public void Whole_unit_conversion_never_rounds(
        long coefficient,
        byte scale,
        bool expectedSuccess,
        long expectedUnits)
    {
        var success = new ScaledQuantity(coefficient, scale).TryGetWholeUnits(out var units);

        success.Should().Be(expectedSuccess);
        units.Should().Be(expectedUnits);
    }

    [Fact]
    public void Alignment_normalizes_trailing_zeroes_and_uses_the_larger_scale()
    {
        var success = ScaledValueMath.TryAlign(
            1,
            3,
            100,
            2,
            out var left,
            out var right,
            out var scale);

        success.Should().BeTrue();
        left.Should().Be((Int128)1);
        right.Should().Be((Int128)1_000);
        scale.Should().Be(3);
    }

    [Fact]
    public void Checked_power_and_multiplication_report_overflow_instead_of_wrapping()
    {
        ScaledValueMath.TryMultiplyPower10(Int128.MaxValue, 1, out var powered)
            .Should().BeFalse();
        powered.Should().Be((Int128)0);

        ScaledValueMath.TryMultiply(Int128.MaxValue, 2, out var product)
            .Should().BeFalse();
        product.Should().Be((Int128)0);

        ScaledValueMath.TryMultiply(-9, 7, out product).Should().BeTrue();
        product.Should().Be((Int128)(-63));
    }

    [Fact]
    public void Addition_aligns_then_narrowing_canonicalizes_the_result()
    {
        ScaledValueMath.TryAdd(125, 2, 75, 2, out var coefficient, out var scale)
            .Should().BeTrue();
        ScaledValueMath.TryNarrow(coefficient, scale, out var narrowed, out var narrowedScale)
            .Should().BeTrue();

        narrowed.Should().Be(2);
        narrowedScale.Should().Be(0);
    }

    [Theory]
    [InlineData(5, 2, 2)]
    [InlineData(7, 2, 4)]
    [InlineData(-5, 2, -2)]
    [InlineData(-7, 2, -4)]
    public void Ratio_rounding_is_midpoint_to_even(long numerator, long denominator, long expected)
    {
        ScaledValueMath.TryRoundRatioToLong(numerator, denominator, out var actual)
            .Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Fact]
    public void Positive_comparison_handles_scale_differences_without_unsafe_alignment()
    {
        ScaledValueMath.TryComparePositive(
                9_999_999_999_999_999_999,
                18,
                9,
                0,
                out var comparison)
            .Should().BeTrue();
        comparison.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Double_quantization_rejects_non_finite_and_rounds_midpoints_to_even()
    {
        ScaledValueMath.TryQuantizeDouble(double.NaN, 2, out _).Should().BeFalse();
        ScaledValueMath.TryQuantizeDouble(1.125d, 2, out var even).Should().BeTrue();
        even.Should().Be(112);
        ScaledValueMath.TryQuantizeDouble(1.135d, 2, out var odd).Should().BeTrue();
        odd.Should().Be(114);
    }

    [Fact]
    public void Decimal_boundary_preserves_exact_trailing_scale_normalization()
    {
        ExecutionNumericBoundary.QuantityFromDecimal(0.0010m)
            .Should().Be(new ScaledQuantity(1, 3));
        ExecutionNumericBoundary.PriceFromDecimal(100.125m)
            .Should().Be(new ScaledPrice(100_125, 3));
        ExecutionNumericBoundary.ToDecimal(new ScaledMoney(-25, 2)).Should().Be(-0.25m);
    }

    [Fact]
    public void Windows_storage_ipc_and_canonical_scaled_value_bytes_are_frozen_separately()
    {
        var storage = new JsonSerializerOptions(JsonSerializerDefaults.General);
        var ipc = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        var fixtures = new (object Value, string Storage, string Ipc, string Canonical)[]
        {
            (
                new ScaledQuantity(1, 3),
                "{\"Coefficient\":1,\"Scale\":3,\"IsValid\":true}",
                "{\"coefficient\":1,\"scale\":3,\"isValid\":true}",
                "{\"coefficient\":1,\"isValid\":true,\"scale\":3}"),
            (
                new ScaledPrice(100_125, 3),
                "{\"Coefficient\":100125,\"Scale\":3,\"IsValid\":true}",
                "{\"coefficient\":100125,\"scale\":3,\"isValid\":true}",
                "{\"coefficient\":100125,\"isValid\":true,\"scale\":3}"),
            (
                new ScaledMoney(-25, 2),
                "{\"Coefficient\":-25,\"Scale\":2,\"IsValid\":true}",
                "{\"coefficient\":-25,\"scale\":2,\"isValid\":true}",
                "{\"coefficient\":-25,\"isValid\":true,\"scale\":2}"),
            (
                new ScaledRatio(125, 2),
                "{\"Coefficient\":125,\"Scale\":2,\"IsValid\":true}",
                "{\"coefficient\":125,\"scale\":2,\"isValid\":true}",
                "{\"coefficient\":125,\"isValid\":true,\"scale\":2}"),
        };

        foreach (var fixture in fixtures)
        {
            var type = fixture.Value.GetType();
            JsonSerializer.Serialize(fixture.Value, type, storage).Should().Be(fixture.Storage);
            var ipcJson = JsonSerializer.Serialize(fixture.Value, type, ipc);
            ipcJson.Should().Be(fixture.Ipc);
            ExecutionCanonicalJson.Canonicalize(ipcJson).Should().Be(fixture.Canonical);
            JsonSerializer.Deserialize(fixture.Storage, type, storage).Should().Be(fixture.Value);
            JsonSerializer.Deserialize(fixture.Ipc, type, ipc).Should().Be(fixture.Value);
        }
    }

    [Fact]
    public void Windows_scaled_wire_shape_includes_derived_validity_and_rejects_extra_ipc_fields()
    {
        var ipc = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        JsonSerializer.Serialize(new ScaledQuantity(1, 19), ipc)
            .Should().Be("{\"coefficient\":1,\"scale\":19,\"isValid\":false}");
        var deserialize = () => JsonSerializer.Deserialize<ScaledQuantity>(
            "{\"coefficient\":1,\"scale\":3,\"isValid\":true,\"unexpected\":0}",
            ipc);

        deserialize.Should().Throw<JsonException>();
    }
}
