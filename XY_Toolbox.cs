using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Windows;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Font = System.Drawing.Font;
using System.Data;

[assembly: ExtensionApplication(typeof(XY_Deepening_Toolbox.PluginApp))]
[assembly: CommandClass(typeof(XY_Deepening_Toolbox.Commands))]

namespace XY_Deepening_Toolbox
{
    public class PluginApp : IExtensionApplication
    {
        private static bool _autoStarted = false;

        public void Initialize()
        {
            try
            {
                // 初始化输入法智能自动切换模块 (命令全自动纯英文，文字全自动中文，完美复刻老李作品)
                Commands.ImeAutoSwitchManager.Initialize();

                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    Editor ed = doc.Editor;
                    ed.WriteMessage("\n========================================================");
                    ed.WriteMessage("\n★ [XY工具箱 V1.0] 已成功装载！工作台已在侧边栏自动就绪。");
                    ed.WriteMessage("\n  1. 敲 XGJ  : 呼出 / 隐藏 XY工具箱侧边栏工作台");
                    ed.WriteMessage("\n  2. 敲 XHB  : 弹出图层合并器可视化面板 (HB2)");
                    ed.WriteMessage("\n  3. 敲 XJH  : 极速图层合并 (HB1 免弹窗点选合并，零黑屏)");
                    ed.WriteMessage("\n  4. 敲 IME  : 输入法智能切换 (画图全自动英文，文字全自动中文)");
                    ed.WriteMessage("\n  5. 敲 XKJ  : 自定义快捷键修改与即时热重载");
                    ed.WriteMessage("\n========================================================\n");
                }

                // 核心防重：确保在整个宿主进程中，绝对只允许自启动弹出一次工作台面板！
                if (!_autoStarted && AppDomain.CurrentDomain.GetData("XY_TOOLBOX_PANEL_AUTOSTARTED") == null)
                {
                    _autoStarted = true;
                    AppDomain.CurrentDomain.SetData("XY_TOOLBOX_PANEL_AUTOSTARTED", true);
                    Autodesk.AutoCAD.ApplicationServices.Application.Idle += OnCadIdle;
                }
            }
            catch { }
        }

        private void OnCadIdle(object sender, EventArgs e)
        {
            try
            {
                Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnCadIdle;
                Commands.ShowToolboxPalette();
            }
            catch { }
        }

        public void Terminate()
        {
            Commands.CleanupExistingPalettes();
        }
    }

    public class Commands
    {
        public static string FindToolboxFile(string fileName)
        {
            try
            {
                string asmDir = System.IO.Path.GetDirectoryName(typeof(Commands).Assembly.Location);
                string p = System.IO.Path.Combine(asmDir, fileName);
                if (System.IO.File.Exists(p)) return p;
            }
            catch { }
            try
            {
                string acadPath = Autodesk.AutoCAD.DatabaseServices.HostApplicationServices.Current.FindFile(fileName, null, Autodesk.AutoCAD.DatabaseServices.FindFileHint.Default);
                if (!string.IsNullOrEmpty(acadPath) && System.IO.File.Exists(acadPath)) return acadPath;
            }
            catch { }
            return fileName;
        }

        public static Bitmap GetInvertedWhiteLogo()
        {
            try
            {
                string asmDir = System.IO.Path.GetDirectoryName(typeof(Commands).Assembly.Location);
                string[] pngCandidates = new string[] {
                    System.IO.Path.Combine(asmDir, "app_logo_white.png"),
                    System.IO.Path.Combine(asmDir, "app_white_logo.png")
                };
                foreach (string p in pngCandidates)
                {
                    if (System.IO.File.Exists(p))
                    {
                        using (System.Drawing.Image img = System.Drawing.Image.FromFile(p))
                        {
                            return new Bitmap(img);
                        }
                    }
                }

                // Fallback: dynamically invert pixels from app_pure_transparent.ico
                string icoPath = GetAppIconPath();
                if (!string.IsNullOrEmpty(icoPath))
                {
                    using (Icon ico = new Icon(icoPath, 64, 64))
                    using (Bitmap raw = ico.ToBitmap())
                    {
                        Bitmap inv = new Bitmap(raw.Width, raw.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        for (int y = 0; y < raw.Height; y++)
                        {
                            for (int x = 0; x < raw.Width; x++)
                            {
                                Color c = raw.GetPixel(x, y);
                                if (c.A > 0)
                                {
                                    inv.SetPixel(x, y, Color.FromArgb(c.A, 255 - c.R, 255 - c.G, 255 - c.B));
                                }
                            }
                        }
                        return inv;
                    }
                }
            }
            catch { }
            return null;
        }

        public static string GetAppIconPath()
        {
            string p = FindToolboxFile("app_pure_transparent.ico");
            if (System.IO.File.Exists(p)) return p;
            return null;
        }

        private static PaletteSet _paletteSet = null;
        private static ToolboxControl _control = null;
        private const string PALETTE_GLOBAL_KEY = "XY_TOOLBOX_PALETTE_GLOBAL_KEY";

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        private static extern bool LockWindowUpdate(IntPtr hWndLock);

        [DllImport("accore.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "acedEvaluateLisp")]
        private static extern int acedEvaluateLisp(string lispExpression, out IntPtr result);

        private const uint WM_CLOSE = 0x0010;
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static void CleanupExistingPalettes()
        {
            try
            {
                if (_paletteSet != null)
                {
                    try { _paletteSet.Visible = false; } catch { }
                }
            }
            catch { }
        }

        public static void EnsureLspLoaded()
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    string lspPath = FindToolboxFile("autoload.lsp").Replace('\\', '/');
                    string cmd = string.Format("(progn (vl-load-com) (if (null c:fast_merge_quick) (if (findfile \"{0}\") (load \"{0}\") (if (findfile \"autoload.lsp\") (load (findfile \"autoload.lsp\"))))) (if apply_all_toolbox_shortcuts (apply_all_toolbox_shortcuts)) (princ)) ", lspPath);
                    doc.SendStringToExecute(cmd, true, false, false);
                }
            }
            catch { }
        }

        // 固定稳定 GUID，用于永久记忆用户的停靠位置、宽度、停靠边与折叠偏好
        private static readonly Guid ToolboxPaletteGuid = new Guid("B1C2D3E4-F5A6-4B7C-8D9E-0F1A2B3C4D5E");

        // 强制刷新并重建工作台面板
        [CommandMethod("XY_UPDATE_PANEL")]
        [CommandMethod("XYPANEL_V49")]
        [CommandMethod("XYREFRESH")]
        public static void ForceReloadPalette()
        {
            try
            {
                if (_paletteSet != null)
                {
                    if (!_paletteSet.Visible) _paletteSet.Visible = true;
                    return;
                }
                ShowToolboxPalette();
            }
            catch { }
        }

        // 呼出/隐藏 工作台面板 (XGJ / XYGJ / XYSZ / ZYSZ / XYPANEL)
        [CommandMethod("XGJ")]
        [CommandMethod("XYGJ")]
        [CommandMethod("XYSZ")]
        [CommandMethod("ZYSZ")]
        [CommandMethod("XYPANEL")]
        public static void ShowToolboxPalette()
        {
            try
            {
                EnsureLspLoaded();

                if (_paletteSet == null)
                {
                    _paletteSet = new PaletteSet("XY工具箱 V1.0", ToolboxPaletteGuid);
                    _paletteSet.Style = PaletteSetStyles.ShowAutoHideButton | 
                                        PaletteSetStyles.ShowCloseButton;
                    _paletteSet.MinimumSize = new Size(250, 420);
                    _paletteSet.Size = new Size(270, 560);
                    _paletteSet.DockEnabled = DockSides.Left | DockSides.Right;

                    try
                    {
                        string icoPath = GetAppIconPath();
                        if (!string.IsNullOrEmpty(icoPath))
                        {
                            _paletteSet.Icon = new Icon(icoPath);
                        }
                    }
                    catch { }

                    _control = new ToolboxControl();
                    _paletteSet.Add("XY工具箱", _control);
                }

                if (!_paletteSet.Visible)
                {
                    _paletteSet.Visible = true;
                }
                else
                {
                    try
                    {
                        if (_paletteSet.RolledUp)
                        {
                            _paletteSet.RolledUp = false;
                        }
                    }
                    catch { }
                }

                if (_control != null)
                {
                    _control.RefreshLayerList();
                }
        }
            catch (System.Exception ex)
            {
                Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog("呼出XY工具箱失败:\n" + ex.Message);
            }
        }

        // 快捷键设置窗口 (SETKEY / KJSZ / CFG)
        [CommandMethod("XKJ")]
        [CommandMethod("SETKEY")]
        [CommandMethod("KJSZ")]
        [CommandMethod("CFG")]
        public static void ShowShortcutConfigDialog()
        {
            EnsureLspLoaded();
            try
            {
                using (ShortcutConfigDialog dlg = new ShortcutConfigDialog())
                {
                    dlg.StartPosition = FormStartPosition.CenterScreen;
                    Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dlg);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("打开快捷键面板失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 图层合并独立可视化弹窗 (HB2 / HBDLG / XHB / LYRMERGE)
        [CommandMethod("XHB")]
        [CommandMethod("HB2")]
        [CommandMethod("HBDLG")]
        public static void ShowLayerMergeDialog()
        {
            EnsureLspLoaded();
            try
            {
                using (LayerMergeDialog dlg = new LayerMergeDialog())
                {
                    dlg.StartPosition = FormStartPosition.CenterScreen;
                    Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dlg);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("打开图层合并面板失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [LispFunction("xy_show_layermerge")]
        public static object LispShowLayerMergeDialog(ResultBuffer args)
        {
            ShowLayerMergeDialog();
            return null;
        }

        // 图层深度对比与智能合并系统 (LYCOMPARE / TCH_COMPARE / XYCOMPARE / LYC)
        [CommandMethod("XYCOMPARE")]
        [CommandMethod("LYC")]
        [CommandMethod("LYCOMPARE")]
        [CommandMethod("TCH_COMPARE")]
        public static void ShowLayerCompareDialog()
        {
            EnsureLspLoaded();
            try
            {
                LayerCompareForm form = new LayerCompareForm();
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModelessDialog(form);
            }
            catch (System.Exception ex)
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null && doc.Editor != null)
                {
                    doc.Editor.WriteMessage("\n[错误] LYCOMPARE 呼出失败: " + ex.Message);
                }
            }
        }

        // 一键极速安全移除未使用空图层 (XQTC / PURGEUNUSED)
        [CommandMethod("XQTC")]
        [CommandMethod("PURGEUNUSED")]
        public static void PurgeUnusedLayersCommand()
        {
            EnsureLspLoaded();
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                Editor ed = doc.Editor;
                PurgeReport report = LayerPurgeService.PurgeUnusedLayers(doc.Database);
                if (ed != null)
                {
                    ed.WriteMessage("\n" + report.SummaryMessage);
                }
            }
            catch (System.Exception ex)
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null && doc.Editor != null)
                {
                    doc.Editor.WriteMessage("\n[错误] XQTC 清理失败: " + ex.Message);
                }
            }
        }

        // ========================================================
        // 核心热重载命令 (XCZ / RELOAD / XYLOAD)
        // 纯内存级自适应重载最新程序集，并重新载入 LISP 配置，绝不弹窗打扰
        // ========================================================
        [CommandMethod("XCZ")]
        [CommandMethod("RELOAD")]
        [CommandMethod("XYLOAD")]
        public static void ReloadAppCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                // 1. 刷新 WPF / WinForms 侧边栏面板
                ForceReloadPalette();

                // 2. 重新加载最新 LISP 配置
                string appDir = System.IO.Path.GetDirectoryName(typeof(Commands).Assembly.Location);
                string autoloadFile = System.IO.Path.Combine(appDir, "autoload.lsp").Replace("\\", "/");
                string eraseFile = System.IO.Path.Combine(appDir, "xy_block_erase.lsp").Replace("\\", "/");

                string cmdStr = string.Format("(progn (if (findfile \"{0}\") (load \"{0}\")) (if (findfile \"{1}\") (load \"{1}\")) (princ))\n",
                    autoloadFile, eraseFile);

                doc.SendStringToExecute(cmdStr, false, false, false);

                ed.WriteMessage("\n========================================================");
                ed.WriteMessage("\n★ [XY工具箱] 程序热重载成功 (XCZ)！");
                ed.WriteMessage("\n★ 核心 .NET 内核与 LISP 快捷键已全部刷新为最新版本");
                ed.WriteMessage("\n★ 免炸开穿透删除 (KSC/XSC) 与图层合并器 (XHB/HB1) 已就绪");
                ed.WriteMessage("\n========================================================\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n[XY工具箱] 重载发生异常: {0}\n", ex.Message));
            }
        }

        // ========================================================
        // 极速图层合并 (免弹窗纯命令行点选: HB1 / XJH / FASTMERGE)
        // 纯内存直接事务操作，0.005秒完成，彻底告别 CTAB/空间切换、绝无 Regen 黑屏！
        // ========================================================
        private static string _lastQuickTargetLayer = null;

        [CommandMethod("XJH")]
        [CommandMethod("HB1")]
        [CommandMethod("FASTMERGE")]
        public static void FastLayerMergeQuickCmd()
        {
            EnsureLspLoaded();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            string srcLayer = null;
            string tarLayer = null;

            // 1. 点选源图层图元 (支持穿透视口和块嵌套)
            PromptNestedEntityOptions opt1 = new PromptNestedEntityOptions("\n>> 请在图面上点选【源图层】图元 (按ESC退出): ");
            opt1.AllowNone = false;
            PromptNestedEntityResult res1 = ed.GetNestedEntity(opt1);
            if (res1.Status != PromptStatus.OK) return;

            using (doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Entity ent = tr.GetObject(res1.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        srcLayer = ent.Layer;
                        if (string.Equals(srcLayer, "0", StringComparison.OrdinalIgnoreCase))
                        {
                            ObjectId[] conts = res1.GetContainers();
                            if (conts != null && conts.Length > 0)
                            {
                                Entity pEnt = tr.GetObject(conts[0], OpenMode.ForRead) as Entity;
                                if (pEnt != null && !string.IsNullOrEmpty(pEnt.Layer))
                                    srcLayer = pEnt.Layer;
                            }
                        }
                    }
                    tr.Commit();
                }
            }

            if (string.IsNullOrEmpty(srcLayer))
            {
                ed.WriteMessage("\n[提示] 未能识别源图层。\n");
                return;
            }

            ed.WriteMessage(string.Format(" -> 源图层: [{0}]", srcLayer));

            // 2. 点选目标图层图元
            string lastTar = _lastQuickTargetLayer;
            string prompt2 = string.IsNullOrEmpty(lastTar)
                ? "\n>> 请点选【目标图层】图元: "
                : string.Format("\n>> 请点选【目标图层】图元 (回车直接采用: [{0}]): ", lastTar);

            PromptNestedEntityOptions opt2 = new PromptNestedEntityOptions(prompt2);
            opt2.AllowNone = true;
            PromptNestedEntityResult res2 = ed.GetNestedEntity(opt2);

            if (res2.Status == PromptStatus.None)
            {
                if (!string.IsNullOrEmpty(lastTar))
                {
                    tarLayer = lastTar;
                }
                else return;
            }
            else if (res2.Status == PromptStatus.OK)
            {
                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        Entity ent = tr.GetObject(res2.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent != null)
                        {
                            tarLayer = ent.Layer;
                            if (string.Equals(tarLayer, "0", StringComparison.OrdinalIgnoreCase))
                            {
                                ObjectId[] conts = res2.GetContainers();
                                if (conts != null && conts.Length > 0)
                                {
                                    Entity pEnt = tr.GetObject(conts[0], OpenMode.ForRead) as Entity;
                                    if (pEnt != null && !string.IsNullOrEmpty(pEnt.Layer))
                                        tarLayer = pEnt.Layer;
                                }
                            }
                        }
                        tr.Commit();
                    }
                }
            }
            else
            {
                return;
            }

            if (string.IsNullOrEmpty(tarLayer))
            {
                ed.WriteMessage("\n[提示] 未能获取目标图层。\n");
                return;
            }

            if (string.Equals(srcLayer, tarLayer, StringComparison.OrdinalIgnoreCase))
            {
                ed.WriteMessage("\n[提示] 源图层与目标图层相同，无需合并。\n");
                return;
            }

            _lastQuickTargetLayer = tarLayer;

            // 3. 极速纯内存合并 (零空间切换，零黑屏)
            int entCount = 0;
            int vpCount = 0;
            bool ok = LayerMergeDialog.FastLayerMerge(srcLayer, tarLayer, true, true, out entCount, out vpCount);
            if (ok)
            {
                ed.WriteMessage(string.Format("\n★ [XY工具箱] 极速合并成功: [{0}] -> [{1}] (处理图元: {2}, 视口同步: {3})\n",
                    srcLayer, tarLayer, entCount, vpCount));
            }
            else
            {
                ed.WriteMessage("\n[提示] 合并执行未完成或图层不存在。\n");
            }
        }

        // ========================================================
        // 输入法智能自动切换控制 (IME / XSRF)
        // 完美复刻老李作品：画图快捷键全自动英文，文字编辑全自动中文！
        // ========================================================
        [CommandMethod("XSR")]
        [CommandMethod("XSRF")]
        [CommandMethod("IME")]
        public static void ToggleImeCommand()
        {
            ImeAutoSwitchManager.Enabled = !ImeAutoSwitchManager.Enabled;
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                if (ImeAutoSwitchManager.Enabled)
                {
                    doc.Editor.WriteMessage("\n★ [XY工具箱] 输入法智能自动切换已【开启】(绘图快捷键全自动英文，文本编辑全自动中文)\n");
                    ImeAutoSwitchManager.SetImeEnglish();
                }
                else
                {
                    doc.Editor.WriteMessage("\n★ [XY工具箱] 输入法智能自动切换已【关闭】(恢复系统默认手动切换)\n");
                }
            }
        }

        // ========================================================
        // 核心突破：原位编块·免进编辑静默穿透拉伸/移动 (XKB / XLS / XSTRETCH)
        // 颠覆传统 REFEDIT / BEDIT 模式：
        // 1. 彻底告别双击、弹窗、变暗变灰与 REFCLOSE 繁琐流程！
        // 2. 外部直接交叉框选(Crossing Window)：像普通未成块线条一样直接 S 拉伸或 M 移动
        // 3. 几何逆矩阵(Inverse Matrix)精准逆映射到图块内部坐标系，自动识别框内顶点
        // 4. 支持【仅改当前(S)】与【全部联动(A)】双模式，默认仅改当前，绝不误改全图其他同名块
        // 5. 穿透拉伸线段、多段线、圆弧、文字、标注、嵌套块等，毫秒级无感静默更新
        // ========================================================
        private static bool _stretchOnlyCurrentInstance = true; // 默认仅改当前选中图块

        [CommandMethod("XKB")]
        [CommandMethod("XLS")]
        [CommandMethod("XSTRETCH")]
        public static void FastBlockStretchCommand()
        {
            EnsureLspLoaded();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // 1. 获取交叉框选区域 (角点1 与 角点2)
            Point3d corner1;
            while (true)
            {
                string modeStr = _stretchOnlyCurrentInstance ? "仅改当前(S)" : "全局联动(A)";
                PromptPointOptions ppo1 = new PromptPointOptions(string.Format("\n>> [XY工具箱·穿透编块] 请框选要拉伸的块内图元 [模式切换: S仅改当前 / A全部联动] <当前: {0}>: ", modeStr));
                ppo1.Keywords.Add("S", "S", "仅改当前(S)");
                ppo1.Keywords.Add("A", "A", "全局联动(A)");
                ppo1.AllowNone = false;

                PromptPointResult ppr1 = ed.GetPoint(ppo1);
                if (ppr1.Status == PromptStatus.Keyword)
                {
                    if (ppr1.StringResult == "S")
                    {
                        _stretchOnlyCurrentInstance = true;
                        ed.WriteMessage("\n★ 已切换为【仅改当前】模式：仅修改所选图块，绝不影响图纸中其他同名图块。\n");
                    }
                    else if (ppr1.StringResult == "A")
                    {
                        _stretchOnlyCurrentInstance = false;
                        ed.WriteMessage("\n★ 已切换为【全局联动】模式：修改将同步更新图纸中所有同名图块。\n");
                    }
                    continue;
                }
                if (ppr1.Status != PromptStatus.OK) return;
                corner1 = ppr1.Value;
                break;
            }

            PromptCornerOptions pco = new PromptCornerOptions("\n>> 指定对角点 (拉框交叉框选需拉伸的线条/端点): ", corner1);
            pco.UseDashedLine = true;
            PromptPointResult ppr2 = ed.GetCorner(pco);
            if (ppr2.Status != PromptStatus.OK) return;
            Point3d corner2 = ppr2.Value;

            double minX = Math.Min(corner1.X, corner2.X);
            double maxX = Math.Max(corner1.X, corner2.X);
            double minY = Math.Min(corner1.Y, corner2.Y);
            double maxY = Math.Max(corner1.Y, corner2.Y);

            // 2. 选择框选区域内相交的图元 (Crossing Selection)
            PromptSelectionResult selRes = ed.SelectCrossingWindow(corner1, corner2);
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\n[提示] 未框选到任何图元。\n");
                return;
            }

            // 3. 用户指定位移基点与目标点 (带原生动态橡皮筋线)
            PromptPointOptions ppoBase = new PromptPointOptions("\n>> 指定拉伸基点: ");
            ppoBase.AllowNone = false;
            PromptPointResult pprBase = ed.GetPoint(ppoBase);
            if (pprBase.Status != PromptStatus.OK) return;
            Point3d basePt = pprBase.Value;

            PromptPointOptions ppoDest = new PromptPointOptions("\n>> 指定第二点 (可直接输入拉伸距离或捕捉目标点): ");
            ppoDest.UseBasePoint = true;
            ppoDest.BasePoint = basePt;
            PromptPointResult pprDest = ed.GetPoint(ppoDest);
            if (pprDest.Status != PromptStatus.OK) return;
            Point3d destPt = pprDest.Value;

            Vector3d wcsVector = destPt - basePt;
            if (wcsVector.Length < 1e-6)
            {
                ed.WriteMessage("\n[提示] 位移量为 0，操作已取消。\n");
                return;
            }

            // 4. 执行穿透拉伸核心逻辑
            int totalModified = 0;
            using (doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    HashSet<ObjectId> processedBlocks = new HashSet<ObjectId>();

                    foreach (SelectedObject so in selRes.Value)
                    {
                        if (so == null || so.ObjectId == ObjectId.Null) continue;
                        Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        BlockReference br = ent as BlockReference;
                        if (br != null)
                        {
                            // A. 穿透图块内部进行拉伸
                            ObjectId targetBtrId = br.BlockTableRecord;
                            if (processedBlocks.Contains(br.ObjectId)) continue;
                            processedBlocks.Add(br.ObjectId);

                            // 若处于“仅改当前”模式且该块在图纸中存在多个实例，则静默克隆为独立块定义
                            if (_stretchOnlyCurrentInstance)
                            {
                                targetBtrId = EnsureUniqueBlockDefinition(tr, db, bt, br);
                            }

                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(targetBtrId, OpenMode.ForWrite);
                            int modInBlock = StretchEntitiesInBlock(tr, btr, br.BlockTransform, minX, maxX, minY, maxY, wcsVector);
                            if (modInBlock > 0)
                            {
                                br.UpgradeOpen();
                                br.RecordGraphicsModified(true);
                                totalModified += modInBlock;
                            }
                        }
                        else
                        {
                            // B. 普通非块图元同步拉伸 (实现框选无感混编)
                            int modEnt = StretchOrdinaryEntity(tr, ent, minX, maxX, minY, maxY, wcsVector);
                            totalModified += modEnt;
                        }
                    }

                    tr.Commit();
                }
            }

            doc.Editor.UpdateScreen();
            ed.WriteMessage(string.Format("\n★ [XY工具箱] 原位编块穿透拉伸完成！共精准调整 {0} 个图元顶点 (位移: X={1:0.##}, Y={2:0.##})\n",
                totalModified, wcsVector.X, wcsVector.Y));
        }

        private static bool IsPointIn2DBox(Point3d pt, double minX, double maxX, double minY, double maxY, double tol = 1e-4)
        {
            return pt.X >= minX - tol && pt.X <= maxX + tol &&
                   pt.Y >= minY - tol && pt.Y <= maxY + tol;
        }

        private static ObjectId EnsureUniqueBlockDefinition(Transaction tr, Database db, BlockTable bt, BlockReference br)
        {
            try
            {
                ObjectId originalBtrId = br.BlockTableRecord;
                BlockTableRecord origBtr = (BlockTableRecord)tr.GetObject(originalBtrId, OpenMode.ForRead);

                if (origBtr.IsLayout || origBtr.IsAnonymous) return originalBtrId;

                ObjectIdCollection refs = origBtr.GetBlockReferenceIds(true, true);
                if (refs.Count <= 1)
                {
                    return originalBtrId;
                }

                string baseName = origBtr.Name;
                int idx = 1;
                string newName;
                while (true)
                {
                    newName = string.Format("{0}_X{1}", baseName, idx);
                    if (!bt.Has(newName)) break;
                    idx++;
                }

                bt.UpgradeOpen();
                BlockTableRecord newBtr = (BlockTableRecord)origBtr.Clone();
                newBtr.Name = newName;
                ObjectId newBtrId = bt.Add(newBtr);
                tr.AddNewlyCreatedDBObject(newBtr, true);

                br.UpgradeOpen();
                br.BlockTableRecord = newBtrId;
                return newBtrId;
            }
            catch
            {
                return br.BlockTableRecord;
            }
        }

        private static int StretchEntitiesInBlock(Transaction tr, BlockTableRecord btr, Matrix3d blkTransform, double minX, double maxX, double minY, double maxY, Vector3d wcsVector)
        {
            int count = 0;
            try
            {
                Matrix3d invMat = blkTransform.Inverse();
                Vector3d blkVector = (Point3d.Origin + wcsVector).TransformBy(invMat) - Point3d.Origin.TransformBy(invMat);

                foreach (ObjectId entId in btr)
                {
                    Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.IsErased) continue;

                    // 1. 直线 (Line)
                    Line line = ent as Line;
                    if (line != null)
                    {
                        Point3d p1Wcs = line.StartPoint.TransformBy(blkTransform);
                        Point3d p2Wcs = line.EndPoint.TransformBy(blkTransform);
                        bool in1 = IsPointIn2DBox(p1Wcs, minX, maxX, minY, maxY);
                        bool in2 = IsPointIn2DBox(p2Wcs, minX, maxX, minY, maxY);

                        if (in1 && in2)
                        {
                            line.UpgradeOpen();
                            line.StartPoint += blkVector;
                            line.EndPoint += blkVector;
                            line.RecordGraphicsModified(true);
                            count++;
                        }
                        else if (in1 && !in2)
                        {
                            line.UpgradeOpen();
                            line.StartPoint += blkVector;
                            line.RecordGraphicsModified(true);
                            count++;
                        }
                        else if (!in1 && in2)
                        {
                            line.UpgradeOpen();
                            line.EndPoint += blkVector;
                            line.RecordGraphicsModified(true);
                            count++;
                        }
                        continue;
                    }

                    // 2. 轻量多段线 (Polyline / LWPolyline)
                    Autodesk.AutoCAD.DatabaseServices.Polyline pl = ent as Autodesk.AutoCAD.DatabaseServices.Polyline;
                    if (pl != null)
                    {
                        bool modified = false;
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            Point2d v2d = pl.GetPoint2dAt(i);
                            Point3d vWcs = (new Point3d(v2d.X, v2d.Y, pl.Elevation)).TransformBy(blkTransform);
                            if (IsPointIn2DBox(vWcs, minX, maxX, minY, maxY))
                            {
                                if (!modified) { pl.UpgradeOpen(); modified = true; }
                                Point2d newV2d = new Point2d(v2d.X + blkVector.X, v2d.Y + blkVector.Y);
                                pl.SetPointAt(i, newV2d);
                                count++;
                            }
                        }
                        if (modified) pl.RecordGraphicsModified(true);
                        continue;
                    }

                    // 3. 圆弧 (Arc)
                    Arc arc = ent as Arc;
                    if (arc != null)
                    {
                        Point3d centerWcs = arc.Center.TransformBy(blkTransform);
                        Point3d startWcs = arc.StartPoint.TransformBy(blkTransform);
                        Point3d endWcs = arc.EndPoint.TransformBy(blkTransform);
                        if (IsPointIn2DBox(centerWcs, minX, maxX, minY, maxY) &&
                            IsPointIn2DBox(startWcs, minX, maxX, minY, maxY) &&
                            IsPointIn2DBox(endWcs, minX, maxX, minY, maxY))
                        {
                            arc.UpgradeOpen();
                            arc.Center += blkVector;
                            arc.RecordGraphicsModified(true);
                            count++;
                        }
                        continue;
                    }

                    // 4. 圆 (Circle)
                    Circle circle = ent as Circle;
                    if (circle != null)
                    {
                        Point3d cWcs = circle.Center.TransformBy(blkTransform);
                        if (IsPointIn2DBox(cWcs, minX, maxX, minY, maxY))
                        {
                            circle.UpgradeOpen();
                            circle.Center += blkVector;
                            circle.RecordGraphicsModified(true);
                            count++;
                        }
                        continue;
                    }

                    // 5. 文字 (DBText / MText)
                    DBText txt = ent as DBText;
                    if (txt != null)
                    {
                        Point3d posWcs = txt.Position.TransformBy(blkTransform);
                        if (IsPointIn2DBox(posWcs, minX, maxX, minY, maxY))
                        {
                            txt.UpgradeOpen();
                            txt.Position += blkVector;
                            if (txt.HorizontalMode != TextHorizontalMode.TextLeft || txt.VerticalMode != TextVerticalMode.TextBase)
                            {
                                txt.AlignmentPoint += blkVector;
                            }
                            txt.RecordGraphicsModified(true);
                            count++;
                        }
                        continue;
                    }
                    MText mtxt = ent as MText;
                    if (mtxt != null)
                    {
                        Point3d locWcs = mtxt.Location.TransformBy(blkTransform);
                        if (IsPointIn2DBox(locWcs, minX, maxX, minY, maxY))
                        {
                            mtxt.UpgradeOpen();
                            mtxt.Location += blkVector;
                            mtxt.RecordGraphicsModified(true);
                            count++;
                        }
                        continue;
                    }

                    // 6. 尺寸标注 (Dimension)
                    Dimension dim = ent as Dimension;
                    if (dim != null)
                    {
                        dim.UpgradeOpen();
                        dim.TransformBy(Matrix3d.Displacement(blkVector));
                        dim.RecordGraphicsModified(true);
                        count++;
                        continue;
                    }

                    // 7. 嵌套子块 (BlockReference)
                    BlockReference subBr = ent as BlockReference;
                    if (subBr != null)
                    {
                        Point3d posWcs = subBr.Position.TransformBy(blkTransform);
                        if (IsPointIn2DBox(posWcs, minX, maxX, minY, maxY))
                        {
                            subBr.UpgradeOpen();
                            subBr.Position += blkVector;
                            subBr.RecordGraphicsModified(true);
                            count++;
                        }
                        continue;
                    }
                }
            }
            catch { }
            return count;
        }

        private static int StretchOrdinaryEntity(Transaction tr, Entity ent, double minX, double maxX, double minY, double maxY, Vector3d wcsVector)
        {
            int count = 0;
            try
            {
                Line line = ent as Line;
                if (line != null)
                {
                    bool in1 = IsPointIn2DBox(line.StartPoint, minX, maxX, minY, maxY);
                    bool in2 = IsPointIn2DBox(line.EndPoint, minX, maxX, minY, maxY);
                    if (in1 && in2)
                    {
                        line.UpgradeOpen();
                        line.StartPoint += wcsVector;
                        line.EndPoint += wcsVector;
                        line.RecordGraphicsModified(true);
                        count++;
                    }
                    else if (in1 && !in2)
                    {
                        line.UpgradeOpen();
                        line.StartPoint += wcsVector;
                        line.RecordGraphicsModified(true);
                        count++;
                    }
                    else if (!in1 && in2)
                    {
                        line.UpgradeOpen();
                        line.EndPoint += wcsVector;
                        line.RecordGraphicsModified(true);
                        count++;
                    }
                    return count;
                }

                Autodesk.AutoCAD.DatabaseServices.Polyline pl = ent as Autodesk.AutoCAD.DatabaseServices.Polyline;
                if (pl != null)
                {
                    bool modified = false;
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        Point2d v2d = pl.GetPoint2dAt(i);
                        Point3d pt3d = new Point3d(v2d.X, v2d.Y, pl.Elevation);
                        if (IsPointIn2DBox(pt3d, minX, maxX, minY, maxY))
                        {
                            if (!modified) { pl.UpgradeOpen(); modified = true; }
                            pl.SetPointAt(i, new Point2d(v2d.X + wcsVector.X, v2d.Y + wcsVector.Y));
                            count++;
                        }
                    }
                    if (modified) pl.RecordGraphicsModified(true);
                    return count;
                }

                if (ent.Bounds.HasValue)
                {
                    Extents3d ext = ent.Bounds.Value;
                    if (IsPointIn2DBox(ext.MinPoint, minX, maxX, minY, maxY) && IsPointIn2DBox(ext.MaxPoint, minX, maxX, minY, maxY))
                    {
                        ent.UpgradeOpen();
                        ent.TransformBy(Matrix3d.Displacement(wcsVector));
                        ent.RecordGraphicsModified(true);
                        count++;
                    }
                }
            }
            catch { }
            return count;
        }


        // ========================================================
        // 图块镜像反字智能穿透纠偏 (FZW / XFZ / CORRECTTEXT)
        // 核心技术特性：
        // 1. 负负得正几何补偿：穿透检测被镜像（MI）图块内部的属性字/普通字，智能施加反向镜像标志
        // 2. 几何中心严格锁定：翻转前后自动计算世界坐标系（WCS）几何包围盒中心，反向补偿位移向量，文字原地翻转绝不位移
        // 3. 智能分支克隆保护：当图块内普通文字被修改时，若图纸存在其他正常引用的块，自动创建独立克隆分支，绝不破坏正常图块
        // 4. 大面积极速批量框选：支持框选成百上千个图块与独立文字，秒级自动识别反字并纠偏，正常图块自动跳过
        // ========================================================
        [CommandMethod("FZW")]
        [CommandMethod("XFZ")]
        [CommandMethod("CORRECTTEXT")]
        [CommandMethod("XY_FIXTEXT")]
        public static void CorrectMirroredBlockText()
        {
            EnsureLspLoaded();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptSelectionOptions pso = new PromptSelectionOptions();
            pso.MessageForAdding = "\n>> 请框选或点选需要翻正文字的图块或文字 (支持大面积框选，按ESC或回车结束): ";
            pso.AllowDuplicates = false;

            TypedValue[] tvs = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "INSERT"),
                new TypedValue((int)DxfCode.Start, "TEXT"),
                new TypedValue((int)DxfCode.Start, "MTEXT"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            };
            SelectionFilter filter = new SelectionFilter(tvs);

            PromptSelectionResult psr = ed.GetSelection(pso, filter);
            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
            {
                return;
            }

            int totalScanned = 0;
            int attrFixed = 0;
            int blockDefFixed = 0;
            int plainFixed = 0;
            int normalSkipped = 0;

            using (doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;

                    foreach (SelectedObject so in psr.Value)
                    {
                        if (so == null || so.ObjectId.IsNull) continue;
                        totalScanned++;

                        DBObject obj = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                        BlockReference blkRef = obj as BlockReference;
                        if (blkRef != null)
                        {
                            Matrix3d tf = blkRef.BlockTransform;
                            CoordinateSystem3d cs = tf.CoordinateSystem3d;
                            double det = cs.Xaxis.CrossProduct(cs.Yaxis).DotProduct(cs.Zaxis);
                            bool isBlockMirrored = (blkRef.ScaleFactors.X * blkRef.ScaleFactors.Y < -1e-6) || (det < -1e-6);

                            // 优先获取动态块真正的母块定义 (如果是动态块，其真正的属性定义在 DynamicBlockTableRecord 中)
                            ObjectId btrId = blkRef.IsDynamicBlock ? blkRef.DynamicBlockTableRecord : blkRef.BlockTableRecord;
                            BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;

                            // 1. 处理属性文字：支持任意多次镜像后的动态翻正与设计位置还原
                            if (blkRef.AttributeCollection != null && blkRef.AttributeCollection.Count > 0)
                            {
                                foreach (ObjectId attId in blkRef.AttributeCollection)
                                {
                                    AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                                    if (attRef != null)
                                    {
                                        bool ok = RestoreAttributeTextToPreMirror(blkRef, attRef, btr, tr, db);
                                        if (ok) attrFixed++;
                                    }
                                }
                            }

                            // 2. 检查普通块定义内部文字 (支持非属性块的镜像反字)
                            bool btrFixed = FixBlockDefinitionTextInternal(blkRef, tr, db, bt);
                            if (btrFixed)
                            {
                                blockDefFixed++;
                            }
                        }
                        else
                        {
                            // 3. 散放普通文字：原地消除镜像
                            DBText txt = obj as DBText;
                            if (txt != null)
                            {
                                if (txt.IsMirroredInX ^ txt.IsMirroredInY)
                                {
                                    txt.UpgradeOpen();
                                    txt.IsMirroredInX = !txt.IsMirroredInX;
                                    txt.AdjustAlignment(db);
                                    plainFixed++;
                                }
                                else
                                {
                                    normalSkipped++;
                                }
                            }
                        }
                    }

                    tr.Commit();
                }
            }

            int totalFixed = attrFixed + blockDefFixed + plainFixed;
            ed.WriteMessage(string.Format(
                "\n=======================================================\n" +
                "[XY工具箱] 翻正处理完毕 (FZW)！\n" +
                "  • 扫描图元: {0} 个\n" +
                "  • 成功翻正/复位: {1} 处 (属性文字: {2}, 块内普通文字: {3}, 独立文字: {4})\n" +
                "=======================================================\n",
                totalScanned, totalFixed, attrFixed, blockDefFixed, plainFixed));
        }

        private static bool RestoreAttributeTextToPreMirror(BlockReference blkRef, AttributeReference attRef, BlockTableRecord btr, Transaction tr, Database db)
        {
            AttributeDefinition attDef = null;
            if (btr != null)
            {
                foreach (ObjectId id in btr)
                {
                    if (id.ObjectClass.DxfName.Equals("ATTDEF", StringComparison.OrdinalIgnoreCase))
                    {
                        AttributeDefinition def = tr.GetObject(id, OpenMode.ForRead) as AttributeDefinition;
                        if (def != null && string.Equals(def.Tag, attRef.Tag, StringComparison.OrdinalIgnoreCase))
                        {
                            attDef = def;
                            break;
                        }
                    }
                }
            }

            Matrix3d tf = blkRef.BlockTransform;
            CoordinateSystem3d cs = tf.CoordinateSystem3d;
            double det = cs.Xaxis.CrossProduct(cs.Yaxis).DotProduct(cs.Zaxis);
            bool isBlockMirrored = (blkRef.ScaleFactors.X * blkRef.ScaleFactors.Y < -1e-6) || (det < -1e-6);

            attRef.UpgradeOpen();

            if (attDef != null)
            {
                // 计算当前图块正向基准方向向量与绝对缩放
                double scaleX = Math.Abs(blkRef.ScaleFactors.X);
                if (scaleX < 1e-6) scaleX = 1.0;
                double scaleY = Math.Abs(blkRef.ScaleFactors.Y);
                if (scaleY < 1e-6) scaleY = 1.0;

                double rot = blkRef.Rotation;
                Vector3d ux = new Vector3d(Math.Cos(rot), Math.Sin(rot), 0);
                Vector3d uy = new Vector3d(-Math.Sin(rot), Math.Cos(rot), 0);

                // 根据原始母块定义中 attDef 的相对设计坐标，计算当前图块标准理想位置
                Vector3d relPos = attDef.Position - btr.Origin;
                Point3d targetPos = blkRef.Position + (relPos.X * scaleX * ux) + (relPos.Y * scaleY * uy);

                Vector3d relAlign = attDef.AlignmentPoint - btr.Origin;
                Point3d targetAlign = blkRef.Position + (relAlign.X * scaleX * ux) + (relAlign.Y * scaleY * uy);

                double targetRotation = rot + attDef.Rotation;

                // 核心手性平衡 (支持无限次再次镜像)：
                // 若图块当前为奇数次镜像 (det < 0)，文字自身必须设为 IsMirroredInX = true (负负得正，呈现正字)；
                // 若图块当前为偶数次镜像 (det > 0)，文字自身设为 IsMirroredInX = false (正正得正，呈现正字)！
                attRef.IsMirroredInX = isBlockMirrored;
                attRef.IsMirroredInY = false;
                attRef.Position = targetPos;
                if (attRef.Justify != AttachmentPoint.BaseLeft)
                {
                    attRef.AlignmentPoint = targetAlign;
                }
                attRef.Rotation = targetRotation;
                attRef.AdjustAlignment(db);
                return true;
            }
            else
            {
                // 无对应 ATTDEF 模板时，根据图块手性做翻转
                attRef.IsMirroredInX = isBlockMirrored ? !attRef.IsMirroredInX : false;
                attRef.AdjustAlignment(db);
                return true;
            }
        }

        private static bool FixBlockDefinitionTextInternal(BlockReference blkRef, Transaction tr, Database db, BlockTable bt)
        {
            BlockTableRecord btr = tr.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null || btr.IsLayout || btr.IsAnonymous) return false;

            List<ObjectId> textIds = new List<ObjectId>();
            foreach (ObjectId id in btr)
            {
                if (id.ObjectClass.DxfName.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                {
                    DBText t = tr.GetObject(id, OpenMode.ForRead) as DBText;
                    if (t != null)
                    {
                        bool isVisuallyMirrored = true ^ t.IsMirroredInX ^ t.IsMirroredInY;
                        if (isVisuallyMirrored)
                        {
                            textIds.Add(id);
                        }
                    }
                }
            }

            if (textIds.Count == 0) return false;

            ObjectIdCollection refIds = btr.GetBlockReferenceIds(true, false);
            if (refIds.Count <= 1)
            {
                btr.UpgradeOpen();
                foreach (ObjectId tid in textIds)
                {
                    DBText t = tr.GetObject(tid, OpenMode.ForWrite) as DBText;
                    if (t != null)
                    {
                        Point3d p = t.Position;
                        t.Position = new Point3d(-p.X, p.Y, p.Z);
                        if (t.Justify != AttachmentPoint.BaseLeft)
                        {
                            Point3d al = t.AlignmentPoint;
                            t.AlignmentPoint = new Point3d(-al.X, al.Y, al.Z);
                        }
                        t.IsMirroredInX = true;
                        t.AdjustAlignment(db);
                    }
                }
                return true;
            }
            else
            {
                string newBlockName = GetUniqueBlockName(bt, btr.Name + "_FZ");
                BlockTableRecord newBtr = new BlockTableRecord();
                newBtr.Name = newBlockName;
                newBtr.Origin = btr.Origin;

                bt.UpgradeOpen();
                ObjectId newBtrId = bt.Add(newBtr);
                tr.AddNewlyCreatedDBObject(newBtr, true);

                foreach (ObjectId id in btr)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    Entity clonedEnt = ent.Clone() as Entity;
                    DBText txt = clonedEnt as DBText;
                    if (txt != null && textIds.Contains(id))
                    {
                        Point3d p = txt.Position;
                        txt.Position = new Point3d(-p.X, p.Y, p.Z);
                        if (txt.Justify != AttachmentPoint.BaseLeft)
                        {
                            Point3d al = txt.AlignmentPoint;
                            txt.AlignmentPoint = new Point3d(-al.X, al.Y, al.Z);
                        }
                        txt.IsMirroredInX = true;
                        txt.AdjustAlignment(db);
                    }

                    newBtr.AppendEntity(clonedEnt);
                    tr.AddNewlyCreatedDBObject(clonedEnt, true);
                }

                blkRef.UpgradeOpen();
                blkRef.BlockTableRecord = newBtrId;
                return true;
            }
        }

        private static string GetUniqueBlockName(BlockTable bt, string baseName)
        {
            if (!bt.Has(baseName)) return baseName;
            int idx = 1;
            while (bt.Has(baseName + "_" + idx))
            {
                idx++;
            }
            return baseName + "_" + idx;
        }

        private static Point3d GetEntityCenterSafe(Entity ent)
        {
            try
            {
                Extents3d ext = ent.GeometricExtents;
                return new Point3d(
                    (ext.MinPoint.X + ext.MaxPoint.X) * 0.5,
                    (ext.MinPoint.Y + ext.MaxPoint.Y) * 0.5,
                    (ext.MinPoint.Z + ext.MaxPoint.Z) * 0.5);
            }
            catch
            {
                DBText txt = ent as DBText;
                if (txt != null)
                {
                    if (txt.Justify != AttachmentPoint.BaseLeft)
                        return txt.AlignmentPoint;
                    return txt.Position;
                }
                return Point3d.Origin;
            }
        }

        // ========================================================
        // 属性块单字穿透动态平移 (MW / MY / MOVETEXT)
        // 核心亮点：
        // 1. 同名块全部同步更新：修改一处，全图（模型+所有布局空间）同名图块全部联动平移
        // 2. 块定义与实例双向数学映射：彻底杜绝视口比例缩放与 REGEN 位置还原 BUG
        // 3. DrawJig 实时平滑拖拽预览 + 键盘精确数值输入
        // 4. 单字精准穿透：同一图块内多行文字（如标题、比例、图号）完全独立分别平移
        // ========================================================
        [CommandMethod("XSY")]
        [CommandMethod("MW")]
        [CommandMethod("MY")]
        [CommandMethod("XY_MOVETEXT")]
        [CommandMethod("MOVETEXT")]
        public static void MoveSingleAttributeText()
        {
            EnsureLspLoaded();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            while (true)
            {
                PromptNestedEntityOptions opt = new PromptNestedEntityOptions("\n>> 请点选要平移的属性文字 (同名图块将全部同步更新，按ESC或回车退出): ");
                PromptNestedEntityResult res = ed.GetNestedEntity(opt);
                if (res.Status != PromptStatus.OK) break;

                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        DBObject obj = tr.GetObject(res.ObjectId, OpenMode.ForRead);
                        AttributeReference attRef = obj as AttributeReference;
                        if (attRef == null)
                        {
                            ed.WriteMessage("\n[提示] 所选图元不是属性文字(AttributeReference)，请重新直接点击图块内的文字。\n");
                            tr.Abort();
                            continue;
                        }

                        // 获取所属母块引用
                        BlockReference parentBlk = tr.GetObject(attRef.OwnerId, OpenMode.ForRead) as BlockReference;
                        if (parentBlk == null)
                        {
                            ObjectId[] containers = res.GetContainers();
                            if (containers != null && containers.Length > 0)
                            {
                                parentBlk = tr.GetObject(containers[0], OpenMode.ForRead) as BlockReference;
                            }
                        }

                        if (parentBlk == null)
                        {
                            ed.WriteMessage("\n[提示] 未能获取该文字所在的图块参照。\n");
                            tr.Abort();
                            continue;
                        }

                        string targetTag = attRef.Tag;
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(parentBlk.BlockTableRecord, OpenMode.ForWrite);
                        string blockName = btr.Name;

                        ed.WriteMessage(string.Format("\n>> 选中属性: 【{0}】 当前内容: \"{1}\" (图块: 【{2}】)", targetTag, attRef.TextString, blockName));

                        // 1. 动态拖拽拾取基点
                        Point3d basePt = res.PickedPoint;

                        // 2. 启动原生 DrawJig 实时拖拽
                        AttributeTextMoveJig jig = new AttributeTextMoveJig(basePt, attRef);
                        PromptResult dragRes = ed.Drag(jig);

                        if (dragRes.Status != PromptStatus.OK)
                        {
                            tr.Abort();
                            continue;
                        }

                        Vector3d wcsDisp = jig.DisplacementVector;
                        if (wcsDisp.IsZeroLength())
                        {
                            tr.Abort();
                            continue;
                        }

                        // 3. 将 WCS 位移逆变换为母块定义内部的局部坐标系位移 (localDisp)
                        // 彻底解决布局视口与模型空间比例不一致、刷新还原的世纪难题！
                        Matrix3d invMat = parentBlk.BlockTransform.Inverse();
                        Vector3d localDisp = wcsDisp.TransformBy(invMat);

                        // 4. 同步更新块定义内部的 AttributeDefinition
                        foreach (ObjectId entId in btr)
                        {
                            AttributeDefinition attDef = tr.GetObject(entId, OpenMode.ForRead) as AttributeDefinition;
                            if (attDef != null && string.Equals(attDef.Tag, targetTag, StringComparison.OrdinalIgnoreCase))
                            {
                                attDef.UpgradeOpen();
                                attDef.TransformBy(Matrix3d.Displacement(localDisp));
                                attDef.AdjustAlignment(db);
                                break;
                            }
                        }

                        // 5. 收集并同步更新全图（模型空间 + 所有布局图纸空间）所有同名块实例
                        HashSet<ObjectId> allRefIds = new HashSet<ObjectId>();
                        foreach (ObjectId id in btr.GetBlockReferenceIds(true, true))
                        {
                            allRefIds.Add(id);
                        }

                        // 支持动态块所有匿名派生实例
                        if (parentBlk.IsDynamicBlock)
                        {
                            BlockTableRecord dynBtr = (BlockTableRecord)tr.GetObject(parentBlk.DynamicBlockTableRecord, OpenMode.ForRead);
                            foreach (ObjectId id in dynBtr.GetBlockReferenceIds(true, true)) allRefIds.Add(id);
                            foreach (ObjectId anonId in dynBtr.GetAnonymousBlockIds())
                            {
                                BlockTableRecord anonBtr = (BlockTableRecord)tr.GetObject(anonId, OpenMode.ForRead);
                                foreach (ObjectId id in anonBtr.GetBlockReferenceIds(true, true)) allRefIds.Add(id);
                            }
                        }

                        int syncCount = 0;
                        foreach (ObjectId refId in allRefIds)
                        {
                            BlockReference bRef = tr.GetObject(refId, OpenMode.ForRead) as BlockReference;
                            if (bRef == null) continue;

                            // 各实例根据自身的插入比例和旋转矩阵，精确映射位移
                            Vector3d instDisp = localDisp.TransformBy(bRef.BlockTransform);

                            foreach (ObjectId attId in bRef.AttributeCollection)
                            {
                                AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                                if (att != null && string.Equals(att.Tag, targetTag, StringComparison.OrdinalIgnoreCase))
                                {
                                    att.UpgradeOpen();
                                    att.LockPositionInBlock = false;
                                    att.TransformBy(Matrix3d.Displacement(instDisp));
                                    att.AdjustAlignment(db);
                                    att.RecordGraphicsModified(true);
                                    syncCount++;
                                    break;
                                }
                            }
                        }

                        tr.Commit();
                        ed.UpdateScreen();
                        ed.WriteMessage(string.Format("\n✔ [XY工具箱] 属性文字【{0}】已成功同步更新全图 {1} 处同名块！\n", targetTag, syncCount));
                    }
                }
            }
        }

        // ========================================================
        // 图层冻结操作历史记录（支持精确单步撤销、逐步解冻、全部恢复）
        // ========================================================
        public class FreezeActionRecord
        {
            public string LayerName { get; set; }
            public ObjectId LayerId { get; set; }
            public bool IsViewportFreeze { get; set; }
            public ObjectId ViewportId { get; set; }
            public string SourceInfo { get; set; }
            public DateTime Timestamp { get; set; }

            public FreezeActionRecord(string layerName, ObjectId layerId, bool isVp, ObjectId vpId, string sourceInfo)
            {
                LayerName = layerName;
                LayerId = layerId;
                IsViewportFreeze = isVp;
                ViewportId = vpId;
                SourceInfo = sourceInfo;
                Timestamp = DateTime.Now;
            }
        }

        // 全局冻结操作历史栈（跨命令持久化，支持随时 XJD 逐步解冻）
        private static readonly List<FreezeActionRecord> _freezeHistory = new List<FreezeActionRecord>();

        private static void InvalidateDocWindowSafe(Document doc)
        {
            try
            {
                if (doc != null && doc.Window != null)
                {
                    IntPtr hWnd = doc.Window.Handle;
                    if (hWnd != IntPtr.Zero)
                    {
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    }
                }
            }
            catch { }
        }

        // 执行单条解冻逻辑（返回是否成功解冻，零黑屏、零全图重生成）
        private static bool RevertFreezeRecord(Database db, Editor ed, FreezeActionRecord rec)
        {
            if (rec == null || string.IsNullOrEmpty(rec.LayerName)) return false;

            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

                // 1. 窗口级帧锁定 + 静默模式（拦截黑屏擦除帧与命令行刷屏）
                IntPtr mainWnd = IntPtr.Zero;
                try { mainWnd = Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle; } catch { }
                short oldNomutt = 0;
                try
                {
                    object nomuttObj = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("NOMUTT");
                    if (nomuttObj != null) oldNomutt = Convert.ToInt16(nomuttObj);
                    Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("NOMUTT", (short)1);
                }
                catch { }

                if (mainWnd != IntPtr.Zero)
                {
                    try { LockWindowUpdate(mainWnd); } catch { }
                }

                try
                {
                    if (rec.IsViewportFreeze)
                    {
                        // ====================================================
                        // 视口解冻：海龙同款原生命令管道同步解冻 (平滑离屏双缓冲，零黑屏)
                        // 彻底绕过 .NET 托管层 Viewport.ThawLayersInViewport 对 Direct3D 的强制清黑！
                        // ====================================================
                        try
                        {
                            IntPtr resPtr;
                            string lispExpr = string.Format("(command \"_.vplayer\" \"_thaw\" \"{0}\" \"_current\" \"\")", rec.LayerName);
                            acedEvaluateLisp(lispExpr, out resPtr);

                            ed.WriteMessage(string.Format("\n☀️ [XY工具箱] 已恢复【视口图层】: [{0}] (来自: {1})\n", rec.LayerName, rec.SourceInfo));
                            RefreshPanelSafe();
                            return true;
                        }
                        catch
                        {
                            // 备用纯内存事务兜底
                            using (Transaction tr = db.TransactionManager.StartTransaction())
                            {
                                if (!rec.ViewportId.IsNull && !rec.ViewportId.IsErased)
                                {
                                    Autodesk.AutoCAD.DatabaseServices.Viewport vp = tr.GetObject(rec.ViewportId, OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.Viewport;
                                    if (vp != null)
                                    {
                                        vp.ThawLayersInViewport(new ObjectId[] { rec.LayerId }.GetEnumerator());
                                        vp.RecordGraphicsModified(true);
                                        tr.Commit();
                                        ed.WriteMessage(string.Format("\n☀️ [XY工具箱] 已恢复【视口图层】: [{0}] (来自: {1})\n", rec.LayerName, rec.SourceInfo));
                                        RefreshPanelSafe();
                                        return true;
                                    }
                                }
                                tr.Commit();
                            }
                        }
                    }
                    else
                    {
                        // 模型空间全局解冻
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                            if (lt.Has(rec.LayerName))
                            {
                                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[rec.LayerName], OpenMode.ForWrite);
                                if (ltr.IsFrozen)
                                {
                                    ltr.IsFrozen = false;
                                    tr.Commit();

                                    ed.WriteMessage(string.Format("\n☀️ [XY工具箱] 已恢复【全局图层】: [{0}] (来自: {1})\n", rec.LayerName, rec.SourceInfo));
                                    RefreshPanelSafe();
                                    return true;
                                }
                            }
                            tr.Commit();
                        }
                    }
                }
                finally
                {
                    // 2. 解除窗口锁定并触发无擦除增量更新
                    if (mainWnd != IntPtr.Zero)
                    {
                        try { LockWindowUpdate(IntPtr.Zero); } catch { }
                    }
                    try { Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("NOMUTT", oldNomutt); } catch { }
                    InvalidateDocWindowSafe(doc);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[提示] 恢复图层失败: " + ex.Message + "\n");
            }
            return false;
        }

        public static void RefreshPanelSafe()
        {
            try
            {
                if (_control != null && !_control.IsDisposed && _control.Created)
                {
                    _control.BeginInvoke(new Action(() =>
                    {
                        try { _control.Invalidate(true); _control.Update(); } catch { }
                    }));
                }
            }
            catch { }
        }

        // ========================================================
        // 核心功能 1：穿透点取冻结对象图层 (XDJ / XDK / XDF)
        // 彻底解决传统海龙/原生LAYFRZ无法穿透图块内子图层的痛点！
        // 1. 深度穿透多层嵌套图块(GetNestedEntity)，直接获取块内叶子图元真实图层
        // 2. 智能层名解析：子图元图层为 0 时向上溯源至容器母块图层；子图元具有独立图层时直接穿透冻结该子图层
        // 3. 视口空间与模型空间自适应：
        //    - 若在布局激活视口：精准执行视口冻结 (FreezeLayersInViewport)，仅隐匿当前视口，毫秒级无黑屏
        //    - 若在模型空间：执行全局图层冻结 (LayerTableRecord.IsFrozen = true)
        // 4. 交互进阶支持实时微步撤销：点选时直接输入 U 回车，即刻撤销上一个误点冻结的图层！
        // ========================================================
        [CommandMethod("XDJ")]
        [CommandMethod("XDK")]
        [CommandMethod("XDF")]
        [CommandMethod("XY_FREEZE")]
        public static void DeepFreezeEntityLayer()
        {
            EnsureLspLoaded();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // 本次运行内部的冻结栈（专供本次连续点选时的 U 撤销）
            Stack<FreezeActionRecord> sessionStack = new Stack<FreezeActionRecord>();

            while (true)
            {
                PromptNestedEntityOptions opt = new PromptNestedEntityOptions("\n>> 请点选要冻结图层的图元 [撤销上步(U)] (按ESC或回车结束): ");
                opt.AllowNone = true;
                opt.Keywords.Add("U");

                PromptNestedEntityResult res = ed.GetNestedEntity(opt);

                // 响应输入 U 键实时微步撤销
                if (res.Status == PromptStatus.Keyword && string.Equals(res.StringResult, "U", StringComparison.OrdinalIgnoreCase))
                {
                    if (sessionStack.Count > 0)
                    {
                        FreezeActionRecord lastRec = sessionStack.Pop();
                        using (doc.LockDocument())
                        {
                            if (RevertFreezeRecord(db, ed, lastRec))
                            {
                                // 同步从全局历史移除
                                if (_freezeHistory.Count > 0 && _freezeHistory[_freezeHistory.Count - 1] == lastRec)
                                {
                                    _freezeHistory.RemoveAt(_freezeHistory.Count - 1);
                                }
                            }
                        }
                    }
                    else
                    {
                        ed.WriteMessage("\n[提示] 本次操作已无可以撤销的冻结图层。\n");
                    }
                    continue;
                }

                if (res.Status != PromptStatus.OK)
                {
                    break;
                }

                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            Entity ent = tr.GetObject(res.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent == null)
                            {
                                ed.WriteMessage("\n[提示] 未能识别所选对象，请重新点选。\n");
                                tr.Abort();
                                continue;
                            }

                            string targetLayer = ent.Layer;
                            string sourceInfo = "图元";

                            // 若点中的是视口边框本身，友好提示
                            Autodesk.AutoCAD.DatabaseServices.Viewport vpEntity = ent as Autodesk.AutoCAD.DatabaseServices.Viewport;
                            if (vpEntity != null)
                            {
                                ed.WriteMessage("\n[提示] 您点中了视口边框，请双击激活视口后直接点击视口内的图元，或点选具体模型线元。\n");
                                tr.Abort();
                                continue;
                            }

                            // 穿透图块深度解析：如果是在图块内部
                            ObjectId[] containers = res.GetContainers();
                            if (containers != null && containers.Length > 0)
                            {
                                BlockReference topBlock = tr.GetObject(containers[0], OpenMode.ForRead) as BlockReference;
                                string topBlockName = topBlock != null ? topBlock.Name : "图块";

                                if (string.Equals(targetLayer, "0", StringComparison.OrdinalIgnoreCase))
                                {
                                    // 块内0层图元，继承母块图层
                                    if (topBlock != null && !string.IsNullOrEmpty(topBlock.Layer))
                                    {
                                        targetLayer = topBlock.Layer;
                                        sourceInfo = string.Format("图块【{0}】母层 (块内子图元为0层)", topBlockName);
                                    }
                                }
                                else
                                {
                                    // 块内子图元自身拥有独立图层（穿透块内拾取核心！）
                                    sourceInfo = string.Format("图块【{0}】内子对象", topBlockName);
                                }
                            }

                            if (string.IsNullOrEmpty(targetLayer))
                            {
                                ed.WriteMessage("\n[提示] 获取目标图层失败。\n");
                                tr.Abort();
                                continue;
                            }

                            // 检查图层表
                            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                            if (!lt.Has(targetLayer))
                            {
                                ed.WriteMessage(string.Format("\n[提示] 未在图层表中找到图层 [{0}]。\n", targetLayer));
                                tr.Abort();
                                continue;
                            }

                            ObjectId layerId = lt[targetLayer];

                            // 判断当前是在布局激活视口、还是在图纸空间、还是在模型空间
                            bool isTilemode = (Convert.ToInt16(Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("TILEMODE")) == 1);
                            short cvport = 1;
                            try
                            {
                                object cvObj = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("CVPORT");
                                if (cvObj != null) cvport = Convert.ToInt16(cvObj);
                            }
                            catch { }

                            bool isInsideViewport = (!isTilemode && cvport > 1);

                            if (isInsideViewport)
                            {
                                // ====================================================
                                // 模式 1: 布局空间内的激活视口 -> 毫秒级视口局部冻结
                                // ====================================================
                                ObjectId activeVpId = ed.ActiveViewportId;
                                Autodesk.AutoCAD.DatabaseServices.Viewport curVp = tr.GetObject(activeVpId, OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.Viewport;

                                if (curVp != null)
                                {
                                    if (curVp.IsLayerFrozenInViewport(layerId))
                                    {
                                        ed.WriteMessage(string.Format("\n[提示] 图层 [{0}] 在当前视口中已经是冻结状态。\n", targetLayer));
                                        tr.Abort();
                                        continue;
                                    }

                                    curVp.FreezeLayersInViewport(new ObjectId[] { layerId }.GetEnumerator());
                                    curVp.RecordGraphicsModified(true);
                                    tr.Commit();
                                    InvalidateDocWindowSafe(doc);

                                    FreezeActionRecord rec = new FreezeActionRecord(targetLayer, layerId, true, activeVpId, sourceInfo);
                                    sessionStack.Push(rec);
                                    _freezeHistory.Add(rec);

                                    ed.WriteMessage(string.Format("\n❄ [XY工具箱] 成功在【当前视口】穿透冻结图层: [{0}] (来自: {1})", targetLayer, sourceInfo));
                                }
                                else
                                {
                                    // 容错回退
                                    tr.Abort();
                                    Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.SendStringToExecute(
                                        string.Format("_.vplayer _freeze \"{0}\" _current \"\"\n", targetLayer), true, false, false);
                                    ed.WriteMessage(string.Format("\n❄ [XY工具箱] 成功在当前视口冻结图层: [{0}]\n", targetLayer));
                                }
                            }
                            else
                            {
                                // ====================================================
                                // 模式 2: 模型空间 -> 全局图层冻结 (LayerTableRecord.IsFrozen = true)
                                // ====================================================
                                if (layerId == db.Clayer)
                                {
                                    ed.WriteMessage(string.Format("\n[提示] 图层 [{0}] 为当前工作图层，无法直接冻结。请先切换当前层。\n", targetLayer));
                                    tr.Abort();
                                    continue;
                                }

                                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                                if (ltr.IsFrozen)
                                {
                                    ed.WriteMessage(string.Format("\n[提示] 图层 [{0}] 已经是全局冻结状态。\n", targetLayer));
                                    tr.Abort();
                                    continue;
                                }

                                ltr.IsFrozen = true;
                                tr.Commit();
                                InvalidateDocWindowSafe(doc);

                                FreezeActionRecord rec = new FreezeActionRecord(targetLayer, layerId, false, ObjectId.Null, sourceInfo);
                                sessionStack.Push(rec);
                                _freezeHistory.Add(rec);

                                ed.WriteMessage(string.Format("\n❄ [XY工具箱] 成功【全局冻结】图层: [{0}] (来自: {1})", targetLayer, sourceInfo));
                            }
                        }
                        catch (System.Exception ex)
                        {
                            tr.Abort();
                            ed.WriteMessage("\n[错误] 穿透冻结图层异常: " + ex.Message + "\n");
                        }
                    }
                }
            }

            if (sessionStack.Count > 0)
            {
                ed.WriteMessage(string.Format("\n✔ [XY工具箱] 穿透冻结结束，本次共保持隐匿 {0} 个图层！(随时输入 XJD 可逐步解冻恢复)\n", sessionStack.Count));
            }
        }

        // ========================================================
        // 核心功能 2：独立逐步解冻命令 (XJD / XTCU / THAWONE)
        // 无论何时输入 XJD，精准逆序还原最近一次被冻结的图层！
        // 连续按空格回车，可一步步持续还原历史冻结图层，不干扰正常绘图！
        // ========================================================
        [CommandMethod("XJD")]
        [CommandMethod("XY_THAW")]
        [CommandMethod("THAWONE")]
        public static void UndoDeepFreezeLayer()
        {
            EnsureLspLoaded();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            if (_freezeHistory.Count == 0)
            {
                ed.WriteMessage("\n[提示] 暂无通过 XY工具箱冻结的图层历史记录。\n");
                return;
            }

            using (doc.LockDocument())
            {
                // 逆序查找最后一个可以成功解冻的记录
                while (_freezeHistory.Count > 0)
                {
                    int lastIdx = _freezeHistory.Count - 1;
                    FreezeActionRecord rec = _freezeHistory[lastIdx];
                    _freezeHistory.RemoveAt(lastIdx);

                    if (RevertFreezeRecord(db, ed, rec))
                    {
                        ed.WriteMessage(string.Format(">> 剩余已记录冻结图层数: {0} (继续按空格/回车可继续解冻上一层)\n", _freezeHistory.Count));
                        return;
                    }
                }
                ed.WriteMessage("\n[提示] 历史记录中的图层已全部处于解冻状态。\n");
            }
        }

        // ========================================================
        // 核心功能 3：一键恢复全部被冻结图层 (XHF / THAWALL)
        // 一键将工具箱记录的图层全部解除冻结，防灾保险！
        // ========================================================
        // 核心功能: 免炸开穿透删除 (KSC / XSC / BDE)
        // ========================================================
        [CommandMethod("KSC")]
        [CommandMethod("XSC")]
        [CommandMethod("BDE")]
        public static void BlockDeepEraseCommand()
        {
            EnsureLspLoaded();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                doc.SendStringToExecute("(c:KSC) ", true, false, false);
            }
        }

        // ========================================================
        [CommandMethod("XHF")]
        [CommandMethod("XY_THAWALL")]
        public static void RestoreAllFrozenLayers()
        {
            EnsureLspLoaded();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            if (_freezeHistory.Count == 0)
            {
                ed.WriteMessage("\n[提示] 暂无通过 XY工具箱冻结的图层记录。\n");
                return;
            }

            int restoredCount = 0;
            using (doc.LockDocument())
            {
                for (int i = _freezeHistory.Count - 1; i >= 0; i--)
                {
                    FreezeActionRecord rec = _freezeHistory[i];
                    if (RevertFreezeRecord(db, ed, rec))
                    {
                        restoredCount++;
                    }
                }
                _freezeHistory.Clear();
            }

            ed.WriteMessage(string.Format("\n☀️ [XY工具箱] 已一键全部恢复 {0} 个图层的冻结状态！\n", restoredCount));
        }

        // ========================================================
        // 图层合并独立可视化弹窗面板 (HB2)
        // ========================================================
        // 图层合并与标准化规范器 (HB2)
        // 支持模式 A：自由修改层名 / 加前缀 / 调整颜色 (无参考图层时，新建并深度继承属性)
        // 支持模式 B：图面直接拾取已有目标图层合并
        // ========================================================

    public class ShortcutConfigDialog : Form
    {
        private TextBox txtMergeQuick;
        private TextBox txtMerge;
        private TextBox txtFreezeDeep;
        private TextBox txtBlockZero;
        private TextBox txtMoveText;
        private TextBox txtDetect;
        private TextBox txtPanel;
        private TextBox txtIme;
        private Button btnSave;
        private Button btnNotepad;
        private Button btnDefault;
        private Button btnCancel;

        private string _iniPath = "";

        public ShortcutConfigDialog()
        {
            InitializeDialog();
            LoadConfig();
        }

        private string GetIniPath()
        {
            return Commands.FindToolboxFile("shortcuts.ini");
        }

        private void InitializeDialog()
        {
            this.Text = "自定义快捷键设置 - XY工具箱 V1.0";
            this.Size = new Size(460, 590);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            int leftLabel = 20;
            int leftBox = 250;
            int boxWidth = 170;
            int top = 16;
            int step = 42;

            Label lblHeader = new Label();
            lblHeader.Text = "★ 快捷键定制 (支持多个别名，用逗号隔开)";
            lblHeader.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            lblHeader.ForeColor = Color.FromArgb(30, 41, 59);
            lblHeader.Location = new Point(leftLabel, top);
            lblHeader.Size = new Size(390, 22);
            this.Controls.Add(lblHeader);
            top += 30;

            // 1. 图层极速合并 (免弹窗)
            Label l1 = new Label();
            l1.Text = "1. 图层极速合并 (免弹窗):\r\n   (点选源图层和目标层直接合并)";
            l1.Location = new Point(leftLabel, top);
            l1.Size = new Size(220, 36);
            this.Controls.Add(l1);
            txtMergeQuick = new TextBox();
            txtMergeQuick.Location = new Point(leftBox, top + 4);
            txtMergeQuick.Size = new Size(boxWidth, 24);
            this.Controls.Add(txtMergeQuick);
            top += step;

            // 2. 图层合并 (面板选择版)
            Label l2 = new Label();
            l2.Text = "2. 图层合并 (面板选择版):\r\n   (呼出合并配置面板执行)";
            l2.Location = new Point(leftLabel, top);
            l2.Size = new Size(220, 36);
            this.Controls.Add(l2);
            txtMerge = new TextBox();
            txtMerge.Location = new Point(leftBox, top + 4);
            txtMerge.Size = new Size(boxWidth, 24);
            this.Controls.Add(txtMerge);
            top += step;

            // 3. 图层穿透冻结 (视口/全局)
            Label lFreeze = new Label();
            lFreeze.Text = "3. 穿透点取冻结图层:\r\n   (穿透图块内子对象/视口及全局冻结)";
            lFreeze.Location = new Point(leftLabel, top);
            lFreeze.Size = new Size(220, 36);
            this.Controls.Add(lFreeze);
            txtFreezeDeep = new TextBox();
            txtFreezeDeep.Location = new Point(leftBox, top + 4);
            txtFreezeDeep.Size = new Size(boxWidth, 24);
            this.Controls.Add(txtFreezeDeep);
            top += step;

            // 4. 图块深度归0层
            Label l3 = new Label();
            l3.Text = "4. 图块一键深度归0层:\r\n   (穿透多层嵌套块/随层颜色)";
            l3.Location = new Point(leftLabel, top);
            l3.Size = new Size(220, 36);
            this.Controls.Add(l3);
            txtBlockZero = new TextBox();
            txtBlockZero.Location = new Point(leftBox, top + 4);
            txtBlockZero.Size = new Size(boxWidth, 24);
            this.Controls.Add(txtBlockZero);
            top += step;

            // 5. 属性块文字同步平移
            Label l4 = new Label();
            l4.Text = "5. 属性块文字同步平移:\r\n   (全图同名块同标签文字平移)";
            l4.Location = new Point(leftLabel, top);
            l4.Size = new Size(220, 36);
            this.Controls.Add(l4);
            txtMoveText = new TextBox();
            txtMoveText.Location = new Point(leftBox, top + 4);
            txtMoveText.Size = new Size(boxWidth, 24);
            this.Controls.Add(txtMoveText);
            top += step;

            // 6. 底层图元属性探测
            Label l5 = new Label();
            l5.Text = "6. 底层图元属性探测:\r\n   (穿透嵌套块/视口探测真实层)";
            l5.Location = new Point(leftLabel, top);
            l5.Size = new Size(220, 36);
            this.Controls.Add(l5);
            txtDetect = new TextBox();
            txtDetect.Location = new Point(leftBox, top + 4);
            txtDetect.Size = new Size(boxWidth, 24);
            this.Controls.Add(txtDetect);
            top += step;

            // 7. 提资面板呼出
            Label l6 = new Label();
            l6.Text = "7. 提资设置面板呼出:\r\n   (打开侧边栏可视化控制面板)";
            l6.Location = new Point(leftLabel, top);
            l6.Size = new Size(220, 36);
            this.Controls.Add(l6);
            txtPanel = new TextBox();
            txtPanel.Location = new Point(leftBox, top + 4);
            txtPanel.Size = new Size(boxWidth, 24);
            this.Controls.Add(txtPanel);
            top += step;

            // 8. 输入法智能切换
            Label l7 = new Label();
            l7.Text = "8. 输入法智能切换:\r\n   (画图全自动英文/文字全自动中文)";
            l7.Location = new Point(leftLabel, top);
            l7.Size = new Size(220, 36);
            this.Controls.Add(l7);
            txtIme = new TextBox();
            txtIme.Location = new Point(leftBox, top + 4);
            txtIme.Size = new Size(boxWidth, 24);
            this.Controls.Add(txtIme);
            top += 46;

            // 按钮区域
            btnSave = new Button();
            btnSave.Text = "★ 保存并立即生效";
            btnSave.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnSave.BackColor = Color.FromArgb(37, 99, 235);
            btnSave.ForeColor = Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Location = new Point(20, top);
            btnSave.Size = new Size(195, 34);
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnNotepad = new Button();
            btnNotepad.Text = "📝 记事本编辑";
            btnNotepad.Font = new Font("Microsoft YaHei UI", 8.5F);
            btnNotepad.Location = new Point(225, top);
            btnNotepad.Size = new Size(195, 34);
            btnNotepad.Click += BtnNotepad_Click;
            this.Controls.Add(btnNotepad);
            top += 42;

            btnDefault = new Button();
            btnDefault.Text = "恢复默认快捷键";
            btnDefault.Font = new Font("Microsoft YaHei UI", 8.5F);
            btnDefault.Location = new Point(20, top);
            btnDefault.Size = new Size(195, 30);
            btnDefault.Click += BtnDefault_Click;
            this.Controls.Add(btnDefault);

            btnCancel = new Button();
            btnCancel.Text = "取消关闭";
            btnCancel.Font = new Font("Microsoft YaHei UI", 8.5F);
            btnCancel.Location = new Point(225, top);
            btnCancel.Size = new Size(195, 30);
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);
        }

        private void LoadConfig()
        {
            _iniPath = GetIniPath();
            Dictionary<string, string> cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(_iniPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(_iniPath);
                    foreach (string rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("[")) continue;
                        int idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            string k = line.Substring(0, idx).Trim();
                            string v = line.Substring(idx + 1).Trim();
                            cfg[k] = v;
                        }
                    }
                }
                catch { }
            }

            txtMergeQuick.Text = cfg.ContainsKey("CMD_MERGE_QUICK") ? cfg["CMD_MERGE_QUICK"] : "XJH, HB1";
            txtMerge.Text = cfg.ContainsKey("CMD_MERGE") ? cfg["CMD_MERGE"] : "XHB, HB2";
            txtFreezeDeep.Text = cfg.ContainsKey("CMD_FREEZE_DEEP") ? cfg["CMD_FREEZE_DEEP"] : "XDJ, XDK, XDF";
            txtBlockZero.Text = cfg.ContainsKey("CMD_BLOCK_ZERO") ? cfg["CMD_BLOCK_ZERO"] : "XGL";
            txtMoveText.Text = cfg.ContainsKey("CMD_MOVE_TEXT") ? cfg["CMD_MOVE_TEXT"] : "XSY";
            txtDetect.Text = cfg.ContainsKey("CMD_DETECT") ? cfg["CMD_DETECT"] : "XTC";
            txtPanel.Text = cfg.ContainsKey("CMD_PANEL") ? cfg["CMD_PANEL"] : "XGJ";
            txtIme.Text = cfg.ContainsKey("CMD_IME") ? cfg["CMD_IME"] : "XSR, XSRF, IME";
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                _iniPath = GetIniPath();
                List<string> lines = new List<string>();
                lines.Add("; ========================================================");
                lines.Add("; XY工具箱 V1.0 - 快捷键配置文件 (shortcuts.ini)");
                lines.Add("; 支持逗号隔开多个别名，修改后输入 REKEY 即可立即刷新生效");
                lines.Add("; 命名规范：严格遵循 X前缀 + 精简拼音首字母组合");
                lines.Add("; ========================================================");
                lines.Add("");
                lines.Add("[Shortcuts]");
                lines.Add("CMD_MERGE_QUICK=" + txtMergeQuick.Text.Trim());
                lines.Add("CMD_MERGE=" + txtMerge.Text.Trim());
                lines.Add("CMD_FREEZE_DEEP=" + txtFreezeDeep.Text.Trim());
                lines.Add("CMD_BLOCK_ZERO=" + txtBlockZero.Text.Trim());
                lines.Add("CMD_MOVE_TEXT=" + txtMoveText.Text.Trim());
                lines.Add("CMD_DETECT=" + txtDetect.Text.Trim());
                lines.Add("CMD_PANEL=" + txtPanel.Text.Trim());
                lines.Add("CMD_IME=" + txtIme.Text.Trim());
                lines.Add("ENABLE_00_LEGACY=0");

                File.WriteAllLines(_iniPath, lines.ToArray(), System.Text.Encoding.UTF8);

                // 发送命令使 AutoLISP 立即热重载快捷键
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.SendStringToExecute("(apply_all_toolbox_shortcuts) ", true, false, false);
                }

                MessageBox.Show("★ 快捷键设置已成功保存并立即生效！\n\n" + 
                                "图层极速合并: " + txtMergeQuick.Text + "\n" + 
                                "图层面板合并: " + txtMerge.Text + "\n" + 
                                "穿透图层冻结: " + txtFreezeDeep.Text + "\n" + 
                                "图块归0层: " + txtBlockZero.Text + "\n" + 
                                "文字平移: " + txtMoveText.Text + "\n" + 
                                "图元探测: " + txtDetect.Text + "\n" + 
                                "工具箱面板: " + txtPanel.Text + "\n" +
                                "输入法切换: " + txtIme.Text, 
                                "设置生效", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("保存快捷键失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnNotepad_Click(object sender, EventArgs e)
        {
            try
            {
                _iniPath = GetIniPath();
                if (!File.Exists(_iniPath))
                {
                    BtnSave_Click(null, null);
                }
                Process.Start("notepad.exe", _iniPath);
                this.Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("启动记事本失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDefault_Click(object sender, EventArgs e)
        {
            txtMergeQuick.Text = "XJH, HB1";
            txtMerge.Text = "XHB, HB2";
            txtFreezeDeep.Text = "XDJ, XDK, XDF";
            txtBlockZero.Text = "XGL";
            txtMoveText.Text = "XSY";
            txtDetect.Text = "XTC";
            txtPanel.Text = "XGJ";
            txtIme.Text = "XSR, XSRF, IME";
        }
    }

    public class LayerMergeDialog : Form
    {
        private ComboBox cboSrc;
        private Button btnPickSrc;

        private RadioButton rdoPrefix;
        private TextBox txtPrefix;
        private TextBox txtBaseName;
        private Button btnPickName;
        private Label lblPreview;

        private Panel pnlColor;
        private Label lblColorDesc;
        private Button btnChangeColor;
        private CheckBox chkInheritColor;

        private ComboBox cboLinetype;
        private CheckBox chkInheritLinetype;

        private RadioButton rdoPickExist;
        private ComboBox cboTar;
        private Button btnPickTar;

        private Label lblColorBTitle;
        private Panel pnlColorB;
        private Label lblColorDescB;
        private Button btnChangeColorB;
        private CheckBox chkInheritColorB;

        private Label lblLtBTitle;
        private ComboBox cboLinetypeB;
        private CheckBox chkInheritLinetypeB;

        private CheckBox chkByLayer;
        private CheckBox chkVP;
        private Button btnMerge;
        private Button btnCancel;
        private Button btnSetKey;

        private Autodesk.AutoCAD.Colors.Color _chosenColor = null;
        private Autodesk.AutoCAD.Colors.Color _srcColor = null;
        private string _srcLinetype = "Continuous";

        private Autodesk.AutoCAD.Colors.Color _chosenColorB = null;
        private Autodesk.AutoCAD.Colors.Color _tarBOriginalColor = null;
        private string _tarBOriginalLinetype = "Continuous";

        public LayerMergeDialog()
        {
            InitializeComponent();
            RefreshLayerList();
            RefreshLinetypeList();
            rdoPickExist.Checked = true;
            UpdateModeUI(false);
            UpdateTargetPreview();
        }

        private void InitializeComponent()
        {
            this.Text = "XY工具箱 - 图层标准合并与规范器 (XHB)";
            try
            {
                string icoPath = Commands.GetAppIconPath();
                if (!string.IsNullOrEmpty(icoPath))
                {
                    this.Icon = new Icon(icoPath);
                }
            }
            catch { }
            this.Size = new Size(520, 595);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            int pad = 16;
            int w = 472;
            int top = 12;

            // 1. 源图层
            GroupBox grpSrc = new GroupBox();
            grpSrc.Text = " 源图层 (要合并/规范化重命名) ";
            grpSrc.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            grpSrc.ForeColor = Color.FromArgb(30, 41, 59);
            grpSrc.Location = new Point(pad, top);
            grpSrc.Size = new Size(w, 65);

            Label lblSrc = new Label();
            lblSrc.Text = "源 图 层:";
            lblSrc.Font = new Font("Microsoft YaHei UI", 9F);
            lblSrc.ForeColor = Color.FromArgb(71, 85, 105);
            lblSrc.Location = new Point(12, 26);
            lblSrc.Size = new Size(60, 22);
            grpSrc.Controls.Add(lblSrc);

            cboSrc = new ComboBox();
            cboSrc.DropDownStyle = ComboBoxStyle.DropDownList;
            cboSrc.Font = new Font("Microsoft YaHei UI", 9F);
            cboSrc.Location = new Point(75, 23);
            cboSrc.Size = new Size(275, 24);
            cboSrc.SelectedIndexChanged += CboSrc_SelectedIndexChanged;
            grpSrc.Controls.Add(cboSrc);

            btnPickSrc = new Button();
            btnPickSrc.Text = "图面拾取...";
            btnPickSrc.Font = new Font("Microsoft YaHei UI", 8.5F);
            btnPickSrc.Location = new Point(360, 22);
            btnPickSrc.Size = new Size(98, 26);
            btnPickSrc.BackColor = Color.FromArgb(241, 245, 249);
            btnPickSrc.Click += BtnPickSrc_Click;
            grpSrc.Controls.Add(btnPickSrc);

            this.Controls.Add(grpSrc);
            top += 74;

            // 2. 目标图层及规范化配置
            GroupBox grpTar = new GroupBox();
            grpTar.Text = " 目标图层 (保留/合并到/规范化命名) ";
            grpTar.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            grpTar.ForeColor = Color.FromArgb(30, 41, 59);
            grpTar.Location = new Point(pad, top);
            grpTar.Size = new Size(w, 288);

            // 模式 A
            rdoPrefix = new RadioButton();
            rdoPrefix.Text = "模式 A：添加前缀 / 自由修改新层名 (无参考图层时推荐)";
            rdoPrefix.Font = new Font("Microsoft YaHei UI", 8.5F);
            rdoPrefix.ForeColor = Color.FromArgb(100, 116, 139);
            rdoPrefix.Location = new Point(12, 22);
            rdoPrefix.Size = new Size(430, 22);
            rdoPrefix.Checked = false;
            rdoPrefix.CheckedChanged += (s, e) => { if (rdoPrefix.Checked) UpdateModeUI(true); };
            grpTar.Controls.Add(rdoPrefix);

            Label lblPrefix = new Label();
            lblPrefix.Text = "前缀(记忆):";
            lblPrefix.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblPrefix.ForeColor = Color.FromArgb(71, 85, 105);
            lblPrefix.Location = new Point(28, 52);
            lblPrefix.Size = new Size(68, 22);
            grpTar.Controls.Add(lblPrefix);

            txtPrefix = new TextBox();
            txtPrefix.Text = LoadLastPrefix();
            txtPrefix.Font = new Font("Consolas", 9.5F, FontStyle.Bold);
            txtPrefix.ForeColor = Color.FromArgb(2, 132, 199);
            txtPrefix.Location = new Point(98, 49);
            txtPrefix.Size = new Size(68, 24);
            txtPrefix.TextChanged += (s, e) => { UpdateTargetPreview(); };
            txtPrefix.Enter += (s, e) => { rdoPrefix.Checked = true; UpdateModeUI(true); };
            grpTar.Controls.Add(txtPrefix);

            Label lblBase = new Label();
            lblBase.Text = "图层名称:";
            lblBase.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblBase.ForeColor = Color.FromArgb(71, 85, 105);
            lblBase.Location = new Point(175, 52);
            lblBase.Size = new Size(60, 22);
            grpTar.Controls.Add(lblBase);

            txtBaseName = new TextBox();
            txtBaseName.Text = "门";
            txtBaseName.Font = new Font("Microsoft YaHei UI", 9F);
            txtBaseName.Location = new Point(238, 49);
            txtBaseName.Size = new Size(118, 24);
            txtBaseName.TextChanged += (s, e) => { UpdateTargetPreview(); };
            txtBaseName.Enter += (s, e) => { rdoPrefix.Checked = true; UpdateModeUI(true); };
            grpTar.Controls.Add(txtBaseName);

            btnPickName = new Button();
            btnPickName.Text = "图面拾取名";
            btnPickName.Font = new Font("Microsoft YaHei UI", 8F);
            btnPickName.Location = new Point(362, 48);
            btnPickName.Size = new Size(95, 25);
            btnPickName.BackColor = Color.FromArgb(241, 245, 249);
            btnPickName.Click += BtnPickName_Click;
            grpTar.Controls.Add(btnPickName);

            // 最终目标层预览
            Label lblPreviewTitle = new Label();
            lblPreviewTitle.Text = "最终目标层:";
            lblPreviewTitle.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblPreviewTitle.ForeColor = Color.FromArgb(71, 85, 105);
            lblPreviewTitle.Location = new Point(28, 81);
            lblPreviewTitle.Size = new Size(68, 22);
            grpTar.Controls.Add(lblPreviewTitle);

            lblPreview = new Label();
            lblPreview.Text = "UDG-门";
            lblPreview.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold);
            lblPreview.ForeColor = Color.FromArgb(16, 185, 129);
            lblPreview.Location = new Point(98, 81);
            lblPreview.Size = new Size(360, 22);
            grpTar.Controls.Add(lblPreview);

            // 图层颜色
            Label lblColor = new Label();
            lblColor.Text = "图层颜色:";
            lblColor.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblColor.ForeColor = Color.FromArgb(71, 85, 105);
            lblColor.Location = new Point(28, 110);
            lblColor.Size = new Size(68, 22);
            grpTar.Controls.Add(lblColor);

            pnlColor = new Panel();
            pnlColor.BackColor = Color.Red;
            pnlColor.BorderStyle = BorderStyle.FixedSingle;
            pnlColor.Location = new Point(98, 109);
            pnlColor.Size = new Size(24, 22);
            grpTar.Controls.Add(pnlColor);

            lblColorDesc = new Label();
            lblColorDesc.Text = "1 (红)";
            lblColorDesc.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblColorDesc.ForeColor = Color.FromArgb(51, 65, 85);
            lblColorDesc.Location = new Point(128, 111);
            lblColorDesc.Size = new Size(55, 20);
            grpTar.Controls.Add(lblColorDesc);

            btnChangeColor = new Button();
            btnChangeColor.Text = "修改颜色...";
            btnChangeColor.Font = new Font("Microsoft YaHei UI", 8F);
            btnChangeColor.Location = new Point(188, 108);
            btnChangeColor.Size = new Size(78, 24);
            btnChangeColor.BackColor = Color.FromArgb(241, 245, 249);
            btnChangeColor.Click += BtnChangeColor_Click;
            grpTar.Controls.Add(btnChangeColor);

            chkInheritColor = new CheckBox();
            chkInheritColor.Text = "继承源层颜色";
            chkInheritColor.Font = new Font("Microsoft YaHei UI", 8.5F);
            chkInheritColor.ForeColor = Color.FromArgb(71, 85, 105);
            chkInheritColor.Checked = true;
            chkInheritColor.Location = new Point(275, 110);
            chkInheritColor.Size = new Size(120, 20);
            chkInheritColor.CheckedChanged += ChkInheritColor_CheckedChanged;
            grpTar.Controls.Add(chkInheritColor);

            // 图层线型设置
            Label lblLt = new Label();
            lblLt.Text = "图层线型:";
            lblLt.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblLt.ForeColor = Color.FromArgb(71, 85, 105);
            lblLt.Location = new Point(28, 140);
            lblLt.Size = new Size(68, 22);
            grpTar.Controls.Add(lblLt);

            cboLinetype = new ComboBox();
            cboLinetype.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLinetype.Font = new Font("Microsoft YaHei UI", 8.5F);
            cboLinetype.Location = new Point(98, 137);
            cboLinetype.Size = new Size(168, 24);
            cboLinetype.SelectedIndexChanged += (s, e) => {
                if (cboLinetype.SelectedItem != null && (string)cboLinetype.SelectedItem != _srcLinetype)
                {
                    chkInheritLinetype.Checked = false;
                }
            };
            grpTar.Controls.Add(cboLinetype);

            chkInheritLinetype = new CheckBox();
            chkInheritLinetype.Text = "继承源层线型";
            chkInheritLinetype.Font = new Font("Microsoft YaHei UI", 8.5F);
            chkInheritLinetype.ForeColor = Color.FromArgb(71, 85, 105);
            chkInheritLinetype.Checked = true;
            chkInheritLinetype.Location = new Point(275, 139);
            chkInheritLinetype.Size = new Size(120, 20);
            chkInheritLinetype.CheckedChanged += (s, e) => {
                if (chkInheritLinetype.Checked && !string.IsNullOrEmpty(_srcLinetype))
                {
                    if (cboLinetype.Items.Contains(_srcLinetype))
                        cboLinetype.SelectedItem = _srcLinetype;
                }
            };
            grpTar.Controls.Add(chkInheritLinetype);

            // 模式 B
            rdoPickExist = new RadioButton();
            rdoPickExist.Text = "模式 B：从图面拾取或下拉选择已有目标图层";
            rdoPickExist.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            rdoPickExist.ForeColor = Color.FromArgb(3, 105, 161);
            rdoPickExist.Location = new Point(12, 175);
            rdoPickExist.Size = new Size(255, 22);
            rdoPickExist.Checked = true;
            rdoPickExist.CheckedChanged += (s, e) => { if (rdoPickExist.Checked) UpdateModeUI(false); };
            grpTar.Controls.Add(rdoPickExist);

            cboTar = new ComboBox();
            cboTar.DropDownStyle = ComboBoxStyle.DropDownList;
            cboTar.Font = new Font("Microsoft YaHei UI", 8.5F);
            cboTar.Location = new Point(270, 173);
            cboTar.Size = new Size(100, 24);
            cboTar.SelectedIndexChanged += (s, e) => { rdoPickExist.Checked = true; UpdateTargetPreview(); OnTargetBChanged(); };
            grpTar.Controls.Add(cboTar);

            btnPickTar = new Button();
            btnPickTar.Text = "拾取目标层...";
            btnPickTar.Font = new Font("Microsoft YaHei UI", 8F);
            btnPickTar.Location = new Point(375, 173);
            btnPickTar.Size = new Size(82, 25);
            btnPickTar.BackColor = Color.FromArgb(241, 245, 249);
            btnPickTar.Click += BtnPickTar_Click;
            grpTar.Controls.Add(btnPickTar);

            // 模式 B 图层颜色设置
            lblColorBTitle = new Label();
            lblColorBTitle.Text = "图层颜色:";
            lblColorBTitle.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblColorBTitle.ForeColor = Color.FromArgb(71, 85, 105);
            lblColorBTitle.Location = new Point(28, 204);
            lblColorBTitle.Size = new Size(68, 22);
            grpTar.Controls.Add(lblColorBTitle);

            pnlColorB = new Panel();
            pnlColorB.BackColor = Color.Yellow;
            pnlColorB.BorderStyle = BorderStyle.FixedSingle;
            pnlColorB.Location = new Point(98, 203);
            pnlColorB.Size = new Size(24, 22);
            grpTar.Controls.Add(pnlColorB);

            lblColorDescB = new Label();
            lblColorDescB.Text = "2 (黄)";
            lblColorDescB.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblColorDescB.ForeColor = Color.FromArgb(51, 65, 85);
            lblColorDescB.Location = new Point(128, 205);
            lblColorDescB.Size = new Size(55, 20);
            grpTar.Controls.Add(lblColorDescB);

            btnChangeColorB = new Button();
            btnChangeColorB.Text = "修改颜色...";
            btnChangeColorB.Font = new Font("Microsoft YaHei UI", 8F);
            btnChangeColorB.Location = new Point(188, 202);
            btnChangeColorB.Size = new Size(78, 24);
            btnChangeColorB.BackColor = Color.FromArgb(241, 245, 249);
            btnChangeColorB.Click += BtnChangeColorB_Click;
            grpTar.Controls.Add(btnChangeColorB);

            chkInheritColorB = new CheckBox();
            chkInheritColorB.Text = "继承源层颜色";
            chkInheritColorB.Font = new Font("Microsoft YaHei UI", 8.5F);
            chkInheritColorB.ForeColor = Color.FromArgb(71, 85, 105);
            chkInheritColorB.Checked = true;
            chkInheritColorB.Location = new Point(275, 204);
            chkInheritColorB.Size = new Size(120, 20);
            chkInheritColorB.CheckedChanged += ChkInheritColorB_CheckedChanged;
            grpTar.Controls.Add(chkInheritColorB);

            // 模式 B 图层线型设置
            lblLtBTitle = new Label();
            lblLtBTitle.Text = "图层线型:";
            lblLtBTitle.Font = new Font("Microsoft YaHei UI", 8.5F);
            lblLtBTitle.ForeColor = Color.FromArgb(71, 85, 105);
            lblLtBTitle.Location = new Point(28, 234);
            lblLtBTitle.Size = new Size(68, 22);
            grpTar.Controls.Add(lblLtBTitle);

            cboLinetypeB = new ComboBox();
            cboLinetypeB.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLinetypeB.Font = new Font("Microsoft YaHei UI", 8.5F);
            cboLinetypeB.Location = new Point(98, 231);
            cboLinetypeB.Size = new Size(168, 24);
            cboLinetypeB.SelectedIndexChanged += (s, e) => {
                if (cboLinetypeB.SelectedItem != null && (string)cboLinetypeB.SelectedItem != _srcLinetype)
                {
                    chkInheritLinetypeB.Checked = false;
                }
            };
            grpTar.Controls.Add(cboLinetypeB);

            chkInheritLinetypeB = new CheckBox();
            chkInheritLinetypeB.Text = "继承源层线型";
            chkInheritLinetypeB.Font = new Font("Microsoft YaHei UI", 8.5F);
            chkInheritLinetypeB.ForeColor = Color.FromArgb(71, 85, 105);
            chkInheritLinetypeB.Checked = true;
            chkInheritLinetypeB.Location = new Point(275, 233);
            chkInheritLinetypeB.Size = new Size(120, 20);
            chkInheritLinetypeB.CheckedChanged += (s, e) => {
                if (chkInheritLinetypeB.Checked && !string.IsNullOrEmpty(_srcLinetype))
                {
                    if (cboLinetypeB.Items.Contains(_srcLinetype))
                        cboLinetypeB.SelectedItem = _srcLinetype;
                }
            };
            grpTar.Controls.Add(chkInheritLinetypeB);

            this.Controls.Add(grpTar);
            top += 298;

            // 3. 合并选项
            GroupBox grpOpt = new GroupBox();
            grpOpt.Text = " 合并与转换选项 (深度穿透处理) ";
            grpOpt.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            grpOpt.ForeColor = Color.FromArgb(30, 41, 59);
            grpOpt.Location = new Point(pad, top);
            grpOpt.Size = new Size(w, 80);

            chkByLayer = new CheckBox();
            chkByLayer.Text = "图元强制 ByLayer 颜色与线型 (穿透图块内部图元，净化图元)";
            chkByLayer.Font = new Font("Microsoft YaHei UI", 8.5F);
            chkByLayer.ForeColor = Color.FromArgb(51, 65, 85);
            chkByLayer.Checked = false;
            chkByLayer.Location = new Point(12, 24);
            chkByLayer.Size = new Size(430, 22);
            grpOpt.Controls.Add(chkByLayer);

            chkVP = new CheckBox();
            chkVP.Text = "继承视口覆盖 (各布局视口内冻结与视口局部覆盖颜色/线型同步)";
            chkVP.Font = new Font("Microsoft YaHei UI", 8.5F);
            chkVP.ForeColor = Color.FromArgb(51, 65, 85);
            chkVP.Checked = true;
            chkVP.Location = new Point(12, 50);
            chkVP.Size = new Size(430, 22);
            grpOpt.Controls.Add(chkVP);

            this.Controls.Add(grpOpt);
            top += 90;

            // 底部按钮栏
            btnSetKey = new Button();
            btnSetKey.Text = "快捷键设置 (XYSZ)...";
            btnSetKey.Font = new Font("Microsoft YaHei UI", 9F);
            btnSetKey.Location = new Point(pad, top);
            btnSetKey.Size = new Size(145, 32);
            btnSetKey.BackColor = Color.White;
            btnSetKey.Click += (s, e) => Commands.ShowShortcutConfigDialog();
            this.Controls.Add(btnSetKey);

            btnMerge = new Button();
            btnMerge.Text = "确定合并 / 极速转换";
            btnMerge.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnMerge.ForeColor = Color.White;
            btnMerge.BackColor = Color.FromArgb(14, 165, 233);
            btnMerge.FlatStyle = FlatStyle.Flat;
            btnMerge.FlatAppearance.BorderSize = 0;
            btnMerge.Location = new Point(265, top);
            btnMerge.Size = new Size(140, 32);
            btnMerge.Click += BtnMerge_Click;
            this.Controls.Add(btnMerge);

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Font = new Font("Microsoft YaHei UI", 9F);
            btnCancel.Location = new Point(415, top);
            btnCancel.Size = new Size(72, 32);
            btnCancel.BackColor = Color.White;
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);
        }

        private void UpdateModeUI(bool isModeA)
        {
            if (isModeA)
            {
                rdoPrefix.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                rdoPrefix.ForeColor = Color.FromArgb(3, 105, 161);
                rdoPickExist.Font = new Font("Microsoft YaHei UI", 8.5F);
                rdoPickExist.ForeColor = Color.FromArgb(100, 116, 139);
            }
            else
            {
                rdoPickExist.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                rdoPickExist.ForeColor = Color.FromArgb(3, 105, 161);
                rdoPrefix.Font = new Font("Microsoft YaHei UI", 8.5F);
                rdoPrefix.ForeColor = Color.FromArgb(100, 116, 139);
            }

            txtPrefix.Enabled = isModeA;
            txtBaseName.Enabled = isModeA;
            btnPickName.Enabled = isModeA;
            btnChangeColor.Enabled = isModeA;
            chkInheritColor.Enabled = isModeA;
            cboLinetype.Enabled = isModeA;
            chkInheritLinetype.Enabled = isModeA;

            cboTar.Enabled = !isModeA;
            btnPickTar.Enabled = !isModeA;
            btnChangeColorB.Enabled = !isModeA;
            chkInheritColorB.Enabled = !isModeA;
            cboLinetypeB.Enabled = !isModeA;
            chkInheritLinetypeB.Enabled = !isModeA;

            if (!isModeA)
            {
                OnTargetBChanged();
            }

            UpdateTargetPreview();
        }

        private void UpdateTargetPreview()
        {
            if (lblPreview == null) return;

            string targetName = "";
            if (rdoPrefix.Checked)
            {
                targetName = (txtPrefix.Text.Trim() + txtBaseName.Text.Trim());
            }
            else
            {
                targetName = cboTar.SelectedItem as string ?? "";
            }

            if (string.IsNullOrEmpty(targetName))
            {
                lblPreview.Text = "(请指定或拾取目标图层)";
                lblPreview.ForeColor = Color.FromArgb(100, 116, 139);
                return;
            }

            bool exists = LayerExistsInDrawing(targetName);
            if (exists)
            {
                lblPreview.Text = targetName + "  (图纸中已存在此层，将直接合并)";
                lblPreview.ForeColor = Color.FromArgb(2, 132, 199);
            }
            else
            {
                lblPreview.Text = targetName + "  (图纸中无此层，确定时自动新建并继承属性)";
                lblPreview.ForeColor = Color.FromArgb(16, 185, 129);
            }
        }

        private bool LayerExistsInDrawing(string layerName)
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return false;
                using (doc.LockDocument())
                {
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                        bool res = lt.Has(layerName);
                        tr.Commit();
                        return res;
                    }
                }
            }
            catch { return false; }
        }

        public void RefreshLinetypeList()
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                Database db = doc.Database;
                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        LinetypeTable lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                        cboLinetype.Items.Clear();
                        cboLinetypeB.Items.Clear();
                        foreach (ObjectId id in lt)
                        {
                            LinetypeTableRecord ltr = (LinetypeTableRecord)tr.GetObject(id, OpenMode.ForRead);
                            cboLinetype.Items.Add(ltr.Name);
                            cboLinetypeB.Items.Add(ltr.Name);
                        }
                        tr.Commit();
                    }
                }
                if (cboLinetype.Items.Contains("Continuous"))
                    cboLinetype.SelectedItem = "Continuous";
                else if (cboLinetype.Items.Count > 0)
                    cboLinetype.SelectedIndex = 0;

                if (cboLinetypeB.Items.Contains("Continuous"))
                    cboLinetypeB.SelectedItem = "Continuous";
                else if (cboLinetypeB.Items.Count > 0)
                    cboLinetypeB.SelectedIndex = 0;
            }
            catch { }
        }

        private void CboSrc_SelectedIndexChanged(object sender, EventArgs e)
        {
            string src = cboSrc.SelectedItem as string;
            if (string.IsNullOrEmpty(src)) return;

            // 提取基础层名
            txtBaseName.Text = ExtractBaseLayerName(src);

            // 读取源图层颜色与线型
            ReadSourceLayerProperties(src);

            UpdateTargetPreview();
        }

        private void ReadSourceLayerProperties(string layerName)
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                using (doc.LockDocument())
                {
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                        if (lt.Has(layerName))
                        {
                            LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                            _srcColor = ltr.Color;
                            
                            // 读取线型名
                            LinetypeTableRecord ltRec = tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
                            if (ltRec != null)
                            {
                                _srcLinetype = ltRec.Name;
                            }
                            else
                            {
                                _srcLinetype = "Continuous";
                            }
                        }
                        tr.Commit();
                    }
                }

                if (chkInheritColor.Checked || _chosenColor == null)
                {
                    _chosenColor = _srcColor;
                    UpdateColorDisplay(_srcColor);
                }

                if (chkInheritLinetype.Checked && !string.IsNullOrEmpty(_srcLinetype))
                {
                    if (cboLinetype.Items.Contains(_srcLinetype))
                        cboLinetype.SelectedItem = _srcLinetype;
                }

                if (chkInheritColorB.Checked || _chosenColorB == null)
                {
                    _chosenColorB = _srcColor;
                    UpdateColorBDisplay(_srcColor);
                }

                if (chkInheritLinetypeB.Checked && !string.IsNullOrEmpty(_srcLinetype))
                {
                    if (cboLinetypeB.Items.Contains(_srcLinetype))
                        cboLinetypeB.SelectedItem = _srcLinetype;
                }
            }
            catch { }
        }

        private void OnTargetBChanged()
        {
            if (cboTar == null || cboTar.SelectedItem == null) return;
            string tarName = cboTar.SelectedItem.ToString();
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                Database db = doc.Database;
                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                        if (lt.Has(tarName))
                        {
                            LayerTableRecord tarLtr = (LayerTableRecord)tr.GetObject(lt[tarName], OpenMode.ForRead);
                            _tarBOriginalColor = tarLtr.Color;
                            LinetypeTableRecord ltRec = tr.GetObject(tarLtr.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
                            _tarBOriginalLinetype = (ltRec != null) ? ltRec.Name : "Continuous";
                        }
                        tr.Commit();
                    }
                }

                if (chkInheritColorB.Checked)
                {
                    _chosenColorB = _srcColor;
                    UpdateColorBDisplay(_srcColor);
                }
                else
                {
                    _chosenColorB = _tarBOriginalColor;
                    UpdateColorBDisplay(_tarBOriginalColor);
                }

                if (chkInheritLinetypeB.Checked && !string.IsNullOrEmpty(_srcLinetype))
                {
                    if (cboLinetypeB.Items.Contains(_srcLinetype))
                        cboLinetypeB.SelectedItem = _srcLinetype;
                }
                else
                {
                    if (cboLinetypeB.Items.Contains(_tarBOriginalLinetype))
                        cboLinetypeB.SelectedItem = _tarBOriginalLinetype;
                }
            }
            catch { }
        }

        private void UpdateColorBDisplay(Autodesk.AutoCAD.Colors.Color c)
        {
            if (c == null) return;
            try
            {
                pnlColorB.BackColor = c.ColorValue;
                short idx = c.ColorIndex;
                lblColorDescB.Text = idx + " (" + GetColorName(idx) + ")";
            }
            catch { }
        }

        private void BtnChangeColorB_Click(object sender, EventArgs e)
        {
            try
            {
                Autodesk.AutoCAD.Windows.ColorDialog dlg = new Autodesk.AutoCAD.Windows.ColorDialog();
                if (_chosenColorB != null) dlg.Color = _chosenColorB;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _chosenColorB = dlg.Color;
                    chkInheritColorB.Checked = false;
                    UpdateColorBDisplay(_chosenColorB);
                }
            }
            catch { }
        }

        private void ChkInheritColorB_CheckedChanged(object sender, EventArgs e)
        {
            if (chkInheritColorB.Checked)
            {
                if (_srcColor != null)
                {
                    _chosenColorB = _srcColor;
                    UpdateColorBDisplay(_srcColor);
                }
            }
            else
            {
                if (_tarBOriginalColor != null)
                {
                    _chosenColorB = _tarBOriginalColor;
                    UpdateColorBDisplay(_tarBOriginalColor);
                }
            }
        }

        private void UpdateColorDisplay(Autodesk.AutoCAD.Colors.Color c)
        {
            if (c == null) return;
            try
            {
                pnlColor.BackColor = c.ColorValue;
                short idx = c.ColorIndex;
                lblColorDesc.Text = idx + " (" + GetColorName(idx) + ")";
            }
            catch { }
        }

        private string GetColorName(short colorIndex)
        {
            switch (colorIndex)
            {
                case 1: return "红";
                case 2: return "黄";
                case 3: return "绿";
                case 4: return "青";
                case 5: return "蓝";
                case 6: return "洋红";
                case 7: return "白/黑";
                case 8: return "灰";
                case 9: return "浅灰";
                default: return "索引" + colorIndex;
            }
        }

        private void BtnChangeColor_Click(object sender, EventArgs e)
        {
            try
            {
                Autodesk.AutoCAD.Windows.ColorDialog dlg = new Autodesk.AutoCAD.Windows.ColorDialog();
                if (_chosenColor != null)
                {
                    dlg.Color = _chosenColor;
                }
                else if (_srcColor != null)
                {
                    dlg.Color = _srcColor;
                }

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _chosenColor = dlg.Color;
                    chkInheritColor.Checked = false;
                    UpdateColorDisplay(_chosenColor);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("调起颜色选择面板失败: " + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ChkInheritColor_CheckedChanged(object sender, EventArgs e)
        {
            if (chkInheritColor.Checked)
            {
                if (_srcColor != null)
                {
                    _chosenColor = _srcColor;
                    UpdateColorDisplay(_srcColor);
                }
            }
        }

        private string ExtractBaseLayerName(string fullLayerName)
        {
            if (string.IsNullOrEmpty(fullLayerName)) return "";

            // 1. 优先提取括号内的中文，如 FF-Door【门】 -> 门
            int idx1 = fullLayerName.IndexOf('【');
            int idx2 = fullLayerName.IndexOf('】');
            if (idx1 >= 0 && idx2 > idx1)
            {
                return fullLayerName.Substring(idx1 + 1, idx2 - idx1 - 1).Trim();
            }

            idx1 = fullLayerName.IndexOf('[');
            idx2 = fullLayerName.IndexOf(']');
            if (idx1 >= 0 && idx2 > idx1)
            {
                return fullLayerName.Substring(idx1 + 1, idx2 - idx1 - 1).Trim();
            }

            idx1 = fullLayerName.IndexOf('（');
            idx2 = fullLayerName.IndexOf('）');
            if (idx1 >= 0 && idx2 > idx1)
            {
                return fullLayerName.Substring(idx1 + 1, idx2 - idx1 - 1).Trim();
            }

            // 2. 查找最后一个分隔符 - 或 _
            int lastSep = Math.Max(fullLayerName.LastIndexOf('-'), fullLayerName.LastIndexOf('_'));
            if (lastSep >= 0 && lastSep < fullLayerName.Length - 1)
            {
                return fullLayerName.Substring(lastSep + 1).Trim();
            }

            return fullLayerName;
        }

        public void RefreshLayerList()
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                List<string> layers = new List<string>();
                using (doc.LockDocument())
                {
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                        foreach (ObjectId id in lt)
                        {
                            LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                            layers.Add(ltr.Name);
                        }
                        tr.Commit();
                    }
                }

                layers.Sort();

                string oldSrc = cboSrc.SelectedItem as string;
                string oldTar = cboTar.SelectedItem as string;

                cboSrc.Items.Clear();
                cboTar.Items.Clear();

                foreach (string l in layers)
                {
                    cboSrc.Items.Add(l);
                    cboTar.Items.Add(l);
                }

                if (!string.IsNullOrEmpty(oldSrc) && cboSrc.Items.Contains(oldSrc)) cboSrc.SelectedItem = oldSrc;
                else if (cboSrc.Items.Count > 0) cboSrc.SelectedIndex = 0;

                if (!string.IsNullOrEmpty(oldTar) && cboTar.Items.Contains(oldTar)) cboTar.SelectedItem = oldTar;
                else if (cboTar.Items.Count > 1) cboTar.SelectedIndex = 1;
                else if (cboTar.Items.Count > 0) cboTar.SelectedIndex = 0;
            }
            catch { }
        }

        private void BtnPickSrc_Click(object sender, EventArgs e)
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            string picked = null;
            try
            {
                // 使用 StartUserInteraction 隐藏弹窗并彻底解冻 CAD 主窗口拾取消息
                using (EditorUserInteraction interaction = ed.StartUserInteraction(this))
                {
                    picked = PickLayerFromDrawing("\n>> 请在图面上点选【源图层】图元 (已支持穿透布局视口与嵌套块): ");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[提示] 拾取异常: " + ex.Message);
            }

            this.Activate();
            if (!string.IsNullOrEmpty(picked))
            {
                if (!cboSrc.Items.Contains(picked)) cboSrc.Items.Add(picked);
                cboSrc.SelectedItem = picked;

                // 用户明确要求：拾取原图层以后需要默认模式B
                rdoPickExist.Checked = true;
                UpdateModeUI(false);
                UpdateTargetPreview();
            }
        }

        private void BtnPickTar_Click(object sender, EventArgs e)
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            string picked = null;
            try
            {
                // 使用 StartUserInteraction 隐藏弹窗并彻底解冻 CAD 主窗口拾取消息
                using (EditorUserInteraction interaction = ed.StartUserInteraction(this))
                {
                    picked = PickLayerFromDrawing("\n>> 请在图面上点选【目标图层】图元 (已支持穿透布局视口与嵌套块): ");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[提示] 拾取异常: " + ex.Message);
            }

            this.Activate();
            if (!string.IsNullOrEmpty(picked))
            {
                rdoPickExist.Checked = true;
                if (!cboTar.Items.Contains(picked)) cboTar.Items.Add(picked);
                cboTar.SelectedItem = picked;
            }
        }

        private void BtnPickName_Click(object sender, EventArgs e)
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            string pickedName = null;
            try
            {
                using (EditorUserInteraction interaction = ed.StartUserInteraction(this))
                {
                    PromptNestedEntityOptions opt = new PromptNestedEntityOptions("\n>> 请在图面上点选包含名称的文字或图元 (支持穿透视口): ");
                    PromptNestedEntityResult res = ed.GetNestedEntity(opt);
                    if (res.Status == PromptStatus.OK)
                    {
                        using (doc.LockDocument())
                        {
                            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                            {
                                DBObject obj = tr.GetObject(res.ObjectId, OpenMode.ForRead);
                                Autodesk.AutoCAD.DatabaseServices.Viewport vp = obj as Autodesk.AutoCAD.DatabaseServices.Viewport;
                                if (vp != null && vp.Number > 1)
                                {
                                    string lyr = PickEntityInsideViewport(doc, tr, vp, res.PickedPoint);
                                    if (!string.IsNullOrEmpty(lyr)) pickedName = ExtractBaseLayerName(lyr);
                                }
                                else
                                {
                                    DBText dbText = obj as DBText;
                                    if (dbText != null)
                                    {
                                        pickedName = dbText.TextString.Trim();
                                    }
                                    else
                                    {
                                        MText mText = obj as MText;
                                        if (mText != null)
                                        {
                                            pickedName = mText.Text.Trim();
                                        }
                                        else
                                        {
                                            AttributeReference attRef = obj as AttributeReference;
                                            if (attRef != null)
                                            {
                                                pickedName = attRef.TextString.Trim();
                                            }
                                            else
                                            {
                                                Entity ent = obj as Entity;
                                                if (ent != null)
                                                {
                                                    pickedName = ExtractBaseLayerName(ent.Layer);
                                                }
                                            }
                                        }
                                    }
                                }
                                tr.Commit();
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[提示] 拾取异常: " + ex.Message);
            }

            this.Activate();
            if (!string.IsNullOrEmpty(pickedName))
            {
                txtBaseName.Text = pickedName;
            }
            UpdateTargetPreview();
        }

        private string PickLayerFromDrawing(string prompt)
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;

            Editor ed = doc.Editor;
            Database db = doc.Database;
            PromptNestedEntityOptions opt = new PromptNestedEntityOptions(prompt);
            opt.AllowNone = false;
            PromptNestedEntityResult res = ed.GetNestedEntity(opt);
            if (res.Status != PromptStatus.OK) return null;

            string layerName = null;
            using (doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Entity ent = tr.GetObject(res.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        // 1. 判断是否在布局空间点中了视口 (用户在图纸空间点击了视口内的模型图元)
                        Autodesk.AutoCAD.DatabaseServices.Viewport vp = ent as Autodesk.AutoCAD.DatabaseServices.Viewport;
                        if (vp != null && vp.Number > 1)
                        {
                            // 穿透布局视口深入模型空间精准溯源
                            layerName = PickEntityInsideViewport(doc, tr, vp, res.PickedPoint);
                        }
                        else
                        {
                            layerName = ent.Layer;
                            // 块内0层图元自动溯源至外层母块图层
                            if (string.Equals(layerName, "0", StringComparison.OrdinalIgnoreCase))
                            {
                                ObjectId[] containers = res.GetContainers();
                                if (containers != null && containers.Length > 0)
                                {
                                    Entity parentEnt = tr.GetObject(containers[0], OpenMode.ForRead) as Entity;
                                    if (parentEnt != null && !string.IsNullOrEmpty(parentEnt.Layer))
                                    {
                                        layerName = parentEnt.Layer;
                                    }
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            return layerName;
        }

        private string PickEntityInsideViewport(Document doc, Transaction tr, Autodesk.AutoCAD.DatabaseServices.Viewport vp, Point3d psPt)
        {
            Editor ed = doc.Editor;
            string layer = null;
            short oldCvport = 1;
            try
            {
                object cvObj = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("CVPORT");
                if (cvObj != null) oldCvport = Convert.ToInt16(cvObj);
            }
            catch { }

            try
            {
                // 1. 先尝试将图纸空间点击点坐标逆向投影到视口模型空间坐标系 (零切换、零黑屏、零命令行杂音)
                try
                {
                    Matrix3d msToPs = vp.CompoundObjectTransform;
                    Point3d msPt = psPt.TransformBy(msToPs.Inverse());

                    double boxSize = vp.ViewHeight * 0.01;
                    Point3d p1 = new Point3d(msPt.X - boxSize, msPt.Y - boxSize, msPt.Z);
                    Point3d p2 = new Point3d(msPt.X + boxSize, msPt.Y + boxSize, msPt.Z);

                    PromptSelectionResult selRes = ed.SelectCrossingWindow(p1, p2);
                    if (selRes.Status == PromptStatus.OK && selRes.Value != null && selRes.Value.Count > 0)
                    {
                        ObjectId pickedId = selRes.Value[0].ObjectId;
                        Entity modelEnt = tr.GetObject(pickedId, OpenMode.ForRead) as Entity;
                        if (modelEnt != null && !string.IsNullOrEmpty(modelEnt.Layer) && modelEnt.Layer != "0")
                        {
                            return modelEnt.Layer;
                        }
                    }
                }
                catch { }

                // 2. 仅在逆投影未直接命中时，才短暂穿透进入视口
                Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("CVPORT", vp.Number);

                // 3. 若坐标几何逆投影未直接命中（如点击了图块非线框缝隙），直接在已激活视口内精准二次拾取
                if (string.IsNullOrEmpty(layer) || layer == "0")
                {
                    PromptNestedEntityOptions nestOpt = new PromptNestedEntityOptions("\n>> [已自动穿透进入视口]，请直接点选视口内目标图元: ");
                    PromptNestedEntityResult nestRes = ed.GetNestedEntity(nestOpt);
                    if (nestRes.Status == PromptStatus.OK)
                    {
                        Entity nestEnt = tr.GetObject(nestRes.ObjectId, OpenMode.ForRead) as Entity;
                        if (nestEnt != null)
                        {
                            layer = nestEnt.Layer;
                            if (string.Equals(layer, "0", StringComparison.OrdinalIgnoreCase))
                            {
                                ObjectId[] conts = nestRes.GetContainers();
                                if (conts != null && conts.Length > 0)
                                {
                                    Entity pEnt = tr.GetObject(conts[0], OpenMode.ForRead) as Entity;
                                    if (pEnt != null && !string.IsNullOrEmpty(pEnt.Layer))
                                    {
                                        layer = pEnt.Layer;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[提示] 视口穿透异常: " + ex.Message);
            }
            finally
            {
                // 4. 无论成功与否，立即恢复回图纸空间！对用户完全透明无感！
                try
                {
                    Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("CVPORT", oldCvport);
                }
                catch { }
            }
            return layer;
        }

        private void BtnMerge_Click(object sender, EventArgs e)
        {
            string src = cboSrc.SelectedItem as string;
            string tar = "";

            if (rdoPrefix.Checked)
            {
                tar = txtPrefix.Text.Trim() + txtBaseName.Text.Trim();
                if (string.IsNullOrEmpty(txtBaseName.Text.Trim()))
                {
                    MessageBox.Show("请指定有效的目标图层名称！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SaveLastPrefix(txtPrefix.Text.Trim());
            }
            else
            {
                tar = cboTar.SelectedItem as string;
            }

            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tar))
            {
                MessageBox.Show("请先选择源图层和目标图层！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.Equals(src, tar, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("源图层与目标图层不能相同！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 1. 若在模式 A，自动在底层安全建层并继承属性 (颜色 + 线型 + 线宽 + 打印状态)
            string chosenLt = cboLinetype.SelectedItem as string ?? "Continuous";
            if (rdoPrefix.Checked)
            {
                EnsureTargetLayer(src, tar, _chosenColor, chkInheritColor.Checked, chosenLt, chkInheritLinetype.Checked);
            }
            else
            {
                string chosenLtB = cboLinetypeB.SelectedItem as string ?? "Continuous";
                EnsureTargetLayer(src, tar, _chosenColorB, chkInheritColorB.Checked, chosenLtB, chkInheritLinetypeB.Checked);
            }

            // 2. 执行极速原生 .NET 数据库合并 (0.01秒完成，绝不黑屏，绝不卡死，绝无弹窗干扰)
            this.Hide();
            try
            {
                int entCount = 0;
                int vpCount = 0;
                bool ok = FastLayerMerge(src, tar, chkByLayer.Checked, chkVP.Checked, out entCount, out vpCount);
                if (ok)
                {
                    Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        doc.Editor.WriteMessage(string.Format("\n★ [XY工具箱] 图层合并成功: [{0}] -> [{1}] (处理图元: {2}, 视口同步: {3})\n", 
                            src, tar, entCount, vpCount));
                    }
                }
                else
                {
                    // Fallback to LISP if needed
                    string bylayerFlag = chkByLayer.Checked ? "T" : "nil";
                    string vpFlag = chkVP.Checked ? "T" : "nil";
                    string lispCmd = string.Format("(execute_layer_merge \"{0}\" \"{1}\" {2} {3}) ", 
                        src.Replace("\"", "\\\""), 
                        tar.Replace("\"", "\\\""), 
                        bylayerFlag, 
                        vpFlag);
                    SendCadCommand(lispCmd);
                }
            }
            finally
            {
                this.Close();
            }
        }

        public static bool FastLayerMerge(string srcName, string tarName, bool forceByLayer, bool inheritVp, out int entCount, out int vpCount)
        {
            entCount = 0;
            vpCount = 0;
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return false;
                Database db = doc.Database;
                Editor ed = doc.Editor;

                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                        if (!lt.Has(srcName) || !lt.Has(tarName)) return false;

                        ObjectId srcId = lt[srcName];
                        ObjectId tarId = lt[tarName];

                        // 1. 若当前层为源层，切换至目标层
                        if (db.Clayer == srcId)
                        {
                            db.Clayer = tarId;
                        }

                        // 2. 毫秒级视口属性全量同步 (冻结/解冻 + 视口颜色/线型/线宽字典覆盖，绝不切换空间，绝不触发全图重绘)
                        if (inheritVp)
                        {
                            // A. 同步各布局视口的冻结与解冻状态 (穿透所有图纸空间浮动视口)
                            BlockTable btLayouts = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            foreach (ObjectId btrId in btLayouts)
                            {
                                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                                if (btr.IsLayout)
                                {
                                    Layout layout = (Layout)tr.GetObject(btr.LayoutId, OpenMode.ForRead);
                                    ObjectIdCollection vpIds = layout.GetViewports();
                                    // 索引 0 为图纸纸张自身总视口，索引 1 起为图纸浮动工作视口
                                    for (int vpIdx = 1; vpIdx < vpIds.Count; vpIdx++)
                                    {
                                        ObjectId vpId = vpIds[vpIdx];
                                        Autodesk.AutoCAD.DatabaseServices.Viewport vp = tr.GetObject(vpId, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Viewport;
                                        if (vp == null) continue;

                                        // 深度检测源图层是否在视口中被冻结 (支持 API 判断与扩展数据 XData 双重检测)
                                        bool isSrcFrozen = vp.IsLayerFrozenInViewport(srcId);
                                        if (!isSrcFrozen && vp.XData != null)
                                        {
                                            try
                                            {
                                                ResultBuffer xd = vp.GetXDataForApplication("ACAD");
                                                if (xd != null)
                                                {
                                                    foreach (TypedValue tv in xd)
                                                    {
                                                        if (tv.TypeCode == 1003 && string.Equals(Convert.ToString(tv.Value), srcName, StringComparison.OrdinalIgnoreCase))
                                                        {
                                                            isSrcFrozen = true;
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                            catch { }
                                        }

                                        bool isTarFrozen = vp.IsLayerFrozenInViewport(tarId);
                                        if (isSrcFrozen && !isTarFrozen)
                                        {
                                            vp.UpgradeOpen();
                                            ObjectIdCollection freezeColl = new ObjectIdCollection(new ObjectId[] { tarId });
                                            vp.FreezeLayersInViewport(freezeColl.GetEnumerator());
                                            vpCount++;
                                        }
                                        else if (!isSrcFrozen && isTarFrozen)
                                        {
                                            vp.UpgradeOpen();
                                            ObjectIdCollection thawColl = new ObjectIdCollection(new ObjectId[] { tarId });
                                            vp.ThawLayersInViewport(thawColl.GetEnumerator());
                                            vpCount++;
                                        }
                                    }
                                }
                            }

                            // B. 深度同步视口覆盖字典 (ADSK_XREC_LAYER_COLOR_OVR 等视口颜色/线型/线宽覆盖)
                            try
                            {
                                LayerTableRecord srcLtrRec = (LayerTableRecord)tr.GetObject(srcId, OpenMode.ForRead);
                                if (srcLtrRec.ExtensionDictionary != ObjectId.Null)
                                {
                                    DBDictionary srcDict = (DBDictionary)tr.GetObject(srcLtrRec.ExtensionDictionary, OpenMode.ForRead);
                                    LayerTableRecord tarLtrRec = (LayerTableRecord)tr.GetObject(tarId, OpenMode.ForWrite);
                                    if (tarLtrRec.ExtensionDictionary == ObjectId.Null)
                                    {
                                        tarLtrRec.CreateExtensionDictionary();
                                    }
                                    DBDictionary tarDict = (DBDictionary)tr.GetObject(tarLtrRec.ExtensionDictionary, OpenMode.ForWrite);

                                    string[] ovrKeys = new string[] {
                                        "ADSK_XREC_LAYER_COLOR_OVR",
                                        "ADSK_XREC_LAYER_LINETYPE_OVR",
                                        "ADSK_XREC_LAYER_LINEWT_OVR",
                                        "ADSK_XREC_LAYER_ALPHA_OVR",
                                        "ADSK_XREC_LAYER_PLOTSTYLE_OVR"
                                    };

                                    foreach (string key in ovrKeys)
                                    {
                                        if (srcDict.Contains(key))
                                        {
                                            ObjectId objId = srcDict.GetAt(key);
                                            DBObject srcObj = tr.GetObject(objId, OpenMode.ForRead);
                                            if (srcObj is Xrecord)
                                            {
                                                Xrecord srcXrec = (Xrecord)srcObj;
                                                Xrecord newXrec = new Xrecord();
                                                newXrec.Data = srcXrec.Data;
                                                if (tarDict.Contains(key))
                                                {
                                                    tarDict.Remove(key);
                                                }
                                                tarDict.SetAt(key, newXrec);
                                                tr.AddNewlyCreatedDBObject(newXrec, true);
                                                vpCount++;
                                            }
                                            else if (srcObj is DBDictionary)
                                            {
                                                DBDictionary srcSubDict = (DBDictionary)srcObj;
                                                DBDictionary tarSubDict;
                                                if (tarDict.Contains(key))
                                                {
                                                    tarSubDict = (DBDictionary)tr.GetObject(tarDict.GetAt(key), OpenMode.ForWrite);
                                                }
                                                else
                                                {
                                                    tarSubDict = new DBDictionary();
                                                    tarDict.SetAt(key, tarSubDict);
                                                    tr.AddNewlyCreatedDBObject(tarSubDict, true);
                                                }
                                                foreach (DBDictionaryEntry entry in srcSubDict)
                                                {
                                                    DBObject subObj = tr.GetObject(entry.Value, OpenMode.ForRead);
                                                    if (subObj is Xrecord)
                                                    {
                                                        Xrecord subXrec = (Xrecord)subObj;
                                                        Xrecord newSubXrec = new Xrecord();
                                                        newSubXrec.Data = subXrec.Data;
                                                        if (tarSubDict.Contains(entry.Key))
                                                        {
                                                            tarSubDict.Remove(entry.Key);
                                                        }
                                                        tarSubDict.SetAt(entry.Key, newSubXrec);
                                                        tr.AddNewlyCreatedDBObject(newSubXrec, true);
                                                        vpCount++;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        // 3. 极速穿透处理所有图元 (支持穿透图块内部图元与块属性，净化图元)
                        HashSet<ObjectId> blocksToPurge = new HashSet<ObjectId>();
                        BlockTable btAll = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                        foreach (ObjectId btrId in btAll)
                        {
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                            if (btr.IsFromExternalReference) continue;

                            foreach (ObjectId entId in btr)
                            {
                                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                                if (ent != null && ent.LayerId == srcId)
                                {
                                    ent.UpgradeOpen();
                                    ent.LayerId = tarId;
                                    if (forceByLayer)
                                    {
                                        ent.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
                                        ent.LinetypeId = db.ByLayerLinetype;
                                        ent.LineWeight = LineWeight.ByLayer;
                                    }
                                    ent.RecordGraphicsModified(true);
                                    entCount++;

                                    // 若图元是图块引用，穿透净化其块属性并记录块定义以便内部穿透
                                    BlockReference br = ent as BlockReference;
                                    if (br != null && forceByLayer)
                                    {
                                        blocksToPurge.Add(br.BlockTableRecord);
                                        foreach (ObjectId attId in br.AttributeCollection)
                                        {
                                            AttributeReference att = tr.GetObject(attId, OpenMode.ForWrite) as AttributeReference;
                                            if (att != null)
                                            {
                                                att.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
                                                att.LinetypeId = db.ByLayerLinetype;
                                                att.LineWeight = LineWeight.ByLayer;
                                                att.RecordGraphicsModified(true);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // 若勾选强制 ByLayer，穿透净化源图层中所有被引用的图块内部子图元
                        if (forceByLayer && blocksToPurge.Count > 0)
                        {
                            foreach (ObjectId blkDefId in blocksToPurge)
                            {
                                try
                                {
                                    BlockTableRecord blkDef = (BlockTableRecord)tr.GetObject(blkDefId, OpenMode.ForRead);
                                    if (blkDef.IsFromExternalReference) continue;
                                    foreach (ObjectId subEntId in blkDef)
                                    {
                                        Entity subEnt = tr.GetObject(subEntId, OpenMode.ForRead) as Entity;
                                        if (subEnt != null)
                                        {
                                            subEnt.UpgradeOpen();
                                            subEnt.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
                                            subEnt.LinetypeId = db.ByLayerLinetype;
                                            subEnt.LineWeight = LineWeight.ByLayer;
                                            subEnt.RecordGraphicsModified(true);
                                        }
                                    }
                                }
                                catch { }
                            }
                        }

                        // 4. 安全彻底删除源图层
                        LayerTableRecord srcLtr = (LayerTableRecord)tr.GetObject(srcId, OpenMode.ForWrite);
                        srcLtr.Erase(true);

                        tr.Commit();
                    }
                }

                // 5. 彻底移除全图 Regen (绝无“正在生成布局/模型”及全屏黑屏)，采用轻量平滑的 UpdateScreen 瞬间就绪
                try
                {
                    doc.Editor.UpdateScreen();
                }
                catch { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool EnsureTargetLayer(string srcName, string tarName, Autodesk.AutoCAD.Colors.Color targetColor, bool inheritColor, string targetLinetype, bool inheritLinetype)
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return false;
                Database db = doc.Database;

                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                        LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

                        LayerTableRecord srcLtr = null;
                        if (lt.Has(srcName))
                        {
                            srcLtr = (LayerTableRecord)tr.GetObject(lt[srcName], OpenMode.ForRead);
                        }

                        ObjectId chosenLtId = db.ContinuousLinetype;
                        if (!inheritLinetype && !string.IsNullOrEmpty(targetLinetype) && ltt.Has(targetLinetype))
                        {
                            chosenLtId = ltt[targetLinetype];
                        }
                        else if (srcLtr != null)
                        {
                            chosenLtId = srcLtr.LinetypeObjectId;
                        }

                        if (!lt.Has(tarName))
                        {
                            lt.UpgradeOpen();
                            LayerTableRecord tarLtr = new LayerTableRecord();
                            tarLtr.Name = tarName;

                            // 1. 深度继承源图层属性
                            if (srcLtr != null)
                            {
                                tarLtr.LinetypeObjectId = chosenLtId;
                                tarLtr.LineWeight = srcLtr.LineWeight;
                                tarLtr.IsPlottable = srcLtr.IsPlottable;
                                tarLtr.Description = srcLtr.Description;
                                try { tarLtr.ViewportVisibilityDefault = srcLtr.ViewportVisibilityDefault; } catch { }

                                if (inheritColor || targetColor == null)
                                {
                                    tarLtr.Color = srcLtr.Color;
                                }
                                else
                                {
                                    tarLtr.Color = targetColor;
                                }
                            }
                            else
                            {
                                if (targetColor != null) tarLtr.Color = targetColor;
                                tarLtr.LinetypeObjectId = chosenLtId;
                            }

                            lt.Add(tarLtr);
                            tr.AddNewlyCreatedDBObject(tarLtr, true);
                        }
                        else
                        {
                            // 目标层已存在，更新颜色与线型 (支持模式 A / 模式 B 继承源图层或自定义)
                            LayerTableRecord tarLtr = (LayerTableRecord)tr.GetObject(lt[tarName], OpenMode.ForWrite);
                            if (inheritColor && srcLtr != null)
                            {
                                tarLtr.Color = srcLtr.Color;
                            }
                            else if (targetColor != null)
                            {
                                tarLtr.Color = targetColor;
                            }

                            if (inheritLinetype && srcLtr != null)
                            {
                                tarLtr.LinetypeObjectId = srcLtr.LinetypeObjectId;
                            }
                            else if (chosenLtId != ObjectId.Null)
                            {
                                tarLtr.LinetypeObjectId = chosenLtId;
                            }
                        }
                        tr.Commit();
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private string LoadLastPrefix()
        {
            try
            {
                string appDir = System.IO.Path.GetDirectoryName(typeof(Commands).Assembly.Location);
                string iniPath = System.IO.Path.Combine(appDir, "cmd_config.ini");
                if (System.IO.File.Exists(iniPath))
                {
                    string[] lines = System.IO.File.ReadAllLines(iniPath, System.Text.Encoding.GetEncoding("GBK"));
                    foreach (string line in lines)
                    {
                        if (line.Trim().StartsWith("LastPrefix=", StringComparison.OrdinalIgnoreCase))
                        {
                            string p = line.Substring(line.IndexOf('=') + 1).Trim();
                            if (!string.IsNullOrEmpty(p)) return p;
                        }
                    }
                }
            }
            catch { }
            return "UDG-";
        }

        private void SaveLastPrefix(string prefix)
        {
            try
            {
                string appDir = System.IO.Path.GetDirectoryName(typeof(Commands).Assembly.Location);
                string iniPath = System.IO.Path.Combine(appDir, "cmd_config.ini");
                List<string> lines = new List<string>();
                if (System.IO.File.Exists(iniPath))
                {
                    lines.AddRange(System.IO.File.ReadAllLines(iniPath, System.Text.Encoding.GetEncoding("GBK")));
                }

                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Trim().StartsWith("LastPrefix=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = "LastPrefix=" + prefix;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    lines.Add("LastPrefix=" + prefix);
                }
                System.IO.File.WriteAllLines(iniPath, lines, System.Text.Encoding.GetEncoding("GBK"));
            }
            catch { }
        }

        private void SendCadCommand(string cmd)
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.SendStringToExecute(cmd, true, false, false);
                }
            }
            catch { }
        }
    }

    public class ToolboxControl : UserControl
    {
        private Panel pnlHeader;
        private Panel pnlBody;
        private PictureBox picLogo;
        private ToolTip toolTipHelp;

        private class CategorySection
        {
            public Panel HeaderPanel;
            public Panel ContentPanel;
            public Label ArrowLabel;
            public bool IsExpanded;
            public int ContentHeight;
            public int ItemCount;
            public string Title;
        }

        private List<CategorySection> _sections = new List<CategorySection>();

        public void RefreshLayerList() { }

        public ToolboxControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                          ControlStyles.UserPaint | 
                          ControlStyles.OptimizedDoubleBuffer | 
                          ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;

            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(28, 32, 40);
            this.Font = new Font("SimSun", 9F, FontStyle.Regular, GraphicsUnit.Point);

            toolTipHelp = new ToolTip();
            toolTipHelp.AutoPopDelay = 8000;
            toolTipHelp.InitialDelay = 350;
            toolTipHelp.ReshowDelay = 150;
            toolTipHelp.ShowAlways = true;

            // 1. 顶部纯净 Header (海龙纯正炭黑底色，反转亮白Logo + 锐利精细标题)
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 30;
            pnlHeader.BackColor = Color.FromArgb(18, 20, 25);

            picLogo = new PictureBox();
            picLogo.Location = new Point(8, 5);
            picLogo.Size = new Size(20, 20);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            try
            {
                Bitmap whiteLogo = Commands.GetInvertedWhiteLogo();
                if (whiteLogo != null)
                {
                    picLogo.Image = whiteLogo;
                }
            }
            catch { }
            pnlHeader.Controls.Add(picLogo);

            Label lblTitle = new Label();
            lblTitle.Text = "XY工具箱 V1.0";
            lblTitle.Font = new Font("SimSun", 9F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(34, 7);
            lblTitle.Size = new Size(180, 18);
            lblTitle.UseCompatibleTextRendering = true;
            pnlHeader.Controls.Add(lblTitle);

            this.Controls.Add(pnlHeader);

            // 2. 中间卡片手风琴容器 (海龙 1:1 炭黑底色 #21252C，单行高密度)
            pnlBody = new Panel();
            pnlBody.Dock = DockStyle.Fill;
            pnlBody.AutoScroll = true;
            pnlBody.BackColor = Color.FromArgb(33, 37, 44);
            this.Controls.Add(pnlBody);
            pnlBody.BringToFront();

            pnlBody.Resize += (s, e) => RelayoutSections();

            BuildHailongFunctionalSections();
        }

        private void BuildHailongFunctionalSections()
        {
            pnlBody.Controls.Clear();
            _sections.Clear();

            // 分类 1: 图层工具 (默认展开，单行高密度排布)
            CategorySection sec1 = CreateCategorySection("图层工具", 7, true);
            AddItemRowToSection(sec1, "🎨", "图层合并", "XHB", 
                "图层合并 (XHB)\n弹出独立可视化面板：前缀规范/层名修改/颜色线型/属性继承", 
                () => Commands.ShowLayerMergeDialog());
            AddItemRowToSection(sec1, "⚡", "极速合层", "XJH", 
                "极速合层 (XJH)\n免弹窗图面直选：连续点选源图元与目标图元，秒级彻底合并", 
                () => SendCadCommand("XJH "));
            AddItemRowToSection(sec1, "🧹", "清理空层", "XQTC", 
                "清理空层 (XQTC)\n0.05秒极速物理安全移除全部未使用空图层 (绝对保护0层/Defpoints/当前层)", 
                () => SendCadCommand("XQTC "));
            AddItemRowToSection(sec1, "❄️", "穿透冻结", "XDJ", 
                "穿透冻结 (XDJ)\n穿透嵌套块深度拾取子图元，视口/模型即时冻结图层 [连续点选时敲U撤销] (别名: XDK, XDF)", 
                () => SendCadCommand("XDJ "));
            AddItemRowToSection(sec1, "☀️", "逐步解冻", "XJD", 
                "逐步解冻 (XJD)\n单步逐级恢复历史冻结图层，连续回车逐步倒退，不干扰绘图 (全解命令: XHF)", 
                () => SendCadCommand("XJD "));
            AddItemRowToSection(sec1, "🔍", "图元探测", "XTC", 
                "图元探测 (XTC)\n穿透多重复杂嵌套图块，直接探测深层子图元属性与文字", 
                () => SendCadCommand("XTC "));
            AddItemRowToSection(sec1, "🔬", "图层对比", "LYCOMPARE", 
                "图层对比 (LYCOMPARE)\n加载引用图纸，自动比对图层/线型/颜色，智能评分并一键合并", 
                () => Commands.ShowLayerCompareDialog());
            RegisterSection(sec1);

            // 分类 2: 图块工具 (默认展开，方便直观查看)
            CategorySection sec2 = CreateCategorySection("图块工具", 5, true);
            AddItemRowToSection(sec2, "✂️", "穿透删除", "KSC", 
                "穿透删除 (KSC)\n免炸开直接点选或框选穿透删除块内图元，按 M 键自由切换【同名块全部联动】或【仅改当前(独占)】模式 (别名: XSC, BDE)", 
                () => SendCadCommand("KSC "));
            AddItemRowToSection(sec2, "🔤", "翻正反字", "FZW", 
                "翻正反字 (FZW / XFZ)\n智能穿透图块，大面积框选秒级原位翻正被镜像的反字，几何中心严格锁死，绝不位移 (支持属性字/块内普通字)", 
                () => SendCadCommand("FZW "));
            AddItemRowToSection(sec2, "✏️", "原位编块", "XKB", 
                "原位编块 (XKB / XLS)\n免在位编辑/免炸开，直接外部框选块内线条静默S拉伸或M平移，像没成块一样无感修改", 
                () => SendCadCommand("XKB "));
            AddItemRowToSection(sec2, "↔️", "属性平移", "XSY", 
                "属性平移 (XSY)\n单字穿透平移，实时Jig动态拖动预览，全图模型与视口同名图块全部联动同步", 
                () => SendCadCommand("XSY "));
            AddItemRowToSection(sec2, "🧹", "图块归零", "XGL", 
                "图块归零 (XGL)\n穿透嵌套块，彻底清洗全图所有图块内部图元至0层，颜色线型归ByLayer", 
                () => SendCadCommand("XGL "));
            RegisterSection(sec2);

            // 分类 3: 辅助工具 (默认折叠)
            CategorySection sec3 = CreateCategorySection("辅助工具", 2, false);
            AddItemRowToSection(sec3, "🔄", "视口重置", "XSK", 
                "视口重置 (XSK)\n一键快速清除当前图纸布局视口内多余的图层颜色与线型覆盖", 
                () => SendCadCommand("XSK "));
            AddItemRowToSection(sec3, "🗑️", "深度清理", "XQL", 
                "深度清理 (XQL)\n深度物理清理全图无用废弃图层、孤立图块、注册应用与无效字典", 
                () => SendCadCommand("XQL "));
            RegisterSection(sec3);

            // 分类 4: 系统设置 (默认展开)
            CategorySection sec4 = CreateCategorySection("系统设置", 3, true);
            AddItemRowToSection(sec4, "⌨️", "输入切换", "XSR", 
                "输入切换 (XSR)\n智能输入法自动切换：画图命令保持纯英文盲打，文字编辑自动切中文 (别名: XSRF, IME)", 
                () => SendCadCommand("XSR "));
            AddItemRowToSection(sec4, "⚙️", "快捷设置", "XKJ", 
                "快捷设置 (XKJ)\n自定义快捷键与别名管理配置窗口，修改即刻存盘热重载", 
                () => Commands.ShowShortcutConfigDialog());
            AddItemRowToSection(sec4, "🚀", "程序重载", "XCZ", 
                "程序重载 (XCZ)\n无需重启 AutoCAD，一秒级热重载最新核心代码与所有快捷键", 
                () => SendCadCommand("XCZ "));
            RegisterSection(sec4);

            RelayoutSections();
        }

        private CategorySection CreateCategorySection(string title, int itemCount, bool defaultExpanded)
        {
            CategorySection sec = new CategorySection();
            sec.Title = title;
            sec.ItemCount = itemCount;
            sec.IsExpanded = defaultExpanded;
            sec.ContentHeight = 0;

            // 头部折叠条 Panel (海龙标准紧凑高 23px)
            Panel pnlH = new Panel();
            pnlH.Height = 23;
            pnlH.Cursor = Cursors.Hand;
            pnlH.BackColor = defaultExpanded ? Color.FromArgb(36, 42, 52) : Color.FromArgb(28, 32, 40);

            Label arrow = new Label();
            arrow.Text = defaultExpanded ? "▼" : "▶";
            arrow.Font = new Font("SimSun", 7.5F, FontStyle.Regular);
            arrow.ForeColor = defaultExpanded ? Color.FromArgb(97, 175, 239) : Color.FromArgb(160, 170, 185);
            arrow.Location = new Point(4, 4);
            arrow.Size = new Size(16, 15);
            arrow.UseCompatibleTextRendering = true;
            pnlH.Controls.Add(arrow);
            sec.ArrowLabel = arrow;

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.Font = new Font("SimSun", 9F, FontStyle.Regular);
            lblTitle.ForeColor = Color.FromArgb(235, 240, 245);
            lblTitle.Location = new Point(22, 4);
            lblTitle.Size = new Size(160, 16);
            lblTitle.UseCompatibleTextRendering = true;
            pnlH.Controls.Add(lblTitle);

            // 底部 1px 细线
            pnlH.Paint += (s, e) => {
                using (Pen p = new Pen(Color.FromArgb(20, 23, 28)))
                {
                    e.Graphics.DrawLine(p, 0, pnlH.Height - 1, pnlH.Width, pnlH.Height - 1);
                }
            };

            Action toggleExpand = () => {
                sec.IsExpanded = !sec.IsExpanded;
                UpdateSectionHeaderStyle(sec);
                RelayoutSections();
            };

            pnlH.Click += (s, e) => toggleExpand();
            foreach (Control c in pnlH.Controls)
            {
                c.Cursor = Cursors.Hand;
                c.Click += (s, e) => toggleExpand();
            }

            sec.HeaderPanel = pnlH;

            // 内容容器 Panel
            Panel pnlC = new Panel();
            pnlC.BackColor = Color.FromArgb(22, 25, 31);
            pnlC.Visible = defaultExpanded;
            sec.ContentPanel = pnlC;

            return sec;
        }

        private void UpdateSectionHeaderStyle(CategorySection sec)
        {
            if (sec.IsExpanded)
            {
                sec.HeaderPanel.BackColor = Color.FromArgb(36, 42, 52);
                sec.ArrowLabel.Text = "▼";
                sec.ArrowLabel.ForeColor = Color.FromArgb(97, 175, 239);
            }
            else
            {
                sec.HeaderPanel.BackColor = Color.FromArgb(28, 32, 40);
                sec.ArrowLabel.Text = "▶";
                sec.ArrowLabel.ForeColor = Color.FromArgb(160, 170, 185);
            }
            sec.HeaderPanel.Invalidate();
        }

        private void AddItemRowToSection(CategorySection sec, string iconGlyph, string title, string hotkey, string desc, Action onClick)
        {
            int rowY = sec.ContentHeight;
            int rowH = 22; // 海龙经典紧凑行高 22px

            Panel row = new Panel();
            row.Location = new Point(0, rowY);
            row.Size = new Size(254, rowH);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.BackColor = Color.FromArgb(22, 25, 31);
            row.Cursor = Cursors.Hand;

            // 1. 图标 Glyph (16x16 视觉)
            Label lblIcon = new Label();
            lblIcon.Text = iconGlyph;
            lblIcon.Font = new Font("Segoe UI Emoji", 8.5F, FontStyle.Regular);
            lblIcon.ForeColor = Color.White;
            lblIcon.Location = new Point(4, 2);
            lblIcon.Size = new Size(18, 17);
            lblIcon.TextAlign = ContentAlignment.MiddleCenter;
            lblIcon.UseCompatibleTextRendering = false;
            row.Controls.Add(lblIcon);

            // 2. 功能名与快捷键合一 (海龙1:1经典排版: "图层合并<xhb>")
            // 采用天正/海龙经典宋体 SimSun 9F，细致精悍，绝不粗胀发黑
            Label lblText = new Label();
            lblText.Text = string.Format("{0}<{1}>", title, hotkey.ToLower());
            lblText.Font = new Font("SimSun", 9F, FontStyle.Regular);
            lblText.ForeColor = Color.FromArgb(245, 245, 245);
            lblText.Location = new Point(24, 4);
            lblText.Size = new Size(row.Width - 26, 16);
            lblText.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblText.UseCompatibleTextRendering = true;
            row.Controls.Add(lblText);

            // 3. 浮动详细说明 ToolTip
            toolTipHelp.SetToolTip(row, desc);
            toolTipHelp.SetToolTip(lblIcon, desc);
            toolTipHelp.SetToolTip(lblText, desc);

            // 4. 悬浮反馈 (Hover) 与底部分割线
            row.Paint += (s, e) => {
                using (Pen p = new Pen(Color.FromArgb(28, 32, 40)))
                {
                    e.Graphics.DrawLine(p, 4, row.Height - 1, row.Width - 4, row.Height - 1);
                }
            };

            Action onEnter = () => {
                row.BackColor = Color.FromArgb(38, 56, 84); // 海龙经典深天蓝高亮
                lblText.ForeColor = Color.White;
            };
            Action onLeave = () => {
                row.BackColor = Color.FromArgb(22, 25, 31);
                lblText.ForeColor = Color.FromArgb(245, 245, 245);
            };

            row.MouseEnter += (s, e) => onEnter();
            row.MouseLeave += (s, e) => onLeave();
            row.Click += (s, e) => onClick();

            foreach (Control c in row.Controls)
            {
                c.Cursor = Cursors.Hand;
                c.MouseEnter += (s, e) => onEnter();
                c.MouseLeave += (s, e) => onLeave();
                c.Click += (s, e) => onClick();
            }

            sec.ContentPanel.Controls.Add(row);
            sec.ContentHeight += rowH;
        }

        private void RegisterSection(CategorySection sec)
        {
            sec.ContentPanel.Height = sec.ContentHeight;
            pnlBody.Controls.Add(sec.HeaderPanel);
            pnlBody.Controls.Add(sec.ContentPanel);
            _sections.Add(sec);
        }

        private void RelayoutSections()
        {
            pnlBody.SuspendLayout();
            int currentY = 6;
            int availWidth = pnlBody.ClientSize.Width - 8;
            if (availWidth < 180) availWidth = 230;

            foreach (CategorySection sec in _sections)
            {
                sec.HeaderPanel.Location = new Point(4, currentY);
                sec.HeaderPanel.Width = availWidth;
                currentY += sec.HeaderPanel.Height;

                if (sec.IsExpanded)
                {
                    sec.ContentPanel.Visible = true;
                    sec.ContentPanel.Location = new Point(4, currentY);
                    sec.ContentPanel.Width = availWidth;
                    foreach (Control row in sec.ContentPanel.Controls)
                    {
                        row.Width = availWidth;
                    }
                    currentY += sec.ContentHeight + 4;
                }
                else
                {
                    sec.ContentPanel.Visible = false;
                }
            }

            pnlBody.ResumeLayout();
        }

        private void SendCadCommand(string cmd)
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.SendStringToExecute(cmd, true, false, false);
                }
            }
            catch { }
        }
    }
public class AttributeTextMoveJig : DrawJig
    {
        private Point3d _basePoint;
        private Point3d _currentPoint;
        private AttributeReference _previewAtt;

        public Vector3d DisplacementVector
        {
            get { return _basePoint.GetVectorTo(_currentPoint); }
        }

        public AttributeTextMoveJig(Point3d basePoint, AttributeReference attRef)
        {
            _basePoint = basePoint;
            _currentPoint = basePoint;
            _previewAtt = (AttributeReference)attRef.Clone();
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions opt = new JigPromptPointOptions();
            opt.Message = "\n>> 指定平移目标点或直接输入平移距离: ";
            opt.BasePoint = _basePoint;
            opt.UseBasePoint = true;
            opt.Cursor = CursorType.RubberBand;
            opt.UserInputControls = UserInputControls.Accept3dCoordinates | 
                                    UserInputControls.NoZeroResponseAccepted | 
                                    UserInputControls.GovernedByOrthoMode;

            PromptPointResult res = prompts.AcquirePoint(opt);
            if (res.Status == PromptStatus.Cancel || res.Status == PromptStatus.Error)
                return SamplerStatus.Cancel;

            if (res.Value.IsEqualTo(_currentPoint))
                return SamplerStatus.NoChange;

            _currentPoint = res.Value;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (_previewAtt != null)
            {
                Vector3d vec = DisplacementVector;
                using (AttributeReference temp = (AttributeReference)_previewAtt.Clone())
                {
                    temp.TransformBy(Matrix3d.Displacement(vec));
                    draw.Geometry.Draw(temp);
                }
            }
            return true;
        }
    }

    // ========================================================
    // 输入法智能自动切换管理器 (ImeAutoSwitchManager)
    // 工业级双模立体输入法驱动：
    // 1. 绘图与命令快捷键状态：100% 自动保持纯英文模式，按快捷键绝不误弹拼音选词框！
    // 2. 文字与多行文本编辑：100% 自动切换中文模式，点开文字直接输入汉字！
    // 3. 命令结束 / ESC取消：0.001秒无感恢复纯英文，随心盲打！
    // 4. 全链路监听：
    //    - Document.CommandWillStart / CommandEnded / CommandCancelled / CommandFailed
    //    - Document.LispWillStart / LispEnded / LispCancelled
    //    - Editor.PromptingForString / PromptedForString
    //    - Editor.EnteringQuiescentState
    //    - Application.PreTranslateMessage (实时拦截 A-Z / ESC 按键，根据当前上下文智能纠偏)
    //    - 实时读取 CMDNAMES 环境变量，双重锁定当前文字编辑状态
    // 5. 语言布局 + IMM32 + WM_IME_CONTROL 深度立体驱动：
    //    - 针对原生 C++ 窗口和子控件发送 WM_INPUTLANGCHANGEREQUEST
    //    - 通过 ImmGetDefaultIMEWnd 发送 IMC_SETOPENSTATUS / IMC_SETCONVERSIONMODE
    //    - 针对微软拼音穿透模拟 Shift 键，三重绝对保障！
    // ========================================================
    public static class ImeAutoSwitchManager
    {
        private static bool _enabled = true;
        private static bool _initialized = false;
        private static bool _isInTextCommand = false;

        private static InputLanguage _enLanguage = null;
        private static InputLanguage _zhLanguage = null;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll")]
        private static extern bool ImmGetConversionStatus(IntPtr hIMC, out int conversion, out int sentence);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetConversionStatus(IntPtr hIMC, int conversion, int sentence);

        [DllImport("imm32.dll")]
        private static extern bool ImmGetOpenStatus(IntPtr hIMC);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetOpenStatus(IntPtr hIMC, bool open);

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private const byte VK_SHIFT = 0x10;
        private const byte VK_ESCAPE = 0x1B;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
        private const uint KLF_ACTIVATE = 0x00000001;
        private const uint KLF_SETFORPROCESS = 0x00000100;
        private const uint WM_IME_CONTROL = 0x0283;
        private const int IMC_GETOPENSTATUS = 0x0005;
        private const int IMC_SETOPENSTATUS = 0x0006;
        private const int IMC_GETCONVERSIONMODE = 0x0001;
        private const int IMC_SETCONVERSIONMODE = 0x0002;
        private const int IME_CMODE_NATIVE = 0x0001;

        // 识别所有文字输入编辑相关命令 (包括各种前缀、天正命令、表格命令、属性定义等)
        private static readonly HashSet<string> TextCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TEXTEDIT", "TEXT", "DTEXT", "-TEXT", "MTEXT", "-MTEXT", "MTEDIT", "DDEDIT", "ED", "T", "MT", "DT",
            "ATTEDIT", "EATTEDIT", "-ATTEDIT", "ATTDEF", "-ATTDEF", "DDATTE", "BATTMAN", "FIND",
            "QLEADER", "LEADER", "MLEADER", "MLEADEREDIT", "DIMEDIT", "DIMTEDIT", "TEDIT", "TABLEDIT", "TABLE",
            "STYLE", "SPELL", "CHANGETEXT", "SCALETEXT", "JUSTIFYTEXT", "TXT2MTXT", "ARCTEXT", "RTEXT",
            // 天正建筑系列常用文字命令
            "TCH_TEXT", "TCH_MTEXT", "TCH_TEXTEDIT", "TCH_EDITTEXT", "TCH_DIM", "TCH_DIMEDIT", "TCH_WZ", "WZ", "DZ",
            "TCH_DRAWINGNAME", "TCH_INDEX", "TCH_DOOR_NUM", "TCH_WINDOW_NUM", "TCH_ELEV_SYM",
            "TCH_TABLE", "TCH_TABLEEDIT", "BG", "BGBJ", "TCH_BG", "TCH_BGBJ", "TCH_TITLE", "TCH_FIND",
            "TCH_LEADER", "TCH_YINCHU", "TCH_SECTION", "TCH_ROOM_NAME", "FJMC", "TCH_FJMC"
        };

        public static string CleanCommandName(string cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return "";
            string s = cmd.Trim();
            while (s.Length > 0 && (s.StartsWith("C:", StringComparison.OrdinalIgnoreCase) ||
                                  s.StartsWith("c:", StringComparison.OrdinalIgnoreCase) ||
                                  s.StartsWith("_") || s.StartsWith("'") || s.StartsWith(".")))
            {
                if (s.StartsWith("C:", StringComparison.OrdinalIgnoreCase) || s.StartsWith("c:", StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(2);
                }
                else
                {
                    s = s.Substring(1);
                }
                s = s.Trim();
            }
            return s;
        }

        public static bool IsTextEditingCommand(string cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return false;
            string clean = CleanCommandName(cmd);
            if (string.IsNullOrEmpty(clean)) return false;

            if (TextCommands.Contains(clean)) return true;

            string upper = clean.ToUpperInvariant();
            if (upper.Contains("TEXT") || upper.Contains("MTEXT") || upper.Contains("DTEXT"))
            {
                if (!upper.Contains("LAYER") && !upper.Contains("STYLE") && !upper.Contains("SCR"))
                    return true;
            }
            if (upper.Contains("ATTEDIT") || upper.Contains("ATTDEF") || upper.Contains("BATTMAN")) return true;
            if (upper.Contains("TABLEDIT") || upper.Contains("DIMEDIT") || upper.Contains("TEDIT") || upper.Contains("MLEADER")) return true;
            if (upper.StartsWith("TCH_") && (upper.Contains("TEXT") || upper.Contains("WZ") || upper.Contains("DZ") || upper.Contains("DIM") || upper.Contains("BG") || upper.Contains("SYM") || upper.Contains("NUM") || upper.Contains("NAME"))) return true;
            if (upper == "WZ" || upper == "DZ" || upper == "FJMC" || upper == "BG" || upper == "BGBJ" || upper == "ED" || upper == "FIND" || upper == "T" || upper == "DT" || upper == "MT") return true;

            return false;
        }

        private static bool IsTextLispExpression(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return false;
            string upper = expr.ToUpperInvariant();
            if (upper.Contains("C:WZ") || upper.Contains("C:DZ") || upper.Contains("C:FJMC") ||
                upper.Contains("C:BGBJ") || upper.Contains("C:BG") || upper.Contains("C:ED") ||
                upper.Contains("TCH_TEXT") || upper.Contains("TCH_WZ") || upper.Contains("TCH_DZ") ||
                upper.Contains("TEXT") || upper.Contains("MTEXT") || upper.Contains("DTEXT"))
            {
                return true;
            }
            return false;
        }

        public static bool IsInTextCommandContext()
        {
            try
            {
                object cmdVar = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("CMDNAMES");
                if (cmdVar != null)
                {
                    string cmdNames = cmdVar.ToString();
                    if (!string.IsNullOrEmpty(cmdNames))
                    {
                        string[] parts = cmdNames.Split(new char[] { '\'', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string part in parts)
                        {
                            if (IsTextEditingCommand(part))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool IsFocusOnTextEditControl()
        {
            try
            {
                IntPtr focusWnd = GetFocus();
                if (focusWnd == IntPtr.Zero) return false;
                System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                GetClassName(focusWnd, sb, 256);
                string cls = sb.ToString().ToLower();
                if (cls.Contains("edit") || cls.Contains("rich") || cls.Contains("textbox") ||
                    cls.Contains("scintilla") || cls.Contains("inplace") || cls.Contains("in-place"))
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // 1. 扫描系统已安装的输入语言
                DetectInputLanguages();

                // 2. 挂接文档与命令监听
                DocumentCollection dm = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager;
                dm.DocumentActivated += (s, e) => { if (e.Document != null) AttachDocument(e.Document); };
                dm.DocumentCreated += (s, e) => { if (e.Document != null) AttachDocument(e.Document); };

                if (dm.MdiActiveDocument != null)
                {
                    AttachDocument(dm.MdiActiveDocument);
                }

                // 3. 挂接空闲守护：确保进入命令行就绪态时自动为纯英文
                Autodesk.AutoCAD.ApplicationServices.Application.Idle += OnApplicationIdle;

                // 4. 挂接全局按键预处理 (PreTranslateMessage)：精准纠偏
                Autodesk.AutoCAD.ApplicationServices.Application.PreTranslateMessage += OnPreTranslateMessage;

                // 立即初始化为纯英文
                SetImeEnglish();
            }
            catch { }
        }

        private static void DetectInputLanguages()
        {
            try
            {
                foreach (InputLanguage lang in InputLanguage.InstalledInputLanguages)
                {
                    string culture = lang.Culture.Name.ToLower();
                    string layout = lang.LayoutName.ToLower();

                    if (_enLanguage == null && (culture.Contains("en") || layout.Contains("美式键盘") || layout.Contains("united states") || layout.Contains("us")))
                    {
                        _enLanguage = lang;
                    }

                    if (_zhLanguage == null && (culture.Contains("zh") || layout.Contains("中文") || layout.Contains("chinese") || layout.Contains("拼音") || layout.Contains("pinyin")))
                    {
                        _zhLanguage = lang;
                    }
                }
            }
            catch { }
        }

        private static void AttachDocument(Document doc)
        {
            try
            {
                doc.CommandWillStart -= OnCommandWillStart;
                doc.CommandEnded -= OnCommandEnded;
                doc.CommandCancelled -= OnCommandCancelled;
                doc.CommandFailed -= OnCommandFailed;
                doc.LispWillStart -= OnLispWillStart;
                doc.LispEnded -= OnLispEnded;
                doc.LispCancelled -= OnLispCancelled;

                doc.CommandWillStart += OnCommandWillStart;
                doc.CommandEnded += OnCommandEnded;
                doc.CommandCancelled += OnCommandCancelled;
                doc.CommandFailed += OnCommandFailed;
                doc.LispWillStart += OnLispWillStart;
                doc.LispEnded += OnLispEnded;
                doc.LispCancelled += OnLispCancelled;

                if (doc.Editor != null)
                {
                    doc.Editor.PromptingForString -= OnPromptingForString;
                    doc.Editor.PromptedForString -= OnPromptedForString;
                    doc.Editor.EnteringQuiescentState -= OnEnteringQuiescentState;

                    doc.Editor.PromptingForString += OnPromptingForString;
                    doc.Editor.PromptedForString += OnPromptedForString;
                    doc.Editor.EnteringQuiescentState += OnEnteringQuiescentState;
                }
            }
            catch { }
        }

        private static void OnPromptingForString(object sender, PromptStringOptionsEventArgs e)
        {
            if (!_enabled) return;
            _isInTextCommand = true;
            SetImeChinese();
        }

        private static void OnPromptedForString(object sender, PromptStringResultEventArgs e)
        {
            if (!_enabled) return;
            if (!IsInTextCommandContext())
            {
                _isInTextCommand = false;
                SetImeEnglish();
            }
        }

        private static void OnEnteringQuiescentState(object sender, EventArgs e)
        {
            if (!_enabled) return;
            _isInTextCommand = false;
            SetImeEnglish();
        }

        private static void OnLispWillStart(object sender, LispWillStartEventArgs e)
        {
            if (!_enabled) return;
            if (IsTextLispExpression(e.FirstLine))
            {
                _isInTextCommand = true;
                SetImeChinese();
            }
        }

        private static void OnLispEnded(object sender, EventArgs e)
        {
            if (!_enabled) return;
            if (IsInTextCommandContext())
            {
                _isInTextCommand = true;
                SetImeChinese();
            }
            else
            {
                _isInTextCommand = false;
                SetImeEnglish();
            }
        }

        private static void OnLispCancelled(object sender, EventArgs e)
        {
            if (!_enabled) return;
            _isInTextCommand = false;
            SetImeEnglish();
        }

        private static void OnCommandWillStart(object sender, CommandEventArgs e)
        {
            if (!_enabled) return;
            string cmd = e.GlobalCommandName;
            if (IsTextEditingCommand(cmd))
            {
                _isInTextCommand = true;
                SetImeChinese();
            }
            else
            {
                _isInTextCommand = false;
                SetImeEnglish();
            }
        }

        private static void OnCommandEnded(object sender, CommandEventArgs e)
        {
            if (!_enabled) return;
            if (IsInTextCommandContext())
            {
                _isInTextCommand = true;
                SetImeChinese();
            }
            else
            {
                _isInTextCommand = false;
                SetImeEnglish();
            }
        }

        private static void OnCommandCancelled(object sender, CommandEventArgs e)
        {
            if (!_enabled) return;
            _isInTextCommand = false;
            SetImeEnglish();
        }

        private static void OnCommandFailed(object sender, CommandEventArgs e)
        {
            if (!_enabled) return;
            _isInTextCommand = false;
            SetImeEnglish();
        }

        private static void OnApplicationIdle(object sender, EventArgs e)
        {
            if (!_enabled) return;
            try
            {
                if (_isInTextCommand || IsInTextCommandContext() || IsFocusOnTextEditControl())
                {
                    return;
                }

                IntPtr fg = GetForegroundWindow();
                IntPtr acadWnd = Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle;
                if (fg == acadWnd)
                {
                    if (IsImeInChineseMode(acadWnd))
                    {
                        SetImeEnglish();
                    }
                }
            }
            catch { }
        }

        private static void OnPreTranslateMessage(object sender, PreTranslateMessageEventArgs e)
        {
            if (!_enabled) return;
            try
            {
                // ESC 键按下：瞬间清除文字模式并恢复纯英文
                if (e.Message.message == 0x0100 && e.Message.wParam.ToInt32() == VK_ESCAPE)
                {
                    _isInTextCommand = false;
                    SetImeEnglish();
                    return;
                }

                // 1. 检查是否在文字编辑上下文中
                bool inText = _isInTextCommand || IsInTextCommandContext() || IsFocusOnTextEditControl();
                if (inText)
                {
                    _isInTextCommand = true;
                    // 用户在文字编辑时按下了字母键 (A-Z)，若输入法处于英文状态，瞬间切换为中文！
                    if (e.Message.message == 0x0100) // WM_KEYDOWN
                    {
                        int vk = e.Message.wParam.ToInt32();
                        if (vk >= 0x41 && vk <= 0x5A)
                        {
                            if (!IsImeInChineseMode())
                            {
                                SetImeChinese();
                            }
                        }
                    }
                    return; // 坚决不切英文！
                }

                // 2. 非文字状态（普通绘图/命令行就绪态）：当按下 A-Z 准备执行 CAD 快捷键时，自动保持纯英文
                if (e.Message.message == 0x0100)
                {
                    int vk = e.Message.wParam.ToInt32();
                    if (vk >= 0x41 && vk <= 0x5A)
                    {
                        if (IsImeInChineseMode())
                        {
                            SetImeEnglish();
                        }
                    }
                }
            }
            catch { }
        }

        public static bool IsImeInChineseMode(IntPtr hWnd = default(IntPtr))
        {
            try
            {
                if (hWnd == IntPtr.Zero)
                {
                    hWnd = GetFocus();
                    if (hWnd == IntPtr.Zero) hWnd = GetForegroundWindow();
                    if (hWnd == IntPtr.Zero) hWnd = Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle;
                }
                if (hWnd == IntPtr.Zero) return false;

                // 1. 如果当前键盘布局就是独立美式键盘 (en-US 0x04090409)，直接判定为非中文模式
                IntPtr hkl = GetKeyboardLayout(0);
                long hklVal = hkl.ToInt64() & 0xFFFFFFFF;
                if (hklVal == 0x04090409) return false;

                // 2. 检查 IME 默认窗口状态 (兼容搜狗输入法/微软拼音等)
                IntPtr imeWnd = ImmGetDefaultIMEWnd(hWnd);
                if (imeWnd != IntPtr.Zero)
                {
                    int status = (int)SendMessage(imeWnd, WM_IME_CONTROL, (IntPtr)IMC_GETOPENSTATUS, IntPtr.Zero);
                    int mode = (int)SendMessage(imeWnd, WM_IME_CONTROL, (IntPtr)IMC_GETCONVERSIONMODE, IntPtr.Zero);
                    if (status != 0 && (mode & IME_CMODE_NATIVE) != 0)
                    {
                        return true;
                    }
                }

                // 3. 检查传统 IMM 上下文
                IntPtr hIMC = ImmGetContext(hWnd);
                if (hIMC != IntPtr.Zero)
                {
                    int conv, sent;
                    bool open = ImmGetOpenStatus(hIMC);
                    bool got = ImmGetConversionStatus(hIMC, out conv, out sent);
                    ImmReleaseContext(hWnd, hIMC);
                    if (got && open && (conv & IME_CMODE_NATIVE) != 0)
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static void SetImeEnglish()
        {
            try
            {
                // 方案 A：优先切换系统纯英文键盘 (en-US)
                if (_enLanguage != null)
                {
                    ActivateKeyboardLayout(_enLanguage.Handle, KLF_ACTIVATE | KLF_SETFORPROCESS);
                    try { InputLanguage.CurrentInputLanguage = _enLanguage; } catch { }
                }

                IntPtr focusWnd = GetFocus();
                IntPtr fgWnd = GetForegroundWindow();
                IntPtr acadWnd = Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle;

                IntPtr[] wnds = new IntPtr[] { focusWnd, fgWnd, acadWnd };
                foreach (IntPtr w in wnds)
                {
                    if (w == IntPtr.Zero) continue;

                    if (_enLanguage != null)
                    {
                        PostMessage(w, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, _enLanguage.Handle);
                    }

                    // 方案 B：通过 WM_IME_CONTROL 关闭输入法开启状态与模式
                    IntPtr imeWnd = ImmGetDefaultIMEWnd(w);
                    if (imeWnd != IntPtr.Zero)
                    {
                        SendMessage(imeWnd, WM_IME_CONTROL, (IntPtr)IMC_SETOPENSTATUS, IntPtr.Zero);
                        SendMessage(imeWnd, WM_IME_CONTROL, (IntPtr)IMC_SETCONVERSIONMODE, IntPtr.Zero);
                    }

                    // 方案 D：传统 IMM32 上下文关闭
                    IntPtr hIMC = ImmGetContext(w);
                    if (hIMC != IntPtr.Zero)
                    {
                        int conv, sent;
                        if (ImmGetConversionStatus(hIMC, out conv, out sent))
                        {
                            conv &= ~IME_CMODE_NATIVE;
                            ImmSetConversionStatus(hIMC, conv, sent);
                        }
                        ImmSetOpenStatus(hIMC, false);
                        ImmReleaseContext(w, hIMC);
                    }
                }

                // 方案 C：针对微软拼音，如果仍处于中文模式，模拟 Shift 键穿透切换为英文
                if (IsImeInChineseMode())
                {
                    keybd_event(VK_SHIFT, 0, 0, 0);
                    keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
                }
            }
            catch { }
        }

        public static void SetImeChinese()
        {
            try
            {
                // 方案 A：优先切回中文键盘 (zh-CN)
                if (_zhLanguage != null)
                {
                    ActivateKeyboardLayout(_zhLanguage.Handle, KLF_ACTIVATE | KLF_SETFORPROCESS);
                    try { InputLanguage.CurrentInputLanguage = _zhLanguage; } catch { }
                }

                IntPtr focusWnd = GetFocus();
                IntPtr fgWnd = GetForegroundWindow();
                IntPtr acadWnd = Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle;

                IntPtr[] wnds = new IntPtr[] { focusWnd, fgWnd, acadWnd };
                foreach (IntPtr w in wnds)
                {
                    if (w == IntPtr.Zero) continue;

                    if (_zhLanguage != null)
                    {
                        PostMessage(w, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, _zhLanguage.Handle);
                    }

                    IntPtr imeWnd = ImmGetDefaultIMEWnd(w);
                    if (imeWnd != IntPtr.Zero)
                    {
                        SendMessage(imeWnd, WM_IME_CONTROL, (IntPtr)IMC_SETOPENSTATUS, (IntPtr)1);
                        SendMessage(imeWnd, WM_IME_CONTROL, (IntPtr)IMC_SETCONVERSIONMODE, (IntPtr)IME_CMODE_NATIVE);
                    }

                    IntPtr hIMC = ImmGetContext(w);
                    if (hIMC != IntPtr.Zero)
                    {
                        int conv, sent;
                        if (ImmGetConversionStatus(hIMC, out conv, out sent))
                        {
                            conv |= IME_CMODE_NATIVE;
                            ImmSetConversionStatus(hIMC, conv, sent);
                        }
                        ImmSetOpenStatus(hIMC, true);
                        ImmReleaseContext(w, hIMC);
                    }
                }

                // 针对微软拼音：如果当前处于英文模式，模拟 Shift 键瞬间切回中文
                if (!IsImeInChineseMode())
                {
                    keybd_event(VK_SHIFT, 0, 0, 0);
                    keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
                }
            }
            catch { }
        }

        public static void ToggleOrReport()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            _enabled = !_enabled;
            if (_enabled)
            {
                SetImeEnglish();
                ed.WriteMessage("\n[XY工具箱] 输入法智能自动切换: ★ 已开启 (画图/命令保持纯英文，文字编辑自动切中文)");
            }
            else
            {
                ed.WriteMessage("\n[XY工具箱] 输入法智能自动切换: ○ 已关闭");
            }

            string enName = _enLanguage != null ? _enLanguage.LayoutName : "未检测到独立美式键盘";
            string zhName = _zhLanguage != null ? _zhLanguage.LayoutName : "未检测到中文输入法";
            ed.WriteMessage(string.Format("\n  [诊断] 当前英文布局: {0} | 中文布局: {1} | 当前活动键盘: {2}\n",
                enName, zhName, InputLanguage.CurrentInputLanguage.LayoutName));
        }
    }
}
}