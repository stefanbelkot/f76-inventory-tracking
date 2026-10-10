using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Core.Types;
using InventoryTracking.Server.Settings;

namespace InventoryTracking.Server.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Missing_settings_give_the_defaults_and_loading_writes_nothing()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Combine("settings"));

        var settings = store.Load();

        Assert.Empty(settings.Folders);
        Assert.Equal("system", settings.Theme);
        Assert.False(Directory.Exists(temp.Combine("settings")));
    }

    [Fact]
    public void An_update_is_saved_and_leaves_only_the_settings_file()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Combine("settings"));

        store.Update(s => s with { Folders = [@"D:\Game\modsdata", @"C:\Steam\modsdata"], Theme = "dark", Columns = ["name", "count"] });
        var loaded = new SettingsStore(temp.Combine("settings")).Load();

        Assert.Equal([@"D:\Game\modsdata", @"C:\Steam\modsdata"], loaded.Folders);
        Assert.Equal("dark", loaded.Theme);
        Assert.Equal(["name", "count"], loaded.Columns);
        Assert.Equal(["settings.json"], Directory.GetFiles(temp.Combine("settings")).Select(Path.GetFileName));
    }

    [Fact]
    public void A_corrupt_settings_file_falls_back_to_the_defaults()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.Combine("settings.json"), "{ not json");

        Assert.Equal("system", new SettingsStore(temp.Path).Load().Theme);
    }

    [Fact]
    public void The_folder_session_prefers_the_command_line_folder_and_does_not_save_it()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Combine("settings"));
        store.Update(s => s with { Folders = [temp.Combine("saved")] });
        using var catalog = new ExportCatalogService(ItemTypeTable.Default);

        var session = new FolderSession(catalog, store, [temp.Combine("cli")]);
        session.Start();

        Assert.Equal([temp.Combine("cli")], session.Folders);
        Assert.Equal(FolderOrigin.CommandLine, session.Origin);
        Assert.Equal([temp.Combine("saved")], store.Load().Folders);
    }

    [Fact]
    public void Without_any_folder_nothing_is_loaded_and_setup_is_needed()
    {
        using var temp = new TempFolder();
        using var catalog = new ExportCatalogService(ItemTypeTable.Default);

        var session = new FolderSession(catalog, new SettingsStore(temp.Path), []);
        session.Start();

        Assert.Empty(session.Folders);
        Assert.Empty(catalog.Current.Groups);
    }

    [Fact]
    public void The_single_folder_of_the_first_version_is_migrated_into_the_folder_list()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.Combine("settings.json"), """{ "folder": "D:\\Game\\modsdata", "theme": "dark" }""");
        var store = new SettingsStore(temp.Path);

        var loaded = store.Load();
        store.Update(s => s with { Theme = "light" });

        Assert.Equal([@"D:\Game\modsdata"], loaded.Folders);
        Assert.Null(loaded.Folder);
        Assert.DoesNotContain("\"folder\"", File.ReadAllText(temp.Combine("settings.json")));
        Assert.Equal([@"D:\Game\modsdata"], new SettingsStore(temp.Path).Load().Folders);
    }

    [Fact]
    public void Folders_added_and_removed_in_the_session_are_saved_and_not_duplicated()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Combine("settings"));
        using var catalog = new ExportCatalogService(ItemTypeTable.Default);
        var session = new FolderSession(catalog, store, []);

        session.Add(temp.Combine("steam"));
        session.Add(temp.Combine("xbox"));
        session.Add(temp.Combine("STEAM") + "\\");
        var afterAdds = store.Load().Folders;
        session.Remove(temp.Combine("steam"));

        Assert.Equal([temp.Combine("steam"), temp.Combine("xbox")], afterAdds);
        Assert.Equal([temp.Combine("xbox")], session.Folders);
        Assert.Equal([temp.Combine("xbox")], store.Load().Folders);
    }

    [Fact]
    public void Adding_a_folder_to_the_command_line_folders_saves_them_together()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Combine("settings"));
        using var catalog = new ExportCatalogService(ItemTypeTable.Default);
        var session = new FolderSession(catalog, store, [temp.Combine("cli")]);
        session.Start();

        session.Add(temp.Combine("added"));

        Assert.Equal(FolderOrigin.Saved, session.Origin);
        Assert.Equal([temp.Combine("cli"), temp.Combine("added")], store.Load().Folders);
    }
}
