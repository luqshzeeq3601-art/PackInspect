using System.Globalization;

namespace PackInspect.Devices.Scpi;

/// <summary>Parsing of SCPI replies. Anything unexpected is rejected, never guessed.</summary>
public static class ScpiReply
{
    /// <summary>Parses a numeric reply such as <c>+9.98700000E+01</c>.</summary>
    /// <exception cref="FormatException">The reply is not a finite number.</exception>
    public static double ParseNumber(string reply)
    {
        // Invariant culture: an instrument always uses '.', whatever the PC's regional settings are.
        if (double.TryParse(reply.AsSpan().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            && double.IsFinite(value))
        {
            return value;
        }

        throw new FormatException($"The instrument reply '{reply}' is not a number.");
    }
}
