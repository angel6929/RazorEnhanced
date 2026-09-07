param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Win32\Release\RazorEnhanced.exe'),
    [switch]$ExpectLegacyCrash,
    [int]$Repeat = 100
)

$ErrorActionPreference = 'Stop'
if ([IntPtr]::Size -ne 4) {
    throw 'Run with C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -NoProfile -STA -File tests\PreLoginStatePacketRegression.ps1'
}
if ($Repeat -lt 1) { throw 'Repeat must be positive.' }
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path

# Exercise the actual handlers directly, then through the actual viewer dispatcher.
# Test adapters capture callback exceptions before ProcessViewers can call LogCrash.
# This process never initializes a client, logs in, sends packets, or touches settings.
$harness = @'
using System;
using System.Reflection;

public static class PreLoginStatePacketRegression
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static PropertyInfo player, warmode, serialValue, blocked;
    private static FieldInfo combatant;
    private static ConstructorInfo playerConstructor, serialConstructor, readerConstructor, argsConstructor;
    private static MethodInfo warHandler, combatantHandler, moveToData;
    private static MethodInfo registerViewer, removeViewer, onServerPacket, readByte, readUInt32;
    private static Type viewerType;
    private static Exception callbackFailure;
    private static int passed, failed;

    private static object Serial(uint value)
    {
        return serialConstructor.Invoke(new object[] { value });
    }

    private static object NewPlayer(uint value)
    {
        return playerConstructor.Invoke(new object[] { Serial(value) });
    }

    private static void SetPlayer(object value)
    {
        player.SetValue(null, value, null);
    }

    private static uint LastCombatant()
    {
        return (uint)serialValue.GetValue(combatant.GetValue(null), null);
    }

    private static bool Warmode(object value)
    {
        return (bool)warmode.GetValue(value, null);
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Send(MethodInfo handler, byte[] packet)
    {
        object reader = readerConstructor.Invoke(new object[] { packet, false });
        object args = argsConstructor.Invoke(new object[0]);
        moveToData.Invoke(reader, null);
        try
        {
            handler.Invoke(null, new object[] { reader, args });
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException;
        }
        Expect(!(bool)blocked.GetValue(args, null), "The viewer blocked the packet.");
    }

    private static void War(bool enabled)
    {
        Send(warHandler, new byte[] { 0x72, enabled ? (byte)1 : (byte)0, 0, 0x32, 0 });
    }

    private static void Combatant(uint value)
    {
        Send(combatantHandler, new byte[] { 0xAA, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
    }

    private static void WithViewers(int packetId, Action<object, object>[] callbacks, Action test)
    {
        Delegate[] viewers = new Delegate[callbacks.Length];
        try
        {
            for (int i = 0; i < callbacks.Length; i++)
            {
                Action<object, object> callback = callbacks[i];
                Action<object, object> guarded = (reader, args) =>
                {
                    try { callback(reader, args); }
                    catch (Exception ex) { callbackFailure = ex; }
                };
                viewers[i] = Delegate.CreateDelegate(viewerType, guarded.Target, guarded.Method);
                registerViewer.Invoke(null, new object[] { packetId, viewers[i] });
            }
            test();
        }
        finally
        {
            foreach (Delegate viewer in viewers)
            {
                if (viewer != null) removeViewer.Invoke(null, new object[] { packetId, viewer });
            }
        }
    }

    private static Action<object, object> Handler(MethodInfo handler)
    {
        return (reader, args) => handler.Invoke(null, new object[] { reader, args });
    }

    private static bool Dispatch(byte[] packet)
    {
        object reader = readerConstructor.Invoke(new object[] { packet, false });
        callbackFailure = null;
        bool result = (bool)onServerPacket.Invoke(null, new object[] { (int)packet[0], reader, null });
        if (callbackFailure != null) throw new Exception("Viewer callback failed without entering LogCrash.", callbackFailure);
        return result;
    }

    private static byte Byte(object reader)
    {
        return (byte)readByte.Invoke(reader, null);
    }

    private static uint UInt32(object reader)
    {
        return (uint)readUInt32.Invoke(reader, null);
    }

    private static void Check(string name, Action test)
    {
        SetPlayer(null);
        combatant.SetValue(null, Serial(0));
        try
        {
            test();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine("FAIL " + name + ": " + ex.GetType().Name + " " + ex.Message);
        }
        finally
        {
            SetPlayer(null);
        }
    }

    private static void ExpectNullReference(string handlerName, Action send)
    {
        try { send(); }
        catch (NullReferenceException)
        {
            Console.WriteLine("REPRODUCED System.NullReferenceException in " + handlerName + " with World.Player == null");
            return;
        }
        throw new Exception("Expected the legacy null-player crash, but the handler returned.");
    }

    public static int Run(string assemblyPath, bool expectLegacyCrash, int repeat)
    {
        Assembly assembly = Assembly.LoadFrom(assemblyPath);
        Type serialType = assembly.GetType("Assistant.Serial", true);
        Type playerType = assembly.GetType("Assistant.PlayerData", true);
        Type readerType = assembly.GetType("Assistant.PacketReader", true);
        Type argsType = assembly.GetType("Assistant.PacketHandlerEventArgs", true);
        player = assembly.GetType("Assistant.World", true).GetProperty("Player", Static);
        warmode = assembly.GetType("Assistant.Mobile", true).GetProperty("Warmode", Instance);
        serialValue = serialType.GetProperty("Value", Instance);
        blocked = argsType.GetProperty("Block", Instance);
        combatant = assembly.GetType("Assistant.Targeting", true).GetField("m_LastCombatant", Static);
        serialConstructor = serialType.GetConstructor(Instance, null, new Type[] { typeof(uint) }, null);
        playerConstructor = playerType.GetConstructor(Instance, null, new Type[] { serialType }, null);
        readerConstructor = readerType.GetConstructor(Instance, null, new Type[] { typeof(byte[]), typeof(bool) }, null);
        argsConstructor = argsType.GetConstructor(Instance, null, Type.EmptyTypes, null);
        moveToData = readerType.GetMethod("MoveToData", Instance);
        warHandler = assembly.GetType("Assistant.PacketHandlers", true).GetMethod("ServerSetWarMode", Static);
        combatantHandler = assembly.GetType("Assistant.Targeting", true).GetMethod("CombatantChange", Static);
        Type packetHandlerType = assembly.GetType("Assistant.PacketHandler", true);
        viewerType = assembly.GetType("Assistant.PacketViewerCallback", true);
        registerViewer = packetHandlerType.GetMethod("RegisterServerToClientViewer", Static);
        removeViewer = packetHandlerType.GetMethod("RemoveServerToClientViewer", Static);
        onServerPacket = packetHandlerType.GetMethod("OnServerPacket", Static);
        readByte = readerType.GetMethod("ReadByte", Instance);
        readUInt32 = readerType.GetMethod("ReadUInt32", Instance);
        passed = failed = 0;
        Console.WriteLine("ASSEMBLY " + assembly.Location);

        if (expectLegacyCrash)
        {
            Check("legacy 0x72 null-player crash", () => ExpectNullReference("Assistant.PacketHandlers.ServerSetWarMode", () => War(true)));
            Check("legacy 0xAA null-player crash", () => ExpectNullReference("Assistant.Targeting.CombatantChange", () => Combatant(2)));
        }
        else
        {
            Check("0x72 before player creation", () => { War(true); War(false); });
            Check("0xAA before player creation keeps combatant", () =>
            {
                combatant.SetValue(null, Serial(123));
                Combatant(2);
                Expect(LastCombatant() == 123, "A pre-login packet changed the combatant.");
            });
            Check("war mode true/false updates current player", () =>
            {
                object current = NewPlayer(1);
                SetPlayer(current);
                War(true);
                Expect(Warmode(current), "War mode did not enable.");
                War(false);
                Expect(!Warmode(current), "War mode did not disable.");
                War(true);
                Expect(Warmode(current), "War mode did not re-enable.");
            });
            Check("valid mobile combatant updates immediately", () =>
            {
                SetPlayer(NewPlayer(1));
                Combatant(2);
                Expect(LastCombatant() == 2, "First combatant was ignored.");
                Combatant(0x3FFFFFFF);
                Expect(LastCombatant() == 0x3FFFFFFF, "The upper mobile serial boundary was ignored.");
            });
            foreach (uint ignored in new uint[] { 1, 0, uint.MaxValue, 0x40000000, 0x7FFFFF00 })
            {
                uint value = ignored;
                Check("combatant ignores 0x" + value.ToString("X8"), () =>
                {
                    SetPlayer(NewPlayer(1));
                    Combatant(2);
                    Combatant(value);
                    Expect(LastCombatant() == 2, "An excluded serial replaced the combatant.");
                });
            }
            Check("player lifecycle repeated " + repeat + " times", () =>
            {
                for (int i = 0; i < repeat; i++)
                {
                    uint currentSerial = (uint)(100 + i * 4);
                    uint targetSerial = currentSerial + 1;
                    uint retainedCombatant = LastCombatant();
                    War(true);
                    Combatant(targetSerial);
                    Expect(LastCombatant() == retainedCombatant, "A packet changed combatant while player was absent.");
                    object oldPlayer = NewPlayer(currentSerial);
                    SetPlayer(oldPlayer);
                    Expect(!Warmode(oldPlayer), "A pre-login packet was replayed onto the new player.");
                    War(true);
                    Combatant(targetSerial);
                    Expect(Warmode(oldPlayer) && LastCombatant() == targetSerial, "Live player packet state was wrong.");
                    SetPlayer(null);
                    War(false);
                    Combatant(targetSerial + 1);
                    Expect(Warmode(oldPlayer), "A post-removal packet changed the retired player.");
                    Expect(LastCombatant() == targetSerial, "A post-removal packet changed the combatant.");
                    object replacement = NewPlayer(currentSerial + 2);
                    SetPlayer(replacement);
                    Expect(!Warmode(replacement), "The replacement inherited stale war mode.");
                    War(true);
                    War(false);
                    Combatant(currentSerial + 2);
                    Expect(LastCombatant() == targetSerial, "Replacement self was accepted as a combatant.");
                    Combatant(currentSerial);
                    Expect(LastCombatant() == currentSerial, "Handler retained the previous player's serial.");
                    Expect(!Warmode(replacement) && Warmode(oldPlayer), "War mode used a stale player reference.");
                    SetPlayer(null);
                }
            });
            Check("dispatch early return preserves body and reaches the next viewer", () =>
            {
                int seen = 0;
                WithViewers(0x72, new Action<object, object>[]
                {
                    Handler(warHandler),
                    (reader, args) => { Expect(Byte(reader) == 1, "War body offset was lost."); seen++; }
                }, () => Expect(!Dispatch(new byte[] { 0x72, 1, 0, 0x32, 0 }), "Early return blocked war mode."));
                WithViewers(0xAA, new Action<object, object>[]
                {
                    Handler(combatantHandler),
                    (reader, args) => { Expect(UInt32(reader) == 0x01020304, "Combatant body offset/endian was lost."); seen++; }
                }, () => Expect(!Dispatch(new byte[] { 0xAA, 1, 2, 3, 4 }), "Early return blocked combatant."));
                Expect(seen == 2 && LastCombatant() == 0, "An early-return viewer altered state or stopped dispatch.");
            });
            Check("dispatch resets the reader before and after each live handler", () =>
            {
                object current = NewPlayer(1);
                SetPlayer(current);
                int seen = 0;
                WithViewers(0x72, new Action<object, object>[]
                {
                    (reader, args) => { UInt32(reader); seen++; },
                    Handler(warHandler),
                    (reader, args) => { Expect(Byte(reader) == 1, "War body was consumed by an earlier viewer."); seen++; }
                }, () => Expect(!Dispatch(new byte[] { 0x72, 1, 0, 0x32, 0 }), "War mode was blocked."));
                WithViewers(0xAA, new Action<object, object>[]
                {
                    (reader, args) => { UInt32(reader); seen++; },
                    Handler(combatantHandler),
                    (reader, args) => { Expect(UInt32(reader) == 0x01020304, "Combatant body was consumed by an earlier viewer."); seen++; }
                }, () => Expect(!Dispatch(new byte[] { 0xAA, 1, 2, 3, 4 }), "Combatant was blocked."));
                Expect(seen == 4 && Warmode(current) && LastCombatant() == 0x01020304, "Live dispatcher state was wrong.");
            });
            Check("dispatch preserves Block within a packet and resets it for the next packet", () =>
            {
                foreach (int packetId in new int[] { 0x72, 0xAA })
                {
                    int packet = 0, seen = 0;
                    SetPlayer(null);
                    object current = NewPlayer(1);
                    byte[] bytes = packetId == 0x72 ? new byte[] { 0x72, 1, 0, 0x32, 0 } : new byte[] { 0xAA, 0, 0, 0, 2 };
                    WithViewers(packetId, new Action<object, object>[]
                    {
                        (reader, args) => { packet++; if (packet == 1) blocked.SetValue(args, true, null); },
                        Handler(packetId == 0x72 ? warHandler : combatantHandler),
                        (reader, args) =>
                        {
                            Expect((bool)blocked.GetValue(args, null) == (packet == 1), "Block was cleared by the handler or leaked from the previous packet.");
                            Expect(packetId == 0x72 ? Byte(reader) == 1 : UInt32(reader) == 2, "A blocked packet was not available to the next viewer.");
                            seen++;
                        }
                    }, () =>
                    {
                        Expect(Dispatch(bytes), "Block was not returned.");
                        SetPlayer(current);
                        Expect(!Dispatch(bytes), "Block leaked into the next packet.");
                    });
                    Expect(seen == 2, "Block stopped the remaining viewers.");
                    Expect(packetId == 0x72 ? Warmode(current) : LastCombatant() == 2, "The next packet did not update the current player state.");
                }
            });
            Check("dispatch observes player removal or replacement by a preceding viewer", () =>
            {
                object retired = NewPlayer(1), replacement = NewPlayer(2);
                SetPlayer(retired);
                War(true);
                int packet = 0;
                WithViewers(0x72, new Action<object, object>[]
                {
                    (reader, args) => { packet++; SetPlayer(packet == 1 ? null : replacement); },
                    Handler(warHandler)
                }, () =>
                {
                    Dispatch(new byte[] { 0x72, 0, 0, 0x32, 0 });
                    Expect(Warmode(retired), "Removal allowed a packet to update the retired player.");
                    Dispatch(new byte[] { 0x72, 1, 0, 0x32, 0 });
                    Expect(Warmode(replacement), "Replacement player was not updated.");
                });
                packet = 0;
                WithViewers(0xAA, new Action<object, object>[]
                {
                    (reader, args) => { packet++; SetPlayer(packet == 1 ? null : replacement); },
                    Handler(combatantHandler)
                }, () =>
                {
                    SetPlayer(retired);
                    Dispatch(new byte[] { 0xAA, 0, 0, 0, 3 });
                    Expect(LastCombatant() == 0, "Removal allowed a packet to change the combatant.");
                    Dispatch(new byte[] { 0xAA, 0, 0, 0, 2 });
                    Expect(LastCombatant() == 0, "Replacement player's self serial was accepted.");
                    Dispatch(new byte[] { 0xAA, 0, 0, 0, 1 });
                    Expect(LastCombatant() == 1, "Old player's serial was still excluded.");
                });
            });
            Check("duplicate and interleaved state packets retain last accepted state", () =>
            {
                object current = NewPlayer(1);
                SetPlayer(current);
                WithViewers(0x72, new Action<object, object>[] { Handler(warHandler) }, () =>
                    WithViewers(0xAA, new Action<object, object>[] { Handler(combatantHandler) }, () =>
                    {
                        foreach (byte[] packet in new byte[][]
                        {
                            new byte[] { 0xAA, 0, 0, 0, 2 }, new byte[] { 0xAA, 0, 0, 0, 2 },
                            new byte[] { 0x72, 1, 0, 0x32, 0 }, new byte[] { 0x72, 1, 0, 0x32, 0 },
                            new byte[] { 0xAA, 0, 0, 0, 0 }, new byte[] { 0x72, 0, 0, 0x32, 0 },
                            new byte[] { 0xAA, 0, 0, 0, 3 }, new byte[] { 0xAA, 0, 0, 0, 1 },
                            new byte[] { 0xAA, 0x40, 0, 0, 0 }, new byte[] { 0x72, 0, 0, 0x32, 0 }
                        })
                        {
                            Expect(!Dispatch(packet), "State packet was blocked.");
                        }
                        Expect(!Warmode(current) && LastCombatant() == 3, "Duplicate/interleaved state handling changed semantics.");
                    }));
            });
        }
        Console.WriteLine("PRELOGIN_STATE_PACKET_REGRESSION passed=" + passed + " failed=" + failed);
        return failed;
    }
}
'@
Add-Type -TypeDefinition $harness -Language CSharp
if ([PreLoginStatePacketRegression]::Run($AssemblyPath, $ExpectLegacyCrash.IsPresent, $Repeat) -ne 0) { exit 1 }
