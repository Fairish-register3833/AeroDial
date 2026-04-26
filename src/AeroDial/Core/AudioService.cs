// AeroDial — AudioService.cs
// Reads the Windows master playback volume via IAudioEndpointVolume COM.
// Uses Type.GetTypeFromCLSID so no NuGet package is required.
// The COM endpoint is created lazily and cached for the process lifetime.

using System.Runtime.InteropServices;

namespace AeroDial.Core;

internal static class AudioService
{
    // ── COM interface declarations ────────────────────────────────────────
    // Method ordering must exactly match the Windows SDK vtable.
    // IUnknown methods (QueryInterface/AddRef/Release) are implicit with
    // InterfaceIsIUnknown — the first declared method maps to vtable slot 4.

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        // vtable 4 — stub; must be declared to preserve slot ordering
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out nint ppDevices);
        // vtable 5
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        // vtable 4
        [PreserveSig] int Activate(
            ref Guid iid, uint dwClsCtx, nint pActivationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        // vtable 4-10 — all must be declared in SDK order
        [PreserveSig] int RegisterControlChangeNotify(nint pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(nint pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel); // vtable 10 ← we need this
    }

    // MMDeviceEnumerator CLSID (Windows Core Audio — registered in HKCR)
    private static readonly Guid ClsidMMDeviceEnumerator =
        new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    // IAudioEndpointVolume IID (used with IMMDevice.Activate to QI the right interface)
    private static readonly Guid IidAudioEndpointVolume =
        new("5CDF2C82-841E-4546-9722-0CF74078229A");

    // ── State ─────────────────────────────────────────────────────────────

    private static IAudioEndpointVolume? _endpoint;
    private static bool                  _initialized;

    // Force endpoint re-acquisition every 2 s to pick up default-device switches.
    // Initialised to long.MinValue/2 (not long.MinValue) to avoid signed-integer overflow in
    // the "now - _lastReinit >= ReinitIntervalMs" subtraction on first call.
    private static long _lastReinit      = long.MinValue / 2;
    private const  long ReinitIntervalMs = 2000;

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current Windows master playback volume as a 0.0–1.0 scalar.
    /// Safe to call from any thread.  The COM endpoint is refreshed every 2 s so
    /// output-device switches are reflected promptly.  Falls back to 0.5 if the
    /// Windows Audio service cannot be reached.
    /// </summary>
    public static float GetMasterVolume()
    {
        // Re-acquire every 2 s so changing the default playback device is reflected promptly.
        long now = Environment.TickCount64;
        if (now - _lastReinit >= ReinitIntervalMs)
        {
            _lastReinit  = now;
            _endpoint    = null;
            _initialized = false;
        }

        EnsureInitialized();
        if (_endpoint is null) return 0.5f;
        try
        {
            _endpoint.GetMasterVolumeLevelScalar(out float level);
            return Math.Clamp(level, 0f, 1f);
        }
        catch
        {
            // COM interface stale (service restart, device removal) — retry next poll.
            _endpoint    = null;
            _initialized = false;
            return 0.5f;
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true; // set optimistically; reset below if init fails
        try
        {
            // Type.GetTypeFromCLSID creates the CoClass via CoCreateInstance.
            // Works from both STA and MTA threads — Windows Audio is a free-threaded server.
            var type       = Type.GetTypeFromCLSID(ClsidMMDeviceEnumerator)
                             ?? throw new COMException("MMDeviceEnumerator CLSID not found");
            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type)!;

            enumerator.GetDefaultAudioEndpoint(
                0 /* eRender  — playback endpoint */,
                0 /* eConsole — default role      */,
                out var device);

            var iid = IidAudioEndpointVolume;
            device.Activate(ref iid, 23 /* CLSCTX_ALL */, 0, out var volObj);
            _endpoint = (IAudioEndpointVolume)volObj;

            Logger.Debug("AudioService: IAudioEndpointVolume acquired.");
        }
        catch (Exception ex)
        {
            // Allow retry on the next call — audio service may be temporarily unavailable.
            _initialized = false;
            Logger.Warn("AudioService: could not open IAudioEndpointVolume — will retry next poll.", ex);
        }
    }
}
