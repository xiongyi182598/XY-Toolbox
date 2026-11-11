using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using LayerMappingWorkbench.Services;
using LayerMappingWorkbench.Views;

[assembly: CommandClass(typeof(LayerMappingWorkbench.Commands.ExtensionCommands))]

namespace LayerMappingWorkbench.Commands
{
    /// <summary>
    /// 图层智能对位工作台与视口移植引擎 CAD 入口命令注册类 (全量独立解耦，绝不侵入原有命令)
    /// </summary>
    public class ExtensionCommands
    {
        // ========================================================
        // 命令 1：唤起双栏图层智能对位工作台
        // 快捷键: LMW_WORKBENCH / XDB (对标看板)
        // ========================================================
        [CommandMethod("LMW_WORKBENCH")]
        [CommandMethod("XDB")]
        public void ShowWorkbench()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                LayerMappingViewWindow.ShowWorkbench(doc.Database);
            }
            catch (System.Exception ex)
            {
                if (doc.Editor != null)
                {
                    doc.Editor.WriteMessage(string.Format("\n[错误] 唤起图层智能对位看板失败: {0}\n", ex.Message));
                }
            }
        }

        // ========================================================
        // 命令 2：直接根据上一次保存的规则批量重放当前图纸
        // 快捷键: LMW_APPLYLAST / XDB_APPLY
        // ========================================================
        [CommandMethod("LMW_APPLYLAST")]
        [CommandMethod("XDB_APPLY")]
        public void ApplyLastRules()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                LayerMergeService service = new LayerMergeService();
                MergeExecutionResult res = service.ExecuteFromDefaultConfigFile(doc.Database);

                if (res.Success)
                {
                    ed.WriteMessage(string.Format(
                        "\n★ [XY-Toolbox] 规则重放成功！已映射 {0} 层，穿透图元 {1} 个，同步属性 {2} 个，清理空层 {3} 个 (耗时 {4}ms)。\n",
                        res.TotalLayersMapped, res.TotalEntitiesModified, res.AttributesUpdated, res.LayersPurged, res.ElapsedMilliseconds));
                }
                else
                {
                    ed.WriteMessage(string.Format("\n[提示] 规则重放未执行: {0}\n", res.ErrorMessage));
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n[错误] 规则重放异常: {0}\n", ex.Message));
            }
        }

        // ========================================================
        // 命令 3：从外部 DWG 提取视口放入当前布局 (无损移植)
        // 快捷键: LMW_VPCLONE / XSK (视口克隆)
        // ========================================================
        [CommandMethod("LMW_VPCLONE")]
        [CommandMethod("XSK")]
        public void CloneViewport()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                try
                {
                    object fileDia = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("FILEDIA");
                    if (fileDia != null && Convert.ToInt32(fileDia) == 0)
                    {
                        Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("FILEDIA", 1);
                    }
                }
                catch { }

                System.Windows.Forms.OpenFileDialog dlg = new System.Windows.Forms.OpenFileDialog();
                dlg.Title = "选择源标准图纸 (提取视口)";
                dlg.Filter = "AutoCAD 图纸 (*.dwg)|*.dwg|所有文件 (*.*)|*.*";
                dlg.Multiselect = false;

                try
                {
                    Database curDb = HostApplicationServices.WorkingDatabase;
                    if (curDb != null && !string.IsNullOrEmpty(curDb.Filename))
                    {
                        string dir = System.IO.Path.GetDirectoryName(curDb.Filename);
                        if (System.IO.Directory.Exists(dir))
                        {
                            dlg.InitialDirectory = dir;
                        }
                    }
                }
                catch { }

                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                ViewportCloneDialog.ShowDialog(dlg.FileName);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n[错误] 视口无损移植引擎呼出失败: {0}\n", ex.Message));
            }
        }
    }
}
