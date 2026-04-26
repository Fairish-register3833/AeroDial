// AeroDial — ActionDispatcher.cs
// Executes menu item actions. Each action type is handled by a dedicated
// private method so the logic stays readable and easy to extend.

using System.Diagnostics;
using System.Runtime.InteropServices;
using AeroDial.Config;
using AeroDial.Core;

namespace AeroDial.Actions;

internal sealed class ActionDispatcher
{
    // ── Entry point ───────────────────────────────────────────────────────

    /// <summary>Execute the action defined by a menu item. Fire-and-forget safe.</summary>
    public void Execute(MenuItemConfig item)
    {
        try
        {
            Logger.Info($"Executing action: {item.ActionType} — '{item.Label}'");

            switch (item.ActionType)
            {
                case ActionType.LaunchApp:      LaunchApp(item);     break;
                case ActionType.OpenUrl:        OpenUrl(item);       break;
                case ActionType.KeyCombo:       SendKeyCombo(item);  break;
                case ActionType.Media:          SendMedia(item);     break;
                case ActionType.RunScript:      RunScript(item);     break;
                case ActionType.PasteClipboard: PasteClip(item);    break;
                case ActionType.OpenSettings:   OpenSettings();      break;
                case ActionType.FocusWindow:    FocusWindow(item);   break;
                case ActionType.SubMenu:
                case ActionType.None:           /* handled by overlay */ break;
                default:
                    Logger.Warn($"Unhandled action type: {item.ActionType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Action execution failed for '{item.Label}'", ex);
        }
    }

    // ── Action implementations ────────────────────────────────────────────

    private static void LaunchApp(MenuItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.AppPath)) return;

        var psi = new ProcessStartInfo
        {
            FileName        = item.AppPath,
            Arguments       = item.AppArgs ?? string.Empty,
            UseShellExecute = true,
        };
        Process.Start(psi);
    }

    private static void OpenUrl(MenuItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.Url)) return;
        Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true });
    }

    private static void SendKeyCombo(MenuItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.KeyCombo)) return;

        var parts    = item.KeyCombo.Split('+', StringSplitOptions.RemoveEmptyEntries);
        var keys     = new List<byte>();
        var modifiers = new List<byte>();

        foreach (var part in parts)
        {
            byte vk = part.Trim().ToUpperInvariant() switch
            {
                "WIN"   or "WINDOWS" => 0x5B,
                "CTRL"  or "CONTROL" => 0x11,
                "ALT"                => 0x12,
                "SHIFT"              => 0x10,
                "TAB"                => 0x09,
                "ENTER"              => 0x0D,
                "ESC"   or "ESCAPE"  => 0x1B,
                "SPACE"              => 0x20,
                "DEL"   or "DELETE"  => 0x2E,
                "HOME"               => 0x24,
                "END"                => 0x23,
                "LEFT"               => 0x25,
                "UP"                 => 0x26,
                "RIGHT"              => 0x27,
                "DOWN"               => 0x28,
                _ when part.Length == 1 && char.IsLetter(part[0])
                                     => (byte)char.ToUpper(part[0]),
                _ when part.StartsWith('F') && int.TryParse(part[1..], out int fn)
                                     => (byte)(0x6F + fn),
                _                    => 0,
            };

            if (vk == 0) continue;

            if (vk is 0x5B or 0x11 or 0x12 or 0x10)
                modifiers.Add(vk);
            else
                keys.Add(vk);
        }

        // Build input array: press modifiers → press keys → release keys → release modifiers
        var allDown = modifiers.Concat(keys).ToArray();
        var allUp   = allDown.Reverse().ToArray();

        var inputs = new Win32.INPUT[allDown.Length + allUp.Length];
        int i = 0;
        foreach (var vk in allDown)
            inputs[i++] = MakeKeyInput(vk, false);
        foreach (var vk in allUp)
            inputs[i++] = MakeKeyInput(vk, true);

        Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static Win32.INPUT MakeKeyInput(byte vk, bool keyUp) => new()
    {
        type = 1, // INPUT_KEYBOARD
        u = new Win32.INPUTUNION
        {
            ki = new Win32.KEYBDINPUT
            {
                wVk     = vk,
                dwFlags = keyUp ? 0x0002u : 0u, // KEYEVENTF_KEYUP
            }
        }
    };

    private static void SendMedia(MenuItemConfig item)
    {
        byte vk = item.MediaAction switch
        {
            MediaActionType.PlayPause   => 0xB3,
            MediaActionType.Next        => 0xB0,
            MediaActionType.Previous    => 0xB1,
            MediaActionType.VolumeUp    => 0xAF,
            MediaActionType.VolumeDown  => 0xAE,
            MediaActionType.Mute        => 0xAD,
            _                           => 0,
        };

        if (vk == 0) return;

        // Media keys require a down/up cycle.
        Win32.keybd_event(vk, 0, 0, 0);
        Win32.keybd_event(vk, 0, 2, 0); // KEYEVENTF_KEYUP = 2
    }

    private static void RunScript(MenuItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.ScriptPath)) return;

        var ext = Path.GetExtension(item.ScriptPath).ToLowerInvariant();
        var psi = ext switch
        {
            ".ps1" => new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NonInteractive -File \"{item.ScriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow  = true,
            },
            _ => new ProcessStartInfo
            {
                FileName        = item.ScriptPath,
                UseShellExecute = true,
            }
        };
        Process.Start(psi);
    }

    private static void PasteClip(MenuItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.ClipText)) return;

        // Set clipboard and then send Ctrl+V.
        // We dispatch to the UI thread for clipboard access.
        App.Tray.DispatcherQueue.TryEnqueue(() =>
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(item.ClipText);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        });

        // Small delay so the clipboard is populated before we paste.
        Task.Delay(80).ContinueWith(_ =>
            SendKeyCombo(new MenuItemConfig { KeyCombo = "Ctrl+V" }));
    }

    private static void FocusWindow(MenuItemConfig item)
    {
        if (item.WindowHandle == 0) return;
        Win32.ShowWindow(item.WindowHandle, Win32.SW_RESTORE);
        Win32.SetForegroundWindow(item.WindowHandle);
    }

    private static void OpenSettings()
        => App.Tray.DispatcherQueue.TryEnqueue(() =>
            AeroDial.UI.Views.SettingsWindow.ShowOrActivate());
}
