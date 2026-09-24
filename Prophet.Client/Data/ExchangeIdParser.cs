using System;

namespace Prophet.Client.Data;

public static class ExchangeIdParser
{
    public static ExchangeId ParseOrDefault(string? value, ExchangeId defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (Enum.TryParse<ExchangeId>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }
}


