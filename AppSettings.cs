using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace StealthSnip;

public class AppSettings
{
    private const string AppName = "StealthSnip";
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool OpenEditorAfterSnip { get; set; } = true;
    public bool AutoSave { get; set; } = false;
    public string SaveDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Screenshots");
    public bool PlaySound { get; set; } = false;
    public bool ShowNotification { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;

    private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    // Sync with actual registry value
                    settings.StartWithWindows = IsStartupWithWindowsEnabled();
                    return settings;
                }
            }
        }
        catch
        {
            // Fallback to default
        }

        var def = new AppSettings();
        def.StartWithWindows = IsStartupWithWindowsEnabled();
        return def;
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
