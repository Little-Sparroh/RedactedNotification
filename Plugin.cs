using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("sparroh.uilibrary")]
[MycoMod(null, ModFlags.IsClientSide)]
public class SparrohPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "sparroh.redactednotification";
    public const string PluginName = "RedactedNotification";
    public const string PluginVersion = "1.0.1";

    internal static new ManualLogSource Logger;

    internal static RedactedBoardScanner Scanner { get; private set; }

    private Harmony _harmony;
    private RedactedHUD _hud;

    private void Awake()
    {
        Logger = base.Logger;

        try
        {
            _harmony = new Harmony(PluginGUID);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create Harmony instance: {ex.Message}");
            return;
        }

        var configFile = Config;
        try
        {
            var watcher = new FileSystemWatcher(Paths.ConfigPath, "sparroh.redactednotification.cfg");
            watcher.Changed += (s, e) =>
            {
                try { configFile.Reload(); }
                catch { /* ignore reload races */ }
            };
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to set up config watcher: {ex.Message}");
        }

        try
        {
            Scanner = new RedactedBoardScanner();
            _hud = new RedactedHUD(configFile, Scanner);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize RedactedNotification: {ex.Message}");
        }

        try
        {
            _harmony.PatchAll();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to apply Harmony patches: {ex.Message}");
        }

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded.");
    }

    private void Update()
    {
        try
        {
            Scanner?.Update();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in RedactedBoardScanner.Update(): {ex.Message}");
        }

        try
        {
            _hud?.UpdateHudVisibility();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in RedactedHUD.UpdateHudVisibility(): {ex.Message}");
        }

        try
        {
            _hud?.Update();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in RedactedHUD.Update(): {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        try
        {
            _hud?.OnDestroy();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in RedactedHUD.OnDestroy(): {ex.Message}");
        }

        try
        {
            _harmony?.UnpatchSelf();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error unpatching Harmony: {ex.Message}");
        }
    }
}
