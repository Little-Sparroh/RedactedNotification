using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("sparroh.uilibrary")]
[MycoMod(null, ModFlags.IsClientSide)]
public class RedactedNotificationPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "sparroh.redactednotification";
    public const string PluginName = "RedactedNotification";
    public const string PluginVersion = "1.0.2";

    internal new static ManualLogSource Logger;

    private Harmony _harmony;
    private RedactedHUD _hud;

    internal static RedactedBoardScanner Scanner { get; private set; }

    private void Awake()
    {
        Logger = base.Logger;

        ConfigManager.Initialize(Config, Logger);

        try
        {
            _harmony = new Harmony(PluginGUID);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create Harmony instance: {ex.Message}");
            return;
        }

        try
        {
            Scanner = new RedactedBoardScanner();
            _hud = new RedactedHUD(Scanner);
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
        ConfigManager.Tick();

        if (ConfigManager.ConsumePendingRefresh())
            try
            {
                _hud?.OnConfigChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in RedactedHUD.OnConfigChanged(): {ex.Message}");
            }

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

        ConfigManager.Dispose();

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