using RazorEnhanced;
using System;
using System.Threading;
using System.Windows.Forms;

namespace Assistant
{
    internal enum HotKeyFocusTarget
    {
        None,
        Normal,
        Master,
        Macro
    }

    public partial class MainForm : System.Windows.Forms.Form
    {
        internal TextBox HotKeyTextBox { get { return hotkeytextbox; } }
        internal TextBox MacroHotKeyTextBox { get { return macroHotkeyTextBox; } }
        internal TreeView HotKeyTreeView { get { return hotkeytreeView; } }
        internal Label HotKeyKeyMasterLabel { get { return hotkeyKeyMasterLabel; } }
        internal Label HotKeyStatusLabel { get { return hotkeyStatusLabel; } }
        internal TextBox HotKeyKeyMasterTextBox { get { return hotkeyKeyMasterTextBox; } }

        private volatile HotKeyFocusTarget m_hotKeyFocus;
        private volatile bool m_hotKeyWindowActive;
        private readonly int[] m_hotKeyTextRevisions = new int[4];
        private readonly int m_hotKeyUiThreadId = Environment.CurrentManagedThreadId;
        internal HotKeyFocusTarget HotKeyFocus { get { return m_hotKeyWindowActive ? m_hotKeyFocus : HotKeyFocusTarget.None; } }

        internal HotKeyFocusTarget GetHotKeyFocusForInput()
        {
            // Native Razor textbox/mouse input can arrive before CUO drains an old
            // focus notification. Only the owning UI thread may refresh live focus.
            if (Environment.CurrentManagedThreadId == m_hotKeyUiThreadId && ContainsFocus)
            {
                m_hotKeyWindowActive = true;
                UpdateHotKeyFocus(this, EventArgs.Empty);
            }

            return HotKeyFocus;
        }

        private void InitializeHotKeyFocusTracking()
        {
            foreach (Control input in new Control[] { hotkeytextbox, hotkeyKeyMasterTextBox, macroHotkeyTextBox })
            {
                input.GotFocus += UpdateHotKeyFocus;
                input.LostFocus += UpdateHotKeyFocus;
                HotKeyFocusTarget target = input == hotkeytextbox ? HotKeyFocusTarget.Normal
                    : input == hotkeyKeyMasterTextBox ? HotKeyFocusTarget.Master : HotKeyFocusTarget.Macro;
                input.TextChanged += (s, e) => InvalidateHotKeyAssignmentUpdate(target);
            }

            hotkeyClearButton.Click += (s, e) => InvalidateHotKeyAssignmentUpdate(HotKeyFocusTarget.Normal);
            hotkeyMasterSetButton.Click += (s, e) => InvalidateHotKeyAssignmentUpdate(HotKeyFocusTarget.Master);
            hotkeyMasterClearButton.Click += (s, e) => InvalidateHotKeyAssignmentUpdate(HotKeyFocusTarget.Master);
            btnMacroClearHotkey.Click += (s, e) => InvalidateHotKeyAssignmentUpdate(HotKeyFocusTarget.Macro);
            Activated += HotKeyWindowActivated;
            Deactivate += ClearHotKeyFocus;
            VisibleChanged += UpdateHotKeyFocus;
            HandleDestroyed += ClearHotKeyFocus;
            HandleCreated += HotKeyWindowHandleCreated;
            UpdateHotKeyFocus(this, EventArgs.Empty);
        }

        private void HotKeyWindowActivated(object sender, EventArgs e)
        {
            m_hotKeyWindowActive = true;
            UpdateHotKeyFocus(sender, e);
        }

        private void HotKeyWindowHandleCreated(object sender, EventArgs e)
        {
            m_hotKeyWindowActive = ContainsFocus;
            UpdateHotKeyFocus(sender, e);
        }

        internal void ClearHotKeyFocus(object sender, EventArgs e)
        {
            m_hotKeyWindowActive = false;
            m_hotKeyFocus = HotKeyFocusTarget.None;
        }

        private void UpdateHotKeyFocus(object sender, EventArgs e)
        {
            // Only the UI thread reads controls. The game input thread reads one snapshot.
            m_hotKeyFocus = !m_hotKeyWindowActive || !Visible || IsDisposed
                ? HotKeyFocusTarget.None
                : hotkeytextbox.Focused ? HotKeyFocusTarget.Normal
                : macroHotkeyTextBox.Focused ? HotKeyFocusTarget.Macro
                : hotkeyKeyMasterTextBox.Focused ? HotKeyFocusTarget.Master
                : HotKeyFocusTarget.None;
        }

        private void InvalidateHotKeyAssignmentUpdate(HotKeyFocusTarget target)
        {
            Interlocked.Increment(ref m_hotKeyTextRevisions[(int)target]);
        }

        internal int GetHotKeyAssignmentRevision(HotKeyFocusTarget target)
        {
            return Volatile.Read(ref m_hotKeyTextRevisions[(int)target]);
        }

        internal void PostHotKeyAssignmentUpdate(HotKeyFocusTarget target, int revision, Action<MainForm> update)
        {
            PostHotKeyUpdate(s =>
            {
                if (revision == Volatile.Read(ref m_hotKeyTextRevisions[(int)target]))
                {
                    update(s);
                }
            });
        }

        internal void PostHotKeyUpdate(Action<MainForm> update)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;

            if (!InvokeRequired)
            {
                update(this);
                return;
            }

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed && !Disposing)
                        update(this);
                }));
            }
            catch (InvalidOperationException)
            {
                // The UI can destroy its handle between the check and BeginInvoke.
            }
        }

        private void hotkeySetButton_Click(object sender, EventArgs e)
        {
            if (hotkeytreeView.SelectedNode != null && hotkeytreeView.SelectedNode.Name != null && hotkeytextbox.Text != String.Empty && hotkeytextbox.Text != RazorEnhanced.HotKey.KeyString(Keys.None))
            {
                if (hotkeytreeView.SelectedNode.Name == String.Empty)
                {
                    return;
                }
                if (hotkeytreeView.SelectedNode.Parent.Name != null && hotkeytreeView.SelectedNode.Parent.Name == "TList")
                    RazorEnhanced.HotKey.UpdateTargetKey(hotkeytreeView.SelectedNode, hotkeypassCheckBox.Checked);     // Aggiorno hotkey target
                else if (hotkeytreeView.SelectedNode.Parent.Name != null && hotkeytreeView.SelectedNode.Parent.Name == "SList")
                {
                    RazorEnhanced.HotKey.UpdateScriptKey(hotkeytreeView.SelectedNode, hotkeypassCheckBox.Checked);     // Aggiorno hotkey Script
                    // Can refresh the script tables, but it causes hotkey tables to collapse. not worth it
                    Scripts.PatchUpHotkeys(hotkeytreeView.SelectedNode.Name);
                }
                else if (hotkeytreeView.SelectedNode.Parent.Name != null && hotkeytreeView.SelectedNode.Parent.Name == "MList")
                {
                    RazorEnhanced.HotKey.UpdateMacroKey(hotkeytreeView.SelectedNode, hotkeypassCheckBox.Checked);     // Aggiorno hotkey Macro
                }
                else if (hotkeytreeView.SelectedNode.Parent.Name != null && hotkeytreeView.SelectedNode.Parent.Name == "DList")
                {
                    RazorEnhanced.HotKey.UpdateDressKey(hotkeytreeView.SelectedNode, hotkeypassCheckBox.Checked);     // Aggiorno hotkey Dress List
                }
                else
                    RazorEnhanced.HotKey.UpdateKey(hotkeytreeView.SelectedNode, hotkeypassCheckBox.Checked);
            }
        }

        private void hotkeyClearButton_Click(object sender, EventArgs e)
        {
            if (hotkeytreeView.SelectedNode != null && hotkeytreeView.SelectedNode.Name != null)
            {
                if (hotkeytreeView.SelectedNode.Name == String.Empty)
                {
                    return;
                }
                if (hotkeytreeView.SelectedNode.Parent.Name != null)
                    RazorEnhanced.HotKey.ClearKey(hotkeytreeView.SelectedNode, hotkeytreeView.SelectedNode.Parent.Name);
                else
                    RazorEnhanced.HotKey.ClearKey(hotkeytreeView.SelectedNode, "General");
            }
            hotkeytextbox.Text = RazorEnhanced.HotKey.KeyString(Keys.None);
        }

        private void hotkeytreeView_AfterSelect(object sender, System.Windows.Forms.TreeViewEventArgs e)
        {
            if (hotkeytreeView.SelectedNode != null && hotkeytreeView.SelectedNode.Name != null)
            {
                Keys k;
                RazorEnhanced.Settings.HotKey.FindKeyGui(hotkeytreeView.SelectedNode.Name, out k, out bool passkey);
                hotkeytextbox.Text = HotKey.KeyString(k);
                hotkeypassCheckBox.Checked = passkey;
                hotkeytextbox.LastKey = Keys.None;
                HotKey.NormalKey = k;
            }
        }

        private void hotkeyMasterSetButton_Click(object sender, EventArgs e)
        {
            if (hotkeyKeyMasterTextBox.Text != String.Empty && hotkeyKeyMasterTextBox.Text != RazorEnhanced.HotKey.KeyString(Keys.None))
            {
                RazorEnhanced.HotKey.UpdateMaster();
                hotkeyKeyMasterTextBox.Text = String.Empty;
            }
        }

        private void hotkeyMasterClearButton_Click(object sender, EventArgs e)
        {
            RazorEnhanced.HotKey.ClearMasterKey();
        }

        private void hotkeyEnableButton_Click(object sender, EventArgs e)
        {
            Assistant.Engine.MainWindow.HotKeyStatusLabel.Text = "状态: 启用";
            RazorEnhanced.Settings.General.WriteBool("HotKeyEnable", true);
            if (World.Player != null)
                RazorEnhanced.Misc.SendMessage("HotKey: ENABLED", 168, false);
        }

        private void hotkeyDisableButton_Click(object sender, EventArgs e)
        {
            RazorEnhanced.Settings.General.WriteBool("HotKeyEnable", false);
            Assistant.Engine.MainWindow.HotKeyStatusLabel.Text = "状态: 禁用";
            if (World.Player != null)
                RazorEnhanced.Misc.SendMessage("HotKey: DISABLED", 37, false);
        }

        private void HotKey_MouseRoll(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Delta > 0)
                RazorEnhanced.HotKey.KeyDown((Keys)502 | Control.ModifierKeys);
            else if (e.Delta < 0)
                RazorEnhanced.HotKey.KeyDown((Keys)501 | Control.ModifierKeys);
        }

        private void HotKey_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
                RazorEnhanced.HotKey.KeyDown((Keys)500 | Control.ModifierKeys);
            else if (e.Button == MouseButtons.XButton1)
                RazorEnhanced.HotKey.KeyDown((Keys)503 | Control.ModifierKeys);
            else if (e.Button == MouseButtons.XButton2)
                RazorEnhanced.HotKey.KeyDown((Keys)504 | Control.ModifierKeys);
        }

    }
}
