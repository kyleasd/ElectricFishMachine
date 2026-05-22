using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using StardewValley.Tools;

namespace ElectricFishMachine
{
    public class ModEntry : Mod
    {
        private ModConfig _config = null!;

        private Random random = new Random();

        private readonly List<Point> _fishableTilesBuffer = new();

        /// <summary>离开水边或停用后重置；在水边持续使用时每满一轮间隔扣 1 份电池。</summary>
        private int _electricBatteryDrainCooldownRemaining = ElectricBatteryDrainIntervalFrames;

        /// <summary>在水边且满足电鱼条件时，每隔多少帧尝试扣除 1 个电池组（约 60 帧 = 1 秒）。</summary>
        private const int ElectricBatteryDrainIntervalFrames = 180;

        /// <summary>在水边电鱼激活时，每隔多少帧受到一次电击伤害（约 60 帧 ≈ 1 秒 @60fps）。</summary>
        private const int ElectricFishHealthDrainIntervalFrames = 120;

        /// <summary>每次电击扣除的生命值（原版生命归零会晕倒）。</summary>
        private const int ElectricFishHealthDamagePerPulse = 8;

        /// <summary>离开电鱼资格时重置；在水边持续使用时递减。</summary>
        private int _electricHealthDrainCooldownRemaining = ElectricFishHealthDrainIntervalFrames;

        internal ModConfig Config => _config;

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<ModConfig>();

            CustomToolData.Register(helper);
            ElectricFishMachineRecipeAndShop.Register(helper);

            Harmony harmony = new(ModManifest.UniqueID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;

            helper.ConsoleCommands.Add(
                "efm_give_core",
                "把「电鱼机」放进背包（测试用）。加载存档后在 SMAPI 控制台输入：efm_give_core",
                (_, _) =>
                {
                    if (!Context.IsWorldReady)
                    {
                        Monitor.Log("请先加载存档再使用该命令。", LogLevel.Warn);
                        return;
                    }

                    Item? item = ItemRegistry.Create("(T)" + CustomToolData.ToolId);
                    if (item == null)
                    {
                        Monitor.Log("创建工具失败。", LogLevel.Error);
                        return;
                    }

                    Game1.player.addItemByMenuIfNecessaryElseHoldUp(item);
                    Monitor.Log("已获得「电鱼机」。", LogLevel.Info);
                });

            helper.ConsoleCommands.Add(
                "efm_set_fishing_level",
                "测试用：同步设置钓鱼等级与经验（0–10）。例：efm_set_fishing_level 1",
                OnConsoleSetFishingLevel);

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            ModConfigMenuIntegration.Register(this);
        }

        internal void ResetConfigToDefault()
        {
            _config = new ModConfig();
        }

        internal void SaveConfigToFile()
        {
            Helper.WriteConfig(_config);
        }

        private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            foreach (Item? item in Game1.player.Items)
            {
                if (item is FishingRod rod)
                    CustomToolData.MigrateElectricRodBatteryFromLegacySecondSlot(rod);
            }
        }

        /// <summary>控制台：efm_set_fishing_level &lt;0-10&gt;，同步钓鱼经验/等级并清钓鱼专精（测试用）。</summary>
        private void OnConsoleSetFishingLevel(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("请先加载存档再使用该命令。", LogLevel.Warn);
                return;
            }

            Farmer who = Game1.player;
            if (!who.IsLocalPlayer)
            {
                Monitor.Log("仅本地玩家可用。", LogLevel.Warn);
                return;
            }

            if (args.Length == 0 || !int.TryParse(args[0].Trim(), out int level) || level < 0 || level > 10)
            {
                Monitor.Log("用法：efm_set_fishing_level <0-10>  例：efm_set_fishing_level 1", LogLevel.Warn);
                return;
            }

            int xp = level <= 0 ? 0 : Farmer.getBaseExperienceForLevel(level);
            who.experiencePoints[Farmer.fishingSkill] = xp;
            who.fishingLevel.Value = level;

            foreach (int p in new[] { 6, 7, 8, 9, 10, 11 })
                who.professions.Remove(p);

            for (int i = who.newLevels.Count - 1; i >= 0; i--)
            {
                if (who.newLevels[i].X == Farmer.fishingSkill)
                    who.newLevels.RemoveAt(i);
            }

            Monitor.Log($"钓鱼已设为 {level} 级（经验 {xp}，已清除钓鱼专精与待弹升级条）。", LogLevel.Info);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree)
                return;

            if (Game1.paused)
                return;

            Farmer player = Game1.player;
            GameLocation loc = player.currentLocation;

            bool canElectric = CustomToolData.PlayerCanElectricFish(player);
            bool activeElectric = CustomToolData.IsPlayerActivelyElectricFishing(player);

            ElectricFishBatteryHud.IntervalFrames = ElectricBatteryDrainIntervalFrames;

            // 只有失去「电鱼资格」（换工具 / 电池用尽）时才重置倒计时；离开水边只暂停递减，避免条永远走不满。
            if (!canElectric)
            {
                _electricBatteryDrainCooldownRemaining = ElectricBatteryDrainIntervalFrames;
                ElectricFishBatteryHud.CooldownRemaining = _electricBatteryDrainCooldownRemaining;
                _electricHealthDrainCooldownRemaining = ElectricFishHealthDrainIntervalFrames;
                return;
            }

            if (activeElectric && Game1.game1 is { IsActive: true })
            {
                if (_electricBatteryDrainCooldownRemaining > 0)
                    _electricBatteryDrainCooldownRemaining--;
                else if (player.CurrentTool is FishingRod rod
                         && CustomToolData.TryConsumeOneBatteryCharge(rod))
                {
                    _electricBatteryDrainCooldownRemaining = ElectricBatteryDrainIntervalFrames;
                    OnElectricFishBatteryUnitConsumed(player);
                }

                if (_electricHealthDrainCooldownRemaining > 0)
                    _electricHealthDrainCooldownRemaining--;
                else
                {
                    ApplyElectricFishShockDamage(player);
                    _electricHealthDrainCooldownRemaining = ElectricFishHealthDrainIntervalFrames;
                }
            }

            ElectricFishBatteryHud.CooldownRemaining = _electricBatteryDrainCooldownRemaining;

            if (!activeElectric)
                return;

            if (Game1.game1 is { IsActive: true })
                SpawnFishJump(loc, player);
        }

        /// <summary>
        /// 电鱼机每成功消耗 1 份电池组电量时调用：立刻加钓鱼经验（数值与技能页「向下一级」进度条上约一格相当）。
        /// </summary>
        private static void OnElectricFishBatteryUnitConsumed(Farmer player)
        {
            if (!player.IsLocalPlayer)
                return;

            int xp = GetFishingXpForOneSkillMenuBarSegment(player);
            if (xp > 0)
                player.gainExperience(Farmer.fishingSkill, xp);
        }

        /// <summary>与技能页面向下一级进度条上约一格（五分之一段）相当的经验值。</summary>
        private static int GetFishingXpForOneSkillMenuBarSegment(Farmer who)
        {
            int lvl = who.fishingLevel.Value;
            int span;
            if (lvl >= 10)
            {
                span = Farmer.getBaseExperienceForLevel(10) - Farmer.getBaseExperienceForLevel(9);
            }
            else
            {
                int floorXp = lvl > 0 ? Farmer.getBaseExperienceForLevel(lvl) : 0;
                int ceilXp = Farmer.getBaseExperienceForLevel(lvl + 1);
                span = Math.Max(1, ceilXp - floorXp);
            }

            return Math.Max(1, span / 5);
        }

        /// <summary>电击扣血：仅本地玩家，避免联机误伤。</summary>
        private static void ApplyElectricFishShockDamage(Farmer player)
        {
            if (!player.IsLocalPlayer)
                return;

            if (player.health <= 0)
                return;

            player.health = Math.Max(0, player.health - ElectricFishHealthDamagePerPulse);
            player.currentLocation.playSound("ow");
        }

        /// <summary>
        /// 以 <paramref name="player"/> 站立点（脚底像素坐标）为圆心，收集切比雪夫半径内的可钓鱼格。
        /// 半径 = 配置 <see cref="ModConfig.ElectricFishRange"/>（[3, 10]，默认 3）。
        /// </summary>
        private void CollectFishableTilesAroundPlayer(GameLocation loc, Farmer player, List<Point> output)
        {
            output.Clear();

            Vector2 standing = player.getStandingPosition();
            float centerTileX = standing.X / 64f;
            float centerTileY = standing.Y / 64f;
            int radius = _config.ClampedElectricFishRange;

            int minTileX = (int)Math.Floor(centerTileX) - radius;
            int maxTileX = (int)Math.Floor(centerTileX) + radius;
            int minTileY = (int)Math.Floor(centerTileY) - radius;
            int maxTileY = (int)Math.Floor(centerTileY) + radius;

            for (int x = minTileX; x <= maxTileX; x++)
            {
                for (int y = minTileY; y <= maxTileY; y++)
                {
                    float cheb = Math.Max(
                        Math.Abs(x + 0.5f - centerTileX),
                        Math.Abs(y + 0.5f - centerTileY));
                    if (cheb > radius)
                        continue;

                    if (loc.isTileFishable(x, y))
                        output.Add(new Point(x, y));
                }
            }
        }

        /// <summary>在范围内按距人物中心的距离加权随机选一格。</summary>
        private Point PickFishableTileWeightedTowardPlayer(List<Point> tiles, float centerTileX, float centerTileY)
        {
            if (tiles.Count == 1)
                return tiles[0];

            double totalWeight = 0;
            foreach (Point tile in tiles)
            {
                float cheb = Math.Max(
                    Math.Abs(tile.X + 0.5f - centerTileX),
                    Math.Abs(tile.Y + 0.5f - centerTileY));
                totalWeight += 1.0 / (1.0 + cheb);
            }

            double roll = random.NextDouble() * totalWeight;
            foreach (Point tile in tiles)
            {
                float cheb = Math.Max(
                    Math.Abs(tile.X + 0.5f - centerTileX),
                    Math.Abs(tile.Y + 0.5f - centerTileY));
                roll -= 1.0 / (1.0 + cheb);
                if (roll <= 0)
                    return tile;
            }

            return tiles[tiles.Count - 1];
        }

        /// <summary>每帧在配置范围内加权随机一格刷鱼（离人物越近概率越大）。</summary>
        private void SpawnFishJump(GameLocation loc, Farmer player)
        {
            if (Game1.activeClickableMenu != null || (Game1.options.pauseWhenOutOfFocus && !Game1.game1.IsActive))
                return;

            CollectFishableTilesAroundPlayer(loc, player, _fishableTilesBuffer);
            if (_fishableTilesBuffer.Count == 0)
                return;

            Vector2 standing = player.getStandingPosition();
            float centerTileX = standing.X / 64f;
            float centerTileY = standing.Y / 64f;
            Point tile = PickFishableTileWeightedTowardPlayer(_fishableTilesBuffer, centerTileX, centerTileY);
            float fishX = tile.X * 64 + 32;
            float fishY = tile.Y * 64 + 32;
            SpawnFishAndEffectsAtTile(loc, fishX, fishY, player);
        }

        /// <summary>
        /// 电鱼机生成物为「鱼」时按钓鱼等级提高银/金/铱星概率（0 级几乎全普通，10 级高星明显增多）。
        /// </summary>
        private int RollElectricFishQualityByFishingLevel(int fishingLevel)
        {
            int lvl = Math.Clamp(fishingLevel, 0, 10);
            int r = random.Next(100);

            if (lvl >= 10)
            {
                if (r < 28)
                    return 4;
                if (r < 58)
                    return 2;
                if (r < 83)
                    return 1;
                return 0;
            }

            if (lvl >= 8)
            {
                if (r < 16)
                    return 4;
                if (r < 46)
                    return 2;
                if (r < 78)
                    return 1;
                return 0;
            }

            if (lvl >= 6)
            {
                if (r < 6)
                    return 4;
                if (r < 34)
                    return 2;
                if (r < 72)
                    return 1;
                return 0;
            }

            if (lvl >= 4)
            {
                if (r < 18)
                    return 2;
                if (r < 58)
                    return 1;
                return 0;
            }

            if (lvl >= 2)
            {
                if (r < 8)
                    return 2;
                if (r < 38)
                    return 1;
                return 0;
            }

            if (lvl >= 1)
            {
                if (r < 15)
                    return 1;
                return 0;
            }

            return 0;
        }

        private void SpawnFishAndEffectsAtTile(GameLocation loc, float x, float y, Farmer farmer)
        {
            Point bobberTile = new Point((int)(x / 64), (int)(y / 64));
            if (!loc.isTileFishable(bobberTile.X, bobberTile.Y))
                return;

            VanillaFishingQuery.BobberFishingInfo fishingInfo = VanillaFishingQuery.QueryBobberTile(loc, bobberTile, farmer);
            if (Monitor.IsVerbose)
            {
                string candidates = fishingInfo.PossibleQualifiedItemIds.Count > 0
                    ? string.Join(", ", fishingInfo.PossibleQualifiedItemIds)
                    : "(无/仅矿井等特殊规则)";
                Monitor.VerboseLog(
                    $"电鱼格 ({bobberTile.X},{bobberTile.Y}) 钓区={fishingInfo.FishAreaId ?? "默认"}"
                    + (fishingInfo.FishAreaDisplayName != null ? $" ({fishingInfo.FishAreaDisplayName})" : "")
                    + $" 水深={fishingInfo.WaterDepth} 季节={fishingInfo.SeasonForLocation} 候选={candidates}");
            }

            Item? fishItem = VanillaFishingQuery.RollCatchAtTile(loc, bobberTile, farmer);
            if (fishItem == null)
                return;

            fishItem.Stack = 1;

            if (fishItem is StardewValley.Object obj && obj.Category == StardewValley.Object.FishCategory)
            {
                obj.Quality = _config.OnlyIridiumQuality
                    ? 4
                    : RollElectricFishQualityByFishingLevel(Math.Min(farmer.FishingLevel, 10));
            }

            Vector2 position = new Vector2(x, y);
            Game1.createItemDebris(fishItem, position, -1, loc);

            // 与原版收竿一致登记钓获，解锁 Collections 钓鱼图鉴。
            // 不判断 IsLocalPlayer：联机访客、分屏等场景下「正在电鱼」的农民也应写入其本人图鉴（各客户端各写自己的 Farmer）。
            if (!string.IsNullOrEmpty(fishItem.QualifiedItemId))
            {
                string qid = fishItem.QualifiedItemId;
                farmer.caughtFish(qid, 1, false, 1);
                // 海/河/洞穴凝胶等使用 UseFishCaughtSeededRandom，依赖 PreciseFishCaught（原版收竿在 FishingRod 里递增）。
                if (CountsForPreciseFishCaughtStat(fishItem))
                    farmer.stats.Increment("PreciseFishCaught", 1);
                // 日记里类型为 Fishing/ 的任务（如威利「钓 3 条沙鱼」）由 FishingQuest 单独计数，见 Modding:Quest_data。
                NotifyJournalFishingQuestsForCatch(farmer, qid);
                // 镇长家特别订单里 Type: Fish（如钓 20 条河鱼/海鱼）只认「钓鱼」；原版走 FishObjective.OnFishCaught，见 Modding:Special_orders。
                NotifySpecialOrderFishObjectives(farmer, fishItem);
            }

            // 创建鱼跳跃的水花动画
            var splash = new TemporaryAnimatedSprite(
                "TileSheets\\animations",
                new Rectangle(0, 3264, 64, 64),
                new Vector2(x - 32, y - 32),
                false,
                0.02f,
                Color.White
            )
            {
                scale = 1f,
                motion = Vector2.Zero,
                alphaFade = 0f,
                layerDepth = 0.0001f,
                animationLength = 10,
                totalNumberOfLoops = 1,
                interval = 150f
            };
            loc.temporarySprites.Add(splash);
        }

        /// <summary>与 <see cref="StardewValley.Tools.FishingRod"/> 收竿统计一致，供凝胶等鱼种的种子随机使用。</summary>
        private static bool CountsForPreciseFishCaughtStat(Item item)
        {
            return item.Category == StardewValley.Object.FishCategory
                || item.HasContextTag("counts_as_fish_catch");
        }

        /// <summary>
        /// <see cref="Farmer.caughtFish"/> 会更新图鉴等，但 Data/Quests 中前缀为 <c>Fishing/</c> 的日记任务由
        /// <see cref="FishingQuest"/> 通过 <see cref="FishingQuest.OnFishCaught"/> 推进（与收竿路径一致）。
        /// </summary>
        /// <seealso href="https://stardewvalleywiki.com/Modding:Quest_data">Modding:Quest data</seealso>
        private static void NotifyJournalFishingQuestsForCatch(Farmer farmer, string qualifiedFishItemId)
        {
            foreach (Quest? quest in farmer.questLog)
            {
                if (quest is FishingQuest fishingQuest)
                    fishingQuest.OnFishCaught(qualifiedFishItemId, numberCaught: 1, size: 1, probe: false);
            }
        }

        /// <summary>
        /// Data/SpecialOrders 中 <c>Type: Fish</c> 与 Collect 类似，但 Wiki 写明「只统计通过钓鱼获得的鱼」；
        /// 游戏在 <see cref="FishObjective.OnFishCaught"/> 内按鱼的上下文标签匹配 <c>AcceptedContextTags</c> 并推进计数。
        /// </summary>
        /// <seealso href="https://stardewvalleywiki.com/Modding:Special_orders">Modding:Special orders</seealso>
        private static void NotifySpecialOrderFishObjectives(Farmer farmer, Item fishItem)
        {
            if (farmer.team?.specialOrders == null)
                return;

            foreach (SpecialOrder? order in farmer.team.specialOrders)
            {
                if (order?.objectives == null)
                    continue;

                foreach (OrderObjective objective in order.objectives)
                {
                    if (objective is FishObjective fishObjective)
                        fishObjective.OnFishCaught(farmer, fishItem);
                }
            }
        }
    }
}
