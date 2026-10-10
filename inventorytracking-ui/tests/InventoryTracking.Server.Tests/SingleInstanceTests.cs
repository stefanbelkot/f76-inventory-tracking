using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Instance;
using InventoryTracking.Server.Shell;
using InventoryTracking.Server.Tests.Support;

namespace InventoryTracking.Server.Tests;

public class SingleInstanceTests
{
    private static InstanceNames NewNames() => InstanceNames.For("test-" + Guid.NewGuid().ToString("N"));

    private sealed class RecordingBrowser : IBrowserLauncher
    {
        public List<string> Opened { get; } = [];

        public void Open(string address)
        {
            lock (Opened)
            {
                Opened.Add(address);
            }
        }
    }

    private sealed class RecordingNotifier : IUserNotifier
    {
        public List<string> Errors { get; } = [];

        public void ShowError(string message) => Errors.Add(message);
    }

    [Fact]
    public void A_second_acquire_fails_while_the_first_is_held_and_works_after_release()
    {
        var names = NewNames();
        var first = SingleInstanceGuard.TryAcquire(names);
        Assert.NotNull(first);

        Assert.Null(SingleInstanceGuard.TryAcquire(names));
        first.Dispose();

        using var again = SingleInstanceGuard.TryAcquire(names);
        Assert.NotNull(again);
    }

    [Fact]
    public void Different_instance_ids_do_not_block_each_other()
    {
        using var one = SingleInstanceGuard.TryAcquire(NewNames());
        using var two = SingleInstanceGuard.TryAcquire(NewNames());

        Assert.NotNull(one);
        Assert.NotNull(two);
    }

    /// <summary>Holds the instance lock on its own thread, because a mutex must be released by the thread that took it.</summary>
    private sealed class HeldInstance : IDisposable
    {
        private readonly ManualResetEventSlim _release = new();
        private readonly Thread _thread;

        public HeldInstance(InstanceNames names)
        {
            var acquired = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                using var guard = SingleInstanceGuard.TryAcquire(names);
                acquired.Set();
                _release.Wait();
            });
            _thread.Start();
            acquired.Wait();
        }

        public void Dispose()
        {
            _release.Set();
            _thread.Join();
        }
    }

    [Fact]
    public async Task The_pipe_request_runs_the_callback_of_the_running_instance()
    {
        var names = NewNames();
        var opened = new TaskCompletionSource();
        using var server = new InstancePipeServer(names.Pipe, () => opened.TrySetResult());

        var answered = await InstancePipeClient.RequestOpenAsync(names.Pipe, TimeSpan.FromSeconds(5));

        Assert.True(answered);
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task The_pipe_serves_several_requests_in_a_row()
    {
        var names = NewNames();
        var count = 0;
        using var server = new InstancePipeServer(names.Pipe, () => Interlocked.Increment(ref count));

        for (var i = 0; i < 3; i++)
        {
            Assert.True(await InstancePipeClient.RequestOpenAsync(names.Pipe, TimeSpan.FromSeconds(5)));
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Without_a_listening_instance_the_request_fails_within_the_timeout()
    {
        var started = DateTime.UtcNow;

        var answered = await InstancePipeClient.RequestOpenAsync(NewNames().Pipe, TimeSpan.FromMilliseconds(500));

        Assert.False(answered);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_second_start_opens_the_browser_of_the_first_and_a_quit_allows_a_fresh_start()
    {
        using var scratch = new TempFolder();
        var instanceId = "app-" + Guid.NewGuid().ToString("N");
        var folder = scratch.Combine("modsdata");
        Directory.CreateDirectory(folder);
        var port = TestViewer.FreePort();
        var options = new CommandLineOptions
        {
            Folders = [folder],
            SettingsDirectory = scratch.Combine("settings"),
            StartPort = port,
            NoTray = true,
            InstanceId = instanceId,
        };
        var browser = new RecordingBrowser();
        var notifier = new RecordingNotifier();
        var application = new ViewerApplication(browser, notifier, new FakeMachine(null));

        var first = Task.Run(() => application.Run(options));
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };
        http.DefaultRequestHeaders.Add("X-InventoryTracking", "1");
        await WaitUntilAsync(async () => (await TryGet(http)) is true);
        await WaitUntilAsync(() => Task.FromResult(browser.Opened.Count == 1));

        var secondResult = await Task.Run(() => application.Run(options with { NoBrowser = true }));

        Assert.Equal(0, secondResult);
        await WaitUntilAsync(() => Task.FromResult(browser.Opened.Count == 2));
        Assert.All(browser.Opened, address => Assert.Equal($"http://127.0.0.1:{port}/", address));
        Assert.Empty(notifier.Errors);

        using var shutdown = await http.PostAsync("/api/v1/shutdown", null);
        Assert.Equal(0, await first.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Null(await TryGet(http));

        var third = Task.Run(() => application.Run(options with { NoBrowser = true }));
        await WaitUntilAsync(async () => (await TryGet(http)) is true);
        using var stopAgain = await http.PostAsync("/api/v1/shutdown", null);
        Assert.Equal(0, await third.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task A_start_that_finds_a_silent_instance_shows_a_message_and_exits()
    {
        var instanceId = "silent-" + Guid.NewGuid().ToString("N");
        using var held = new HeldInstance(InstanceNames.For(instanceId));
        var notifier = new RecordingNotifier();
        var application = new ViewerApplication(new RecordingBrowser(), notifier, new FakeMachine(null), TimeSpan.FromMilliseconds(300));

        var result = await Task.Run(() => application.Run(new CommandLineOptions { InstanceId = instanceId, NoTray = true }));

        Assert.Equal(1, result);
        Assert.Contains("did not answer", Assert.Single(notifier.Errors));
    }

    private static async Task<bool?> TryGet(HttpClient http)
    {
        try
        {
            using var response = await http.GetAsync("/api/v1/status");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not met in time.");
            await Task.Delay(50);
        }
    }
}
