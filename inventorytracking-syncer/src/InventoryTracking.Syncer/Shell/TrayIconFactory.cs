using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using InventoryTracking.Syncer.Sync;

namespace InventoryTracking.Syncer.Shell;

/// <summary>Draws the tray icon at run time (no icon asset in the executable); the colour shows the state.</summary>
internal static class TrayIconFactory
{
    public static Icon Create(SyncState state)
    {
        var color = state switch
        {
            SyncState.Ok => Color.FromArgb(34, 139, 94),
            SyncState.Syncing => Color.FromArgb(37, 99, 235),
            SyncState.Offline or SyncState.Problem or SyncState.SessionExpired => Color.FromArgb(200, 60, 40),
            _ => Color.FromArgb(120, 120, 120),
        };
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var background = new SolidBrush(color);
            graphics.FillEllipse(background, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString("IS", font, Brushes.White, new RectangleF(0, 1, 32, 32), format);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
