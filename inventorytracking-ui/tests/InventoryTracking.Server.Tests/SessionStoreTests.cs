using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Sessions;
using InventoryTracking.Server.Tests.Support;

namespace InventoryTracking.Server.Tests;

public class SessionStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static byte[] Export(string storage = "stash") =>
        new ExportFileBuilder().Storage(storage).Rows(ExportFileBuilder.Row("Thing", 1)).Build();

    private static SessionStore Store(TempFolder temp, WebOptions? options = null, TimeProvider? time = null) =>
        new(temp.Combine("sessions"), options ?? new WebOptions(), time);

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF", false)]
    [InlineData("0123456789abcdef0123456789abcde", false)]
    [InlineData("..\\..\\windows", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_a_32_digit_lowercase_hex_number_is_a_session_id(string? id, bool valid) =>
        Assert.Equal(valid, SessionStore.IsValidId(id));

    [Fact]
    public void A_new_session_is_a_random_id_with_its_own_folder()
    {
        using var temp = new TempFolder();
        var store = Store(temp);

        var first = store.Create();
        var second = store.Create();

        Assert.NotEqual(first, second);
        Assert.True(SessionStore.IsValidId(first));
        Assert.True(Directory.Exists(store.FolderOf(first)));
        Assert.True(store.Touch(first));
        Assert.False(store.Touch("f".PadLeft(32, 'f')));
    }

    [Fact]
    public void The_server_refuses_new_sessions_beyond_its_limit()
    {
        using var temp = new TempFolder();
        var store = Store(temp, new WebOptions { MaxSessions = 2 });
        store.Create();
        store.Create();

        Assert.Throws<SessionLimitException>(() => store.Create());
    }

    [Fact]
    public void A_valid_export_file_is_stored_listed_and_replaced_by_the_same_name()
    {
        using var temp = new TempFolder();
        var store = Store(temp);
        var id = store.Create();

        var first = store.Save(id, "itemdump_a_h_stash_1.json", Export());
        store.Save(id, "itemdump_a_h_aid_1.json", Export("aid"));
        var replaced = store.Save(id, "itemdump_a_h_stash_1.json", Export());

        Assert.True(first.Accepted);
        Assert.True(replaced.Accepted);
        Assert.Equal(["itemdump_a_h_aid_1.json", "itemdump_a_h_stash_1.json"], store.List(id).Select(file => file.Name));
        Assert.DoesNotContain(Directory.GetFiles(store.FolderOf(id)), path => path.EndsWith(".tmp"));
    }

    [Theory]
    [InlineData("notes.json")]
    [InlineData("itemdump_a_h_stash_1.txt")]
    [InlineData("itemdump_.json")]
    [InlineData("..\\itemdump_a_h_stash_1.json.exe")]
    public void Files_that_are_not_export_files_are_refused_by_name(string name)
    {
        using var temp = new TempFolder();
        var store = Store(temp);
        var id = store.Create();

        var result = store.Save(id, name, Export());

        Assert.False(result.Accepted);
        Assert.Empty(store.List(id));
    }

    [Fact]
    public void A_path_in_the_file_name_cannot_leave_the_session_folder()
    {
        using var temp = new TempFolder();
        var store = Store(temp);
        var id = store.Create();

        var result = store.Save(id, "..\\..\\itemdump_a_h_stash_1.json", Export());

        Assert.True(result.Accepted);
        Assert.Equal("itemdump_a_h_stash_1.json", result.Name);
        Assert.Equal(["itemdump_a_h_stash_1.json"], store.List(id).Select(file => file.Name));
        Assert.False(File.Exists(temp.Combine("itemdump_a_h_stash_1.json")));
    }

    [Fact]
    public void Damaged_empty_and_oversized_content_is_refused_with_a_reason()
    {
        using var temp = new TempFolder();
        var store = Store(temp, new WebOptions { MaxFileBytes = 1000 });
        var id = store.Create();

        var damaged = store.Save(id, "itemdump_a_h_stash_1.json", "{ not json"u8.ToArray());
        var empty = store.Save(id, "itemdump_a_h_stash_2.json", []);
        var large = store.Save(id, "itemdump_a_h_stash_3.json", new byte[1001]);

        Assert.All(new[] { damaged, empty, large }, result => Assert.False(result.Accepted));
        Assert.Contains("JSON", damaged.Error);
        Assert.Contains("empty", empty.Error);
        Assert.Contains("larger", large.Error);
        Assert.Empty(store.List(id));
    }

    [Fact]
    public void A_session_holds_a_limited_number_of_files_and_bytes_but_a_replacement_always_fits()
    {
        using var temp = new TempFolder();
        var size = Export().Length;
        var store = Store(temp, new WebOptions { MaxFilesPerSession = 2, MaxSessionBytes = size * 2 + 10 });
        var id = store.Create();

        Assert.True(store.Save(id, "itemdump_a_h_stash_1.json", Export()).Accepted);
        Assert.True(store.Save(id, "itemdump_a_h_stash_2.json", Export()).Accepted);
        var third = store.Save(id, "itemdump_a_h_stash_3.json", Export());
        var replacement = store.Save(id, "itemdump_a_h_stash_2.json", Export());

        Assert.False(third.Accepted);
        Assert.Contains("at most 2 files", third.Error);
        Assert.True(replacement.Accepted);
    }

    [Fact]
    public void Files_can_be_deleted_one_by_one_or_several_at_once_and_unknown_names_are_ignored()
    {
        using var temp = new TempFolder();
        var store = Store(temp);
        var id = store.Create();
        foreach (var part in new[] { "1", "2", "3", "4" })
        {
            store.Save(id, $"itemdump_a_h_stash_{part}.json", Export());
        }

        var deleted = store.Delete(id, ["itemdump_a_h_stash_1.json", "itemdump_a_h_stash_3.json", "itemdump_missing.json", "..\\..\\x", ".used"]);

        Assert.Equal(2, deleted);
        Assert.Equal(["itemdump_a_h_stash_2.json", "itemdump_a_h_stash_4.json"], store.List(id).Select(file => file.Name));
    }

    [Fact]
    public void Deleting_a_session_removes_its_folder()
    {
        using var temp = new TempFolder();
        var store = Store(temp);
        var id = store.Create();
        store.Save(id, "itemdump_a_h_stash_1.json", Export());

        store.DeleteSession(id);

        Assert.False(Directory.Exists(store.FolderOf(id)));
        Assert.False(store.Touch(id));
    }

    [Fact]
    public void A_session_expires_a_week_after_its_last_use_not_after_its_creation()
    {
        using var temp = new TempFolder();
        var clock = new ManualTimeProvider(Start);
        var store = Store(temp, time: clock);
        var id = store.Create();

        clock.Advance(TimeSpan.FromDays(6));
        store.Touch(id);
        clock.Advance(TimeSpan.FromDays(6));
        var beforeLimit = store.DeleteExpired();
        clock.Advance(TimeSpan.FromDays(2));
        var afterLimit = store.DeleteExpired();

        Assert.Equal(0, beforeLimit);
        Assert.Equal(1, afterLimit);
        Assert.False(Directory.Exists(store.FolderOf(id)));
    }

    [Fact]
    public void Uploading_counts_as_use_and_the_last_use_survives_a_restart()
    {
        using var temp = new TempFolder();
        var clock = new ManualTimeProvider(Start);
        var id = Store(temp, time: clock).Create();
        clock.Advance(TimeSpan.FromDays(5));
        Store(temp, time: clock).Save(id, "itemdump_a_h_stash_1.json", Export());
        clock.Advance(TimeSpan.FromDays(5));

        var restarted = Store(temp, time: clock);

        Assert.Equal(0, restarted.DeleteExpired());
        clock.Advance(TimeSpan.FromDays(3));
        Assert.Equal(1, restarted.DeleteExpired());
    }

    [Fact]
    public void Only_session_folders_are_ever_deleted_by_the_cleanup()
    {
        using var temp = new TempFolder();
        var clock = new ManualTimeProvider(Start);
        var store = Store(temp, time: clock);
        store.Create();
        Directory.CreateDirectory(temp.Combine("sessions", "not-a-session"));
        File.WriteAllText(temp.Combine("sessions", "readme.txt"), "keep");
        clock.Advance(TimeSpan.FromDays(30));

        store.DeleteExpired();

        Assert.True(Directory.Exists(temp.Combine("sessions", "not-a-session")));
        Assert.True(File.Exists(temp.Combine("sessions", "readme.txt")));
    }
}
