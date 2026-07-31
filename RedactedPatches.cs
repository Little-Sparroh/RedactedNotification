using HarmonyLib;

[HarmonyPatch]
public static class RedactedPatches
{
    [HarmonyPatch(typeof(MissionSelectButton), nameof(MissionSelectButton.Setup), typeof(MissionData),
        typeof(MissionSelectWindow), typeof(bool), typeof(bool), typeof(bool))]
    [HarmonyPostfix]
    private static void MissionSelectButton_Setup_Postfix(MissionSelectButton __instance)
    {
        try
        {
            if (RedactedNotificationPlugin.Scanner == null || __instance == null)
                return;

            ref var mission = ref __instance.Mission;
            RedactedNotificationPlugin.Scanner.ObserveMission(ref mission);
        }
        catch
        {
        }
    }

    [HarmonyPatch(typeof(MissionSelectWindow), "Setup")]
    [HarmonyPostfix]
    private static void MissionSelectWindow_Setup_Postfix()
    {
        try
        {
            RedactedNotificationPlugin.Scanner?.ForceRescan();
        }
        catch
        {
        }
    }
}