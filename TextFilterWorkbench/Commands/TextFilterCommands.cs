using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using TextFilterWorkbench.Models;
using TextFilterWorkbench.Services;
using TextFilterWorkbench.Views;

[assembly: CommandClass(typeof(TextFilterWorkbench.Commands.TextFilterCommands))]

namespace TextFilterWorkbench.Commands
{
    /// <summary>
    /// 文字智能选择与过滤命令集合
    /// </summary>
    public class TextFilterCommands
    {
        // ========================================================
        // 核心命令 1: 极速文字吸管选择器 (XTX / XSW)
        // 点选图面任意文字 -> 自动提取字高/样式/颜色 -> 一键全选或框选
        // ========================================================
        [CommandMethod("XTX")]
        [CommandMethod("XSW")]
        public void QuickTextSelect()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                ed.WriteMessage("\n========================================================");
                ed.WriteMessage("\n【XY-Toolbox】智能文字选择与过滤 (快捷键: XTX / XSW)");
                ed.WriteMessage("\n========================================================");

                // 1. 点选参考文字 (或回车进入输入模式/打开面板)
                PromptEntityOptions opt = new PromptEntityOptions("\n请点选图纸中的参考文字 [或直接按回车以预设字高过滤/打开看板]: ");
                opt.SetRejectMessage("\n只能点选单行文字、多行文字或块属性！");
                opt.AddAllowedClass(typeof(DBText), true);
                opt.AddAllowedClass(typeof(MText), true);
                opt.AddAllowedClass(typeof(AttributeReference), true);

                PromptEntityResult res = ed.GetEntity(opt);

                TextFilterCriteria criteria = new TextFilterCriteria();

                if (res.Status == PromptStatus.OK)
                {
                    TextSampleInfo sample = null;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        sample = TextFilterService.ExtractSample(res.ObjectId, db, tr);
                        tr.Commit();
                    }

                    if (sample == null)
                    {
                        ed.WriteMessage("\n未能提取选中文本的属性。");
                        return;
                    }

                    ed.WriteMessage(string.Format("\n[已吸取参考] {0} | 字高: {1:0.###} | 样式: {2} | 颜色: {3} | 图层: {4}",
                        sample.EntityTypeName, sample.Height, sample.TextStyleName, sample.GetColorDisplayName(), sample.LayerName));

                    // 询问按何种规则过滤
                    PromptKeywordOptions kwOpt = new PromptKeywordOptions(
                        "\n请选择匹配维度 [仅字高(H)/仅样式(S)/仅颜色(C)/字高+样式(HS)/全匹配(A)/打开看板(D)] <仅字高(H)>: ");
                    kwOpt.Keywords.Add("H");
                    kwOpt.Keywords.Add("S");
                    kwOpt.Keywords.Add("C");
                    kwOpt.Keywords.Add("HS");
                    kwOpt.Keywords.Add("A");
                    kwOpt.Keywords.Add("D");
                    kwOpt.Keywords.Default = "H";

                    PromptResult kwRes = ed.GetKeywords(kwOpt);
                    if (kwRes.Status != PromptStatus.OK) return;

                    string choice = kwRes.StringResult.ToUpper();
                    if (choice == "D")
                    {
                        TextFilterDialog.ShowDialog(db, ed);
                        return;
                    }

                    // 配置条件
                    if (choice == "H" || choice == "HS" || choice == "A")
                    {
                        criteria.FilterByHeight = true;
                        criteria.HeightValue = sample.Height;
                        criteria.HeightTolerance = 0.05;
                    }
                    if (choice == "S" || choice == "HS" || choice == "A")
                    {
                        criteria.FilterByStyle = true;
                        criteria.TextStyleName = sample.TextStyleName;
                    }
                    if (choice == "C" || choice == "A")
                    {
                        criteria.FilterByColor = true;
                        criteria.ColorIndex = sample.EffectiveColorIndex;
                        criteria.ResolveByLayerEffectiveColor = true;
                    }
                    if (choice == "A")
                    {
                        criteria.FilterByLayer = true;
                        criteria.LayerName = sample.LayerName;
                    }
                }
                else if (res.Status == PromptStatus.None)
                {
                    // 用户直接按了回车
                    PromptKeywordOptions emptyKw = new PromptKeywordOptions("\n请选择操作 [输入字高过滤(H)/打开可视化看板(D)] <输入字高(H)>: ");
                    emptyKw.Keywords.Add("H");
                    emptyKw.Keywords.Add("D");
                    emptyKw.Keywords.Default = "H";
                    PromptResult ekRes = ed.GetKeywords(emptyKw);
                    if (ekRes.Status != PromptStatus.OK) return;

                    if (ekRes.StringResult.ToUpper() == "D")
                    {
                        TextFilterDialog.ShowDialog(db, ed);
                        return;
                    }

                    PromptDoubleOptions pdo = new PromptDoubleOptions("\n请输入要检索的目标字高 <3.5>: ");
                    pdo.DefaultValue = 3.5;
                    PromptDoubleResult pdr = ed.GetDouble(pdo);
                    if (pdr.Status != PromptStatus.OK) return;

                    criteria.FilterByHeight = true;
                    criteria.HeightValue = pdr.Value;
                    criteria.HeightTolerance = 0.05;
                }
                else
                {
                    return;
                }

                // 2. 选择范围 (直接回车全图，或框选)
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n请框选查找范围 (直接按回车在当前空间全图查找): ";
                PromptSelectionResult selRes = ed.GetSelection(pso);

                IEnumerable<ObjectId> scope = null;
                if (selRes.Status == PromptStatus.OK && selRes.Value != null)
                {
                    scope = selRes.Value.GetObjectIds();
                }

                // 3. 执行过滤
                List<ObjectId> matched = null;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    matched = TextFilterService.FilterTexts(db, tr, criteria, scope);
                    tr.Commit();
                }

                if (matched == null || matched.Count == 0)
                {
                    ed.WriteMessage("\n[XY-Toolbox] 未找到符合条件的文字。");
                    return;
                }

                // 4. 置入夹点选择集并亮显
                ed.SetImpliedSelection(matched.ToArray());
                ed.WriteMessage(string.Format("\n[XY-Toolbox] ★ 成功检索并选中 {0} 个匹配文字！已置入夹点选择集，可直接进行移动(M)、删除(E)或按 Ctrl+1 查看属性。\n", matched.Count));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n[错误] 执行文字选择失败: {0}\n", ex.Message));
            }
        }

        // ========================================================
        // 核心命令 2: 唤起可视化文字智能选择看板 (XTXD / XY_TEXTFILTER)
        // ========================================================
        [CommandMethod("XTXD")]
        [CommandMethod("XY_TEXTFILTER")]
        public void ShowTextFilterDialog()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                TextFilterDialog.ShowDialog(doc.Database, doc.Editor);
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage(string.Format("\n[错误] 打开文字智能过滤工作台失败: {0}\n", ex.Message));
            }
        }

        // ========================================================
        // 核心命令 3: 纯字高快速选择 (XTXH)
        // ========================================================
        [CommandMethod("XTXH")]
        public void SelectByHeight()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                PromptDoubleOptions pdo = new PromptDoubleOptions("\n[按字高选择] 请输入目标字高: ");
                pdo.AllowNegative = false;
                pdo.AllowZero = false;
                PromptDoubleResult pdr = ed.GetDouble(pdo);
                if (pdr.Status != PromptStatus.OK) return;

                TextFilterCriteria criteria = new TextFilterCriteria();
                criteria.FilterByHeight = true;
                criteria.HeightValue = pdr.Value;
                criteria.HeightTolerance = 0.05;

                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n请框选查找范围 (直接按回车查找全图): ";
                PromptSelectionResult selRes = ed.GetSelection(pso);
                IEnumerable<ObjectId> scope = (selRes.Status == PromptStatus.OK && selRes.Value != null) ? selRes.Value.GetObjectIds() : null;

                List<ObjectId> matched = null;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    matched = TextFilterService.FilterTexts(db, tr, criteria, scope);
                    tr.Commit();
                }

                if (matched == null || matched.Count == 0)
                {
                    ed.WriteMessage(string.Format("\n[XY-Toolbox] 未找到字高为 {0:0.###} 的文字。", pdr.Value));
                    return;
                }

                ed.SetImpliedSelection(matched.ToArray());
                ed.WriteMessage(string.Format("\n[XY-Toolbox] ★ 成功选中 {0} 个字高为 {1:0.###} 的文字！\n", matched.Count, pdr.Value));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n[错误] 按字高选择失败: {0}\n", ex.Message));
            }
        }

        // ========================================================
        // 核心命令 4: 纯颜色快速选择 (XTXC)
        // ========================================================
        [CommandMethod("XTXC")]
        public void SelectByColor()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                PromptIntegerOptions pio = new PromptIntegerOptions("\n[按颜色选择] 请输入颜色号 (1=红, 2=黄, 3=绿, 4=青, 5=蓝, 6=洋红, 7=白, 256=随层): ");
                pio.LowerLimit = 1;
                pio.UpperLimit = 256;
                PromptIntegerResult pir = ed.GetInteger(pio);
                if (pir.Status != PromptStatus.OK) return;

                TextFilterCriteria criteria = new TextFilterCriteria();
                criteria.FilterByColor = true;
                criteria.ColorIndex = (short)pir.Value;
                criteria.ResolveByLayerEffectiveColor = true;

                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n请框选查找范围 (直接按回车查找全图): ";
                PromptSelectionResult selRes = ed.GetSelection(pso);
                IEnumerable<ObjectId> scope = (selRes.Status == PromptStatus.OK && selRes.Value != null) ? selRes.Value.GetObjectIds() : null;

                List<ObjectId> matched = null;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    matched = TextFilterService.FilterTexts(db, tr, criteria, scope);
                    tr.Commit();
                }

                if (matched == null || matched.Count == 0)
                {
                    ed.WriteMessage(string.Format("\n[XY-Toolbox] 未找到颜色为 {0} 的文字。", TextFilterService.GetAciColorName((short)pir.Value)));
                    return;
                }

                ed.SetImpliedSelection(matched.ToArray());
                ed.WriteMessage(string.Format("\n[XY-Toolbox] ★ 成功选中 {0} 个颜色为 {1} 的文字！\n", matched.Count, TextFilterService.GetAciColorName((short)pir.Value)));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n[错误] 按颜色选择失败: {0}\n", ex.Message));
            }
        }

        // ========================================================
        // 自动化单元与集成测试命令: TEST_TEXTFILTER
        // ========================================================
        [CommandMethod("TEST_TEXTFILTER")]
        public void RunAutomatedTests()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            ed.WriteMessage("\n>>> 开始执行【文字智能选择与过滤引擎】自动化回归测试...");

            try
            {
                ObjectId greenLayerId = ObjectId.Null;
                ObjectId t1Id = ObjectId.Null;
                ObjectId t2Id = ObjectId.Null;
                ObjectId t3Id = ObjectId.Null;
                ObjectId m1Id = ObjectId.Null;
                ObjectId t4Id = ObjectId.Null;

                // 1. 创建测试实体
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    // 创建测试图层 TEST_GREEN (颜色为 3 - 绿)
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
                    string layerName = "TEST_GREEN";
                    if (lt.Has(layerName))
                    {
                        greenLayerId = lt[layerName];
                    }
                    else
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = layerName;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                        greenLayerId = lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }

                    // t1: 2.5 字高, 红色(1), 0层
                    DBText t1 = new DBText();
                    t1.Height = 2.5;
                    t1.TextString = "Sample 2.5 Red";
                    t1.Position = new Point3d(0, 0, 0);
                    t1.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                    t1Id = ms.AppendEntity(t1);
                    tr.AddNewlyCreatedDBObject(t1, true);

                    // t2: 3.5 字高, 随层(256) on TEST_GREEN
                    DBText t2 = new DBText();
                    t2.Height = 3.5;
                    t2.TextString = "Sample 3.5 Green ByLayer";
                    t2.Position = new Point3d(0, 10, 0);
                    t2.Layer = layerName;
                    t2.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                    t2Id = ms.AppendEntity(t2);
                    tr.AddNewlyCreatedDBObject(t2, true);

                    // t3: 3.5 字高, 蓝色(5), 0层
                    DBText t3 = new DBText();
                    t3.Height = 3.5;
                    t3.TextString = "Sample 3.5 Blue";
                    t3.Position = new Point3d(0, 20, 0);
                    t3.Color = Color.FromColorIndex(ColorMethod.ByAci, 5);
                    t3Id = ms.AppendEntity(t3);
                    tr.AddNewlyCreatedDBObject(t3, true);

                    // m1: 3.5 字高, 随层(256) on TEST_GREEN, 富文本内容
                    MText m1 = new MText();
                    m1.TextHeight = 3.5;
                    m1.Contents = @"\A1;{\C1;Sample MText} 3.5";
                    m1.Location = new Point3d(0, 30, 0);
                    m1.Layer = layerName;
                    m1.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                    m1Id = ms.AppendEntity(m1);
                    tr.AddNewlyCreatedDBObject(m1, true);

                    // t4: 5.0 字高, 黄色(2), 0层
                    DBText t4 = new DBText();
                    t4.Height = 5.0;
                    t4.TextString = "Sample 5.0 Yellow";
                    t4.Position = new Point3d(0, 40, 0);
                    t4.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                    t4Id = ms.AppendEntity(t4);
                    tr.AddNewlyCreatedDBObject(t4, true);

                    tr.Commit();
                }

                List<ObjectId> testScope = new List<ObjectId> { t1Id, t2Id, t3Id, m1Id, t4Id };

                // 测试用例 1: 按字高 3.5 过滤 (应命中 t2, t3, m1，共3个)
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    TextFilterCriteria c1 = new TextFilterCriteria();
                    c1.FilterByHeight = true;
                    c1.HeightValue = 3.5;
                    c1.HeightTolerance = 0.05;
                    List<ObjectId> res1 = TextFilterService.FilterTexts(db, tr, c1, testScope);
                    if (res1.Count != 3)
                    {
                        throw new System.Exception(string.Format("Test Case 1 失败: 期望命中3个，实际命中 {0} 个", res1.Count));
                    }
                    ed.WriteMessage("\n[PASS] 测试用例 1 (字高 3.5 过滤) 验证通过 (命中 3/3)");
                    tr.Commit();
                }

                // 测试用例 2: 按绿色(3) + 穿透解析随层 过滤 (t2 和 m1 是随层绿，应精准命中2个)
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    TextFilterCriteria c2 = new TextFilterCriteria();
                    c2.FilterByColor = true;
                    c2.ColorIndex = 3; // 绿色
                    c2.ResolveByLayerEffectiveColor = true;
                    List<ObjectId> res2 = TextFilterService.FilterTexts(db, tr, c2, testScope);
                    if (res2.Count != 2)
                    {
                        throw new System.Exception(string.Format("Test Case 2 失败: 期望穿透命中2个绿色随层文字，实际命中 {0} 个", res2.Count));
                    }
                    ed.WriteMessage("\n[PASS] 测试用例 2 (随层视效颜色穿透解析) 验证通过 (命中 2/2)");
                    tr.Commit();
                }

                // 测试用例 3: 按绿色(3) + 不穿透随层 (直接实体颜色没有3号绿，应命中0个)
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    TextFilterCriteria c3 = new TextFilterCriteria();
                    c3.FilterByColor = true;
                    c3.ColorIndex = 3;
                    c3.ResolveByLayerEffectiveColor = false;
                    List<ObjectId> res3 = TextFilterService.FilterTexts(db, tr, c3, testScope);
                    if (res3.Count != 0)
                    {
                        throw new System.Exception(string.Format("Test Case 3 失败: 期望不穿透命中0个，实际命中 {0} 个", res3.Count));
                    }
                    ed.WriteMessage("\n[PASS] 测试用例 3 (纯直接颜色精确模式) 验证通过 (命中 0/0)");
                    tr.Commit();
                }

                // 测试用例 4: MText 富文本清洗匹配 (匹配 'Sample MText')
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    TextFilterCriteria c4 = new TextFilterCriteria();
                    c4.FilterByContent = true;
                    c4.ContentPattern = "Sample MText";
                    c4.ContentMatchType = ContentMatchType.Contains;
                    List<ObjectId> res4 = TextFilterService.FilterTexts(db, tr, c4, testScope);
                    if (res4.Count != 1 || res4[0] != m1Id)
                    {
                        throw new System.Exception(string.Format("Test Case 4 失败: 期望命中富文本多行文字，实际命中 {0} 个", res4.Count));
                    }
                    ed.WriteMessage("\n[PASS] 测试用例 4 (MText 内联标签清洗与内容过滤) 验证通过 (命中 1/1)");
                    tr.Commit();
                }

                // 测试用例 5: 批量修改属性 (将 t2, t3, m1 的字高修改为 6.25)
                List<ObjectId> targetToMod = new List<ObjectId> { t2Id, t3Id, m1Id };
                int modCount = TextFilterService.BatchModifyTexts(db, targetToMod, 6.25, null, null, null);
                if (modCount != 3)
                {
                    throw new System.Exception(string.Format("Test Case 5 失败: 期望修改3个实体字高，实际修改 {0} 个", modCount));
                }
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBText t2Check = tr.GetObject(t2Id, OpenMode.ForRead) as DBText;
                    MText m1Check = tr.GetObject(m1Id, OpenMode.ForRead) as MText;
                    if (Math.Abs(t2Check.Height - 6.25) > 0.001 || Math.Abs(m1Check.TextHeight - 6.25) > 0.001)
                    {
                        throw new System.Exception("Test Case 5 失败: 实体字高未能正确更新为 6.25");
                    }
                    ed.WriteMessage("\n[PASS] 测试用例 5 (批量修改字高) 验证通过 (修改 3/3，字高更新为 6.25)");
                    tr.Commit();
                }

                ed.WriteMessage("\n\n★ ★ ★ ALL 5 TEXT FILTER TESTS PASSED SUCCESSFULLY! ★ ★ ★\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n[FAIL] 自动化测试失败: {0}\n", ex.Message));
            }
        }
    }
}

