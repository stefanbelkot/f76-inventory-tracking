using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Sources;
using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Core.Types;

namespace InventoryTracking.Core.Tests;

public class LocalFolderSourceTests
{
    private static byte[] ValidFile(string storage = "stash") =>
        new ExportFileBuilder().Storage(storage).Rows(ExportFileBuilder.Row("Thing", 1)).Build();

    [Fact]
    public void Only_itemdump_json_files_directly_in_the_folder_are_read()
    {
        using var temp = new TempFolder();
        File.WriteAllBytes(temp.Combine("itemdump_a_b_stash_1.json"), ValidFile());
        File.WriteAllBytes(temp.Combine("other.json"), ValidFile());
        File.WriteAllBytes(temp.Combine("itemdump_a_b_stash_1.json.bak"), ValidFile());
        File.WriteAllBytes(temp.Combine("improvedbars.json"), []);
        Directory.CreateDirectory(temp.Combine("decoded"));
        File.WriteAllBytes(temp.Combine("decoded", "itemdump_x_y_stash_1.json"), ValidFile());

        using var source = new LocalFolderSource(temp.Path);

        var file = Assert.Single(source.ReadFiles());
        Assert.Equal("itemdump_a_b_stash_1.json", file.Name);
        Assert.NotNull(file.Content);
    }

    [Fact]
    public void A_missing_folder_throws_an_io_exception()
    {
        using var source = new LocalFolderSource(Path.Combine(Path.GetTempPath(), "it-missing-" + Guid.NewGuid().ToString("N")));

        Assert.Throws<DirectoryNotFoundException>(() => source.ReadFiles());
    }

    [Fact]
    public void A_file_that_is_too_large_is_reported_without_reading_it()
    {
        using var temp = new TempFolder();
        using (var stream = File.Create(temp.Combine("itemdump_a_b_stash_1.json")))
        {
            stream.SetLength(Decoding.DecodeLimits.MaxFileBytes + 1);
        }

        using var source = new LocalFolderSource(temp.Path);

        var file = Assert.Single(source.ReadFiles());
        Assert.Null(file.Content);
        Assert.Contains("larger", file.Error);
    }

    [Fact]
    public void A_file_that_is_open_for_writing_can_still_be_read()
    {
        using var temp = new TempFolder();
        var path = temp.Combine("itemdump_a_b_stash_1.json");
        using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        writer.Write(ValidFile());
        writer.Flush();

        using var source = new LocalFolderSource(temp.Path);

        Assert.NotNull(Assert.Single(source.ReadFiles()).Content);
    }

    [Fact]
    public void A_change_in_the_folder_raises_one_debounced_notification()
    {
        using var temp = new TempFolder();
        using var source = new LocalFolderSource(temp.Path, TimeSpan.FromMilliseconds(100));
        var raised = new CountdownEvent(1);
        var count = 0;
        source.Changed += () =>
        {
            Interlocked.Increment(ref count);
            raised.Signal();
        };

        File.WriteAllBytes(temp.Combine("itemdump_a_b_stash_1.json"), ValidFile());
        File.AppendAllText(temp.Combine("itemdump_a_b_stash_1.json"), " ");

        Assert.True(raised.Wait(TimeSpan.FromSeconds(5)));
        Thread.Sleep(300);
        Assert.Equal(1, count);
    }

    [Fact]
    public void The_catalog_service_reloads_on_change_and_bumps_the_data_version()
    {
        using var temp = new TempFolder();
        File.WriteAllBytes(temp.Combine("itemdump_a_b_stash_1.json"), ValidFile());
        using var service = new ExportCatalogService(ItemTypeTable.Default);
        service.Attach(new LocalFolderSource(temp.Path, TimeSpan.FromMilliseconds(100)));
        var first = service.Current;
        Assert.Single(first.Groups);

        File.WriteAllBytes(temp.Combine("itemdump_a_b_player_1.json"), ValidFile("player"));

        SpinWait.SpinUntil(() => service.Current.Groups.Count == 2, TimeSpan.FromSeconds(5));
        Assert.Equal(2, service.Current.Groups.Count);
        Assert.True(service.Current.Version > first.Version);
    }

    [Fact]
    public void A_manual_reload_picks_up_changes_and_a_missing_folder_becomes_an_issue()
    {
        using var temp = new TempFolder();
        var folder = temp.Combine("modsdata");
        Directory.CreateDirectory(folder);
        using var service = new ExportCatalogService(ItemTypeTable.Default);
        service.Attach(new LocalFolderSource(folder));
        Assert.Empty(service.Current.Groups);

        File.WriteAllBytes(Path.Combine(folder, "itemdump_a_b_stash_1.json"), ValidFile());
        service.Reload();
        Assert.Single(service.Current.Groups);

        Directory.Delete(folder, recursive: true);
        service.Reload();
        Assert.Empty(service.Current.Groups);
        Assert.Contains("does not exist", Assert.Single(service.Current.Issues).Message);
    }

    [Fact]
    public void Detaching_the_source_empties_the_catalog()
    {
        using var service = new ExportCatalogService(ItemTypeTable.Default);
        service.Attach(new LocalFolderSource(Fixtures.RealFolder));
        Assert.NotEmpty(service.Current.Groups);

        service.Attach(null);

        Assert.Empty(service.Current.Groups);
    }
}
