using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Sparroh.UI;
using UnityEngine;

public static class ConfigManager
{
    private const float DebounceSeconds = 0.25f;

    private static ConfigFile config;
    private static ManualLogSource logger;
    private static FileSystemWatcher configWatcher;
    private static volatile bool pendingRefresh;
    private static volatile bool reloadPending;
    private static float lastReloadTime;
    public static ConfigEntry<bool> EnableHud { get; private set; }
    public static ConfigEntry<bool> ShowWhenNotDetected { get; private set; }
    public static ConfigEntry<string> AlertText { get; private set; }
    public static ConfigEntry<string> IdleText { get; private set; }
    public static ConfigEntry<bool> EnableBlink { get; private set; }
    public static ConfigEntry<float> BlinkSpeed { get; private set; }
    public static ConfigEntry<float> BlinkMinAlpha { get; private set; }
    public static HudAnchors Anchors { get; private set; }

    public static void Initialize(ConfigFile configFile, ManualLogSource log)
    {
        config = configFile;
        logger = log;

        EnableHud = config.Bind(
            "General",
            "Enable Notification",
            true,
            "Show the ERROR REDACTED board notification on the player HUD.");

        ShowWhenNotDetected = config.Bind(
            "General",
            "Show When Not Detected",
            false,
            "When enabled, always show a grey idle indicator (\"no ERROR detected.\") when ERROR REDACTED is not on the board. When disabled, the HUD is hidden until the modifier appears.");

        AlertText = config.Bind(
            "General",
            "Alert Text",
            "ERROR REDACTED has appeared!",
            "Text shown when ERROR REDACTED is on the current mission board.");

        IdleText = config.Bind(
            "General",
            "Idle Text",
            "no ERROR detected.",
            "Text shown in idle mode when ERROR REDACTED is not on the board.");

        EnableBlink = config.Bind(
            "General",
            "Enable Blink",
            true,
            "Pulse the alert HUD when ERROR REDACTED is on the board so it is easier to notice.");

        BlinkSpeed = config.Bind(
            "General",
            "Blink Speed",
            2.0f,
            "Blink rate in full cycles per second (soft alpha pulse). Reasonable range: 1–3.");

        BlinkMinAlpha = config.Bind(
            "General",
            "Blink Min Alpha",
            0.35f,
            "Minimum alpha during the dim phase of the blink (0–1). Higher = subtler blink.");

        Anchors = HudAnchors.Bind(config, "Notification", 0.50f, 0.12f);

        EnableHud.SettingChanged += OnSettingChanged;
        ShowWhenNotDetected.SettingChanged += OnSettingChanged;
        AlertText.SettingChanged += OnSettingChanged;
        IdleText.SettingChanged += OnSettingChanged;
        EnableBlink.SettingChanged += OnSettingChanged;
        BlinkSpeed.SettingChanged += OnSettingChanged;
        BlinkMinAlpha.SettingChanged += OnSettingChanged;

        try
        {
            SetupFileWatcher();
        }
        catch (Exception ex)
        {
            logger.LogError($"Error setting up config file watcher: {ex.Message}");
        }
    }


    public static void Tick()
    {
        if (!reloadPending)
            return;

        if (Time.unscaledTime - lastReloadTime < DebounceSeconds)
            return;

        reloadPending = false;
        lastReloadTime = Time.unscaledTime;

        try
        {
            config.Reload();
            pendingRefresh = true;
            logger.LogInfo("Config reloaded from disk.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error reloading config: {ex.Message}");
        }
    }

    public static bool ConsumePendingRefresh()
    {
        if (!pendingRefresh)
            return false;

        pendingRefresh = false;
        return true;
    }

    public static void Dispose()
    {
        if (EnableHud != null)
            EnableHud.SettingChanged -= OnSettingChanged;
        if (ShowWhenNotDetected != null)
            ShowWhenNotDetected.SettingChanged -= OnSettingChanged;
        if (AlertText != null)
            AlertText.SettingChanged -= OnSettingChanged;
        if (IdleText != null)
            IdleText.SettingChanged -= OnSettingChanged;
        if (EnableBlink != null)
            EnableBlink.SettingChanged -= OnSettingChanged;
        if (BlinkSpeed != null)
            BlinkSpeed.SettingChanged -= OnSettingChanged;
        if (BlinkMinAlpha != null)
            BlinkMinAlpha.SettingChanged -= OnSettingChanged;

        if (configWatcher != null)
        {
            configWatcher.EnableRaisingEvents = false;
            configWatcher.Changed -= OnConfigFileChanged;
            configWatcher.Created -= OnConfigFileChanged;
            configWatcher.Renamed -= OnConfigFileChanged;
            configWatcher.Dispose();
            configWatcher = null;
        }
    }

    private static void SetupFileWatcher()
    {
        configWatcher = new FileSystemWatcher(Paths.ConfigPath, $"{RedactedNotificationPlugin.PluginGUID}.cfg");
        configWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
        configWatcher.Changed += OnConfigFileChanged;
        configWatcher.Created += OnConfigFileChanged;
        configWatcher.Renamed += OnConfigFileChanged;
        configWatcher.EnableRaisingEvents = true;
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        reloadPending = true;
    }

    private static void OnSettingChanged(object sender, EventArgs e)
    {
        pendingRefresh = true;
    }
}