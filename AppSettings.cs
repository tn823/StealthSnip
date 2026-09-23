using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace StealthSnip;

public class AppSettings
{
    private const string AppName = "StealthSnip";
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool ShowPreviewPopup { get; set; } = true;
    public bool OpenEditorAfterSnip { get; set; } = false;
    public bool AutoSave { get; set; } = false;
    public string SaveDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Screenshots");
    public bool PlaySound { get; set; } = false;
    public bool ShowNotification { get; set; } = false;
    public bool StartWithWindows { get; set; } = true;

    private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppSettings Load()
    {
        AppSettings settings = new AppSettings();
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    settings = loaded;
                }
            }
        }
        catch
        {
            // Fallback to default
        }

        // Sync with actual registry value or auto-register if enabled by default
        bool inRegistry = IsStartupWithWindowsEnabled();
        if (settings.StartWithWindows && !inRegistry)
        {
            settings.SetStartWithWindows(true);
        }
        else
        {
            settings.StartWithWindows = inRegistry;
        }

        return settings;
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch
        {
            // Ignore write errors
        }
    }

    public void SetStartWithWindows(bool enable)
    {
        StartWithWindows = enable;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key != null)
            {
                if (enable)
                {
                    string? exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }
        catch
        {
            // Registry access error
        }
        Save();
    }

    private static bool IsStartupWithWindowsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }
}
