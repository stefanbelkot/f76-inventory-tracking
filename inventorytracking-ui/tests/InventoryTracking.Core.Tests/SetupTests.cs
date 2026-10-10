using InventoryTracking.Core.Setup;
using InventoryTracking.Core.Tests.Support;

namespace InventoryTracking.Core.Tests;

public class SetupTests
{
    private static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
    }

    [Fact]
    public void A_modsdata_folder_is_accepted_as_it_is()
    {
        using var temp = new TempFolder();
        Touch(temp.Combine("Data", "modsdata", "itemdump_a_b_stash_1.json"));

        var resolution = GameFolderResolver.Resolve(temp.Combine("Data", "modsdata"));

        Assert.Equal((temp.Combine("Data", "modsdata"), FolderKind.ModsData, 1), (resolution!.Path, resolution.Kind, resolution.ExportFiles));
    }

    [Fact]
    public void A_folder_containing_data_modsdata_resolves_to_it()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(temp.Combine("Data", "modsdata"));

        var resolution = GameFolderResolver.Resolve(temp.Path);

        Assert.Equal((temp.Combine("Data", "modsdata"), FolderKind.InstallFolder), (resolution!.Path, resolution.Kind));
    }

    [Fact]
    public void A_folder_containing_content_data_modsdata_resolves_to_it()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(temp.Combine("Content", "Data", "modsdata"));

        var resolution = GameFolderResolver.Resolve(temp.Path);

        Assert.Equal((temp.Combine("Content", "Data", "modsdata"), FolderKind.ParentFolder), (resolution!.Path, resolution.Kind));
    }

    [Fact]
    public void A_folder_that_only_holds_export_files_is_accepted()
    {
        using var temp = new TempFolder();
        Touch(temp.Combine("itemdump_a_b_stash_1.json"));

        Assert.Equal(FolderKind.ExportFolder, GameFolderResolver.Resolve(temp.Path)!.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative\\path")]
    [InlineData("\\\\server\\share")]
    [InlineData("C:\\definitely\\not\\here\\anywhere")]
    public void Unusable_paths_do_not_resolve(string? path) => Assert.Null(GameFolderResolver.Resolve(path));

    [Fact]
    public void An_unrelated_folder_does_not_resolve()
    {
        using var temp = new TempFolder();

        Assert.Null(GameFolderResolver.Resolve(temp.Path));
    }

    [Fact]
    public void Library_paths_are_read_from_the_steam_vdf_text()
    {
        const string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t}\n}";

        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], ModsDataCandidates.ParseLibraryFolders(vdf));
    }

    [Fact]
    public void Candidates_are_found_for_steam_libraries_and_xbox_drives()
    {
        using var steam = new TempFolder();
        using var drive = new TempFolder();
        using var library = new TempFolder();
        Directory.CreateDirectory(Path.Combine(steam.Path, "steamapps"));
        File.WriteAllText(
            Path.Combine(steam.Path, "steamapps", "libraryfolders.vdf"),
            "\"path\"\t\t\"" + library.Path.Replace("\\", "\\\\") + "\"");
        Directory.CreateDirectory(Path.Combine(library.Path, "steamapps", "common", "Fallout76", "Data", "modsdata"));
        Touch(Path.Combine(drive.Path, "XboxGames", "Fallout 76", "Content", "Data", "modsdata", "itemdump_a_b_stash_1.json"));

        var found = ModsDataCandidates.Find(steam.Path, [drive.Path]).ToList();

        Assert.Equal(["Steam", "Xbox / Game Pass"], found.Select(c => c.Label));
        Assert.Equal([0, 1], found.Select(c => c.ExportFiles));
    }

    [Fact]
    public void Browsing_lists_only_sub_folders_and_marks_those_with_exports()
    {
        using var temp = new TempFolder();
        Touch(temp.Combine("withExports", "itemdump_a_b_stash_1.json"));
        Directory.CreateDirectory(temp.Combine("empty"));
        File.WriteAllText(temp.Combine("file.txt"), "x");

        var listing = DirectoryBrowser.List(temp.Path, [])!;

        Assert.Equal(["empty", "withExports"], listing.Dirs.Select(d => d.Name));
        Assert.Equal([false, true], listing.Dirs.Select(d => d.HasExports));
        Assert.Equal(Directory.GetParent(temp.Path)!.FullName, listing.Parent);
    }

    [Fact]
    public void Browsing_without_a_path_lists_the_drive_roots()
    {
        var listing = DirectoryBrowser.List(null, ["C:\\", "D:\\"])!;

        Assert.Equal("", listing.Path);
        Assert.Equal(["C:\\", "D:\\"], listing.Dirs.Select(d => d.Name));
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("\\\\server\\share")]
    [InlineData("C:\\no\\such\\folder")]
    public void Browsing_rejects_unusable_paths(string path) => Assert.Null(DirectoryBrowser.List(path, []));
}
