using Microsoft.Data.Sqlite;

namespace PackInspect.Tests.Fakes;

/// <summary>A private folder for one test's database and images, removed afterwards.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "packinspect-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would keep the file locked.
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Left for the operating system's temp cleanup; not worth failing a test over.
        }
    }
}
