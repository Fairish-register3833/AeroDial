// AeroDial — Extensions.cs
// Extension methods used across the codebase.

using Microsoft.UI.Dispatching;

namespace AeroDial.Core;

internal static class Extensions
{
    /// <summary>
    /// Fire-and-forget an async void safely — logs exceptions instead of crashing.
    /// Use this anywhere you need to start an async task from a synchronous context
    /// without awaiting it (e.g. constructors, event handlers).
    /// </summary>
    public static async void FireAndForget(this Task task)
    {
        try   { await task.ConfigureAwait(false); }
        catch (Exception ex) { Logger.Error("FireAndForget unhandled exception", ex); }
    }

    /// <summary>
    /// Enqueue an action on a DispatcherQueue and return an awaitable Task.
    /// Bridges the gap between background threads and the UI thread cleanly.
    /// </summary>
    public static Task EnqueueAsync(this DispatcherQueue queue, Action action)
    {
        var tcs = new TaskCompletionSource();
        queue.TryEnqueue(() =>
        {
            try   { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    /// <summary>Clamp a float between min and max.</summary>
    public static float Clamp(this float value, float min, float max)
        => MathF.Max(min, MathF.Min(max, value));

    /// <summary>Linear interpolation.</summary>
    public static float Lerp(float a, float b, float t)
        => a + (b - a) * t.Clamp(0f, 1f);

    /// <summary>Convert degrees to radians.</summary>
    public static float ToRadians(this float degrees) => degrees * MathF.PI / 180f;

    /// <summary>Ease-out cubic — smooth deceleration for animations.</summary>
    public static float EaseOutCubic(this float t) => 1f - MathF.Pow(1f - t.Clamp(0f, 1f), 3f);

    /// <summary>Ease-in-out quad — smooth acceleration + deceleration.</summary>
    public static float EaseInOutQuad(this float t)
    {
        t = t.Clamp(0f, 1f);
        return t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
    }

    /// <summary>Ease-out back — overshoots slightly then settles (spring effect).</summary>
    public static float EaseOutBack(this float t)
    {
        t = t.Clamp(0f, 1f);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
    }
}
