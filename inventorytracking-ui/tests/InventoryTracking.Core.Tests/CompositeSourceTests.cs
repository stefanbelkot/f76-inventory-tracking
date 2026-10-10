using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Sources;
using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Core.Types;

namespace InventoryTracking.Core.Tests;

public class CompositeSourceTests
{
    private static byte[] File1(string storage, string snapshot = "aaaaaaaa") =>
        new ExportFileBuilder().Storage(storage).Snapshot(snapshot).Rows(ExportFileBuilder.Row("Thing", 1)).Build();

    private static CompositeExportSource Combine(params string[] folders) =>
        new(folders.Select(folder => new LocalFolderSource(folder)));

    [Fact]
    public void Files_of_all_folders_are_read_and_tagged_with_their_folder()
    {
        using var steam = new TempFolder();
        using var xbox = new TempFolder();
        File.WriteAllBytes(steam.Combine("itemdump_a_b_stash_1.json"), File1("stash"));
        File.WriteAllBytes(xbox.Combine("itemdump_c_d_ammo_1.json"), File1("ammo"));
        using var source = Combine(steam.Path, xbox.Path);

        var files = source.ReadFiles();

        Assert.Equal(["itemdump_a_b_stash_1.json", "itemdump_c_d_ammo_1.json"], files.Select(file => file.Name));
        Assert.Equal([Path.GetFullPath(steam.Path), Path.GetFullPath(xbox.Path)], files.Select(file => file.Origin));
        Assert.Contains(Path.GetFullPath(xbox.Path), source.Location);
    }

    [Fact]
    public void A_copy_of_the_same_file_in_another_folder_is_read_once()
    {
        using var first = new TempFolder();
        using var copy = new TempFolder();
        var bytes = File1("stash");
        File.WriteAllBytes(first.Combine("itemdump_a_b_stash_1.json"), bytes);
        File.WriteAllBytes(copy.Combine("itemdump_a_b_stash_1.json"), bytes);
        using var source = Combine(first.Path, copy.Path);

        var file = Assert.Single(source.ReadFiles());

        Assert.Equal(Path.GetFullPath(first.Path), file.Origin);
    }

    [Fact]
    public void The_same_name_with_other_content_is_kept_so_both_snapshots_show()
    {
        using var steam = new TempFolder();
        using var copied = new TempFolder();
        File.WriteAllBytes(steam.Combine("itemdump_a_b_stash_1.json"), File1("stash", "11111111"));
        File.WriteAllBytes(copied.Combine("itemdump_a_b_stash_1.json"), File1("stash", "22222222"));
        using var source = Combine(steam.Path, copied.Path);

        var catalog = CatalogBuilder.Build(source.ReadFiles(), ItemTypeTable.Default, 1);

        Assert.Equal(2, catalog.Groups.Count);
        Assert.All(catalog.Groups, group => Assert.Equal(Model.GroupStatus.MixedSnapshots, group.Status));
        Assert.All(catalog.Groups, group => Assert.Single(group.Folders));
    }

    [Fact]
    public void A_folder_that_cannot_be_listed_is_one_unreadable_entry_and_the_others_still_load()
    {
        using var steam = new TempFolder();
        File.WriteAllBytes(steam.Combine("itemdump_a_b_stash_1.json"), File1("stash"));
        var gone = Path.Combine(Path.GetTempPath(), "it-gone-" + Guid.NewGuid().ToString("N"));
        using var source = Combine(gone, steam.Path);

        var files = source.ReadFiles();
        var catalog = CatalogBuilder.Build(files, ItemTypeTable.Default, 1);

        Assert.Equal(2, files.Count);
        Assert.Equal(Path.GetFullPath(gone), files[0].Name);
        Assert.Null(files[0].Content);
        Assert.Single(catalog.Groups);
        Assert.Contains(catalog.Issues, issue => issue.File == Path.GetFullPath(gone));
    }

    [Fact]
    public void A_change_in_any_folder_raises_the_changed_event()
    {
        using var first = new TempFolder();
        using var second = new TempFolder();
        using var source = new CompositeExportSource(
        [
            new LocalFolderSource(first.Path, TimeSpan.FromMilliseconds(20)),
            new LocalFolderSource(second.Path, TimeSpan.FromMilliseconds(20)),
        ]);
        using var raised = new ManualResetEventSlim();
        source.Changed += raised.Set;

        File.WriteAllBytes(second.Combine("itemdump_a_b_stash_1.json"), File1("stash"));

        Assert.True(raised.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void A_group_lists_the_folder_it_was_read_from()
    {
        using var steam = new TempFolder();
        File.WriteAllBytes(steam.Combine("itemdump_a_b_stash_1.json"), File1("stash"));
        using var source = Combine(steam.Path);

        var group = Assert.Single(CatalogBuilder.Build(source.ReadFiles(), ItemTypeTable.Default, 1).Groups);

        Assert.Equal([Path.GetFullPath(steam.Path)], group.Folders);
    }
}
