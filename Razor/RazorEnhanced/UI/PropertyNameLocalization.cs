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
            new PropertyOption("Cold Resist", "寒冷抗性"),
            new PropertyOption("Damage Increase", "攻击伤害增加"),
            new PropertyOption("Defense Chance Increase", "防御机率增加"),
            new PropertyOption("Dexterity Bonus", "敏捷加权"),
            new PropertyOption("Energy Resists", "能量抗性"),
            new PropertyOption("Faster Cast Recovery", "快速施法回复"),
            new PropertyOption("Enhance Potion", "药效增强"),
            new PropertyOption("Energy Damage", "能量伤害"),
            new PropertyOption("Poison Damage", "毒物伤害"),
            new PropertyOption("Fire Damage", "火焰伤害"),
            new PropertyOption("Cold Damage", "寒冷伤害"),
            new PropertyOption("Physical Damage", "物理伤害"),
            new PropertyOption("Faster Casting", "快速施法"),
            new PropertyOption("Gold Increase", "金币增加"),
            new PropertyOption("Fire Resist", "火焰抗性"),
            new PropertyOption("Hit Chance Increase", "命中机率增加"),
            new PropertyOption("Hit Energy Area", "能量伤害范围"),
            new PropertyOption("Hit Dispel", "消除术攻击"),
            new PropertyOption("Hit Cold Area", "寒冷伤害范围"),
            new PropertyOption("Hit Fire Area", "火焰伤害范围"),
            new PropertyOption("Hit Fireball", "火球术攻击"),
            new PropertyOption("Hit Life Leech", "吸取生命攻击"),
            new PropertyOption("Hit Point Increase", "生命值增加"),
            new PropertyOption("Hit Point Regeneration", "生命值回复"),
            new PropertyOption("Hit Stamina Leech", "吸取精力攻击"),
            new PropertyOption("Hit Poison Area", "毒物伤害范围"),
            new PropertyOption("Hit Physical Area", "物理攻击范围"),
            new PropertyOption("Hit Mana Leech", "吸取魔法攻击"),
            new PropertyOption("Hit Magic Arrow", "魔法箭攻击"),
            new PropertyOption("Hit Lower Defense", "降低对方防御"),
            new PropertyOption("Hit Lower Attack", "降低对方攻击"),
            new PropertyOption("Hit Lightning", "闪电术攻击"),
            new PropertyOption("Hit Harm", "伤害术攻击"),
            new PropertyOption("Intelligence Bonus", "智力加权"),
            new PropertyOption("Lower Mana Cost", "降低魔法消耗"),
            new PropertyOption("Lower Reagent Cost", "降低药材消耗"),
            new PropertyOption("Lower Requirements", "降低需求"),
            new PropertyOption("Luck", "幸运"),
            new PropertyOption("Mana Increase", "魔法值增加"),
            new PropertyOption("Mana Regeneration", "魔法值回复"),
            new PropertyOption("Physical Resist", "物理抗性"),
            new PropertyOption("Poison Resist", "毒物抗性"),
            new PropertyOption("Night Sight", "夜视术"),
            new PropertyOption("Spell Channeling", "魔法转移"),
            new PropertyOption("Spell Damage Increase", "魔法攻击增加"),
            new PropertyOption("Splintering Weapon", "碎裂武器"),
            new PropertyOption("Stamina Increase", "精力值增加"),
            new PropertyOption("Stamina Regeneration", "精力值回复"),
            new PropertyOption("Swing Speed Increase", "攻击速度增加"),
            new PropertyOption("Velocity", "速度"),
            new PropertyOption("Self Repair", "自行修复"),
            new PropertyOption("Reflect Physical Damage", "物理伤害反射"),
            new PropertyOption("Mage Armor", "法师防具"),
            new PropertyOption("Strength Bonus", "力量加权"),
            new PropertyOption("Antique", "古董"),
            new PropertyOption("Battle Lust", "斗志"),
            new PropertyOption("Brittle", "不可强化"),
            new PropertyOption("Casting Focus", "施法集中"),
            new PropertyOption("Chaos Damage", "混沌伤害"),
            new PropertyOption("Cold Eater", "寒冷吞噬"),
            new PropertyOption("Cursed", "[被诅咒]"),
            new PropertyOption("Damage Eater", "伤害吞噬"),
            new PropertyOption("Durability", "耐用度"),
            new PropertyOption("Energy Eater", "能量吞噬"),
            new PropertyOption("Fire Eater", "火焰吞噬"),
            new PropertyOption("Gargoyles Only", "只限翼魔"),
            new PropertyOption("Greater Artifact", "中级工艺品"),
            new PropertyOption("Hit Fatigue", "疲劳攻击"),
            new PropertyOption("Hit Mana Drain", "魔法耗弱攻击"),
            new PropertyOption("Kinetic Eater", "物理吞噬"),
            new PropertyOption("Legendary Artifact", "传奇工艺品"),
            new PropertyOption("Lesser Artifact", "次级工艺品"),
            new PropertyOption("Lesser Magic Item", "魔法物品(中级)"),
            new PropertyOption("Mage Weapon", "法师武器"),
            new PropertyOption("Major Artifact", "高级工艺品"),
            new PropertyOption("Major Magic Item", "魔法物品(顶级)"),
            new PropertyOption("Mana Phase", "法力阶段"),
            new PropertyOption("Minor Magic Item", "魔法物品(低级)"),
            new PropertyOption("Poison Eater", "毒物吞噬"),
            new PropertyOption("Prized", "珍贵的"),
            new PropertyOption("Reactive Paralyze", "反应性麻痹"),
            new PropertyOption("Skill Bonus", "技能加成"),
            new PropertyOption("Soul Charge", "灵魂冲锋"),
            new PropertyOption("Use Best Weapon Skill", "使用最佳武器技能"),
            new PropertyOption("Water Elemental Slayer", "水元素屠杀者"),
            new PropertyOption("Troll Slayer", "巨人屠杀者"),
            new PropertyOption("Undead Slayer", "不死生物屠杀者"),
            new PropertyOption("Terathan Slayer", "蜘蛛人屠杀者"),
            new PropertyOption("Spider Slayer", "蜘蛛屠杀者"),
            new PropertyOption("Snow Elemental Slayer", "雪元素屠杀者"),
            new PropertyOption("Snake Slayer", "蛇屠杀者"),
            new PropertyOption("Scorpion Slayer", "蠍子屠杀者"),
            new PropertyOption("Reptile Slayer", "爬虫屠杀者"),
            new PropertyOption("Repond Slayer", "灭人屠杀者"),
            new PropertyOption("Poison Elemental Slayer", "毒元素屠杀者"),
            new PropertyOption("Orc Slayer", "半兽人屠杀者"),
            new PropertyOption("Ophidian Slayer", "蛇人屠杀者"),
            new PropertyOption("Ogre Slayer", "食人魔屠杀者"),
            new PropertyOption("Lizardman Slayer", "蜥蜴人屠杀者"),
            new PropertyOption("Gargoyle Slayer", "翼魔屠杀者"),
            new PropertyOption("Fire Elemental Slayer", "火元素屠杀者"),
            new PropertyOption("Elemental Slayer", "元素屠杀者"),
            new PropertyOption("Earth Elemental Slayer", "土元素屠杀者"),
            new PropertyOption("Dragon Slayer", "龙屠杀者"),
            new PropertyOption("Demon Slayer", "恶魔屠杀者"),
            new PropertyOption("Blood Elemental Slayer", "血元素屠杀者"),
            new PropertyOption("Arachnid Slayer", "蛛形屠杀者"),
            new PropertyOption("Air Elemental Slayer", "风元素屠杀者"),
            new PropertyOption("Magic Arrow Charges", "魔法箭点数"),
            new PropertyOption("Lightning Charges", "闪电术点数"),
            new PropertyOption("Healing Charges", "治疗术点数"),
            new PropertyOption("Harm Charges", "伤害术点数"),
            new PropertyOption("Greater Healing Charges", "强力医疗点数"),
            new PropertyOption("Fireball Charges", "火球术点数"),
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
