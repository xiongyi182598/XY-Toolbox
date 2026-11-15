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
        public string CurrentLayer { get; set; }
        public Autodesk.AutoCAD.Colors.Color CurrentColor { get; set; }

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

            ObjectId btrId = btr.ObjectId;
            string blockName = btr.Name;

            // 收集全图所有同名块引用（包含模型空间与所有布局图纸空间）
            HashSet<ObjectId> allRefIds = new HashSet<ObjectId>();
            foreach (ObjectId id in btr.GetBlockReferenceIds(true, true))
            {
                allRefIds.Add(id);
            }

            // 支持动态块匿名派生实例 (*U...) 并定位母块定义 DynamicBlockTableRecord
            if (parentBlk.IsDynamicBlock)
            {
                BlockTableRecord dynBtr = tr.GetObject(parentBlk.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (dynBtr != null)
                {
                    btrId = dynBtr.ObjectId;
                    blockName = dynBtr.Name;
                    foreach (ObjectId id in dynBtr.GetBlockReferenceIds(true, true))
                    {
                        allRefIds.Add(id);
                    }
                    foreach (ObjectId anonId in dynBtr.GetAnonymousBlockIds())
                    {
                        BlockTableRecord anonBtr = tr.GetObject(anonId, OpenMode.ForRead) as BlockTableRecord;
                        if (anonBtr != null)
                        {
                            foreach (ObjectId id in anonBtr.GetBlockReferenceIds(true, true))
                            {
                                allRefIds.Add(id);
                            }
                        }
                    }
                }
            }

            AttributeTargetInfo info = new AttributeTargetInfo
            {
                AttributeId = attRefId,
                ParentBlockId = parentBlk.ObjectId,
                BlockTableRecordId = btrId,
                BlockName = blockName,
                Tag = attRef.Tag,
                TextString = attRef.TextString,
                CurrentHeight = attRef.Height,
                CurrentLayer = attRef.Layer,
                CurrentColor = attRef.Color,
                PickedPoint = pickedPoint,
                IsDynamicBlock = parentBlk.IsDynamicBlock,
                BlockTransform = parentBlk.BlockTransform,
                AllBlockReferenceIds = allRefIds
            };

            // 自动侦测并收集同块内的复合属性部件 (如天正门窗多段编号 FM / 乙 / 1023)
            // 必须与当前点选属性处于物理相邻范围且字高相近，彻底杜绝把图框等不相关属性混淆
            List<AttributePartInfo> candidateParts = new List<AttributePartInfo>();
            int origIdx = 0;
            Point3d refPos = attRef.Justify == AttachmentPoint.BaseLeft ? attRef.Position : (attRef.AlignmentPoint != Point3d.Origin ? attRef.AlignmentPoint : attRef.Position);

            foreach (ObjectId attId in parentBlk.AttributeCollection)
            {
                AttributeReference a = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                if (a == null) continue;
                if (!a.Invisible && !string.IsNullOrEmpty(a.TextString) && a.TextString.Trim().Length > 0)
                {
                    Point3d aPos = a.Justify == AttachmentPoint.BaseLeft ? a.Position : (a.AlignmentPoint != Point3d.Origin ? a.AlignmentPoint : a.Position);
                    double dist = aPos.DistanceTo(refPos);
                    // 门窗复合编号间距紧密贴合（通常在 8 倍字高以内），字高比例兼容各种级配字高
                    double maxDist = Math.Max(Math.Max(attRef.Height, a.Height) * 8.0, 100.0);
                    bool isNearby = (attId == attRefId) || (dist <= maxDist && Math.Abs(a.Height - attRef.Height) <= Math.Max(attRef.Height, a.Height) * 0.8);

                    if (isNearby)
                    {
                        candidateParts.Add(new AttributePartInfo
                        {
                            AttributeId = attId,
                            Tag = a.Tag,
                            TextString = a.TextString,
                            Position = a.Position,
                            AlignmentPoint = (a.Justify == AttachmentPoint.BaseLeft) ? a.Position : (a.AlignmentPoint != Point3d.Origin ? a.AlignmentPoint : a.Position),
                            Height = a.Height,
                            Rotation = a.Rotation,
                            WidthFactor = a.WidthFactor > 0 ? a.WidthFactor : 0.7,
                            Invisible = a.Invisible,
                            OriginalIndex = origIdx++
                        });
                    }
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
                            try { att.LockPositionInBlock = false; } catch { }
                            att.Height = newHeight;
                            SafeAdjustAlignment(att, db);
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
                            SafeAdjustAlignment(attDef, db);
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
                            try { att.LockPositionInBlock = false; } catch { }
                            att.Height = newHeight;
                            SafeAdjustAlignment(att, db);
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
        /// 批量更新属性文字的图层与颜色（支持母块定义与全图所有同名块联动）
        /// </summary>
        public static int ModifyAttributeLayerAndColor(Database db, AttributeTargetInfo target, string newLayer, Autodesk.AutoCAD.Colors.Color newColor, bool syncAllBlocks, HashSet<ObjectId> allowedBlockIds = null)
        {
            if (target == null || (string.IsNullOrEmpty(newLayer) && newColor == null)) return 0;
            int syncCount = 0;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 1. 若指定了图层且不存在，先自动安全创建图层
                if (!string.IsNullOrEmpty(newLayer))
                {
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (lt != null && !lt.Has(newLayer))
                    {
                        lt.UpgradeOpen();
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = newLayer;
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }
                }

                // 2. 确定需要修改的属性 Tag (若多部件未合并，整体换层换色)
                HashSet<string> targetTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (target.HasMultipleParts)
                {
                    foreach (AttributePartInfo p in target.RelatedParts) targetTags.Add(p.Tag);
                }
                else
                {
                    targetTags.Add(target.Tag);
                }

                // 3. 修改当前选中的图块内的属性
                BlockReference currentBlk = tr.GetObject(target.ParentBlockId, OpenMode.ForRead) as BlockReference;
                if (currentBlk != null)
                {
                    foreach (ObjectId attId in currentBlk.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && targetTags.Contains(att.Tag))
                        {
                            att.UpgradeOpen();
                            if (!string.IsNullOrEmpty(newLayer)) att.Layer = newLayer;
                            if (newColor != null) att.Color = newColor;
                            att.RecordGraphicsModified(true);
                            if (attId == target.AttributeId) syncCount++;
                        }
                    }
                }

                // 若不联动全图同名块，提交并返回
                if (!syncAllBlocks)
                {
                    tr.Commit();
                    if (!string.IsNullOrEmpty(newLayer)) target.CurrentLayer = newLayer;
                    if (newColor != null) target.CurrentColor = newColor;
                    return syncCount > 0 ? 1 : 0;
                }

                // 4. 同步更新母块定义中的 AttributeDefinition（锁定后续 ATTSYNC 不丢失图层颜色）
                BlockTableRecord btr = tr.GetObject(target.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                if (btr != null)
                {
                    foreach (ObjectId entId in btr)
                    {
                        AttributeDefinition attDef = tr.GetObject(entId, OpenMode.ForRead) as AttributeDefinition;
                        if (attDef != null && targetTags.Contains(attDef.Tag))
                        {
                            attDef.UpgradeOpen();
                            if (!string.IsNullOrEmpty(newLayer)) attDef.Layer = newLayer;
                            if (newColor != null) attDef.Color = newColor;
                            attDef.RecordGraphicsModified(true);
                        }
                    }
                }

                // 5. 同步更新全图所有同名块实例（若有 allowedBlockIds 框选范围限定，则只更新限定范围内）
                foreach (ObjectId refId in target.AllBlockReferenceIds)
                {
                    if (refId == target.ParentBlockId) continue;
                    if (allowedBlockIds != null && !allowedBlockIds.Contains(refId)) continue;

                    BlockReference bRef = tr.GetObject(refId, OpenMode.ForRead) as BlockReference;
                    if (bRef == null) continue;

                    bool touched = false;
                    foreach (ObjectId attId in bRef.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && targetTags.Contains(att.Tag))
                        {
                            att.UpgradeOpen();
                            if (!string.IsNullOrEmpty(newLayer)) att.Layer = newLayer;
                            if (newColor != null) att.Color = newColor;
                            att.RecordGraphicsModified(true);
                            touched = true;
                        }
                    }
                    if (touched) syncCount++;
                }

                tr.Commit();
                if (!string.IsNullOrEmpty(newLayer)) target.CurrentLayer = newLayer;
                if (newColor != null) target.CurrentColor = newColor;
            }

            return syncCount;
        }

        /// <summary>
        /// 获取图纸中所有图层名称列表（首字母排序）
        /// </summary>
        public static List<string> GetAllLayerNames(Database db)
        {
            List<string> list = new List<string>();
            if (db == null) return list;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                if (lt != null)
                {
                    foreach (ObjectId lid in lt)
                    {
                        LayerTableRecord ltr = tr.GetObject(lid, OpenMode.ForRead) as LayerTableRecord;
                        if (ltr != null) list.Add(ltr.Name);
                    }
                }
                tr.Commit();
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
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
                if (currentBlk == null) return 0;

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

                if (primAtt == null) return 0;

                primAtt.UpgradeOpen();
                primAtt.LockPositionInBlock = false;
                primAtt.TextString = target.CombinedText;
                primAtt.Height = target.CurrentHeight;
                primAtt.Invisible = false;
                try { primAtt.AdjustAlignment(db); } catch { }
                primAtt.RecordGraphicsModified(true);

                foreach (AttributeReference sAtt in secAtts)
                {
                    sAtt.UpgradeOpen();
                    sAtt.TextString = "";
                    sAtt.Invisible = true;
                    sAtt.RecordGraphicsModified(true);
                }
                syncCount++;

                // 关键核心修复：把 target 对象的指针与标签彻底重定向到合并后的主属性！
                // 杜绝后续平移时由于 target 仍指着被清空的辅助属性导致主文字完全不移动的假死现象！
                target.Tag = primAtt.Tag;
                target.AttributeId = primAtt.ObjectId;

                // 计算合并后文字在当前门块中的局部视觉中心 (Local Center)
                Point3d primAnchor = GetTextAnchor(primAtt);
                double primH = target.CurrentHeight > 0.001 ? target.CurrentHeight : 200.0;
                double primWf = primAtt.WidthFactor > 0.01 ? primAtt.WidthFactor : 0.7;
                double primLen = EstimateTextLength(target.CombinedText, primH, primWf);
                double primRot = primAtt.Rotation;
                Vector3d primDir = new Vector3d(Math.Cos(primRot), Math.Sin(primRot), 0);
                Vector3d primUp = new Vector3d(-Math.Sin(primRot), Math.Cos(primRot), 0);
                Vector3d primOffset = GetOffsetFromAnchorToCenter(primAtt.Justify, primLen, primH, primDir, primUp);
                Point3d primVisualCenter = primAnchor + primOffset;

                Matrix3d invCurrentMat = Matrix3d.Identity;
                try { invCurrentMat = currentBlk.BlockTransform.Inverse(); } catch { }
                Point3d localCenter = primVisualCenter.TransformBy(invCurrentMat);

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
                            // 保持母块定义为通用模板，严禁将具体某一处的门编号写入母块默认定义
                            def.Invisible = false;

                            Point3d defAnchor = localCenter - primOffset;
                            SetTextLocation(def, defAnchor);
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

                // 3. 同步全图所有同名块实例（强行正向投影覆盖，彻底抹平历史错乱基点！）
                foreach (ObjectId refId in target.AllBlockReferenceIds)
                {
                    if (refId == target.ParentBlockId) continue;

                    BlockReference bRef = tr.GetObject(refId, OpenMode.ForRead) as BlockReference;
                    if (bRef == null) continue;

                    AttributeReference instPrim = null;
                    List<AttributeReference> instSecs = new List<AttributeReference>();

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
                        // 智能判断该门窗实例自身是否原本就有可见/有效的编号内容
                        bool hasAnyContent = (!instPrim.Invisible && !string.IsNullOrEmpty(instPrim.TextString) && instPrim.TextString.Trim().Length > 0);
                        System.Text.StringBuilder instSb = new System.Text.StringBuilder();
                        if (!string.IsNullOrEmpty(instPrim.TextString))
                        {
                            instSb.Append(instPrim.TextString.Trim());
                        }

                        foreach (AttributeReference sAtt in instSecs)
                        {
                            if (!sAtt.Invisible && !string.IsNullOrEmpty(sAtt.TextString) && sAtt.TextString.Trim().Length > 0)
                            {
                                hasAnyContent = true;
                            }
                            if (!string.IsNullOrEmpty(sAtt.TextString))
                            {
                                instSb.Append(sAtt.TextString.Trim());
                            }
                        }

                        string instCombined = instSb.ToString().Trim();

                        // 关键保护：若该图块实例原本完全没有输入编号或所有编号属性均为空/不可见，
                        // 绝对禁止强行赋予其他门窗的编号！保持原样为空且隐形！
                        if (!hasAnyContent || string.IsNullOrEmpty(instCombined))
                        {
                            instPrim.UpgradeOpen();
                            instPrim.TextString = "";
                            instPrim.Invisible = true;
                            instPrim.RecordGraphicsModified(true);

                            foreach (AttributeReference sAtt in instSecs)
                            {
                                sAtt.UpgradeOpen();
                                sAtt.TextString = "";
                                sAtt.Invisible = true;
                                sAtt.RecordGraphicsModified(true);
                            }
                            continue;
                        }

                        instPrim.UpgradeOpen();
                        instPrim.LockPositionInBlock = false;
                        instPrim.TextString = instCombined;
                        instPrim.Height = target.CurrentHeight;
                        instPrim.Invisible = false;

                        // 核心：基于图块几何矩阵强行重定位，彻底消除旋转和镜像导致的应用字高后基点不同步问题！
                        Point3d targetWcsCenter = localCenter.TransformBy(bRef.BlockTransform);
                        double instRot = NormalizeAngleReadable(instPrim.Rotation);
                        instPrim.Rotation = instRot;
                        double instWf = instPrim.WidthFactor > 0.01 ? instPrim.WidthFactor : 0.7;
                        double instLen = EstimateTextLength(instCombined, primH, instWf);
                        Vector3d instDir = new Vector3d(Math.Cos(instRot), Math.Sin(instRot), 0);
                        Vector3d instUp = new Vector3d(-Math.Sin(instRot), Math.Cos(instRot), 0);
                        Vector3d instOffset = GetOffsetFromAnchorToCenter(instPrim.Justify, instLen, primH, instDir, instUp);
                        Point3d targetAnchor = targetWcsCenter - instOffset;
                        SetTextLocation(instPrim, targetAnchor);
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
                            try { att.LockPositionInBlock = false; } catch { }
                            att.Height = height;
                            TranslateText(att, disp);
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

                // 4. 专业 CAD 几何投影核心 (Canonical Physical Offset Projection)：
                // 4. 母块定义与全图所有同名块正向投影重对齐 (基于图块定义局部坐标系映射)
                Matrix3d invTargetMat = Matrix3d.Identity;
                try { invTargetMat = currentBlk.BlockTransform.Inverse(); } catch { }
                Dictionary<string, Point3d> localAnchorMap = new Dictionary<string, Point3d>(StringComparer.OrdinalIgnoreCase);

                if (currentBlk != null)
                {
                    foreach (ObjectId attId in currentBlk.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && partDispMap.ContainsKey(att.Tag))
                        {
                            Point3d wcsPt = (att.Justify == AttachmentPoint.BaseLeft)
                                ? att.Position
                                : (att.AlignmentPoint != Point3d.Origin ? att.AlignmentPoint : att.Position);
                            localAnchorMap[att.Tag] = wcsPt.TransformBy(invTargetMat);
                        }
                    }
                }

                // 母块定义同步
                BlockTableRecord btr = tr.GetObject(target.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                if (btr != null)
                {
                    foreach (ObjectId entId in btr)
                    {
                        AttributeDefinition def = tr.GetObject(entId, OpenMode.ForRead) as AttributeDefinition;
                        if (def != null && localAnchorMap.ContainsKey(def.Tag))
                        {
                            Point3d locPt = localAnchorMap[def.Tag];
                            def.UpgradeOpen();
                            def.Height = height;
                            SetTextLocation(def, locPt);
                            def.RecordGraphicsModified(true);
                        }
                    }
                }

                // 全图所有同名块正向投影重对齐 (严格基于 BlockTransform 映射，完美支持旋转/镜像)
                foreach (ObjectId refId in target.AllBlockReferenceIds)
                {
                    if (refId == target.ParentBlockId) continue;

                    BlockReference bRef = tr.GetObject(refId, OpenMode.ForRead) as BlockReference;
                    if (bRef == null) continue;

                    bool touched = false;
                    foreach (ObjectId attId in bRef.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && localAnchorMap.ContainsKey(att.Tag))
                        {
                            Point3d targetWcsAnchor = localAnchorMap[att.Tag].TransformBy(bRef.BlockTransform);
                            att.UpgradeOpen();
                            att.LockPositionInBlock = false;
                            att.Height = height;
                            SetTextLocation(att, targetWcsAnchor);
                            att.RecordGraphicsModified(true);
                            touched = true;
                        }
                    }
                    if (touched) syncCount++;
                }

                // 更新内存中的部件坐标，防止后续连续操作基点错乱
                if (target.RelatedParts != null)
                {
                    foreach (AttributePartInfo p in target.RelatedParts)
                    {
                        if (partDispMap.ContainsKey(p.Tag))
                        {
                            Vector3d disp = partDispMap[p.Tag];
                            p.Position = p.Position + disp;
                            p.AlignmentPoint = p.AlignmentPoint + disp;
                        }
                    }
                }

                tr.Commit();
            }

            return syncCount;
        }

        /// <summary>
        /// 批量更新属性文字位置（平移，支持多部件整体平移与单图块/全图同步控制）
        /// 核心算法：图块局部视觉中心映射流水线 (Local Visual Center Inversion & Forward Projection)
        /// 完美适应任意旋转角度 (0°/90°/180°/270°)、水平/垂直/任意轴镜像、比例缩放及动态块
        /// 彻底杜绝镜像导致文字对齐撕裂、反向飞离图块或向不同方向乱跑的现象
        /// </summary>
        public static int MoveAttributePosition(Database db, AttributeTargetInfo target, Vector3d wcsDisp, bool syncAllBlocks,
            HashSet<ObjectId> allowedBlockIds = null, bool moveAllParts = false)
        {
            if (target == null || wcsDisp.IsZeroLength()) return 0;
            int syncCount = 0;

            HashSet<string> movingTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (moveAllParts && target.HasMultipleParts)
            {
                foreach (AttributePartInfo p in target.RelatedParts) movingTags.Add(p.Tag);
            }
            else
            {
                movingTags.Add(target.Tag);
            }

            Matrix3d dispMat = Matrix3d.Displacement(wcsDisp);

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockReference currentBlk = tr.GetObject(target.ParentBlockId, OpenMode.ForRead) as BlockReference;
                if (currentBlk == null) return 0;

                // ── 1. 修改当前选中的图块实例，并精确提取更新后的绝对 WCS 视觉中心点 (Visual Center)
                Dictionary<string, Point3d> newWcsCenterMap = new Dictionary<string, Point3d>(StringComparer.OrdinalIgnoreCase);

                foreach (ObjectId attId in currentBlk.AttributeCollection)
                {
                    AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (att != null && movingTags.Contains(att.Tag))
                    {
                        att.UpgradeOpen();
                        try { att.LockPositionInBlock = false; } catch { }
                        TranslateText(att, wcsDisp);
                        att.RecordGraphicsModified(true);

                        Point3d anchor = GetTextAnchor(att);

                        double h = att.Height > 0.001 ? att.Height : 200.0;
                        double wf = att.WidthFactor > 0.01 ? att.WidthFactor : 0.7;
                        double len = EstimateTextLength(att.TextString, h, wf);
                        double rot = att.Rotation;
                        Vector3d dir = new Vector3d(Math.Cos(rot), Math.Sin(rot), 0);
                        Vector3d up = new Vector3d(-Math.Sin(rot), Math.Cos(rot), 0);
                        Vector3d offset = GetOffsetFromAnchorToCenter(att.Justify, len, h, dir, up);
                        Point3d visualCenter = anchor + offset;

                        newWcsCenterMap[att.Tag] = visualCenter;
                    }
                }
                syncCount++;

                // ── 同步更新 target 内存对象的基准坐标与多部件坐标，杜绝连续多次平移时基点锁死旧位置导致方向错乱
                if (target.RelatedParts != null)
                {
                    foreach (AttributePartInfo p in target.RelatedParts)
                    {
                        if (movingTags.Contains(p.Tag))
                        {
                            p.Position = p.Position + wcsDisp;
                            p.AlignmentPoint = p.AlignmentPoint + wcsDisp;
                        }
                    }
                }
                target.PickedPoint = target.PickedPoint + wcsDisp;

                // 仅修改当前单个块：提交后直接返回
                if (!syncAllBlocks)
                {
                    tr.Commit();
                    return syncCount;
                }

                // ── 2. 将选定图块的视觉中心点反向逆变换至母块局部坐标空间 (OCS / Block Definition Space)
                Matrix3d invTargetMat = Matrix3d.Identity;
                try { invTargetMat = currentBlk.BlockTransform.Inverse(); } catch { }
                Dictionary<string, Point3d> localCenterMap = new Dictionary<string, Point3d>(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, Point3d> kvp in newWcsCenterMap)
                {
                    localCenterMap[kvp.Key] = kvp.Value.TransformBy(invTargetMat);
                }

                // ── 3. 更新母块定义 (BlockTableRecord) 中的 AttributeDefinition 坐标
                BlockTableRecord btr = tr.GetObject(target.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                if (btr != null)
                {
                    foreach (ObjectId entId in btr)
                    {
                        AttributeDefinition attDef = tr.GetObject(entId, OpenMode.ForRead) as AttributeDefinition;
                        if (attDef != null && localCenterMap.ContainsKey(attDef.Tag))
                        {
                            Point3d locCenter = localCenterMap[attDef.Tag];
                            attDef.UpgradeOpen();

                            double hDef = attDef.Height > 0.001 ? attDef.Height : 200.0;
                            double wfDef = attDef.WidthFactor > 0.01 ? attDef.WidthFactor : 0.7;
                            string defText = !string.IsNullOrEmpty(attDef.TextString) ? attDef.TextString : attDef.Tag;
                            double lenDef = EstimateTextLength(defText, hDef, wfDef);
                            double rotDef = attDef.Rotation;
                            Vector3d dirDef = new Vector3d(Math.Cos(rotDef), Math.Sin(rotDef), 0);
                            Vector3d upDef = new Vector3d(-Math.Sin(rotDef), Math.Cos(rotDef), 0);
                            Vector3d offsetDef = GetOffsetFromAnchorToCenter(attDef.Justify, lenDef, hDef, dirDef, upDef);
                            Point3d anchorDef = locCenter - offsetDef;
                            SetTextLocation(attDef, anchorDef);
                            attDef.RecordGraphicsModified(true);
                        }
                    }
                }

                // ── 4. 全图所有同名块正向投影重对齐 (基于视觉中心映射，彻底解决旋转与镜像问题)
                foreach (ObjectId refId in target.AllBlockReferenceIds)
                {
                    if (refId == target.ParentBlockId) continue; // 当前块已在第一步更新

                    if (allowedBlockIds != null && !allowedBlockIds.Contains(refId)) continue;

                    BlockReference bRef = tr.GetObject(refId, OpenMode.ForRead) as BlockReference;
                    if (bRef == null) continue;

                    bool touched = false;
                    foreach (ObjectId attId in bRef.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && localCenterMap.ContainsKey(att.Tag))
                        {
                            Point3d locCenter = localCenterMap[att.Tag];
                            Point3d targetWcsCenter = locCenter.TransformBy(bRef.BlockTransform);

                            att.UpgradeOpen();
                            try { att.LockPositionInBlock = false; } catch { }

                            // 规范化文字旋转角度，确保保持易读（不倒立）
                            double rot = NormalizeAngleReadable(att.Rotation);
                            att.Rotation = rot;

                            double h = att.Height > 0.001 ? att.Height : 200.0;
                            double wf = att.WidthFactor > 0.01 ? att.WidthFactor : 0.7;
                            double len = EstimateTextLength(att.TextString, h, wf);
                            Vector3d dir = new Vector3d(Math.Cos(rot), Math.Sin(rot), 0);
                            Vector3d up = new Vector3d(-Math.Sin(rot), Math.Cos(rot), 0);
                            Vector3d offset = GetOffsetFromAnchorToCenter(att.Justify, len, h, dir, up);
                            Point3d targetAnchor = targetWcsCenter - offset;
                            SetTextLocation(att, targetAnchor);
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

        #region 视觉中心与文字几何计算辅助方法

        /// <summary>
        /// 估算属性文字串的视觉排版物理长度
        /// </summary>
        private static double EstimateTextLength(string text, double height, double widthFactor)
        {
            if (string.IsNullOrEmpty(text)) return Math.Max(height, 50.0);
            double h = height > 0.001 ? height : 200.0;
            double wf = widthFactor > 0.01 ? widthFactor : 0.7;
            double charWidth = h * wf;
            double totalLen = 0;
            foreach (char c in text)
            {
                if (c <= 127)
                {
                    if (char.IsDigit(c))
                        totalLen += charWidth * 0.65;
                    else if (char.IsUpper(c))
                        totalLen += charWidth * 0.75;
                    else if (char.IsLower(c))
                        totalLen += charWidth * 0.6;
                    else
                        totalLen += charWidth * 0.45;
                }
                else
                {
                    // 中文全角汉字
                    totalLen += charWidth * 1.0;
                }
            }
            return Math.Max(totalLen, charWidth * 0.5);
        }

        /// <summary>
        /// 计算对齐锚点 (Anchor) 到文字视觉中心点 (Center) 的偏移向量
        /// </summary>
        private static Vector3d GetOffsetFromAnchorToCenter(AttachmentPoint justify, double length, double height, Vector3d dir, Vector3d up)
        {
            double halfLen = length * 0.5;
            double halfH = height * 0.5;

            double deltaU = 0;
            double deltaV = 0;

            switch (justify)
            {
                case AttachmentPoint.BaseLeft:
                case AttachmentPoint.BottomLeft:
                    deltaU = halfLen;
                    deltaV = halfH;
                    break;
                case AttachmentPoint.BaseCenter:
                case AttachmentPoint.BottomCenter:
                    deltaU = 0;
                    deltaV = halfH;
                    break;
                case AttachmentPoint.BaseRight:
                case AttachmentPoint.BottomRight:
                    deltaU = -halfLen;
                    deltaV = halfH;
                    break;
                case AttachmentPoint.MiddleLeft:
                    deltaU = halfLen;
                    deltaV = 0;
                    break;
                case AttachmentPoint.MiddleCenter:
                    deltaU = 0;
                    deltaV = 0;
                    break;
                case AttachmentPoint.MiddleRight:
                    deltaU = -halfLen;
                    deltaV = 0;
                    break;
                case AttachmentPoint.TopLeft:
                    deltaU = halfLen;
                    deltaV = -halfH;
                    break;
                case AttachmentPoint.TopCenter:
                    deltaU = 0;
                    deltaV = -halfH;
                    break;
                case AttachmentPoint.TopRight:
                    deltaU = -halfLen;
                    deltaV = -halfH;
                    break;
                default:
                    deltaU = halfLen;
                    deltaV = halfH;
                    break;
            }

            return deltaU * dir + deltaV * up;
        }

        /// <summary>
        /// 安全设置文字绝对锚点坐标
        /// 严格遵循 AutoCAD 规范：BaseLeft 仅写入 Position，非 BaseLeft 写入 AlignmentPoint 并重算对齐
        /// 绝不触发 AutoCAD 对 BaseLeft 文字访问 AlignmentPoint 抛出的 eNotApplicable 异常
        /// </summary>
        public static void SetTextLocation(DBText txt, Point3d anchorPt)
        {
            if (txt == null) return;
            if (txt.Justify == AttachmentPoint.BaseLeft)
            {
                txt.Position = anchorPt;
            }
            else
            {
                try
                {
                    txt.AlignmentPoint = anchorPt;
                    Database db = txt.Database;
                    if (db == null)
                    {
                        try { db = HostApplicationServices.WorkingDatabase; } catch { }
                    }
                    if (db != null)
                    {
                        try { txt.AdjustAlignment(db); } catch { }
                    }
                }
                catch
                {
                    try { txt.Position = anchorPt; } catch { }
                }
            }
        }

        /// <summary>
        /// 安全平移文字（增量位移）
        /// 严格保护 BaseLeft 文字，绝不调用其 AlignmentPoint
        /// </summary>
        public static void TranslateText(DBText txt, Vector3d disp)
        {
            if (txt == null) return;
            txt.Position = txt.Position + disp;
            if (txt.Justify != AttachmentPoint.BaseLeft)
            {
                try
                {
                    txt.AlignmentPoint = txt.AlignmentPoint + disp;
                    Database db = txt.Database;
                    if (db == null)
                    {
                        try { db = HostApplicationServices.WorkingDatabase; } catch { }
                    }
                    if (db != null)
                    {
                        try { txt.AdjustAlignment(db); } catch { }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 安全获取文字在世界坐标系/局部坐标系下的基准锚点
        /// </summary>
        public static Point3d GetTextAnchor(DBText txt)
        {
            if (txt == null) return Point3d.Origin;
            if (txt.Justify != AttachmentPoint.BaseLeft)
            {
                try
                {
                    return txt.AlignmentPoint;
                }
                catch { }
            }
            return txt.Position;
        }

        /// <summary>
        /// 规范化文字旋转角度，确保保持建筑制图人眼易读（禁止倒立字）
        /// </summary>
        private static double NormalizeAngleReadable(double rot)
        {
            while (rot < 0) rot += Math.PI * 2;
            while (rot >= Math.PI * 2) rot -= Math.PI * 2;
            if (rot > Math.PI * 0.5 + 0.001 && rot <= Math.PI * 1.5 + 0.001)
            {
                rot -= Math.PI;
                if (rot < 0) rot += Math.PI * 2;
            }
            return rot;
        }

        /// <summary>
        /// 安全调整属性文字对齐（自动跳过 BaseLeft，杜绝 AutoCAD 抛出 eNotApplicable 异常导致事务回滚）
        /// </summary>
        public static void SafeAdjustAlignment(DBText txt, Database db)
        {
            if (txt == null || txt.Justify == AttachmentPoint.BaseLeft) return;
            try
            {
                Database targetDb = db ?? txt.Database;
                if (targetDb == null)
                {
                    try { targetDb = HostApplicationServices.WorkingDatabase; } catch { }
                }
                if (targetDb != null)
                {
                    txt.AdjustAlignment(targetDb);
                }
            }
            catch { }
        }

        #endregion
    }
}
