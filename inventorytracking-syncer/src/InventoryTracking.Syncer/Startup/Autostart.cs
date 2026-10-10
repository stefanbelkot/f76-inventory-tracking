using Microsoft.Win32;

namespace InventoryTracking.Syncer.Startup;

/// <summary>The one value the tool owns in the per-user Run key.</summary>
public interface IRunKey
{
    string? Get();

    void Set(string command);

    void Remove();
}

public sealed class RegistryRunKey : IRunKey
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "InventoryTrackingSyncer";

    public string? Get()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) as string;
    }

    public void Set(string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(ValueName, command);
    }

    public void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

/// <summary>Start with Windows: <c>"&lt;exe&gt;" --background</c> in the user's Run key (no admin rights).</summary>
public sealed class Autostart(IRunKey key, string exePath)
{
    public string Command => $"\"{exePath}\" --background";

    public bool IsEnabled => key.Get() is { Length: > 0 };

    public void Set(bool enabled)
    {
        if (enabled)
        {
            key.Set(Command);
        }
        else
        {
            key.Remove();
        }
    }

    /// <summary>When autostart is wanted, make the registry value point at the current exe (it moves when the user moves the file).</summary>
    public void Reconcile(bool wanted)
    {
        if (!wanted)
        {
            return;
        }

        if (key.Get() != Command)
        {
            key.Set(Command);
        }
    }
}
