using System;
using System.Collections.Generic;

namespace RazorEnhanced.UI
{
    internal sealed class PropertyOption
    {
        internal PropertyOption(string internalName, string displayName)
        {
            InternalName = internalName;
            DisplayName = displayName;
        }

        public string InternalName { get; }
        public string DisplayName { get; }
    }

    internal static class PropertyNameLocalization
    {
        private static readonly PropertyOption[] s_defaultOptions =
        {
            new PropertyOption("Balanced", "平衡"),
            new PropertyOption("Cold Resist", "冰冷抗性"),
            new PropertyOption("Damage Increase", "伤害增加"),
            new PropertyOption("Defense Chance Increase", "防御几率增加"),
            new PropertyOption("Dexterity Bonus", "敏捷加成"),
            new PropertyOption("Energy Resists", "能量抗性"),
            new PropertyOption("Faster Cast Recovery", "快速施法恢复"),
            new PropertyOption("Enhance Potion", "药剂增强"),
            new PropertyOption("Energy Damage", "能量伤害"),
            new PropertyOption("Poison Damage", "毒素伤害"),
            new PropertyOption("Fire Damage", "火焰伤害"),
            new PropertyOption("Cold Damage", "冰冷伤害"),
            new PropertyOption("Physical Damage", "物理伤害"),
            new PropertyOption("Faster Casting", "快速施法"),
            new PropertyOption("Gold Increase", "金币增加"),
            new PropertyOption("Fire Resist", "火焰抗性"),
            new PropertyOption("Hit Chance Increase", "命中几率增加"),
            new PropertyOption("Hit Energy Area", "命中能量区域"),
            new PropertyOption("Hit Dispel", "命中驱散"),
            new PropertyOption("Hit Cold Area", "命中冰冷区域"),
            new PropertyOption("Hit Fire Area", "命中火焰区域"),
            new PropertyOption("Hit Fireball", "命中火球术"),
            new PropertyOption("Hit Life Leech", "命中生命吸取"),
            new PropertyOption("Hit Point Increase", "生命上限增加"),
            new PropertyOption("Hit Point Regeneration", "生命恢复"),
            new PropertyOption("Hit Stamina Leech", "命中耐力吸取"),
            new PropertyOption("Hit Poison Area", "命中毒素区域"),
            new PropertyOption("Hit Physical Area", "命中物理区域"),
            new PropertyOption("Hit Mana Leech", "命中魔力吸取"),
            new PropertyOption("Hit Magic Arrow", "命中魔法箭"),
            new PropertyOption("Hit Lower Defense", "命中降低防御"),
            new PropertyOption("Hit Lower Attack", "命中降低攻击"),
            new PropertyOption("Hit Lightning", "命中闪电术"),
            new PropertyOption("Hit Harm", "命中伤害术"),
            new PropertyOption("Intelligence Bonus", "智力加成"),
            new PropertyOption("Lower Mana Cost", "降低魔力消耗"),
            new PropertyOption("Lower Reagent Cost", "降低施法材料消耗"),
            new PropertyOption("Lower Requirements", "降低装备需求"),
            new PropertyOption("Luck", "幸运"),
            new PropertyOption("Mana Increase", "魔力上限增加"),
            new PropertyOption("Mana Regeneration", "魔力恢复"),
            new PropertyOption("Physical Resist", "物理抗性"),
            new PropertyOption("Poison Resist", "毒素抗性"),
            new PropertyOption("Night Sight", "夜视"),
            new PropertyOption("Spell Channeling", "法术引导"),
            new PropertyOption("Spell Damage Increase", "法术伤害增加"),
            new PropertyOption("Splintering Weapon", "碎裂武器"),
            new PropertyOption("Stamina Increase", "耐力上限增加"),
            new PropertyOption("Stamina Regeneration", "耐力恢复"),
            new PropertyOption("Swing Speed Increase", "武器速度增加"),
            new PropertyOption("Velocity", "速度"),
            new PropertyOption("Balanced", "平衡"),
            new PropertyOption("Self Repair", "自我修复"),
            new PropertyOption("Reflect Physical Damage", "反射物理伤害"),
            new PropertyOption("Night Sight", "夜视"),
            new PropertyOption("Mage Armor", "法师护甲"),
            new PropertyOption("Swing Speed Increase", "武器速度增加"),
            new PropertyOption("Strength Bonus", "力量加成"),
            new PropertyOption("Water Elemental Slayer", "水元素屠杀者"),
            new PropertyOption("Troll Slayer", "巨魔屠杀者"),
            new PropertyOption("Undead Slayer", "不死族屠杀者"),
            new PropertyOption("Terathan Slayer", "特拉桑族屠杀者"),
            new PropertyOption("Spider Slayer", "蜘蛛屠杀者"),
            new PropertyOption("Snow Elemental Slayer", "雪元素屠杀者"),
            new PropertyOption("Snake Slayer", "蛇类屠杀者"),
            new PropertyOption("Scorpion Slayer", "蝎子屠杀者"),
            new PropertyOption("Reptile Slayer", "爬行类屠杀者"),
            new PropertyOption("Repond Slayer", "类人族屠杀者"),
            new PropertyOption("Poison Elemental Slayer", "毒元素屠杀者"),
            new PropertyOption("Orc Slayer", "兽人屠杀者"),
            new PropertyOption("Ophidian Slayer", "蛇人族屠杀者"),
            new PropertyOption("Ogre Slayer", "食人魔屠杀者"),
            new PropertyOption("Lizardman Slayer", "蜥蜴人屠杀者"),
            new PropertyOption("Gargoyle Slayer", "石像鬼屠杀者"),
            new PropertyOption("Fire Elemental Slayer", "火元素屠杀者"),
            new PropertyOption("Elemental Slayer", "元素屠杀者"),
            new PropertyOption("Earth Elemental Slayer", "土元素屠杀者"),
            new PropertyOption("Dragon Slayer", "龙族屠杀者"),
            new PropertyOption("Demon Slayer", "恶魔屠杀者"),
            new PropertyOption("Blood Elemental Slayer", "血元素屠杀者"),
            new PropertyOption("Arachnid Slayer", "蛛形类屠杀者"),
            new PropertyOption("Air Elemental Slayer", "风元素屠杀者"),
            new PropertyOption("Magic Arrow Charges", "魔法箭充能"),
            new PropertyOption("Lightning Charges", "闪电术充能"),
            new PropertyOption("Healing Charges", "治疗术充能"),
            new PropertyOption("Harm Charges", "伤害术充能"),
            new PropertyOption("Greater Healing Charges", "强效治疗术充能"),
            new PropertyOption("Fireball Charges", "火球术充能"),
        };

        internal static List<PropertyOption> CreateDefaultOptions()
        {
            return new List<PropertyOption>(s_defaultOptions);
        }

        internal static string ToDisplayName(string internalName)
        {
            if (String.IsNullOrEmpty(internalName))
                return internalName;

            foreach (PropertyOption option in s_defaultOptions)
            {
                if (String.Equals(option.InternalName, internalName, StringComparison.Ordinal))
                    return option.DisplayName;
            }

            return internalName;
        }

        internal static string ToInternalName(string displayName)
        {
            if (String.IsNullOrEmpty(displayName))
                return displayName;

            foreach (PropertyOption option in s_defaultOptions)
            {
                if (String.Equals(option.DisplayName, displayName, StringComparison.Ordinal))
                    return option.InternalName;
            }

            return displayName;
        }
    }
}
