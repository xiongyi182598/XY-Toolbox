using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace LayerMappingWorkbench.Services
{
    /// <summary>
    /// 视口关键状态与几何参数备忘录 (跨数据库安全传递，杜绝直接跨库引用 ObjectId)
    /// </summary>
    public class ViewportDataMemento
    {
        public Point3d CenterPoint { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Point2d ViewCenter { get; set; }
        public double ViewHeight { get; set; }
        public double CustomScale { get; set; }
        public StandardScaleType StandardScale { get; set; }
        public double TwistAngle { get; set; }
        public double LensLength { get; set; }
        public Vector3d ViewDirection { get; set; }
        public Point3d ViewTarget { get; set; }
        public bool NonRectClipOn { get; set; }
        public ObjectId SourceNonRectClipId { get; set; }
        public List<string> FrozenLayerNames { get; set; }

        public ViewportDataMemento()
        {
            CenterPoint = Point3d.Origin;
            ViewCenter = Point2d.Origin;
            ViewHeight = 100.0;
            Width = 100.0;
            Height = 100.0;
            CustomScale = 1.0;
            StandardScale = StandardScaleType.CustomScale;
            TwistAngle = 0.0;
            LensLength = 50.0;
            ViewDirection = new Vector3d(0, 0, 1);
            ViewTarget = Point3d.Origin;
            NonRectClipOn = false;
            SourceNonRectClipId = ObjectId.Null;
            FrozenLayerNames = new List<string>();
        }

        public static ViewportDataMemento CreateFrom(Transaction srcTr, Database srcDb, Viewport srcVp)
        {
            ViewportDataMemento m = new ViewportDataMemento();
            m.CenterPoint = srcVp.CenterPoint;
            m.Width = srcVp.Width;
            m.Height = srcVp.Height;
            m.ViewCenter = srcVp.ViewCenter;
            m.ViewHeight = srcVp.ViewHeight;
            m.CustomScale = srcVp.CustomScale;
            m.StandardScale = srcVp.StandardScale;
            m.TwistAngle = srcVp.TwistAngle;
            m.LensLength = srcVp.LensLength;
            m.ViewDirection = srcVp.ViewDirection;
            m.ViewTarget = srcVp.ViewTarget;
            m.NonRectClipOn = srcVp.NonRectClipOn;
            m.SourceNonRectClipId = srcVp.NonRectClipEntityId;

            // 提取视口冻结图层名
            try
            {
                LayerTable lt = (LayerTable)srcTr.GetObject(srcDb.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId layId in lt)
                {
                    if (srcVp.IsLayerFrozenInViewport(layId))
                    {
                        LayerTableRecord ltr = (LayerTableRecord)srcTr.GetObject(layId, OpenMode.ForRead);
                        m.FrozenLayerNames.Add(ltr.Name);
                    }
                }
            }
            catch { }

            return m;
        }

        public void ApplyGeometryTo(Viewport tgtVp)
        {
            ApplyGeometryTo(tgtVp, new Vector2d(0, 0));
        }

        public void ApplyGeometryTo(Viewport tgtVp, Vector2d offset)
        {
            // 1. 图纸空间位置与几何尺寸
            tgtVp.CenterPoint = CenterPoint;
            tgtVp.Width = Width;
            tgtVp.Height = Height;

            // 2. 建立三维观察系与相机基准 (DCS)（必须先于 ViewCenter 设置，防止重置视心）
            tgtVp.ViewDirection = ViewDirection;
            tgtVp.ViewTarget = ViewTarget;
            tgtVp.TwistAngle = TwistAngle;
            tgtVp.LensLength = LensLength;

            // 3. 确立比例与视野高度
            tgtVp.CustomScale = CustomScale;
            tgtVp.ViewHeight = ViewHeight;

            // 4. 应用带几何中心偏移补偿的视心 (必须在 DCS 确立后赋值)
            tgtVp.ViewCenter = new Point2d(ViewCenter.X + offset.X, ViewCenter.Y + offset.Y);
        }
    }

    public enum ModelAlignmentMode
    {
        Center,     // 几何中心对齐 (两图图元包围盒几何中心重合)
        BottomLeft, // 左下角基准对齐 (两图图元包围盒左下角重合)
        CustomDelta // 自定义或鼠标拾取基准点偏移
    }

    /// <summary>
    /// 跨外部 DWG 视口无损移植引擎 (非矩形剪裁跨库克隆 + DCS/WCS几何精确保全 + 视口图层状态还原)
    /// </summary>
    public class ViewportCloneService
    {
        public class CloneResult
        {
            public bool Success { get; set; }
            public int ViewportsCloned { get; set; }
            public int ClipBoundariesCloned { get; set; }
            public string Message { get; set; }

            public CloneResult()
            {
                Success = false;
                ViewportsCloned = 0;
                ClipBoundariesCloned = 0;
                Message = string.Empty;
            }
        }

        /// <summary>
        /// 从外部 DWG 文件枚举所有图纸空间布局名称
        /// </summary>
        public static List<string> GetLayoutNamesFromExternalDwg(string sourceDwgPath)
        {
            List<string> layoutNames = new List<string>();
            if (string.IsNullOrEmpty(sourceDwgPath) || !File.Exists(sourceDwgPath))
            {
                return layoutNames;
            }

            try
            {
                using (Database sourceDb = new Database(false, true))
                {
                    sourceDb.ReadDwgFile(sourceDwgPath, FileOpenMode.OpenForReadAndAllShare, true, "");
                    using (Transaction tr = sourceDb.TransactionManager.StartTransaction())
                    {
                        DBDictionary layoutDict = (DBDictionary)tr.GetObject(sourceDb.LayoutDictionaryId, OpenMode.ForRead);
                        foreach (DBDictionaryEntry entry in layoutDict)
                        {
                            string name = entry.Key;
                            if (!string.Equals(name, "Model", StringComparison.OrdinalIgnoreCase))
                            {
                                layoutNames.Add(name);
                            }
                        }
                        tr.Commit();
                    }
                }
            }
            catch { }

            return layoutNames;
        }

        /// <summary>
        /// 扫描数据库模型空间的实际有效图元几何包围盒 (忽略不可见图元)
        /// </summary>
        public static bool TryGetModelSpaceBounds(Database db, Transaction tr, out Point3d minPt, out Point3d maxPt)
        {
            minPt = Point3d.Origin;
            maxPt = Point3d.Origin;
            try
            {
                ObjectId msId = SymbolUtilityServices.GetBlockModelSpaceId(db);
                BlockTableRecord msBtr = (BlockTableRecord)tr.GetObject(msId, OpenMode.ForRead);
                bool hasExt = false;
                Extents3d totalExt = new Extents3d();
                foreach (ObjectId id in msBtr)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead, false, false) as Entity;
                    if (ent != null && ent.Visible)
                    {
                        try
                        {
                            if (!hasExt)
                            {
                                totalExt = ent.GeometricExtents;
                                hasExt = true;
                            }
                            else
                            {
                                totalExt.AddExtents(ent.GeometricExtents);
                            }
                        }
                        catch { }
                    }
                }

                if (hasExt)
                {
                    minPt = totalExt.MinPoint;
                    maxPt = totalExt.MaxPoint;
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 探测两图纸模型空间几何包围盒 (源图与当前宿主目标图)
        /// </summary>
        public static bool DetectModelBounds(string sourceDwgPath,
            out Point3d srcMin, out Point3d srcMax,
            out Point3d tgtMin, out Point3d tgtMax)
        {
            srcMin = Point3d.Origin;
            srcMax = Point3d.Origin;
            tgtMin = Point3d.Origin;
            tgtMax = Point3d.Origin;

            if (string.IsNullOrEmpty(sourceDwgPath) || !File.Exists(sourceDwgPath)) return false;
            Database targetDb = HostApplicationServices.WorkingDatabase;
            if (targetDb == null) return false;

            try
            {
                using (Database sourceDb = new Database(false, true))
                {
                    sourceDb.ReadDwgFile(sourceDwgPath, FileOpenMode.OpenForReadAndAllShare, true, "");
                    using (Transaction srcTr = sourceDb.TransactionManager.StartTransaction())
                    using (Transaction tgtTr = targetDb.TransactionManager.StartTransaction())
                    {
                        bool srcOk = TryGetModelSpaceBounds(sourceDb, srcTr, out srcMin, out srcMax);
                        bool tgtOk = TryGetModelSpaceBounds(targetDb, tgtTr, out tgtMin, out tgtMax);
                        return srcOk && tgtOk;
                    }
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// 探测两图纸模型空间几何中心偏移向量 (targetCenter - sourceCenter)
        /// </summary>
        public static bool DetectModelOffset(string sourceDwgPath, out Vector2d offset, out string info)
        {
            offset = new Vector2d(0, 0);
            info = string.Empty;

            Point3d srcMin, srcMax, tgtMin, tgtMax;
            if (DetectModelBounds(sourceDwgPath, out srcMin, out srcMax, out tgtMin, out tgtMax))
            {
                Point3d srcCenter = new Point3d((srcMin.X + srcMax.X) * 0.5, (srcMin.Y + srcMax.Y) * 0.5, 0);
                Point3d tgtCenter = new Point3d((tgtMin.X + tgtMax.X) * 0.5, (tgtMin.Y + tgtMax.Y) * 0.5, 0);
                double dx = tgtCenter.X - srcCenter.X;
                double dy = tgtCenter.Y - srcCenter.Y;
                offset = new Vector2d(dx, dy);
                info = string.Format("ΔX={0:+0.00;-0.00;0.00}, ΔY={1:+0.00;-0.00;0.00}", dx, dy);
                return true;
            }
            else
            {
                info = "未能识别完整模型几何包围盒";
                return false;
            }
        }

        public CloneResult CloneViewportsToLayout(
            string sourceDwgPath,
            string sourceLayoutName,
            ObjectId targetLayoutId,
            Dictionary<string, string> layerMappingRules)
        {
            return CloneViewportsToLayout(sourceDwgPath, sourceLayoutName, targetLayoutId, layerMappingRules, ModelAlignmentMode.Center, new Vector2d(0, 0));
        }

        public CloneResult CloneViewportsToLayout(
            string sourceDwgPath,
            string sourceLayoutName,
            ObjectId targetLayoutId,
            Dictionary<string, string> layerMappingRules,
            bool autoAlignModel)
        {
            return CloneViewportsToLayout(sourceDwgPath, sourceLayoutName, targetLayoutId, layerMappingRules, 
                autoAlignModel ? ModelAlignmentMode.Center : ModelAlignmentMode.CustomDelta, new Vector2d(0, 0));
        }

        /// <summary>
        /// 移植外部 DWG 布局中的所有视口到当前图纸的活动布局或指定布局 (支持中心对齐、角点对齐与鼠标拾取点对齐)
        /// </summary>
        public CloneResult CloneViewportsToLayout(
            string sourceDwgPath,
            string sourceLayoutName,
            ObjectId targetLayoutId,
            Dictionary<string, string> layerMappingRules,
            ModelAlignmentMode alignMode,
            Vector2d customOffset)
        {
            CloneResult result = new CloneResult();

            if (string.IsNullOrEmpty(sourceDwgPath) || !File.Exists(sourceDwgPath))
            {
                result.Message = "源 DWG 文件不存在: " + sourceDwgPath;
                return result;
            }

            Database targetDb = HostApplicationServices.WorkingDatabase;
            if (targetDb == null)
            {
                result.Message = "当前宿主图纸数据库未就绪";
                return result;
            }

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            DocumentLock docLock = null;

            try
            {
                if (doc != null)
                {
                    docLock = doc.LockDocument();
                }

                using (Database sourceDb = new Database(false, true))
                {
                    // 1. 静默只读共享模式打开源 DWG
                    sourceDb.ReadDwgFile(sourceDwgPath, FileOpenMode.OpenForReadAndAllShare, true, "");

                    using (Transaction srcTr = sourceDb.TransactionManager.StartTransaction())
                    using (Transaction tgtTr = targetDb.TransactionManager.StartTransaction())
                    {
                        // 计算源与目标模型空间的几何偏移量
                        Vector2d modelOffset = new Vector2d(0, 0);
                        if (alignMode == ModelAlignmentMode.CustomDelta)
                        {
                            modelOffset = customOffset;
                        }
                        else
                        {
                            Point3d srcMin, srcMax, tgtMin, tgtMax;
                            bool srcOk = TryGetModelSpaceBounds(sourceDb, srcTr, out srcMin, out srcMax);
                            bool tgtOk = TryGetModelSpaceBounds(targetDb, tgtTr, out tgtMin, out tgtMax);
                            if (srcOk && tgtOk)
                            {
                                if (alignMode == ModelAlignmentMode.Center)
                                {
                                    Point3d srcCenter = new Point3d((srcMin.X + srcMax.X) * 0.5, (srcMin.Y + srcMax.Y) * 0.5, 0);
                                    Point3d tgtCenter = new Point3d((tgtMin.X + tgtMax.X) * 0.5, (tgtMin.Y + tgtMax.Y) * 0.5, 0);
                                    modelOffset = new Vector2d(tgtCenter.X - srcCenter.X, tgtCenter.Y - srcCenter.Y);
                                }
                                else if (alignMode == ModelAlignmentMode.BottomLeft)
                                {
                                    modelOffset = new Vector2d(tgtMin.X - srcMin.X, tgtMin.Y - srcMin.Y);
                                }
                            }
                        }

                        // 2. 定位源布局与视口实体
                        DBDictionary srcLayoutDict = (DBDictionary)srcTr.GetObject(sourceDb.LayoutDictionaryId, OpenMode.ForRead);
                        if (!srcLayoutDict.Contains(sourceLayoutName))
                        {
                            result.Message = "源 DWG 中未找到指定布局: " + sourceLayoutName;
                            return result;
                        }

                        Layout srcLayout = (Layout)srcTr.GetObject(srcLayoutDict.GetAt(sourceLayoutName), OpenMode.ForRead);
                        BlockTableRecord srcBtr = (BlockTableRecord)srcTr.GetObject(srcLayout.BlockTableRecordId, OpenMode.ForRead);

                        // 定位目标布局的 BlockTableRecordId
                        ObjectId targetBtrId;
                        if (targetLayoutId != ObjectId.Null)
                        {
                            Layout tgtLayout = (Layout)tgtTr.GetObject(targetLayoutId, OpenMode.ForRead);
                            targetBtrId = tgtLayout.BlockTableRecordId;
                        }
                        else
                        {
                            // 默认使用当前活动布局；若当前为模型空间，自动定位至第一个图纸空间布局
                            LayoutManager lm = LayoutManager.Current;
                            ObjectId curLayoutId = ObjectId.Null;
                            if (!string.Equals(lm.CurrentLayout, "Model", StringComparison.OrdinalIgnoreCase))
                            {
                                curLayoutId = lm.GetLayoutId(lm.CurrentLayout);
                            }
                            else
                            {
                                DBDictionary tgtLayoutDict = (DBDictionary)tgtTr.GetObject(targetDb.LayoutDictionaryId, OpenMode.ForRead);
                                foreach (DBDictionaryEntry entry in tgtLayoutDict)
                                {
                                    if (!string.Equals(entry.Key, "Model", StringComparison.OrdinalIgnoreCase))
                                    {
                                        curLayoutId = entry.Value;
                                        break;
                                    }
                                }
                            }

                            if (curLayoutId == ObjectId.Null)
                            {
                                result.Message = "目标图纸中未找到有效的图纸空间布局";
                                return result;
                            }

                            Layout curLayout = (Layout)tgtTr.GetObject(curLayoutId, OpenMode.ForRead);
                            targetBtrId = curLayout.BlockTableRecordId;
                        }

                        BlockTableRecord tgtBtr = (BlockTableRecord)tgtTr.GetObject(targetBtrId, OpenMode.ForWrite);
                        LayerTable tgtLt = (LayerTable)tgtTr.GetObject(targetDb.LayerTableId, OpenMode.ForRead);

                        // 3. 筛选并提取源视口
                        List<Viewport> srcViewports = new List<Viewport>();
                        foreach (ObjectId entId in srcBtr)
                        {
                            Viewport vp = srcTr.GetObject(entId, OpenMode.ForRead, false, false) as Viewport;
                            if (vp != null)
                            {
                                // 过滤布局默认空间背景视口 (通常视口 1 为整个纸张边界背景)
                                if (vp.Number > 1 || vp.NonRectClipOn || vp.CustomScale > 0)
                                {
                                    srcViewports.Add(vp);
                                }
                            }
                        }

                        if (srcViewports.Count == 0)
                        {
                            result.Message = "源布局中未检索到有效视口实体";
                            return result;
                        }

                        foreach (Viewport srcVp in srcViewports)
                        {
                            // 提取备忘录快照
                            ViewportDataMemento memento = ViewportDataMemento.CreateFrom(srcTr, sourceDb, srcVp);

                            // 4. 在目标布局实例化新视口
                            Viewport tgtVp = new Viewport();
                            tgtVp.SetDatabaseDefaults();
                            tgtBtr.AppendEntity(tgtVp);
                            tgtTr.AddNewlyCreatedDBObject(tgtVp, true);

                            // 还原几何核心参数 (自动叠加模型空间几何中心位移)
                            memento.ApplyGeometryTo(tgtVp, modelOffset);

                            // 5. 跨图纸深度克隆非矩形剪裁多段线 (WblockCloneObjects)
                            if (memento.NonRectClipOn && memento.SourceNonRectClipId != ObjectId.Null)
                            {
                                try
                                {
                                    ObjectIdCollection cloneList = new ObjectIdCollection();
                                    cloneList.Add(memento.SourceNonRectClipId);
                                    IdMapping idMap = new IdMapping();

                                    sourceDb.WblockCloneObjects(cloneList, targetBtrId, idMap, DuplicateRecordCloning.Replace, false);

                                    if (idMap.Contains(memento.SourceNonRectClipId))
                                    {
                                        IdPair pair = idMap[memento.SourceNonRectClipId];
                                        if (pair != null && pair.IsCloned && pair.Value != ObjectId.Null)
                                        {
                                            tgtVp.NonRectClipEntityId = pair.Value;
                                            tgtVp.NonRectClipOn = true;
                                            result.ClipBoundariesCloned++;
                                        }
                                    }
                                }
                                catch { }
                            }

                            // 6. 重定向视口图层冻结设置
                            if (memento.FrozenLayerNames.Count > 0)
                            {
                                ObjectIdCollection freezeLayers = new ObjectIdCollection();
                                foreach (string srcLayName in memento.FrozenLayerNames)
                                {
                                    string mappedName = srcLayName;
                                    if (layerMappingRules != null && layerMappingRules.ContainsKey(srcLayName))
                                    {
                                        mappedName = layerMappingRules[srcLayName];
                                    }

                                    if (tgtLt.Has(mappedName))
                                    {
                                        freezeLayers.Add(tgtLt[mappedName]);
                                    }
                                }

                                if (freezeLayers.Count > 0)
                                {
                                    tgtVp.FreezeLayersInViewport(freezeLayers.GetEnumerator());
                                }
                            }

                            // 7. 激活视口与点亮显示
                            try
                            {
                                tgtVp.On = true;
                                tgtVp.UpdateDisplay();
                            }
                            catch { }

                            result.ViewportsCloned++;
                        }

                        tgtTr.Commit();
                        srcTr.Commit();
                        result.Success = true;
                        result.Message = string.Format("成功无损移植 {0} 个视口 (含 {1} 个非矩形边界)", result.ViewportsCloned, result.ClipBoundariesCloned);
                    }
                }
            }
            catch (System.Exception ex)
            {
                result.Success = false;
                result.Message = "视口移植异常: " + ex.Message;
            }
            finally
            {
                if (docLock != null)
                {
                    docLock.Dispose();
                }
            }

            return result;
        }
    }
}
