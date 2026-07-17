using System;
using System.Collections.Generic;

namespace Assistant
{
    /// <summary>
    /// 宏条件下拉框的显示项。Value 始终保留英文内部值，避免汉化影响宏的保存与执行。
    /// </summary>
    internal sealed class LocalizedMacroOption
    {
        internal LocalizedMacroOption(string value, string displayText)
        {
            Value = value;
            DisplayText = displayText;
        }

        internal string Value { get; }
        internal string DisplayText { get; }

        public override string ToString()
        {
            return DisplayText;
        }
    }

    internal static class MacroOptionLocalizer
    {
        // 与 ClassicUO 技能窗口使用同一套中文术语。
        private static readonly Dictionary<string, string> s_skillNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Alchemy", "炼金术" },
                { "Anatomy", "解剖学" },
                { "AnimalLore", "动物学" },
                { "AnimalTaming", "驯兽" },
                { "Archery", "弓箭" },
                { "ArmsLore", "武器学" },
                { "Begging", "乞讨" },
                { "Blacksmith", "锻造" },
                { "Blacksmithy", "锻造" },
                { "Bowcraft", "弓箭制作" },
                { "Bowcraft/Fletching", "弓箭制作" },
                { "Bushido", "武士道" },
                { "Camping", "露营" },
                { "Carpentry", "木工" },
                { "Cartography", "制图" },
                { "Chivalry", "骑士精神" },
                { "Cooking", "烹饪" },
                { "DetectHidden", "侦测隐形" },
                { "DetectingHidden", "侦测隐形" },
                { "Discordance", "煽动" },
                { "EvalInt", "评估智力" },
                { "EvaluatingIntelligence", "评估智力" },
                { "Fencing", "刺剑" },
                { "Fishing", "钓鱼" },
                { "Fletching", "弓箭制作" },
                { "Focus", "专注" },
                { "Forensics", "法医鉴定" },
                { "ForensicEvaluation", "法医鉴定" },
                { "Healing", "治疗" },
                { "Herding", "放牧" },
                { "Hiding", "隐藏" },
                { "Imbuing", "注魔" },
                { "Inscribe", "抄写" },
                { "Inscription", "抄写" },
                { "ItemID", "物品鉴定" },
                { "ItemIdentification", "物品鉴定" },
                { "Lockpicking", "开锁" },
                { "Lumberjacking", "伐木" },
                { "Macefighting", "钝器" },
                { "Magery", "魔法" },
                { "MagicResist", "魔法抗性" },
                { "Meditation", "冥想" },
                { "Mining", "采矿" },
                { "Musicianship", "音乐" },
                { "Mysticism", "秘法" },
                { "Necromancy", "死灵法术" },
                { "Ninjitsu", "忍术" },
                { "Parry", "招架" },
                { "Parrying", "招架" },
                { "Peacemaking", "安抚" },
                { "Poisoning", "下毒" },
                { "Provocation", "挑拨" },
                { "RemoveTrap", "解除陷阱" },
                { "ResistingSpells", "魔法抗性" },
                { "Snooping", "窥探" },
                { "Spellweaving", "集成咒文" },
                { "SpiritSpeak", "通灵" },
                { "Stealing", "偷窃" },
                { "Stealth", "潜行" },
                { "Swords", "剑术" },
                { "Swordsmanship", "剑术" },
                { "Tactics", "战术" },
                { "Tailoring", "裁缝" },
                { "TasteID", "品尝鉴定" },
                { "TasteIdentification", "品尝鉴定" },
                { "Tinkering", "工匠" },
                { "Tracking", "追踪" },
                { "Veterinary", "兽医" },
                { "Wrestling", "格斗" },
                { "Throwing", "投掷" }
            };

        private static readonly Dictionary<string, string> s_buffNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Agility", "敏捷术" },
                { "Animal Form", "动物形态" },
                { "Anticipate Hit", "预判攻击" },
                { "Arcane Enpowerment", "奥术强化" },
                { "Arcane Enpowerment (new)", "奥术强化（新版）" },
                { "Arch Protection", "群体防护" },
                { "Armor Pierce", "穿甲" },
                { "Attunement", "武器调谐" },
                { "Aura of Nausea", "恶心光环" },
                { "Barako Draft Of Might", "巴拉科力量药剂" },
                { "Barrab Hemolymph Concentrate", "巴拉布血淋巴浓缩液" },
                { "Berserk", "狂暴" },
                { "Bleed", "流血" },
                { "Bless", "祝福" },
                { "Bload Oath (caster)", "血誓（施法者）" },
                { "Bload Oath (curse)", "血誓（受术者）" },
                { "Block", "格挡" },
                { "BloodWorm Anemia", "血虫贫血症" },
                { "Boarding", "登船作战" },
                { "Bodyguard", "贴身护卫" },
                { "Called Shot", "精准射击" },
                { "City Trade Deal", "城市贸易协定" },
                { "Clumsy", "笨拙" },
                { "Combat Training", "战斗训练" },
                { "Conduit", "导流" },
                { "Confidence", "自信" },
                { "Consecrate Weapon", "神圣武器" },
                { "Corpse Skin", "尸皮术" },
                { "Counter Attack", "反击" },
                { "Criminal", "犯罪状态" },
                { "Cunning", "聪慧术" },
                { "Curse", "诅咒" },
                { "Curse Weapon", "诅咒武器" },
                { "Death Ray", "死亡射线" },
                { "Death Ray Debuff", "死亡射线（减益）" },
                { "Death Strike", "死亡一击" },
                { "Defense Mastery", "防御精通" },
                { "Despair", "绝望" },
                { "Despair (target)", "绝望（目标）" },
                { "Disarm (new)", "缴械（新版）" },
                { "Disguised", "伪装" },
                { "Dismount Prevention", "禁止骑乘" },
                { "Divine Fury", "神圣狂怒" },
                { "Dragon Slasher Fear", "屠龙者恐惧" },
                { "Dragon Turtle Debuff", "龙龟减益" },
                { "Elemental Fury", "元素狂怒" },
                { "Elemental Fury Debuff", "元素狂怒（减益）" },
                { "Enchant", "附魔" },
                { "Enchanted Summoning", "强化召唤" },
                { "Enemy Of One", "唯一之敌" },
                { "Enemy Of One (debuf)", "唯一之敌（减益）" },
                { "Essence Of Wind", "风之精华" },
                { "Ethereal Burst", "灵体爆发" },
                { "Ethereal Voyage", "灵体旅程" },
                { "Evasion", "闪避" },
                { "Evil Omen", "邪恶预兆" },
                { "Faction Loss", "阵营损失" },
                { "Fan Dancer Fan Fire", "扇舞者扇火" },
                { "Feeble Mind", "弱智术" },
                { "Feint", "佯攻" },
                { "Fists Of Fury", "狂怒之拳" },
                { "Fly", "飞行" },
                { "Focused Eye", "专注之眼" },
                { "Force Arrow", "强力箭" },
                { "Gaze Despair", "绝望凝视" },
                { "Gift Of Life", "生命恩赐" },
                { "Gift Of Renewal", "复苏恩赐" },
                { "Grapes of Wrath", "愤怒葡萄" },
                { "Hawl Of Cacophony", "嘈杂怒嚎" },
                { "Healing", "治疗" },
                { "Heat Of Battle", "战斗热潮" },
                { "Heightened Senses", "感官强化" },
                { "Hiding", "隐藏" },
                { "Hiryu Physical Malus", "飞龙物理抗性削弱" },
                { "Hit Dual Wield", "双持命中" },
                { "Hit Lower Attack", "降低攻击命中" },
                { "Hit Lower Defense", "降低防御命中" },
                { "Honorable Execution", "荣耀处决" },
                { "Honored", "受尊敬" },
                { "Horrific Beast", "恐怖野兽" },
                { "Immolating Weapon", "献祭武器" },
                { "Incognito", "易容术" },
                { "Injected Strike", "注毒打击" },
                { "Injected Strike Debuff", "注毒打击（减益）" },
                { "Inspire", "激励" },
                { "Intuition", "直觉" },
                { "Invigorate", "振奋" },
                { "Invisibility", "隐形术" },
                { "Jukari Burn Poiltice", "朱卡里灼伤药膏" },
                { "Knockout", "击倒" },
                { "Kurak Ambushers Essence", "库拉克伏击者精华" },
                { "Lich Form", "巫妖形态" },
                { "Lightning Strike", "雷霆一击" },
                { "Magic Fish", "魔法鱼效果" },
                { "Magic Reflection", "魔法反射" },
                { "Mana Phase", "法力相位" },
                { "Mana Shield", "法力护盾" },
                { "Mass Curse", "群体诅咒" },
                { "Meditation", "冥想" },
                { "Medusa Stone", "美杜莎石化" },
                { "Mind Rot", "心智腐化" },
                { "Momentum Strike", "动量打击" },
                { "Mortal Strike", "致命一击" },
                { "Mystic Weapon", "秘法武器" },
                { "Night Sight", "夜视术" },
                { "NoRearm", "禁止重新装备" },
                { "Onslaught", "猛攻" },
                { "Orange Petals", "橙色花瓣" },
                { "Pain Spike", "痛苦尖刺" },
                { "Paralyze", "麻痹" },
                { "Perfection", "完美" },
                { "Perseverance", "坚毅" },
                { "Pierce", "穿刺" },
                { "Playing The Odds", "孤注一掷" },
                { "Playing The Odds Debuff", "孤注一掷（减益）" },
                { "Poison", "中毒" },
                { "Poison Resistance", "毒素免疫" },
                { "Polymorph", "变形术" },
                { "Potency", "效能" },
                { "Protection", "防护术" },
                { "Psychic Attack", "心灵攻击" },
                { "Rage", "狂怒" },
                { "Rage Focusing", "聚焦狂怒" },
                { "Rage Focusing (target)", "聚焦狂怒（目标）" },
                { "Rampage", "横冲直撞" },
                { "Reactive Armor", "反应护甲" },
                { "Reaper Form", "收割者形态" },
                { "Resilience", "韧性" },
                { "Rose Of Trinsic", "特林西克玫瑰" },
                { "Rotworm Blood Disease", "腐虫血液病" },
                { "Rune Beetle Corruption", "符文甲虫腐化" },
                { "Sakkhra Prophylaxis", "萨克拉预防剂" },
                { "Saving Throw", "豁免" },
                { "Shadow", "暗影" },
                { "Shield Bash", "盾击" },
                { "Skill Use Delay", "技能使用延迟" },
                { "Sleep", "睡眠" },
                { "Spell Focusing", "法术聚焦" },
                { "Spell Focusing (target)", "法术聚焦（目标）" },
                { "Spell Plague", "法术瘟疫" },
                { "Splintering Effect", "碎裂效果" },
                { "Stagger", "踉跄" },
                { "Stone Form", "石化形态" },
                { "Strangle", "绞杀" },
                { "Strength", "力量术" },
                { "Surge", "涌动" },
                { "Swing Speed", "攻击速度" },
                { "Talon Strike", "利爪打击" },
                { "Thrust", "突刺" },
                { "Thrust Debuff", "突刺（减益）" },
                { "Tolerance", "耐受" },
                { "Toughness", "强韧" },
                { "Tribulation", "磨难" },
                { "TribulationTarget", "磨难（目标）" },
                { "Unknown Tomato", "未知番茄效果" },
                { "Urali Trance Tonic,", "乌拉利入神药剂" },
                { "Vampiric Embrace", "吸血鬼拥抱" },
                { "Veterinary", "兽医" },
                { "Warcry", "战吼" },
                { "Weaken", "虚弱术" },
                { "Whispering", "低语" },
                { "White Tiger Form", "白虎形态" },
                { "Wraith Form", "幽魂形态" }
            };

        internal static string LocalizeSkill(string value)
        {
            return Localize(s_skillNames, value);
        }

        internal static string LocalizeBuff(string value)
        {
            return Localize(s_buffNames, value);
        }

        private static string Localize(Dictionary<string, string> names, string value)
        {
            if (String.IsNullOrEmpty(value))
                return value;

            string translated;
            return names.TryGetValue(value, out translated) ? translated : value;
        }
    }
}
