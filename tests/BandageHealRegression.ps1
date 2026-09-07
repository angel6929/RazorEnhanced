param([string]$SourcePath = (Join-Path $PSScriptRoot '..\Razor\RazorEnhanced\BandageHeal.cs'))

$ErrorActionPreference = 'Stop'

# Run the production methods unchanged, with controllable packet/UI boundaries.
# No game client, network connection, settings files, or healing actions are used.
$source = [System.IO.File]::ReadAllText($SourcePath)
$autoStart = $source.IndexOf('        internal static void AutoRun()')
$autoEnd = $source.IndexOf('        // Funzioni da script', $autoStart)
$engineStart = $source.IndexOf('        internal static void EngineRun(Assistant.Mobile target)')
$engineEnd = $source.IndexOf('        internal static ManualResetEventSlim', $engineStart)
if ($autoStart -lt 0 -or $autoEnd -lt 0 -or $engineStart -lt 0 -or $engineEnd -lt 0) {
    throw 'Could not locate the complete production bandage methods.'
}
$autoRun = $source.Substring($autoStart, $autoEnd - $autoStart)
$engineRun = $source.Substring($engineStart, $engineEnd - $engineStart)
$harness = @'
using System;
using System.Collections.Generic;
using Assistant;

namespace Assistant
{
    public struct Point2D { public int X, Y; public Point2D(int x, int y) { X = x; Y = y; } }
    public enum BuffIcon { MortalStrike }
    public class Mobile
    {
        public int Serial;
        public ushort Hits;
        public ushort MaximumHits = 100;
        public Queue<ushort> MaximumUpdates;
        public int MaximumReads;
        public ushort HitsMax
        {
            get
            {
                MaximumReads++;
                if (MaximumUpdates != null && MaximumUpdates.Count > 0)
                    MaximumHits = MaximumUpdates.Dequeue();
                return MaximumHits;
            }
        }
        public bool Poisoned;
        public Point2D Position = new Point2D(100, 100);
    }
    public class PlayerData : Mobile
    {
        public bool IsGhost;
        public bool Visible = true;
        public Dictionary<BuffIcon, bool> Buffs = new Dictionary<BuffIcon, bool>();
    }
    public static class Client { public static bool Running = true; }
    public static class World
    {
        public static PlayerData CurrentPlayer;
        public static Action AfterPlayerRead;
        public static PlayerData Player
        {
            get
            {
                PlayerData player = CurrentPlayer;
                Action callback = AfterPlayerRead;
                AfterPlayerRead = null;
                callback?.Invoke();
                return player;
            }
        }
        public static Dictionary<int, Mobile> Mobiles = new Dictionary<int, Mobile>();
        public static Mobile FindMobile(int serial) => Mobiles.TryGetValue(serial, out Mobile value) ? value : null;
    }
    public static class Utility
    {
        public static bool InRange(Point2D a, Point2D b, int range) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= range;
    }
}
namespace RazorEnhanced
{
    public class Mobile
    {
        public Assistant.Mobile Value;
        public int Hits => Value.Hits;
        public int HitsMax => Value.HitsMax;
        public int Serial => Value.Serial;
    }
    public static class Mobiles
    {
        public class Filter { public bool Enabled; public int Friend; public int RangeMax; }
        public static Mobile Friend;
        public static Action DuringSelection;
        public static List<Mobile> ApplyFilter(Filter filter) => new List<Mobile>();
        public static Mobile Select(List<Mobile> values, string method)
        {
            DuringSelection?.Invoke();
            return Friend;
        }
    }
    public static class Settings
    {
        public static class General
        {
            public static string Mode = "Friend Or Self";
            public static HashSet<string> Enabled = new HashSet<string>();
            public static string ReadString(string key) => Mode;
            public static bool ReadBool(string key) => Enabled.Contains(key);
        }
    }
    public static class Player { public static int Serial => World.Player.Serial; }
    public static class BandageHeal
    {
        private static int m_maxrange = 12;
        private static int m_hplimit = 95;
        private static int TargetSerial = 2;
        public static Assistant.Mobile Healed;
        public static void Heal(Assistant.Mobile target, bool wait) { Healed = target; }
__AUTO_RUN__
__ENGINE_RUN__
    }
    public static class BandageHealRegression
    {
        private static int passed, failed;
        private static PlayerData self;
        private static Assistant.Mobile friend;
        private static void Reset()
        {
            Client.Running = true;
            self = new PlayerData { Serial = 1, Hits = 60 };
            friend = new Assistant.Mobile { Serial = 2, Hits = 30 };
            World.CurrentPlayer = self;
            World.AfterPlayerRead = null;
            World.Mobiles.Clear();
            World.Mobiles.Add(1, self);
            World.Mobiles.Add(2, friend);
            Mobiles.Friend = new Mobile { Value = friend };
            Mobiles.DuringSelection = null;
            Settings.General.Mode = "Friend Or Self";
            Settings.General.Enabled.Clear();
            BandageHeal.Healed = null;
        }
        private static void Expect(Assistant.Mobile expected)
        {
            if (!ReferenceEquals(expected, BandageHeal.Healed))
                throw new Exception("Wrong healing target.");
        }
        private static void Check(string name, Action test)
        {
            Reset();
            try { test(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.GetType().Name + " " + ex.Message); }
        }
        public static int Run()
        {
            Check("friend weaker", () => { BandageHeal.AutoRun(); Expect(friend); });
            Check("self weaker", () => { self.Hits = 10; BandageHeal.AutoRun(); Expect(self); });
            Check("equal health prefers self", () => { self.Hits = 30; BandageHeal.AutoRun(); Expect(self); });
            Check("self unknown maximum", () => { self.MaximumHits = 0; BandageHeal.AutoRun(); Expect(friend); });
            Check("friend unknown maximum", () => { friend.MaximumHits = 0; BandageHeal.AutoRun(); Expect(self); });
            Check("both unknown maximum", () => { self.Hits = 0; self.MaximumHits = 0; friend.MaximumHits = 0; BandageHeal.AutoRun(); Expect(self); });
            Check("friend maximum changes after read", () => { friend.MaximumUpdates = new Queue<ushort>(new ushort[] { 100, 0 }); BandageHeal.AutoRun(); Expect(null); });
            Check("engine maximum changes after read", () => { friend.MaximumUpdates = new Queue<ushort>(new ushort[] { 100, 0 }); BandageHeal.EngineRun(friend); Expect(friend); if (friend.MaximumReads != 1) throw new Exception("Maximum was read twice."); });
            Check("engine zero maximum", () => { friend.Hits = 0; friend.MaximumHits = 0; BandageHeal.EngineRun(friend); Expect(friend); });
            Check("no friends", () => { Mobiles.Friend = null; BandageHeal.AutoRun(); Expect(self); });
            Check("no player", () => { World.CurrentPlayer = null; BandageHeal.AutoRun(); Expect(null); });
            Check("player clears after initial read", () => { World.AfterPlayerRead = () => World.CurrentPlayer = null; BandageHeal.AutoRun(); Expect(null); });
            Check("player clears during selection", () => { Mobiles.DuringSelection = () => World.CurrentPlayer = null; BandageHeal.AutoRun(); Expect(null); });
            Check("player replaced during selection", () => { Mobiles.DuringSelection = () => World.CurrentPlayer = new PlayerData { Serial = 3 }; BandageHeal.AutoRun(); Expect(null); });
            Check("client stopped", () => { Client.Running = false; BandageHeal.AutoRun(); Expect(null); });
            Check("ghost", () => { self.IsGhost = true; BandageHeal.AutoRun(); Expect(null); });
            Check("self mode", () => { Settings.General.Mode = "Self"; BandageHeal.AutoRun(); Expect(self); });
            Check("target mode", () => { Settings.General.Mode = "Target"; BandageHeal.AutoRun(); Expect(friend); });
            Check("friend mode", () => { Settings.General.Mode = "Friend"; BandageHeal.AutoRun(); Expect(friend); });
            Check("friend leaves world", () => { Settings.General.Mode = "Friend"; World.Mobiles.Remove(2); BandageHeal.AutoRun(); Expect(null); });
            Check("target out of range", () => { friend.Position = new Point2D(113, 100); BandageHeal.AutoRun(); Expect(null); });
            Check("target at range edge", () => { friend.Position = new Point2D(112, 100); BandageHeal.AutoRun(); Expect(friend); });
            Check("healthy targets", () => { self.Hits = 100; friend.Hits = 100; BandageHeal.AutoRun(); Expect(null); });
            Check("poison still heals", () => { friend.Hits = 100; friend.Poisoned = true; BandageHeal.EngineRun(friend); Expect(friend); });
            Check("hidden block", () => { self.Visible = false; Settings.General.Enabled.Add("BandageHealhiddedCheckBox"); BandageHeal.EngineRun(friend); Expect(null); });
            Check("poison block", () => { friend.Poisoned = true; Settings.General.Enabled.Add("BandageHealpoisonCheckBox"); BandageHeal.EngineRun(friend); Expect(null); });
            Check("mortal self block", () => { self.Buffs.Add(BuffIcon.MortalStrike, true); Settings.General.Enabled.Add("BandageHealmortalCheckBox"); BandageHeal.EngineRun(self); Expect(null); });
            Check("engine player missing", () => { World.CurrentPlayer = null; BandageHeal.EngineRun(friend); Expect(null); });
            Check("mortal block keeps player snapshot", () => { self.Buffs.Add(BuffIcon.MortalStrike, true); World.AfterPlayerRead = () => World.CurrentPlayer = null; Settings.General.Enabled.Add("BandageHealmortalCheckBox"); BandageHeal.EngineRun(self); Expect(null); });
            Console.WriteLine("BANDAGE_REGRESSION passed=" + passed + " failed=" + failed);
            return failed;
        }
    }
}
'@
$harness = $harness.Replace('__AUTO_RUN__', $autoRun).Replace('__ENGINE_RUN__', $engineRun)
Add-Type -TypeDefinition $harness -Language CSharp
if ([RazorEnhanced.BandageHealRegression]::Run() -ne 0) { exit 1 }
