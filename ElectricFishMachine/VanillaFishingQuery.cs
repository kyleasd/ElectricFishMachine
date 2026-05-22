using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Tools;

namespace ElectricFishMachine
{
    /// <summary>
    /// 使用原版钓鱼 API：先读取浮漂格的钓区等信息，再调用 <see cref="GameLocation.getFish"/> 生成物品。
    /// </summary>
    internal static class VanillaFishingQuery
    {
        /// <summary>电鱼固定按最深离岸格数（与 <see cref="FishingRod.distanceToLand"/> 上限一致）。</summary>
        private const int ElectricFishWaterDepth = 5;

        /// <summary>该格支持蟹笼产出时，单次电鱼走蟹笼掉落表的概率（电鱼频率高，不宜每次都用蟹笼表）。</summary>
        private const float CrabPotLootMixChance = 0.25f;

        /// <summary>电鱼时传说鱼王独立池概率（四季鱼王 + 变种鲤鱼；绕过已钓过限制，可重复刷）。</summary>
        private const double LegendaryElectricFishChance = 0.05;

        /// <summary>下水道等，全季节（原版地点规则，非季节绑定）。</summary>
        private const string MutantCarpQualifiedId = "(O)682";

        private const int LuremasterProfessionId = 10;

        /// <summary>四季传说之鱼（春传说之鱼、夏绯红鱼、秋翻车鱼、冬冰川鱼）。</summary>
        private static readonly (Season Season, string QualifiedItemId)[] SeasonLegendaryFish =
        {
            (Season.Spring, "(O)163"),   // Legend
            (Season.Summer, "(O)159"),   // Crimsonfish
            (Season.Fall, "(O)160"),     // Angler
            (Season.Winter, "(O)775"),   // Glacierfish
        };

        /// <summary>某一水面格的原版钓鱼上下文。</summary>
        internal readonly struct BobberFishingInfo
        {
            public Vector2 BobberTile { get; init; }

            public int WaterDepth { get; init; }

            public Season SeasonForLocation { get; init; }

            public string? FishAreaId { get; init; }

            public string? FishAreaDisplayName { get; init; }

            /// <summary>按 <see cref="GameLocation.GetFishFromLocationData"/> 规则筛出的候选（未做概率掷骰）。</summary>
            public IReadOnlyList<string> PossibleQualifiedItemIds { get; init; }
        }

        /// <summary>浮漂格坐标（瓦片坐标，与 <see cref="GameLocation.getFish"/> 一致）。</summary>
        internal static Vector2 TileToBobber(Point tile) => new(tile.X, tile.Y);

        /// <summary>查询该格在原版逻辑下属于哪个钓区、水深与季节。</summary>
        internal static BobberFishingInfo QueryBobberTile(GameLocation location, Point tile, Farmer farmer)
        {
            Vector2 bobber = TileToBobber(tile);
            int waterDepth = GetWaterDepth();
            Season season = Game1.GetSeasonForLocation(location);

            string? fishAreaId = null;
            string? fishAreaDisplayName = null;
            if (location.TryGetFishAreaForTile(bobber, out string? areaId, out FishAreaData? _))
            {
                fishAreaId = areaId;
                fishAreaDisplayName = location.GetFishingAreaDisplayName(areaId);
            }

            IReadOnlyList<string> possibleRod = BuildPossibleQualifiedItemIds(
                location,
                bobber,
                waterDepth,
                farmer,
                FishCandidateQueryOptions.Default);
            IReadOnlyList<string> possibleCrabPot = BuildPossibleCrabPotQualifiedItemIds(location, bobber);
            var possible = possibleRod
                .Concat(possibleCrabPot)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new BobberFishingInfo
            {
                BobberTile = bobber,
                WaterDepth = waterDepth,
                SeasonForLocation = season,
                FishAreaId = fishAreaId,
                FishAreaDisplayName = fishAreaDisplayName,
                PossibleQualifiedItemIds = possible
            };
        }

        /// <summary>
        /// 在指定水面格生成物品：优先按概率走蟹笼表（<see cref="StardewValley.Objects.CrabPot"/> 同款规则），否则 <see cref="GameLocation.getFish"/>。
        /// </summary>
        internal static Item? RollCatchAtTile(GameLocation location, Point tile, Farmer farmer)
        {
            if (!location.isTileFishable(tile.X, tile.Y))
                return null;

            Vector2 bobber = TileToBobber(tile);

            if (Game1.random.NextDouble() < LegendaryElectricFishChance)
            {
                Item? legendary = TryRollLegendaryElectricFish(location, bobber, farmer);
                if (legendary != null)
                    return legendary;
            }

            if (HasCrabPotFishForTile(location, bobber) && Game1.random.NextDouble() < CrabPotLootMixChance)
            {
                Item? crabPotLoot = TryRollCrabPotCatch(location, bobber, farmer);
                if (crabPotLoot != null)
                    return crabPotLoot;
            }

            int waterDepth = GetWaterDepth();

            return location.getFish(
                millisecondsAfterNibble: Game1.random.Next(100, 500),
                bait: GetBaitQualifiedId(farmer),
                waterDepth: waterDepth,
                who: farmer,
                baitPotency: 0.0,
                bobberTile: bobber);
        }

        private static int GetWaterDepth() => ElectricFishWaterDepth;

        /// <summary>查询候选鱼表时的选项；鱼王 5% 池忽略捕获次数与 Data/Locations 概率，改由 <see cref="LegendaryElectricFishChance"/> 单独掷骰。</summary>
        private readonly struct FishCandidateQueryOptions
        {
            public bool IgnoreCatchLimit { get; init; }

            public bool IgnoreSpawnChance { get; init; }

            public static FishCandidateQueryOptions Default => default;

            public static FishCandidateQueryOptions ForLegendaryElectricFish { get; } = new()
            {
                IgnoreCatchLimit = true,
                IgnoreSpawnChance = true,
            };
        }

        /// <summary>
        /// 5% 独立概率：在该格原版可出的鱼王中随机一种（四季鱼王 + 变种鲤鱼；季节/天气/钓区等同 <see cref="BuildPossibleQualifiedItemIds"/>）。
        /// 不检查 <see cref="Farmer.fishCaught"/>，可重复刷。
        /// </summary>
        private static Item? TryRollLegendaryElectricFish(GameLocation location, Vector2 bobberTile, Farmer farmer)
        {
            var eligible = new List<string>(5);

            string? seasonalId = GetSeasonLegendaryQualifiedId(Game1.GetSeasonForLocation(location));
            if (seasonalId != null && IsLegendaryEligibleAtBobber(location, bobberTile, farmer, seasonalId))
                eligible.Add(seasonalId);

            if (IsLegendaryEligibleAtBobber(location, bobberTile, farmer, MutantCarpQualifiedId))
                eligible.Add(MutantCarpQualifiedId);

            if (eligible.Count == 0)
                return null;

            string picked = eligible[Game1.random.Next(eligible.Count)];
            Item item = ItemRegistry.Create(picked);
            item.SetTempData("IsBossFish", value: true);
            return item;
        }

        private static string? GetSeasonLegendaryQualifiedId(Season season)
        {
            foreach ((Season fishSeason, string qid) in SeasonLegendaryFish)
            {
                if (fishSeason == season)
                    return qid;
            }

            return null;
        }

        /// <summary>该格是否满足 Data/Locations + Data/Fish 对指定鱼王的全部条件（不含捕获次数、不含原版概率）。</summary>
        private static bool IsLegendaryEligibleAtBobber(
            GameLocation location,
            Vector2 bobberTile,
            Farmer farmer,
            string legendaryQualifiedId)
        {
            IReadOnlyList<string> candidates = BuildPossibleQualifiedItemIds(
                location,
                bobberTile,
                GetWaterDepth(),
                farmer,
                FishCandidateQueryOptions.ForLegendaryElectricFish);

            foreach (string qid in candidates)
            {
                if (string.Equals(qid, legendaryQualifiedId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string GetBaitQualifiedId(Farmer farmer)
        {
            if (farmer.CurrentTool is not FishingRod rod)
                return "";

            StardewValley.Object? bait = rod.GetBait();
            return bait?.QualifiedItemId ?? "";
        }

        /// <summary>
        /// 复刻 <see cref="GameLocation.GetFishFromLocationData"/> 的筛选条件，收集所有通过检查的候选（不做概率掷骰）。
        /// </summary>
        private static IReadOnlyList<string> BuildPossibleQualifiedItemIds(
            GameLocation location,
            Vector2 bobberTile,
            int waterDepth,
            Farmer farmer,
            FishCandidateQueryOptions options)
        {
            LocationData? locationData = location.GetData();
            Dictionary<string, string> allFishData = DataLoader.Fish(Game1.content);
            Season seasonForLocation = Game1.GetSeasonForLocation(location);

            string? fishAreaId = null;
            if (!location.TryGetFishAreaForTile(bobberTile, out fishAreaId, out _))
                fishAreaId = null;

            bool usingMagicBait = false;
            bool hasCuriosityLure = false;
            string? targetedBaitItemId = null;

            if (farmer.CurrentTool is FishingRod rod)
            {
                usingMagicBait = rod.HasMagicBait();
                hasCuriosityLure = rod.HasCuriosityLure();
                StardewValley.Object? bait = rod.GetBait();
                if (bait?.QualifiedItemId == "(O)SpecificBait" && bait.preservedParentSheetIndex.Value != null)
                    targetedBaitItemId = "(O)" + bait.preservedParentSheetIndex.Value;
            }

            bool isTutorialCatch = farmer.fishCaught.Length == 0;
            Point farmerTile = farmer.TilePoint;

            IEnumerable<SpawnFishData> spawnRules = Game1.locationData["Default"].Fish;
            if (locationData?.Fish is { Count: > 0 } locationFish)
                spawnRules = spawnRules.Concat(locationFish);

            spawnRules = spawnRules.OrderBy(p => p.Precedence);

            HashSet<string>? ignoreQueryKeys = usingMagicBait ? GameStateQuery.MagicBaitIgnoreQueryKeys : null;
            var itemQueryContext = new ItemQueryContext(location, farmer, Game1.random, "ElectricFishMachine > fish query");

            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (SpawnFishData spawn in spawnRules)
            {
                if (spawn.FishAreaId != null && fishAreaId != spawn.FishAreaId)
                    continue;

                if (spawn.Season.HasValue && !usingMagicBait && spawn.Season != seasonForLocation)
                    continue;

                if (spawn.PlayerPosition.HasValue && !spawn.PlayerPosition.Value.Contains(farmerTile.X, farmerTile.Y))
                    continue;

                if (spawn.BobberPosition.HasValue && !spawn.BobberPosition.Value.Contains((int)bobberTile.X, (int)bobberTile.Y))
                    continue;

                if (farmer.FishingLevel < spawn.MinFishingLevel)
                    continue;

                if (waterDepth < spawn.MinDistanceFromShore)
                    continue;

                if (spawn.MaxDistanceFromShore > -1 && waterDepth > spawn.MaxDistanceFromShore)
                    continue;

                if (spawn.RequireMagicBait && !usingMagicBait)
                    continue;

                if (!options.IgnoreSpawnChance)
                {
                    float chance = spawn.GetChance(
                        hasCuriosityLure,
                        farmer.DailyLuck,
                        farmer.LuckLevel,
                        (value, modifiers, mode) => Utility.ApplyQuantityModifiers(value, modifiers, mode, location),
                        spawn.ItemId == targetedBaitItemId);

                    if (chance <= 0f)
                        continue;
                }

                if (spawn.Condition != null && !GameStateQuery.CheckConditions(spawn.Condition, location, null, null, null, null, ignoreQueryKeys))
                    continue;

                Item? resolved = ItemQueryResolver.TryResolveRandomItem(
                    spawn,
                    itemQueryContext,
                    avoidRepeat: false,
                    null,
                    query => query
                        .Replace("BOBBER_X", ((int)bobberTile.X).ToString())
                        .Replace("BOBBER_Y", ((int)bobberTile.Y).ToString())
                        .Replace("WATER_DEPTH", waterDepth.ToString()),
                    null,
                    null);

                if (resolved == null)
                    continue;

                if (!options.IgnoreCatchLimit
                    && spawn.CatchLimit > -1
                    && farmer.fishCaught.TryGetValue(resolved.QualifiedItemId, out int[]? caught)
                    && caught[0] >= spawn.CatchLimit)
                    continue;

                bool usingTargetBait = targetedBaitItemId != null && spawn.ItemId == targetedBaitItemId;
                if (!InvokeCheckGenericFishRequirements(
                        resolved,
                        allFishData,
                        location,
                        farmer,
                        spawn,
                        waterDepth,
                        usingMagicBait,
                        hasCuriosityLure,
                        usingTargetBait,
                        isTutorialCatch))
                    continue;

                if (seen.Add(resolved.QualifiedItemId))
                    results.Add(resolved.QualifiedItemId);
            }

            return results;
        }

        private static bool HasCrabPotFishForTile(GameLocation location, Vector2 bobberTile)
        {
            IList<string> types = location.GetCrabPotFishForTile(bobberTile);
            return types != null && types.Count > 0;
        }

        /// <summary>该格蟹笼可出的物品（不含垃圾），供调试/verbose 显示。</summary>
        private static IReadOnlyList<string> BuildPossibleCrabPotQualifiedItemIds(GameLocation location, Vector2 bobberTile)
        {
            IList<string> crabPotFishTypes = location.GetCrabPotFishForTile(bobberTile);
            if (crabPotFishTypes == null || crabPotFishTypes.Count == 0)
                return Array.Empty<string>();

            var results = new List<string>();
            foreach (KeyValuePair<string, string> entry in DataLoader.Fish(Game1.content))
            {
                if (!entry.Value.Contains("trap", StringComparison.Ordinal))
                    continue;

                string[] fields = entry.Value.Split('/');
                if (fields.Length < 5 || fields[1] != "trap")
                    continue;

                string[] typeTags = ArgUtility.SplitBySpace(fields[4]);
                if (!typeTags.Any(type => crabPotFishTypes.Contains(type)))
                    continue;

                results.Add("(O)" + entry.Key);
            }

            return results;
        }

        /// <summary>复刻 <see cref="StardewValley.Objects.CrabPot.DayUpdate"/> 的产出逻辑（使用 <see cref="Game1.random"/> 而非当日固定种子）。</summary>
        private static Item? TryRollCrabPotCatch(GameLocation location, Vector2 bobberTile, Farmer farmer)
        {
            IList<string> crabPotFishTypes = location.GetCrabPotFishForTile(bobberTile);
            if (crabPotFishTypes == null || crabPotFishTypes.Count == 0)
                return null;

            bool luremaster = farmer.professions.Contains(LuremasterProfessionId);
            location.TryGetFishAreaForTile(bobberTile, out _, out FishAreaData? fishAreaData);

            double junkChance = luremaster ? 0.0 : fishAreaData?.CrabPotJunkChance ?? 0.2;

            int amount = 1;
            int quality = 0;
            string? targetedTrapFishId = null;

            if (farmer.CurrentTool is FishingRod rod)
            {
                StardewValley.Object? bait = rod.GetBait();
                switch (bait?.QualifiedItemId)
                {
                    case "(O)DeluxeBait":
                        quality = 1;
                        junkChance /= 2.0;
                        break;
                    case "(O)774":
                        junkChance /= 2.0;
                        if (Game1.random.NextDouble() < 0.25)
                            amount = 2;
                        break;
                    case "(O)SpecificBait":
                        if (bait.preservedParentSheetIndex.Value != null)
                        {
                            targetedTrapFishId = bait.preservedParentSheetIndex.Value;
                            junkChance /= 2.0;
                        }
                        break;
                }
            }

            var luremasterPool = new List<string>();

            if (Game1.random.NextDouble() >= junkChance)
            {
                foreach (KeyValuePair<string, string> entry in DataLoader.Fish(Game1.content))
                {
                    if (!entry.Value.Contains("trap", StringComparison.Ordinal))
                        continue;

                    string[] fields = entry.Value.Split('/');
                    if (fields.Length < 3 || fields[1] != "trap")
                        continue;

                    string[] typeTags = ArgUtility.SplitBySpace(fields[4]);
                    if (!typeTags.Any(type => crabPotFishTypes.Contains(type)))
                        continue;

                    if (luremaster)
                    {
                        luremasterPool.Add(entry.Key);
                        continue;
                    }

                    if (!double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double catchChance))
                        continue;

                    if (targetedTrapFishId != null && targetedTrapFishId == entry.Key)
                    {
                        catchChance *= catchChance < 0.1 ? 4 : catchChance < 0.2 ? 3 : 2;
                    }

                    if (Game1.random.NextDouble() < catchChance)
                        return ItemRegistry.Create("(O)" + entry.Key, amount, quality);
                }
            }

            if (luremaster && luremasterPool.Count > 0)
            {
                string picked = luremasterPool[Game1.random.Next(luremasterPool.Count)];
                return ItemRegistry.Create("(O)" + picked, amount, quality);
            }

            return ItemRegistry.Create("(O)" + Game1.random.Next(168, 173).ToString(), amount, quality);
        }

        private static MethodInfo? _checkGenericFishRequirements;

        private static bool InvokeCheckGenericFishRequirements(
            Item fish,
            Dictionary<string, string> allFishData,
            GameLocation location,
            Farmer player,
            SpawnFishData spawn,
            int waterDepth,
            bool usingMagicBait,
            bool hasCuriosityLure,
            bool usingTargetBait,
            bool isTutorialCatch)
        {
            _checkGenericFishRequirements ??= typeof(GameLocation).GetMethod(
                "CheckGenericFishRequirements",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (_checkGenericFishRequirements == null)
                return true;

            object? result = _checkGenericFishRequirements.Invoke(
                null,
                new object[]
                {
                    fish,
                    allFishData,
                    location,
                    player,
                    spawn,
                    waterDepth,
                    usingMagicBait,
                    hasCuriosityLure,
                    usingTargetBait,
                    isTutorialCatch
                });

            return result is bool ok && ok;
        }
    }
}
