// AeroDial — Logger.cs
// Thread-safe, append-only file logger. Deliberately lightweight —
// no NuGet dependency required for something this simple.

using System.Collections.Concurrent;

namespace AeroDial.Core;

internal static class Logger
{
    private static readonly ConcurrentQueue<string> _queue = new();
    private static readonly CancellationTokenSource _cts   = new();
    private static readonly Task _flushTask;

    static Logger()
    {
        Directory.CreateDirectory(AppConstants.AppDataDir);
        _flushTask = Task.Run(FlushLoopAsync);
    }

    private static volatile bool _debugEnabled;

    // ── Public API ────────────────────────────────────────────────────────

    public static void SetDebugMode(bool enabled) => _debugEnabled = enabled;

    public static void Info (string msg, Exception? ex = null) => Write("INFO ", msg, ex);
    public static void Warn (string msg, Exception? ex = null) => Write("WARN ", msg, ex);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", msg, ex);
    public static void Fatal(string msg, Exception? ex = null) => Write("FATAL", msg, ex);
    public static void Debug(string msg, Exception? ex = null) { if (_debugEnabled) Write("DEBUG", msg, ex); }

    // ── Implementation ────────────────────────────────────────────────────

    private static void Write(string level, string msg, Exception? ex)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level}] {msg}";
        if (ex is not null)
            line += Environment.NewLine + "  " + ex.ToString().Replace("\n", "\n  ");

        _queue.Enqueue(line);

#if DEBUG
        System.Diagnostics.Debug.WriteLine(line);
#endif
    }

    private static async Task FlushLoopAsync()
    {
        using var writer = new StreamWriter(AppConstants.LogPath, append: true);
        writer.AutoFlush = false;

        while (!_cts.Token.IsCancellationRequested)
        {
            while (_queue.TryDequeue(out var line))
                await writer.WriteLineAsync(line);

            await writer.FlushAsync();
            await Task.Delay(500, _cts.Token).ContinueWith(_ => { });
        }
    }
}
