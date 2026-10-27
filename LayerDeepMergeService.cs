using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Geometry;

namespace XY_Deepening_Toolbox
{
    public class MergeOptions
    {
        public bool MigrateViewportOverrides { get; set; }
        public bool DeepBlockTraversal { get; set; }
        public bool ResetEntitiesByLayer { get; set; }
        public bool PurgeSourceLayersAfterMerge { get; set; }
        public bool NormalizeAttributesOnly { get; set; }

        public MergeOptions()
        {
            MigrateViewportOverrides = true;
            DeepBlockTraversal = true;
            ResetEntitiesByLayer = false;
            PurgeSourceLayersAfterMerge = true;
            NormalizeAttributesOnly = false;
        }
    }

    public class MergeReport
    {
        public int TotalLayersMerged { get; set; }
        public int TotalEntitiesProcessed { get; set; }
        public int BlocksProcessed { get; set; }
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
        public static MergeReport ExecuteDeepMerge(Database db, List<LayerMatchResult> mergePlan, MergeOptions options)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MergeReport report = new MergeReport();

            if (db == null || mergePlan == null || mergePlan.Count == 0)
            {
                report.SummaryMessage = "合并计划为空或图纸无效。";
                return report;
            }

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

                    // 1. 如果仅做“规范化属性对齐”，不改变图层名称，只统一颜色、线型和线宽
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

                                // 统一颜色
                                ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)item.BestMatchRefLayer.ColorIndex);

                                // 统一线型
                                if (!string.IsNullOrEmpty(item.BestMatchRefLayer.LinetypeName) && ltt.Has(item.BestMatchRefLayer.LinetypeName))
                                {
                                    ltr.LinetypeObjectId = ltt[item.BestMatchRefLayer.LinetypeName];
                                }

                                // 统一线宽与打印
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

                    // 2. 深度合并处理：构建映射表 (源图层名 -> 目标标准图层名)
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

                    if (layerMap.Count == 0)
                    {
                        report.SummaryMessage = "没有选择需要执行跨层合并的图层项。";
                        return report;
                    }

                    // 确保所有目标标准图层在当前图中存在（若不存在则自动克隆新建并赋予标准属性）
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

                    // 3. 视口全覆盖穿透迁移 (Viewport Freeze/Thaw 同步)
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
                        }
                        catch { }
                    }
                    report.ViewportsProcessed = viewportsCount;

                    // 4. 全图极速穿透处理所有图元 (包含模型空间、图纸空间及全部块定义，支持深层嵌套块)
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

                                    if (options.ResetEntitiesByLayer)
                                    {
                                        ent.ColorIndex = 256; // ByLayer
                                        ent.Linetype = "ByLayer";
                                        ent.LineWeight = LineWeight.ByLayer;
                                    }

                                    ent.RecordGraphicsModified(true);
                                    entitiesCount++;
                                }
                            }
                            catch { }
                        }
                    }

                    report.TotalEntitiesProcessed = entitiesCount;
                    report.BlocksProcessed = blocksCount;

                    // 5. 合并后物理安全抹除源图层 (通过官方 Purge 机制)
                    if (options.PurgeSourceLayersAfterMerge)
                    {
                        ObjectIdCollection toPurge = new ObjectIdCollection();
                        foreach (var kvp in layerMap)
                        {
                            string src = kvp.Key;
                            if (src == "0" || src.Equals("Defpoints", StringComparison.OrdinalIgnoreCase)) continue;
                            if (lt.Has(src))
                            {
                                ObjectId sId = lt[src];
                                if (sId != db.Clayer)
                                {
                                    toPurge.Add(sId);
                                }
                            }
                        }

                        if (toPurge.Count > 0)
                        {
                            db.Purge(toPurge);
                            foreach (ObjectId pId in toPurge)
                            {
                                try
                                {
                                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(pId, OpenMode.ForWrite);
                                    string erasedName = ltr.Name;
                                    ltr.Erase(true);
                                    report.SourceLayersPurged++;
                                    report.Details.Add(string.Format("物理抹除已清空源图层: [{0}]", erasedName));
                                }
                                catch { }
                            }
                        }
                    }

                    tr.Commit();
                }
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
                "全自动深度合并成功！共穿透迁移 {0} 个图层，迁移处理 {1} 个图元（深度穿透 {2} 个块定义与 {3} 个视口），安全清理 {4} 个旧图层，耗时 {5} ms",
                report.TotalLayersMerged, report.TotalEntitiesProcessed, report.BlocksProcessed, report.ViewportsProcessed, report.SourceLayersPurged, report.ElapsedMilliseconds);

            return report;
        }
    }
}
