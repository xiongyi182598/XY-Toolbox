using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;
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
        private List<BlockMatchInfo> _currentBlocks = new List<BlockMatchInfo>();
        private List<BlockMatchInfo> _refBlocks = new List<BlockMatchInfo>();
        private List<BlockMatchInfo> _blockMatches = new List<BlockMatchInfo>();

        private List<LayerMatchResult> _allMatches = new List<LayerMatchResult>();
        private List<LayerMatchResult> _displayMatches = new List<LayerMatchResult>();

        private string _refDwgPath = "";
        private string _linFilePath = "";

        // UI 控件
        private Panel pnlHeader;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;

        private Panel pnlToolbar;
        private Button btnLoadLastPreset;
        private Button btnQuickFlipNow;
        private Button btnReverseInfer;
        private Button btnSavePreset;
        private Button btnLoadPreset;

        private Panel pnlFilterBar;
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
        private CheckBox chkRedefineBlocks;
        private CheckBox chkMigrateVp;
        private CheckBox chkDeepBlock;
        private CheckBox chkPurgeSource;
        private CheckBox chkResetByLayer;
        private Label lblStatus;
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
            this.Text = "图层与图块规范深度对比与智能翻图系统 - XY工具箱";
            this.Size = new Size(1180, 740);
            this.MinimumSize = new Size(1000, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 34, 42);
            this.ForeColor = Color.FromArgb(220, 225, 232);
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // ==========================================
            // 1. 顶部 Header
            // ==========================================
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 50;
            pnlHeader.BackColor = Color.FromArgb(24, 27, 34);
            pnlHeader.Padding = new Padding(16, 6, 16, 6);

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "🔬 图层与图块规范深度对比 · 智能批量翻图系统";
            lblHeaderTitle.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            lblHeaderTitle.ForeColor = Color.White;
            lblHeaderTitle.Location = new Point(14, 5);
            lblHeaderTitle.Size = new Size(520, 22);
            pnlHeader.Controls.Add(lblHeaderTitle);

            lblHeaderSubtitle = new Label();
            lblHeaderSubtitle.Text = "HB1/HB2 属性与视口无损继承 · 同名图块最高优先级合并 · 历史记忆一键秒翻 · 双图逆向反推规则";
            lblHeaderSubtitle.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblHeaderSubtitle.ForeColor = Color.FromArgb(145, 155, 170);
            lblHeaderSubtitle.Location = new Point(16, 28);
            lblHeaderSubtitle.Size = new Size(720, 16);
            pnlHeader.Controls.Add(lblHeaderSubtitle);

            this.Controls.Add(pnlHeader);

            // ==========================================
            // 2. 快捷翻图与记忆操作栏 (Toolbar Row 1)
            // ==========================================
            pnlToolbar = new Panel();
            pnlToolbar.Dock = DockStyle.Top;
            pnlToolbar.Height = 42;
            pnlToolbar.BackColor = Color.FromArgb(34, 39, 49);
            pnlToolbar.Padding = new Padding(12, 5, 12, 5);

            btnLoadLastPreset = new Button();
            btnLoadLastPreset.Text = "📋 载入上次翻图记忆";
            btnLoadLastPreset.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnLoadLastPreset.BackColor = Color.FromArgb(50, 60, 78);
            btnLoadLastPreset.ForeColor = Color.FromArgb(120, 215, 255);
            btnLoadLastPreset.FlatStyle = FlatStyle.Flat;
            btnLoadLastPreset.FlatAppearance.BorderColor = Color.FromArgb(70, 130, 180);
            btnLoadLastPreset.Location = new Point(12, 6);
            btnLoadLastPreset.Size = new Size(160, 30);
            btnLoadLastPreset.Cursor = Cursors.Hand;
            btnLoadLastPreset.Click += BtnLoadLastPreset_Click;
            pnlToolbar.Controls.Add(btnLoadLastPreset);

            btnQuickFlipNow = new Button();
            btnQuickFlipNow.Text = "⚡ 一键套用并执行翻图";
            btnQuickFlipNow.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnQuickFlipNow.BackColor = Color.FromArgb(40, 110, 60);
            btnQuickFlipNow.ForeColor = Color.FromArgb(140, 255, 170);
            btnQuickFlipNow.FlatStyle = FlatStyle.Flat;
            btnQuickFlipNow.FlatAppearance.BorderColor = Color.FromArgb(60, 160, 90);
            btnQuickFlipNow.Location = new Point(180, 6);
            btnQuickFlipNow.Size = new Size(165, 30);
            btnQuickFlipNow.Cursor = Cursors.Hand;
            btnQuickFlipNow.Click += BtnQuickFlipNow_Click;
            pnlToolbar.Controls.Add(btnQuickFlipNow);

            btnReverseInfer = new Button();
            btnReverseInfer.Text = "🔍 双图逆向分析 (反推提取规则)";
            btnReverseInfer.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnReverseInfer.BackColor = Color.FromArgb(85, 55, 120);
            btnReverseInfer.ForeColor = Color.FromArgb(235, 195, 255);
            btnReverseInfer.FlatStyle = FlatStyle.Flat;
            btnReverseInfer.FlatAppearance.BorderColor = Color.FromArgb(140, 90, 200);
            btnReverseInfer.Location = new Point(352, 6);
            btnReverseInfer.Size = new Size(215, 30);
            btnReverseInfer.Cursor = Cursors.Hand;
            btnReverseInfer.Click += BtnReverseInfer_Click;
            pnlToolbar.Controls.Add(btnReverseInfer);

            btnSavePreset = new Button();
            btnSavePreset.Text = "💾 保存方案";
            btnSavePreset.Location = new Point(575, 6);
            btnSavePreset.Size = new Size(95, 30);
            btnSavePreset.BackColor = Color.FromArgb(48, 54, 66);
            btnSavePreset.ForeColor = Color.White;
            btnSavePreset.FlatStyle = FlatStyle.Flat;
            btnSavePreset.FlatAppearance.BorderSize = 0;
            btnSavePreset.Cursor = Cursors.Hand;
            btnSavePreset.Click += BtnSavePreset_Click;
            pnlToolbar.Controls.Add(btnSavePreset);

            btnLoadPreset = new Button();
            btnLoadPreset.Text = "📂 载入方案";
            btnLoadPreset.Location = new Point(676, 6);
            btnLoadPreset.Size = new Size(95, 30);
            btnLoadPreset.BackColor = Color.FromArgb(48, 54, 66);
            btnLoadPreset.ForeColor = Color.White;
            btnLoadPreset.FlatStyle = FlatStyle.Flat;
            btnLoadPreset.FlatAppearance.BorderSize = 0;
            btnLoadPreset.Cursor = Cursors.Hand;
            btnLoadPreset.Click += BtnLoadPreset_Click;
            pnlToolbar.Controls.Add(btnLoadPreset);

            btnBrowseRefDwg = new Button();
            btnBrowseRefDwg.Text = "📁 选择参考标准DWG";
            btnBrowseRefDwg.Location = new Point(pnlToolbar.Width - 175, 6);
            btnBrowseRefDwg.Size = new Size(160, 30);
            btnBrowseRefDwg.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowseRefDwg.BackColor = Color.FromArgb(14, 99, 156);
            btnBrowseRefDwg.ForeColor = Color.White;
            btnBrowseRefDwg.FlatStyle = FlatStyle.Flat;
            btnBrowseRefDwg.FlatAppearance.BorderSize = 0;
            btnBrowseRefDwg.Cursor = Cursors.Hand;
            btnBrowseRefDwg.Click += BtnBrowseRefDwg_Click;
            pnlToolbar.Controls.Add(btnBrowseRefDwg);

            this.Controls.Add(pnlToolbar);

            // ==========================================
            // 3. 过滤与瘦身栏 (Toolbar Row 2)
            // ==========================================
            pnlFilterBar = new Panel();
            pnlFilterBar.Dock = DockStyle.Top;
            pnlFilterBar.Height = 40;
            pnlFilterBar.BackColor = Color.FromArgb(28, 32, 40);
            pnlFilterBar.Padding = new Padding(12, 4, 12, 4);

            btnPurgeUnused = new Button();
            btnPurgeUnused.Text = "🧹 一键移除未使用空图层";
            btnPurgeUnused.Font = new Font("Microsoft YaHei UI", 9F);
            btnPurgeUnused.BackColor = Color.FromArgb(35, 120, 60);
            btnPurgeUnused.ForeColor = Color.White;
            btnPurgeUnused.FlatStyle = FlatStyle.Flat;
            btnPurgeUnused.FlatAppearance.BorderSize = 0;
            btnPurgeUnused.Location = new Point(12, 5);
            btnPurgeUnused.Size = new Size(165, 28);
            btnPurgeUnused.Cursor = Cursors.Hand;
            btnPurgeUnused.Click += BtnPurgeUnused_Click;
            pnlFilterBar.Controls.Add(btnPurgeUnused);

            rbShowAll = new RadioButton();
            rbShowAll.Text = "显示全部图层";
            rbShowAll.Checked = true;
            rbShowAll.Location = new Point(190, 8);
            rbShowAll.Size = new Size(110, 22);
            rbShowAll.ForeColor = Color.FromArgb(230, 235, 245);
            rbShowAll.CheckedChanged += (s, e) => { if (rbShowAll.Checked) ApplyFilter(); };
            pnlFilterBar.Controls.Add(rbShowAll);

            rbShowActiveOnly = new RadioButton();
            rbShowActiveOnly.Text = "仅显示在用图层 (隐藏空图层)";
            rbShowActiveOnly.Location = new Point(305, 8);
            rbShowActiveOnly.Size = new Size(195, 22);
            rbShowActiveOnly.ForeColor = Color.FromArgb(97, 175, 239);
            rbShowActiveOnly.CheckedChanged += (s, e) => { if (rbShowActiveOnly.Checked) ApplyFilter(); };
            pnlFilterBar.Controls.Add(rbShowActiveOnly);

            Label lblSearch = new Label();
            lblSearch.Text = "🔍";
            lblSearch.Location = new Point(510, 10);
            lblSearch.Size = new Size(24, 20);
            lblSearch.ForeColor = Color.Gray;
            pnlFilterBar.Controls.Add(lblSearch);

            txtSearch = new TextBox();
            txtSearch.Location = new Point(535, 7);
            txtSearch.Size = new Size(130, 23);
            txtSearch.BackColor = Color.FromArgb(20, 23, 29);
            txtSearch.ForeColor = Color.White;
            txtSearch.BorderStyle = BorderStyle.FixedSingle;
            txtSearch.TextChanged += (s, e) => ApplyFilter();
            pnlFilterBar.Controls.Add(txtSearch);

            btnBrowseLin = new Button();
            btnBrowseLin.Text = "📄 载入.LIN线型";
            btnBrowseLin.Location = new Point(pnlFilterBar.Width - 140, 5);
            btnBrowseLin.Size = new Size(125, 28);
            btnBrowseLin.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowseLin.BackColor = Color.FromArgb(48, 54, 66);
            btnBrowseLin.ForeColor = Color.FromArgb(220, 225, 235);
            btnBrowseLin.FlatStyle = FlatStyle.Flat;
            btnBrowseLin.FlatAppearance.BorderSize = 0;
            btnBrowseLin.Cursor = Cursors.Hand;
            btnBrowseLin.Click += BtnBrowseLin_Click;
            pnlFilterBar.Controls.Add(btnBrowseLin);

            lblRefFileStatus = new Label();
            lblRefFileStatus.Text = "未载入参考DWG";
            lblRefFileStatus.ForeColor = Color.FromArgb(150, 160, 175);
            lblRefFileStatus.Location = new Point(pnlFilterBar.Width - 460, 9);
            lblRefFileStatus.Size = new Size(310, 20);
            lblRefFileStatus.TextAlign = ContentAlignment.MiddleRight;
            lblRefFileStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlFilterBar.Controls.Add(lblRefFileStatus);

            this.Controls.Add(pnlFilterBar);

            // ==========================================
            // 4. 底部执行栏
            // ==========================================
            pnlBottom = new Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 110;
            pnlBottom.BackColor = Color.FromArgb(24, 27, 34);
            pnlBottom.Padding = new Padding(16, 8, 16, 8);

            chkRedefineBlocks = new CheckBox();
            chkRedefineBlocks.Text = "同名图块最高优先级合并重定义 (防跑位/动态块母名穿透)";
            chkRedefineBlocks.Checked = true;
            chkRedefineBlocks.ForeColor = Color.FromArgb(255, 215, 0); // 金色
            chkRedefineBlocks.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            chkRedefineBlocks.Location = new Point(16, 8);
            chkRedefineBlocks.Size = new Size(400, 22);
            pnlBottom.Controls.Add(chkRedefineBlocks);

            chkMigrateVp = new CheckBox();
            chkMigrateVp.Text = "视口属性无损继承 (HB1/HB2 冻结与覆盖字典)";
            chkMigrateVp.Checked = true;
            chkMigrateVp.ForeColor = Color.FromArgb(100, 220, 255);
            chkMigrateVp.Location = new Point(420, 8);
            chkMigrateVp.Size = new Size(320, 22);
            pnlBottom.Controls.Add(chkMigrateVp);

            chkDeepBlock = new CheckBox();
            chkDeepBlock.Text = "穿透图块内部实体";
            chkDeepBlock.Checked = true;
            chkDeepBlock.Location = new Point(745, 8);
            chkDeepBlock.Size = new Size(130, 22);
            pnlBottom.Controls.Add(chkDeepBlock);

            chkResetByLayer = new CheckBox();
            chkResetByLayer.Text = "图元净化为随层(ByLayer)";
            chkResetByLayer.Checked = true;
            chkResetByLayer.Location = new Point(880, 8);
            chkResetByLayer.Size = new Size(165, 22);
            pnlBottom.Controls.Add(chkResetByLayer);

            chkPurgeSource = new CheckBox();
            chkPurgeSource.Text = "合并后物理移除源图层";
            chkPurgeSource.Checked = true;
            chkPurgeSource.Location = new Point(1050, 8);
            chkPurgeSource.Size = new Size(155, 22);
            pnlBottom.Controls.Add(chkPurgeSource);

            lblStatus = new Label();
            lblStatus.Text = "就绪。";
            lblStatus.ForeColor = Color.FromArgb(170, 180, 195);
            lblStatus.Location = new Point(16, 36);
            lblStatus.Size = new Size(1140, 22);
            pnlBottom.Controls.Add(lblStatus);

            btnExportReport = new Button();
            btnExportReport.Text = "📊 导出比对报表";
            btnExportReport.Location = new Point(16, 66);
            btnExportReport.Size = new Size(130, 34);
            btnExportReport.BackColor = Color.FromArgb(48, 54, 66);
            btnExportReport.ForeColor = Color.White;
            btnExportReport.FlatStyle = FlatStyle.Flat;
            btnExportReport.FlatAppearance.BorderSize = 0;
            btnExportReport.Cursor = Cursors.Hand;
            btnExportReport.Click += BtnExportReport_Click;
            pnlBottom.Controls.Add(btnExportReport);

            btnNormalizeProps = new Button();
            btnNormalizeProps.Text = "🎨 仅对齐属性规范 (不改层名)";
            btnNormalizeProps.Location = new Point(155, 66);
            btnNormalizeProps.Size = new Size(195, 34);
            btnNormalizeProps.BackColor = Color.FromArgb(40, 50, 68);
            btnNormalizeProps.ForeColor = Color.FromArgb(180, 205, 240);
            btnNormalizeProps.FlatStyle = FlatStyle.Flat;
            btnNormalizeProps.FlatAppearance.BorderColor = Color.FromArgb(60, 80, 110);
            btnNormalizeProps.Cursor = Cursors.Hand;
            btnNormalizeProps.Click += BtnNormalizeProps_Click;
            pnlBottom.Controls.Add(btnNormalizeProps);

            btnExecuteMerge = new Button();
            btnExecuteMerge.Text = "🚀 执行全自动深度翻图合并";
            btnExecuteMerge.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            btnExecuteMerge.Location = new Point(360, 64);
            btnExecuteMerge.Size = new Size(240, 38);
            btnExecuteMerge.BackColor = Color.FromArgb(0, 122, 204);
            btnExecuteMerge.ForeColor = Color.White;
            btnExecuteMerge.FlatStyle = FlatStyle.Flat;
            btnExecuteMerge.FlatAppearance.BorderSize = 0;
            btnExecuteMerge.Cursor = Cursors.Hand;
            btnExecuteMerge.Click += BtnExecuteMerge_Click;
            pnlBottom.Controls.Add(btnExecuteMerge);

            btnClose = new Button();
            btnClose.Text = "关闭";
            btnClose.Location = new Point(pnlBottom.Width - 110, 66);
            btnClose.Size = new Size(95, 34);
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.BackColor = Color.FromArgb(48, 54, 66);
            btnClose.ForeColor = Color.White;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => this.Close();
            pnlBottom.Controls.Add(btnClose);

            this.Controls.Add(pnlBottom);

            // ==========================================
            // 5. 核心 DataGridView
            // ==========================================
            dgvCompare = new DataGridView();
            dgvCompare.Dock = DockStyle.Fill;
            dgvCompare.BorderStyle = BorderStyle.None;
            dgvCompare.BackgroundColor = Color.FromArgb(20, 23, 29);
            dgvCompare.GridColor = Color.FromArgb(45, 50, 62);
            dgvCompare.RowHeadersVisible = false;
            dgvCompare.AllowUserToAddRows = false;
            dgvCompare.AllowUserToDeleteRows = false;
            dgvCompare.AllowUserToResizeRows = false;
            dgvCompare.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            colCheck = new DataGridViewCheckBoxColumn();
            colCheck.HeaderText = "合并";
            colCheck.Width = 46;
            dgvCompare.Columns.Add(colCheck);

            colIndex = new DataGridViewTextBoxColumn();
            colIndex.HeaderText = "#";
            colIndex.Width = 42;
            colIndex.ReadOnly = true;
            dgvCompare.Columns.Add(colIndex);

            colCurName = new DataGridViewTextBoxColumn();
            colCurName.HeaderText = "当前图层名称";
            colCurName.Width = 170;
            colCurName.ReadOnly = true;
            dgvCompare.Columns.Add(colCurName);

            colEntityCount = new DataGridViewTextBoxColumn();
            colEntityCount.HeaderText = "图元数";
            colEntityCount.Width = 75;
            colEntityCount.ReadOnly = true;
            dgvCompare.Columns.Add(colEntityCount);

            colCurColor = new DataGridViewTextBoxColumn();
            colCurColor.HeaderText = "当前颜色";
            colCurColor.Width = 105;
            colCurColor.ReadOnly = true;
            dgvCompare.Columns.Add(colCurColor);

            colCurLt = new DataGridViewTextBoxColumn();
            colCurLt.HeaderText = "当前线型";
            colCurLt.Width = 105;
            colCurLt.ReadOnly = true;
            dgvCompare.Columns.Add(colCurLt);

            colCurLw = new DataGridViewTextBoxColumn();
            colCurLw.HeaderText = "线宽";
            colCurLw.Width = 65;
            colCurLw.ReadOnly = true;
            dgvCompare.Columns.Add(colCurLw);

            colMatchStatus = new DataGridViewTextBoxColumn();
            colMatchStatus.HeaderText = "智能匹配结果 (置信度)";
            colMatchStatus.Width = 200;
            colMatchStatus.ReadOnly = true;
            dgvCompare.Columns.Add(colMatchStatus);

            colTarName = new DataGridViewTextBoxColumn();
            colTarName.HeaderText = "目标标准图层 (双击微调)";
            colTarName.Width = 185;
            dgvCompare.Columns.Add(colTarName);

            colTarColor = new DataGridViewTextBoxColumn();
            colTarColor.HeaderText = "标准颜色";
            colTarColor.Width = 100;
            colTarColor.ReadOnly = true;
            dgvCompare.Columns.Add(colTarColor);

            colTarLt = new DataGridViewTextBoxColumn();
            colTarLt.HeaderText = "标准线型";
            colTarLt.Width = 100;
            colTarLt.ReadOnly = true;
            dgvCompare.Columns.Add(colTarLt);

            dgvCompare.CellPainting += DgvCompare_CellPainting;
            dgvCompare.CellValueChanged += DgvCompare_CellValueChanged;
            dgvCompare.CurrentCellDirtyStateChanged += (s, e) => { if (dgvCompare.IsCurrentCellDirty) dgvCompare.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            this.Controls.Add(dgvCompare);
            dgvCompare.BringToFront();
        }

        private void ApplyCustomStyles()
        {
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
                _currentBlocks = LayerCompareEngine.ExtractBlocks(db);

                int total = _currentLayers.Count;
                int active = _currentLayers.FindAll(x => !x.IsEmpty).Count;
                int empty = total - active;

                rbShowAll.Text = string.Format("显示全部 ({0})", total);
                rbShowActiveOnly.Text = string.Format("仅在用 (藏 {0} 空层)", empty);

                _allMatches = LayerCompareEngine.ComputeMatches(_currentLayers, _refLayers);
                _blockMatches = LayerCompareEngine.ComputeBlockMatches(_currentBlocks, _refBlocks);

                ApplyFilter();

                int sameBlockCount = _blockMatches.FindAll(x => x.IsSameNameMatched).Count;
                lblStatus.Text = string.Format("当前图层: {0} 个 (在用 {1}) | 图块: {2} 个 (同名匹配 {3} 个最高优先级) | 参考标准层: {4} 个",
                    total, active, _currentBlocks.Count, sameBlockCount, _refLayers.Count);
            }
            catch (System.Exception ex)
            {
                lblStatus.Text = "读取当前图纸失败: " + ex.Message;
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
                    statusText = "— 保持原样";
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
                if (item.CurrentLayer.IsEmpty)
                {
                    dgvCompare.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.FromArgb(130, 140, 155);
                }
            }
        }

        private void DgvCompare_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvCompare.Rows.Count) return;

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
        // 核心事件：载入上次翻图记忆 (多图复用)
        // ==========================================
        private void BtnLoadLastPreset_Click(object sender, EventArgs e)
        {
            FlipRulePreset preset = FlipRuleStorage.LoadLastPreset();
            if (preset == null || preset.LayerMappings == null || preset.LayerMappings.Count == 0)
            {
                MessageBox.Show(this, "暂无历史翻图记忆记录。\n请先选择参考图完成一次标准翻图！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int hitCount = 0;
            foreach (var match in _allMatches)
            {
                if (preset.LayerMappings.ContainsKey(match.CurrentLayer.Name))
                {
                    match.TargetLayerName = preset.LayerMappings[match.CurrentLayer.Name];
                    match.IsSelectedForMerge = true;
                    match.MatchReason = "★ 记忆方案自动对齐";
                    match.Level = MatchConfidenceLevel.High;
                    hitCount++;
                }
            }

            ApplyFilter();
            MessageBox.Show(this, string.Format("已成功载入上次翻图记忆！\n共自动匹配当前图纸中的 {0} 个业务图层。\n核对无误后点击下方【执行全自动深度翻图合并】即可！", hitCount), "载入记忆成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ==========================================
        // 核心事件：一键套用并执行翻图 (秒翻)
        // ==========================================
        private void BtnQuickFlipNow_Click(object sender, EventArgs e)
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            using (doc.LockDocument())
            {
                MergeReport r = LayerDeepMergeService.QuickApplyLastPreset(doc.Database);
                MessageBox.Show(this, r.SummaryMessage, "极速翻图结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                doc.Editor.UpdateScreen();
            }

            RefreshCurrentDrawingLayers();
        }

        // ==========================================
        // 核心事件：双图逆向分析 (反推提取合并规则)
        // ==========================================
        private void BtnReverseInfer_Click(object sender, EventArgs e)
        {
            string rawPath = "";
            string finPath = "";

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "第 1 步: 请选择【翻图前的原始底图 (旧图)】";
                ofd.Filter = "AutoCAD 图纸 (*.dwg)|*.dwg";
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                rawPath = ofd.FileName;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "第 2 步: 请选择【已翻好的标准成品图 (新图)】";
                ofd.Filter = "AutoCAD 图纸 (*.dwg)|*.dwg";
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                finPath = ofd.FileName;
            }

            lblStatus.Text = "正在进行双图句柄碰撞与空间图元逆向分析，请稍候...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                FlipRulePreset inferred = LayerCompareEngine.ReverseInferMappings(rawPath, finPath);
                this.Cursor = Cursors.Default;

                if (inferred == null || inferred.LayerMappings.Count == 0)
                {
                    MessageBox.Show(this, "未能在两张图纸中检测出图层变化，或两图图元不匹配。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 将反推结果自动保存为记忆，并直接应用到当前面板
                FlipRuleStorage.SaveLastPreset(inferred);

                int hitCount = 0;
                foreach (var match in _allMatches)
                {
                    if (inferred.LayerMappings.ContainsKey(match.CurrentLayer.Name))
                    {
                        match.TargetLayerName = inferred.LayerMappings[match.CurrentLayer.Name];
                        match.IsSelectedForMerge = true;
                        match.MatchReason = "★ 双图逆向反推规则";
                        match.Level = MatchConfidenceLevel.ExactMatch;
                        hitCount++;
                    }
                }

                ApplyFilter();

                string msg = string.Format(
                    "★ 逆向对比破译成功！\n\n" +
                    "• 成功反推提取出 {0} 条图层合并映射规则\n" +
                    "• 成功提取出 {1} 个同名图块重定义规则\n" +
                    "• 已完美命中当前图纸中的 {2} 个业务图层\n\n" +
                    "已自动将该规则保存为最新记忆，后续图纸可直接敲 XFT 秒翻！",
                    inferred.LayerMappings.Count, inferred.BlockMappings.Count, hitCount);

                MessageBox.Show(this, msg, "逆向分析完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show(this, "逆向分析失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 保存方案
        private void BtnSavePreset_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "保存翻图方案规则包";
                sfd.Filter = "翻图规则方案 (*.xyrule)|*.xyrule|JSON 文件 (*.json)|*.json";
                sfd.FileName = "翻图方案_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xyrule";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    FlipRulePreset preset = new FlipRulePreset();
                    preset.PresetName = Path.GetFileNameWithoutExtension(sfd.FileName);
                    preset.RefDwgPath = _refDwgPath;
                    preset.DeepBlockTraversal = chkDeepBlock.Checked;
                    preset.MigrateViewportOverrides = chkMigrateVp.Checked;
                    preset.PurgeSourceLayersAfterMerge = chkPurgeSource.Checked;
                    preset.ResetEntitiesByLayer = chkResetByLayer.Checked;
                    preset.RedefineSameNameBlocks = chkRedefineBlocks.Checked;

                    foreach (var item in _allMatches)
                    {
                        if (item.IsSelectedForMerge && !string.IsNullOrEmpty(item.TargetLayerName) && item.TargetLayerName != item.CurrentLayer.Name)
                        {
                            preset.LayerMappings[item.CurrentLayer.Name] = item.TargetLayerName;
                        }
                    }

                    foreach (var bm in _blockMatches)
                    {
                        if (bm.IsSelectedForMerge)
                        {
                            preset.BlockMappings[bm.BlockName] = bm.TargetBlockName;
                        }
                    }

                    string json = new JavaScriptSerializer().Serialize(preset);
                    File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                    MessageBox.Show(this, "方案已成功导出！可分享给全组团队复用:\n" + sfd.FileName, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        // 载入方案
        private void BtnLoadPreset_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "选择翻图方案规则包";
                ofd.Filter = "翻图规则方案 (*.xyrule)|*.xyrule|JSON 文件 (*.json)|*.json";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string json = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                        FlipRulePreset preset = new JavaScriptSerializer().Deserialize<FlipRulePreset>(json);
                        if (preset != null && preset.LayerMappings != null)
                        {
                            FlipRuleStorage.SaveLastPreset(preset);
                            int hitCount = 0;
                            foreach (var match in _allMatches)
                            {
                                if (preset.LayerMappings.ContainsKey(match.CurrentLayer.Name))
                                {
                                    match.TargetLayerName = preset.LayerMappings[match.CurrentLayer.Name];
                                    match.IsSelectedForMerge = true;
                                    match.MatchReason = "★ 方案包规则匹配";
                                    hitCount++;
                                }
                            }
                            ApplyFilter();
                            MessageBox.Show(this, string.Format("方案载入成功！共匹配 {0} 个图层，已同步至最新记忆！", hitCount), "载入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show(this, "载入方案失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // 一键清理空图层
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

            RefreshCurrentDrawingLayers();
        }

        // 选择参考标准 DWG
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
                    _refBlocks = LayerCompareEngine.ExtractReferenceBlocks(_refDwgPath);

                    _allMatches = LayerCompareEngine.ComputeMatches(_currentLayers, _refLayers);
                    _blockMatches = LayerCompareEngine.ComputeBlockMatches(_currentBlocks, _refBlocks);

                    ApplyFilter();

                    int sameBlockCount = _blockMatches.FindAll(x => x.IsSameNameMatched).Count;
                    lblStatus.Text = string.Format("标准库已载入: {0} 个图层，{1} 个图块 (同名命中 {2} 个，同名块将最高优先级重定义)！",
                        _refLayers.Count, _refBlocks.Count, sameBlockCount);
                }
            }
        }

        // 载入 LIN
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
                MergeReport r = LayerDeepMergeService.ExecuteDeepMerge(doc.Database, _allMatches, opts, _blockMatches);
                MessageBox.Show(this, r.SummaryMessage, "规范化对齐完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                doc.Editor.UpdateScreen();
            }

            RefreshCurrentDrawingLayers();
        }

        // 执行全自动深度翻图合并
        private void BtnExecuteMerge_Click(object sender, EventArgs e)
        {
            List<LayerMatchResult> selected = _allMatches.FindAll(x => x.IsSelectedForMerge && !string.IsNullOrEmpty(x.TargetLayerName) && x.TargetLayerName != x.CurrentLayer.Name);
            int sameBlocksToRedefine = chkRedefineBlocks.Checked ? _blockMatches.FindAll(x => x.IsSelectedForMerge && x.IsSameNameMatched).Count : 0;

            if (selected.Count == 0 && sameBlocksToRedefine == 0)
            {
                MessageBox.Show(this, "当前没有勾选需要执行合并的图层或图块项！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string confirmMsg = string.Format(
                "即将执行全自动深度穿透翻图合并：\n\n" +
                "• 待合并图层数: {0} 个\n" +
                "• 同名图块重定义: {1} 个 (最高优先级 · 坐标防跑偏)\n" +
                "• 视口图层覆盖无损迁移: {2} (HB1/HB2 逻辑)\n" +
                "• 穿透图块与嵌套块定义: {3}\n" +
                "• 合并后物理移除源图层: {4}\n" +
                "• 图元属性重置随层(ByLayer): {5}\n\n" +
                "是否立即开始处理？",
                selected.Count,
                sameBlocksToRedefine,
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
            opts.RedefineSameNameBlocks = chkRedefineBlocks.Checked;
            opts.RefDwgPath = _refDwgPath;
            opts.NormalizeAttributesOnly = false;

            using (doc.LockDocument())
            {
                MergeReport r = LayerDeepMergeService.ExecuteDeepMerge(doc.Database, _allMatches, opts, _blockMatches);
                MessageBox.Show(this, r.SummaryMessage, "深度翻图合并完毕", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                sfd.FileName = "图层与图块规范翻图报表_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("序号,当前图层名,图元数,当前颜色,当前线型,线宽,智能匹配状态,目标标准图层,标准颜色,标准线型,是否执行合并");
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
