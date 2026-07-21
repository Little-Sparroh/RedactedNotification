using System;
using Pigeon.Math;
using UnityEngine;

/// <summary>
/// Detects whether ERROR REDACTED is present on the current mission board.
/// Mirrors MissionSelectWindow board generation so it works mid-mission when the board refreshes.
/// </summary>
public sealed class RedactedBoardScanner
{
    private int _cachedSeed = int.MinValue;
    private bool _hasRedactedOnBoard;
    private int _redactedIndex = -1;
    private MissionModifier _redactedModifier;
    private bool _resolveAttempted;
    private int _matchCount;

    public bool HasRedactedOnBoard => _hasRedactedOnBoard;
    public int MatchCount => _matchCount;
    public MissionModifier RedactedModifier => _redactedModifier;
    public int RedactedIndex => _redactedIndex;
    public int LastScannedSeed => _cachedSeed;

    public bool IsResolved => _redactedIndex >= 0 && _redactedModifier != null;

    /// <summary>
    /// Poll seed and rescan when the mission board rotates.
    /// </summary>
    public void Update()
    {
        try
        {
            if (Global.Instance == null)
                return;

            if (!IsResolved && !TryResolveRedacted())
                return;

            int seed = Global.MissionSelectSeed;
            if (seed != _cachedSeed)
            {
                _cachedSeed = seed;
                RescanBoard(seed);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"RedactedBoardScanner.Update failed: {ex.Message}");
        }
    }

    public void ForceRescan()
    {
        if (Global.Instance == null)
            return;

        if (!IsResolved && !TryResolveRedacted())
            return;

        _cachedSeed = Global.MissionSelectSeed;
        RescanBoard(_cachedSeed);
    }

    /// <summary>
    /// Live feed from mission select UI (more accurate for special/custom buttons).
    /// Call when a mission button is set up; does not replace seed-based scanning.
    /// </summary>
    public void ObserveMission(ref MissionData mission)
    {
        if (!IsResolved && !TryResolveRedacted())
            return;

        if (MissionHasRedacted(ref mission))
        {
            if (!_hasRedactedOnBoard)
            {
                _hasRedactedOnBoard = true;
                SparrohPlugin.Logger.LogInfo("ERROR REDACTED observed on mission select board.");
            }
        }
    }

    public bool TryResolveRedacted()
    {
        if (IsResolved)
            return true;

        if (Global.Instance == null || Global.Instance.MissionModifiers == null)
            return false;

        // Avoid spamming resolve every frame before Global is fully ready
        if (_resolveAttempted && _redactedIndex < 0)
        {
            // Retry occasionally: modifiers array may populate after first frame
        }

        _resolveAttempted = true;
        var modifiers = Global.Instance.MissionModifiers;
        int length = modifiers.Length;

        for (int i = 0; i < length; i++)
        {
            MissionModifier mod = modifiers[i];
            if (mod == null)
                continue;

            if (IsErrorRedacted(mod))
            {
                _redactedIndex = i;
                _redactedModifier = mod;
                SparrohPlugin.Logger.LogInfo(
                    $"Resolved ERROR REDACTED modifier at index {i} (APIName='{mod.APIName}', Name='{SafeName(mod)}').");
                return true;
            }
        }

        // Fallback: scan by localized name / API without requiring exact class
        for (int i = 0; i < length; i++)
        {
            MissionModifier mod = modifiers[i];
            if (mod == null)
                continue;

            string api = mod.APIName ?? string.Empty;
            string display = SafeName(mod);
            string combined = (api + " " + display).ToLowerInvariant();
            if (combined.Contains("redact") || combined.Contains("error redacted") || combined.Contains("error_redacted"))
            {
                _redactedIndex = i;
                _redactedModifier = mod;
                SparrohPlugin.Logger.LogInfo(
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

        string api = mod.APIName ?? string.Empty;
        string apiLower = api.ToLowerInvariant();

        // Common asset id patterns
        if (apiLower.Contains("error_redacted") ||
            apiLower.Contains("error-redacted") ||
            apiLower == "redacted" ||
            apiLower.Contains("redacted"))
        {
            // Prefer names that also imply ERROR, but accept pure "redacted" asset ids
            if (apiLower.Contains("redact"))
                return true;
        }

        try
        {
            string name = mod.Name ?? string.Empty;
            string nameLower = name.ToLowerInvariant();
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
        _hasRedactedOnBoard = false;
        _matchCount = 0;

        if (Global.Instance == null || Global.Instance.Regions == null)
            return;

        // Early game: intro incomplete hides the normal board
        try
        {
            Mission intro = Global.GetMission("intro");
            if (intro != null && PlayerData.Instance != null && !PlayerData.Instance.HasCompletedMission(intro))
            {
                SparrohPlugin.Logger.LogDebug("Board scan skipped: intro mission not completed.");
                return;
            }
        }
        catch
        {
            // If intro lookup fails, still scan
        }

        WorldRegion[] regions = Global.Instance.Regions;
        for (int r = 0; r < regions.Length; r++)
        {
            WorldRegion region = regions[r];
            if (region == null || region.LockRegion)
                continue;

            ScanRegion(region, currentSeed);
        }

        if (_hasRedactedOnBoard)
            SparrohPlugin.Logger.LogInfo($"ERROR REDACTED present on mission board (seed={currentSeed}, matches={_matchCount}).");
        else
            SparrohPlugin.Logger.LogDebug($"Board scan complete (seed={currentSeed}): no ERROR REDACTED.");
    }

    private void ScanRegion(WorldRegion region, int currentSeed)
    {
        const int missionCount = 8; // MissionSelectWindow.MissionCount

        // net48: avoid System.Span entirely (Mission.GetValidMissions takes ref Span<int>).
        int[] missionBag = new int[Global.Instance.Missions.Length];
        int missionBagCount = FillValidMissions(region.Flags, missionBag);

        Pigeon.Math.Random random = new Pigeon.Math.Random(
            MathUtil.Squirrel3Hash((region.ID + 3173412) * 71239192, currentSeed));

        bool[] usedMissions = new bool[Global.Instance.Missions.Length];

        for (int j = 0; j < missionCount; j++)
        {
            if (missionBagCount == 0)
                missionBagCount = FillValidMissions(region.Flags, missionBag);

            if (missionBagCount == 0)
                break;

            int missionSeed = random.Next() + 1;
            Pigeon.Math.Random missionRand = new Pigeon.Math.Random(MathUtil.Squirrel3Hash(missionSeed, currentSeed));

            // Last slot may force designed/legacy scenes (authored) — match MissionSelectWindow
            LevelFlags levelFlags = region.Flags;
            bool useLegacy = false;
            if (missionCount > 1 && j >= missionCount - 1 && (region.Flags & LevelFlags.Procedural) != 0)
            {
                SceneData[] legacyScenes = region.LegacyScenes;
                if (legacyScenes != null && legacyScenes.Length != 0)
                {
                    useLegacy = true;
                    levelFlags &= ~LevelFlags.Procedural;
                    for (int num4 = missionBagCount - 1; num4 >= 0; num4--)
                    {
                        if ((Global.Instance.Missions[missionBag[num4]].MissionFlags & MissionFlags.AllowInDesignedLevels) == 0)
                        {
                            missionBag[num4] = missionBag[missionBagCount - 1];
                            missionBagCount--;
                        }
                    }
                }
            }

            if (missionBagCount == 0)
                missionBagCount = FillValidMissions(levelFlags, missionBag);

            if (missionBagCount == 0)
                break;

            int bagIndex = missionRand.Next(missionBagCount);
            int missionIndex = missionBag[bagIndex];
            missionBag[bagIndex] = missionBag[missionBagCount - 1];
            missionBagCount--;
            usedMissions[missionIndex] = true;


            Mission mission = Global.Instance.Missions[missionIndex];
            if (mission == null)
                continue;

            SceneData[] scenes = useLegacy && region.LegacyScenes != null && region.LegacyScenes.Length > 0
                ? region.LegacyScenes
                : region.Scenes;


            if (scenes == null || scenes.Length == 0)
                continue;

            SceneData scene = scenes[missionRand.Next(scenes.Length)];
            var data = new MissionData(
                missionSeed,
                mission,
                region,
                scene,
                Global.Instance.DefaultMissionContainer,
                mission.GetAdditionalData());

            CheckAndCount(ref data);
        }

        // Always-show + hidden rare missions (same as MissionSelectWindow.SetupMissions tail)
        for (int k = 0; k < Global.Instance.Missions.Length; k++)
        {
            Mission mission = Global.Instance.Missions[k];
            if (mission == null)
                continue;

            if ((mission.MissionFlags & MissionFlags.NormalMission) == 0 && mission.ShowHiddenMissionInSelectScreen(region))
            {
                int missionSeed = random.Next() + 1;
                AddSpecialMission(mission, missionSeed, region, currentSeed, spawnInDesignedScene: false);
            }
            else if ((mission.MissionFlags & MissionFlags.AlwaysShowInMissionSelect) != MissionFlags.None && !usedMissions[k])
            {
                usedMissions[k] = true;
                int missionSeed = random.Next() + 1;
                bool designed = (mission.MissionFlags & MissionFlags.AllowInDesignedLevels) != MissionFlags.None
                    && random.NextFloat() <= 0.35f;
                AddSpecialMission(mission, missionSeed, region, currentSeed, designed);
            }
        }

        for (int l = 0; l < Global.Instance.Missions.Length; l++)
        {
            Mission mission = Global.Instance.Missions[l];
            if (mission == null)
                continue;

            if ((mission.MissionFlags & MissionFlags.DontShow) != MissionFlags.None)
            {
                float rareChance = mission.GetRareSpawnChance();
                if (random.NextFloat() < rareChance)
                {
                    int missionSeed = random.Next() + 1;
                    AddSpecialMission(mission, missionSeed, region, currentSeed, spawnInDesignedScene: false);
                }
            }
        }
    }

    private void AddSpecialMission(Mission mission, int missionSeed, WorldRegion region, int currentSeed, bool spawnInDesignedScene)
    {
        Pigeon.Math.Random missionRand = new Pigeon.Math.Random(MathUtil.Squirrel3Hash(missionSeed, currentSeed));
        SceneData[] scenes = region.Scenes;
        if (spawnInDesignedScene && (region.Flags & LevelFlags.Procedural) != 0)
        {
            SceneData[] legacy = region.LegacyScenes;
            if (legacy != null && legacy.Length != 0)
                scenes = legacy;
        }

        if (scenes == null || scenes.Length == 0)
            return;

        SceneData scene = scenes[missionRand.Next(scenes.Length)];
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

        _hasRedactedOnBoard = true;
        _matchCount++;
    }

    public bool MissionHasRedacted(ref MissionData data)
    {
        if (!IsResolved)
            return false;

        int count = data.GetModifierCount();
        if (count <= 0)
            return false;

        for (int i = 0; i < count; i++)
        {
            if (data.GetModifier(i) == _redactedIndex)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Array-based reimplementation of Mission.GetValidMissions (avoids Span on net48).
    /// </summary>
    private static int FillValidMissions(LevelFlags compatibleLevels, int[] indexBuffer, int onlyThisIndex = -1)
    {
        Mission[] missions = Global.Instance.Missions;
        int result = 0;
        for (int i = 0; i < missions.Length; i++)
        {
            if (onlyThisIndex >= 0)
            {
                indexBuffer[result++] = onlyThisIndex;
            }
            else if ((missions[i].MissionFlags & MissionFlags.DontShow) == 0)
            {
                bool flag = false;
                if ((missions[i].MissionFlags & MissionFlags.AllowInProceduralLevels) != MissionFlags.None
                    && (compatibleLevels & LevelFlags.Procedural) != 0)
                {
                    flag = true;
                }

                bool flag2 = false;
                if ((missions[i].MissionFlags & MissionFlags.AllowInDesignedLevels) != MissionFlags.None
                    && (compatibleLevels & LevelFlags.Procedural) == 0)
                {
                    flag2 = true;
                }

                if ((flag || flag2)
                    && missions[i].CanBeSelected()
                    && (missions[i].CompatibleLevels & LevelFlags.AllRegions & compatibleLevels) != 0
                    && (missions[i].MissionFlags & MissionFlags.SecretMission) == 0)
                {
                    indexBuffer[result++] = i;
                }
            }
        }

        return result;
    }
}


