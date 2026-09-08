// The runner inserts complete production hotkey and focus methods unchanged.
// Only game/settings actions and the surrounding MainForm are substituted.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Assistant;
using Assistant.UI;

namespace Assistant
{
__FOCUS_ENUM__
    public class MainForm : Form
    {
        private readonly TextBox hotkeytextbox = new RazorEnhanced.UI.RazorHotKeyTextBox();
        private readonly TextBox hotkeyKeyMasterTextBox = new RazorEnhanced.UI.RazorHotKeyTextBox();
        private readonly TextBox macroHotkeyTextBox = new RazorEnhanced.UI.RazorHotKeyTextBox();
        private readonly Button hotkeyClearButton = new Button();
        private readonly Button hotkeyMasterSetButton = new Button();
        private readonly Button hotkeyMasterClearButton = new Button();
        private readonly Button btnMacroClearHotkey = new Button();
        internal readonly Button Other = new Button();
        internal TextBox HotKeyTextBox => hotkeytextbox;
        internal TextBox HotKeyKeyMasterTextBox => hotkeyKeyMasterTextBox;
        internal TextBox MacroHotKeyTextBox => macroHotkeyTextBox;
        internal Label HotKeyStatusLabel = new Label();

        internal MainForm()
        {
            ShowInTaskbar = false;
            Opacity = 0;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
            Controls.AddRange(new Control[] { hotkeytextbox, hotkeyKeyMasterTextBox, macroHotkeyTextBox, Other, HotKeyStatusLabel });
            for (int i = 0; i < Controls.Count; i++) Controls[i].Top = i * 25;
            InitializeHotKeyFocusTracking();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ClearHotKeyFocus(this, EventArgs.Empty);
            base.Dispose(disposing);
        }

        internal void RecreateWindowHandle() { RecreateHandle(); }

__FOCUS_METHODS__
    }

    internal static class Engine { internal static MainForm MainWindow; }
    internal class ClassicUOClient
    {
__CLIENT_FOCUS_GAINED__
    }
    internal class PlayerData { internal int WalkScriptRequest; }
    internal static class World { internal static PlayerData Player; }
    internal static class Utility
    {
        internal static class Logger { internal static void Debug(string text, params object[] args) { } }
    }
}

namespace RazorEnhanced
{
__HOTKEY_TYPES__
    internal static class Settings
    {
        internal static class General
        {
            internal static volatile bool Enabled;
            internal static Keys Master = Keys.F12;
            internal static Keys ReadKey(string key) => Master;
            internal static bool ReadBool(string key) => Enabled;
            internal static void WriteBool(string key, bool value) { Enabled = value; }
        }
        internal static class HotKey
        {
            internal static bool Pass;
            internal static void FindGroup(Keys key, out string group, out bool pass) { group = "test"; pass = Pass; }
        }
    }
    internal static class Misc { internal static void SendMessage(string text, int hue, bool wait) { } }
    internal static class HotKey
    {
        internal static int Executed;
        internal static Keys ExecutedKey;
__HOTKEY_METHODS__
        private static void ProcessGroup(string group, Keys key) { Executed++; ExecutedKey = key; }
    }
}

namespace RazorEnhanced.UI
{
__HOTKEY_TEXTBOX__
}

internal static class HotKeyFocusRegression
{
    private static MainForm window;
    private static Form inactive;
    private static readonly ManualResetEvent ready = new ManualResetEvent(false);
    private static int passed, failed;
    private static Exception uiError;

    private static void UiLoop()
    {
        try
        {
            Control.CheckForIllegalCrossThreadCalls = true;
            window = new MainForm();
            IntPtr handle = window.Handle;
            inactive = new Form { ShowInTaskbar = false, Opacity = 0, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
            ready.Set();
            Application.Run();
            inactive.Dispose();
            window.Dispose();
        }
        catch (Exception error) { uiError = error; ready.Set(); }
    }

    private static void Ui(Action action) { window.Invoke(action); }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Check(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); }
    }
    private static void Focus(Control input, HotKeyFocusTarget expected)
    {
        Ui(() =>
        {
            window.Show();
            window.Activate();
            Assert(input.Focus(), "Native control focus was not established.");
            Assert(input.Focused, "Native control is not focused.");
            Assert(window.HotKeyFocus == expected, "Focus snapshot does not match the native control.");
        });
    }
    private static void BusyKey(string name, int delay, Keys key, bool expected, Action beforeUiDrain = null, Action beforeKey = null)
    {
        using (ManualResetEvent entered = new ManualResetEvent(false))
        {
            window.BeginInvoke(new Action(() => { entered.Set(); Thread.Sleep(delay); }));
            Assert(entered.WaitOne(5000), "UI did not enter the busy interval.");
            beforeKey?.Invoke();
            Stopwatch watch = Stopwatch.StartNew();
            bool result = RazorEnhanced.HotKey.KeyDown(key);
            watch.Stop();
            Console.WriteLine("TIMING {0} UI_busy_ms={1} key_ms={2}", name, delay, watch.ElapsedMilliseconds);
            Assert(result == expected, "Wrong hotkey propagation result.");
            Assert(watch.ElapsedMilliseconds < 150, "Hotkey waited for the busy UI.");
            beforeUiDrain?.Invoke();
            Ui(() => { });
        }
    }

    [STAThread]
    public static int Main()
    {
        Thread ui = new Thread(UiLoop) { IsBackground = true };
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();
        if (!ready.WaitOne(5000) || uiError != null) { Console.WriteLine(uiError); return 10; }
        try
        {
            Check("pre-window input passes", () => Assert(RazorEnhanced.HotKey.KeyDown(Keys.F1), "Input was swallowed before UI initialization."));
            Check("pre-window game bindings still execute", () =>
            {
                World.Player = new PlayerData();
                RazorEnhanced.Settings.General.Enabled = true;
                RazorEnhanced.Settings.HotKey.Pass = false;
                int previous = RazorEnhanced.HotKey.Executed;
                Assert(!RazorEnhanced.HotKey.KeyDown(Keys.F2) && RazorEnhanced.HotKey.Executed == previous + 1, "No-window binding behavior changed.");
            });
            Check("pre-window master toggle needs no UI", () =>
            {
                Assert(RazorEnhanced.HotKey.KeyDown(Keys.F12) && !RazorEnhanced.Settings.General.Enabled, "No-window master toggle failed.");
            });
            Engine.MainWindow = window;
            Check("initialized hidden window has no focus", () => Assert(window.HotKeyFocus == HotKeyFocusTarget.None, "Initial focus was retained."));
            World.Player = new PlayerData();
            RazorEnhanced.Settings.General.Enabled = true;
            RazorEnhanced.Settings.HotKey.Pass = true;
            Focus(window.Other, HotKeyFocusTarget.None);
            Check("normal hotkey preserves modifiers and pass flag", () =>
            {
                int previous = RazorEnhanced.HotKey.Executed;
                Assert(RazorEnhanced.HotKey.OnKeyDown((int)Keys.F3, RazorEnhanced.ModKeys.Control | RazorEnhanced.ModKeys.Shift), "Pass flag changed.");
                Assert(RazorEnhanced.HotKey.Executed == previous + 1 && RazorEnhanced.HotKey.ExecutedKey == (Keys.F3 | Keys.Control | Keys.Shift), "Binding changed.");
            });
            Check("blocked binding preserves false", () => { RazorEnhanced.Settings.HotKey.Pass = false; Assert(!RazorEnhanced.HotKey.KeyDown(Keys.F3), "Binding was passed through."); });
            Check("disabled hotkeys pass through", () => { RazorEnhanced.Settings.General.Enabled = false; Assert(RazorEnhanced.HotKey.KeyDown(Keys.F3), "Disabled hotkey was consumed."); });
            Check("movement script arrow bypass retained", () => { RazorEnhanced.Settings.General.Enabled = true; World.Player.WalkScriptRequest = 1; Assert(RazorEnhanced.HotKey.KeyDown(Keys.Up), "Movement key was consumed."); World.Player.WalkScriptRequest = 0; });

            TextBox[] inputs = { window.HotKeyTextBox, window.HotKeyKeyMasterTextBox, window.MacroHotKeyTextBox };
            HotKeyFocusTarget[] targets = { HotKeyFocusTarget.Normal, HotKeyFocusTarget.Master, HotKeyFocusTarget.Macro };
            for (int i = 0; i < inputs.Length; i++)
            {
                TextBox input = inputs[i];
                HotKeyFocusTarget target = targets[i];
                Check(target + " native focus and assignment", () =>
                {
                    Focus(input, target);
                    int previous = RazorEnhanced.HotKey.Executed;
                    Assert(!RazorEnhanced.HotKey.KeyDown(Keys.F12 | Keys.Alt), "Assignment was passed through.");
                    Assert(RazorEnhanced.HotKey.Executed == previous, "Assignment executed a binding.");
                    Assert((target == HotKeyFocusTarget.Master ? RazorEnhanced.HotKey.MasterKey : RazorEnhanced.HotKey.NormalKey) == (Keys.F12 | Keys.Alt), "Wrong assignment value.");
                    Ui(() => Assert(input.Text == RazorEnhanced.HotKey.KeyString(Keys.F12 | Keys.Alt), "Assignment display was lost."));
                    bool enabled = RazorEnhanced.Settings.General.Enabled;
                    Assert(!RazorEnhanced.HotKey.KeyDown(Keys.F12), "Master-key assignment escaped.");
                    Assert(RazorEnhanced.Settings.General.Enabled == enabled, "Assignment toggled master enable.");
                });
                Check(target + " busy UI assignment is nonblocking", () => { Focus(input, target); BusyKey(target.ToString(), 350, Keys.F8, false); Ui(() => Assert(input.Text == "F8", "Deferred display did not arrive.")); });
            }

            Check("focus moving to another control clears assignment", () => Focus(window.Other, HotKeyFocusTarget.None));
            Check("inactive form clears focus and game input executes", () =>
            {
                Focus(window.HotKeyTextBox, HotKeyFocusTarget.Normal);
                Ui(() => { inactive.Show(); inactive.Activate(); });
                Assert(window.HotKeyFocus == HotKeyFocusTarget.None, "Inactive form retained assignment focus.");
                int previous = RazorEnhanced.HotKey.Executed;
                Assert(!RazorEnhanced.HotKey.KeyDown(Keys.F4), "Binding pass flag changed.");
                Assert(RazorEnhanced.HotKey.Executed == previous + 1, "Inactive UI swallowed the game binding.");
            });
            Check("reactivation restores the focused assignment", () => Focus(window.HotKeyTextBox, HotKeyFocusTarget.Normal));
            Check("hide clears focus", () => { Ui(() => window.Hide()); Assert(window.HotKeyFocus == HotKeyFocusTarget.None, "Hidden form retained assignment focus."); });
            Check("show restores focus through events", () => Focus(window.MacroHotKeyTextBox, HotKeyFocusTarget.Macro));
            Check("handle recreation retains native focus snapshot", () =>
            {
                Focus(window.HotKeyKeyMasterTextBox, HotKeyFocusTarget.Master);
                Ui(() =>
                {
                    window.RecreateWindowHandle();
                    Assert(window.HotKeyKeyMasterTextBox.Focused, "Native focus was not restored after handle recreation.");
                    Assert(window.HotKeyFocus == HotKeyFocusTarget.Master, "Restored native focus was not published.");
                });
            });
            Check("disabling a focused input follows native replacement focus", () =>
            {
                Ui(() =>
                {
                    window.HotKeyKeyMasterTextBox.Enabled = false;
                    HotKeyFocusTarget nativeFocus = window.HotKeyTextBox.Focused ? HotKeyFocusTarget.Normal
                        : window.MacroHotKeyTextBox.Focused ? HotKeyFocusTarget.Macro
                        : window.HotKeyKeyMasterTextBox.Focused ? HotKeyFocusTarget.Master : HotKeyFocusTarget.None;
                    Assert(window.HotKeyFocus == nativeFocus, "Disabled-input transition differs from native focus: " + nativeFocus + " / " + window.HotKeyFocus);
                    Assert(!window.HotKeyKeyMasterTextBox.Focused, "WinForms retained native focus on the disabled input.");
                    window.HotKeyKeyMasterTextBox.Enabled = true;
                });
            });

            Focus(window.Other, HotKeyFocusTarget.None);
            RazorEnhanced.Settings.HotKey.Pass = true;
            for (int round = 1; round <= 3; round++)
            {
                int number = round;
                Check("ordinary input busy UI round " + number, () => BusyKey("ordinary-" + number, 350, Keys.F5, true));
            }
            Check("master toggle busy UI disables immediately", () => { RazorEnhanced.Settings.General.Enabled = true; BusyKey("master-disable", 350, Keys.F12, true, () => Assert(!RazorEnhanced.Settings.General.Enabled, "Master did not disable before the UI resumed.")); Ui(() => Assert(window.HotKeyStatusLabel.Text == "状态: 禁用", "Disable display wrong.")); });
            Check("master toggle busy UI enables immediately", () => { BusyKey("master-enable", 350, Keys.F12, true, () => Assert(RazorEnhanced.Settings.General.Enabled, "Master did not enable before the UI resumed.")); Ui(() => Assert(window.HotKeyStatusLabel.Text == "状态: 启用", "Enable display wrong.")); });
            Check("queued status follows newer UI state", () =>
            {
                using (ManualResetEvent entered = new ManualResetEvent(false))
                using (ManualResetEvent release = new ManualResetEvent(false))
                {
                    window.BeginInvoke(new Action(() => { entered.Set(); release.WaitOne(5000); RazorEnhanced.Settings.General.Enabled = true; window.HotKeyStatusLabel.Text = "状态: 启用"; }));
                    Assert(entered.WaitOne(5000), "UI blocker did not start.");
                    try { RazorEnhanced.HotKey.KeyDown(Keys.F12); }
                    finally { release.Set(); }
                    Ui(() => Assert(RazorEnhanced.Settings.General.Enabled && window.HotKeyStatusLabel.Text == "状态: 启用", "An old queued hotkey display overwrote the newer UI setting."));
                }
            });
            Check("CUO focus notification clears assignment while UI is busy", () =>
            {
                Focus(window.HotKeyTextBox, HotKeyFocusTarget.Normal);
                int previous = RazorEnhanced.HotKey.Executed;
                BusyKey("CUO-focus", 350, Keys.F7, true,
                    () => Assert(RazorEnhanced.HotKey.Executed == previous + 1, "A stale assignment swallowed the game hotkey."),
                    () => new ClassicUOClient().OnFocusGained());
                Ui(() =>
                {
                    // The native focus-changing UI messages were withheld during the busy event.
                    // An old focus notification must not resurrect assignment after the host clears it.
                    typeof(Control).GetMethod("OnGotFocus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(window.HotKeyTextBox, new object[] { EventArgs.Empty });
                    Assert(window.HotKeyFocus == HotKeyFocusTarget.None, "A late UI focus event restored stale assignment.");
                    typeof(Form).GetMethod("OnDeactivate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(window, new object[] { EventArgs.Empty });
                    Assert(window.HotKeyFocus == HotKeyFocusTarget.None, "A late deactivation changed the cleared snapshot.");
                });
                Ui(() => inactive.Activate());
                Focus(window.Other, HotKeyFocusTarget.None);
            });
            Check("late CUO focus event cannot divert native assignment input", () =>
            {
                Focus(window.HotKeyTextBox, HotKeyFocusTarget.Normal);
                new ClassicUOClient().OnFocusGained();
                Ui(() =>
                {
                    Assert(window.HotKeyTextBox.Focused && window.ContainsFocus, "RA no longer owns native input.");
                    int previous = RazorEnhanced.HotKey.Executed;
                    Message message = Message.Create(window.HotKeyTextBox.Handle, 0x0100, (IntPtr)Keys.F9, IntPtr.Zero);
                    typeof(RazorEnhanced.UI.RazorHotKeyTextBox).GetMethod("ProcessCmdKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(window.HotKeyTextBox, new object[] { message, Keys.F9 });
                    Assert(RazorEnhanced.HotKey.Executed == previous, "A delayed CUO focus event diverted RA assignment into a game binding.");
                    Assert(RazorEnhanced.HotKey.NormalKey == Keys.F9 && window.HotKeyTextBox.Text == "F9", "Native assignment input was lost after delayed host focus.");
                });
            });
            for (int index = 0; index < inputs.Length; index++)
            {
                TextBox input = inputs[index];
                HotKeyFocusTarget target = targets[index];
                Check(target + " old queued display cannot replace newer native assignment", () =>
                {
                    Focus(input, target);
                    using (ManualResetEvent entered = new ManualResetEvent(false))
                    using (ManualResetEvent release = new ManualResetEvent(false))
                    {
                        window.BeginInvoke(new Action(() =>
                        {
                            entered.Set();
                            release.WaitOne(5000);
                            // UI input already in progress completes before the queued presentation.
                            RazorEnhanced.HotKey.KeyDown(Keys.F10);
                        }));
                        Assert(entered.WaitOne(5000), "Assignment ordering barrier did not start.");
                        try { Assert(!RazorEnhanced.HotKey.KeyDown(Keys.F9), "Old assignment escaped."); }
                        finally { release.Set(); }
                        Ui(() =>
                        {
                            Keys current = target == HotKeyFocusTarget.Master ? RazorEnhanced.HotKey.MasterKey : RazorEnhanced.HotKey.NormalKey;
                            Assert(current == Keys.F10, "New native assignment field was lost.");
                            Assert(input.Text == "F10", "Old queued display replaced newer native assignment: " + input.Text);
                        });
                    }
                });
            }
            for (int index = 0; index < inputs.Length; index++)
            {
                TextBox input = inputs[index];
                HotKeyFocusTarget target = targets[index];
                Check(target + " pending same-key display cannot undo UI clear", () =>
                {
                    Focus(input, target);
                    Ui(() => RazorEnhanced.HotKey.KeyDown(Keys.F9));
                    using (ManualResetEvent entered = new ManualResetEvent(false))
                    using (ManualResetEvent release = new ManualResetEvent(false))
                    {
                        window.BeginInvoke(new Action(() =>
                        {
                            entered.Set();
                            release.WaitOne(5000);
                            // Normal/macro clear and master save update the display on the UI thread.
                            input.Text = String.Empty;
                        }));
                        Assert(entered.WaitOne(5000), "Clear ordering barrier did not start.");
                        try { Assert(!RazorEnhanced.HotKey.KeyDown(Keys.F9), "Pending assignment escaped."); }
                        finally { release.Set(); }
                        Ui(() => Assert(input.Text == String.Empty, "Pending same-key display undid the UI clear: " + input.Text));
                    }
                });
            }
            string[] invalidateButtons = { "hotkeyClearButton", "hotkeyMasterSetButton", "btnMacroClearHotkey", "hotkeyMasterClearButton" };
            int[] buttonInputs = { 0, 1, 2, 1 };
            for (int index = 0; index < invalidateButtons.Length; index++)
            {
                string buttonName = invalidateButtons[index];
                TextBox input = inputs[buttonInputs[index]];
                HotKeyFocusTarget target = targets[buttonInputs[index]];
                Check(buttonName + " cancels pending display even when text was already empty", () =>
                {
                    Focus(input, target);
                    Ui(() => input.Text = String.Empty);
                    using (ManualResetEvent entered = new ManualResetEvent(false))
                    using (ManualResetEvent release = new ManualResetEvent(false))
                    {
                        window.BeginInvoke(new Action(() =>
                        {
                            entered.Set();
                            release.WaitOne(5000);
                            Button button = (Button)typeof(MainForm).GetField(buttonName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window);
                            typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(button, new object[] { EventArgs.Empty });
                        }));
                        Assert(entered.WaitOne(5000), "Button ordering barrier did not start.");
                        try { Assert(!RazorEnhanced.HotKey.KeyDown(Keys.F9), "Pending assignment escaped."); }
                        finally { release.Set(); }
                        Ui(() => Assert(input.Text == String.Empty, "Pending display undid an unchanged-text UI operation: " + input.Text));
                    }
                });
            }
            for (int index = 0; index < 2; index++)
            {
                int oldIndex = index;
                int newIndex = 1 - index;
                Check(targets[oldIndex] + " queued assignment survives independent " + targets[newIndex] + " input", () =>
                {
                    Focus(inputs[oldIndex], targets[oldIndex]);
                    using (ManualResetEvent entered = new ManualResetEvent(false))
                    using (ManualResetEvent release = new ManualResetEvent(false))
                    {
                        window.BeginInvoke(new Action(() =>
                        {
                            entered.Set();
                            release.WaitOne(5000);
                            Assert(inputs[newIndex].Focus(), "New input could not focus.");
                            RazorEnhanced.HotKey.KeyDown(Keys.F10);
                        }));
                        Assert(entered.WaitOne(5000), "Cross-input barrier did not start.");
                        try { Assert(!RazorEnhanced.HotKey.KeyDown(Keys.F9), "Old input escaped."); }
                        finally { release.Set(); }
                        Ui(() =>
                        {
                            Assert(inputs[oldIndex].Text == "F9", "Independent new input discarded the previous field's pending display: " + inputs[oldIndex].Text);
                            Assert(inputs[newIndex].Text == "F10", "New independent input was overwritten.");
                        });
                    }
                });
            }
            for (int index = 0; index < inputs.Length; index++)
            {
                TextBox input = inputs[index];
                HotKeyFocusTarget target = targets[index];
                Check(target + " consecutive queued assignments retain the newest key", () =>
                {
                    Focus(input, target);
                    using (ManualResetEvent entered = new ManualResetEvent(false))
                    using (ManualResetEvent release = new ManualResetEvent(false))
                    {
                        window.BeginInvoke(new Action(() => { entered.Set(); release.WaitOne(5000); }));
                        Assert(entered.WaitOne(5000), "Queued-input barrier did not start.");
                        try
                        {
                            RazorEnhanced.HotKey.KeyDown(Keys.F6);
                            RazorEnhanced.HotKey.KeyDown(Keys.F7);
                            RazorEnhanced.HotKey.KeyDown(Keys.F8);
                        }
                        finally { release.Set(); }
                        Ui(() => Assert(input.Text == "F8", "Queued assignments did not retain their newest value."));
                    }
                });
            }
            for (int index = 0; index < inputs.Length; index++)
            {
                TextBox input = inputs[index];
                HotKeyFocusTarget target = targets[index];
                Check(target + " clear between input capture and presentation enqueue wins", () =>
                {
                    Focus(input, target);
                    Ui(() => input.Text = "F8");
                    int revision = window.GetHotKeyAssignmentRevision(target);
                    if (target == HotKeyFocusTarget.Master) RazorEnhanced.HotKey.MasterKey = Keys.F9;
                    else RazorEnhanced.HotKey.NormalKey = Keys.F9;
                    Ui(() => input.Text = String.Empty);
                    window.PostHotKeyAssignmentUpdate(target, revision, s => input.Text = "F9");
                    Ui(() => Assert(input.Text == String.Empty, "An input captured before UI clear adopted a newer revision while enqueuing."));
                });
            }
            Check("disposed window clears focus and ignores display posts", () =>
            {
                Focus(window.HotKeyTextBox, HotKeyFocusTarget.Normal);
                bool ranAfterDispose = false;
                using (ManualResetEvent entered = new ManualResetEvent(false))
                using (ManualResetEvent release = new ManualResetEvent(false))
                {
                    window.BeginInvoke(new Action(() => { entered.Set(); release.WaitOne(5000); window.Dispose(); }));
                    Assert(entered.WaitOne(5000), "Disposal blocker did not start.");
                    window.PostHotKeyUpdate(s => ranAfterDispose = true);
                    release.Set();
                    inactive.Invoke(new Action(() => { }));
                }
                Assert(window.HotKeyFocus == HotKeyFocusTarget.None, "Disposed form retained focus.");
                Assert(!ranAfterDispose, "A queued display update ran after disposal.");
                window.PostHotKeyUpdate(s => { throw new Exception("Disposed UI received a callback."); });
                RazorEnhanced.Settings.General.Enabled = false;
                Assert(RazorEnhanced.HotKey.KeyDown(Keys.F6), "Disposed UI swallowed disabled input.");
            });
        }
        finally
        {
            if (inactive.IsHandleCreated) inactive.BeginInvoke(new Action(Application.ExitThread));
            if (!ui.Join(5000)) { failed++; Console.WriteLine("FAIL UI thread did not stop."); }
        }
        Console.WriteLine("HOTKEY_FOCUS_REGRESSION passed={0} failed={1}", passed, failed);
        return failed == 0 ? 0 : 1;
    }
}
