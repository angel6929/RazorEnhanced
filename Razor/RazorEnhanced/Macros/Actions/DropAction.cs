using System;

namespace RazorEnhanced.Macros.Actions
{
    public class DropAction : MacroAction
    {
        public int Serial { get; set; }
        public int Graphic { get; set; }
        public int Container { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int Amount { get; set; }

        public DropAction() { }

        public DropAction(uint serial, uint container, ushort x, ushort y, sbyte z, int amount = 0)
        {
            Serial = (int)serial;
            Container = unchecked((int)container); // Preserve 0xFFFFFFFF
            X = (short)x; // Cast to signed to handle 0xFFFF as -1
            Y = (short)y;
            Z = z;
            Amount = amount; // Now captures the actual amount being moved
        }

        public DropAction(int serial, int container, int x, int y, int z, int amount = 0)
        {
            Serial = serial;
            Container = container;
            X = x;
            Y = y;
            Z = z;
            Amount = amount;
        }

        public override string GetActionName() => "Drop";

        public override void Execute()
        {
            if (!IsValid()) return;

            var backpack = Graphic != 0 && Assistant.World.Player != null ? Player.Backpack : null;
            var item = Graphic == 0
                ? Items.FindBySerial(Serial)
                : backpack == null ? null : Items.FindByID(Graphic, -1, backpack.Serial, true);
            if (item == null)
            {
                Misc.SendMessage(Graphic == 0 ? $"Item 0x{Serial:X8} not found" : $"Item type 0x{Graphic:X4} not found in backpack", 33);
                return;
            }

            var destination = Container == -1 ? null : Items.FindBySerial(Container);
            if (destination != null && (destination.Serial == item.Serial || destination.IsChildOf(item)))
            {
                Misc.SendMessage("Cannot drop an item into itself or a container it holds", 33);
                return;
            }

            // Determine amount to move (0 means all)
            int amountToMove = (Amount <= 0) ? item.Amount : Math.Min(Amount, item.Amount);

            // Match ScriptRecorder pattern: if dest != 0xFFFFFFFF, it's a container drop
            if (Container != unchecked((int)0xFFFFFFFF))
            {
                // Dropping into a container
                if (X == -1 && Y == -1)
                {
                    // Auto-position in container
                    Items.Move(item.Serial, Container, amountToMove);
                }
                else
                {
                    // Specific position in container
                    Items.Move(item.Serial, Container, amountToMove, X, Y);
                }
            }
            else
            {
                // Dropping on ground (0xFFFFFFFF)
                Items.MoveOnGround(item.Serial, amountToMove, X, Y, Z);
                //Items.DropItemGroundSelf(Serial, amountToMove);
            }
        }


        public override int GetDelay() => 650; // Standard item movement delay

        public override string Serialize()
        {
            string data = $"Drop|0x{Serial:X8}|0x{Container:X8}|{X}|{Y}|{Z}|{Amount}";
            return Graphic == 0 ? data : $"{data}|{Graphic}";
        }

        public override void Deserialize(string data)
        {
            Graphic = 0;
            var parts = data.Split('|');
            if (parts.Length >= 6)
            {
                string hexSerial = parts[1].Replace("0x", "");
                string hexContainer = parts[2].Replace("0x", "");
                Serial = Convert.ToInt32(hexSerial, 16);
                Container = Convert.ToInt32(hexContainer, 16);
                int.TryParse(parts[3], out int x);
                int.TryParse(parts[4], out int y);
                int.TryParse(parts[5], out int z);
                X = x;
                Y = y;
                Z = z;

                // Amount is optional for backwards compatibility
                if (parts.Length >= 7)
                {
                    int.TryParse(parts[6], out int amt);
                    Amount = amt;
                }

                if (parts.Length >= 8 && int.TryParse(parts[7], out int graphic))
                {
                    Graphic = graphic;
                }
            }
        }

        public override bool IsValid()
        {
            return Graphic == 0 ? Serial != 0 : Graphic > 0 && Graphic <= ushort.MaxValue;
        }
    }
}
