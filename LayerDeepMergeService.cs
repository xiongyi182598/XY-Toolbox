using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;

namespace XY_Deepening_Toolbox
{
    public class MergeOptions
    {
        public bool MigrateViewportOverrides { get; set; }
        public bool DeepBlockTraversal { get; set; }
        public bool ResetEntitiesByLayer { get; set; }
        public bool PurgeSourceLayersAfterMerge { get; set; }
        public bool NormalizeAttributesOnly { get; set; }
        public bool RedefineSameNameBlocks { get; set; }
        public string RefDwgPath { get; set; }

        public MergeOptions()
        {
            MigrateViewportOverrides = true;
            DeepBlockTraversal = true;
            ResetEntitiesByLayer = true;
            PurgeSourceLayersAfterMerge = true;
            NormalizeAttributesOnly = false;
            RedefineSameNameBlocks = true;
            RefDwgPath = "";
        }
    }

    public class MergeReport
    {
        public int TotalLayersMerged { get; set; }
        public int TotalEntitiesProcessed { get; set; }
        public int BlocksProcessed { get; set; }
        public int BlocksRedefined { get; set; }
        public int ViewportsProcessed { get; set; }
        public int SourceLayersPurged { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public List<string> Details { get; set; }
        public string SummaryMessage { get; set; }

        public MergeReport()
        {
            Details = new List<string>();
        }
    }

    public static class LayerDeepMergeService
    {
        /// <summary>
        /// 执行工业级深度合并 (HB1/HB2 属性全量继承 + 视口覆盖克隆 + 同名块最高优先级重定义 + 自动记忆)
        /// </summary>
        public static MergeReport ExecuteDeepMerge(Database db, List<LayerMatchResult> mergePlan, MergeOptions options, List<BlockMatchInfo> blockMatches = null)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MergeReport report = new MergeReport();

            if (db == null || mergePlan == null || mergePlan.Count == 0)
            {
                report.SummaryMessage = "合并计划为空或图纸无效。";
                return report;
            }

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

                    // 1. 若仅做规范化属性对齐
                    if (options.NormalizeAttributesOnly)
                    {
                        int normalizedCount = 0;
                        foreach (LayerMatchResult item in mergePlan)
                        {
                            if (!item.IsSelectedForMerge || item.BestMatchRefLayer == null) continue;
                            if (lt.Has(item.CurrentLayer.Name))
                            {
                                ObjectId lyrId = lt[item.CurrentLayer.Name];
                                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lyrId, OpenMode.ForWrite);

                                ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)item.BestMatchRefLayer.ColorIndex);
                                if (!string.IsNullOrEmpty(item.BestMatchRefLayer.LinetypeName) && ltt.Has(item.BestMatchRefLayer.LinetypeName))
                                {
                                    ltr.LinetypeObjectId = ltt[item.BestMatchRefLayer.LinetypeName];
                                }
                                ltr.LineWeight = item.BestMatchRefLayer.LineWeightValue;
                                ltr.IsPlottable = item.BestMatchRefLayer.IsPlottable;

                                normalizedCount++;
                                report.Details.Add(string.Format("已规范化图层 [{0}] 属性 -> 颜色: {1}, 线型: {2}",
                                    ltr.Name, item.BestMatchRefLayer.ColorDescription, item.BestMatchRefLayer.LinetypeName));
                            }
                        }

                        tr.Commit();
                        sw.Stop();
                        report.ElapsedMilliseconds = sw.ElapsedMilliseconds;
                        report.TotalLayersMerged = normalizedCount;
                        report.SummaryMessage = string.Format("属性规范化完成！共成功对齐 {0} 个图层的颜色与线型标准。耗时 {1} ms",
                            normalizedCount, report.ElapsedMilliseconds);
                        return report;
                    }

                    // 2. 深度合并处理：构建映射字典 (源图层名 -> 目标标准图层名)
                    Dictionary<string, string> layerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, LayerInfo> targetLayerDefs = new Dictionary<string, LayerInfo>(StringComparer.OrdinalIgnoreCase);

                    foreach (LayerMatchResult item in mergePlan)
                    {
                        if (!item.IsSelectedForMerge) continue;
                        string src = item.CurrentLayer.Name;
                        string target = !string.IsNullOrEmpty(item.TargetLayerName) ? item.TargetLayerName : (item.BestMatchRefLayer != null ? item.BestMatchRefLayer.Name : null);

                        if (string.IsNullOrEmpty(target) || string.Equals(src, target, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        layerMap[src] = target;
                        if (item.BestMatchRefLayer != null && !targetLayerDefs.ContainsKey(target))
                        {
                            targetLayerDefs[target] = item.BestMatchRefLayer;
                        }
                    }

                    // 确保所有目标标准图层在当前图中存在（若不存在则自动新建并赋予标准属性）
                    foreach (var kvp in targetLayerDefs)
                    {
                        string targetName = kvp.Key;
                        LayerInfo tDef = kvp.Value;

                        if (!lt.Has(targetName))
                        {
                            LayerTableRecord newLtr = new LayerTableRecord();
                            newLtr.Name = targetName;
                            newLtr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)tDef.ColorIndex);
                            if (!string.IsNullOrEmpty(tDef.LinetypeName) && ltt.Has(tDef.LinetypeName))
                            {
                                newLtr.LinetypeObjectId = ltt[tDef.LinetypeName];
                            }
                            newLtr.LineWeight = tDef.LineWeightValue;
                            newLtr.IsPlottable = tDef.IsPlottable;

                            lt.Add(newLtr);
                            tr.AddNewlyCreatedDBObject(newLtr, true);
                            report.Details.Add(string.Format("自动引入标准新图层: [{0}]", targetName));
                        }
                    }

                    // 若当前活动图层（CLAYER）将被合并抹除，安全先切换到目标图层
                    ObjectId curLayerId = db.Clayer;
                    foreach (var map in layerMap)
                    {
                        if (lt.Has(map.Key) && lt.Has(map.Value))
                        {
                            if (lt[map.Key] == curLayerId)
                            {
                                db.Clayer = lt[map.Value];
                                curLayerId = lt[map.Value];
                            }
                        }
                    }

                    // 3. 采用 HB1/HB2 逻辑：视口属性深度全量继承 (冻结/解冻 + 覆盖字典克隆无损转移)
                    int viewportsCount = 0;
                    if (options.MigrateViewportOverrides)
                    {
                        try
                        {
                            BlockTable btLayouts = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            foreach (ObjectId btrId in btLayouts)
                            {
                                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                                if (btr.IsLayout)
                                {
                                    Layout layout = (Layout)tr.GetObject(btr.LayoutId, OpenMode.ForRead);
                                    ObjectIdCollection vpIds = layout.GetViewports();
                                    for (int vpIdx = 1; vpIdx < vpIds.Count; vpIdx++)
                                    {
                                        ObjectId vpId = vpIds[vpIdx];
                                        Viewport vp = tr.GetObject(vpId, OpenMode.ForRead) as Viewport;
                                        if (vp == null) continue;
                                        viewportsCount++;

                                        foreach (var map in layerMap)
                                        {
                                            if (lt.Has(map.Key) && lt.Has(map.Value))
                                            {
                                                ObjectId srcId = lt[map.Key];
                                                ObjectId tarId = lt[map.Value];

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
                                                                if (tv.TypeCode == 1003 && string.Equals(Convert.ToString(tv.Value), map.Key, StringComparison.OrdinalIgnoreCase))
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
                                                }
                                                else if (!isSrcFrozen && isTarFrozen)
                                                {
                                                    vp.UpgradeOpen();
                                                    ObjectIdCollection thawColl = new ObjectIdCollection(new ObjectId[] { tarId });
                                                    vp.ThawLayersInViewport(thawColl.GetEnumerator());
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // 深度克隆视口样式覆盖字典 (ADSK_XREC_LAYER_COLOR_OVR 等视口置灰/线型覆盖)
                            foreach (var map in layerMap)
                            {
                                if (lt.Has(map.Key) && lt.Has(map.Value))
                                {
                                    try
                                    {
                                        LayerTableRecord srcLtrRec = (LayerTableRecord)tr.GetObject(lt[map.Key], OpenMode.ForRead);
                                        if (srcLtrRec.ExtensionDictionary != ObjectId.Null)
                                        {
                                            DBDictionary srcDict = (DBDictionary)tr.GetObject(srcLtrRec.ExtensionDictionary, OpenMode.ForRead);
                                            LayerTableRecord tarLtrRec = (LayerTableRecord)tr.GetObject(lt[map.Value], OpenMode.ForWrite);
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
                                                        Xrecord newXrec = new Xrecord();
                                                        newXrec.Data = ((Xrecord)srcObj).Data;
                                                        if (tarDict.Contains(key)) tarDict.Remove(key);
                                                        tarDict.SetAt(key, newXrec);
                                                        tr.AddNewlyCreatedDBObject(newXrec, true);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                    }
                    report.ViewportsProcessed = viewportsCount;

                    // 4. 【核心特性】：相同图块名称优先级最高合并 (重定义块定义与实例图层归正)
                    int redefinedBlocksCount = 0;
                    if (options.RedefineSameNameBlocks && !string.IsNullOrEmpty(options.RefDwgPath) && File.Exists(options.RefDwgPath))
                    {
                        redefinedBlocksCount = ExecuteSameNameBlockRedefinition(db, tr, options.RefDwgPath, blockMatches);
                    }
                    report.BlocksRedefined = redefinedBlocksCount;

                    // 5. 采用 HB1/HB2 逻辑：全图极速穿透处理图元 (净化 ByLayer + 块属性穿透)
                    BlockTable btAll = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    int entitiesCount = 0;
                    int blocksCount = 0;

                    foreach (ObjectId btrId in btAll)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        if (btr.IsFromExternalReference) continue;
                        blocksCount++;

                        bool isModelOrPaper = btr.IsLayout;
                        if (!options.DeepBlockTraversal && !isModelOrPaper)
                        {
                            continue;
                        }

                        foreach (ObjectId entId in btr)
                        {
                            try
                            {
                                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                                if (ent != null && layerMap.ContainsKey(ent.Layer))
                                {
                                    string targetLayer = layerMap[ent.Layer];
                                    ent.UpgradeOpen();
                                    ent.Layer = targetLayer;

                                    // 净化为 ByLayer
                                    if (options.ResetEntitiesByLayer)
                                    {
                                        ent.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                        ent.LinetypeId = db.ByLayerLinetype;
                                        ent.LineWeight = LineWeight.ByLayer;
                                    }

                                    ent.RecordGraphicsModified(true);
                                    entitiesCount++;

                                    // 穿透净化图块引用的属性文字 (AttributeReference 保留文字内容，重置为 ByLayer)
                                    BlockReference br = ent as BlockReference;
                                    if (br != null && options.ResetEntitiesByLayer)
                                    {
                                        foreach (ObjectId attId in br.AttributeCollection)
                                        {
                                            AttributeReference att = tr.GetObject(attId, OpenMode.ForWrite) as AttributeReference;
                                            if (att != null)
                                            {
                                                att.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                                att.LinetypeId = db.ByLayerLinetype;
                                                att.LineWeight = LineWeight.ByLayer;
                                                att.RecordGraphicsModified(true);
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    report.TotalEntitiesProcessed = entitiesCount;
                    report.BlocksProcessed = blocksCount;

                    // 6. 采用 HB1/HB2 逻辑：物理安全抹除源图层 (Erase 物理清除)
                    if (options.PurgeSourceLayersAfterMerge)
                    {
                        foreach (var kvp in layerMap)
                        {
                            string src = kvp.Key;
                            if (src == "0" || src.Equals("Defpoints", StringComparison.OrdinalIgnoreCase)) continue;
                            if (lt.Has(src))
                            {
                                ObjectId sId = lt[src];
                                if (sId != db.Clayer)
                                {
                                    try
                                    {
                                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(sId, OpenMode.ForWrite);
                                        ltr.Erase(true);
                                        report.SourceLayersPurged++;
                                    }
                                    catch { }
                                }
                            }
                        }
                    }

                    tr.Commit();
                }

                // 7. 自动持久化记忆最后一次翻图方案 (供后续多套图纸一键复用)
                FlipRulePreset lastPreset = new FlipRulePreset();
                lastPreset.RefDwgPath = options.RefDwgPath;
                lastPreset.DeepBlockTraversal = options.DeepBlockTraversal;
                lastPreset.MigrateViewportOverrides = options.MigrateViewportOverrides;
                lastPreset.PurgeSourceLayersAfterMerge = options.PurgeSourceLayersAfterMerge;
                lastPreset.ResetEntitiesByLayer = options.ResetEntitiesByLayer;
                lastPreset.RedefineSameNameBlocks = options.RedefineSameNameBlocks;

                foreach (var item in mergePlan)
                {
                    if (item.IsSelectedForMerge && !string.IsNullOrEmpty(item.TargetLayerName) && item.TargetLayerName != item.CurrentLayer.Name)
                    {
                        lastPreset.LayerMappings[item.CurrentLayer.Name] = item.TargetLayerName;
                    }
                }

                if (blockMatches != null)
                {
                    foreach (var bm in blockMatches)
                    {
                        if (bm.IsSelectedForMerge && !string.IsNullOrEmpty(bm.TargetBlockName))
                        {
                            lastPreset.BlockMappings[bm.BlockName] = bm.TargetBlockName;
                        }
                    }
                }

                FlipRuleStorage.SaveLastPreset(lastPreset);
            }
            catch (System.Exception ex)
            {
                report.SummaryMessage = "合并过程异常: " + ex.Message;
                return report;
            }

            sw.Stop();
            report.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            report.TotalLayersMerged = mergePlan.FindAll(x => x.IsSelectedForMerge && !string.IsNullOrEmpty(x.TargetLayerName) && x.TargetLayerName != x.CurrentLayer.Name).Count;

            report.SummaryMessage = string.Format(
                "★ [XY工具箱] 全自动深度翻图合并成功！\n" +
                "• 穿透迁移业务图层: {0} 个\n" +
                "• 重定义同名图块: {1} 个 (最高优先级)\n" +
                "• 穿透处理图元: {2} 个 (含 {3} 个块定义与 {4} 个视口覆盖)\n" +
                "• 物理清理旧图层: {5} 个\n" +
                "• 本次规则已自动持久化记忆，后续图纸可直接一键秒翻！耗时: {6} ms",
                report.TotalLayersMerged, report.BlocksRedefined, report.TotalEntitiesProcessed,
                report.BlocksProcessed, report.ViewportsProcessed, report.SourceLayersPurged, report.ElapsedMilliseconds);

            return report;
        }

        /// <summary>
        /// 同名图块最高优先级重定义 (从参考图纸中克隆同名母块，并保持图面位置防跑偏)
        /// </summary>
        private static int ExecuteSameNameBlockRedefinition(Database targetDb, Transaction trTarget, string refDwgPath, List<BlockMatchInfo> blockMatches)
        {
            int count = 0;
            if (string.IsNullOrEmpty(refDwgPath) || !File.Exists(refDwgPath)) return 0;

            Database refDb = new Database(false, true);
            try
            {
                refDb.ReadDwgFile(refDwgPath, FileShare.Read, true, "");

                HashSet<string> targetBlockNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (blockMatches != null && blockMatches.Count > 0)
                {
                    foreach (var bm in blockMatches)
                    {
                        if (bm.IsSelectedForMerge && bm.IsSameNameMatched)
                        {
                            targetBlockNames.Add(bm.BlockName);
                        }
                    }
                }

                using (Transaction trRef = refDb.TransactionManager.StartTransaction())
                {
                    BlockTable btRef = (BlockTable)trRef.GetObject(refDb.BlockTableId, OpenMode.ForRead);
                    BlockTable btTarget = (BlockTable)trTarget.GetObject(targetDb.BlockTableId, OpenMode.ForWrite);

                    foreach (ObjectId refBtrId in btRef)
                    {
                        BlockTableRecord refBtr = (BlockTableRecord)trRef.GetObject(refBtrId, OpenMode.ForRead);
                        if (refBtr.IsLayout || refBtr.IsAnonymous || refBtr.IsFromExternalReference) continue;

                        string blkName = refBtr.Name;
                        if (targetBlockNames.Count > 0 && !targetBlockNames.Contains(blkName)) continue;

                        if (btTarget.Has(blkName))
                        {
                            // 目标图中存在同名块，进行子图元深层重定义
                            BlockTableRecord targetBtr = (BlockTableRecord)trTarget.GetObject(btTarget[blkName], OpenMode.ForWrite);

                            // 1. 清空当前块内已有子图元
                            foreach (ObjectId curSubId in targetBtr)
                            {
                                try
                                {
                                    DBObject curSub = trTarget.GetObject(curSubId, OpenMode.ForWrite);
                                    curSub.Erase(true);
                                }
                                catch { }
                            }

                            // 2. 从参考块中克隆所有图元
                            ObjectIdCollection idsToClone = new ObjectIdCollection();
                            foreach (ObjectId refSubId in refBtr)
                            {
                                idsToClone.Add(refSubId);
                            }

                            if (idsToClone.Count > 0)
                            {
                                IdMapping idMap = new IdMapping();
                                refDb.WblockCloneObjects(idsToClone, targetBtr.ObjectId, idMap, DuplicateRecordCloning.Replace, false);
                            }

                            count++;
                        }
                    }

                    trRef.Commit();
                }
            }
            catch { }
            finally
            {
                refDb.Dispose();
            }

            return count;
        }

        /// <summary>
        /// 一键套用记忆方案快速翻图 (无弹窗秒级执行)
        /// </summary>
        public static MergeReport QuickApplyLastPreset(Database db)
        {
            FlipRulePreset preset = FlipRuleStorage.LoadLastPreset();
            if (preset == null || preset.LayerMappings == null || preset.LayerMappings.Count == 0)
            {
                MergeReport r = new MergeReport();
                r.SummaryMessage = "未检测到历史翻图记忆记录。请先在【图层对比合并】面板(XTC/HB2)中进行一次标准对比翻图！";
                return r;
            }

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            List<LayerMatchResult> mergePlan = new List<LayerMatchResult>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId lyrId in lt)
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lyrId, OpenMode.ForRead);
                    string name = ltr.Name;

                    if (preset.LayerMappings.ContainsKey(name))
                    {
                        string target = preset.LayerMappings[name];
                        LayerMatchResult res = new LayerMatchResult();
                        res.CurrentLayer = new LayerInfo { Name = name };
                        res.TargetLayerName = target;
                        res.IsSelectedForMerge = true;
                        res.MatchReason = "记忆方案自动匹配";
                        mergePlan.Add(res);
                    }
                }
                tr.Commit();
            }

            if (mergePlan.Count == 0)
            {
                MergeReport r = new MergeReport();
                r.SummaryMessage = "当前图纸中未发现符合记忆方案的图层，无需合并。";
                return r;
            }

            MergeOptions opts = new MergeOptions();
            opts.RefDwgPath = preset.RefDwgPath;
            opts.DeepBlockTraversal = preset.DeepBlockTraversal;
            opts.MigrateViewportOverrides = preset.MigrateViewportOverrides;
            opts.PurgeSourceLayersAfterMerge = preset.PurgeSourceLayersAfterMerge;
            opts.ResetEntitiesByLayer = preset.ResetEntitiesByLayer;
            opts.RedefineSameNameBlocks = preset.RedefineSameNameBlocks;

            List<BlockMatchInfo> blockMatches = new List<BlockMatchInfo>();
            if (preset.BlockMappings != null)
            {
                foreach (var kvp in preset.BlockMappings)
                {
                    BlockMatchInfo bmi = new BlockMatchInfo();
                    bmi.BlockName = kvp.Key;
                    bmi.TargetBlockName = kvp.Value;
                    bmi.IsSameNameMatched = true;
                    bmi.IsSelectedForMerge = true;
                    blockMatches.Add(bmi);
                }
            }

            return ExecuteDeepMerge(db, mergePlan, opts, blockMatches);
        }
    }
}
