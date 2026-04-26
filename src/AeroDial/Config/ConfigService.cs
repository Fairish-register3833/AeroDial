// AeroDial — ConfigService.cs
// Responsible for loading, saving, and live-reloading the configuration file.
// Uses System.Text.Json with indented output so the file is human-readable
// and editable by power users.

using System.Text.Json;
using System.Text.Json.Serialization;
using AeroDial.Core;

namespace AeroDial.Config;

internal sealed class ConfigService
{
    // ── State ─────────────────────────────────────────────────────────────
    public AeroDialConfig Current { get; private set; }

    public event Action? ConfigChanged;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented         = true,
        PropertyNamingPolicy  = JsonNamingPolicy.CamelCase,
        Converters            = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling   = JsonCommentHandling.Skip,
        AllowTrailingCommas   = true,
    };

    private ConfigService(AeroDialConfig config) => Current = config;

    // ── Bootstrap ─────────────────────────────────────────────────────────

    public static async Task<ConfigService> LoadAsync()
    {
        Directory.CreateDirectory(AppConstants.AppDataDir);

        AeroDialConfig config;

        if (File.Exists(AppConstants.ConfigPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(AppConstants.ConfigPath);
                config = JsonSerializer.Deserialize<AeroDialConfig>(json, s_jsonOptions)
                         ?? new AeroDialConfig();
                Logger.Info($"Config loaded from {AppConstants.ConfigPath}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to parse config.json — using defaults", ex);
                config = new AeroDialConfig();
            }
        }
        else
        {
            config = new AeroDialConfig();
            Logger.Info("No config found — writing defaults.");
        }

        var service = new ConfigService(config);
        await service.SaveAsync(); // always flush so file stays in sync with model
        return service;
    }

    // ── Persistence ───────────────────────────────────────────────────────

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, s_jsonOptions);
            // Atomic write: write to temp file then replace, so a crash mid-write
            // never corrupts the config.
            var tmpPath = AppConstants.ConfigPath + ".tmp";
            await File.WriteAllTextAsync(tmpPath, json);
            File.Move(tmpPath, AppConstants.ConfigPath, overwrite: true);
            Logger.Info("Config saved.");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save config", ex);
        }
    }

    // ── Mutation helpers ─────────────────────────────────────────────────

    /// <summary>Apply a mutation, persist, and raise ConfigChanged.</summary>
    public async Task UpdateAsync(Action<AeroDialConfig> mutation)
    {
        mutation(Current);
        await SaveAsync();
        ConfigChanged?.Invoke();
    }

    /// <summary>Returns the active menu for the given foreground process name.</summary>
    public RadialMenuConfig GetActiveMenu(string? processName = null)
    {
        if (processName is not null)
        {
            var profile = Current.AppProfiles.FirstOrDefault(p =>
                string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase));

            if (profile is not null)
            {
                var menu = Current.Menus.FirstOrDefault(m => m.Id == profile.MenuId);
                if (menu is not null) return menu;
            }
        }

        return Current.Menus.FirstOrDefault(m => m.Id == Current.ActiveMenuId)
               ?? Current.Menus.First();
    }

    /// <summary>Resolve a submenu by id.</summary>
    public RadialMenuConfig? GetMenu(string id)
        => Current.Menus.FirstOrDefault(m => m.Id == id);
}
