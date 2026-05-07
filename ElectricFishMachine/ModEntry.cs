using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace WaterBubbleMod
{
    public class ModEntry : Mod
    {
        private bool wasNearWater = false;
        private List<TemporaryAnimatedSprite> activeBubbles = new List<TemporaryAnimatedSprite>();
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

            // 检测状态变化：从非水域进入水域
            if (nearWater && !wasNearWater)
            {
                SpawnPersistentBubbles(loc, player);
            }
            // 检测状态变化：从水域离开
            else if (!nearWater && wasNearWater)
            {
                ClearBubbles(loc);
            }

            wasNearWater = nearWater;

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

            // 清除旧气泡
            ClearBubbles(loc);

            // 在周围水域生成持续的气泡：8列 × 8行（扩大范围）
            int bubbleCount = 0;
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

                    if (isWater)
                    {
                        float bubbleX = x * 64 + 64;
                        float bubbleY = y * 64 + 64;

                        var bubble = CreatePersistentBubble(bubbleX, bubbleY);
                        loc.temporarySprites.Add(bubble);
                        activeBubbles.Add(bubble);
                        bubbleCount++;
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
            string season = Game1.currentSeason;
            int currentHour = Game1.timeOfDay / 100;
            bool isGingerIsland = FishDataHelper.IsGingerIslandLocation(loc);
            if (isGingerIsland)
            {
                season = "summer";
            }
            bool isLava = FishDataHelper.IsLavaLocation(loc);

            List<int> availableFishIds = FishDataHelper.GetAvailableFishForLocation(loc, season, isGingerIsland, isLava);
            availableFishIds = FishDataHelper.FilterFishByTime(availableFishIds, currentHour);

            if (availableFishIds.Count == 0)
            {
                return;
            }

            int fishId = availableFishIds[random.Next(availableFishIds.Count)];
            var fishItem = ItemRegistry.Create("(O)" + fishId);
            if (fishItem == null)
            {
                return;
            }

            fishItem.Stack = 1;
            Vector2 position = new Vector2(x, y);
            Game1.createItemDebris(fishItem, position, -1, loc);

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

        private TemporaryAnimatedSprite CreatePersistentBubble(float x, float y)
        {
            // 创建持续的气泡动画效果
            var bubble = new TemporaryAnimatedSprite(
                "TileSheets\\animations",
                new Rectangle(0, 3200, 64, 64),  // 气泡动画位置
                new Vector2(x, y),
                false,
                0.01f,
                Color.White
            )
            {
                scale = 0.5f + (float)(random.NextDouble() * 0.3),  // 随机大小 0.5-0.8
                motion = new Vector2(0, -0.5f),  // 向上漂浮
                alphaFade = 0.002f,
                layerDepth = 0.0001f,
                animationLength = 8,
                totalNumberOfLoops = 999999,  // 几乎无限循环
                interval = 200f
            };
            
            return bubble;
        }

        private void ClearBubbles(GameLocation loc)
        {
            // 清除所有活动的气泡
            foreach (var bubble in activeBubbles)
            {
                loc.temporarySprites.Remove(bubble);
            }
            activeBubbles.Clear();
        }
    }
}
