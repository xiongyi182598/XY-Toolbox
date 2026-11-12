using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using LayerMappingWorkbench.Models;
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

        // ========================================================
        // 诊断命令：自动化跑通测试移植流程并全面验证视口内属性
        // 命令: TEST_CLONE_RUN
        // ========================================================
        [CommandMethod("TEST_CLONE_RUN")]
        public void TestCloneRun()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            string logPath = @"C:\Users\1\.gemini\antigravity-ide\brain\f7989972-3d33-4112-8568-9be7a8448479\scratch\test_clone_log.txt";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            try
            {
                string srcPath = @"C:\Users\1\Documents\test02.dwg";
                sb.AppendLine("=== [TEST_CLONE_RUN] 自动化视口移植诊断测试启动 ===");
                sb.AppendLine("源 DWG: " + srcPath);
                sb.AppendLine("目标 DWG: " + doc.Database.Filename);

                // 1. 扫描当前目标图纸已存在的图层与模型空间图元分布
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                    sb.AppendLine("\n--- 目标图纸已有图层列表 ---");
                    foreach (ObjectId layId in lt)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layId, OpenMode.ForRead);
                        sb.AppendLine(string.Format("  Target Layer: {0} (Color={1}, Linetype={2})", ltr.Name, ltr.Color.ColorIndex, ltr.LinetypeObjectId));
                    }

                    ObjectId msId = SymbolUtilityServices.GetBlockModelSpaceId(doc.Database);
                    BlockTableRecord msBtr = (BlockTableRecord)tr.GetObject(msId, OpenMode.ForRead);
                    Dictionary<string, int> msCounts = new Dictionary<string, int>();
                    foreach (ObjectId entId in msBtr)
                    {
                        Entity ent = tr.GetObject(entId, OpenMode.ForRead, false, false) as Entity;
                        if (ent != null)
                        {
                            if (!msCounts.ContainsKey(ent.Layer)) msCounts[ent.Layer] = 0;
                            msCounts[ent.Layer]++;
                        }
                    }
                    sb.AppendLine("\n--- 目标图纸模型空间图元所属图层 ---");
                    foreach (KeyValuePair<string, int> kv in msCounts)
                    {
                        sb.AppendLine(string.Format("  MS Layer: {0} (图元数={1})", kv.Key, kv.Value));
                    }
                    tr.Commit();
                }

                // 2. 扫描源图纸中的视口与冻结图层
                using (Database srcDb = new Database(false, true))
                {
                    srcDb.ReadDwgFile(srcPath, FileOpenMode.OpenForReadAndAllShare, true, "");
                    using (Transaction srcTr = srcDb.TransactionManager.StartTransaction())
                    {
                        DBDictionary srcLayDict = (DBDictionary)srcTr.GetObject(srcDb.LayoutDictionaryId, OpenMode.ForRead);
                        if (srcLayDict.Contains("布局1"))
                        {
                            Layout srcLay = (Layout)srcTr.GetObject(srcLayDict.GetAt("布局1"), OpenMode.ForRead);
                            BlockTableRecord srcBtr = (BlockTableRecord)srcTr.GetObject(srcLay.BlockTableRecordId, OpenMode.ForRead);
                            sb.AppendLine("\n--- 源 DWG (test02.dwg) 布局1 视口详情 ---");
                            int sIdx = 0;
                            foreach (ObjectId entId in srcBtr)
                            {
                                Viewport vp = srcTr.GetObject(entId, OpenMode.ForRead, false, false) as Viewport;
                                if (vp != null)
                                {
                                    sb.AppendLine(string.Format("  Src VP #{0}: Number={1}, Layer={2}, W={3:F2}, H={4:F2}, Locked={5}, CustomScale={6:F6}",
                                        sIdx++, vp.Number, vp.Layer, vp.Width, vp.Height, vp.Locked, vp.CustomScale));
                                    ObjectIdCollection fz = vp.GetFrozenLayers();
                                    if (fz != null && fz.Count > 0)
                                    {
                                        sb.Append("    FrozenLayers (" + fz.Count + "): ");
                                        foreach (ObjectId fid in fz)
                                        {
                                            try
                                            {
                                                LayerTableRecord ltr = (LayerTableRecord)srcTr.GetObject(fid, OpenMode.ForRead);
                                                sb.Append(ltr.Name + "; ");
                                            }
                                            catch { }
                                        }
                                        sb.AppendLine();
                                    }
                                    else
                                    {
                                        sb.AppendLine("    FrozenLayers: (none)");
                                    }
                                }
                            }
                        }
                    }
                }

                // 3. 配置并执行视口移植
                ViewportCloneOptions opt = new ViewportCloneOptions();
                opt.SourceDwgPath = srcPath;
                opt.SourceLayoutName = "布局1";
                opt.TargetLayoutId = ObjectId.Null;
                opt.AlignMode = ModelAlignmentMode.Center;
                opt.TargetCustomScale = -1.0;

                string rulePath = LayerAlignmentEngine.GetDefaultRuleFilePath(doc.Database.Filename);
                sb.AppendLine("\n--- 规则加载 ---");
                sb.AppendLine("规则文件: " + rulePath);
                List<LayerMapRule> rules = LayerAlignmentEngine.LoadRulesFromCsv(rulePath);
                sb.AppendLine(string.Format("读取规则条数: {0}", rules.Count));
                foreach (LayerMapRule r in rules)
                {
                    if (!string.IsNullOrEmpty(r.SourceLayer) && !string.IsNullOrEmpty(r.TargetLayer))
                    {
                        opt.LayerMappingRules[r.SourceLayer] = r.TargetLayer;
                        sb.AppendLine(string.Format("  Rule: [{0}] -> [{1}]", r.SourceLayer, r.TargetLayer));
                    }
                }

                ViewportCloneService svc = new ViewportCloneService();
                ViewportCloneService.CloneResult res = svc.CloneViewportsToLayout(opt);
                sb.AppendLine(string.Format("\n移植结果: Success={0}, Viewports={1}, Msg={2}", res.Success, res.ViewportsCloned, res.Message));

                // 4. 检查目标图纸中最终视口冻结状态
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    LayoutManager lm = LayoutManager.Current;
                    ObjectId curLayId = ObjectId.Null;
                    if (!string.Equals(lm.CurrentLayout, "Model", StringComparison.OrdinalIgnoreCase))
                    {
                        curLayId = lm.GetLayoutId(lm.CurrentLayout);
                    }
                    else
                    {
                        DBDictionary tgtLayoutDict = (DBDictionary)tr.GetObject(doc.Database.LayoutDictionaryId, OpenMode.ForRead);
                        foreach (DBDictionaryEntry entry in tgtLayoutDict)
                        {
                            if (!string.Equals(entry.Key, "Model", StringComparison.OrdinalIgnoreCase))
                            {
                                curLayId = entry.Value;
                                break;
                            }
                        }
                    }
                    Layout curLay = (Layout)tr.GetObject(curLayId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(curLay.BlockTableRecordId, OpenMode.ForRead);
                    sb.AppendLine(string.Format("\n--- 目标布局 ({0}) 中现有所有视口状态 ---", curLay.LayoutName));
                    int vpIdx = 0;
                    foreach (ObjectId entId in btr)
                    {
                        Viewport vp = tr.GetObject(entId, OpenMode.ForRead, false, false) as Viewport;
                        if (vp != null)
                        {
                            sb.AppendLine(string.Format("  Target VP #{0}: Number={1}, Layer={2}, W={3:F2}, H={4:F2}, CustomScale={5:F6}",
                                vpIdx++, vp.Number, vp.Layer, vp.Width, vp.Height, vp.CustomScale));
                            ObjectIdCollection frozen = vp.GetFrozenLayers();
                            if (frozen != null && frozen.Count > 0)
                            {
                                sb.Append("    FrozenLayers (" + frozen.Count + "): ");
                                foreach (ObjectId fId in frozen)
                                {
                                    LayerTableRecord fLtr = (LayerTableRecord)tr.GetObject(fId, OpenMode.ForRead);
                                    sb.Append(fLtr.Name + "; ");
                                }
                                sb.AppendLine();
                            }
                            else
                            {
                                sb.AppendLine("    FrozenLayers: (none)");
                            }
                        }
                    }
                    tr.Commit();
                }

                sb.AppendLine("\n=== [TEST_CLONE_RUN] 测试执行完成 ===");
            }
            catch (System.Exception ex)
            {
                sb.AppendLine("\n[TEST_CLONE_RUN 异常]: " + ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                try
                {
                    System.IO.File.WriteAllText(logPath, sb.ToString(), System.Text.Encoding.UTF8);
                    ed.WriteMessage("\n[TEST_CLONE_RUN] 日志已写入: " + logPath + "\n");
                }
                catch { }
            }
        }
    }
}
