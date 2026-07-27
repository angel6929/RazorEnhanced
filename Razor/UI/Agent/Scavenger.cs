using RazorEnhanced;
using RazorEnhanced.UI;
using System;
using System.Windows.Forms;

namespace Assistant
{
    public partial class MainForm : System.Windows.Forms.Form
    {
        internal CheckBox ScavengerCheckBox { get { return scavengerCheckBox; } }
        internal RazorAgentNumOnlyTextBox ScavengerDragDelay { get { return scavengerDragDelay; } }
        internal RazorAgentNumOnlyTextBox ScavengerRange { get { return scavengerRange; } }
        internal Label ScavengerContainerLabel { get { return scavengerContainerLabel; } }
        internal ListBox ScavengerLogBox { get { return scavengerLogBox; } }
        internal ComboBox ScavengerListSelect { get { return scavengerListSelect; } }
        internal DataGridView ScavengerDataGridView { get { return scavengerdataGridView; } }
        internal CheckBox ScavengerAutostartCheckBox { get { return scavengerautostartCheckBox; } }

        private void scavengerautostartCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (scavengerautostartCheckBox.Focused)
                Settings.General.WriteBool("ScavengerAutostartCheckBox", scavengerautostartCheckBox.Checked);
        }

        private void scavengerEditProps_Click(object sender, EventArgs e)
        {
            if (scavengerListSelect.Text != String.Empty)
            {
                if (scavengerdataGridView.CurrentCell == null)
                    return;

                DataGridViewRow row = scavengerdataGridView.Rows[scavengerdataGridView.CurrentCell.RowIndex];
                EnhancedScavengerEditItemProps editProp = new(ref row)
                {
                    TopMost = true
                };
                editProp.Show();
            }
            else
                Scavenger.AddLog("未选择物品列表！");
        }

        private void scavengerAddItemTarget_Click(object sender, EventArgs e)
        {
            ScavengerAddItem();
        }

        internal void ScavengerAddItem()
        {
            if (showagentmessageCheckBox.Checked)
                Misc.SendMessage("拾荒：请选择要添加到列表的物品", false);

            if (scavengerListSelect.Text != String.Empty)
                Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(ScavengerItemTarget_Callback));
            else
                Scavenger.AddLog("未选择物品列表！");
        }

        private void ScavengerItemTarget_Callback(bool loc, Serial serial, Point3D pt, ushort itemid)
        {
            Item scavengerItem = World.FindItem(serial);
            if (scavengerItem != null && scavengerItem.Serial.IsItem)
            {
                if (showagentmessageCheckBox.Checked)
                    Misc.SendMessage("拾荒：已添加物品：" + scavengerItem.ToString(), false);
                Scavenger.AddLog("拾荒：已添加物品：" + scavengerItem.ToString());
                this.Invoke((MethodInvoker)delegate { Scavenger.AddItemToList(scavengerItem.Name, scavengerItem.TypeID, scavengerItem.Hue); });
            }
            else
            {
                if (showagentmessageCheckBox.Checked)
                    Misc.SendMessage("拾荒：目标无效", false);
                Scavenger.AddLog("拾荒：目标无效");
            }
        }

        private void scavengerSetContainer_Click(object sender, EventArgs e)
        {
            ScavengerSetBag();
        }

        internal void ScavengerSetBag()
        {
            if (showagentmessageCheckBox.Checked)
                RazorEnhanced.Misc.SendMessage("拾荒：请选择拾荒容器", false);

            if (scavengerListSelect.Text != String.Empty)
                Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(ScavengerItemContainerTarget_Callback));
            else
                RazorEnhanced.Scavenger.AddLog("未选择物品列表！");
        }

        private void ScavengerItemContainerTarget_Callback(bool loc, Assistant.Serial serial, Assistant.Point3D pt, ushort itemid)
        {
            Assistant.Item scavengerBag = Assistant.World.FindItem(serial);

            if (scavengerBag == null)
                return;

            bool bagOfSending = false;
            string prop = Items.GetPropStringByIndex(serial, 0);
            if (prop.IndexOf("bag of sending", 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                bagOfSending = true;

            if (scavengerBag != null && scavengerBag.Serial.IsItem && scavengerBag.IsLootableTarget && (!bagOfSending))
            {
                if (showagentmessageCheckBox.Checked)
                    RazorEnhanced.Misc.SendMessage("拾荒容器已设置为：" + scavengerBag.ToString(), false);
                RazorEnhanced.Scavenger.AddLog("拾荒容器已设置为：" + scavengerBag.ToString());
                Scavenger.ScavengerBag = (int)scavengerBag.Serial.Value;
            }
            else
            {
                if (showagentmessageCheckBox.Checked)
                    RazorEnhanced.Misc.SendMessage("拾荒容器无效，已改用背包", false);
                RazorEnhanced.Scavenger.AddLog("拾荒容器无效，已改用背包");
                Scavenger.ScavengerBag = (int)World.Player.Backpack.Serial.Value;
            }

            this.Invoke((MethodInvoker)delegate
            {
                RazorEnhanced.Settings.Scavenger.ListUpdate(scavengerListSelect.Text, RazorEnhanced.Scavenger.ScavengerDelay, serial, true, Scavenger.MaxRange);
                RazorEnhanced.Scavenger.RefreshLists();
            });
        }

        private void scavengerAddList_Click(object sender, EventArgs e)
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f is EnhancedAgentAddList af)
                {
                    af.AgentID = 2;
                    af.Focus();
                    return;
                }
            }
            new EnhancedAgentAddList(2).Show();
        }

        private void scavengerButtonClone_Click(object sender, EventArgs e)
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f is EnhancedAgentAddList af)
                {
                    af.AgentID = 11;
                    af.Focus();
                    return;
                }
            }
            new EnhancedAgentAddList(11).Show();
        }

        private void scavengerRemoveList_Click(object sender, EventArgs e)
        {
            if (scavengerListSelect.Text != String.Empty)
            {
                var dialogResult = RazorEnhanced.UI.RE_MessageBox.Show("删除拾荒列表？",
                    $"确定要删除此拾荒列表吗：\r\n{scavengerListSelect.Text}",
                    ok: "是", no: "否", cancel: null, backColor: null);
                if (dialogResult == DialogResult.OK)
                {
                    RazorEnhanced.Scavenger.AddLog("拾荒列表 " + scavengerListSelect.Text + " 已删除！");
                    RazorEnhanced.Scavenger.ScavengerBag = 0;
                    RazorEnhanced.Scavenger.RemoveList(scavengerListSelect.Text);
                }
            }
        }

        private void scavengertListSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            Scavenger.UpdateListParam(scavengerListSelect.Text);

            if (scavengerListSelect.Focused && scavengerListSelect.Text != String.Empty)
            {
                Settings.Scavenger.ListUpdate(scavengerListSelect.Text, Scavenger.ScavengerDelay, Scavenger.ScavengerBag, true, Scavenger.MaxRange);
                Scavenger.AddLog("拾荒列表已切换为：" + scavengerListSelect.Text);
            }

            Scavenger.InitGrid();
        }

        private void scavengerEnableCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (World.Player == null)  // offline
            {
                if (scavengerCheckBox.Checked)
                {
                    scavengerCheckBox.Checked = false;
                    Scavenger.AddLog("尚未登录游戏！");
                }
                return;
            }

            if (scavengerListSelect.Text == String.Empty) // Nessuna lista
            {
                if (scavengerCheckBox.Checked)
                {
                    scavengerCheckBox.Checked = false;
                    Scavenger.AddLog("未选择物品列表！");
                }
                return;
            }

            if (scavengerCheckBox.Checked)
            {
                ScavengerListSelect.Enabled = false;
                scavengerButtonAddList.Enabled = false;
                scavengerButtonRemoveList.Enabled = false;
                scavengerButtonClone.Enabled = false;
                scavengerDragDelay.Enabled = false;
                scavengerRange.Enabled = false;

                Scavenger.ResetIgnore();
                Scavenger.AutoMode = true;
                Scavenger.AddLog("拾荒引擎已启动...");
                if (showagentmessageCheckBox.Checked)
                    Misc.SendMessage("拾荒：引擎已启动...", false);
            }
            else
            {
                ScavengerListSelect.Enabled = true;
                scavengerButtonAddList.Enabled = true;
                scavengerButtonRemoveList.Enabled = true;
                scavengerButtonClone.Enabled = true;
                scavengerDragDelay.Enabled = true;
                scavengerRange.Enabled = true;

                Scavenger.AutoMode = false;
                Scavenger.AddLog("拾荒引擎已停止...");
                if (showagentmessageCheckBox.Checked)
                    Misc.SendMessage("拾荒：引擎已停止...", false);
            }
        }

        private void scavengerDragDelay_Leave(object sender, EventArgs e)
        {
            if (scavengerDragDelay.Text == String.Empty)
                scavengerDragDelay.Text = "100";

            Scavenger.ScavengerDelay = Convert.ToInt32(scavengerDragDelay.Text);

            RazorEnhanced.Settings.Scavenger.ListUpdate(scavengerListSelect.Text, Scavenger.ScavengerDelay, Scavenger.ScavengerBag, true, Scavenger.MaxRange);
            RazorEnhanced.Scavenger.RefreshLists();
        }

        private void scavengerRange_Leave(object sender, EventArgs e)
        {
            if (scavengerRange.Text == String.Empty)
                scavengerRange.Text = "0";

            Scavenger.MaxRange = Convert.ToInt32(scavengerRange.Text);

            RazorEnhanced.Settings.Scavenger.ListUpdate(scavengerListSelect.Text, Scavenger.ScavengerDelay, Scavenger.ScavengerBag, true, Scavenger.MaxRange);
            RazorEnhanced.Scavenger.RefreshLists();
        }

        private void scavengerdataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            DataGridViewCell cell = scavengerdataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (e.ColumnIndex == 3)
            {
                cell.Value = Utility.FormatDatagridColorCell(cell);
            }
            else if (e.ColumnIndex == 2)
            {
                cell.Value = Utility.FormatDatagridItemIDCell(cell);
            }

            RazorEnhanced.Scavenger.CopyTable();
        }
        private void scavengerdataGridView_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells[0].Value = false;
            e.Row.Cells[1].Value = "新物品";
            e.Row.Cells[2].Value = "0x0000";
            e.Row.Cells[3].Value = "0x0000";
            e.Row.Cells[4].Value = null;
        }
    }
}
