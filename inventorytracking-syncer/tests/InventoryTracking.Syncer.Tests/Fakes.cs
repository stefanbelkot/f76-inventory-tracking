using InventoryTracking.Syncer.Sync;

namespace InventoryTracking.Syncer.Tests;

internal sealed class TempDir : IDisposable
{
    public TempDir() => Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "syncer-tests-" + Guid.NewGuid().ToString("N"));

    public string Path { get; }

    public string Sub(string name)
    {
        var path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

internal sealed class ListLog : ILog
{
    public List<string> Lines { get; } = [];

    public void Write(string message) => Lines.Add(message);
}

internal sealed class FakeClient : ISessionClient
{
    public List<string> Uploaded { get; } = [];
    public List<string> Deleted { get; } = [];
    public int UploadCalls { get; private set; }
    public SessionException? Fail { get; set; }
    public HashSet<string> Refuse { get; } = [];

    public Task<string> CreateSessionAsync(CancellationToken cancellation) => Task.FromResult(new string('a', 32));

    public Task CheckSessionAsync(string session, CancellationToken cancellation) => Fail is null ? Task.CompletedTask : Task.FromException(Fail);

    public Task<IReadOnlyList<UploadOutcome>> UploadAsync(string session, IReadOnlyList<LocalFile> files, CancellationToken cancellation)
    {
        UploadCalls++;
        if (Fail is not null)
        {
            return Task.FromException<IReadOnlyList<UploadOutcome>>(Fail);
        }

        Uploaded.AddRange(files.Select(file => file.Name));
        return Task.FromResult<IReadOnlyList<UploadOutcome>>(files.Select(file => Refuse.Contains(file.Name)
            ? new UploadOutcome(file.Name, false, "refused")
            : new UploadOutcome(file.Name, true, null)).ToList());
    }

    public Task DeleteAsync(string session, IReadOnlyList<string> names, CancellationToken cancellation)
    {
        if (Fail is not null)
        {
            return Task.FromException(Fail);
        }

        Deleted.AddRange(names);
        return Task.CompletedTask;
    }
}
