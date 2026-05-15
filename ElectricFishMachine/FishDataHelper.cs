using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;

namespace ElectricFishMachine
{
    public static class FishDataHelper
    {
        private static readonly Random random = new Random();

        public static List<int> GetAvailableFishForLocation(GameLocation loc, string season, bool isGingerIsland = false, bool isLava = false, int mineLevel = 0)
        {
            List<int> fishIds = new List<int>();

            // 检查是否是矿井并且在特定层数
            bool isMine = IsMineLocation(loc);
            if (isMine && mineLevel > 0)
            {
                if (mineLevel == 20)
                {
                    // 矿井20层：鬼鱼、石鱼、洞穴凝胶（与原版矿井地下水一致）
                    fishIds.Add(156); // 鬼鱼 (Ghostfish)
                    fishIds.Add(158); // 石鱼 (Stonefish)
                    fishIds.Add(874); // 洞穴凝胶 (Cave Jelly)
                    return fishIds;
                }
                else if (mineLevel == 60)
                {
                    // 矿井60层：冰柱鱼、洞穴凝胶
                    fishIds.Add(161); // 冰柱鱼 (Ice Pip)
                    fishIds.Add(874); // 洞穴凝胶 (Cave Jelly)
                    return fishIds;
                }
                else if (mineLevel >= 100)
                {
                    // 矿井100层及以下：岩浆鳗鱼、洞穴凝胶
                    fishIds.Add(162); // 岩浆鳗鱼 (Lava Eel)
                    fishIds.Add(874); // 洞穴凝胶 (Cave Jelly)
                    return fishIds;
                }
            }

            if (isLava)
            {
                // 岩浆区域（如姜岛火山）：只有岩浆鳗鱼
                fishIds.Add(162); // 岩浆鳗鱼 (Lava Eel)
                return fishIds;
            }

            if (isGingerIsland)
            {
                bool isGingerIslandVolcano = IsGingerIslandVolcanoLocation(loc);

                int[] gingerIslandFish;

                if (isGingerIslandVolcano)
                {
                    // 姜岛火山区域：只有岩浆鳗鱼
                    gingerIslandFish = new int[] {
                        162  // 岩浆鳗鱼 (Lava Eel) - 姜岛火山特有
                    };
                }
                else
                {
                    gingerIslandFish = new int[] {
                        128, // 河豚 (Pufferfish) - 姜岛海洋
                        130, // 金枪鱼 (Tuna) - 姜岛海洋
                        149, // 章鱼 (Octopus) - 姜岛海洋
                        155, // 大海参 (Super Cucumber) - 姜岛海洋
                        267, // 比目鱼 (Flounder) - 姜岛海洋
                        269, // 午夜鲤鱼 (Midnight Carp) - 姜岛淡水
                        701, // 罗非鱼 (Tilapia) - 姜岛淡水
                        836, // 黄貂鱼 (Stingray) - 姜岛海洋特有
                        837, // 狮子鱼 (Lionfish) - 姜岛海洋特有
                        838  // 蓝铁饼鱼 (Blue Discus) - 姜岛淡水特有
                    };
                }

                fishIds.AddRange(gingerIslandFish);

                if (random.Next(100) < 15)
                {
                    int[] junkItems = new int[] { 152, 153, 157 };
                    fishIds.Add(junkItems[random.Next(junkItems.Length)]);
                }

                return fishIds;
            }

            bool isDesert = IsDesertLocation(loc);
            if (isDesert)
            {
                int[] desertFish = new int[] {
                    164, // 沙鱼 (Sandfish) - 沙漠特有，全年可用，6:00-20:00
                    165  // 蝎鲤鱼 (Scorpion Carp) - 沙漠特有，全年可用，6:00-20:00，需要钓鱼等级4+
                };

                fishIds.AddRange(desertFish);

                if (random.Next(100) < 15)
                {
                    int[] junkItems = new int[] { 152, 153, 157 };
                    fishIds.Add(junkItems[random.Next(junkItems.Length)]);
                }

                return fishIds;
            }

            bool isFarm = IsFarmLocation(loc);
            if (isFarm)
            {
                int[] farmTrash = new int[] {
                    167, // Joja可乐 (Joja Cola)
                    168, // 垃圾 (Trash)
                    169, // 浮木 (Driftwood)
                    170, // 破损的眼镜 (Broken Glasses)
                    171, // 破损的CD (Broken CD)
                    172  // 湿透的报纸 (Soggy Newspaper)
                };

                fishIds.AddRange(farmTrash);

                return fishIds;
            }

            bool isSecretWoods = IsSecretWoodsLocation(loc);
            if (isSecretWoods)
            {
                int[] secretWoodsFish = new int[] {
                    142, // 鲤鱼 (Carp) - 全年
                    734  // 木跃鱼 (Woodskip) - 全年
                };

                fishIds.AddRange(secretWoodsFish);

                if (random.Next(100) < 15)
                {
                    int[] junkItems = new int[] { 152, 153, 157 };
                    fishIds.Add(junkItems[random.Next(junkItems.Length)]);
                }

                return fishIds;
            }

            bool isSewer = IsSewerLocation(loc);
            if (isSewer)
            {
                int[] sewerFish = new int[] {
                    682, // 变种鲤鱼 (Mutant Carp) - 全年
                    157  // 白藻 (White Algae) - 全年
                };

                fishIds.AddRange(sewerFish);

                return fishIds;
            }

            bool isMutantBugLair = IsMutantBugLairLocation(loc);
            if (isMutantBugLair)
            {
                int[] mutantBugLairFish = new int[] {
                    142, // 鲤鱼 (Carp) - 全年
                    796, // 史莱姆鱼 (Slimejack) - 全年
                    157  // 白藻 (White Algae) - 全年
                };

                fishIds.AddRange(mutantBugLairFish);

                return fishIds;
            }

            bool isWitchSwamp = IsWitchSwampLocation(loc);
            if (isWitchSwamp)
            {
                int[] witchSwampFish = new int[] {
                    795, // 虚空鲑鱼 (Void Salmon) - 全年
                    143, // 鲶鱼 (Catfish) - 春季/秋季，6:00-0:00，雨天
                    157  // 白藻 (White Algae) - 全年
                };

                fishIds.AddRange(witchSwampFish);

                return fishIds;
            }

            int[] saltwaterFish = new int[] {
                128, // 河豚 (Pufferfish) - 夏季
                129, // 鳀鱼 (Anchovy) - 春季/秋季
                130, // 金枪鱼 (Tuna) - 夏季
                131, // 沙丁鱼 (Sardine) - 春季/秋季/冬季
                146, // 红鲻鱼 (Red Mullet) - 春季/夏季/冬季
                147, // 鲱鱼 (Herring) - 春季/冬季
                148, // 鳗鱼 (Eel) - 春季/秋季（雨天）
                149, // 章鱼 (Octopus) - 夏季
                150, // 红鲷鱼 (Red Snapper) - 夏季/秋季/冬季（雨天）
                151, // 鱿鱼 (Squid) - 冬季
                154, // 海参 (Sea Cucumber) - 秋季/冬季
                155, // 大海参 (Super Cucumber) - 夏季/秋季/冬季
                267, // 比目鱼 (Flounder) - 春季
                701, // 罗非鱼 (Tilapia) - 夏季/秋季
                705, // 青花鱼 (Albacore) - 秋季/冬季
                708, // 大比目鱼 (Halibut) - 春季/冬季
                134, // 海凝胶 (Sea Jelly) - 全年
                372, // 蛤蜊 (Clam) - 全年
                715, // 龙虾 (Lobster) - 全年
                717, // 螃蟹 (Crab) - 全年
                718, // 鸟蛤 (Cockle) - 全年
                719, // 贻贝 (Mussel) - 全年
                720, // 虾 (Shrimp) - 全年
                722, // 滨螺 (Periwinkle) - 全年
                723, // 牡蛎 (Oyster) - 全年
                798, // 午夜鱿鱼 (Midnight Squid) - 冬季夜市
                800, // 水滴鱼 (Blobfish) - 冬季夜市
                799, // 幽灵鱼 (Spook Fish) - 冬季夜市
                159, // 绯红鱼 (Crimsonfish) - 夏季
                898  // 绯红鱼之子 (Son of Crimsonfish) - 全年
            };

            int[] freshwaterFish = new int[] {
                132, // 鲷鱼 (Bream) - 全年，18:00-2:00
                136, // 大嘴鲈鱼 (Largemouth Bass) - 全年湖泊，6:00-19:00
                137, // 小嘴鲈鱼 (Smallmouth Bass) - 春季/夏季/秋季，全天
                138, // 虹鳟鱼 (Rainbow Trout) - 夏季，12:00-2:00，晴天
                139, // 鲑鱼 (Salmon) - 夏季/秋季，6:00-19:00
                140, // 大眼鱼 (Walleye) - 秋季/冬季，12:00-2:00，雨天
                141, // 鲈鱼 (Perch) - 冬季，全天
                142, // 鲤鱼 (Carp) - 全年，全天
                143, // 鲶鱼 (Catfish) - 春季/秋季，6:00-0:00，雨天
                144, // 狗鱼 (Pike) - 夏季/冬季，全天
                145, // 太阳鱼 (Sunfish) - 春季/夏季，6:00-19:00，晴天/有风
                146, // 红鲻鱼 (Red Mullet) - 冬季，全天
                151, // 大头鱼 (Bullhead) - 全年湖泊，全天
                269, // 午夜鲤鱼 (Midnight Carp) - 冬季，22:00-2:00
                698, // 鲟鱼 (Sturgeon) - 全年湖泊，6:00-19:00
                699, // 虎纹鳟鱼 (Tiger Trout) - 秋季/冬季，6:00-19:00
                700, // 大头鱼 (Bullhead) - 全年湖泊，全天
                702, // 鲢鱼 (Chub) - 全年，全天
                704, // 麻哈脂鲤 (Dorado) - 夏季，6:00-19:00
                706, // 西鲱 (Shad) - 春季/夏季/秋季，9:00-2:00，雨天
                707, // 蛇齿单线鱼 (Lingcod) - 冬季，全天
                153, // 绿藻 (Green Algae) - 全年
                716, // 小龙虾 (Crayfish) - 全年
                721, // 蜗牛 (Snail) - 全年
                734, // 木跃鱼 (Woodskip) - 全年
                873, // 河凝胶 (River Jelly) - 全年
                -1, // 虾虎鱼 (Goby) - 使用字符串ID
                163, // 传说之鱼 (Legend) - 春季
                160, // 鮟鱇鱼 (Angler) - 秋季
                775, // 冰川鱼 (Glacierfish) - 冬季
                899, // 雌鮟鱇鱼 (Ms. Angler) - 全年
                900, // 传说之鱼二代 (Legend II) - 全年
                902  // 小冰川鱼 (Glacierfish Jr.) - 全年
            };

            bool isSaltwater = IsSaltwaterLocation(loc);
            int[] fishPool = isSaltwater ? saltwaterFish : freshwaterFish;

            foreach (int fishId in fishPool)
            {
                if (IsFishAvailableInSeason(fishId, season))
                {
                    fishIds.Add(fishId);
                }
            }

            if (fishIds.Count == 0)
            {
                fishIds.AddRange(new int[] { 132, 136, 142, 702 });
            }

            if (random.Next(100) < 15)
            {
                int[] junkItems = new int[] { 152, 153, 157 };
                fishIds.Add(junkItems[random.Next(junkItems.Length)]);
            }

            return fishIds;
        }

        public static bool IsSaltwaterLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return locationName.Contains("beach") ||
                   locationName.Contains("ocean") ||
                   locationName.Contains("tide");
        }

        public static bool IsGingerIslandLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return locationName.Contains("island") ||
                   locationName.Contains("ginger");
        }

        public static bool IsGingerIslandVolcanoLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return (locationName.Contains("island") || locationName.Contains("ginger")) &&
                   (locationName.Contains("volcano") || locationName.Contains("mountain"));
        }

        public static bool IsMineLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return locationName.Contains("mine") || loc is StardewValley.Locations.MineShaft;
        }

        public static bool IsLavaLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return locationName.Contains("volcano") ||
                   locationName.Contains("lava") ||
                   locationName.Contains("caldera");
        }

        public static bool IsDesertLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return locationName.Contains("desert") ||
                   locationName.Contains("calico");
        }

        public static bool IsFarmLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return locationName.Contains("farm") || locationName == "farm";
        }

        public static bool IsSecretWoodsLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return locationName.Contains("secret") && locationName.Contains("wood");
        }

        public static bool IsSewerLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return locationName.Contains("sewer");
        }

        public static bool IsMutantBugLairLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return locationName.Contains("mutant") || locationName.Contains("bug");
        }

        public static bool IsWitchSwampLocation(GameLocation loc)
        {
            string locationName = loc.Name.ToLower();
            return locationName.Contains("witch") || locationName.Contains("swamp");
        }

        public static bool IsFishAvailableInSeason(int fishId, string season)
        {
            Dictionary<int, string[]> fishSeasons = new Dictionary<int, string[]>
            {
                { 152, new string[] { "spring", "summer", "fall", "winter" } },
                { 153, new string[] { "spring", "summer", "fall", "winter" } },
                { 157, new string[] { "spring", "summer", "fall", "winter" } },
                { 167, new string[] { "spring", "summer", "fall", "winter" } },
                { 168, new string[] { "spring", "summer", "fall", "winter" } },
                { 169, new string[] { "spring", "summer", "fall", "winter" } },
                { 170, new string[] { "spring", "summer", "fall", "winter" } },
                { 171, new string[] { "spring", "summer", "fall", "winter" } },
                { 172, new string[] { "spring", "summer", "fall", "winter" } },

                { 873, new string[] { "spring", "summer", "fall", "winter" } },
                { 874, new string[] { "spring", "summer", "fall", "winter" } },
                { -1, new string[] { "spring", "summer", "fall", "winter" } }, // 虾虎鱼

                { 128, new string[] { "summer" } },
                { 129, new string[] { "spring", "fall" } },
                { 130, new string[] { "summer" } },
                { 131, new string[] { "spring", "fall", "winter" } },
                { 146, new string[] { "spring", "summer", "winter" } },
                { 147, new string[] { "spring", "winter" } },
                { 148, new string[] { "spring", "fall" } },
                { 149, new string[] { "summer" } },
                { 150, new string[] { "summer", "fall", "winter" } },
                { 151, new string[] { "winter" } },
                { 154, new string[] { "fall", "winter" } },
                { 155, new string[] { "summer", "fall", "winter" } },
                { 267, new string[] { "spring" } },
                { 701, new string[] { "summer", "fall" } },
                { 705, new string[] { "fall", "winter" } },
                { 708, new string[] { "spring", "winter" } },

                { 132, new string[] { "spring", "summer", "fall", "winter" } },
                { 136, new string[] { "spring", "summer", "fall", "winter" } },
                { 137, new string[] { "spring", "summer", "fall" } },
                { 138, new string[] { "summer" } },
                { 139, new string[] { "summer", "fall" } },
                { 140, new string[] { "fall", "winter" } },
                { 141, new string[] { "winter" } },
                { 142, new string[] { "spring", "summer", "fall", "winter" } },
                { 143, new string[] { "spring", "fall" } },
                { 144, new string[] { "summer", "winter" } },
                { 145, new string[] { "spring", "summer" } },
                { 269, new string[] { "winter" } },
                { 698, new string[] { "spring", "summer", "fall", "winter" } },
                { 699, new string[] { "fall", "winter" } },
                { 700, new string[] { "spring", "summer", "fall", "winter" } },
                { 702, new string[] { "spring", "summer", "fall", "winter" } },
                { 704, new string[] { "summer" } },
                { 706, new string[] { "spring", "summer", "fall" } },
                { 707, new string[] { "winter" } },

                { 164, new string[] { "spring", "summer", "fall", "winter" } },
                { 165, new string[] { "spring", "summer", "fall", "winter" } },

                { 156, new string[] { "spring", "summer", "fall", "winter" } },
                { 158, new string[] { "spring", "summer", "fall", "winter" } },
                { 161, new string[] { "spring", "summer", "fall", "winter" } },
                { 162, new string[] { "spring", "summer", "fall", "winter" } },

                { 836, new string[] { "spring", "summer", "fall", "winter" } },
                { 837, new string[] { "spring", "summer", "fall", "winter" } },
                { 838, new string[] { "spring", "summer", "fall", "winter" } },

                { 134, new string[] { "spring", "summer", "fall", "winter" } },
                { 372, new string[] { "spring", "summer", "fall", "winter" } },
                { 715, new string[] { "spring", "summer", "fall", "winter" } },
                { 717, new string[] { "spring", "summer", "fall", "winter" } },
                { 718, new string[] { "spring", "summer", "fall", "winter" } },
                { 719, new string[] { "spring", "summer", "fall", "winter" } },
                { 720, new string[] { "spring", "summer", "fall", "winter" } },
                { 722, new string[] { "spring", "summer", "fall", "winter" } },
                { 723, new string[] { "spring", "summer", "fall", "winter" } },
                { 798, new string[] { "winter" } },
                { 800, new string[] { "winter" } },
                { 799, new string[] { "winter" } },
                { 682, new string[] { "spring", "summer", "fall", "winter" } },
                { 734, new string[] { "spring", "summer", "fall", "winter" } },
                { 796, new string[] { "spring", "summer", "fall", "winter" } },
                { 795, new string[] { "spring", "summer", "fall", "winter" } },
                { 163, new string[] { "spring" } },
                { 159, new string[] { "summer" } },
                { 160, new string[] { "fall" } },
                { 775, new string[] { "winter" } },
                { 898, new string[] { "spring", "summer", "fall", "winter" } },
                { 899, new string[] { "spring", "summer", "fall", "winter" } },
                { 900, new string[] { "spring", "summer", "fall", "winter" } },
                { 902, new string[] { "spring", "summer", "fall", "winter" } },
                { 901, new string[] { "spring", "summer", "fall", "winter" } }
            };

            if (fishSeasons.TryGetValue(fishId, out string[]? seasons) && seasons != null)
            {
                return seasons.Contains(season);
            }

            return false;
        }

        public static List<int> FilterFishByTime(List<int> fishIds, int currentHour)
        {
            Dictionary<int, Tuple<int, int>> fishTimeRanges = new Dictionary<int, Tuple<int, int>>
            {
                { 152, new Tuple<int, int>(0, 24) },
                { 153, new Tuple<int, int>(0, 24) },
                { 157, new Tuple<int, int>(0, 24) },
                { 167, new Tuple<int, int>(0, 24) },
                { 168, new Tuple<int, int>(0, 24) },
                { 169, new Tuple<int, int>(0, 24) },
                { 170, new Tuple<int, int>(0, 24) },
                { 171, new Tuple<int, int>(0, 24) },
                { 172, new Tuple<int, int>(0, 24) },

                { 873, new Tuple<int, int>(0, 24) },
                { 874, new Tuple<int, int>(0, 24) },
                { -1, new Tuple<int, int>(0, 24) }, // 虾虎鱼

                { 128, new Tuple<int, int>(12, 16) },
                { 129, new Tuple<int, int>(0, 24) },
                { 130, new Tuple<int, int>(6, 19) },
                { 131, new Tuple<int, int>(6, 19) },
                { 146, new Tuple<int, int>(6, 19) },
                { 147, new Tuple<int, int>(0, 24) },
                { 148, new Tuple<int, int>(16, 2) },
                { 149, new Tuple<int, int>(6, 13) },
                { 150, new Tuple<int, int>(6, 19) },
                { 151, new Tuple<int, int>(18, 2) },
                { 154, new Tuple<int, int>(6, 19) },
                { 155, new Tuple<int, int>(18, 2) },
                { 267, new Tuple<int, int>(6, 20) },
                { 701, new Tuple<int, int>(6, 14) },
                { 705, new Tuple<int, int>(6, 11) },
                { 708, new Tuple<int, int>(6, 11) },

                { 132, new Tuple<int, int>(18, 2) },
                { 136, new Tuple<int, int>(6, 19) },
                { 137, new Tuple<int, int>(0, 24) },
                { 138, new Tuple<int, int>(12, 2) },
                { 139, new Tuple<int, int>(6, 19) },
                { 140, new Tuple<int, int>(12, 2) },
                { 141, new Tuple<int, int>(0, 24) },
                { 142, new Tuple<int, int>(0, 24) },
                { 143, new Tuple<int, int>(6, 0) },
                { 144, new Tuple<int, int>(0, 24) },
                { 145, new Tuple<int, int>(6, 19) },
                { 269, new Tuple<int, int>(22, 2) },
                { 698, new Tuple<int, int>(6, 19) },
                { 699, new Tuple<int, int>(6, 19) },
                { 700, new Tuple<int, int>(0, 24) },
                { 702, new Tuple<int, int>(0, 24) },
                { 704, new Tuple<int, int>(6, 19) },
                { 706, new Tuple<int, int>(9, 2) },
                { 707, new Tuple<int, int>(0, 24) },

                { 164, new Tuple<int, int>(6, 20) },
                { 165, new Tuple<int, int>(6, 20) },

                { 156, new Tuple<int, int>(0, 24) },
                { 158, new Tuple<int, int>(0, 24) },
                { 161, new Tuple<int, int>(0, 24) },
                { 162, new Tuple<int, int>(0, 24) },

                { 836, new Tuple<int, int>(0, 24) },
                { 837, new Tuple<int, int>(0, 24) },
                { 838, new Tuple<int, int>(0, 24) },

                { 134, new Tuple<int, int>(0, 24) },
                { 372, new Tuple<int, int>(0, 24) },
                { 715, new Tuple<int, int>(0, 24) },
                { 717, new Tuple<int, int>(0, 24) },
                { 718, new Tuple<int, int>(0, 24) },
                { 719, new Tuple<int, int>(0, 24) },
                { 720, new Tuple<int, int>(0, 24) },
                { 722, new Tuple<int, int>(0, 24) },
                { 723, new Tuple<int, int>(0, 24) },
                { 798, new Tuple<int, int>(0, 24) },
                { 800, new Tuple<int, int>(0, 24) },
                { 799, new Tuple<int, int>(0, 24) },
                { 682, new Tuple<int, int>(0, 24) },
                { 734, new Tuple<int, int>(0, 24) },
                { 796, new Tuple<int, int>(0, 24) },
                { 795, new Tuple<int, int>(0, 24) },
                { 163, new Tuple<int, int>(0, 24) },
                { 159, new Tuple<int, int>(0, 24) },
                { 160, new Tuple<int, int>(0, 24) },
                { 775, new Tuple<int, int>(0, 24) },
                { 898, new Tuple<int, int>(0, 24) },
                { 899, new Tuple<int, int>(0, 24) },
                { 900, new Tuple<int, int>(0, 24) },
                { 902, new Tuple<int, int>(0, 24) },
                { 901, new Tuple<int, int>(0, 24) }
            };

            List<int> filteredFish = new List<int>();

            foreach (int fishId in fishIds)
            {
                if (fishTimeRanges.TryGetValue(fishId, out Tuple<int, int>? timeRange) && timeRange != null)
                {
                    int startHour = timeRange.Item1;
                    int endHour = timeRange.Item2;

                    bool isAvailable;

                    if (endHour < startHour)
                    {
                        isAvailable = (currentHour >= startHour || currentHour < endHour);
                    }
                    else
                    {
                        isAvailable = (currentHour >= startHour && currentHour < endHour);
                    }

                    if (isAvailable)
                    {
                        filteredFish.Add(fishId);
                    }
                }
                else
                {
                    filteredFish.Add(fishId);
                }
            }

            return filteredFish;
        }
    }
}
