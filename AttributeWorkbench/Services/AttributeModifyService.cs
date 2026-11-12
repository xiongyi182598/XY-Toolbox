using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AttributeWorkbench.Services
{
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

        public AttributeTargetInfo()
        {
            AllBlockReferenceIds = new HashSet<ObjectId>();
        }
    }

    /// <summary>
    /// 属性文字修改核心服务（支持字高修改、实时平移与全空间同步）
    /// </summary>
    public static class AttributeModifyService
    {
        /// <summary>
        /// 从选中的属性实体中提取详细上下文信息
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
                // 1. 修改当前选中的 AttributeReference
                AttributeReference currentAtt = tr.GetObject(target.AttributeId, OpenMode.ForWrite) as AttributeReference;
                if (currentAtt != null)
                {
                    currentAtt.Height = newHeight;
                    currentAtt.AdjustAlignment(db);
                    currentAtt.RecordGraphicsModified(true);
                    syncCount++;
                }

                // 如果仅修改当前单个块，提交并返回
                if (!syncAllBlocks)
                {
                    tr.Commit();
                    target.CurrentHeight = newHeight;
                    return syncCount;
                }

                // 2. 同步更新母块定义内部的 AttributeDefinition（确保后续 ATTSYNC 永久锁死新字高，不被重置）
                BlockTableRecord btr = tr.GetObject(target.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                if (btr != null)
                {
                    foreach (ObjectId entId in btr)
                    {
                        AttributeDefinition attDef = tr.GetObject(entId, OpenMode.ForRead) as AttributeDefinition;
                        if (attDef != null && string.Equals(attDef.Tag, target.Tag, StringComparison.OrdinalIgnoreCase))
                        {
                            attDef.UpgradeOpen();
                            attDef.Height = newHeight;
                            attDef.AdjustAlignment(db);
                            attDef.RecordGraphicsModified(true);
                            break;
                        }
                    }
                }

                // 3. 同步全图所有同名块引用的同名属性
                foreach (ObjectId refId in target.AllBlockReferenceIds)
                {
                    if (refId == target.ParentBlockId) continue; // 当前块前面已修改

                    BlockReference bRef = tr.GetObject(refId, OpenMode.ForRead) as BlockReference;
                    if (bRef == null) continue;

                    foreach (ObjectId attId in bRef.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && string.Equals(att.Tag, target.Tag, StringComparison.OrdinalIgnoreCase))
                        {
                            att.UpgradeOpen();
                            att.Height = newHeight;
                            att.AdjustAlignment(db);
                            att.RecordGraphicsModified(true);
                            syncCount++;
                            break;
                        }
                    }
                }

                tr.Commit();
                target.CurrentHeight = newHeight;
            }

            return syncCount;
        }

        /// <summary>
        /// 批量更新属性文字位置（平移）
        /// </summary>
        public static int MoveAttributePosition(Database db, AttributeTargetInfo target, Vector3d wcsDisp, bool syncAllBlocks)
        {
            if (target == null || wcsDisp.IsZeroLength()) return 0;
            int syncCount = 0;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 将 WCS 位移逆变换为母块定义内部的局部坐标系位移 (localDisp)
                Matrix3d invMat = target.BlockTransform.Inverse();
                Vector3d localDisp = wcsDisp.TransformBy(invMat);

                // 1. 修改当前选中的 AttributeReference
                AttributeReference currentAtt = tr.GetObject(target.AttributeId, OpenMode.ForWrite) as AttributeReference;
                if (currentAtt != null)
                {
                    currentAtt.LockPositionInBlock = false;
                    currentAtt.TransformBy(Matrix3d.Displacement(wcsDisp));
                    currentAtt.AdjustAlignment(db);
                    currentAtt.RecordGraphicsModified(true);
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
                        if (attDef != null && string.Equals(attDef.Tag, target.Tag, StringComparison.OrdinalIgnoreCase))
                        {
                            attDef.UpgradeOpen();
                            attDef.TransformBy(Matrix3d.Displacement(localDisp));
                            attDef.AdjustAlignment(db);
                            attDef.RecordGraphicsModified(true);
                            break;
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

                    foreach (ObjectId attId in bRef.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && string.Equals(att.Tag, target.Tag, StringComparison.OrdinalIgnoreCase))
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
            }

            return syncCount;
        }
    }
}
