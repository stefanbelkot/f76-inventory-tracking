using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Core.Types;
using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Sessions;
using InventoryTracking.Server.Tests.Support;

namespace InventoryTracking.Server.Tests;

public class SessionCatalogsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static byte[] Export(string storage) =>
        new ExportFileBuilder().Storage(storage).Rows(ExportFileBuilder.Row("Thing", 1)).Build();

    private sealed class Setup : IDisposable
    {
        private readonly TempFolder _temp = new();

        public Setup(Func<(int Generation, IReadOnlyList<string> Folders)>? local = null)
        {
            Clock = new ManualTimeProvider(Start);
            Store = new SessionStore(_temp.Combine("sessions"), new WebOptions(), Clock);
            Catalogs = new SessionCatalogs(Store, ItemTypeTable.Default, Clock, local);
        }

        public ManualTimeProvider Clock { get; }

        public SessionStore Store { get; }

        public SessionCatalogs Catalogs { get; }

        public string Path(params string[] parts) => _temp.Combine(parts);

        public void Dispose()
        {
            Catalogs.Dispose();
            _temp.Dispose();
        }
    }

    [Fact]
    public void An_unknown_or_malformed_session_has_no_catalog()
    {
        using var setup = new Setup();

        Assert.Null(setup.Catalogs.Get("0123456789abcdef0123456789abcdef"));
        Assert.Null(setup.Catalogs.Get("nonsense"));
        Assert.Null(setup.Catalogs.Get(null));
        Assert.Equal(0, setup.Catalogs.InMemory);
    }

    [Fact]
    public void The_catalog_is_built_from_the_session_folder_and_rebuilt_after_a_change()
    {
        using var setup = new Setup();
        var id = setup.Store.Create();
        setup.Store.Save(id, "itemdump_a_h_stash_1.json", Export("stash"));
        var catalog = setup.Catalogs.Get(id)!;
        var before = catalog.Current.Version;

        setup.Store.Save(id, "itemdump_a_h_aid_1.json", Export("aid"));
        setup.Catalogs.Refresh(id);

        Assert.Single(catalog.Current.Groups, group => group.Storage == "stash");
        Assert.Equal(2, catalog.Current.Groups.Count);
        Assert.True(catalog.Current.Version > before);
        Assert.Same(catalog, setup.Catalogs.Get(id));

        setup.Store.Delete(id, ["itemdump_a_h_stash_1.json"]);
        setup.Catalogs.Refresh(id);

        Assert.Equal(["aid"], catalog.Current.Groups.Select(group => group.Storage));
    }

    [Fact]
    public void Sessions_do_not_see_each_others_files()
    {
        using var setup = new Setup();
        var first = setup.Store.Create();
        var second = setup.Store.Create();
        setup.Store.Save(first, "itemdump_a_h_stash_1.json", Export("stash"));

        Assert.Single(setup.Catalogs.Get(first)!.Current.Groups);
        Assert.Empty(setup.Catalogs.Get(second)!.Current.Groups);
    }

    [Fact]
    public void An_idle_catalog_leaves_memory_after_an_hour_and_is_read_from_disk_again_on_demand()
    {
        using var setup = new Setup();
        var id = setup.Store.Create();
        setup.Store.Save(id, "itemdump_a_h_stash_1.json", Export("stash"));
        setup.Catalogs.Get(id);

        setup.Clock.Advance(TimeSpan.FromMinutes(59));
        Assert.Equal(0, setup.Catalogs.EvictIdle());
        setup.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, setup.Catalogs.EvictIdle());
        Assert.Equal(0, setup.Catalogs.InMemory);

        var again = setup.Catalogs.Get(id)!;

        Assert.Single(again.Current.Groups);
        Assert.Equal(1, setup.Catalogs.InMemory);
    }

    [Fact]
    public void Using_a_catalog_keeps_it_in_memory()
    {
        using var setup = new Setup();
        var id = setup.Store.Create();
        setup.Catalogs.Get(id);

        setup.Clock.Advance(TimeSpan.FromMinutes(50));
        setup.Catalogs.Get(id);
        setup.Clock.Advance(TimeSpan.FromMinutes(50));

        Assert.Equal(0, setup.Catalogs.EvictIdle());
    }

    [Fact]
    public void A_forgotten_session_is_dropped_from_memory_at_once()
    {
        using var setup = new Setup();
        var id = setup.Store.Create();
        setup.Catalogs.Get(id);

        setup.Catalogs.Forget(id);

        Assert.Equal(0, setup.Catalogs.InMemory);
    }

    [Fact]
    public void In_both_mode_a_session_also_reads_the_local_folders_and_notices_when_they_change()
    {
        using var temp = new TempFolder();
        var local = temp.Combine("modsdata");
        Directory.CreateDirectory(local);
        File.WriteAllBytes(Path.Combine(local, "itemdump_a_h_scrap_1.json"), Export("scrap"));
        var generation = 1;
        IReadOnlyList<string> folders = [local];
        using var setup = new Setup(() => (generation, folders));
        var id = setup.Store.Create();
        setup.Store.Save(id, "itemdump_a_h_stash_1.json", Export("stash"));

        var first = setup.Catalogs.Get(id)!;
        Assert.Equal(["scrap", "stash"], first.Current.Groups.Select(group => group.Storage).Order());

        generation = 2;
        folders = [];
        var second = setup.Catalogs.Get(id)!;

        Assert.Equal(["stash"], second.Current.Groups.Select(group => group.Storage));
        Assert.NotSame(first, second);
    }
}
