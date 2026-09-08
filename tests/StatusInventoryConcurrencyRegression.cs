using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;

namespace ClassicUO.Configuration { public class Settings { public static Settings GlobalSettings = new Settings(); public string IP { get { return "127.0.0.1"; } } } }
internal static class StatusInventoryConcurrencyRegression
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
    static void Check(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static void Ui(Action action) { if (!form.IsDisposed && form.IsHandleCreated) form.Invoke(action); else dispatcher.Invoke(action); }
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
    static void Loop()
    {
        try
        {
            Control.CheckForIllegalCrossThreadCalls = false;
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
            foreach (bool clearMobile in new bool[] { false, true })
            {
            Check(clearMobile ? "full status counter survives mobile equipment clear" : "full status counter survives concurrent equipment removal", delegate
            {
                Ui(delegate { asyncFailure = null; });
                Type itemType = assembly.GetType("Assistant.Item", true);
                Type mobileType = assembly.GetType("Assistant.Mobile", true);
                Type layerType = assembly.GetType("Assistant.Layer", true);
                MethodInfo addItem = mobileType.GetMethod("AddItem", I), removeItem = mobileType.GetMethod("RemoveItem", I);
                object[] equipment = new object[20];
                for (int i = 0; i < equipment.Length; i++)
                {
                    object itemSerial = Activator.CreateInstance(serial, I, null, new object[] { (uint)(0x40000100 + i) }, null);
                    equipment[i] = Activator.CreateInstance(itemType, I, null, new object[] { itemSerial }, null);
                    itemType.GetProperty("Layer", I).SetValue(equipment[i], Enum.ToObject(layerType, i + 1), null);
                    addItem.Invoke(player, new object[] { equipment[i] });
                }
                Ui(delegate
                {
                    ReplaceToolbar();
                    DataTable counts = ds.Tables["TOOLBAR_ITEMS"];
                    if (counts.Columns.Count == 0)
                    {
                        counts.Columns.Add("Name", typeof(string)); counts.Columns.Add("Graphics", typeof(int)); counts.Columns.Add("Color", typeof(int)); counts.Columns.Add("Warning", typeof(bool)); counts.Columns.Add("WarningLimit", typeof(int));
                    }
                    counts.Rows.Add("Audit real inventory slot", 0x0E21, -1, false, 0);
                    toolbarType.GetField("m_slot", S).SetValue(null, 1);
                    System.Collections.IList labels = (System.Collections.IList)toolbarType.GetField("m_panelcount", S).GetValue(null);
                    labels.Clear();
                    Label count = new Label(); toolbarForm.Controls.Add(count); labels.Add(count); IntPtr handle = count.Handle;
                });
                MethodInfo removeMobile = mobileType.GetMethod("Remove", I);
                int mutations = 0; bool stop = false; Exception writerFailure = null;
                Thread writer = new Thread(delegate()
                {
                    try
                    {
                        while (!Volatile.Read(ref stop))
                        {
                            if (clearMobile)
                            {
                                removeMobile.Invoke(player, null);
                                world.GetMethod("AddMobile", S).Invoke(null, new object[] { player });
                            }
                            else
                            {
                                for (int i = equipment.Length - 1; i >= 0; i--) removeItem.Invoke(player, new object[] { equipment[i] });
                            }
                            for (int i = 0; i < equipment.Length; i++) addItem.Invoke(player, new object[] { equipment[i] });
                            int sequence = Interlocked.Increment(ref mutations);
                            Send("MobileStatus", Status((ushort)(sequence % 80 + 1)), true);
                        }
                    }
                    catch (Exception e) { writerFailure = e; }
                });
                writer.IsBackground = true; writer.Start();
                int calls = 0;
                try
                {
                    Stopwatch duration = Stopwatch.StartNew();
                    while (duration.ElapsedMilliseconds < 2000 && asyncFailure == null)
                    {
                        Ui(delegate { });
                        calls++;
                    }
                }
                finally { Volatile.Write(ref stop, true); Assert(writer.Join(5000), "Equipment writer did not terminate; possible lock deadlock."); }
                Ui(delegate { });
                Console.WriteLine("DATA_RACE calls={0} equipment_cycles={1} writer_failure={2} async_failure={3}", calls, mutations, writerFailure, asyncFailure);
                for (int i = 0; i < equipment.Length; i++) removeItem.Invoke(player, new object[] { equipment[i] });
                Ui(delegate { toolbarType.GetField("m_slot", S).SetValue(null, 0); ds.Tables["TOOLBAR_ITEMS"].Rows.Clear(); });
                Assert(writerFailure == null, "Writer failed: " + writerFailure);
                Assert(mutations > 100, "Equipment writer did not perform enough transitions.");
                Assert(asyncFailure == null, "Actual full status UI callback failed: " + asyncFailure);
                Ui(delegate { Assert(!form.Visible && !toolbarForm.Visible, "A probe window became visible."); });
            });
            }

            Check("inventory counter preserves backpack nested bag quiver and color semantics", delegate
            {
                Type itemType = assembly.GetType("Assistant.Item", true), entityType = assembly.GetType("Assistant.UOEntity", true);
                Type mobileType = assembly.GetType("Assistant.Mobile", true), layerType = assembly.GetType("Assistant.Layer", true);
                MethodInfo addItem = mobileType.GetMethod("AddItem", I), removeItem = mobileType.GetMethod("RemoveItem", I);
                MethodInfo count = assembly.GetType("RazorEnhanced.Items", true).GetMethod("PlayerInventoryCount", S);
                uint nextSerial = 0x40001000;
                Func<int, int, object, object> makeItem = delegate(int graphic, int hue, object parent)
                {
                    object value = Activator.CreateInstance(serial, I, null, new object[] { nextSerial++ }, null);
                    object item = Activator.CreateInstance(itemType, I, null, new object[] { value }, null);
                    entityType.GetField("m_TypeID", I).SetValue(item, (ushort)graphic);
                    entityType.GetField("m_Hue", I).SetValue(item, (ushort)hue);
                    itemType.GetField("m_Parent", I).SetValue(item, parent);
                    world.GetMethod("AddItem", S).Invoke(null, new object[] { item });
                    return item;
                };
                object backpack = makeItem(0x0E75, 0, player), quiver = makeItem(0x2B02, 0, player);
                itemType.GetProperty("Layer", I).SetValue(backpack, Enum.ToObject(layerType, 21), null);
                itemType.GetProperty("Layer", I).SetValue(quiver, Enum.ToObject(layerType, 32), null);
                addItem.Invoke(player, new object[] { backpack }); addItem.Invoke(player, new object[] { quiver });
                object bag = makeItem(0x0E76, 0, backpack);
                makeItem(0x0E21, 0, backpack); makeItem(0x0E21, 1, bag); makeItem(0x0E21, 0, quiver); makeItem(0x0E21, 0, null);
                Assert((int)count.Invoke(null, new object[] { 0x0E21, -1 }) == 3, "Nested backpack and equipped quiver count changed.");
                Assert((int)count.Invoke(null, new object[] { 0x0E21, 0 }) == 2, "Color filtering changed.");
                removeItem.Invoke(player, new object[] { quiver });
                itemType.GetField("m_Parent", I).SetValue(quiver, null);
                Assert((int)count.Invoke(null, new object[] { 0x0E21, -1 }) == 2, "Unequipped quiver remained in player count.");
                itemType.GetField("m_Parent", I).SetValue(quiver, player);
                addItem.Invoke(player, new object[] { quiver });
                Assert((int)count.Invoke(null, new object[] { 0x0E21, -1 }) == 3, "Re-equipped quiver did not recover.");
            });
        }
        finally { Ui(delegate { Application.ExitThread(); }); Assert(ui.Join(5000), "UI owner thread did not terminate."); }
        Console.WriteLine("STATUS_INVENTORY_CONCURRENCY_REGRESSION passed={0} failed={1}", passed, failed);
        return failed == 0 ? 0 : 1;
    }
}
