using RazorEnhanced;
using RazorEnhanced.UI;
using System;
using System.Windows.Forms;

namespace Assistant
{
    public partial class MainForm : System.Windows.Forms.Form
    {
        internal CheckBox AutolootCheckBox { get { return autoLootCheckBox; } }
        internal RazorAgentNumOnlyTextBox AutolootLabelDelay { get { return autoLootTextBoxDelay; } }
        internal RazorAgentNumOnlyTextBox AutoLootTextBoxMaxRange { get { return autoLootTextBoxMaxRange; } }
        internal Label AutoLootContainerLabel { get { return autolootContainerLabel; } }
        internal ListBox AutoLootLogBox { get { return autolootLogBox; } }
        internal ComboBox AutoLootListSelect { get { return autolootListSelect; } }
        internal CheckBox AutoLootNoOpenCheckBox { get { return autoLootnoopenCheckBox; } }
        internal DataGridView AutoLootDataGridView { get { return autolootdataGridView; } }
        internal CheckBox AutolootAutostartCheckBox { get { return autolootautostartCheckBox; } }

        private void autolootautostartCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (autolootautostartCheckBox.Focused)
                Settings.General.WriteBool("AutolootAutostartCheckBox", autolootautostartCheckBox.Checked);
        }

        private void autolootContainerButton_Click(object sender, EventArgs e)
        {
            AutolootSetBag();
        }

        internal void AutolootSetBag()
        {
            if (showagentmessageCheckBox.Checked)
                RazorEnhanced.Misc.SendMessage("自动拾取：请选择拾取容器", false);

            if (autolootListSelect.Text != String.Empty)
                Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(autoLootSetContainerTarget_Callback));
            else
                AutoLoot.AddLog("未选择物品列表！");
        }

        private void autoLootSetContainerTarget_Callback(bool loc, Assistant.Serial serial, Assistant.Point3D pt, ushort itemid)
        {
            Assistant.Item autoLootBag = Assistant.World.FindItem(serial);

            if (autoLootBag == null)
                return;

            bool bagOfSending = false;
            string prop = Items.GetPropStringByIndex(serial, 0);
            if (prop.IndexOf("bag of sending", 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                bagOfSending = true;

            if (autoLootBag != null && autoLootBag.Serial.IsItem && autoLootBag.IsLootableTarget && (!bagOfSending))
            {
                if (showagentmessageCheckBox.Checked)
                    RazorEnhanced.Misc.SendMessage("自动拾取容器已设置为：" + autoLootBag.ToString(), false);
                RazorEnhanced.AutoLoot.AddLog("自动拾取容器已设置为：" + autoLootBag.ToString());
                AutoLoot.AutoLootBag = (int)autoLootBag.Serial.Value;
            }
            else
            {
                if (showagentmessageCheckBox.Checked)
                    RazorEnhanced.Misc.SendMessage("自动拾取容器无效，已改用背包", false);
                RazorEnhanced.AutoLoot.AddLog("自动拾取容器无效，已改用背包");
                AutoLoot.AutoLootBag = (int)World.Player.Backpack.Serial.Value;
            }
            BeginInvoke((MethodInvoker)delegate
            {
                RazorEnhanced.Settings.AutoLoot.ListUpdate(autolootListSelect.Text, RazorEnhanced.AutoLoot.AutoLootDelay, serial, true, RazorEnhanced.AutoLoot.NoOpenCorpse, RazorEnhanced.AutoLoot.MaxRange);
                RazorEnhanced.AutoLoot.RefreshLists();
            });
        }

        private void autoLootAddItemTarget_Click(object sender, EventArgs e)
        {
            AutolootAddItem();
        }

        internal void AutolootAddItem()
        {
            if (showagentmessageCheckBox.Checked)
                RazorEnhanced.Misc.SendMessage("自动拾取：请选择要添加到列表的物品", false);

            if (autolootListSelect.Text != String.Empty)
                Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(autoLootItemTarget_Callback));
            else
                RazorEnhanced.AutoLoot.AddLog("未选择物品列表！");
        }

        private void autoLootItemTarget_Callback(bool loc, Assistant.Serial serial, Assistant.Point3D pt, ushort itemid)
        {
            Assistant.Item autoLootItem = Assistant.World.FindItem(serial);
            if (autoLootItem != null && autoLootItem.Serial.IsItem)
            {
                if (showagentmessageCheckBox.Checked)
                    RazorEnhanced.Misc.SendMessage("自动拾取：已添加物品：" + autoLootItem.ToString(), false);
                RazorEnhanced.AutoLoot.AddLog("自动拾取：已添加物品：" + autoLootItem.ToString());
                this.Invoke((MethodInvoker)delegate { RazorEnhanced.AutoLoot.AddItemToList(autoLootItem.Name, autoLootItem.TypeID, autoLootItem.Hue); });
            }
            else
            {
                if (showagentmessageCheckBox.Checked)
                    RazorEnhanced.Misc.SendMessage("自动拾取：目标无效", false);
                RazorEnhanced.AutoLoot.AddLog("自动拾取：目标无效");
            }
        }

        private void autoLootItemProps_Click(object sender, EventArgs e)
        {
            if (autolootListSelect.Text != String.Empty)
            {
                if (autolootdataGridView.CurrentCell == null)
                    return;

                DataGridViewRow row = autolootdataGridView.Rows[autolootdataGridView.CurrentCell.RowIndex];
                EnhancedAutolootEditItemProps editProp = new(ref row)
                {
                    TopMost = true
                };
                editProp.Show();
            }
            else
                RazorEnhanced.AutoLoot.AddLog("未选择物品列表！");
        }

        private void autoLootEnable_CheckedChanged(object sender, EventArgs e)
        {
            if (World.Player == null)  // offline
            {
                if (autoLootCheckBox.Checked)
                {
                    AutoLoot.AddLog("尚未登录游戏！");
                    autoLootCheckBox.Checked = false;
                }
                return;
            }

            if (autolootListSelect.Text == String.Empty) // Nessuna lista
            {
                if (autoLootCheckBox.Checked)
                {
                    autoLootCheckBox.Checked = false;
                    AutoLoot.AddLog("未选择物品列表！");
                }
                return;
            }

            if (autoLootCheckBox.Checked)
            {
                autolootListSelect.Enabled = false;
                autolootButtonAddList.Enabled = false;
                autoLootButtonRemoveList.Enabled = false;
                autoLootButtonListClone.Enabled = false;
                autoLootTextBoxDelay.Enabled = false;
                autoLootTextBoxMaxRange.Enabled = false;

                AutoLoot.ResetIgnore();
                AutoLoot.AutoMode = true;
                AutoLoot.AddLog("自动拾取引擎已启动...");
                if (showagentmessageCheckBox.Checked)
                    Misc.SendMessage("自动拾取：引擎已启动...", false);
            }
            else
            {
                autolootListSelect.Enabled = true;
                autolootButtonAddList.Enabled = true;
                autoLootButtonRemoveList.Enabled = true;
                autoLootButtonListClone.Enabled = true;
                autoLootTextBoxDelay.Enabled = true;
                autoLootTextBoxMaxRange.Enabled = true;

                // Stop autoloot
                AutoLoot.AutoMode = false;
                if (showagentmessageCheckBox.Checked)
                    Misc.SendMessage("自动拾取：引擎已停止...", false);
                AutoLoot.AddLog("自动拾取引擎已停止...");
            }
        }


        private void autoLootListSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            AutoLoot.UpdateListParam(autolootListSelect.Text);

            if (autolootListSelect.Focused && autolootListSelect.Text != String.Empty)
            {
                Settings.AutoLoot.ListUpdate(autolootListSelect.Text, AutoLoot.AutoLootDelay, AutoLoot.AutoLootBag, true, AutoLoot.NoOpenCorpse, AutoLoot.MaxRange);
                AutoLoot.AddLog("自动拾取列表已切换为：" + autolootListSelect.Text);
            }

            AutoLoot.InitGrid();
        }
        private void autoLootnoopenCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (autoLootnoopenCheckBox.Focused)
            {
                AutoLoot.NoOpenCorpse = autoLootnoopenCheckBox.Checked;
                RazorEnhanced.Settings.AutoLoot.ListUpdate(autolootListSelect.Text, AutoLoot.AutoLootDelay, AutoLoot.AutoLootBag, true, AutoLoot.NoOpenCorpse, AutoLoot.MaxRange);
                RazorEnhanced.AutoLoot.RefreshLists();
            }
        }

        private void autoLootButtonAddList_Click(object sender, EventArgs e)
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f is EnhancedAgentAddList af)
                {
                    af.AgentID = 1;
                    af.Focus();
                    return;
                }
            }
            new EnhancedAgentAddList(1).Show();
        }
        private void autoLootButtonListClone_Click(object sender, EventArgs e)
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f is EnhancedAgentAddList af)
                {
                    af.AgentID = 10;
                    af.Focus();
                    return;
                }
            }
            new EnhancedAgentAddList(10).Show();
        }

        private void autoLootButtonRemoveList_Click(object sender, EventArgs e)
        {
            if (autolootListSelect.Text != String.Empty)
            {
                var dialogResult = RazorEnhanced.UI.RE_MessageBox.Show("删除自动拾取列表？",
                    $"确定要删除此自动拾取列表吗：\r\n{autolootListSelect.Text}",
                    ok: "是", no: "否", cancel: null, backColor: null);
                if (dialogResult == DialogResult.OK)
                {
                    RazorEnhanced.AutoLoot.AddLog("自动拾取列表 " + autolootListSelect.Text + " 已删除！");
                    RazorEnhanced.AutoLoot.AutoLootBag = 0;
                    RazorEnhanced.AutoLoot.AutoLootDelay = 100;
                    RazorEnhanced.AutoLoot.NoOpenCorpse = false;
                    RazorEnhanced.AutoLoot.MaxRange = 1;
                    RazorEnhanced.AutoLoot.RemoveList(autolootListSelect.Text);
                    autolootListSelect.SelectedIndex = -1;

                }
            }
        }

        private void autoLootTextBoxDelay_Leave(object sender, EventArgs e)
        {

            if (autoLootTextBoxDelay.Text == String.Empty)
                autoLootTextBoxDelay.Text = "100";

            AutoLoot.AutoLootDelay = Convert.ToInt32(autoLootTextBoxDelay.Text);

            RazorEnhanced.Settings.AutoLoot.ListUpdate(autolootListSelect.Text, AutoLoot.AutoLootDelay, AutoLoot.AutoLootBag, true, AutoLoot.NoOpenCorpse, AutoLoot.MaxRange);
            RazorEnhanced.AutoLoot.RefreshLists();
        }

        private void autoLootTextBoxMaxRange_Leave(object sender, EventArgs e)
        {
            if (autoLootTextBoxMaxRange.Text == String.Empty)
                autoLootTextBoxMaxRange.Text = "0";

            AutoLoot.MaxRange = Convert.ToInt32(autoLootTextBoxMaxRange.Text);

            RazorEnhanced.Settings.AutoLoot.ListUpdate(autolootListSelect.Text, AutoLoot.AutoLootDelay, AutoLoot.AutoLootBag, true, AutoLoot.NoOpenCorpse, AutoLoot.MaxRange);
            RazorEnhanced.AutoLoot.RefreshLists();
        }

        private void autolootdataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            DataGridViewCell cell = autolootdataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex];

            if (e.ColumnIndex == 3)
            {
                cell.Value = Utility.FormatDatagridColorCell(cell);
            }
            else if (e.ColumnIndex == 2)
            {
                cell.Value = Utility.FormatDatagridItemIDCellAutoLoot(cell);
            }
            RazorEnhanced.AutoLoot.CopyTable();
        }

        private void autolootdataGridView_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells[0].Value = false;
            e.Row.Cells[1].Value = "新物品";
            e.Row.Cells[2].Value = "0x0000";
            e.Row.Cells[3].Value = "0x0000";
            e.Row.Cells[4].Value = null;
        }
    }
}
