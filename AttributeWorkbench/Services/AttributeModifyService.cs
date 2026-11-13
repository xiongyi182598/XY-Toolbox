using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AttributeWorkbench.Services
{
    /// <summary>
    /// 复合编号中单个属性部件信息 (如 FM / 乙 / 1023)
    /// </summary>
    public class AttributePartInfo
    {
        public ObjectId AttributeId { get; set; }
        public string Tag { get; set; }
        public string TextString { get; set; }
        public Point3d Position { get; set; }
        public Point3d AlignmentPoint { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public double WidthFactor { get; set; }
        public bool Invisible { get; set; }
        public int OriginalIndex { get; set; }
    }

    /// <summary>
    /// 被选中的属性文字目标信息
    /// </summary>
    public class AttributeTargetInfo
    {
        public ObjectId AttributeId { get; set; }
        public ObjectId ParentBlockId { get; set; }
        public ObjectId BlockTableRecordId { get; set; }
        public string BlockName { get; set; }
        public string Tag { get; set; }
        public string TextString { get; set; }
        public double CurrentHeight { get; set; }
        public Point3d PickedPoint { get; set; }
        public bool IsDynamicBlock { get; set; }
        public Matrix3d BlockTransform { get; set; }
        public HashSet<ObjectId> AllBlockReferenceIds { get; set; }

        // 门窗复合多段编号支持
        public List<AttributePartInfo> RelatedParts { get; set; }
        public bool HasMultipleParts { get { return RelatedParts != null && RelatedParts.Count > 1; } }
        public string CombinedText { get; set; }
        public bool IsAlreadyMerged { get; set; }

        public AttributeTargetInfo()
        {
            AllBlockReferenceIds = new HashSet<ObjectId>();
            RelatedParts = new List<AttributePartInfo>();
        }
    }

    /// <summary>
    /// 属性文字修改核心服务（支持字高修改、多部件合并/紧凑重排、实时平移与全空间同步）
    /// </summary>
    public static class AttributeModifyService
    {
        /// <summary>
        /// 从选中的属性实体中提取详细上下文信息并自动识别多部件编号
        /// </summary>
        public static AttributeTargetInfo ExtractTargetInfo(ObjectId attRefId, Point3d pickedPoint, ObjectId[] containers, Transaction tr)
        {
            if (attRefId.IsNull || !attRefId.IsValid) return null;

            AttributeReference attRef = tr.GetObject(attRefId, OpenMode.ForRead) as AttributeReference;
            if (attRef == null) return null;

            // 获取所属图块参照
            BlockReference parentBlk = tr.GetObject(attRef.OwnerId, OpenMode.ForRead) as BlockReference;
            if (parentBlk == null && containers != null && containers.Length > 0)
            {
                parentBlk = tr.GetObject(containers[0], OpenMode.ForRead) as BlockReference;
            }
            if (parentBlk == null) return null;

            BlockTableRecord btr = tr.GetObject(parentBlk.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return null;

            AttributeTargetInfo info = new AttributeTargetInfo
            {
                AttributeId = attRefId,
                ParentBlockId = parentBlk.ObjectId,
                BlockTableRecordId = btr.ObjectId,
                BlockName = btr.Name,
                Tag = attRef.Tag,
                TextString = attRef.TextString,
                CurrentHeight = attRef.Height,
                PickedPoint = pickedPoint,
                IsDynamicBlock = parentBlk.IsDynamicBlock,
                BlockTransform = parentBlk.BlockTransform
            };

            // 收集全图所有同名块引用（包含模型空间与所有布局图纸空间）
            foreach (ObjectId id in btr.GetBlockReferenceIds(true, true))
            {
                info.AllBlockReferenceIds.Add(id);
            }

            // 支持动态块匿名派生实例 (*U...)
            if (parentBlk.IsDynamicBlock)
            {
                BlockTableRecord dynBtr = tr.GetObject(parentBlk.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (dynBtr != null)
                {
                    foreach (ObjectId id in dynBtr.GetBlockReferenceIds(true, true))
                    {
                        info.AllBlockReferenceIds.Add(id);
                    }
                    foreach (ObjectId anonId in dynBtr.GetAnonymousBlockIds())
                    {
                        BlockTableRecord anonBtr = tr.GetObject(anonId, OpenMode.ForRead) as BlockTableRecord;
                        if (anonBtr != null)
                        {
                            foreach (ObjectId id in anonBtr.GetBlockReferenceIds(true, true))
                            {
                                info.AllBlockReferenceIds.Add(id);
                            }
                        }
                    }
                }
            }

            // 自动侦测并收集同块内的所有可见有效属性部件 (天正门窗多段编号如 FM / 乙 / 1023)
            List<AttributePartInfo> candidateParts = new List<AttributePartInfo>();
            int origIdx = 0;
            foreach (ObjectId attId in parentBlk.AttributeCollection)
            {
                AttributeReference a = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                if (a == null) continue;
                if (!a.Invisible && !string.IsNullOrEmpty(a.TextString) && a.TextString.Trim().Length > 0)
                {
                    candidateParts.Add(new AttributePartInfo
                    {
                        AttributeId = attId,
                        Tag = a.Tag,
                        TextString = a.TextString,
                        Position = a.Position,
                        AlignmentPoint = a.AlignmentPoint,
                        Height = a.Height,
                        Rotation = a.Rotation,
                        WidthFactor = a.WidthFactor > 0 ? a.WidthFactor : 0.7,
                        Invisible = a.Invisible,
                        OriginalIndex = origIdx++
                    });
                }
            }

            if (candidateParts.Count > 1)
            {
                // 沿文本旋转方向向量进行空间几何投影排序，严格还原自然阅读顺序
                double rot = attRef.Rotation;
                Vector3d u = new Vector3d(Math.Cos(rot), Math.Sin(rot), 0);
                candidateParts.Sort(delegate(AttributePartInfo p1, AttributePartInfo p2)
                {
                    double pr1 = p1.Position.X * u.X + p1.Position.Y * u.Y;
                    double pr2 = p2.Position.X * u.X + p2.Position.Y * u.Y;
                    if (Math.Abs(pr1 - pr2) > 1.0)
                    {
                        return pr1.CompareTo(pr2);
                    }
                    return p1.OriginalIndex.CompareTo(p2.OriginalIndex);
                });

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (AttributePartInfo p in candidateParts)
                {
                    sb.Append(p.TextString.Trim());
                }
                info.CombinedText = sb.ToString();
                info.RelatedParts = candidateParts;
            }
            else
            {
                info.CombinedText = attRef.TextString;
                info.RelatedParts = candidateParts;
            }

            return info;
        }

        /// <summary>
        /// 批量更新属性文字字高（支持更新母块定义与全图所有同名块）
        /// </summary>
        public static int ModifyAttributeHeight(Database db, AttributeTargetInfo target, double newHeight, bool syncAllBlocks)
        {
            if (target == null || newHeight <= 0.001) return 0;
            int syncCount = 0;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 1. 修改当前选中的图块内的属性 (如果是多部件，全部联动改字高)
                HashSet<string> targetTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (target.HasMultipleParts)
                {
                    foreach (AttributePartInfo p in target.RelatedParts) targetTags.Add(p.Tag);
                }
                else
                {
                    targetTags.Add(target.Tag);
                }

                BlockReference currentBlk = tr.GetObject(target.ParentBlockId, OpenMode.ForRead) as BlockReference;
                if (currentBlk != null)
                {
                    foreach (ObjectId attId in currentBlk.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && targetTags.Contains(att.Tag))
                        {
                            att.UpgradeOpen();
                            att.Height = newHeight;
                            att.AdjustAlignment(db);
                            att.RecordGraphicsModified(true);
                            if (attId == target.AttributeId) syncCount++;
                        }
                    }
                }

                // 如果仅修改当前单个块，提交并返回
                if (!syncAllBlocks)
                {
                    tr.Commit();
                    target.CurrentHeight = newHeight;
                    return syncCount > 0 ? 1 : 0;
                }

                // 2. 同步更新母块定义内部的 AttributeDefinition（确保后续 ATTSYNC 永久锁死新字高，不被重置）
                BlockTableRecord btr = tr.GetObject(target.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                if (btr != null)
                {
                    foreach (ObjectId entId in btr)
                    {
                        AttributeDefinition attDef = tr.GetObject(entId, OpenMode.ForRead) as AttributeDefinition;
                        if (attDef != null && targetTags.Contains(attDef.Tag))
                        {
                            attDef.UpgradeOpen();
                            attDef.Height = newHeight;
                            attDef.AdjustAlignment(db);
                            attDef.RecordGraphicsModified(true);
                        }
                    }
                }

                // 3. 同步全图所有同名块引用的同名属性
                foreach (ObjectId refId in target.AllBlockReferenceIds)
                {
                    if (refId == target.ParentBlockId) continue; // 当前块前面已修改

                    BlockReference bRef = tr.GetObject(refId, OpenMode.ForRead) as BlockReference;
                    if (bRef == null) continue;

                    bool touched = false;
                    foreach (ObjectId attId in bRef.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && targetTags.Contains(att.Tag))
                        {
                            att.UpgradeOpen();
                            att.Height = newHeight;
                            att.AdjustAlignment(db);
                            att.RecordGraphicsModified(true);
                            touched = true;
                        }
                    }
                    if (touched) syncCount++;
                }

                tr.Commit();
                target.CurrentHeight = newHeight;
            }

            return syncCount;
        }

        /// <summary>
        /// 方案 1: 合并多段编号为单属性文字 (默认推荐，彻底杜绝分散)
        /// 将主属性内容设置为完整拼接字符串 (如 FM乙1023)，其余辅助属性置空并隐藏
        /// </summary>
        public static int MergeMultiPartAttributes(Database db, AttributeTargetInfo target, bool syncAllBlocks)
        {
            if (target == null || !target.HasMultipleParts || target.RelatedParts.Count < 2) return 0;
            int syncCount = 0;

            string primaryTag = target.RelatedParts[0].Tag;
            List<string> secondaryTags = new List<string>();
            for (int i = 1; i < target.RelatedParts.Count; i++)
            {
                secondaryTags.Add(target.RelatedParts[i].Tag);
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 1. 修改当前图块
                BlockReference currentBlk = tr.GetObject(target.ParentBlockId, OpenMode.ForRead) as BlockReference;
                if (currentBlk != null)
                {
                    AttributeReference primAtt = null;
                    List<AttributeReference> secAtts = new List<AttributeReference>();

                    foreach (ObjectId attId in currentBlk.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att == null) continue;
                        if (string.Equals(att.Tag, primaryTag, StringComparison.OrdinalIgnoreCase))
                        {
                            primAtt = att;
                        }
                        else
                        {
                            foreach (string sTag in secondaryTags)
                            {
                                if (string.Equals(att.Tag, sTag, StringComparison.OrdinalIgnoreCase))
                                {
                                    secAtts.Add(att);
                                    break;
                                }
                            }
                        }
                    }

                    if (primAtt != null)
                    {
                        primAtt.UpgradeOpen();
                        primAtt.TextString = target.CombinedText;
                        primAtt.Height = target.CurrentHeight;
                        primAtt.Invisible = false;
                        primAtt.AdjustAlignment(db);
                        primAtt.RecordGraphicsModified(true);

                        foreach (AttributeReference sAtt in secAtts)
                        {
                            sAtt.UpgradeOpen();
                            sAtt.TextString = "";
                            sAtt.Invisible = true;
                            sAtt.RecordGraphicsModified(true);
                        }
                        syncCount++;
                    }
                }

                // 如果仅改当前1处
                if (!syncAllBlocks)
                {
                    tr.Commit();
                    target.TextString = target.CombinedText;
                    target.IsAlreadyMerged = true;
                    return syncCount;
                }

                // 2. 母块定义底层固化
                BlockTableRecord btr = tr.GetObject(target.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                if (btr != null)
                {
                    foreach (ObjectId entId in btr)
                    {
                        AttributeDefinition def = tr.GetObject(entId, OpenMode.ForRead) as AttributeDefinition;
                        if (def == null) continue;
                        if (string.Equals(def.Tag, primaryTag, StringComparison.OrdinalIgnoreCase))
                        {
                            def.UpgradeOpen();
                            def.Height = target.CurrentHeight;
                            def.TextString = target.CombinedText;
                            def.Invisible = false;
                            def.AdjustAlignment(db);
                            def.RecordGraphicsModified(true);
                        }
                        else
                        {
                            foreach (string sTag in secondaryTags)
                            {
                                if (string.Equals(def.Tag, sTag, StringComparison.OrdinalIgnoreCase))
                                {
                                    def.UpgradeOpen();
                                    def.TextString = "";
                                    def.Invisible = true;
                                    def.RecordGraphicsModified(true);
                                    break;
                                }
                            }
                        }
                    }
                }

                // 3. 同步全图所有同名块实例
                foreach (ObjectId refId in target.AllBlockReferenceIds)
                {
                    if (refId == target.ParentBlockId) continue;

                    BlockReference bRef = tr.GetObject(refId, OpenMode.ForRead) as BlockReference;
                    if (bRef == null) continue;

                    AttributeReference instPrim = null;
                    List<AttributeReference> instSecs = new List<AttributeReference>();
                    List<string> instPartTexts = new List<string>();

                    foreach (ObjectId attId in bRef.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att == null) continue;

                        if (string.Equals(att.Tag, primaryTag, StringComparison.OrdinalIgnoreCase))
                        {
                            instPrim = att;
                        }
                        else
                        {
                            foreach (string sTag in secondaryTags)
                            {
                                if (string.Equals(att.Tag, sTag, StringComparison.OrdinalIgnoreCase))
                                {
                                    instSecs.Add(att);
                                    break;
                                }
                            }
                        }
                    }

                    if (instPrim != null)
                    {
                        // 智能拼接每个实例自身原有的分段编号
                        System.Text.StringBuilder instSb = new System.Text.StringBuilder();
                        instSb.Append(instPrim.TextString.Trim());
                        foreach (AttributeReference sAtt in instSecs)
                        {
                            instSb.Append(sAtt.TextString.Trim());
                        }
                        string instCombined = instSb.ToString();
                        if (string.IsNullOrEmpty(instCombined)) instCombined = target.CombinedText;

                        instPrim.UpgradeOpen();
                        instPrim.TextString = instCombined;
                        instPrim.Height = target.CurrentHeight;
                        instPrim.Invisible = false;
                        instPrim.AdjustAlignment(db);
                        instPrim.RecordGraphicsModified(true);

                        foreach (AttributeReference sAtt in instSecs)
                        {
                            sAtt.UpgradeOpen();
                            sAtt.TextString = "";
                            sAtt.Invisible = true;
                            sAtt.RecordGraphicsModified(true);
                        }
                        syncCount++;
                    }
                }

                tr.Commit();
                target.TextString = target.CombinedText;
                target.IsAlreadyMerged = true;
            }

            return syncCount;
        }

        /// <summary>
        /// 方案 2: 保持分段并智能紧凑重排 (按字高自动缩减空隙紧贴)
        /// </summary>
        public static int CompactMultiPartAttributes(Database db, AttributeTargetInfo target, double height, bool syncAllBlocks)
        {
            if (target == null || !target.HasMultipleParts || target.RelatedParts.Count < 2) return 0;
            int syncCount = 0;

            double rot = target.RelatedParts[0].Rotation;
            Vector3d u = new Vector3d(Math.Cos(rot), Math.Sin(rot), 0);
            double gap = height * 0.15; // 紧凑微间隙 (字高的15%)

            // 1. 计算每个部件的视觉长度
            List<double> lengths = new List<double>();
            double totalCompactSpan = 0;
            for (int i = 0; i < target.RelatedParts.Count; i++)
            {
                AttributePartInfo p = target.RelatedParts[i];
                double len = 0;
                foreach (char c in p.TextString)
                {
                    if (c > 127) len += height * p.WidthFactor;
                    else len += height * p.WidthFactor * 0.55;
                }
                lengths.Add(len);
                totalCompactSpan += len;
                if (i > 0) totalCompactSpan += gap;
            }

            // 2. 计算原各部件的原几何投影中点
            double minProj = double.MaxValue;
            double maxProj = double.MinValue;
            for (int i = 0; i < target.RelatedParts.Count; i++)
            {
                AttributePartInfo p = target.RelatedParts[i];
                double pr = p.Position.X * u.X + p.Position.Y * u.Y;
                if (pr < minProj) minProj = pr;
                double prEnd = pr + lengths[i];
                if (prEnd > maxProj) maxProj = prEnd;
            }
            double originalMid = (minProj + maxProj) / 2.0;
            double curProj = originalMid - totalCompactSpan / 2.0;

            // 3. 计算每个部件所需的绝对平移量 (WCS)
            Dictionary<string, Vector3d> partDispMap = new Dictionary<string, Vector3d>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < target.RelatedParts.Count; i++)
            {
                AttributePartInfo p = target.RelatedParts[i];
                double oldPr = p.Position.X * u.X + p.Position.Y * u.Y;
                double delta = curProj - oldPr;
                partDispMap[p.Tag] = u * delta;
                curProj += lengths[i] + gap;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 应用于当前块
                BlockReference currentBlk = tr.GetObject(target.ParentBlockId, OpenMode.ForRead) as BlockReference;
                if (currentBlk != null)
                {
                    foreach (ObjectId attId in currentBlk.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && partDispMap.ContainsKey(att.Tag))
                        {
                            Vector3d disp = partDispMap[att.Tag];
                            att.UpgradeOpen();
                            att.Height = height;
                            att.TransformBy(Matrix3d.Displacement(disp));
                            att.AdjustAlignment(db);
                            att.RecordGraphicsModified(true);
                        }
                    }
                    syncCount++;
                }

                if (!syncAllBlocks)
                {
                    tr.Commit();
                    return syncCount;
                }

                // 4. 将位移映射至母块局部坐标系
                Matrix3d invMat = target.BlockTransform.Inverse();
                Dictionary<string, Vector3d> localDispMap = new Dictionary<string, Vector3d>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, Vector3d> kvp in partDispMap)
                {
                    localDispMap[kvp.Key] = kvp.Value.TransformBy(invMat);
                }

                // 母块定义同步
                BlockTableRecord btr = tr.GetObject(target.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                if (btr != null)
                {
                    foreach (ObjectId entId in btr)
                    {
                        AttributeDefinition def = tr.GetObject(entId, OpenMode.ForRead) as AttributeDefinition;
                        if (def != null && localDispMap.ContainsKey(def.Tag))
                        {
                            def.UpgradeOpen();
                            def.Height = height;
                            def.TransformBy(Matrix3d.Displacement(localDispMap[def.Tag]));
                            def.AdjustAlignment(db);
                            def.RecordGraphicsModified(true);
                        }
                    }
                }

                // 全图所有同名块同步
                foreach (ObjectId refId in target.AllBlockReferenceIds)
                {
                    if (refId == target.ParentBlockId) continue;

                    BlockReference bRef = tr.GetObject(refId, OpenMode.ForRead) as BlockReference;
                    if (bRef == null) continue;

                    bool touched = false;
                    foreach (ObjectId attId in bRef.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && localDispMap.ContainsKey(att.Tag))
                        {
                            Vector3d instDisp = localDispMap[att.Tag].TransformBy(bRef.BlockTransform);
                            att.UpgradeOpen();
                            att.Height = height;
                            att.TransformBy(Matrix3d.Displacement(instDisp));
                            att.AdjustAlignment(db);
                            att.RecordGraphicsModified(true);
                            touched = true;
                        }
                    }
                    if (touched) syncCount++;
                }

                tr.Commit();
            }

            return syncCount;
        }

        /// <summary>
        /// 批量更新属性文字位置（平移，支持多部件整体平移与单图块/全图同步控制）
        /// </summary>
        public static int MoveAttributePosition(Database db, AttributeTargetInfo target, Vector3d wcsDisp, bool syncAllBlocks)
        {
            if (target == null || wcsDisp.IsZeroLength()) return 0;
            int syncCount = 0;

            HashSet<string> movingTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (target.HasMultipleParts)
            {
                foreach (AttributePartInfo p in target.RelatedParts) movingTags.Add(p.Tag);
            }
            else
            {
                movingTags.Add(target.Tag);
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 将 WCS 位移逆变换为母块定义内部的局部坐标系位移 (localDisp)
                Matrix3d invMat = target.BlockTransform.Inverse();
                Vector3d localDisp = wcsDisp.TransformBy(invMat);

                // 1. 修改当前选中的图块
                BlockReference currentBlk = tr.GetObject(target.ParentBlockId, OpenMode.ForRead) as BlockReference;
                if (currentBlk != null)
                {
                    foreach (ObjectId attId in currentBlk.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && movingTags.Contains(att.Tag))
                        {
                            att.UpgradeOpen();
                            att.LockPositionInBlock = false;
                            att.TransformBy(Matrix3d.Displacement(wcsDisp));
                            att.AdjustAlignment(db);
                            att.RecordGraphicsModified(true);
                        }
                    }
                    syncCount++;
                }

                // 如果仅修改当前单个块
                if (!syncAllBlocks)
                {
                    tr.Commit();
                    return syncCount;
                }

                // 2. 更新母块定义的 AttributeDefinition 位置
                BlockTableRecord btr = tr.GetObject(target.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                if (btr != null)
                {
                    foreach (ObjectId entId in btr)
                    {
                        AttributeDefinition attDef = tr.GetObject(entId, OpenMode.ForRead) as AttributeDefinition;
                        if (attDef != null && movingTags.Contains(attDef.Tag))
                        {
                            attDef.UpgradeOpen();
                            attDef.TransformBy(Matrix3d.Displacement(localDisp));
                            attDef.AdjustAlignment(db);
                            attDef.RecordGraphicsModified(true);
                        }
                    }
                }

                // 3. 同步全图所有同名块引用的同名属性位置
                foreach (ObjectId refId in target.AllBlockReferenceIds)
                {
                    if (refId == target.ParentBlockId) continue;

                    BlockReference bRef = tr.GetObject(refId, OpenMode.ForRead) as BlockReference;
                    if (bRef == null) continue;

                    Vector3d instDisp = localDisp.TransformBy(bRef.BlockTransform);
                    bool touched = false;

                    foreach (ObjectId attId in bRef.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && movingTags.Contains(att.Tag))
                        {
                            att.UpgradeOpen();
                            att.LockPositionInBlock = false;
                            att.TransformBy(Matrix3d.Displacement(instDisp));
                            att.AdjustAlignment(db);
                            att.RecordGraphicsModified(true);
                            touched = true;
                        }
                    }
                    if (touched) syncCount++;
                }

                tr.Commit();
            }

            return syncCount;
        }
    }
}
