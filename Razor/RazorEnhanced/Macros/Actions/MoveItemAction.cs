using RazorEnhanced;
using System;
using System.Collections.Generic;

namespace RazorEnhanced.Macros.Actions
{
    public class MoveItemAction : MacroAction
    {
        public enum MoveTargetType
        {
            Entity, // Move to container or mobile
            Ground  // Move to ground coordinates
        }

        public enum MoveSourceType
        {
            SerialOrAlias,
            Type
        }

        public MoveTargetType TargetType { get; set; }
        public MoveSourceType SourceType { get; set; }

        // Accepts either a serial (int) or an alias (string)
        public string ItemSerialOrAlias { get; set; }
        public int ItemGraphic { get; set; }
        public int ItemColor { get; set; }
        public string SourceContainerSerialOrAlias { get; set; }
        public int Amount { get; set; }
        public string TargetSerialOrAlias { get; set; } // For Entity
        public int X { get; set; } // Used for both container (optional) and ground
        public int Y { get; set; } // Used for both container (optional) and ground
        public int Z { get; set; } // Only for ground

        public MoveItemAction()
        {
            TargetType = MoveTargetType.Entity;
            SourceType = MoveSourceType.SerialOrAlias;
            ItemSerialOrAlias = "";
            ItemGraphic = 0;
            ItemColor = -1;
            SourceContainerSerialOrAlias = "";
            Amount = 0;
            TargetSerialOrAlias = "";
            X = -1;
            Y = -1;
            Z = 0;
        }

        public MoveItemAction(MoveTargetType targetType, string itemSerialOrAlias, int amount, string targetSerialOrAlias, int x, int y, int z)
        {
            TargetType = targetType;
            SourceType = MoveSourceType.SerialOrAlias;
            ItemSerialOrAlias = itemSerialOrAlias;
            ItemGraphic = 0;
            ItemColor = -1;
            SourceContainerSerialOrAlias = "";
            Amount = amount;
            TargetSerialOrAlias = targetSerialOrAlias;
            X = x;
            Y = y;
            Z = z;
        }

        public override string GetActionName() => "Move Item";

        public override void Execute()
        {
            if (TargetType == MoveTargetType.Entity)
            {
                int targetSerial = ResolveSerialOrAlias(TargetSerialOrAlias, "target");
                if (targetSerial == 0)
                {
                    Misc.SendMessage($"MoveItemAction: Could not resolve target '{TargetSerialOrAlias}'", 33);
                    return;
                }

                int itemSerial = ResolveItemSerial(targetSerial);
                if (itemSerial == 0)
                {
                    return;
                }

                if (X > 0 && Y > 0)
                    Items.Move(itemSerial, targetSerial, Amount, X, Y);
                else
                    Items.Move(itemSerial, targetSerial, Amount);
            }
            else // Ground
            {
                int itemSerial = ResolveItemSerial();
                if (itemSerial == 0)
                {
                    return;
                }

                Items.MoveOnGround(itemSerial, Amount, X, Y, Z);
            }
        }

        private int ResolveItemSerial(int excludeContainerSerial = 0)
        {
            if (SourceType == MoveSourceType.SerialOrAlias)
            {
                int itemSerial = ResolveSerialOrAlias(ItemSerialOrAlias, "item");
                if (itemSerial == 0)
                    Misc.SendMessage($"MoveItemAction: Could not resolve item '{ItemSerialOrAlias}'", 33);
                return itemSerial;
            }

            if (ItemGraphic == 0)
            {
                Misc.SendMessage("MoveItemAction: Item graphic is not set", 33);
                return 0;
            }

            int containerSerial = ResolveSourceContainer();
            if (containerSerial == 0)
                return 0;

            Item item = FindItemByTypeInSource(containerSerial, excludeContainerSerial);
            if (item == null)
            {
                string containerText = containerSerial == -1 ? "world" : $"0x{containerSerial:X8}";
                string colorText = ItemColor == -1 ? "any" : $"0x{ItemColor:X4}";
                Misc.SendMessage($"MoveItemAction: Could not find type 0x{ItemGraphic:X4}, color {colorText}, container {containerText}", 33);
                return 0;
            }

            return item.Serial;
        }

        private Item FindItemByTypeInSource(int containerSerial, int excludeContainerSerial)
        {
            Item item = FindItemByTypeInSource(containerSerial, ItemColor, excludeContainerSerial);
            if (item != null)
                return item;

            if (ItemColor == 0)
                return FindItemByTypeInSource(containerSerial, -1, excludeContainerSerial);

            return null;
        }

        private Item FindItemByTypeInSource(int containerSerial, int color, int excludeContainerSerial)
        {
            var filter = new Items.Filter
            {
                Graphics = new List<int> { ItemGraphic },
                CheckIgnoreObject = true
            };

            if (color != -1)
                filter.Hues = new List<int> { color };

            List<Item> candidates = Items.ApplyFilter(filter);
            Item nestedCandidate = null;
            foreach (Item item in candidates)
            {
                if (item == null || item.Amount == 0)
                    continue;

                if (excludeContainerSerial != 0 && IsItemInExcludedContainer(item, excludeContainerSerial))
                    continue;

                if (containerSerial == -1)
                    return item;

                if (item.Container == containerSerial)
                    return item;

                if (nestedCandidate == null && IsItemInSourceContainer(item, containerSerial))
                    nestedCandidate = item;
            }

            if (nestedCandidate != null)
                return nestedCandidate;

            if (containerSerial != -1)
            {
                Item fallback = Items.FindByID(ItemGraphic, color, containerSerial, true);
                if (fallback != null && (excludeContainerSerial == 0 || !IsItemInExcludedContainer(fallback, excludeContainerSerial)))
                    return fallback;
            }

            return null;
        }

        private bool IsItemInSourceContainer(Item item, int containerSerial)
        {
            if (item.Container == containerSerial || item.RootContainer == containerSerial)
                return true;

            Item container = Items.FindBySerial(containerSerial);
            return container != null && item.IsChildOf(container);
        }

        private bool IsItemInExcludedContainer(Item item, int excludeContainerSerial)
        {
            if (item.Serial == excludeContainerSerial || item.Container == excludeContainerSerial || item.RootContainer == excludeContainerSerial)
                return true;

            Item excludedContainer = Items.FindBySerial(excludeContainerSerial);
            return excludedContainer != null && item.IsChildOf(excludedContainer);
        }

        private int ResolveSourceContainer()
        {
            string source = SourceContainerSerialOrAlias?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(source) || source.Equals("backpack", StringComparison.OrdinalIgnoreCase))
            {
                if (Player.Backpack != null)
                    return Player.Backpack.Serial;

                Misc.SendMessage("MoveItemAction: Backpack not found", 33);
                return 0;
            }

            if (source == "-1" || source.Equals("any", StringComparison.OrdinalIgnoreCase) || source.Equals("world", StringComparison.OrdinalIgnoreCase))
                return -1;

            int containerSerial = ResolveSerialOrAlias(source, "source container");
            if (containerSerial == 0)
                Misc.SendMessage($"MoveItemAction: Could not resolve source container '{source}'", 33);
            return containerSerial;
        }

        // This matches the alias resolution logic from AttackAction
        private int ResolveSerialOrAlias(string serialOrAlias, string context)
        {
            if (string.IsNullOrWhiteSpace(serialOrAlias))
                return 0;

            // Try parse as hex or decimal serial
            if (serialOrAlias.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(serialOrAlias.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int serial))
                    return serial;
            }
            else if (int.TryParse(serialOrAlias, out int serial))
            {
                return serial;
            }

            // Otherwise, treat as alias and resolve via shared value (like AttackAction)
            string aliasKey = serialOrAlias.ToLower();
            if (Misc.CheckSharedValue(aliasKey))
            {
                object aliasValue = Misc.ReadSharedValue(aliasKey);
                if (aliasValue is uint uintVal)
                {
                    return (int)uintVal;
                }
                else if (uint.TryParse(aliasValue.ToString(), out uint parsedVal))
                {
                    return (int)parsedVal;
                }
                else
                {
                    Misc.SendMessage($"MoveItemAction: Invalid alias value for '{serialOrAlias}' ({context})", 33);
                    return 0;
                }
            }
            else
            {
                Misc.SendMessage($"MoveItemAction: Alias '{serialOrAlias}' not found ({context})", 33);
                return 0;
            }
        }

        public override int GetDelay() => 250; // Standard macro delay

        public override string Serialize()
        {
            return $"MoveItem|{TargetType}|{Escape(ItemSerialOrAlias)}|{Amount}|{Escape(TargetSerialOrAlias)}|{X}|{Y}|{Z}|{SourceType}|{ItemGraphic}|{ItemColor}|{Escape(SourceContainerSerialOrAlias)}";
        }

        public override void Deserialize(string data)
        {
            var parts = data.Split('|');
            if (parts.Length < 8) return;

            Enum.TryParse(parts[1], out MoveTargetType targetType);
            TargetType = targetType;
            ItemSerialOrAlias = Unescape(parts[2]);
            int.TryParse(parts[3], out int amount);
            Amount = amount;
            TargetSerialOrAlias = Unescape(parts[4]);
            int.TryParse(parts[5], out int x);
            X = x;
            int.TryParse(parts[6], out int y);
            Y = y;
            int.TryParse(parts[7], out int z);
            Z = z;

            SourceType = MoveSourceType.SerialOrAlias;
            ItemGraphic = 0;
            ItemColor = -1;
            SourceContainerSerialOrAlias = "";

            if (parts.Length >= 12)
            {
                if (Enum.TryParse(parts[8], out MoveSourceType sourceType))
                    SourceType = sourceType;
                int.TryParse(parts[9], out int itemGraphic);
                ItemGraphic = itemGraphic;
                int.TryParse(parts[10], out int itemColor);
                ItemColor = itemColor;
                SourceContainerSerialOrAlias = Unescape(parts[11]);
            }
        }

        private string Escape(string value)
        {
            return value?.Replace("|", "%7C") ?? "";
        }

        private string Unescape(string value)
        {
            return value?.Replace("%7C", "|") ?? "";
        }
    }
}
