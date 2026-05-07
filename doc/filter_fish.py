import json
import os

def filter_fish_objects(input_file, output_file):
    """
    从Objects.json中过滤出Type为"Fish"的物品
    """
    print(f"正在读取 {input_file}...")
    
    with open(input_file, 'r', encoding='utf-8') as f:
        objects_data = json.load(f)
    
    print(f"原始数据共有 {len(objects_data)} 个物品")
    
    # 过滤出Type为"Fish"的物品
    fish_objects = {}
    removed_count = 0
    
    for item_id, item_data in objects_data.items():
        if item_data.get("Type") == "Fish":
            fish_objects[item_id] = item_data
        else:
            removed_count += 1
    
    print(f"保留了 {len(fish_objects)} 个鱼类物品")
    print(f"删除了 {removed_count} 个非鱼类物品")
    
    # 保存过滤后的数据
    print(f"正在保存到 {output_file}...")
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(fish_objects, f, ensure_ascii=False, indent=2)
    
    print("完成！")

if __name__ == "__main__":
    # 设置文件路径
    doc_dir = os.path.dirname(os.path.abspath(__file__))
    input_file = os.path.join(doc_dir, "Objects.json")
    output_file = os.path.join(doc_dir, "Objects_FishOnly.json")
    
    # 执行过滤
    filter_fish_objects(input_file, output_file)
    
    print(f"\n提示：")
    print(f"- 原始文件: {input_file}")
    print(f"- 过滤后文件: {output_file}")
    print(f"- 请检查过滤后的文件，确认无误后可以替换原文件")
