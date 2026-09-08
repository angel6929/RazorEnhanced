using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;

namespace ClassicUO.Configuration { public class Settings { public static Settings GlobalSettings = new Settings(); public string IP { get { return "127.0.0.1"; } } } }
internal static class StatusPacketUiRegression
{
    const BindingFlags I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Assembly assembly;
    static Form form, toolbarForm;
    static Control dispatcher;
    static Type formType, toolbarType, playerType;
    static object player;
    static DataRow general;
    static readonly ManualResetEvent ready = new ManualResetEvent(false);
    static Exception uiFailure, asyncFailure;
    static ConstructorInfo readerConstructor, argsConstructor;
    static MethodInfo moveToData;
    static int failed, passed;
    static int uiThreadId;
    static void Check(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static void Ui(Action action) { if (!form.IsDisposed && form.IsHandleCreated) form.Invoke(action); else dispatcher.Invoke(action); }
    static Label ManaLabel() { return (Label)toolbarType.GetField("m_manalabelSH", S).GetValue(null); }
    static void ReplaceToolbar()
    {
        Form old = toolbarForm;
        toolbarForm = new Form(); toolbarForm.ShowInTaskbar = false;
        foreach (FieldInfo field in toolbarType.GetFields(S))
        {
            if (field.FieldType == typeof(Label))
            {
                Label label = new Label(); field.SetValue(null, label);
                toolbarForm.Controls.Add(label); IntPtr handle = label.Handle;
            }
        }
        if (old != null) old.Dispose();
        toolbarType.GetField("m_form", S).SetValue(null, toolbarForm);
        general["ToolBoxSizeComboBox"] = "Small";
    }
    static void Transition(string name, Action beforeDrain, Action packets, bool disposeMainWindow = false)
    {
        using (ManualResetEvent entered = new ManualResetEvent(false))
        {
            form.BeginInvoke(new Action(delegate { entered.Set(); Thread.Sleep(350); beforeDrain(); }));
            Assert(entered.WaitOne(5000), "Transition did not start.");
            Stopwatch watch = Stopwatch.StartNew(); packets(); watch.Stop();
            Console.WriteLine("TIMING {0} ui_busy_ms=350 call_ms={1:F3}", name, watch.Elapsed.TotalMilliseconds);
            if (disposeMainWindow) dispatcher.Invoke(new Action(delegate { }));
            else Ui(delegate { });
            Assert(watch.ElapsedMilliseconds < 100, "Transition waited for UI: " + watch.ElapsedMilliseconds);
        }
    }
    static void Loop()
    {
        try
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            uiThreadId = Thread.CurrentThread.ManagedThreadId;
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs args) { asyncFailure = args.Exception; };
            dispatcher = new Control(); IntPtr dispatcherHandle = dispatcher.Handle;
            form = (Form)Activator.CreateInstance(formType, true);
            form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, formType.GetMethod("MainForm_Load", I));
            form.LocationChanged -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, formType.GetMethod("MainForm_LocationChanged", I));
            form.SizeChanged -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, formType.GetMethod("MainForm_SizeChanged", I));
            foreach (FieldInfo field in formType.GetFields(I)) { System.Windows.Forms.Timer timer = field.GetValue(form) as System.Windows.Forms.Timer; if (timer != null) timer.Stop(); }
            form.ShowInTaskbar = false;
            IntPtr handle = form.Handle; // Creates a hidden native handle. Never Show or Activate.
            assembly.GetType("Assistant.Engine", true).GetField("MainWnd", S).SetValue(null, form);
            toolbarForm = new Form(); toolbarForm.ShowInTaskbar = false;
            foreach (FieldInfo field in toolbarType.GetFields(S))
            {
                Label label = field.GetValue(null) as Label;
                if (label != null) { toolbarForm.Controls.Add(label); IntPtr labelHandle = label.Handle; }
            }
            ready.Set(); Application.Run();
            toolbarForm.Dispose(); form.Dispose(); dispatcher.Dispose();
        }
        catch (Exception e) { uiFailure = e; Console.WriteLine("UI_ERROR " + e); ready.Set(); }
    }
    static void Send(string handler, byte[] data, bool dynamic)
    {
        object reader = readerConstructor.Invoke(new object[] { data, dynamic });
        object args = argsConstructor.Invoke(new object[0]);
        moveToData.Invoke(reader, null);
        try { assembly.GetType("Assistant.PacketHandlers", true).GetMethod(handler, S).Invoke(null, new object[] { reader, args }); }
        catch (TargetInvocationException e) { throw e.InnerException; }
        string property = handler == "ManaUpdate" ? "Mana" : handler == "StamUpdate" ? "Stam" : "Hits";
        int offset = dynamic ? 37 : 7;
        ushort expected = (ushort)((data[offset] << 8) | data[offset + 1]);
        Assert((ushort)playerType.GetProperty(property, I).GetValue(player, null) == expected, "Network state was deferred with the UI.");
    }
    static byte[] Update(byte id, ushort value)
    {
        return new byte[] { id, 0, 0, 0, 1, 0, 100, (byte)(value >> 8), (byte)value };
    }
    static void U16(BinaryWriter writer, ushort value) { writer.Write((byte)(value >> 8)); writer.Write((byte)value); }
    static byte[] Status(ushort value)
    {
        using (MemoryStream stream = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((byte)0x11); U16(writer, 66); writer.Write(new byte[] { 0, 0, 0, 1 });
            byte[] name = new byte[30]; System.Text.Encoding.ASCII.GetBytes("Audit").CopyTo(name, 0); writer.Write(name);
            U16(writer, value); U16(writer, 100); writer.Write((byte)0); writer.Write((byte)1); writer.Write((byte)0);
            U16(writer, 100); U16(writer, 100); U16(writer, 100);
            U16(writer, value); U16(writer, 100); U16(writer, value); U16(writer, 100);
            writer.Write(new byte[4]); U16(writer, 0); U16(writer, 0); return stream.ToArray();
        }
    }
    static void Measure(string name, Action call, int busyMs)
    {
        using (ManualResetEvent entered = new ManualResetEvent(false))
        {
            if (busyMs > 0)
            {
                form.BeginInvoke(new Action(delegate { entered.Set(); Thread.Sleep(busyMs); }));
                Assert(entered.WaitOne(5000), "UI blocker did not start.");
            }
            Stopwatch watch = Stopwatch.StartNew(); call(); watch.Stop();
            Console.WriteLine("TIMING {0} ui_busy_ms={1} call_ms={2:F3}", name, busyMs, watch.Elapsed.TotalMilliseconds);
            Ui(delegate { Assert(!form.Visible && !toolbarForm.Visible, "A probe window became visible."); });
            if (busyMs > 0) Assert(watch.ElapsedMilliseconds < 100, "Packet handler waited for the UI: " + watch.ElapsedMilliseconds + " ms.");
        }
    }
    [STAThread] static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception e) { for (; e != null; e = e.InnerException) Console.WriteLine("BOOT_ERROR " + e.GetType().FullName + " " + e.Message); return 11; }
    }
    static int Run(string[] args)
    {
        string release = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.GetDirectoryName(Path.GetFullPath(args[0]));
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) { string path = Path.Combine(release, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(path) ? Assembly.LoadFrom(path) : null; };
        assembly = Assembly.LoadFrom(args[0]); Console.WriteLine("ASSEMBLY " + args[0]);
        Type engine = assembly.GetType("Assistant.Engine", true);
        engine.GetField("_rootPath", S).SetValue(null, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "status-probe-isolated-root"));
        DataSet ds = new DataSet(); DataTable table = ds.Tables.Add("GENERAL");
        table.Columns.Add("ToolBoxSizeComboBox", typeof(string)); table.Columns.Add("ToolBoxStyleComboBox", typeof(string)); table.Columns.Add("SpellGridStyle", typeof(int));
        general = table.Rows.Add("Small", "Horizontal", 0);
        ds.Tables.Add("TOOLBAR_ITEMS");
        assembly.GetType("RazorEnhanced.Settings", true).GetField("m_Dataset", S).SetValue(null, ds);
        formType = assembly.GetType("Assistant.MainForm", true); toolbarType = assembly.GetType("RazorEnhanced.ToolBar", true);
        Type clientType = assembly.GetType("Assistant.ClassicUOClient", true);
        object client = FormatterServices.GetUninitializedObject(clientType);
        clientType.GetField("m_ClientVersion", I).SetValue(client, new Version(7, 0, 104, 0));
        assembly.GetType("Assistant.Client", true).GetField("Instance", S).SetValue(null, client);
        Type serial = assembly.GetType("Assistant.Serial", true); object serialValue = Activator.CreateInstance(serial, I, null, new object[] { (uint)1 }, null);
        playerType = assembly.GetType("Assistant.PlayerData", true);
        player = Activator.CreateInstance(playerType, I, null, new object[] { serialValue }, null);
        Type world = assembly.GetType("Assistant.World", true); world.GetProperty("Player", S).SetValue(null, player, null); world.GetMethod("AddMobile", S).Invoke(null, new object[] { player });
        Type readerType = assembly.GetType("Assistant.PacketReader", true);
        readerConstructor = readerType.GetConstructor(I, null, new Type[] { typeof(byte[]), typeof(bool) }, null);
        argsConstructor = assembly.GetType("Assistant.PacketHandlerEventArgs", true).GetConstructor(I, null, Type.EmptyTypes, null);
        moveToData = readerType.GetMethod("MoveToData", I);
        Thread ui = new Thread(Loop); ui.IsBackground = true; ui.SetApartmentState(ApartmentState.STA); ui.Start();
        if (!ready.WaitOne(15000) || uiFailure != null) return 10;
        try
        {
            Check("toolbar closed, complete own status and mana handlers", delegate
            {
                Measure("0x11-toolbar-closed-warm", delegate { Send("MobileStatus", Status(99), true); }, 0);
                Measure("0xA2-toolbar-closed-warm", delegate { Send("ManaUpdate", Update(0xA2, 98), false); }, 0);
                for (int round = 0; round < 3; round++)
                {
                    ushort value = (ushort)(95 - round);
                    Measure("0x11-toolbar-closed", delegate { Send("MobileStatus", Status(value), true); }, 350);
                    Measure("0xA2-toolbar-closed", delegate { Send("ManaUpdate", Update(0xA2, value), false); }, 350);
                }
            });
            foreach (string size in new string[] { "Small", "Big" })
            {
                string current = size;
                Check("actual toolbar " + current + " labels receive mana/hits/stamina", delegate
                {
                    Ui(delegate { general["ToolBoxSizeComboBox"] = current; toolbarType.GetField("m_form", S).SetValue(null, toolbarForm); });
                    string[] handlers = { "ManaUpdate", "HitsUpdate", "StamUpdate" }; byte[] ids = { 0xA2, 0xA1, 0xA3 };
                    for (int i = 0; i < handlers.Length; i++)
                    {
                        string handler = handlers[i]; byte id = ids[i];
                        Measure(handler + "-" + current + "-warm", delegate { Send(handler, Update(id, 90), false); }, 0);
                        Measure(handler + "-" + current, delegate { Send(handler, Update(id, 89), false); }, 350);
                        Measure(handler + "-" + current + "-unchanged", delegate { Send(handler, Update(id, 89), false); }, 350);
                    }
                });
            }
            Check("1000 changed packets coalesce to newest display and preserve immediate state", delegate
            {
                int changes = 0;
                Ui(delegate { ReplaceToolbar(); ManaLabel().TextChanged += delegate { changes++; }; });
                Transition("1000-changing-mana", delegate { }, delegate
                {
                    for (int i = 1; i <= 1000; i++) Send("ManaUpdate", Update(0xA2, (ushort)(i % 80 + 1)), false);
                });
                Ui(delegate { Assert(ManaLabel().Text == "41 / 100", "Burst did not show newest mana."); Assert(changes == 1, "Burst rendered obsolete values: " + changes); });
            });
            Check("1000 unchanged packets do not repeatedly rewrite the display", delegate
            {
                int changes = 0;
                Ui(delegate { ManaLabel().TextChanged += delegate { changes++; }; });
                Transition("1000-unchanged-mana", delegate { }, delegate
                {
                    for (int i = 0; i < 1000; i++) Send("ManaUpdate", Update(0xA2, 41), false);
                });
                Ui(delegate { Assert(ManaLabel().Text == "41 / 100", "Repeated value changed."); Assert(changes == 0, "Repeated value triggered TextChanged."); });
            });
            Check("a pending vital update includes a later full status and title update", delegate
            {
                Ui(delegate { form.Text = "Before full status"; });
                Transition("vital-then-full", delegate { }, delegate { Send("ManaUpdate", Update(0xA2, 42), false); Send("MobileStatus", Status(62), true); });
                Ui(delegate { Assert(ManaLabel().Text == "62 / 100", "Full status was lost during coalescing."); Assert(form.Text.Contains("Audit"), "Title update was lost."); });
            });
            Check("a pending full status retains title refresh after later vital packets", delegate
            {
                Ui(delegate { form.Text = "Before full status"; });
                Transition("full-then-vital", delegate { }, delegate { Send("MobileStatus", Status(63), true); Send("ManaUpdate", Update(0xA2, 43), false); });
                Ui(delegate { Assert(ManaLabel().Text == "43 / 100", "Old full status overwrote newer mana."); Assert(form.Text.Contains("Audit"), "Later vital erased full status request."); });
            });
            Check("queued status ignores a disposed toolbar and supports its replacement", delegate
            {
                Transition("toolbar-dispose", delegate { toolbarForm.Dispose(); }, delegate { Send("ManaUpdate", Update(0xA2, 64), false); });
                Ui(ReplaceToolbar);
                Send("ManaUpdate", Update(0xA2, 65), false);
                Ui(delegate { Assert(ManaLabel().Text == "65 / 100", "Replacement toolbar stopped receiving updates."); });
            });
            Check("toolbar replacement before queue drain reads current state into new controls", delegate
            {
                Transition("toolbar-replace", ReplaceToolbar, delegate { Send("ManaUpdate", Update(0xA2, 66), false); });
                Ui(delegate { Assert(ManaLabel().Text == "66 / 100", "Pending update targeted destroyed controls."); });
            });
            Check("closed toolbar receives no native update and a reopened toolbar resumes", delegate
            {
                Transition("toolbar-close", delegate { toolbarType.GetField("m_form", S).SetValue(null, null); toolbarForm.Dispose(); }, delegate { Send("ManaUpdate", Update(0xA2, 67), false); });
                Measure("closed-toolbar", delegate { Send("ManaUpdate", Update(0xA2, 68), false); }, 350);
                Ui(ReplaceToolbar); Send("ManaUpdate", Update(0xA2, 69), false);
                Ui(delegate { Assert(ManaLabel().Text == "69 / 100", "Reopened toolbar did not refresh."); });
            });
            Check("toolbar native handle recreation keeps pending status valid", delegate
            {
                Transition("toolbar-handle", delegate { typeof(Control).GetMethod("RecreateHandle", I).Invoke(toolbarForm, null); }, delegate { Send("ManaUpdate", Update(0xA2, 70), false); });
                Ui(delegate { Assert(ManaLabel().Text == "70 / 100", "Handle recreation lost current status."); });
            });
            Check("main window handle recreation cancels old work and accepts new work", delegate
            {
                Transition("main-handle", delegate { typeof(Control).GetMethod("RecreateHandle", I).Invoke(form, null); }, delegate { Send("ManaUpdate", Update(0xA2, 71), false); });
                Send("ManaUpdate", Update(0xA2, 72), false);
                Ui(delegate { Assert(ManaLabel().Text == "72 / 100", "Lost callback kept the status queue permanently pending."); });
            });
            Check("last vital packet survives main window handle recreation without another packet", delegate
            {
                Ui(delegate { ManaLabel().Text = "Before last vital"; });
                Transition("last-vital-handle", delegate { form.ShowInTaskbar = !form.ShowInTaskbar; }, delegate { Send("ManaUpdate", Update(0xA2, 81), false); });
                Ui(delegate { Assert(ManaLabel().Text == "81 / 100", "Handle recreation dropped the last vital packet."); });
            });
            Check("last full status and title survive main window handle recreation without another packet", delegate
            {
                Ui(delegate { form.Text = "Before last full status"; ManaLabel().Text = "Before last full"; });
                Transition("last-full-handle", delegate { form.ShowInTaskbar = !form.ShowInTaskbar; }, delegate { Send("MobileStatus", Status(82), true); });
                Ui(delegate { Assert(ManaLabel().Text == "82 / 100", "Handle recreation dropped the last full status."); Assert(form.Text.Contains("Audit"), "Handle recreation dropped the title update."); });
            });
            Check("a packet received in the native handle gap is recovered after recreation", delegate
            {
                Ui(delegate
                {
                    ManaLabel().Text = "Before handle gap";
                    typeof(Control).GetMethod("DestroyHandle", I).Invoke(form, null);
                    Assert(!form.IsHandleCreated, "The native handle gap was not exercised.");
                });
                try { Send("ManaUpdate", Update(0xA2, 83), false); }
                finally { Ui(delegate { typeof(Control).GetMethod("CreateHandle", I).Invoke(form, null); }); }
                Ui(delegate { Assert(ManaLabel().Text == "83 / 100", "The last packet in the native handle gap was lost."); });
            });
            Check("old callbacks cannot erase the final request across repeated handle recreation", delegate
            {
                int recreations = 0;
                EventHandler created = delegate
                {
                    recreations++;
                    Send("ManaUpdate", Update(0xA2, (ushort)(84 + recreations)), false);
                };
                Ui(delegate { ManaLabel().Text = "Before repeated recreation"; form.Text = "Before repeated full"; form.HandleCreated += created; });
                try
                {
                    Transition("repeated-handle-new-request", delegate { form.ShowInTaskbar = !form.ShowInTaskbar; form.ShowInTaskbar = !form.ShowInTaskbar; }, delegate { Send("MobileStatus", Status(84), true); });
                    Ui(delegate { Assert(recreations == 2, "Two actual handle recreations did not occur."); Assert(ManaLabel().Text == "86 / 100", "An old callback cleared the newest request."); Assert(form.Text.Contains("Audit"), "The newest batch lost its full refresh."); });
                }
                finally { Ui(delegate { form.HandleCreated -= created; }); }
            });
            Check("logout before queued presentation cannot render the old player's status", delegate
            {
                Ui(delegate { ManaLabel().Text = "Logout marker"; });
                Transition("player-logout", delegate { world.GetProperty("Player", S).SetValue(null, null, null); }, delegate { Send("ManaUpdate", Update(0xA2, 73), false); });
                Ui(delegate { Assert(ManaLabel().Text == "Logout marker", "Old status rendered after logout."); world.GetProperty("Player", S).SetValue(null, player, null); });
            });
            Check("switching players before drain renders the current player only", delegate
            {
                object secondSerial = Activator.CreateInstance(serial, I, null, new object[] { (uint)2 }, null);
                object secondPlayer = Activator.CreateInstance(playerType, I, null, new object[] { secondSerial }, null);
                playerType.GetProperty("ManaMax", I).SetValue(secondPlayer, (ushort)200, null);
                playerType.GetProperty("Mana", I).SetValue(secondPlayer, (ushort)123, null);
                Transition("player-switch", delegate { world.GetProperty("Player", S).SetValue(null, secondPlayer, null); }, delegate { Send("ManaUpdate", Update(0xA2, 74), false); });
                Ui(delegate { Assert(ManaLabel().Text == "123 / 200", "Old player overwrote current player."); world.GetProperty("Player", S).SetValue(null, player, null); });
            });
            Check("the compiled automatic toolbar Open branch is marshaled onto the MainForm owner", delegate
            {
                MethodInfo open = toolbarType.GetMethod("Open", S);
                byte[] token = BitConverter.GetBytes(open.MetadataToken);
                MethodInfo automaticOpen = null;
                foreach (Type nested in assembly.GetType("Assistant.PacketHandlers", true).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    foreach (MethodInfo candidate in nested.GetMethods(I | S))
                    {
                        ParameterInfo[] parameters = candidate.GetParameters();
                        if (!candidate.Name.Contains("<LoginConfirm>") || parameters.Length != 1 || parameters[0].ParameterType != formType || candidate.GetMethodBody() == null) continue;
                        byte[] il = candidate.GetMethodBody().GetILAsByteArray();
                        for (int i = 0; i + 4 < il.Length; i++)
                        {
                            if (il[i] == 0x28 && il[i + 1] == token[0] && il[i + 2] == token[1] && il[i + 3] == token[2] && il[i + 4] == token[3]) automaticOpen = candidate;
                        }
                    }
                }
                Assert(automaticOpen != null, "LoginConfirm still calls ToolBar.Open outside its MainForm callback.");
                object closure = automaticOpen.IsStatic ? null : Activator.CreateInstance(automaticOpen.DeclaringType, true);
                int callbackThread = 0;
                Action<object> observed = delegate(object window) { automaticOpen.Invoke(closure, new object[] { window }); callbackThread = Thread.CurrentThread.ManagedThreadId; };
                Type actionType = typeof(Action<>).MakeGenericType(formType);
                Delegate callback = Delegate.CreateDelegate(actionType, observed.Target, observed.Method);
                MethodInfo safeAction = assembly.GetType("Assistant.UI.Ext", true).GetMethod("SafeAction", S).MakeGenericMethod(formType);
                // An already-open hidden toolbar avoids opening an asset-dependent visible window.
                Ui(delegate { ((CheckBox)formType.GetProperty("AutoopenToolBarCheckBox", I).GetValue(form, null)).Checked = true; });
                safeAction.Invoke(null, new object[] { form, callback });
                Assert(callbackThread == uiThreadId, "Automatic-open callback ran on the network thread.");
                Ui(delegate { Assert(!toolbarForm.InvokeRequired, "Toolbar does not belong to MainForm owner."); Assert(!toolbarForm.Visible, "Toolbar was shown."); });
                Console.WriteLine("OWNER automatic_open_callback_thread={0} main_form_thread={1} method={2}", callbackThread, uiThreadId, automaticOpen.Name);
            });
            Check("player replacement during a full display update converges after the new status request", delegate
            {
                object secondSerial = Activator.CreateInstance(serial, I, null, new object[] { (uint)2 }, null);
                object secondPlayer = Activator.CreateInstance(playerType, I, null, new object[] { secondSerial }, null);
                playerType.GetProperty("ManaMax", I).SetValue(secondPlayer, (ushort)200, null);
                playerType.GetProperty("Mana", I).SetValue(secondPlayer, (ushort)134, null);
                playerType.GetProperty("HitsMax", I).SetValue(secondPlayer, (ushort)200, null);
                playerType.GetProperty("Hits", I).SetValue(secondPlayer, (ushort)144, null);
                bool switched = false;
                EventHandler change = delegate
                {
                    if (switched) return;
                    switched = true;
                    world.GetProperty("Player", S).SetValue(null, secondPlayer, null);
                    formType.GetMethod("PostPlayerStatusUpdate", I).Invoke(form, new object[] { true });
                };
                Ui(delegate { ((Label)toolbarType.GetField("m_hitslabelSH", S).GetValue(null)).TextChanged += change; });
                try
                {
                    Send("MobileStatus", Status(78), true);
                    Ui(delegate { }); // The first callback can enqueue the replacement player's second callback.
                    Ui(delegate { Assert(switched, "Controlled mid-render replacement did not occur."); Assert(ManaLabel().Text == "134 / 200", "New player mana did not converge."); Assert(((Label)toolbarType.GetField("m_hitslabelSH", S).GetValue(null)).Text == "144 / 200", "Old player's hits remained."); });
                }
                finally
                {
                    Ui(delegate { ((Label)toolbarType.GetField("m_hitslabelSH", S).GetValue(null)).TextChanged -= change; world.GetProperty("Player", S).SetValue(null, player, null); });
                }
            });
            Check("main window disposal drops queued work without blocking later packet state", delegate
            {
                Transition("main-dispose", delegate { form.Dispose(); }, delegate { Send("ManaUpdate", Update(0xA2, 75), false); }, true);
                Send("ManaUpdate", Update(0xA2, 76), false);
            });
            Check("no asynchronous UI exception or visible window", delegate { Ui(delegate { Assert(asyncFailure == null, asyncFailure == null ? "" : asyncFailure.Message); Assert(!form.Visible && !toolbarForm.Visible, "Probe became visible."); }); });
        }
        finally { Ui(delegate { Application.ExitThread(); }); ui.Join(5000); }
        Console.WriteLine("STATUS_PACKET_UI_REGRESSION passed={0} failed={1}", passed, failed);
        return failed == 0 ? 0 : 1;
    }
}
