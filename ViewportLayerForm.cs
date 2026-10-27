using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Font = System.Drawing.Font;

namespace XY_Toolbox
{
    /// <summary>
    /// 视口图层样式悬浮修改器 (当前视口颜色与线型覆盖 + 极速批量置灰模式)
    /// </summary>
    public class ViewportLayerForm : Form
    {
        private static ViewportLayerForm _instance;

        // 界面控件
        private Panel pnlHeader;
        private Label lblHeaderTitle;
        private Label lblMin;
        private Label lblClose;
        private Button btnPickLayer;
        private Panel pnlInfoCard;
        private Label lblLayerName;
        private Label lblStatusColor;
        private Label lblStatusLinetype;
        private Panel pnlColorPreview;
        private Button btnSetColor;
        private Button btnSetLinetype;
        private Panel pnlQuickGrayBar;
        private Button btnResetOverride;
        private Button btnQuickBatchMode;

        // 折叠/最小化状态支持
        private bool _isMiniMode = false;
        private Size _normalSize = new Size(430, 325);
        private Size _miniSize = new Size(260, 34);

        // 当前选中的图层与视口状态
        private string _currentLayer = "";
        private ObjectId _currentVpId = ObjectId.Null;

        // 支持拖拽窗口 Windows API
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        public static void ShowFloatingPalette()
        {
            try
            {
                if (_instance != null && !_instance.IsDisposed)
                {
                    if (_instance.WindowState == FormWindowState.Minimized)
                        _instance.WindowState = FormWindowState.Normal;
                    _instance.BringToFront();
                    _instance.CheckAndRefreshViewport(false);
                    return;
                }

                _instance = new ViewportLayerForm();
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModelessDialog(null, _instance, false);
            }
            catch (System.Exception ex)
            {
                try
                {
                    Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    if (doc != null) doc.Editor.WriteMessage("\n[XY工具箱] 打开视口面板失败: " + ex.Message);
                }
                catch { }
            }
        }

        public ViewportLayerForm()
        {
            InitializeComponent();
            CheckAndRefreshViewport(false);
        }

        private void InitializeComponent()
        {
            this.Text = "视口图层样式覆盖";
            this.Size = _normalSize;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = System.Drawing.Color.FromArgb(24, 27, 34);
            this.ForeColor = System.Drawing.Color.White;
            this.Font = new Font("SimSun", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.ShowInTaskbar = false;

            // 1. 顶部 Header 拖拽条 (带最小化和关闭按钮)
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 34;
            pnlHeader.BackColor = System.Drawing.Color.FromArgb(18, 20, 26);
            pnlHeader.MouseDown += OnHeaderMouseDown;

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "👁️ 视口图层样式覆盖 (当前视口)";
            lblHeaderTitle.Font = new Font("SimSun", 9F, FontStyle.Bold);
            lblHeaderTitle.ForeColor = System.Drawing.Color.FromArgb(255, 215, 0); // 金色
            lblHeaderTitle.Location = new Point(12, 9);
            lblHeaderTitle.AutoSize = true;
            lblHeaderTitle.MouseDown += OnHeaderMouseDown;
            pnlHeader.Controls.Add(lblHeaderTitle);

            // 最小化按钮
            lblMin = new Label();
            lblMin.Text = "—";
            lblMin.Font = new Font("SimSun", 9F, FontStyle.Bold);
            lblMin.ForeColor = System.Drawing.Color.FromArgb(170, 180, 195);
            lblMin.Location = new Point(375, 8);
            lblMin.Size = new Size(20, 20);
            lblMin.TextAlign = ContentAlignment.MiddleCenter;
            lblMin.Cursor = Cursors.Hand;
            lblMin.Click += (s, e) => ToggleMiniMode();
            lblMin.MouseEnter += (s, e) => lblMin.ForeColor = System.Drawing.Color.White;
            lblMin.MouseLeave += (s, e) => lblMin.ForeColor = System.Drawing.Color.FromArgb(170, 180, 195);
            pnlHeader.Controls.Add(lblMin);

            // 关闭按钮
            lblClose = new Label();
            lblClose.Text = "✕";
            lblClose.Font = new Font("SimSun", 10F, FontStyle.Bold);
            lblClose.ForeColor = System.Drawing.Color.FromArgb(170, 180, 195);
            lblClose.Location = new Point(402, 7);
            lblClose.Size = new Size(20, 20);
            lblClose.TextAlign = ContentAlignment.MiddleCenter;
            lblClose.Cursor = Cursors.Hand;
            lblClose.Click += (s, e) => this.Close();
            lblClose.MouseEnter += (s, e) => lblClose.ForeColor = System.Drawing.Color.FromArgb(255, 80, 80);
            lblClose.MouseLeave += (s, e) => lblClose.ForeColor = System.Drawing.Color.FromArgb(170, 180, 195);
            pnlHeader.Controls.Add(lblClose);

            this.Controls.Add(pnlHeader);

            // 2. 通栏核心大按钮: 【拾取图层】
            btnPickLayer = new Button();
            btnPickLayer.Text = "🎯  拾 取 图 层  (穿透图块点选)";
            btnPickLayer.Font = new Font("SimSun", 10.5F, FontStyle.Bold);
            btnPickLayer.Location = new Point(16, 44);
            btnPickLayer.Size = new Size(398, 38);
            btnPickLayer.BackColor = System.Drawing.Color.FromArgb(36, 42, 54);
            btnPickLayer.ForeColor = System.Drawing.Color.FromArgb(255, 220, 110);
            btnPickLayer.FlatStyle = FlatStyle.Flat;
            btnPickLayer.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(210, 160, 20);
            btnPickLayer.FlatAppearance.BorderSize = 1;
            btnPickLayer.Cursor = Cursors.Hand;
            btnPickLayer.Click += BtnPickLayer_Click;
            this.Controls.Add(btnPickLayer);

            // 3. 当前图层状态信息卡片 (加宽舒适排版)
            pnlInfoCard = new Panel();
            pnlInfoCard.Location = new Point(16, 88);
            pnlInfoCard.Size = new Size(398, 52);
            pnlInfoCard.BackColor = System.Drawing.Color.FromArgb(30, 34, 44);
            pnlInfoCard.BorderStyle = BorderStyle.FixedSingle;

            lblLayerName = new Label();
            lblLayerName.Text = "当前图层: 请先点击上方【拾取图层】选择图元";
            lblLayerName.Font = new Font("SimSun", 9.5F, FontStyle.Bold);
            lblLayerName.ForeColor = System.Drawing.Color.FromArgb(100, 200, 255);
            lblLayerName.Location = new Point(10, 6);
            lblLayerName.Size = new Size(376, 18);
            pnlInfoCard.Controls.Add(lblLayerName);

            pnlColorPreview = new Panel();
            pnlColorPreview.Location = new Point(12, 28);
            pnlColorPreview.Size = new Size(16, 16);
            pnlColorPreview.BackColor = System.Drawing.Color.Gray;
            pnlColorPreview.BorderStyle = BorderStyle.FixedSingle;
            pnlInfoCard.Controls.Add(pnlColorPreview);

            lblStatusColor = new Label();
            lblStatusColor.Text = "视口颜色: 默认";
            lblStatusColor.ForeColor = System.Drawing.Color.FromArgb(210, 215, 225);
            lblStatusColor.Location = new Point(34, 29);
            lblStatusColor.Size = new Size(170, 16);
            pnlInfoCard.Controls.Add(lblStatusColor);

            lblStatusLinetype = new Label();
            lblStatusLinetype.Text = "线型: Continuous";
            lblStatusLinetype.ForeColor = System.Drawing.Color.FromArgb(210, 215, 225);
            lblStatusLinetype.Location = new Point(215, 29);
            lblStatusLinetype.Size = new Size(170, 16);
            pnlInfoCard.Controls.Add(lblStatusLinetype);

            this.Controls.Add(pnlInfoCard);

            // 4. 双核心操作大按钮: 【视口颜色】 和 【视口线型】
            btnSetColor = new Button();
            btnSetColor.Text = "🎨 视口颜色";
            btnSetColor.Font = new Font("SimSun", 9.5F, FontStyle.Bold);
            btnSetColor.Location = new Point(16, 146);
            btnSetColor.Size = new Size(194, 36);
            btnSetColor.BackColor = System.Drawing.Color.FromArgb(0, 120, 215);
            btnSetColor.ForeColor = System.Drawing.Color.White;
            btnSetColor.FlatStyle = FlatStyle.Flat;
            btnSetColor.FlatAppearance.BorderSize = 0;
            btnSetColor.Cursor = Cursors.Hand;
            btnSetColor.Click += BtnSetColor_Click;
            this.Controls.Add(btnSetColor);

            btnSetLinetype = new Button();
            btnSetLinetype.Text = "〰️ 视口线型";
            btnSetLinetype.Font = new Font("SimSun", 9.5F, FontStyle.Bold);
            btnSetLinetype.Location = new Point(220, 146);
            btnSetLinetype.Size = new Size(194, 36);
            btnSetLinetype.BackColor = System.Drawing.Color.FromArgb(105, 65, 165);
            btnSetLinetype.ForeColor = System.Drawing.Color.White;
            btnSetLinetype.FlatStyle = FlatStyle.Flat;
            btnSetLinetype.FlatAppearance.BorderSize = 0;
            btnSetLinetype.Cursor = Cursors.Hand;
            btnSetLinetype.Click += BtnSetLinetype_Click;
            this.Controls.Add(btnSetLinetype);

            // 5. 施工图高频一键置灰快捷色条与清除覆盖 (单行不折行舒适排版)
            pnlQuickGrayBar = new Panel();
            pnlQuickGrayBar.Location = new Point(16, 188);
            pnlQuickGrayBar.Size = new Size(398, 62);
            pnlQuickGrayBar.BackColor = System.Drawing.Color.FromArgb(20, 23, 29);
            pnlQuickGrayBar.BorderStyle = BorderStyle.FixedSingle;

            Label lblQuick = new Label();
            lblQuick.Text = "常用一键弱化置灰:";
            lblQuick.ForeColor = System.Drawing.Color.FromArgb(170, 180, 195);
            lblQuick.Location = new Point(8, 6);
            lblQuick.AutoSize = true;
            pnlQuickGrayBar.Controls.Add(lblQuick);

            FlowLayoutPanel flowColors = new FlowLayoutPanel();
            flowColors.Location = new Point(6, 26);
            flowColors.Size = new Size(290, 30);
            flowColors.WrapContents = false;

            AddQuickColorBtn(flowColors, "8号暗灰", 8, System.Drawing.Color.FromArgb(128, 128, 128));
            AddQuickColorBtn(flowColors, "252灰", 252, System.Drawing.Color.FromArgb(160, 160, 160));
            AddQuickColorBtn(flowColors, "253浅灰", 253, System.Drawing.Color.FromArgb(192, 192, 192));
            AddQuickColorBtn(flowColors, "7号白", 7, System.Drawing.Color.White);
            pnlQuickGrayBar.Controls.Add(flowColors);

            btnResetOverride = new Button();
            btnResetOverride.Text = "还原默认";
            btnResetOverride.Font = new Font("SimSun", 9F, FontStyle.Regular);
            btnResetOverride.Location = new Point(302, 16);
            btnResetOverride.Size = new Size(86, 36);
            btnResetOverride.BackColor = System.Drawing.Color.FromArgb(50, 55, 68);
            btnResetOverride.ForeColor = System.Drawing.Color.FromArgb(245, 180, 90);
            btnResetOverride.FlatStyle = FlatStyle.Flat;
            btnResetOverride.FlatAppearance.BorderSize = 0;
            btnResetOverride.Cursor = Cursors.Hand;
            btnResetOverride.Click += BtnResetOverride_Click;
            pnlQuickGrayBar.Controls.Add(btnResetOverride);

            this.Controls.Add(pnlQuickGrayBar);

            // 6. 新增【快速模式】：连续框选批量置灰神器
            btnQuickBatchMode = new Button();
            btnQuickBatchMode.Text = "⚡ 快速置灰模式 (支持连续大面积框选/点选秒置灰)";
            btnQuickBatchMode.Font = new Font("SimSun", 9F, FontStyle.Bold);
            btnQuickBatchMode.Location = new Point(16, 256);
            btnQuickBatchMode.Size = new Size(398, 36);
            btnQuickBatchMode.BackColor = System.Drawing.Color.FromArgb(45, 75, 45); // 深绿科技底色
            btnQuickBatchMode.ForeColor = System.Drawing.Color.FromArgb(120, 255, 150);
            btnQuickBatchMode.FlatStyle = FlatStyle.Flat;
            btnQuickBatchMode.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 180, 100);
            btnQuickBatchMode.FlatAppearance.BorderSize = 1;
            btnQuickBatchMode.Cursor = Cursors.Hand;
            btnQuickBatchMode.Click += BtnQuickBatchMode_Click;
            this.Controls.Add(btnQuickBatchMode);
        }

        private void OnHeaderMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void ToggleMiniMode()
        {
            _isMiniMode = !_isMiniMode;
            if (_isMiniMode)
            {
                this.Size = _miniSize;
                lblMin.Text = "□";
                foreach (Control c in this.Controls)
                {
                    if (c != pnlHeader) c.Visible = false;
                }
            }
            else
            {
                this.Size = _normalSize;
                lblMin.Text = "—";
                foreach (Control c in this.Controls)
                {
                    c.Visible = true;
                }
            }
        }

        private void AddQuickColorBtn(FlowLayoutPanel panel, string name, short colorIndex, System.Drawing.Color visualColor)
        {
            Button btn = new Button();
            btn.Text = name;
            btn.Size = new Size(66, 26);
            btn.Margin = new Padding(2, 0, 4, 0);
            btn.BackColor = System.Drawing.Color.FromArgb(40, 45, 55);
            btn.ForeColor = visualColor;
            btn.Font = new Font("SimSun", 8F);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = visualColor;
            btn.FlatAppearance.BorderSize = 1;
            btn.Cursor = Cursors.Hand;
            btn.Click += (s, e) =>
            {
                ApplyViewportColor(Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, colorIndex));
            };
            panel.Controls.Add(btn);
        }

        /// <summary>
        /// 检查当前是否在布局中且视口已激活
        /// </summary>
        private bool CheckAndRefreshViewport(bool showAlert)
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return false;

                Database db = doc.Database;
                Editor ed = doc.Editor;

                short tilemode = (short)Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("TILEMODE");
                if (tilemode == 1)
                {
                    lblHeaderTitle.Text = "👁️ 视口图层覆盖 [提示: 请切换到布局视口]";
                    lblHeaderTitle.ForeColor = System.Drawing.Color.FromArgb(255, 120, 120);
                    _currentVpId = ObjectId.Null;
                    if (showAlert)
                    {
                        MessageBox.Show(this, "当前处于【模型空间】。\n本功能专用于【布局视口】中的局部图层覆盖，请切换到布局并双击进入视口后使用！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return false;
                }

                short cvport = (short)Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("CVPORT");
                if (cvport <= 1)
                {
                    lblHeaderTitle.Text = "👁️ 视口图层覆盖 [提示: 请双击激活视口]";
                    lblHeaderTitle.ForeColor = System.Drawing.Color.FromArgb(255, 160, 50);
                    _currentVpId = ObjectId.Null;
                    if (showAlert)
                    {
                        MessageBox.Show(this, "当前处于布局图纸空间，尚未激活视口！\n请【双击进入视口】激活它，再进行视口样式修改。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return false;
                }

                ObjectId vpId = ViewportLayerManager.GetActiveViewportId(db, ed);
                _currentVpId = vpId;
                lblHeaderTitle.Text = "👁️ 视口图层样式覆盖 (当前视口已就绪)";
                lblHeaderTitle.ForeColor = System.Drawing.Color.FromArgb(255, 215, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void BtnPickLayer_Click(object sender, EventArgs e)
        {
            if (!CheckAndRefreshViewport(true)) return;

            this.Hide();

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) { this.Show(); return; }

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                PromptNestedEntityOptions opt = new PromptNestedEntityOptions("\n[XY工具箱] 请在视口中点选图元 (支持穿透图块提取图层): ");
                PromptNestedEntityResult res = ed.GetNestedEntity(opt);

                if (res.Status == PromptStatus.OK)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        Entity ent = tr.GetObject(res.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent != null)
                        {
                            _currentLayer = ent.Layer;
                            UpdateLayerDisplayInfo(db, tr, _currentLayer);
                            ed.WriteMessage(string.Format("\n[XY工具箱] 已成功锁定图层: 【{0}】", _currentLayer));
                        }
                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n拾取图层失败: " + ex.Message);
            }
            finally
            {
                this.Show();
                this.BringToFront();
            }
        }

        private void UpdateLayerDisplayInfo(Database db, Transaction tr, string layerName)
        {
            try
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (lt.Has(layerName))
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                    lblLayerName.Text = "当前图层: " + layerName;

                    bool hasColorOver = false;
                    Autodesk.AutoCAD.Colors.Color clr = ltr.Color;
                    bool hasLtOver = false;
                    ObjectId ltId = ltr.LinetypeObjectId;

                    if (!_currentVpId.IsNull && _currentVpId.IsValid)
                    {
                        LayerViewportProperties vpProps = ltr.GetViewportOverrides(_currentVpId);
                        if (vpProps != null)
                        {
                            hasColorOver = vpProps.IsColorOverridden;
                            if (hasColorOver) clr = vpProps.Color;

                            hasLtOver = vpProps.IsLinetypeOverridden;
                            if (hasLtOver) ltId = vpProps.LinetypeObjectId;
                        }
                    }

                    pnlColorPreview.BackColor = clr.ColorValue;
                    lblStatusColor.Text = string.Format("颜色: {0} ({1})", clr.ColorIndex, hasColorOver ? "已覆盖" : "默认");
                    lblStatusColor.ForeColor = hasColorOver ? System.Drawing.Color.FromArgb(255, 215, 0) : System.Drawing.Color.FromArgb(210, 215, 225);

                    string ltName = "Continuous";
                    try
                    {
                        LinetypeTableRecord ltrLt = (LinetypeTableRecord)tr.GetObject(ltId, OpenMode.ForRead);
                        ltName = ltrLt.Name;
                    }
                    catch { }
                    lblStatusLinetype.Text = string.Format("线型: {0} ({1})", ltName, hasLtOver ? "已覆盖" : "默认");
                    lblStatusLinetype.ForeColor = hasLtOver ? System.Drawing.Color.FromArgb(255, 215, 0) : System.Drawing.Color.FromArgb(210, 215, 225);
                }
            }
            catch { }
        }

        private void BtnSetColor_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentLayer))
            {
                MessageBox.Show(this, "请先点击【拾取图层】选择图元！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!CheckAndRefreshViewport(true)) return;

            try
            {
                Autodesk.AutoCAD.Windows.ColorDialog dlg = new Autodesk.AutoCAD.Windows.ColorDialog();
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    ApplyViewportColor(dlg.Color);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, "选色异常: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyViewportColor(Autodesk.AutoCAD.Colors.Color newColor)
        {
            if (string.IsNullOrEmpty(_currentLayer))
            {
                MessageBox.Show(this, "请先点击【拾取图层】选择图元！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!CheckAndRefreshViewport(true)) return;

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;

            try
            {
                using (DocumentLock dl = doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (lt.Has(_currentLayer))
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[_currentLayer], OpenMode.ForWrite);
                        LayerViewportProperties vpProps = ltr.GetViewportOverrides(_currentVpId);
                        if (vpProps != null)
                        {
                            vpProps.Color = newColor;
                        }
                        UpdateLayerDisplayInfo(db, tr, _currentLayer);
                    }
                    tr.Commit();
                }
                doc.Editor.Regen();
                doc.Editor.WriteMessage(string.Format("\n[XY工具箱] 图层【{0}】在当前视口颜色已变更为: {1}", _currentLayer, newColor.ColorIndex));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, "设置视口颜色失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSetLinetype_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentLayer))
            {
                MessageBox.Show(this, "请先点击【拾取图层】选择图元！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!CheckAndRefreshViewport(true)) return;

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = System.Drawing.Color.FromArgb(28, 32, 42);
            menu.ForeColor = System.Drawing.Color.White;
            menu.Font = new Font("SimSun", 9F);

            ViewportLayerManager.EnsureStandardLinetypesLoaded(db);

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                foreach (ObjectId ltId in ltt)
                {
                    LinetypeTableRecord ltRec = (LinetypeTableRecord)tr.GetObject(ltId, OpenMode.ForRead);
                    string ltName = ltRec.Name;
                    ToolStripMenuItem item = new ToolStripMenuItem(ltName);
                    ObjectId targetLtId = ltId;
                    item.Click += (s, ev) =>
                    {
                        ApplyViewportLinetype(targetLtId, ltName);
                    };
                    menu.Items.Add(item);
                }
                tr.Commit();
            }

            menu.Show(btnSetLinetype, new Point(0, btnSetLinetype.Height));
        }

        private void ApplyViewportLinetype(ObjectId newLtId, string ltName)
        {
            if (string.IsNullOrEmpty(_currentLayer)) return;
            if (!CheckAndRefreshViewport(true)) return;

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;

            try
            {
                using (DocumentLock dl = doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (lt.Has(_currentLayer))
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[_currentLayer], OpenMode.ForWrite);
                        LayerViewportProperties vpProps = ltr.GetViewportOverrides(_currentVpId);
                        if (vpProps != null)
                        {
                            vpProps.LinetypeObjectId = newLtId;
                        }
                        UpdateLayerDisplayInfo(db, tr, _currentLayer);
                    }
                    tr.Commit();
                }
                doc.Editor.Regen();
                doc.Editor.WriteMessage(string.Format("\n[XY工具箱] 图层【{0}】在当前视口线型已设置为: {1}", _currentLayer, ltName));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, "设置视口线型失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnResetOverride_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentLayer))
            {
                MessageBox.Show(this, "请先点击【拾取图层】选择图元！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!CheckAndRefreshViewport(true)) return;

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;

            try
            {
                using (DocumentLock dl = doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (lt.Has(_currentLayer))
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[_currentLayer], OpenMode.ForWrite);
                        LayerViewportProperties vpProps = ltr.GetViewportOverrides(_currentVpId);
                        if (vpProps != null)
                        {
                            vpProps.RemoveOverrides();
                        }
                        UpdateLayerDisplayInfo(db, tr, _currentLayer);
                    }
                    tr.Commit();
                }
                doc.Editor.Regen();
                doc.Editor.WriteMessage(string.Format("\n[XY工具箱] 已清除图层【{0}】在当前视口的覆盖，还原为模型全局默认值！", _currentLayer));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, "还原覆盖失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// ⚡ 快速模式：连续大面积框选或点选图元，批量自动置灰所属图层
        /// </summary>
        private void BtnQuickBatchMode_Click(object sender, EventArgs e)
        {
            if (!CheckAndRefreshViewport(true)) return;

            this.Hide();

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) { this.Show(); return; }

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                PromptSelectionOptions opt = new PromptSelectionOptions();
                opt.MessageForAdding = "\n[XY工具箱] 【快速置灰模式】请在当前视口中框选或连续点选需要置灰的图元 (回车完成): ";
                
                PromptSelectionResult selRes = ed.GetSelection(opt);
                if (selRes.Status == PromptStatus.OK && selRes.Value != null && selRes.Value.Count > 0)
                {
                    HashSet<string> affectedLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject so in selRes.Value)
                        {
                            if (so == null) continue;
                            Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            affectedLayers.Add(ent.Layer);

                            // 如果是图块引用，深度穿透提取母块内部的子图层
                            BlockReference blk = ent as BlockReference;
                            if (blk != null)
                            {
                                ObjectId btrId = blk.IsDynamicBlock ? blk.DynamicBlockTableRecord : blk.BlockTableRecord;
                                BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                                if (btr != null)
                                {
                                    foreach (ObjectId subId in btr)
                                    {
                                        Entity subEnt = tr.GetObject(subId, OpenMode.ForRead) as Entity;
                                        if (subEnt != null && !string.IsNullOrEmpty(subEnt.Layer))
                                        {
                                            affectedLayers.Add(subEnt.Layer);
                                        }
                                    }
                                }
                            }
                        }

                        // 将所有提取到的图层在当前视口设为 8 号暗灰 (施工图标准灰)
                        Autodesk.AutoCAD.Colors.Color grayColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 8);
                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                        int changedCount = 0;
                        foreach (string lay in affectedLayers)
                        {
                            if (lt.Has(lay))
                            {
                                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[lay], OpenMode.ForWrite);
                                LayerViewportProperties vpProps = ltr.GetViewportOverrides(_currentVpId);
                                if (vpProps != null)
                                {
                                    vpProps.Color = grayColor;
                                    changedCount++;
                                }
                            }
                        }
                        tr.Commit();

                        ed.Regen();
                        ed.WriteMessage(string.Format("\n★ [XY工具箱] 快速置灰完成！已成功将 {0} 个图层在当前视口设为 8号灰！", changedCount));
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n快速置灰执行异常: " + ex.Message);
            }
            finally
            {
                this.Show();
                this.BringToFront();
            }
        }
    }

    /// <summary>
    /// 视口底层支持与命令入口
    /// </summary>
    public static class ViewportLayerManager
    {
        public static ObjectId GetActiveViewportId(Database db, Editor ed)
        {
            try
            {
                ObjectId vpId = ed.CurrentViewportObjectId;
                if (!vpId.IsNull && vpId.IsValid) return vpId;

                short cvport = (short)Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("CVPORT");
                if (cvport <= 1) return ObjectId.Null;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord paperSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.PaperSpace], OpenMode.ForRead);
                    foreach (ObjectId id in paperSpace)
                    {
                        if (id.ObjectClass.DxfName.Equals("VIEWPORT", StringComparison.OrdinalIgnoreCase))
                        {
                            Viewport vp = tr.GetObject(id, OpenMode.ForRead) as Viewport;
                            if (vp != null && vp.Number == cvport)
                            {
                                return vp.ObjectId;
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch { }
            return ObjectId.Null;
        }

        public static void EnsureStandardLinetypesLoaded(Database db)
        {
            string[] standardLts = new string[] { "DASHED", "HIDDEN", "CENTER", "PHANTOM", "DOT" };
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                foreach (string lt in standardLts)
                {
                    if (!ltt.Has(lt))
                    {
                        try
                        {
                            db.LoadLineTypeFile(lt, "acad.lin");
                        }
                        catch { }
                    }
                }
                tr.Commit();
            }
        }
    }

    /// <summary>
    /// 注册快捷键命令
    /// </summary>
    public class ViewportCommands
    {
        [CommandMethod("SK")]
        [CommandMethod("SKYS")]
        [CommandMethod("SKTC")]
        [CommandMethod("XY_VPOVERRIDE")]
        public static void CmdViewportLayerOverride()
        {
            ViewportLayerForm.ShowFloatingPalette();
        }
    }
}
