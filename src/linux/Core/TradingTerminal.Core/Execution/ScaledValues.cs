namespace TradingTerminal.Core.Execution;

/// <summary>An exact signed quantity encoded as an integer coefficient and a base-10 scale.</summary>
public readonly record struct ScaledQuantity(long Coefficient, byte Scale = 0)
{
    public static ScaledQuantity Zero => new(0, 0);
    public static ScaledQuantity FromWhole(long units) => new(units, 0);
    public bool IsValid => Scale <= ScaledValueMath.MaximumScale;
    public bool TryGetWholeUnits(out long units) =>
        ScaledValueMath.TryGetWholeUnits(Coefficient, Scale, out units);
}

/// <summary>An exact signed price encoded as an integer coefficient and a base-10 scale.</summary>
public readonly record struct ScaledPrice(long Coefficient, byte Scale)
{
    public bool IsValid => Scale <= ScaledValueMath.MaximumScale;
}

/// <summary>An exact signed money value encoded as an integer coefficient and a base-10 scale.</summary>
public readonly record struct ScaledMoney(long Coefficient, byte Scale)
{
    public static ScaledMoney Zero => new(0, 0);
    public bool IsValid => Scale <= ScaledValueMath.MaximumScale;
}

/// <summary>An exact dimensionless ratio encoded as an integer coefficient and a base-10 scale.</summary>
public readonly record struct ScaledRatio(long Coefficient, byte Scale)
{
    public bool IsValid => Scale <= ScaledValueMath.MaximumScale;
}

/// <summary>
/// Checked base-10 arithmetic shared by execution, risk, projection, and broker-boundary mapping.
/// The implementation intentionally matches the Windows execution authority.
/// </summary>
internal static class ScaledValueMath
{
    internal const byte MaximumScale = 18;

    internal static bool TryGetWholeUnits(long coefficient, byte scale, out long units)
    {
        units = 0;
        if (scale > MaximumScale)
            return false;

        var divisor = Pow10(scale);
        if ((Int128)coefficient % divisor != 0)
            return false;

        var value = (Int128)coefficient / divisor;
        if (value < long.MinValue || value > long.MaxValue)
            return false;

        units = (long)value;
        return true;
    }

    internal static Int128 Pow10(int scale)
    {
        Int128 value = 1;
        for (var index = 0; index < scale; index++)
            value *= 10;
        return value;
    }

    internal static bool TryMultiplyPower10(Int128 value, int scale, out Int128 result)
    {
        result = value;
        for (var index = 0; index < scale; index++)
        {
            if (result > Int128.MaxValue / 10 || result < Int128.MinValue / 10)
            {
                result = 0;
                return false;
            }

            result *= 10;
        }

        return true;
    }

    internal static bool TryAlign(
        Int128 left,
        int leftScale,
        Int128 right,
        int rightScale,
        out Int128 alignedLeft,
        out Int128 alignedRight,
        out int scale)
    {
        Normalize(ref left, ref leftScale);
        Normalize(ref right, ref rightScale);
        alignedLeft = left;
        alignedRight = right;
        scale = Math.Max(leftScale, rightScale);
        return TryMultiplyPower10(left, scale - leftScale, out alignedLeft) &&
               TryMultiplyPower10(right, scale - rightScale, out alignedRight);
    }

    internal static bool TryMultiply(Int128 left, Int128 right, out Int128 result)
    {
        result = 0;
        if (left == 0 || right == 0)
            return true;

        if (left > 0)
        {
            if (right > 0 && left > Int128.MaxValue / right ||
                right < 0 && right < Int128.MinValue / left)
                return false;
        }
        else
        {
            if (right > 0 && left < Int128.MinValue / right ||
                right < 0 && left < Int128.MaxValue / right)
                return false;
        }

        result = left * right;
        return true;
    }

    internal static void Normalize(ref Int128 coefficient, ref int scale)
    {
        while (scale > 0 && coefficient % 10 == 0)
        {
            coefficient /= 10;
            scale--;
        }
    }

    internal static bool TryComparePositive(
        Int128 left,
        int leftScale,
        Int128 right,
        int rightScale,
        out int comparison)
    {
        comparison = 0;
        if (left <= 0 || right <= 0 || leftScale < 0 || rightScale < 0)
            return false;

        Normalize(ref left, ref leftScale);
        Normalize(ref right, ref rightScale);
        if (leftScale == rightScale)
        {
            comparison = left.CompareTo(right);
            return true;
        }

        if (leftScale > rightScale)
        {
            var divisor = Pow10(leftScale - rightScale);
            var quotient = left / divisor;
            comparison = quotient.CompareTo(right);
            if (comparison == 0 && left % divisor != 0)
                comparison = 1;
            return true;
        }

        var rightDivisor = Pow10(rightScale - leftScale);
        var rightQuotient = right / rightDivisor;
        comparison = left.CompareTo(rightQuotient);
        if (comparison == 0 && right % rightDivisor != 0)
            comparison = -1;
        return true;
    }

    internal static bool TryAdd(
        Int128 left,
        int leftScale,
        Int128 right,
        int rightScale,
        out Int128 coefficient,
        out int scale)
    {
        coefficient = 0;
        if (!TryAlign(left, leftScale, right, rightScale, out var alignedLeft, out var alignedRight, out scale))
            return false;
        if (alignedRight > 0 && alignedLeft > Int128.MaxValue - alignedRight ||
            alignedRight < 0 && alignedLeft < Int128.MinValue - alignedRight)
            return false;
        coefficient = alignedLeft + alignedRight;
        return true;
    }

    internal static bool TryCompare(
        Int128 left,
        int leftScale,
        Int128 right,
        int rightScale,
        out int comparison)
    {
        comparison = 0;
        if (leftScale < 0 || rightScale < 0 ||
            !TryAlign(left, leftScale, right, rightScale, out var alignedLeft, out var alignedRight, out _))
            return false;
        comparison = alignedLeft.CompareTo(alignedRight);
        return true;
    }

    internal static bool TryAddQuantity(
        ScaledQuantity left,
        ScaledQuantity right,
        out ScaledQuantity sum)
    {
        sum = default;
        if (!TryAdd(left.Coefficient, left.Scale, right.Coefficient, right.Scale, out var value, out var scale) ||
            !TryNarrow(value, scale, out var coefficient, out var narrowedScale))
            return false;
        sum = new ScaledQuantity(coefficient, narrowedScale);
        return true;
    }

    internal static bool TrySubtractQuantity(
        ScaledQuantity left,
        ScaledQuantity right,
        out ScaledQuantity difference)
    {
        difference = default;
        if (right.Coefficient == long.MinValue)
        {
            if (!TryAdd(left.Coefficient, left.Scale, -(Int128)right.Coefficient, right.Scale, out var wide, out var wideScale) ||
                !TryNarrow(wide, wideScale, out var wideCoefficient, out var wideNarrowedScale))
                return false;
            difference = new ScaledQuantity(wideCoefficient, wideNarrowedScale);
            return true;
        }

        return TryAddQuantity(left, new ScaledQuantity(-right.Coefficient, right.Scale), out difference);
    }

    internal static bool TryAddMoney(
        ScaledMoney left,
        ScaledMoney right,
        out ScaledMoney sum)
    {
        sum = default;
        if (!TryAdd(left.Coefficient, left.Scale, right.Coefficient, right.Scale, out var value, out var scale) ||
            !TryNarrow(value, scale, out var coefficient, out var narrowedScale))
            return false;
        sum = new ScaledMoney(coefficient, narrowedScale);
        return true;
    }

    internal static bool TryAveragePrice(
        Int128 notionalCoefficient,
        int notionalScale,
        in ScaledQuantity quantity,
        out ScaledPrice average)
    {
        average = default;
        if (notionalCoefficient <= 0 || quantity.Coefficient <= 0 || !quantity.IsValid)
            return false;

        var quantityCoefficient = (Int128)quantity.Coefficient;
        var quantityScale = (int)quantity.Scale;
        Normalize(ref notionalCoefficient, ref notionalScale);
        Normalize(ref quantityCoefficient, ref quantityScale);

        const int resultScale = MaximumScale;
        var exponent = quantityScale + resultScale - notionalScale;
        Int128 numerator = notionalCoefficient;
        Int128 denominator = quantityCoefficient;
        if (exponent >= 0)
        {
            if (!TryMultiplyPower10(numerator, exponent, out numerator))
                return false;
        }
        else if (!TryMultiplyPower10(denominator, -exponent, out denominator))
        {
            return false;
        }

        if (!TryRoundRatio(numerator, denominator, out var rounded) ||
            !TryNarrow(rounded, resultScale, out var coefficient, out var narrowedScale))
            return false;

        average = new ScaledPrice(coefficient, narrowedScale);
        return true;
    }

    private static bool TryRoundRatio(Int128 numerator, Int128 denominator, out Int128 value)
    {
        value = 0;
        if (denominator <= 0)
            return false;

        var quotient = numerator / denominator;
        var remainder = numerator % denominator;
        var absoluteRemainder = remainder < 0 ? -remainder : remainder;
        var half = denominator / 2;
        var aboveHalf = absoluteRemainder > half;
        var exactlyHalf = denominator % 2 == 0 && absoluteRemainder == half;
        if (aboveHalf || exactlyHalf && (quotient & 1) != 0)
        {
            if (numerator >= 0 && quotient == Int128.MaxValue ||
                numerator < 0 && quotient == Int128.MinValue)
                return false;
            quotient += numerator < 0 ? -1 : 1;
        }

        value = quotient;
        return true;
    }

    internal static bool TryRoundRatioToLong(Int128 numerator, Int128 denominator, out long value)
    {
        value = 0;
        if (denominator <= 0)
            return false;

        var quotient = numerator / denominator;
        var remainder = numerator % denominator;
        var absoluteRemainder = remainder < 0 ? -remainder : remainder;
        var doubled = absoluteRemainder * 2;
        if (doubled > denominator || doubled == denominator && (quotient & 1) != 0)
            quotient += numerator < 0 ? -1 : 1;

        if (quotient < long.MinValue || quotient > long.MaxValue)
            return false;
        value = (long)quotient;
        return true;
    }

    internal static bool TryNarrow(
        Int128 coefficient,
        int scale,
        out long narrowedCoefficient,
        out byte narrowedScale)
    {
        narrowedCoefficient = 0;
        narrowedScale = 0;
        if (scale < 0)
            return false;

        Normalize(ref coefficient, ref scale);
        if (scale > MaximumScale || coefficient < long.MinValue || coefficient > long.MaxValue)
            return false;

        narrowedCoefficient = (long)coefficient;
        narrowedScale = (byte)scale;
        return true;
    }

    internal static bool TryQuantizeDouble(double value, byte scale, out long coefficient)
    {
        coefficient = 0;
        if (!double.IsFinite(value) || scale > MaximumScale)
            return false;

        try
        {
            var factor = (decimal)Pow10(scale);
            var scaled = decimal.Round((decimal)value * factor, 0, MidpointRounding.ToEven);
            if (scaled is < long.MinValue or > long.MaxValue)
                return false;
            coefficient = (long)scaled;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    internal static bool TryFromDecimal(decimal value, out long coefficient, out byte scale)
    {
        var bits = decimal.GetBits(value);
        var flags = bits[3];
        var decimalScale = (flags >> 16) & 0x7f;
        Int128 wide = (uint)bits[0];
        wide |= (Int128)(uint)bits[1] << 32;
        wide |= (Int128)(uint)bits[2] << 64;
        if ((flags & int.MinValue) != 0)
            wide = -wide;
        return TryNarrow(wide, decimalScale, out coefficient, out scale);
    }

    internal static decimal ToDecimal(long coefficient, byte scale)
    {
        if (scale > MaximumScale)
            throw new ArgumentOutOfRangeException(nameof(scale));
        decimal divisor = 1m;
        for (var index = 0; index < scale; index++)
            divisor *= 10m;
        return coefficient / divisor;
    }
}

/// <summary>
/// Explicit adapter for legacy decimal/double boundaries. Canonical OMS code must retain scaled
/// values and may not use this adapter for internal arithmetic.
/// </summary>
public static class ExecutionNumericBoundary
{
    public static ScaledQuantity QuantityFromDecimal(decimal value)
    {
        var exact = FromDecimal(value, nameof(value));
        return new ScaledQuantity(exact.Coefficient, exact.Scale);
    }

    public static ScaledPrice PriceFromDecimal(decimal value)
    {
        var exact = FromDecimal(value, nameof(value));
        return new ScaledPrice(exact.Coefficient, exact.Scale);
    }

    public static ScaledMoney MoneyFromDecimal(decimal value)
    {
        var exact = FromDecimal(value, nameof(value));
        return new ScaledMoney(exact.Coefficient, exact.Scale);
    }

    public static ScaledRatio RatioFromDecimal(decimal value)
    {
        var exact = FromDecimal(value, nameof(value));
        return new ScaledRatio(exact.Coefficient, exact.Scale);
    }

    public static bool TryQuantityFromDecimal(decimal value, out ScaledQuantity quantity)
    {
        quantity = default;
        if (!ScaledValueMath.TryFromDecimal(value, out var coefficient, out var scale))
            return false;
        quantity = new ScaledQuantity(coefficient, scale);
        return true;
    }

    public static bool TryPriceFromDecimal(decimal value, out ScaledPrice price)
    {
        price = default;
        if (!ScaledValueMath.TryFromDecimal(value, out var coefficient, out var scale))
            return false;
        price = new ScaledPrice(coefficient, scale);
        return true;
    }

    public static bool TryMoneyFromDecimal(decimal value, out ScaledMoney money)
    {
        money = default;
        if (!ScaledValueMath.TryFromDecimal(value, out var coefficient, out var scale))
            return false;
        money = new ScaledMoney(coefficient, scale);
        return true;
    }

    public static bool TryRatioFromDecimal(decimal value, out ScaledRatio ratio)
    {
        ratio = default;
        if (!ScaledValueMath.TryFromDecimal(value, out var coefficient, out var scale))
            return false;
        ratio = new ScaledRatio(coefficient, scale);
        return true;
    }

    public static ScaledPrice PriceFromDouble(double value, byte scale)
    {
        if (!ScaledValueMath.TryQuantizeDouble(value, scale, out var coefficient))
            throw new ArgumentOutOfRangeException(nameof(value), "The value cannot be represented at the requested exact scale.");
        return new ScaledPrice(coefficient, scale);
    }

    public static decimal ToDecimal(ScaledQuantity value) =>
        ScaledValueMath.ToDecimal(value.Coefficient, value.Scale);

    public static decimal ToDecimal(ScaledPrice value) =>
        ScaledValueMath.ToDecimal(value.Coefficient, value.Scale);

    public static decimal ToDecimal(ScaledMoney value) =>
        ScaledValueMath.ToDecimal(value.Coefficient, value.Scale);

    public static decimal ToDecimal(ScaledRatio value) =>
        ScaledValueMath.ToDecimal(value.Coefficient, value.Scale);

    private static (long Coefficient, byte Scale) FromDecimal(decimal value, string parameterName)
    {
        if (!ScaledValueMath.TryFromDecimal(value, out var coefficient, out var scale))
            throw new ArgumentOutOfRangeException(parameterName, "The decimal cannot be represented with a 64-bit coefficient and scale no greater than 18.");
        return (coefficient, scale);
    }
}
