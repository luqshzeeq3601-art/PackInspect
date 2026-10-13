namespace PackInspect.Data.Entities;

/// <summary>Row of the Lots table. Columns are documented in docs/Data.md.</summary>
public sealed class LotEntity
{
    public int Id { get; set; }

    public required string LotNumber { get; set; }

    public int RecipeId { get; set; }

    public RecipeEntity? Recipe { get; set; }

    /// <summary>The recipe version in force when the lot started, so later recipe edits do not rewrite history.</summary>
    public int RecipeVersion { get; set; }

    public int Quantity { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    public int? OperatorUserId { get; set; }

    public UserEntity? OperatorUser { get; set; }

    public List<UnitResultEntity> Results { get; } = [];
}
