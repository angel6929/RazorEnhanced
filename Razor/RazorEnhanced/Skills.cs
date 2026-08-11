using Assistant;
using System;
using System.Collections.Generic;
using System.Text;

namespace RazorEnhanced
{
    internal class Skills
    {
        internal static Dictionary<int, string> m_SkillNameById = null;
        internal static Dictionary<string, int> m_SkillNameByName = null;
        private static Dictionary<string, int> m_SkillIdByNormalizedName = null;
        private static Dictionary<string, int> m_SkillIdByAlias = null;

        internal static int GetSkillId(string skillName)
        {
            if (string.IsNullOrWhiteSpace(skillName))
                return -1;

            if (m_SkillNameByName != null && m_SkillNameByName.TryGetValue(skillName, out int skillId))
                return skillId;

            string normalizedName = NormalizeSkillName(skillName);
            if (m_SkillIdByNormalizedName != null && m_SkillIdByNormalizedName.TryGetValue(normalizedName, out skillId))
                return skillId;

            if (m_SkillIdByAlias != null && m_SkillIdByAlias.TryGetValue(normalizedName, out skillId))
                return skillId;

            return -1;
        }

        internal static string NormalizeSkillName(string skillName)
        {
            if (string.IsNullOrWhiteSpace(skillName))
                return string.Empty;

            StringBuilder normalizedName = new StringBuilder(skillName.Length);
            foreach (char character in skillName)
            {
                if (char.IsLetterOrDigit(character))
                    normalizedName.Append(character);
            }
            return normalizedName.ToString();
        }

        private static void AddNormalizedSkillName(Dictionary<string, int> names, string skillName, int skillId)
        {
            string normalizedName = NormalizeSkillName(skillName);
            if (!string.IsNullOrEmpty(normalizedName) && !names.ContainsKey(normalizedName))
                names.Add(normalizedName, skillId);
        }

        internal static string GetSkillName(int skillId)
        {
            if (m_SkillNameById != null && m_SkillNameById.TryGetValue(skillId, out string skillName))
                return skillName;

            return null;
        }

        internal static void InitData()
        {
            var defaults = new List<(int, string)>()
            {
                (0, "Alchemy"),
                (1, "Anatomy"),
                (2, "AnimalLore"),
                (3, "ItemID"),
                (4, "ArmsLore"),
                (5, "Parry"),
                (6, "Begging"),
                (7, "Blacksmith"),
                (8, "Fletching"),
                (9, "Peacemaking"),
                (10, "Camping"),
                (11, "Carpentry"),
                (12, "Cartography"),
                (13, "Cooking"),
                (14, "DetectHidden"),
                (15, "Discordance"),
                (16, "EvalInt"),
                (17, "Healing"),
                (18, "Fishing"),
                (19, "Forensics"),
                (20, "Herding"),
                (21, "Hiding"),
                (22, "Provocation"),
                (23, "Inscribe"),
                (24, "Lockpicking"),
                (25, "Magery"),
                (26, "MagicResist"),
                (27, "Tactics"),
                (28, "Snooping"),
                (29, "Musicianship"),
                (30, "Poisoning"),
                (31, "Archery"),
                (32, "SpiritSpeak"),
                (33, "Stealing"),
                (34, "Tailoring"),
                (35, "AnimalTaming"),
                (36, "TasteID"),
                (37, "Tinkering"),
                (38, "Tracking"),
                (39, "Veterinary"),
                (40, "Swords"),
                (41, "Macing"),
                (42, "Fencing"),
                (43, "Wrestling"),
                (44, "Lumberjacking"),
                (45, "Mining"),
                (46, "Meditation"),
                (47, "Stealth"),
                (48, "RemoveTrap"),
                (49, "Necromancy"),
                (50, "Focus"),
                (51, "Chivalry"),
                (52, "Bushido"),
                (53, "Ninjitsu"),
                (54, "SpellWeaving"),
                (55, "Mysticism"),
                (56, "Imbuing"),
                (57, "Throwing"),
            };

            m_SkillNameById = new Dictionary<int, string>();
            m_SkillNameByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            m_SkillIdByNormalizedName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            m_SkillIdByAlias = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Keep the built-in API names as aliases. skills.mul replaces the display
            // names below, but existing macros and scripts may still use these names.
            foreach (var entry in defaults)
            {
                m_SkillNameById.Add(entry.Item1, entry.Item2);
                AddNormalizedSkillName(m_SkillIdByAlias, entry.Item2, entry.Item1);
            }
            AddNormalizedSkillName(m_SkillIdByAlias, "Inscription", 23);

            // Runtime names from skills.mul take priority over historical aliases.
            foreach (var skill in Ultima.Skills.SkillEntries)
            {
                m_SkillNameById[skill.Index] = skill.Name;
                AddNormalizedSkillName(m_SkillIdByNormalizedName, skill.Name, skill.Index);
            }

            // Use the resulting dictionary to populate the inverse lookup
            foreach (var entry in m_SkillNameById)
            {
                if (!m_SkillNameByName.ContainsKey(entry.Value))
                    m_SkillNameByName.Add(entry.Value, entry.Key);
            }
        }

        internal static string GuessSkillName(string originalName)
        {
            int distance = 99;
            string closest = "";

            foreach (var skill in m_SkillNameById)
            {
                int computeDistance = UOAssist.LevenshteinDistance(skill.Value, originalName);
                if (computeDistance < distance)
                {
                    distance = computeDistance;
                    closest = skill.Value;
                }
            }

            if (distance < 99)
                return closest;
            return originalName;

        }

        internal static int GuessSkillId(string originalName)
        {
            if (string.IsNullOrWhiteSpace(originalName))
                return -1;

            int exactSkillId = GetSkillId(originalName);
            if (exactSkillId != -1)
                return exactSkillId;

            int distance = 99;
            int closest = -1;

            foreach (var skill in m_SkillNameById)
            {
                int computeDistance = UOAssist.LevenshteinDistance(skill.Value, originalName);
                if (computeDistance < distance)
                {
                    distance = computeDistance;
                    closest = skill.Key;
                }
            }

            if (distance < 99)
                return closest;
            return GetSkillId(originalName);

        }
    }
}
