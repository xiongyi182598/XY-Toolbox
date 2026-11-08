using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace XY_Toolbox
{
    /// <summary>
    /// 在线升级服务元数据实体
    /// </summary>
    public class RemoteVersionInfo
    {
        public string Version { get; set; }
        public int VersionCode { get; set; }
        public string ReleaseDate { get; set; }
        public string Title { get; set; }
        public string Changelog { get; set; }
        public string DownloadUrl { get; set; }
        public List<string> MirrorUrls { get; set; }

        public RemoteVersionInfo()
        {
            MirrorUrls = new List<string>();
        }
    }

    /// <summary>
    /// 商业级在线检测更新服务
    /// 支持 GitHub 官方源与国内多镜像线路竞速与自动降级
    /// </summary>
    public static class UpdateService
    {
        // 本地当前版本常量
        public const string CURRENT_VERSION = "1.0.8";
        public const int CURRENT_VERSION_CODE = 108;
        public const string GITHUB_REPO_URL = "https://github.com/xiongyi182598/XY-Toolbox";

        // 多镜像版本信息地址源（优先国内加速通道）
        // 多镜像版本信息地址源（优先无缓存实时镜像与国内极速直连节点）
        private static readonly string[] VersionEndpoints = new string[]
        {
            "https://ghfast.top/https://raw.githubusercontent.com/xiongyi182598/XY-Toolbox/main/version.json",
            "https://mirror.ghproxy.com/https://raw.githubusercontent.com/xiongyi182598/XY-Toolbox/main/version.json",
            "https://ghproxy.net/https://raw.githubusercontent.com/xiongyi182598/XY-Toolbox/main/version.json",
            "https://cdn.jsdelivr.net/gh/xiongyi182598/XY-Toolbox@main/version.json",
            "https://fastly.jsdelivr.net/gh/xiongyi182598/XY-Toolbox@main/version.json",
            "https://raw.gitmirror.com/xiongyi182598/XY-Toolbox/main/version.json",
            "https://raw.githubusercontent.com/xiongyi182598/XY-Toolbox/main/version.json"
        };

        private static bool _hasPromptedSilentUpdate = false;

        static UpdateService()
        {
            // 确保支持现代 TLS 1.2 加密传输
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 /* Tls12 */ | SecurityProtocolType.Tls | SecurityProtocolType.Ssl3;
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            }
            catch { }
        }

        /// <summary>
        /// 检查更新入口（支持静默检测与手动检测）
        /// </summary>
        /// <param name="silent">是否静默模式（启动时静默，仅发现新版在控制台与面板微弱提示）</param>
        public static void CheckForUpdates(bool silent)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    RemoteVersionInfo info = FetchRemoteVersionInfo();
                    if (info == null)
                    {
                        if (!silent)
                        {
                            ShowUpdateFailureDialog();
                        }
                        return;
                    }

                    bool hasNewVersion = info.VersionCode > CURRENT_VERSION_CODE;

                    if (hasNewVersion)
                    {
                        if (silent)
                        {
                            if (!_hasPromptedSilentUpdate)
                            {
                                _hasPromptedSilentUpdate = true;
                                PrintToCadEditor(string.Format("\n[XY工具箱] 发现新版本 {0} (当前: v{1})！输入 XGX 即可在线查看并一键升级。", info.Version, CURRENT_VERSION));
                            }
                        }
                        else
                        {
                            // 手动点击：弹出深色现代化更新弹窗
                            OpenUpdateDialog(info);
                        }
                    }
                    else
                    {
                        if (!silent)
                        {
                            ShowMessageOnMainThread("已经是最新版本", string.Format("当前版本 v{0} 已是官方最新正式版，无需更新！\n感谢您使用 XY工具箱。", CURRENT_VERSION));
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    if (!silent)
                    {
                        ShowMessageOnMainThread("检测更新异常", "检测过程发生错误: " + ex.Message);
                    }
                }
            });
        }

        /// <summary>
        /// 遍历多线路获取最新版本元数据 (带代理死锁自动脱困机制)
        /// </summary>
        public static RemoteVersionInfo FetchRemoteVersionInfo()
        {
            foreach (string url in VersionEndpoints)
            {
                string json = HttpGetWithFallback(url);
                if (!string.IsNullOrEmpty(json))
                {
                    RemoteVersionInfo info = ParseVersionJson(json);
                    if (info != null && !string.IsNullOrEmpty(info.Version))
                    {
                        return info;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 极速带容灾的 HTTP GET 请求，双重测试系统代理与纯直连模式，防止本地代理死锁
        /// </summary>
        private static string HttpGetWithFallback(string url)
        {
            string fetchUrl = url.Contains("?") ? (url + "&_t=" + DateTime.UtcNow.Ticks) : (url + "?_t=" + DateTime.UtcNow.Ticks);

            // 尝试 1：常规请求
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(fetchUrl);
                req.Timeout = 3000;
                req.ReadWriteTimeout = 3000;
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) XY_Toolbox_Updater/1.0";
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch { }

            // 尝试 2：强制 Bypass 本地无效或残留代理配置，纯粹裸连直通
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Proxy = null;
                req.Timeout = 3000;
                req.ReadWriteTimeout = 3000;
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) XY_Toolbox_Updater/1.0";
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch { }

            return null;
        }

        private static void ShowUpdateFailureDialog()
        {
            try
            {
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                    "\n[XY工具箱] 无法直连版本服务器，您可访问官方页面: " + GITHUB_REPO_URL + "\n");
            }
            catch { }

            Form dlg = new Form();
            dlg.Text = "检查更新提示";
            dlg.Size = new Size(420, 210);
            dlg.StartPosition = FormStartPosition.CenterScreen;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;
            dlg.BackColor = Color.FromArgb(28, 32, 40);
            dlg.ForeColor = Color.White;

            Label lbl = new Label();
            lbl.Text = "暂时无法连接到云端版本服务器（可能受限于国内网络环境）。\n\n您可以直接前往官方 GitHub 仓库页面下载最新发布包。";
            lbl.Location = new Point(25, 25);
            lbl.Size = new Size(360, 65);
            lbl.Font = new Font("SimSun", 9F, FontStyle.Regular);
            lbl.ForeColor = Color.FromArgb(230, 235, 245);
            dlg.Controls.Add(lbl);

            Button btnOpen = new Button();
            btnOpen.Text = "访问 GitHub 页面";
            btnOpen.Location = new Point(110, 115);
            btnOpen.Size = new Size(150, 32);
            btnOpen.BackColor = Color.FromArgb(40, 110, 220);
            btnOpen.ForeColor = Color.White;
            btnOpen.FlatStyle = FlatStyle.Flat;
            btnOpen.Click += (s, e) => {
                try { Process.Start(GITHUB_REPO_URL + "/releases"); } catch { }
                dlg.Close();
            };
            dlg.Controls.Add(btnOpen);

            Button btnCancel = new Button();
            btnCancel.Text = "关闭";
            btnCancel.Location = new Point(280, 115);
            btnCancel.Size = new Size(90, 32);
            btnCancel.BackColor = Color.FromArgb(50, 56, 68);
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Click += (s, e) => dlg.Close();
            dlg.Controls.Add(btnCancel);

            Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dlg);
        }

        /// <summary>
        /// 轻量正则 JSON 解析器（零第三方 DLL 依赖，100% 稳健）
        /// </summary>
        public static RemoteVersionInfo ParseVersionJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                RemoteVersionInfo info = new RemoteVersionInfo();

                // 提取 version
                Match mVer = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (mVer.Success) info.Version = mVer.Groups[1].Value.Trim();

                // 提取 version_code
                Match mCode = Regex.Match(json, "\"version_code\"\\s*:\\s*([0-9]+)", RegexOptions.IgnoreCase);
                if (mCode.Success)
                {
                    int c;
                    if (int.TryParse(mCode.Groups[1].Value, out c)) info.VersionCode = c;
                }

                // 提取 release_date
                Match mDate = Regex.Match(json, "\"release_date\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (mDate.Success) info.ReleaseDate = mDate.Groups[1].Value.Trim();

                // 提取 title
                Match mTitle = Regex.Match(json, "\"title\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (mTitle.Success) info.Title = mTitle.Groups[1].Value.Trim();

                // 提取 download_url
                Match mUrl = Regex.Match(json, "\"download_url\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (mUrl.Success) info.DownloadUrl = mUrl.Groups[1].Value.Trim();

                // 提取 changelog 数组
                Match mLogs = Regex.Match(json, "\"changelog\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (mLogs.Success)
                {
                    string content = mLogs.Groups[1].Value;
                    MatchCollection items = Regex.Matches(content, "\"([^\"]+)\"");
                    StringBuilder sb = new StringBuilder();
                    foreach (Match it in items)
                    {
                        sb.AppendLine(it.Groups[1].Value.Replace("\\n", "\n"));
                    }
                    info.Changelog = sb.ToString().TrimEnd();
                }

                if (string.IsNullOrEmpty(info.Title))
                {
                    info.Title = "XY工具箱 v" + info.Version;
                }

                return info;
            }
            catch
            {
                return null;
            }
        }

        private static void OpenUpdateDialog(RemoteVersionInfo info)
        {
            try
            {
                Thread t = new Thread(() =>
                {
                    using (UpdateDialog dlg = new UpdateDialog(info))
                    {
                        dlg.ShowDialog();
                    }
                });
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
            }
            catch (System.Exception ex)
            {
                PrintToCadEditor("\n[XY工具箱] 打开更新界面异常: " + ex.Message);
            }
        }

        private static void ShowMessageOnMainThread(string title, string msg)
        {
            try
            {
                Thread t = new Thread(() =>
                {
                    MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
            }
            catch { }
        }

        public static void PrintToCadEditor(string msg)
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null && doc.Editor != null)
                {
                    doc.Editor.WriteMessage(msg);
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// 深色现代工业风在线更新对话框
    /// </summary>
    public class UpdateDialog : Form
    {
        private RemoteVersionInfo _info;
        private Label lblHeaderTitle;
        private Label lblVerInfo;
        private Label lblDateInfo;
        private TextBox txtChangelog;
        private ProgressBar prgDownload;
        private Label lblStatus;
        private Button btnUpdate;
        private Button btnGitHub;
        private Button btnCancel;
        private WebClient _webClient;

        public UpdateDialog(RemoteVersionInfo info)
        {
            _info = info;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "XY工具箱 · 在线升级服务";
            this.Size = new Size(520, 430);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(28, 32, 40);
            this.ForeColor = Color.White;
            this.Font = new Font("SimSun", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // 1. 顶部 Header
            Panel pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 50;
            pnlHeader.BackColor = Color.FromArgb(20, 23, 28);

            lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "★ 发现新版本: " + (_info != null ? _info.Title : "XY工具箱");
            lblHeaderTitle.Font = new Font("SimSun", 10.5F, FontStyle.Bold);
            lblHeaderTitle.ForeColor = Color.FromArgb(255, 215, 0); // 金色
            lblHeaderTitle.Location = new Point(16, 15);
            lblHeaderTitle.AutoSize = true;
            pnlHeader.Controls.Add(lblHeaderTitle);
            this.Controls.Add(pnlHeader);

            // 2. 版本简要信息
            lblVerInfo = new Label();
            lblVerInfo.Text = string.Format("当前版本: v{0}    ➔    最新版本: v{1}", UpdateService.CURRENT_VERSION, _info != null ? _info.Version : "未知");
            lblVerInfo.Font = new Font("SimSun", 9.5F, FontStyle.Bold);
            lblVerInfo.ForeColor = Color.FromArgb(100, 210, 255); // 科技蓝
            lblVerInfo.Location = new Point(18, 62);
            lblVerInfo.AutoSize = true;
            this.Controls.Add(lblVerInfo);

            lblDateInfo = new Label();
            lblDateInfo.Text = "发布日期: " + (_info != null && !string.IsNullOrEmpty(_info.ReleaseDate) ? _info.ReleaseDate : DateTime.Now.ToString("yyyy-MM-dd"));
            lblDateInfo.ForeColor = Color.FromArgb(180, 190, 205);
            lblDateInfo.Location = new Point(330, 62);
            lblDateInfo.AutoSize = true;
            this.Controls.Add(lblDateInfo);

            // 3. 更新日志标签与文本框
            Label lblLogTip = new Label();
            lblLogTip.Text = "【本次版本更新内容与特性】:";
            lblLogTip.ForeColor = Color.FromArgb(220, 225, 235);
            lblLogTip.Location = new Point(18, 92);
            lblLogTip.AutoSize = true;
            this.Controls.Add(lblLogTip);

            txtChangelog = new TextBox();
            txtChangelog.Multiline = true;
            txtChangelog.ReadOnly = true;
            txtChangelog.ScrollBars = ScrollBars.Vertical;
            txtChangelog.BackColor = Color.FromArgb(20, 23, 28);
            txtChangelog.ForeColor = Color.FromArgb(240, 245, 250);
            txtChangelog.Location = new Point(18, 114);
            txtChangelog.Size = new Size(468, 180);
            txtChangelog.BorderStyle = BorderStyle.FixedSingle;
            txtChangelog.Text = _info != null && !string.IsNullOrEmpty(_info.Changelog) 
                ? _info.Changelog 
                : "1. 修复已知问题并优化操作体验；\n2. 增强多版本 AutoCAD/天正架构兼容性。";
            this.Controls.Add(txtChangelog);

            // 4. 下载进度条与状态显示
            prgDownload = new ProgressBar();
            prgDownload.Location = new Point(18, 305);
            prgDownload.Size = new Size(468, 14);
            prgDownload.Visible = false;
            this.Controls.Add(prgDownload);

            lblStatus = new Label();
            lblStatus.Location = new Point(18, 325);
            lblStatus.Size = new Size(468, 18);
            lblStatus.ForeColor = Color.FromArgb(255, 200, 100);
            lblStatus.Text = "";
            this.Controls.Add(lblStatus);

            // 5. 底部操作按钮
            btnUpdate = new Button();
            btnUpdate.Text = "一键在线升级";
            btnUpdate.Size = new Size(130, 32);
            btnUpdate.Location = new Point(18, 345);
            btnUpdate.BackColor = Color.FromArgb(0, 122, 204);
            btnUpdate.ForeColor = Color.White;
            btnUpdate.FlatStyle = FlatStyle.Flat;
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.Cursor = Cursors.Hand;
            btnUpdate.Click += BtnUpdate_Click;
            this.Controls.Add(btnUpdate);

            btnGitHub = new Button();
            btnGitHub.Text = "访问 GitHub 页面";
            btnGitHub.Size = new Size(130, 32);
            btnGitHub.Location = new Point(160, 345);
            btnGitHub.BackColor = Color.FromArgb(45, 52, 65);
            btnGitHub.ForeColor = Color.White;
            btnGitHub.FlatStyle = FlatStyle.Flat;
            btnGitHub.FlatAppearance.BorderSize = 0;
            btnGitHub.Cursor = Cursors.Hand;
            btnGitHub.Click += (s, e) =>
            {
                try { Process.Start(UpdateService.GITHUB_REPO_URL); } catch { }
            };
            this.Controls.Add(btnGitHub);

            btnCancel = new Button();
            btnCancel.Text = "稍后再说";
            btnCancel.Size = new Size(90, 32);
            btnCancel.Location = new Point(396, 345);
            btnCancel.BackColor = Color.FromArgb(40, 45, 55);
            btnCancel.ForeColor = Color.FromArgb(200, 205, 215);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);
        }

        public static string GetToolboxRootDirectory()
        {
            try
            {
                string asmLoc = typeof(UpdateService).Assembly.Location;
                if (!string.IsNullOrEmpty(asmLoc))
                {
                    string dir = Path.GetDirectoryName(asmLoc);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        if (Path.GetFileName(dir).Equals("cache", StringComparison.OrdinalIgnoreCase))
                        {
                            DirectoryInfo p = Directory.GetParent(dir);
                            if (p != null && p.Exists) return p.FullName;
                        }
                        if (File.Exists(Path.Combine(dir, "autoload.lsp")))
                            return dir;
                    }
                }
            }
            catch { }
            try
            {
                string lsp = XY_Deepening_Toolbox.Commands.FindToolboxFile("autoload.lsp");
                if (!string.IsNullOrEmpty(lsp) && File.Exists(lsp))
                {
                    string dir = Path.GetDirectoryName(lsp);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        return dir;
                }
            }
            catch { }
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (File.Exists(Path.Combine(baseDir, "autoload.lsp")))
                    return baseDir;
            }
            catch { }
            try
            {
                string asmLoc = typeof(UpdateService).Assembly.Location;
                if (!string.IsNullOrEmpty(asmLoc))
                {
                    string dir = Path.GetDirectoryName(asmLoc);
                    if (Path.GetFileName(dir).Equals("cache", StringComparison.OrdinalIgnoreCase))
                    {
                        DirectoryInfo p = Directory.GetParent(dir);
                        if (p != null && p.Exists) return p.FullName;
                    }
                    return dir;
                }
            }
            catch { }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            if (_info == null || string.IsNullOrEmpty(_info.DownloadUrl))
            {
                MessageBox.Show(this, "未配置更新包下载地址，请直接前往 GitHub Releases 页面下载安装包！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnUpdate.Enabled = false;
            btnCancel.Enabled = false;
            prgDownload.Visible = true;
            prgDownload.Value = 0;
            lblStatus.Text = "正在连接加速镜像节点获取更新包...";

            string rootDir = GetToolboxRootDirectory();
            string tempDir = Path.Combine(Path.GetTempPath(), "XY_Toolbox_Update_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string zipPath = Path.Combine(tempDir, "update.zip");

            // 智能下载线路候选（多镜像国内高速竞速）
            List<string> candidateUrls = new List<string>();
            string rawUrl = _info.DownloadUrl;
            if (!string.IsNullOrEmpty(rawUrl))
            {
                if (rawUrl.StartsWith("https://github.com/"))
                {
                    candidateUrls.Add("https://ghfast.top/" + rawUrl);
                    candidateUrls.Add("https://mirror.ghproxy.com/" + rawUrl);
                    candidateUrls.Add("https://ghproxy.net/" + rawUrl);
                    candidateUrls.Add("https://gh-proxy.com/" + rawUrl);
                    candidateUrls.Add("https://github.moeyy.xyz/" + rawUrl);
                }
                candidateUrls.Add(rawUrl);
            }

            Task.Factory.StartNew(() =>
            {
                bool downloadSuccess = false;
                foreach (string dUrl in candidateUrls)
                {
                    try
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblStatus.Text = "正在下载更新包 (支持国内高速线路)...";
                        }));

                        using (_webClient = new WebClient())
                        {
                            _webClient.Proxy = null; // 关键：防代理卡死
                            _webClient.Headers.Add("User-Agent", "Mozilla/5.0 XY_Toolbox_Updater/1.0");
                            _webClient.DownloadProgressChanged += (s, pe) =>
                            {
                                try
                                {
                                    this.Invoke(new Action(() =>
                                    {
                                        prgDownload.Value = pe.ProgressPercentage;
                                        lblStatus.Text = string.Format("正在下载: {0}% ({1:F1}MB / {2:F1}MB)", 
                                            pe.ProgressPercentage, 
                                            (double)pe.BytesReceived / 1048576.0, 
                                            (double)pe.TotalBytesToReceive / 1048576.0);
                                    }));
                                }
                                catch { }
                            };

                            var syncObj = new object();
                            bool isDone = false;
                            System.Exception downloadEx = null;

                            _webClient.DownloadFileCompleted += (s, ce) =>
                            {
                                lock (syncObj)
                                {
                                    isDone = true;
                                    downloadEx = ce.Error;
                                    Monitor.Pulse(syncObj);
                                }
                            };

                            _webClient.DownloadFileAsync(new Uri(dUrl), zipPath);

                            lock (syncObj)
                            {
                                while (!isDone)
                                {
                                    Monitor.Wait(syncObj, 1000);
                                }
                            }

                            if (downloadEx == null && File.Exists(zipPath) && new FileInfo(zipPath).Length > 1024)
                            {
                                downloadSuccess = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (!downloadSuccess)
                {
                    this.Invoke(new Action(() =>
                    {
                        prgDownload.Visible = false;
                        btnUpdate.Enabled = true;
                        btnCancel.Enabled = true;
                        lblStatus.Text = "下载失败，请检查网络或点击按钮前往 GitHub 手动下载。";
                        MessageBox.Show(this, "更新包下载失败，可能网络受限。\n您可以点击【访问 GitHub 页面】直接下载离线安装包！", "网络提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                    return;
                }

                // 2. 解压更新包并执行部署
                this.Invoke(new Action(() =>
                {
                    lblStatus.Text = "下载完毕，正在解压并热替换部署...";
                }));

                try
                {
                    string extractDir = Path.Combine(tempDir, "extracted");
                    Directory.CreateDirectory(extractDir);
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);

                    // 如果压缩包内只有一层同名根目录，提取之
                    string[] subDirs = Directory.GetDirectories(extractDir);
                    string[] files = Directory.GetFiles(extractDir);
                    string targetContentDir = extractDir;
                    if (files.Length == 0 && subDirs.Length == 1)
                    {
                        targetContentDir = subDirs[0];
                    }

                    // 开始逐文件部署
                    string pendingDir = Path.Combine(rootDir, "pending_update");
                    if (!Directory.Exists(pendingDir)) Directory.CreateDirectory(pendingDir);

                    string verDllName = "XY_Toolbox_" + _info.VersionCode + ".dll";
                    string verDllPath = Path.Combine(rootDir, verDllName);

                    foreach (string srcFile in Directory.GetFiles(targetContentDir, "*.*", SearchOption.AllDirectories))
                    {
                        string relative = srcFile.Substring(targetContentDir.Length).TrimStart('\\', '/');
                        string destFile = Path.Combine(rootDir, relative);
                        string destDir = Path.GetDirectoryName(destFile);
                        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                        string ext = Path.GetExtension(destFile).ToLower();
                        if (ext == ".dll")
                        {
                            // 1. 无锁最新版本代号 DLL (例如 XY_Toolbox_105.dll) 直接物理落盘
                            try { File.Copy(srcFile, verDllPath, true); } catch { }

                            // 2. 写入 pending_update 备用
                            try
                            {
                                string pFile = Path.Combine(pendingDir, Path.GetFileName(destFile));
                                File.Copy(srcFile, pFile, true);
                            }
                            catch { }

                            // 3. 同时尝试直接覆盖主 DLL (若当前 CAD 锁住则在重启批处理中强制覆盖)
                            try { File.Copy(srcFile, destFile, true); } catch { }
                        }
                        else
                        {
                            // 脚本、图标、配置无进程独占锁，直接热覆盖
                            try { File.Copy(srcFile, destFile, true); } catch { }
                        }
                    }

                    this.Invoke(new Action(() =>
                    {
                        prgDownload.Value = 100;
                        lblStatus.Text = "★ 更新部署就绪！点击下方按钮立即重启生效。";

                        btnUpdate.Text = "★ 立即重启 AutoCAD 生效 (推荐)";
                        btnUpdate.Size = new Size(250, 34);
                        btnUpdate.Location = new Point(18, 345);
                        btnUpdate.BackColor = Color.FromArgb(0, 160, 80);
                        btnUpdate.Visible = true;
                        btnUpdate.Enabled = true;
                        btnUpdate.Click -= BtnUpdate_Click;
                        btnUpdate.Click += (s, ev) =>
                        {
                            this.Hide();
                            RestartAndApplyUpdate(rootDir, targetContentDir);
                        };

                        btnGitHub.Visible = false;

                        btnCancel.Text = "稍后手动重启";
                        btnCancel.Location = new Point(280, 345);
                        btnCancel.Size = new Size(110, 34);
                        btnCancel.Enabled = true;

                        DialogResult dr = MessageBox.Show(this, 
                            string.Format("★ 恭喜！XY工具箱全套组件已在线升级至最新正式版 (v{0})！\n\n" +
                                          "是否立即自动重启 AutoCAD 完成全新升级？\n\n" +
                                          "【是 (推荐)】：全自动安全重启 CAD，2秒内秒级生效最新版！\n" +
                                          "【否】：下次您手动启动 CAD 时自动生效。", _info.Version), 
                            "在线升级成功 - 推荐立即重启生效", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                        if (dr == DialogResult.Yes)
                        {
                            this.Hide();
                            RestartAndApplyUpdate(rootDir, targetContentDir);
                        }
                        else
                        {
                            UpdateService.PrintToCadEditor(string.Format("\n★ [XY工具箱] v{0} 组件已就绪！下次启动 CAD 自动加载。\n", _info.Version));
                        }
                    }));
                }
                catch (System.Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        lblStatus.Text = "更新应用异常: " + ex.Message;
                        btnCancel.Enabled = true;
                        MessageBox.Show(this, "部署更新时出错: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }

        public static void RestartAndApplyUpdate(string rootDir, string targetContentDir)
        {
            try
            {
                string acadExe = "";
                try
                {
                    acadExe = Process.GetCurrentProcess().MainModule.FileName;
                }
                catch { }

                string batPath = Path.Combine(rootDir, "_apply_restart.bat");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("chcp 936 >nul 2>&1");
                sb.AppendLine("setlocal enabledelayedexpansion");
                sb.AppendLine("title XY工具箱 - 正在无感应用升级并重启CAD...");
                sb.AppendLine("cd /d \"" + rootDir + "\"");
                sb.AppendLine("");
                sb.AppendLine(":: 1. 等待原 AutoCAD 进程完全退出释放文件锁");
                sb.AppendLine("set /a wait_cnt=0");
                sb.AppendLine(":wait_loop");
                sb.AppendLine("timeout /t 1 /nobreak >nul");
                sb.AppendLine("tasklist /fi \"imagename eq acad.exe\" 2>nul | findstr /i \"acad.exe\" >nul");
                sb.AppendLine("if !errorlevel! equ 0 (");
                sb.AppendLine("    set /a wait_cnt+=1");
                sb.AppendLine("    if !wait_cnt! geq 3 (");
                sb.AppendLine("        taskkill /f /im acad.exe >nul 2>&1");
                sb.AppendLine("    )");
                sb.AppendLine("    if !wait_cnt! leq 10 goto wait_loop");
                sb.AppendLine(")");
                sb.AppendLine("timeout /t 1 /nobreak >nul");
                sb.AppendLine("");
                sb.AppendLine(":: 2. 彻底清理旧 cache 幽灵目录");
                sb.AppendLine("if exist \"cache\" rd /s /q \"cache\" >nul 2>&1");
                sb.AppendLine("");
                sb.AppendLine(":: 3. 从解压目录或 pending_update 将全套文件物理覆盖至工具箱主目录");
                if (!string.IsNullOrEmpty(targetContentDir) && Directory.Exists(targetContentDir))
                {
                    sb.AppendLine("if exist \"" + targetContentDir + "\" (");
                    sb.AppendLine("    xcopy /y /e /q \"" + targetContentDir + "\\*.*\" \".\" >nul 2>&1");
                    sb.AppendLine(")");
                }
                sb.AppendLine("if exist \"pending_update\" (");
                sb.AppendLine("    xcopy /y /e /q \"pending_update\\*.*\" \".\" >nul 2>&1");
                sb.AppendLine("    rd /s /q \"pending_update\" >nul 2>&1");
                sb.AppendLine(")");
                sb.AppendLine("");
                sb.AppendLine(":: 4. 确保母体 XY_Toolbox.dll 与最新代号 DLL 100% 同步覆盖");
                sb.AppendLine("for %%f in (XY_Toolbox_*.dll) do (");
                sb.AppendLine("    copy /y \"%%f\" \"XY_Toolbox.dll\" >nul 2>&1");
                sb.AppendLine(")");
                sb.AppendLine("");
                sb.AppendLine(":: 5. 清理历史辅助批处理");
                sb.AppendLine("if exist \"apply_update.bat\" del /f /q \"apply_update.bat\" >nul 2>&1");
                sb.AppendLine("if exist \"run_updater.bat\" del /f /q \"run_updater.bat\" >nul 2>&1");
                sb.AppendLine("if exist \"restart_apply.bat\" del /f /q \"restart_apply.bat\" >nul 2>&1");
                sb.AppendLine("");
                sb.AppendLine(":: 6. 重新启动 AutoCAD");
                if (!string.IsNullOrEmpty(acadExe))
                {
                    sb.AppendLine("if exist \"" + acadExe + "\" (");
                    sb.AppendLine("    start \"\" \"" + acadExe + "\"");
                    sb.AppendLine("    goto cleanup");
                    sb.AppendLine(")");
                }
                sb.AppendLine("start \"\" acad.exe");
                sb.AppendLine("");
                sb.AppendLine(":cleanup");
                sb.AppendLine("timeout /t 1 /nobreak >nul");
                sb.AppendLine("(goto) 2>nul & del \"%~f0\"");

                File.WriteAllText(batPath, sb.ToString(), Encoding.GetEncoding(936));

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = "/c \"" + batPath + "\"";
                psi.WorkingDirectory = rootDir;
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);

                // 优雅退出当前 CAD
                try
                {
                    Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.SendStringToExecute("_.QUIT ", true, false, false);
                }
                catch
                {
                    try { Autodesk.AutoCAD.ApplicationServices.Application.Quit(); } catch { }
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// 命令挂接类
    /// </summary>
    public class UpdateCommands
    {
        [CommandMethod("XGX")]
        [CommandMethod("UPDATE")]
        [CommandMethod("XY_UPDATE")]
        public static void CmdCheckUpdate()
        {
            UpdateService.CheckForUpdates(silent: false);
        }
    }
}
