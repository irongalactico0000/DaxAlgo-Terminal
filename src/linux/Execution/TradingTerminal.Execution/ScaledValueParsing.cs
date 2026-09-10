namespace TradingTerminal.Execution;

/// <summary>Exact decimal-string parsing into scaled OMS values (no binary float).</summary>
public static class ScaledValueParsing
{
    public static bool TryParseQuantity(string? text, out ScaledQuantity quantity, out string failure)
    {
        quantity = default;
        if (!TryParseScaled(text, out var coefficient, out var scale) || coefficient <= 0)
        {
            failure = "Enter a positive quantity (whole or decimal, invariant culture).";
            return false;
        }

        quantity = new ScaledQuantity(coefficient, scale);
        failure = string.Empty;
        return quantity.IsValid;
    }

    public static bool TryParsePrice(string? text, out ScaledPrice price, out string failure)
    {
        price = default;
        if (!TryParseScaled(text, out var coefficient, out var scale) || coefficient <= 0)
        {
            failure = "Enter a positive limit/stop price (invariant culture, e.g. 190.25).";
            return false;
        }

        price = new ScaledPrice(coefficient, scale);
        failure = string.Empty;
        return price.IsValid;
    }

    private static bool TryParseScaled(string? value, out long coefficient, out byte scale)
    {
        coefficient = 0;
        scale = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var span = value.AsSpan().Trim();
        var negative = false;
        var index = 0;
        if (span[0] is '+' or '-')
        {
            negative = span[0] == '-';
            index++;
        }
        if (index == span.Length)
            return false;

        Int128 magnitude = 0;
        var seenDigit = false;
        var seenDecimal = false;
        var decimals = 0;
        for (; index < span.Length; index++)
        {
            var character = span[index];
            if (character == '.' && !seenDecimal)
            {
                seenDecimal = true;
                continue;
            }
            if (character is < '0' or > '9')
                return false;
            seenDigit = true;
            if (seenDecimal)
                decimals++;
            if (decimals > ScaledValueMath.MaximumScale || magnitude > (Int128.MaxValue - 9) / 10)
                return false;
            magnitude = magnitude * 10 + (character - '0');
        }
        if (!seenDigit)
            return false;

        var signed = negative ? -magnitude : magnitude;
        return ScaledValueMath.TryNarrow(signed, decimals, out coefficient, out scale);
    }
}
