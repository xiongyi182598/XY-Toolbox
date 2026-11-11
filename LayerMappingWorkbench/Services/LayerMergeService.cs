using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using LayerMappingWorkbench.Models;

namespace LayerMappingWorkbench.Services
{
    public class MergeExecutionResult
    {
        public bool Success { get; set; }
        public int TotalLayersMapped { get; set; }
        public int TotalEntitiesModified { get; set; }
        public int BlocksInspected { get; set; }
        public int AttributesUpdated { get; set; }
        public int ViewportsSynced { get; set; }
        public int LayersPurged { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string ErrorMessage { get; set; }

        public MergeExecutionResult()
        {
            Success = false;
            ErrorMessage = string.Empty;
        }
    }

    /// <summary>
    /// 图层深度穿透合并引擎 (支持块定义递归穿透、属性同步、XRef避让、视口覆盖克隆与全量Purge)
    /// </summary>
    public class LayerMergeService
    {
        /// <summary>
        /// 从默认持久化规则文件重放执行当前图纸合并 (对应 LMW_APPLYLAST 命令)
        /// </summary>
        public MergeExecutionResult ExecuteFromDefaultConfigFile(Database db)
        {
            if (db == null)
            {
                MergeExecutionResult r = new MergeExecutionResult();
                r.ErrorMessage = "当前图纸数据库为空";
                return r;
            }

            string csvPath = LayerAlignmentEngine.GetDefaultRuleFilePath(db.Filename);
            if (!File.Exists(csvPath))
            {
                MergeExecutionResult r = new MergeExecutionResult();
                r.ErrorMessage = "未找到持久化映射规则库: " + csvPath;
                return r;
            }

            List<LayerMapRule> rules = LayerAlignmentEngine.LoadRulesFromCsv(csvPath);
            if (rules.Count == 0)
            {
                MergeExecutionResult r = new MergeExecutionResult();
                r.ErrorMessage = "规则库文件为空: " + csvPath;
                return r;
            }

            return ExecuteMerge(db, rules, true, true);
        }

        /// <summary>
        /// 执行工业级深度合并 (严格单一事务保障 + XRef防爆 + 属性引用同步)
        /// </summary>
        public MergeExecutionResult ExecuteMerge(
            Database db,
            IEnumerable<LayerMapRule> activeRules,
            bool forceByLayer,
            bool purgeEmptySourceLayers)
        {
            return ExecuteMerge(db, activeRules, null, forceByLayer, purgeEmptySourceLayers);
        }

        /// <summary>
        /// 执行工业级深度合并 (严格单一事务保障 + XRef防爆 + 属性引用同步 + 目标图层色彩/线型完整继承)
        /// </summary>
        public MergeExecutionResult ExecuteMerge(
            Database db,
            IEnumerable<LayerMapRule> activeRules,
            Dictionary<string, LayerMetaInfo> targetLayerDefs,
            bool forceByLayer,
            bool purgeEmptySourceLayers)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MergeExecutionResult result = new MergeExecutionResult();

            if (db == null || activeRules == null)
            {
                result.ErrorMessage = "数据库或规则参数为空";
                return result;
            }

            // 构建有效映射字典
            Dictionary<string, LayerMapRule> ruleMap = new Dictionary<string, LayerMapRule>(StringComparer.OrdinalIgnoreCase);
            foreach (LayerMapRule r in activeRules)
            {
                if (!string.IsNullOrEmpty(r.SourceLayer) &&
                    !string.IsNullOrEmpty(r.TargetLayer) &&
                    !string.Equals(r.SourceLayer, r.TargetLayer, StringComparison.OrdinalIgnoreCase))
                {
                    ruleMap[r.SourceLayer] = r;
                }
            }

            if (ruleMap.Count == 0)
            {
                result.Success = true;
                result.ErrorMessage = "无有效源-目标图层对需要合并";
                return result;
            }

            result.TotalLayersMapped = ruleMap.Count;

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            DocumentLock docLock = null;

            try
            {
                if (doc != null && doc.Database == db)
                {
                    docLock = doc.LockDocument();
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                    // 1. 确保目标图层存在并继承标准颜色与线型，临时解锁涉及的所有源图层与目标图层
                    EnsureTargetLayersAndUnlock(tr, lt, db, ruleMap, targetLayerDefs);

                    // 2. 深度穿透块定义 (BlockTableRecord)
                    foreach (ObjectId btrId in bt)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                        // 关键安全原则：绝对跳过外部参照与外部参照依赖块，防止 CAD 抛出只读异常闪退！
                        if (btr.IsFromExternalReference || btr.IsDependent)
                        {
                            continue;
                        }

                        result.BlocksInspected++;
                        bool btrUpgraded = false;

                        foreach (ObjectId entId in btr)
                        {
                            Entity ent = tr.GetObject(entId, OpenMode.ForRead, false, false) as Entity;
                            if (ent == null) continue;

                            LayerMapRule matchRule;
                            if (ruleMap.TryGetValue(ent.Layer, out matchRule))
                            {
                                if (!btrUpgraded)
                                {
                                    btr.UpgradeOpen();
                                    btrUpgraded = true;
                                }

                                ent.UpgradeOpen();
                                ent.Layer = matchRule.TargetLayer;

                                if (forceByLayer && matchRule.ForceByLayer)
                                {
                                    ent.ColorIndex = 256; // ByLayer
                                    ent.Linetype = "ByLayer";
                                    ent.LineWeight = LineWeight.ByLayer;
                                }

                                result.TotalEntitiesModified++;
                            }
                        }
                    }

                    // 3. 扫描模型与所有布局中的块引用 (BlockReference)，深度同步更新 AttributeReference 图层
                    result.AttributesUpdated += SyncBlockReferenceAttributes(tr, bt, ruleMap, forceByLayer);

                    // 4. 视口覆盖与局部冻结状态重定向
                    result.ViewportsSynced += SyncViewportLayerOverrides(tr, db, ruleMap);

                    // 5. 可选清理空出的源图层
                    if (purgeEmptySourceLayers)
                    {
                        result.LayersPurged += PurgeEmptySourceLayers(tr, db, lt, ruleMap.Keys);
                    }

                    tr.Commit();
                    result.Success = true;
                }
            }
            catch (System.Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "合并过程异常中断: " + ex.Message;
            }
            finally
            {
                if (docLock != null)
                {
                    docLock.Dispose();
                }
                sw.Stop();
                result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            }

            return result;
        }

        private static void EnsureTargetLayersAndUnlock(
            Transaction tr,
            LayerTable lt,
            Database db,
            Dictionary<string, LayerMapRule> ruleMap,
            Dictionary<string, LayerMetaInfo> targetLayerDefs)
        {
            // 保护当前图层 (Clayer)
            LayerTableRecord curLtr = (LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead);
            string curName = curLtr.Name;

            LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

            // 检查目标图层是否存在，不存在则创建；存在则按需更新颜色与线型（彻底杜绝合并后变白）
            foreach (KeyValuePair<string, LayerMapRule> kv in ruleMap)
            {
                string targetName = kv.Value.TargetLayer;
                LayerMetaInfo def = null;
                bool hasDef = targetLayerDefs != null && targetLayerDefs.TryGetValue(targetName, out def);

                if (!lt.Has(targetName))
                {
                    // ---- 目标图层不存在：新建并赋予标准颜色与线型 ----
                    LayerTableRecord newLtr = new LayerTableRecord();
                    newLtr.Name = targetName;

                    if (hasDef && def != null)
                    {
                        // 继承外部标准图纸的颜色
                        if (def.ColorIndex > 0 && def.ColorIndex <= 255)
                        {
                            newLtr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)def.ColorIndex);
                        }
                        else
                        {
                            newLtr.Color = Autodesk.AutoCAD.Colors.Color.FromRgb(def.Color.R, def.Color.G, def.Color.B);
                        }

                        // 继承外部标准图纸的线型
                        if (!string.IsNullOrEmpty(def.Linetype) && !string.Equals(def.Linetype, "Continuous", StringComparison.OrdinalIgnoreCase))
                        {
                            if (ltt.Has(def.Linetype))
                            {
                                newLtr.LinetypeObjectId = ltt[def.Linetype];
                            }
                            else
                            {
                                try
                                {
                                    db.LoadLineTypeFile(def.Linetype, "acad.lin");
                                    if (ltt.Has(def.Linetype))
                                    {
                                        newLtr.LinetypeObjectId = ltt[def.Linetype];
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    else
                    {
                        // 未加载外部标准图纸：智能继承源图层颜色与线型，彻底杜绝全变成白色
                        string srcName = kv.Key;
                        if (lt.Has(srcName))
                        {
                            LayerTableRecord srcLtr = (LayerTableRecord)tr.GetObject(lt[srcName], OpenMode.ForRead);
                            newLtr.Color = srcLtr.Color;
                            newLtr.LinetypeObjectId = srcLtr.LinetypeObjectId;
                        }
                        // 若源图层也不存在则保留默认（系统会给 ACI 7 白色，可接受）
                    }

                    lt.Add(newLtr);
                    tr.AddNewlyCreatedDBObject(newLtr, true);
                }
                else if (hasDef && def != null)
                {
                    // ---- 目标图层已存在：当有外部标准定义时，同步更新其颜色与线型 ----
                    LayerTableRecord existLtr = (LayerTableRecord)tr.GetObject(lt[targetName], OpenMode.ForRead);
                    bool needUpdate = false;

                    Autodesk.AutoCAD.Colors.Color newColor;
                    if (def.ColorIndex > 0 && def.ColorIndex <= 255)
                    {
                        newColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)def.ColorIndex);
                    }
                    else
                    {
                        newColor = Autodesk.AutoCAD.Colors.Color.FromRgb(def.Color.R, def.Color.G, def.Color.B);
                    }

                    if (existLtr.Color.ColorIndex != newColor.ColorIndex) needUpdate = true;

                    // 检查线型是否需要同步
                    string existLinetypeName = "Continuous";
                    if (existLtr.LinetypeObjectId != ObjectId.Null)
                    {
                        try
                        {
                            LinetypeTableRecord existLtRec = (LinetypeTableRecord)tr.GetObject(existLtr.LinetypeObjectId, OpenMode.ForRead);
                            existLinetypeName = existLtRec.Name;
                        }
                        catch { }
                    }
                    if (!string.IsNullOrEmpty(def.Linetype) && !string.Equals(existLinetypeName, def.Linetype, StringComparison.OrdinalIgnoreCase))
                    {
                        needUpdate = true;
                    }

                    if (needUpdate)
                    {
                        existLtr.UpgradeOpen();
                        existLtr.Color = newColor;

                        if (!string.IsNullOrEmpty(def.Linetype) && !string.Equals(def.Linetype, "Continuous", StringComparison.OrdinalIgnoreCase))
                        {
                            if (ltt.Has(def.Linetype))
                            {
                                existLtr.LinetypeObjectId = ltt[def.Linetype];
                            }
                            else
                            {
                                try
                                {
                                    db.LoadLineTypeFile(def.Linetype, "acad.lin");
                                    if (ltt.Has(def.Linetype))
                                    {
                                        existLtr.LinetypeObjectId = ltt[def.Linetype];
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }

            // 临时解锁所有涉及的源图层与目标图层
            foreach (ObjectId layId in lt)
            {
                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layId, OpenMode.ForRead);
                if (ruleMap.ContainsKey(ltr.Name))
                {
                    if (ltr.IsLocked)
                    {
                        ltr.UpgradeOpen();
                        ltr.IsLocked = false;
                    }
                }
            }

            // 若当前图层即将被合并，则先行安全切至其目标层或 "0" 层
            LayerMapRule curRule;
            if (ruleMap.TryGetValue(curName, out curRule))
            {
                if (lt.Has(curRule.TargetLayer))
                {
                    db.Clayer = lt[curRule.TargetLayer];
                }
                else if (lt.Has("0"))
                {
                    db.Clayer = lt["0"];
                }
            }
        }

        private static int SyncBlockReferenceAttributes(
            Transaction tr,
            BlockTable bt,
            Dictionary<string, LayerMapRule> ruleMap,
            bool forceByLayer)
        {
            int count = 0;

            foreach (ObjectId btrId in bt)
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                if (btr.IsFromExternalReference || btr.IsDependent) continue;

                // 仅扫描具有实际几何引用的容器 (如布局空间与普通块内部的子引用)
                foreach (ObjectId entId in btr)
                {
                    BlockReference br = tr.GetObject(entId, OpenMode.ForRead, false, false) as BlockReference;
                    if (br == null) continue;

                    // 检查块引用本身所在层
                    LayerMapRule brRule;
                    if (ruleMap.TryGetValue(br.Layer, out brRule))
                    {
                        br.UpgradeOpen();
                        br.Layer = brRule.TargetLayer;
                        if (forceByLayer && brRule.ForceByLayer)
                        {
                            br.ColorIndex = 256;
                            br.Linetype = "ByLayer";
                            br.LineWeight = LineWeight.ByLayer;
                        }
                    }

                    // 遍历属性引用集合
                    if (br.AttributeCollection != null && br.AttributeCollection.Count > 0)
                    {
                        foreach (ObjectId attId in br.AttributeCollection)
                        {
                            AttributeReference att = tr.GetObject(attId, OpenMode.ForRead, false, false) as AttributeReference;
                            if (att == null) continue;

                            LayerMapRule attRule;
                            if (ruleMap.TryGetValue(att.Layer, out attRule))
                            {
                                att.UpgradeOpen();
                                att.Layer = attRule.TargetLayer;
                                if (forceByLayer && attRule.ForceByLayer)
                                {
                                    att.ColorIndex = 256;
                                    att.Linetype = "ByLayer";
                                    att.LineWeight = LineWeight.ByLayer;
                                }
                                count++;
                            }
                        }
                    }
                }
            }

            return count;
        }

        private static int SyncViewportLayerOverrides(Transaction tr, Database db, Dictionary<string, LayerMapRule> ruleMap)
        {
            int vpCount = 0;
            try
            {
                DBDictionary layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                foreach (DBDictionaryEntry entry in layoutDict)
                {
                    Layout layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);

                    foreach (ObjectId entId in btr)
                    {
                        Viewport vp = tr.GetObject(entId, OpenMode.ForRead, false, false) as Viewport;
                        if (vp == null) continue;

                        vpCount++;
                        bool vpUpgraded = false;

                        foreach (KeyValuePair<string, LayerMapRule> kv in ruleMap)
                        {
                            string srcLay = kv.Key;
                            string tgtLay = kv.Value.TargetLayer;

                            if (!lt.Has(srcLay) || !lt.Has(tgtLay)) continue;

                            ObjectId srcId = lt[srcLay];
                            ObjectId tgtId = lt[tgtLay];

                            // 检查源图层在视口中是否被冻结
                            if (vp.IsLayerFrozenInViewport(srcId))
                            {
                                if (!vpUpgraded)
                                {
                                    vp.UpgradeOpen();
                                    vpUpgraded = true;
                                }
                                // 将目标图层同步冻结在视口中
                                ObjectIdCollection freezeCollection = new ObjectIdCollection(new ObjectId[] { tgtId });
                                vp.FreezeLayersInViewport(freezeCollection.GetEnumerator());
                            }
                        }
                    }
                }
            }
            catch { }

            return vpCount;
        }

        private static int PurgeEmptySourceLayers(Transaction tr, Database db, LayerTable lt, IEnumerable<string> sourceLayerNames)
        {
            int purged = 0;
            ObjectId curLayerId = db.Clayer;
            ObjectIdCollection candidateIds = new ObjectIdCollection();

            foreach (string name in sourceLayerNames)
            {
                if (string.Equals(name, "0", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Defpoints", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (lt.Has(name))
                {
                    ObjectId layId = lt[name];
                    if (layId != curLayerId)
                    {
                        candidateIds.Add(layId);
                    }
                }
            }

            if (candidateIds.Count == 0) return 0;

            try
            {
                db.Purge(candidateIds);
                foreach (ObjectId layId in candidateIds)
                {
                    try
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layId, OpenMode.ForWrite);
                        ltr.Erase(true);
                        purged++;
                    }
                    catch { }
                }
            }
            catch { }

            return purged;
        }
    }
}
