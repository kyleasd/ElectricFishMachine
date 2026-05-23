#!/bin/bash

echo "========================================"
echo "  ElectricFishMachine Mod 构建并启动"
echo "========================================"
echo ""

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$ROOT_DIR/ElectricFishMachine"
GAME_DIR="/d/Program Files (x86)/Steam/steamapps/common/Stardew Valley"
SMAPI_EXE="$GAME_DIR/StardewModdingAPI.exe"
MOD_DIR="$GAME_DIR/Mods/ElectricFishMachine"

cd "$PROJECT_DIR"

echo "[1/2] 正在 Release 构建（ModBuildConfig 会自动部署到 Mods 并打 zip）..."
dotnet build -c Release
if [ $? -ne 0 ]; then
    echo "编译失败！"
    read -p "按任意键继续..."
    exit 1
fi
echo "编译成功！"
echo ""

echo "[2/2] 启动 SMAPI..."
if [ ! -f "$SMAPI_EXE" ]; then
    echo "找不到 SMAPI：$SMAPI_EXE"
    read -p "按任意键继续..."
    exit 1
fi
start "" "$SMAPI_EXE"
echo "游戏已启动！"
echo ""

echo "========================================"
echo "  完成！"
echo "  Mod 目录: $MOD_DIR"
echo "  发布 zip: $PROJECT_DIR/bin/Release/net6.0/ElectricFishMachine*.zip"
echo "========================================"
