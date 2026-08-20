using System.IO;
using System.Text.Json;
using System.Windows;
using Naibao.Models;

namespace Naibao.Services;

public static class ConfigService
{
    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "naibao");

    public static string ConfigFile => Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null)
                {
                    return Sanitize(cfg);
                }
            }
        }
        catch
        {
            // 配置损坏时回退到默认配置，不打扰用户。
        }

        return new AppConfig();
    }

    public static void Save(AppConfig cfg)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var tmp = ConfigFile + ".tmp";
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tmp, json);
            File.Move(tmp, ConfigFile, true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"配置保存失败：{ex.Message}", "naibao",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static AppConfig Sanitize(AppConfig c)
    {
        if (c.DisplayMode is not ("topmost" or "tray")) c.DisplayMode = "topmost";
        if (c.StartupMode is not ("topmost" or "tray")) c.StartupMode = "topmost";
        if (c.ChimeMode is not ("sound_message" or "message_only" or "full_mute")) c.ChimeMode = "sound_message";
        c.PetSize = c.PetSize is >= 80 and <= 400 ? c.PetSize : 160;
        c.Volume = c.Volume is >= 0 and <= 1 ? c.Volume : 0.8;
        c.WebLinks ??= new List<WebLinkItem>();
        c.WebLinks = c.WebLinks
            .Where(l => !string.IsNullOrWhiteSpace(l.Name) && WebLinkService.IsValidUrl(l.Url))
            .ToList();

        c.KungFuMinMinutes = c.KungFuMinMinutes is >= 0.1 and <= 120 ? c.KungFuMinMinutes : 2;
        c.KungFuMaxMinutes = c.KungFuMaxMinutes is >= 0.1 and <= 120 ? c.KungFuMaxMinutes : 5;
        if (c.KungFuMaxMinutes < c.KungFuMinMinutes)
        {
            (c.KungFuMinMinutes, c.KungFuMaxMinutes) = (c.KungFuMaxMinutes, c.KungFuMinMinutes);
        }

        c.IdleSleepSeconds = c.IdleSleepSeconds is >= 10 and <= 86400 ? c.IdleSleepSeconds : 300;
        return c;
    }
}
