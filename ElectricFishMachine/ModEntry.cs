using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Tools;

namespace ElectricFishMachine
{
    public class ModEntry : Mod
    {
        private Random random = new Random();

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

        public override void Entry(IModHelper helper)
        {
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

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
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

        private void SpawnPersistentBubbles(GameLocation loc, Farmer player)
        {
            int tileX = (int)(player.Position.X / 64);
            int tileY = (int)(player.Position.Y / 64);

            int waterCount = 0;
            for (int dx = -3; dx <= 4; dx++)  // 8列：左3到右4
            {
                for (int dy = -3; dy <= 4; dy++)  // 8行：上3到下4
                {
                    int x = tileX + dx;
                    int y = tileY + dy;

                    // 跳过距离玩家小于2格的区域（确保气泡距离人物至少2格远）
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < 2)
                        continue;

                    bool isWater = loc.isWaterTile(x, y);
                    if (isWater)
                    {
                        waterCount++;
                    }
                }
            }
        }

        private void SpawnFishJump(GameLocation loc, Farmer player)
        {
            // 获取当前水域的格子列表
            List<Point> waterTiles = new List<Point>();
            int tileX = (int)(player.Position.X / 64);
            int tileY = (int)(player.Position.Y / 64);

            for (int dx = -2; dx <= 3; dx++)
            {
                for (int dy = -2; dy <= 3; dy++)
                {
                    int x = tileX + dx;
                    int y = tileY + dy;

                    if (loc.isWaterTile(x, y))
                    {
                        waterTiles.Add(new Point(x, y));
                    }
                }
            }

            if (waterTiles.Count == 0)
                return;

            // 随机选择一个水域格子
            Point randomTile = waterTiles[random.Next(waterTiles.Count)];
            float fishX = randomTile.X * 64 + 32;
            float fishY = randomTile.Y * 64 + 32;

            // 创建鱼跳跃动画
            CreateFishJumpAnimation(loc, fishX, fishY, player);
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

        private void CreateFishJumpAnimation(GameLocation loc, float x, float y, Farmer farmer)
        {
            // 检查游戏是否暂停（鼠标移出窗口或有菜单打开）
            if (Game1.activeClickableMenu != null || (Game1.options.pauseWhenOutOfFocus && !Game1.game1.IsActive))
            {
                return;
            }

            string season = Game1.currentSeason;
            int currentHour = Game1.timeOfDay / 100;
            bool isGingerIsland = FishDataHelper.IsGingerIslandLocation(loc);
            if (isGingerIsland)
            {
                season = "summer";
            }
            bool isLava = FishDataHelper.IsLavaLocation(loc);

            // 获取矿井层数
            int mineLevel = 0;
            if (loc is MineShaft mine)
            {
                mineLevel = mine.mineLevel;
            }

            List<int> availableFishIds = FishDataHelper.GetAvailableFishForLocation(loc, season, isGingerIsland, isLava, mineLevel);
            availableFishIds = FishDataHelper.FilterFishByTime(availableFishIds, currentHour);

            if (availableFishIds.Count == 0)
            {
                return;
            }

            // 传说鱼ID列表
            HashSet<int> legendaryFishIds = new HashSet<int> { 163, 159, 160, 775, 898, 899, 900, 902 };

            // 分离传说鱼和普通鱼
            List<int> legendaryFish = availableFishIds.Where(id => legendaryFishIds.Contains(id)).ToList();
            List<int> normalFish = availableFishIds.Where(id => !legendaryFishIds.Contains(id)).ToList();

            int fishId;

            // 传说鱼0.01%概率生成
            if (legendaryFish.Count > 0 && random.Next(10000) < 1)
            {
                fishId = legendaryFish[random.Next(legendaryFish.Count)];
            }
            else if (normalFish.Count > 0)
            {
                fishId = normalFish[random.Next(normalFish.Count)];
            }
            else
            {
                fishId = availableFishIds[random.Next(availableFishIds.Count)];
            }
            Item fishItem;

            if (fishId == 134)
            {
                fishItem = ItemRegistry.Create("(O)SeaJelly");
            }
            else if (fishId == 873)
            {
                fishItem = ItemRegistry.Create("(O)RiverJelly");
            }
            else if (fishId == 874)
            {
                fishItem = ItemRegistry.Create("(O)CaveJelly");
            }
            else if (fishId == -1)
            {
                fishItem = ItemRegistry.Create("(O)Goby");
            }
            else
            {
                fishItem = ItemRegistry.Create("(O)" + fishId);
            }

            if (fishItem == null)
            {
                return;
            }

            fishItem.Stack = 1;

            if (fishItem is StardewValley.Object obj && obj.Category == StardewValley.Object.FishCategory)
            {
                obj.Quality = RollElectricFishQualityByFishingLevel(Math.Min(farmer.FishingLevel, 10));
            }

            Vector2 position = new Vector2(x, y);
            Game1.createItemDebris(fishItem, position, -1, loc);

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
    }
}
