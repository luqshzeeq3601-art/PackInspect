namespace PackInspect.Core.Domain;

public interface IRecipeStore
{
    Task<IReadOnlyList<Recipe>> GetAllAsync(CancellationToken cancellationToken);

    Task<Recipe?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<Recipe> SaveWithSignOffAsync(Recipe recipe, int engineerUserId, string reason, CancellationToken cancellationToken);

    Task<Recipe> CreateWithSignOffAsync(Recipe recipe, int engineerUserId, string reason, CancellationToken cancellationToken);
}
