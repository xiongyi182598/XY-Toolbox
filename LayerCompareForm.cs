using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Font = System.Drawing.Font;

namespace XY_Deepening_Toolbox
{
    public class LayerCompareForm : Form
    {
        // 核心数据模型
        private List<LayerInfo> _currentLayers = new List<LayerInfo>();
        private List<LayerInfo> _refLayers = new List<LayerInfo>();
        private List<LayerMatchResult> _allMatches = new List<LayerMatchResult>();
        private List<LayerMatchResult> _displayMatches = new List<LayerMatchResult>();

        private string _refDwgPath = "";
        private string _linFilePath = "";

        // UI 控件
        private Panel pnlHeader;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;

        private Panel pnlToolbar;
        private Button btnPurgeUnused;
        private RadioButton rbShowAll;
        private RadioButton rbShowActiveOnly;
        private TextBox txtSearch;
        private Button btnBrowseRefDwg;
        private Button btnBrowseLin;
        private Label lblRefFileStatus;

        private DataGridView dgvCompare;
        private DataGridViewCheckBoxColumn colCheck;
        private DataGridViewTextBoxColumn colIndex;
        private DataGridViewTextBoxColumn colCurName;
        private DataGridViewTextBoxColumn colEntityCount;
        private DataGridViewTextBoxColumn colCurColor;
        private DataGridViewTextBoxColumn colCurLt;
        private DataGridViewTextBoxColumn colCurLw;
        private DataGridViewTextBoxColumn colMatchStatus;
        private DataGridViewTextBoxColumn colTarName;
        private DataGridViewTextBoxColumn colTarColor;
        private DataGridViewTextBoxColumn colTarLt;

        private Panel pnlBottom;
        private CheckBox chkMigrateVp;
        private CheckBox chkDeepBlock;
        private CheckBox chkPurgeSource;
        private CheckBox chkResetByLayer;
        private Label lblStatus;
        private ProgressBar progressBar;
        private Button btnNormalizeProps;
        private Button btnExecuteMerge;
        private Button btnExportReport;
        private Button btnClose;

        public LayerCompareForm()
        {
            InitializeComponent();
            ApplyCustomStyles();
            RefreshCurrentDrawingLayers();
        }

        private void InitializeComponent()
        {
            this.Text = "图层与线型规范深度对比与智能合并系统 - XY工具箱";
            this.Size = new Size(1160, 720);
            this.MinimumSize = new Size(980, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 34, 42);
            this.ForeColor = Color.FromArgb(220, 225, 232);
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // ==========================================
            // 1. 顶部 Header
            // ==========================================
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 54;
            pnlHeader.BackColor = Color.FromArgb(24, 27, 34);
            pnlHeader.Padding = new Padding(16, 8, 16, 8);

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "🔬 图层与线型规范深度对比与智能合并系统";
            lblHeaderTitle.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            lblHeaderTitle.ForeColor = Color.White;
            lblHeaderTitle.Location = new Point(14, 6);
            lblHeaderTitle.Size = new Size(500, 24);
            pnlHeader.Controls.Add(lblHeaderTitle);

            lblHeaderSubtitle = new Label();
            lblHeaderSubtitle.Text = "支持外部参考标准DWG比对 · 语义智能匹配 · 视口穿透无损合并 · 未使用图层物理安全瘦身";
            lblHeaderSubtitle.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblHeaderSubtitle.ForeColor = Color.FromArgb(145, 155, 170);
            lblHeaderSubtitle.Location = new Point(16, 31);
            lblHeaderSubtitle.Size = new Size(700, 18);
            pnlHeader.Controls.Add(lblHeaderSubtitle);

            this.Controls.Add(pnlHeader);

            // ==========================================
            // 2. 核心工具栏 (维度 1 物理清理 + 维度 2 动态过滤)
            // ==========================================
            pnlToolbar = new Panel();
            pnlToolbar.Dock = DockStyle.Top;
            pnlToolbar.Height = 46;
            pnlToolbar.BackColor = Color.FromArgb(34, 39, 49);
            pnlToolbar.Padding = new Padding(12, 6, 12, 6);

            // 【维度 1: 物理一键清理】
            btnPurgeUnused = new Button();
            btnPurgeUnused.Text = "🧹 一键移除未使用空图层";
            btnPurgeUnused.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnPurgeUnused.BackColor = Color.FromArgb(35, 134, 54); // 经典 GitHub/VSCode 绿色高亮
            btnPurgeUnused.ForeColor = Color.White;
            btnPurgeUnused.FlatStyle = FlatStyle.Flat;
            btnPurgeUnused.FlatAppearance.BorderSize = 0;
            btnPurgeUnused.Location = new Point(12, 8);
            btnPurgeUnused.Size = new Size(185, 30);
            btnPurgeUnused.Cursor = Cursors.Hand;
            btnPurgeUnused.Click += BtnPurgeUnused_Click;
            pnlToolbar.Controls.Add(btnPurgeUnused);

            // 【维度 2: 视觉动态过滤】
            rbShowAll = new RadioButton();
            rbShowAll.Text = "显示全部图层";
            rbShowAll.Checked = true;
            rbShowAll.Location = new Point(215, 12);
            rbShowAll.Size = new Size(110, 22);
            rbShowAll.ForeColor = Color.FromArgb(230, 235, 245);
            rbShowAll.CheckedChanged += (s, e) => { if (rbShowAll.Checked) ApplyFilter(); };
            pnlToolbar.Controls.Add(rbShowAll);

            rbShowActiveOnly = new RadioButton();
            rbShowActiveOnly.Text = "仅显示在用图层 (隐藏空图层)";
            rbShowActiveOnly.Location = new Point(330, 12);
            rbShowActiveOnly.Size = new Size(195, 22);
            rbShowActiveOnly.ForeColor = Color.FromArgb(97, 175, 239);
            rbShowActiveOnly.CheckedChanged += (s, e) => { if (rbShowActiveOnly.Checked) ApplyFilter(); };
            pnlToolbar.Controls.Add(rbShowActiveOnly);

            // 搜索过滤框
            Label lblSearch = new Label();
            lblSearch.Text = "🔍";
            lblSearch.Location = new Point(535, 14);
            lblSearch.Size = new Size(24, 20);
            lblSearch.ForeColor = Color.Gray;
            pnlToolbar.Controls.Add(lblSearch);

            txtSearch = new TextBox();
            txtSearch.Location = new Point(560, 11);
            txtSearch.Size = new Size(130, 23);
            txtSearch.BackColor = Color.FromArgb(24, 27, 34);
            txtSearch.ForeColor = Color.White;
            txtSearch.BorderStyle = BorderStyle.FixedSingle;
            txtSearch.TextChanged += (s, e) => ApplyFilter();
            pnlToolbar.Controls.Add(txtSearch);

            // 右侧参考文件载入按钮
            btnBrowseLin = new Button();
            btnBrowseLin.Text = "📄 载入.LIN线型库";
            btnBrowseLin.Location = new Point(pnlToolbar.Width - 145, 8);
            btnBrowseLin.Size = new Size(135, 30);
            btnBrowseLin.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowseLin.BackColor = Color.FromArgb(48, 54, 66);
            btnBrowseLin.ForeColor = Color.FromArgb(220, 225, 235);
            btnBrowseLin.FlatStyle = FlatStyle.Flat;
            btnBrowseLin.FlatAppearance.BorderSize = 0;
            btnBrowseLin.Cursor = Cursors.Hand;
            btnBrowseLin.Click += BtnBrowseLin_Click;
            pnlToolbar.Controls.Add(btnBrowseLin);

            btnBrowseRefDwg = new Button();
            btnBrowseRefDwg.Text = "📁 选择参考标准DWG";
            btnBrowseRefDwg.Location = new Point(pnlToolbar.Width - 315, 8);
            btnBrowseRefDwg.Size = new Size(160, 30);
            btnBrowseRefDwg.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowseRefDwg.BackColor = Color.FromArgb(14, 99, 156);
            btnBrowseRefDwg.ForeColor = Color.White;
            btnBrowseRefDwg.FlatStyle = FlatStyle.Flat;
            btnBrowseRefDwg.FlatAppearance.BorderSize = 0;
            btnBrowseRefDwg.Cursor = Cursors.Hand;
            btnBrowseRefDwg.Click += BtnBrowseRefDwg_Click;
            pnlToolbar.Controls.Add(btnBrowseRefDwg);

            lblRefFileStatus = new Label();
            lblRefFileStatus.Text = "未载入参考DWG (点击右侧按钮选择标准样板)";
            lblRefFileStatus.ForeColor = Color.FromArgb(150, 160, 175);
            lblRefFileStatus.Location = new Point(pnlToolbar.Width - 640, 14);
            lblRefFileStatus.Size = new Size(310, 20);
            lblRefFileStatus.TextAlign = ContentAlignment.MiddleRight;
            lblRefFileStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlToolbar.Controls.Add(lblRefFileStatus);

            this.Controls.Add(pnlToolbar);

            // ==========================================
            // 3. 底部配置与操作栏
            // ==========================================
            pnlBottom = new Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 85;
            pnlBottom.BackColor = Color.FromArgb(24, 27, 34);
            pnlBottom.Padding = new Padding(14, 8, 14, 8);

            // 左侧选项
            chkMigrateVp = new CheckBox();
            chkMigrateVp.Text = "视口全覆盖穿透迁移 (Viewport Overrides)";
            chkMigrateVp.Checked = true;
            chkMigrateVp.Location = new Point(14, 8);
            chkMigrateVp.Size = new Size(270, 22);
            chkMigrateVp.ForeColor = Color.FromArgb(210, 215, 225);
            pnlBottom.Controls.Add(chkMigrateVp);

            chkDeepBlock = new CheckBox();
            chkDeepBlock.Text = "穿透所有图块与深层嵌套块定义 (Deep Traversal)";
            chkDeepBlock.Checked = true;
            chkDeepBlock.Location = new Point(290, 8);
            chkDeepBlock.Size = new Size(310, 22);
            chkDeepBlock.ForeColor = Color.FromArgb(210, 215, 225);
            pnlBottom.Controls.Add(chkDeepBlock);

            chkPurgeSource = new CheckBox();
            chkPurgeSource.Text = "合并后物理移除源图层 (Auto Purge)";
            chkPurgeSource.Checked = true;
            chkPurgeSource.Location = new Point(610, 8);
            chkPurgeSource.Size = new Size(230, 22);
            chkPurgeSource.ForeColor = Color.FromArgb(210, 215, 225);
            pnlBottom.Controls.Add(chkPurgeSource);

            chkResetByLayer = new CheckBox();
            chkResetByLayer.Text = "重置实体属性为随层 (ByLayer)";
            chkResetByLayer.Checked = false;
            chkResetByLayer.Location = new Point(850, 8);
            chkResetByLayer.Size = new Size(220, 22);
            chkResetByLayer.ForeColor = Color.FromArgb(210, 215, 225);
            pnlBottom.Controls.Add(chkResetByLayer);

            // 状态栏
            lblStatus = new Label();
            lblStatus.Text = "就绪";
            lblStatus.Location = new Point(14, 45);
            lblStatus.Size = new Size(540, 24);
            lblStatus.ForeColor = Color.FromArgb(170, 180, 195);
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            pnlBottom.Controls.Add(lblStatus);

            progressBar = new ProgressBar();
            progressBar.Location = new Point(560, 48);
            progressBar.Size = new Size(110, 18);
            progressBar.Visible = false;
            pnlBottom.Controls.Add(progressBar);

            // 右侧操作按钮
            btnClose = new Button();
            btnClose.Text = "关闭";
            btnClose.Location = new Point(pnlBottom.Width - 95, 42);
            btnClose.Size = new Size(80, 32);
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.BackColor = Color.FromArgb(48, 54, 66);
            btnClose.ForeColor = Color.White;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();
            pnlBottom.Controls.Add(btnClose);

            btnExportReport = new Button();
            btnExportReport.Text = "📊 导出报告";
            btnExportReport.Location = new Point(pnlBottom.Width - 215, 42);
            btnExportReport.Size = new Size(110, 32);
            btnExportReport.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnExportReport.BackColor = Color.FromArgb(48, 54, 66);
            btnExportReport.ForeColor = Color.FromArgb(220, 230, 245);
            btnExportReport.FlatStyle = FlatStyle.Flat;
            btnExportReport.FlatAppearance.BorderSize = 0;
            btnExportReport.Click += BtnExportReport_Click;
            pnlBottom.Controls.Add(btnExportReport);

            btnNormalizeProps = new Button();
            btnNormalizeProps.Text = "仅规范化属性 (颜色/线型)";
            btnNormalizeProps.Location = new Point(pnlBottom.Width - 410, 42);
            btnNormalizeProps.Size = new Size(185, 32);
            btnNormalizeProps.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnNormalizeProps.BackColor = Color.FromArgb(40, 75, 110);
            btnNormalizeProps.ForeColor = Color.White;
            btnNormalizeProps.FlatStyle = FlatStyle.Flat;
            btnNormalizeProps.FlatAppearance.BorderSize = 0;
            btnNormalizeProps.Click += BtnNormalizeProps_Click;
            pnlBottom.Controls.Add(btnNormalizeProps);

            btnExecuteMerge = new Button();
            btnExecuteMerge.Text = "⚡ 一键全自动智能合并";
            btnExecuteMerge.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            btnExecuteMerge.Location = new Point(pnlBottom.Width - 620, 42);
            btnExecuteMerge.Size = new Size(200, 32);
            btnExecuteMerge.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnExecuteMerge.BackColor = Color.FromArgb(14, 99, 156);
            btnExecuteMerge.ForeColor = Color.White;
            btnExecuteMerge.FlatStyle = FlatStyle.Flat;
            btnExecuteMerge.FlatAppearance.BorderSize = 0;
            btnExecuteMerge.Cursor = Cursors.Hand;
            btnExecuteMerge.Click += BtnExecuteMerge_Click;
            pnlBottom.Controls.Add(btnExecuteMerge);

            this.Controls.Add(pnlBottom);

            // ==========================================
            // 4. 中间主比对表格 DataGridView
            // ==========================================
            dgvCompare = new DataGridView();
            dgvCompare.Dock = DockStyle.Fill;
            dgvCompare.AllowUserToAddRows = false;
            dgvCompare.AllowUserToDeleteRows = false;
            dgvCompare.AllowUserToResizeRows = false;
            dgvCompare.RowHeadersVisible = false;
            dgvCompare.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvCompare.BackgroundColor = Color.FromArgb(28, 32, 40);
            dgvCompare.BorderStyle = BorderStyle.None;
            dgvCompare.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvCompare.GridColor = Color.FromArgb(42, 47, 58);
            dgvCompare.RowTemplate.Height = 28;

            colCheck = new DataGridViewCheckBoxColumn();
            colCheck.HeaderText = "执行";
            colCheck.Width = 45;
            dgvCompare.Columns.Add(colCheck);

            colIndex = new DataGridViewTextBoxColumn();
            colIndex.HeaderText = "#";
            colIndex.Width = 40;
            colIndex.ReadOnly = true;
            dgvCompare.Columns.Add(colIndex);

            colCurName = new DataGridViewTextBoxColumn();
            colCurName.HeaderText = "当前图层名";
            colCurName.Width = 160;
            colCurName.ReadOnly = true;
            dgvCompare.Columns.Add(colCurName);

            colEntityCount = new DataGridViewTextBoxColumn();
            colEntityCount.HeaderText = "图元数";
            colEntityCount.Width = 70;
            colEntityCount.ReadOnly = true;
            dgvCompare.Columns.Add(colEntityCount);

            colCurColor = new DataGridViewTextBoxColumn();
            colCurColor.HeaderText = "当前颜色";
            colCurColor.Width = 95;
            colCurColor.ReadOnly = true;
            dgvCompare.Columns.Add(colCurColor);

            colCurLt = new DataGridViewTextBoxColumn();
            colCurLt.HeaderText = "当前线型";
            colCurLt.Width = 95;
            colCurLt.ReadOnly = true;
            dgvCompare.Columns.Add(colCurLt);

            colCurLw = new DataGridViewTextBoxColumn();
            colCurLw.HeaderText = "线宽";
            colCurLw.Width = 65;
            colCurLw.ReadOnly = true;
            dgvCompare.Columns.Add(colCurLw);

            colMatchStatus = new DataGridViewTextBoxColumn();
            colMatchStatus.HeaderText = "匹配置信度与状态";
            colMatchStatus.Width = 175;
            colMatchStatus.ReadOnly = true;
            dgvCompare.Columns.Add(colMatchStatus);

            colTarName = new DataGridViewTextBoxColumn();
            colTarName.HeaderText = "推荐目标标准图层 (可直接输入/自选)";
            colTarName.Width = 200;
            dgvCompare.Columns.Add(colTarName);

            colTarColor = new DataGridViewTextBoxColumn();
            colTarColor.HeaderText = "标准颜色";
            colTarColor.Width = 95;
            colTarColor.ReadOnly = true;
            dgvCompare.Columns.Add(colTarColor);

            colTarLt = new DataGridViewTextBoxColumn();
            colTarLt.HeaderText = "标准线型";
            colTarLt.Width = 95;
            colTarLt.ReadOnly = true;
            dgvCompare.Columns.Add(colTarLt);

            dgvCompare.DataError += delegate(object s, DataGridViewDataErrorEventArgs e) {
                e.ThrowException = false;
                e.Cancel = false;
            };
            dgvCompare.CellPainting += DgvCompare_CellPainting;
            dgvCompare.CellValueChanged += DgvCompare_CellValueChanged;
            dgvCompare.CurrentCellDirtyStateChanged += (s, e) => { if (dgvCompare.IsCurrentCellDirty) dgvCompare.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            this.Controls.Add(dgvCompare);
            dgvCompare.BringToFront();
        }

        private void ApplyCustomStyles()
        {
            // 启用双缓冲消除表格闪烁
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, dgvCompare, new object[] { true });

            dgvCompare.EnableHeadersVisualStyles = false;
            dgvCompare.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(36, 42, 52);
            dgvCompare.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(170, 185, 205);
            dgvCompare.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            dgvCompare.ColumnHeadersHeight = 32;

            dgvCompare.DefaultCellStyle.BackColor = Color.FromArgb(28, 32, 40);
            dgvCompare.DefaultCellStyle.ForeColor = Color.FromArgb(220, 225, 235);
            dgvCompare.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 55, 72);
            dgvCompare.DefaultCellStyle.SelectionForeColor = Color.FromArgb(97, 175, 239);
        }

        public void RefreshCurrentDrawingLayers()
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                Database db = doc.Database;

                _currentLayers = LayerCompareEngine.ExtractCurrentLayers(db);

                int total = _currentLayers.Count;
                int active = _currentLayers.FindAll(x => !x.IsEmpty).Count;
                int empty = total - active;

                rbShowAll.Text = string.Format("显示全部图层 ({0})", total);
                rbShowActiveOnly.Text = string.Format("仅显示在用图层 (隐藏 {0} 个空层)", empty);

                // 重新比对
                _allMatches = LayerCompareEngine.ComputeMatches(_currentLayers, _refLayers);
                ApplyFilter();

                lblStatus.Text = string.Format("当前图纸共 {0} 个图层（在用 {1} 个，空图层 {2} 个） | 目标参考图层库: {3} 个",
                    total, active, empty, _refLayers.Count);
            }
            catch (System.Exception ex)
            {
                lblStatus.Text = "读取当前图层失败: " + ex.Message;
            }
        }

        private void ApplyFilter()
        {
            string keyword = txtSearch.Text.Trim().ToLower();
            bool activeOnly = rbShowActiveOnly.Checked;

            _displayMatches = _allMatches.FindAll(x =>
            {
                if (activeOnly && x.CurrentLayer.IsEmpty) return false;
                if (!string.IsNullOrEmpty(keyword))
                {
                    bool mName = x.CurrentLayer.Name.ToLower().Contains(keyword);
                    bool mTar = !string.IsNullOrEmpty(x.TargetLayerName) && x.TargetLayerName.ToLower().Contains(keyword);
                    if (!mName && !mTar) return false;
                }
                return true;
            });

            PopulateGrid();
        }

        private void PopulateGrid()
        {
            dgvCompare.Rows.Clear();

            int idx = 1;
            foreach (LayerMatchResult item in _displayMatches)
            {
                string countStr = item.CurrentLayer.IsEmpty ? "0 [空]" : item.CurrentLayer.EntityCount.ToString();
                string statusText = "";
                if (item.Level == MatchConfidenceLevel.ExactMatch)
                {
                    statusText = "★ 100% 完全精确一致";
                }
                else if (item.Level == MatchConfidenceLevel.High)
                {
                    statusText = string.Format("✔ {0}% 推荐规范 ({1})", item.MatchScore, item.MatchReason);
                }
                else if (item.Level == MatchConfidenceLevel.Medium)
                {
                    statusText = string.Format("○ {0}% 建议匹配", item.MatchScore);
                }
                else
                {
                    statusText = "— 无合适建议";
                }

                string tarColor = item.BestMatchRefLayer != null ? item.BestMatchRefLayer.ColorDescription : "—";
                string tarLt = item.BestMatchRefLayer != null ? item.BestMatchRefLayer.LinetypeName : "—";

                string targetVal = !string.IsNullOrEmpty(item.TargetLayerName) ? item.TargetLayerName : "— 保持原图层 —";

                int rowIndex = dgvCompare.Rows.Add(
                    item.IsSelectedForMerge,
                    idx++,
                    item.CurrentLayer.Name,
                    countStr,
                    item.CurrentLayer.ColorDescription,
                    item.CurrentLayer.LinetypeName,
                    item.CurrentLayer.LineWeightName,
                    statusText,
                    targetVal,
                    tarColor,
                    tarLt
                );

                dgvCompare.Rows[rowIndex].Tag = item;

                // 若为空图层，整行字体设为浅灰，直观一目了然
                if (item.CurrentLayer.IsEmpty)
                {
                    dgvCompare.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.FromArgb(130, 140, 155);
                }
            }
        }

        private void DgvCompare_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvCompare.Rows.Count) return;

            // 在“当前颜色”和“标准颜色”单元格左侧绘制鲜艳的色块 swatch
            if (e.ColumnIndex == colCurColor.Index || e.ColumnIndex == colTarColor.Index)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                LayerMatchResult item = dgvCompare.Rows[e.RowIndex].Tag as LayerMatchResult;
                if (item != null)
                {
                    Color swatchColor = Color.Transparent;
                    if (e.ColumnIndex == colCurColor.Index) swatchColor = item.CurrentLayer.ColorValue;
                    else if (e.ColumnIndex == colTarColor.Index && item.BestMatchRefLayer != null) swatchColor = item.BestMatchRefLayer.ColorValue;

                    if (swatchColor != Color.Transparent)
                    {
                        Rectangle rect = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + (e.CellBounds.Height - 12) / 2, 12, 12);
                        using (SolidBrush b = new SolidBrush(swatchColor))
                        {
                            e.Graphics.FillRectangle(b, rect);
                        }
                        using (Pen p = new Pen(Color.FromArgb(100, 100, 100)))
                        {
                            e.Graphics.DrawRectangle(p, rect);
                        }

                        // 绘制文字
                        string text = e.Value != null ? e.Value.ToString() : "";
                        Rectangle textRect = new Rectangle(e.CellBounds.X + 24, e.CellBounds.Y, e.CellBounds.Width - 24, e.CellBounds.Height);
                        TextRenderer.DrawText(e.Graphics, text, e.CellStyle.Font, textRect, e.CellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                        e.Handled = true;
                    }
                }
            }
        }

        private void DgvCompare_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvCompare.Rows.Count) return;

            LayerMatchResult item = dgvCompare.Rows[e.RowIndex].Tag as LayerMatchResult;
            if (item == null) return;

            if (e.ColumnIndex == colCheck.Index)
            {
                item.IsSelectedForMerge = Convert.ToBoolean(dgvCompare.Rows[e.RowIndex].Cells[colCheck.Index].Value);
            }
            else if (e.ColumnIndex == colTarName.Index)
            {
                string selectedTarget = dgvCompare.Rows[e.RowIndex].Cells[colTarName.Index].Value as string;
                if (string.IsNullOrEmpty(selectedTarget) || selectedTarget == "— 保持原图层 —")
                {
                    item.TargetLayerName = "";
                    item.BestMatchRefLayer = null;
                    dgvCompare.Rows[e.RowIndex].Cells[colTarColor.Index].Value = "—";
                    dgvCompare.Rows[e.RowIndex].Cells[colTarLt.Index].Value = "—";
                }
                else
                {
                    item.TargetLayerName = selectedTarget;
                    LayerInfo refInfo = _refLayers.Find(x => x.Name.Equals(selectedTarget, StringComparison.OrdinalIgnoreCase));
                    if (refInfo != null)
                    {
                        item.BestMatchRefLayer = refInfo;
                        dgvCompare.Rows[e.RowIndex].Cells[colTarColor.Index].Value = refInfo.ColorDescription;
                        dgvCompare.Rows[e.RowIndex].Cells[colTarLt.Index].Value = refInfo.LinetypeName;
                    }
                }
            }
        }

        // ==========================================
        // 核心事件：一键物理清理未使用空图层 (维度 1)
        // ==========================================
        private void BtnPurgeUnused_Click(object sender, EventArgs e)
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;

            using (doc.LockDocument())
            {
                PurgeReport report = LayerPurgeService.PurgeUnusedLayers(db);
                
                string msg = string.Format(
                    "【清理报告】\n\n" +
                    "• 深度扫描图层总数: {0} 个\n" +
                    "• 成功物理抹除空图层: {1} 个\n" +
                    "• 受保护图层 (0/Defpoints/当前层): {2} 个\n" +
                    "• 清理耗时: {3} 毫秒\n\n" +
                    "{4}",
                    report.TotalLayersScanned, report.PurgedLayersCount, report.ProtectedLayerNames.Count, report.ElapsedMilliseconds,
                    report.PurgedLayersCount > 0 ? "已全部安全清理完毕！表格已实时刷新。" : "图纸极为整洁，无多余未用图层。");

                MessageBox.Show(this, msg, "图层物理清理完毕", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            // 实时刷新表格数据
            RefreshCurrentDrawingLayers();
        }

        // 选择参考 DWG
        private void BtnBrowseRefDwg_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "AutoCAD 图纸文件 (*.dwg)|*.dwg";
                ofd.Title = "选择标准规范样板 DWG 图纸";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _refDwgPath = ofd.FileName;
                    lblRefFileStatus.Text = "已载入: " + Path.GetFileName(_refDwgPath);
                    _refLayers = LayerCompareEngine.ExtractReferenceLayers(_refDwgPath);
                    _allMatches = LayerCompareEngine.ComputeMatches(_currentLayers, _refLayers);
                    ApplyFilter();
                    lblStatus.Text = string.Format("标准库已载入 {0} 个参考图层，完成智能语义匹配计算！", _refLayers.Count);
                }
            }
        }

        // 载入 LIN 线型库
        private void BtnBrowseLin_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "AutoCAD 线型定义文件 (*.lin)|*.lin|图案填充文件 (*.pat)|*.pat";
                ofd.Title = "选择线型定义文件 (.LIN)";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _linFilePath = ofd.FileName;
                    Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        using (doc.LockDocument())
                        {
                            try
                            {
                                doc.Database.LoadLineTypeFile("*", _linFilePath);
                            }
                            catch { }
                        }
                    }
                    MessageBox.Show(this, "线型文件已成功挂载至当前图纸！\n" + Path.GetFileName(_linFilePath), "线型载入就绪", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        // 仅规范化属性
        private void BtnNormalizeProps_Click(object sender, EventArgs e)
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            MergeOptions opts = new MergeOptions();
            opts.NormalizeAttributesOnly = true;

            using (doc.LockDocument())
            {
                MergeReport r = LayerDeepMergeService.ExecuteDeepMerge(doc.Database, _allMatches, opts);
                MessageBox.Show(this, r.SummaryMessage, "规范化对齐完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                doc.Editor.UpdateScreen();
            }

            RefreshCurrentDrawingLayers();
        }

        // 执行全自动深度合并
        private void BtnExecuteMerge_Click(object sender, EventArgs e)
        {
            List<LayerMatchResult> selected = _allMatches.FindAll(x => x.IsSelectedForMerge && !string.IsNullOrEmpty(x.TargetLayerName) && x.TargetLayerName != x.CurrentLayer.Name);
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "当前没有勾选需要执行跨层合并的图层项！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string confirmMsg = string.Format(
                "即将执行全自动深度穿透合并：\n\n" +
                "• 待合并图层数: {0} 个\n" +
                "• 视口图层覆盖无损迁移: {1}\n" +
                "• 穿透图块与嵌套块定义: {2}\n" +
                "• 合并后物理移除源图层: {3}\n" +
                "• 图元属性重置随层(ByLayer): {4}\n\n" +
                "是否立即开始处理？",
                selected.Count,
                chkMigrateVp.Checked ? "是" : "否",
                chkDeepBlock.Checked ? "是" : "否",
                chkPurgeSource.Checked ? "是" : "否",
                chkResetByLayer.Checked ? "是" : "否");

            if (MessageBox.Show(this, confirmMsg, "确认执行深度合并", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            MergeOptions opts = new MergeOptions();
            opts.MigrateViewportOverrides = chkMigrateVp.Checked;
            opts.DeepBlockTraversal = chkDeepBlock.Checked;
            opts.PurgeSourceLayersAfterMerge = chkPurgeSource.Checked;
            opts.ResetEntitiesByLayer = chkResetByLayer.Checked;
            opts.NormalizeAttributesOnly = false;

            using (doc.LockDocument())
            {
                MergeReport r = LayerDeepMergeService.ExecuteDeepMerge(doc.Database, _allMatches, opts);
                MessageBox.Show(this, r.SummaryMessage, "深度合并执行完毕", MessageBoxButtons.OK, MessageBoxIcon.Information);
                doc.Editor.UpdateScreen();
            }

            RefreshCurrentDrawingLayers();
        }

        // 导出报告
        private void BtnExportReport_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV 报表文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt";
                sfd.FileName = "图层规范对比与合并报表_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("序号,当前图层名,图元数,当前颜色,当前线型,线宽,匹配状态,目标标准图层,标准颜色,标准线型,是否执行合并");
                    int idx = 1;
                    foreach (var item in _allMatches)
                    {
                        sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\"",
                            idx++,
                            item.CurrentLayer.Name,
                            item.CurrentLayer.EntityCount,
                            item.CurrentLayer.ColorDescription,
                            item.CurrentLayer.LinetypeName,
                            item.CurrentLayer.LineWeightName,
                            item.MatchReason,
                            item.TargetLayerName,
                            item.BestMatchRefLayer != null ? item.BestMatchRefLayer.ColorDescription : "",
                            item.BestMatchRefLayer != null ? item.BestMatchRefLayer.LinetypeName : "",
                            item.IsSelectedForMerge ? "是" : "否"));
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show(this, "报表已成功导出至:\n" + sfd.FileName, "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}
