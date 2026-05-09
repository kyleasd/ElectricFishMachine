#!/bin/bash

echo "========================================"
echo "  ElectricFishMachine Mod 打包工具"
echo "========================================"
echo ""

# [1/3] 编译项目
echo "[1/3] 正在编译项目..."
dotnet build -c Release
if [ $? -ne 0 ]; then
    echo "编译失败！"
    read -p "按任意键继续..."
    exit 1
fi
echo "编译成功！"
echo ""

# [2/3] 复制文件到游戏 Mods 目录
echo "[2/3] 复制文件到游戏 Mods 目录..."
MOD_DIR="/d/Program Files (x86)/Steam/steamapps/common/Stardew Valley/Mods/ElectricFishMachine"
mkdir -p "$MOD_DIR"
cp -f "bin/Release/net6.0/ElectricFishMachine.dll" "$MOD_DIR/"
cp -f "manifest.json" "$MOD_DIR/"
cp -rf "bin/Release/net6.0/assets" "$MOD_DIR/"
echo "文件复制完成！"
echo ""

# [3/3] 启动 SMAPI
echo "[3/3] 启动 SMAPI..."
start "" "/d/Program Files (x86)/Steam/steamapps/common/Stardew Valley/StardewModdingAPI.exe"
echo "游戏已启动！"
echo ""

echo "========================================"
echo "  完成！"
echo "  Mod 位置: $MOD_DIR"
echo "========================================"
