using System;
using Pigeon.Math;
using Random = Pigeon.Math.Random;

public sealed class RedactedBoardScanner
{
    private bool _resolveAttempted;

    public bool HasRedactedOnBoard { get; private set; }

    public int MatchCount { get; private set; }

    public MissionModifier RedactedModifier { get; private set; }

    public int RedactedIndex { get; private set; } = -1;

    public int LastScannedSeed { get; private set; } = int.MinValue;

    public bool IsResolved => RedactedIndex >= 0 && RedactedModifier != null;


    public void Update()
    {
        try
        {
            if (Global.Instance == null)
                return;

            if (!IsResolved && !TryResolveRedacted())
                return;

            var seed = Global.MissionSelectSeed;
            if (seed != LastScannedSeed)
            {
                LastScannedSeed = seed;
                RescanBoard(seed);
            }
        }
        catch (Exception ex)
        {
            RedactedNotificationPlugin.Logger.LogError($"RedactedBoardScanner.Update failed: {ex.Message}");
        }
    }

    public void ForceRescan()
    {
        if (Global.Instance == null)
            return;

        if (!IsResolved && !TryResolveRedacted())
            return;

        LastScannedSeed = Global.MissionSelectSeed;
        RescanBoard(LastScannedSeed);
    }


    public void ObserveMission(ref MissionData mission)
    {
        if (!IsResolved && !TryResolveRedacted())
            return;

        if (MissionHasRedacted(ref mission))
            if (!HasRedactedOnBoard)
            {
                HasRedactedOnBoard = true;
                RedactedNotificationPlugin.Logger.LogInfo("ERROR REDACTED observed on mission select board.");
            }
    }

    public bool TryResolveRedacted()
    {
        if (IsResolved)
            return true;

        if (Global.Instance == null || Global.Instance.MissionModifiers == null)
            return false;


        if (_resolveAttempted && RedactedIndex < 0)
        {
        }

        _resolveAttempted = true;
        var modifiers = Global.Instance.MissionModifiers;
        var length = modifiers.Length;

        for (var i = 0; i < length; i++)
        {
            var mod = modifiers[i];
            if (mod == null)
                continue;

            if (IsErrorRedacted(mod))
            {
                RedactedIndex = i;
                RedactedModifier = mod;
                RedactedNotificationPlugin.Logger.LogInfo(
                    $"Resolved ERROR REDACTED modifier at index {i} (APIName='{mod.APIName}', Name='{SafeName(mod)}').");
                return true;
            }
        }


        for (var i = 0; i < length; i++)
        {
            var mod = modifiers[i];
            if (mod == null)
                continue;

            var api = mod.APIName ?? string.Empty;
            var display = SafeName(mod);
            var combined = (api + " " + display).ToLowerInvariant();
            if (combined.Contains("redact") || combined.Contains("error redacted") ||
                combined.Contains("error_redacted"))
            {
                RedactedIndex = i;
                RedactedModifier = mod;
                RedactedNotificationPlugin.Logger.LogInfo(
                    $"Resolved ERROR REDACTED modifier (fallback) at index {i} (APIName='{api}').");
                return true;
            }
        }

        return false;
    }

    private static string SafeName(MissionModifier mod)
    {
        try
        {
            return mod?.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }


    private static bool IsErrorRedacted(MissionModifier mod)
    {
        if (mod == null)
            return false;

        var api = mod.APIName ?? string.Empty;
        var apiLower = api.ToLowerInvariant();


        if (apiLower.Contains("error_redacted") ||
            apiLower.Contains("error-redacted") ||
            apiLower == "redacted" ||
            apiLower.Contains("redacted"))
            if (apiLower.Contains("redact"))
                return true;

        try
        {
            var name = mod.Name ?? string.Empty;
            var nameLower = name.ToLowerInvariant();
            if (nameLower.Contains("error") && nameLower.Contains("redact"))
                return true;
            if (nameLower == "error redacted" || nameLower.Contains("error redacted"))
                return true;
        }
        catch
        {
        }

        return false;
    }

    private void RescanBoard(int currentSeed)
    {
        HasRedactedOnBoard = false;
        MatchCount = 0;

        if (Global.Instance == null || Global.Instance.Regions == null)
            return;


        try
        {
            var intro = Global.GetMission("intro");
            if (intro != null && PlayerData.Instance != null && !PlayerData.Instance.HasCompletedMission(intro))
            {
                RedactedNotificationPlugin.Logger.LogDebug("Board scan skipped: intro mission not completed.");
                return;
            }
        }
        catch
        {
        }

        var regions = Global.Instance.Regions;
        for (var r = 0; r < regions.Length; r++)
        {
            var region = regions[r];
            if (region == null || region.LockRegion)
                continue;

            ScanRegion(region, currentSeed);
        }

        if (HasRedactedOnBoard)
            RedactedNotificationPlugin.Logger.LogInfo(
                $"ERROR REDACTED present on mission board (seed={currentSeed}, matches={MatchCount}).");
        else
            RedactedNotificationPlugin.Logger.LogDebug($"Board scan complete (seed={currentSeed}): no ERROR REDACTED.");
    }

    private void ScanRegion(WorldRegion region, int currentSeed)
    {
        const int missionCount = 8;


        var missionBag = new int[Global.Instance.Missions.Length];
        var missionBagCount = FillValidMissions(region.Flags, missionBag);

        var random = new Random(
            MathUtil.Squirrel3Hash((region.ID + 3173412) * 71239192, currentSeed));

        var usedMissions = new bool[Global.Instance.Missions.Length];

        for (var j = 0; j < missionCount; j++)
        {
            if (missionBagCount == 0)
                missionBagCount = FillValidMissions(region.Flags, missionBag);

            if (missionBagCount == 0)
                break;

            var missionSeed = random.Next() + 1;
            var missionRand = new Random(MathUtil.Squirrel3Hash(missionSeed, currentSeed));


            var levelFlags = region.Flags;
            var useLegacy = false;
            if (missionCount > 1 && j >= missionCount - 1 && (region.Flags & LevelFlags.Procedural) != 0)
            {
                var legacyScenes = region.LegacyScenes;
                if (legacyScenes != null && legacyScenes.Length != 0)
                {
                    useLegacy = true;
                    levelFlags &= ~LevelFlags.Procedural;
                    for (var num4 = missionBagCount - 1; num4 >= 0; num4--)
                        if ((Global.Instance.Missions[missionBag[num4]].MissionFlags &
                             MissionFlags.AllowInDesignedLevels) == 0)
                        {
                            missionBag[num4] = missionBag[missionBagCount - 1];
                            missionBagCount--;
                        }
                }
            }

            if (missionBagCount == 0)
                missionBagCount = FillValidMissions(levelFlags, missionBag);

            if (missionBagCount == 0)
                break;

            var bagIndex = missionRand.Next(missionBagCount);
            var missionIndex = missionBag[bagIndex];
            missionBag[bagIndex] = missionBag[missionBagCount - 1];
            missionBagCount--;
            usedMissions[missionIndex] = true;


            var mission = Global.Instance.Missions[missionIndex];
            if (mission == null)
                continue;

            var scenes = useLegacy && region.LegacyScenes != null && region.LegacyScenes.Length > 0
                ? region.LegacyScenes
                : region.Scenes;


            if (scenes == null || scenes.Length == 0)
                continue;

            var scene = scenes[missionRand.Next(scenes.Length)];
            var data = new MissionData(
                missionSeed,
                mission,
                region,
                scene,
                Global.Instance.DefaultMissionContainer,
                mission.GetAdditionalData());

            CheckAndCount(ref data);
        }


        for (var k = 0; k < Global.Instance.Missions.Length; k++)
        {
            var mission = Global.Instance.Missions[k];
            if (mission == null)
                continue;

            if ((mission.MissionFlags & MissionFlags.NormalMission) == 0 &&
                mission.ShowHiddenMissionInSelectScreen(region))
            {
                var missionSeed = random.Next() + 1;
                AddSpecialMission(mission, missionSeed, region, currentSeed, false);
            }
            else if ((mission.MissionFlags & MissionFlags.AlwaysShowInMissionSelect) != MissionFlags.None &&
                     !usedMissions[k])
            {
                usedMissions[k] = true;
                var missionSeed = random.Next() + 1;
                var designed = (mission.MissionFlags & MissionFlags.AllowInDesignedLevels) != MissionFlags.None
                               && random.NextFloat() <= 0.35f;
                AddSpecialMission(mission, missionSeed, region, currentSeed, designed);
            }
        }

        for (var l = 0; l < Global.Instance.Missions.Length; l++)
        {
            var mission = Global.Instance.Missions[l];
            if (mission == null)
                continue;

            if ((mission.MissionFlags & MissionFlags.DontShow) != MissionFlags.None)
            {
                var rareChance = mission.GetRareSpawnChance();
                if (random.NextFloat() < rareChance)
                {
                    var missionSeed = random.Next() + 1;
                    AddSpecialMission(mission, missionSeed, region, currentSeed, false);
                }
            }
        }
    }

    private void AddSpecialMission(Mission mission, int missionSeed, WorldRegion region, int currentSeed,
        bool spawnInDesignedScene)
    {
        var missionRand = new Random(MathUtil.Squirrel3Hash(missionSeed, currentSeed));
        var scenes = region.Scenes;
        if (spawnInDesignedScene && (region.Flags & LevelFlags.Procedural) != 0)
        {
            var legacy = region.LegacyScenes;
            if (legacy != null && legacy.Length != 0)
                scenes = legacy;
        }

        if (scenes == null || scenes.Length == 0)
            return;

        var scene = scenes[missionRand.Next(scenes.Length)];
        var data = new MissionData(
            missionSeed,
            mission,
            region,
            scene,
            Global.Instance.DefaultMissionContainer,
            mission.GetAdditionalData());

        CheckAndCount(ref data);
    }

    private void CheckAndCount(ref MissionData data)
    {
        if (!MissionHasRedacted(ref data))
            return;

        HasRedactedOnBoard = true;
        MatchCount++;
    }

    public bool MissionHasRedacted(ref MissionData data)
    {
        if (!IsResolved)
            return false;

        var count = data.GetModifierCount();
        if (count <= 0)
            return false;

        for (var i = 0; i < count; i++)
            if (data.GetModifier(i) == RedactedIndex)
                return true;

        return false;
    }


    private static int FillValidMissions(LevelFlags compatibleLevels, int[] indexBuffer, int onlyThisIndex = -1)
    {
        var missions = Global.Instance.Missions;
        var result = 0;
        for (var i = 0; i < missions.Length; i++)
            if (onlyThisIndex >= 0)
            {
                indexBuffer[result++] = onlyThisIndex;
            }
            else if ((missions[i].MissionFlags & MissionFlags.DontShow) == 0)
            {
                var flag = false;
                if ((missions[i].MissionFlags & MissionFlags.AllowInProceduralLevels) != MissionFlags.None
                    && (compatibleLevels & LevelFlags.Procedural) != 0)
                    flag = true;

                var flag2 = false;
                if ((missions[i].MissionFlags & MissionFlags.AllowInDesignedLevels) != MissionFlags.None
                    && (compatibleLevels & LevelFlags.Procedural) == 0)
                    flag2 = true;

                if ((flag || flag2)
                    && missions[i].CanBeSelected()
                    && (missions[i].CompatibleLevels & LevelFlags.AllRegions & compatibleLevels) != 0
                    && (missions[i].MissionFlags & MissionFlags.SecretMission) == 0)
                    indexBuffer[result++] = i;
            }

        return result;
    }
}