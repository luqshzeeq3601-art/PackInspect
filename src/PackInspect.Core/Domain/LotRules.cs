namespace PackInspect.Core.Domain;

/// <summary>What a lot number and quantity typed by an operator must look like.</summary>
public static class LotRules
{
    public const int MaxLotNumberLength = 32;
    public const int MaxQuantity = 10_000;

    /// <summary>Returns null when the input is acceptable, otherwise a message for the operator.</summary>
    public static string? Validate(string? lotNumber, int quantity)
    {
        if (string.IsNullOrWhiteSpace(lotNumber))
        {
            return "Enter a lot number.";
        }

        if (lotNumber.Length > MaxLotNumberLength)
        {
            return $"The lot number may be at most {MaxLotNumberLength} characters.";
        }

        // Letters, digits, dash and underscore only: enough for real lot numbers,
        // and nothing that could be mistaken for a path or a command.
        if (!lotNumber.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return "The lot number may contain only letters, digits, '-' and '_'.";
        }

        if (quantity is < 1 or > MaxQuantity)
        {
            return $"The quantity must be between 1 and {MaxQuantity}.";
        }

        return null;
    }
}
