using HarmonyLib;

/// <summary>
/// Live observation of mission select buttons so special/custom missions
/// (not fully covered by seed reconstruction) still update the alert.
/// Primary detection remains seed-based board scanning in RedactedBoardScanner.
/// </summary>
[HarmonyPatch]
public static class RedactedPatches
{
    [HarmonyPatch(typeof(MissionSelectButton), nameof(MissionSelectButton.Setup),
        new[] { typeof(MissionData), typeof(MissionSelectWindow), typeof(bool), typeof(bool), typeof(bool) })]
    [HarmonyPostfix]
    private static void MissionSelectButton_Setup_Postfix(MissionSelectButton __instance)
    {
        try
        {
            if (SparrohPlugin.Scanner == null || __instance == null)
                return;

            ref MissionData mission = ref __instance.Mission;
            SparrohPlugin.Scanner.ObserveMission(ref mission);
        }
        catch
        {
            // Never break mission select UI
        }
    }

    [HarmonyPatch(typeof(MissionSelectWindow), "Setup")]
    [HarmonyPostfix]
    private static void MissionSelectWindow_Setup_Postfix()
    {
        try
        {
            // After the board is rebuilt, force a full seed scan so state stays consistent
            SparrohPlugin.Scanner?.ForceRescan();
        }
        catch
        {
        }
    }
}
