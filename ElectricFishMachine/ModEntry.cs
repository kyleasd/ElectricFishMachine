using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;

namespace WaterBubbleMod
{
    public class ModEntry : Mod
    {
        private Random random = new Random();

        public override void Entry(IModHelper helper)
        {
            Monitor.Log("电鱼机MOD已加载", LogLevel.Info);
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree)
                return;

            if (Game1.paused)
                return;

            Farmer player = Game1.player;
            GameLocation loc = player.currentLocation;

            int tileX = (int)(player.Position.X / 64);
            int tileY = (int)(player.Position.Y / 64);

            bool nearWater = IsNearWater(loc, tileX, tileY);

            if (nearWater)
            {
                SpawnFishJump(loc, player);
            }
        }

        private bool IsNearWater(GameLocation loc, int tileX, int tileY)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = tileX + dx;
                    int y = tileY + dy;

                    if (loc.isWaterTile(x, y))
                    {
                        return true;
                    }
                }
            }
            return false;
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
            CreateFishJumpAnimation(loc, fishX, fishY);
        }

        private void CreateFishJumpAnimation(GameLocation loc, float x, float y)
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
