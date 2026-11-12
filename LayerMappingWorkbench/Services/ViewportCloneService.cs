using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace LayerMappingWorkbench.Services
{
    /// <summary>
    /// 视口图层覆盖状态备忘录 (针对单个图层的视口颜色、线型、线宽、透明度独立覆盖)
    /// </summary>
    public class ViewportLayerOverrideMemento
    {
        public string SourceLayerName { get; set; }
        public bool IsColorOverridden { get; set; }
        public Autodesk.AutoCAD.Colors.Color Color { get; set; }
        public bool IsLinetypeOverridden { get; set; }
        public string LinetypeName { get; set; }
        public bool IsLineWeightOverridden { get; set; }
        public LineWeight LineWeight { get; set; }
        public bool IsTransparencyOverridden { get; set; }
        public Autodesk.AutoCAD.Colors.Transparency Transparency { get; set; }

        public ViewportLayerOverrideMemento()
        {
            SourceLayerName = string.Empty;
            IsColorOverridden = false;
            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 256);
            IsLinetypeOverridden = false;
            LinetypeName = string.Empty;
            IsLineWeightOverridden = false;
            LineWeight = LineWeight.ByLayer;
            IsTransparencyOverridden = false;
            Transparency = new Autodesk.AutoCAD.Colors.Transparency(Autodesk.AutoCAD.Colors.TransparencyMethod.ByLayer);
        }
    }

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
        public List<ViewportLayerOverrideMemento> LayerOverrides { get; set; }

        // 视口实体自身图元属性 (全属性完整无损继承)
        public string SourceLayer { get; set; }
        public Autodesk.AutoCAD.Colors.Color Color { get; set; }
        public string Linetype { get; set; }
        public double LinetypeScale { get; set; }
        public LineWeight LineWeight { get; set; }
        public bool Locked { get; set; }
        public Autodesk.AutoCAD.Colors.Transparency Transparency { get; set; }

        // 视口显示控制与渲染模式
        public ShadePlotType ShadePlot { get; set; }
        public bool GridOn { get; set; }
        public bool SnapOn { get; set; }
        public bool DefaultLightingOn { get; set; }
        public DefaultLightingType DefaultLightingType { get; set; }
        public bool UcsFollowModeOn { get; set; }

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
            LayerOverrides = new List<ViewportLayerOverrideMemento>();

            SourceLayer = "0";
            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 256);
            Linetype = "ByLayer";
            LinetypeScale = 1.0;
            LineWeight = LineWeight.ByLayer;
            Locked = false;
            Transparency = new Autodesk.AutoCAD.Colors.Transparency(Autodesk.AutoCAD.Colors.TransparencyMethod.ByLayer);

            ShadePlot = ShadePlotType.AsDisplayed;
            GridOn = false;
            SnapOn = false;
            DefaultLightingOn = true;
            DefaultLightingType = DefaultLightingType.TwoDistantLights;
            UcsFollowModeOn = false;
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

            // 提取视口图元自身属性与渲染显示控制
            try
            {
                m.SourceLayer = srcVp.Layer;
                m.Color = srcVp.Color;
                m.Linetype = srcVp.Linetype;
                m.LinetypeScale = srcVp.LinetypeScale;
                m.LineWeight = srcVp.LineWeight;
                m.Locked = srcVp.Locked;
                m.Transparency = srcVp.Transparency;
                m.ShadePlot = srcVp.ShadePlot;
                m.GridOn = srcVp.GridOn;
                m.SnapOn = srcVp.SnapOn;
                m.DefaultLightingOn = srcVp.DefaultLightingOn;
                m.DefaultLightingType = srcVp.DefaultLightingType;
                m.UcsFollowModeOn = srcVp.UcsFollowModeOn;
            }
            catch { }

            // 提取视口冻结图层名 (优先调用 GetFrozenLayers 获得真实冻结图层)
            try
            {
                ObjectIdCollection frozenIds = srcVp.GetFrozenLayers();
                if (frozenIds != null && frozenIds.Count > 0)
                {
                    foreach (ObjectId layId in frozenIds)
                    {
                        try
                        {
                            LayerTableRecord ltr = (LayerTableRecord)srcTr.GetObject(layId, OpenMode.ForRead);
                            if (ltr != null && !string.IsNullOrEmpty(ltr.Name))
                            {
                                m.FrozenLayerNames.Add(ltr.Name);
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    LayerTable lt = (LayerTable)srcTr.GetObject(srcDb.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId layId in lt)
                    {
                        if (srcVp.IsLayerFrozenInViewport(layId))
                        {
                            LayerTableRecord ltr = (LayerTableRecord)srcTr.GetObject(layId, OpenMode.ForRead);
                            if (ltr != null && !string.IsNullOrEmpty(ltr.Name))
                            {
                                m.FrozenLayerNames.Add(ltr.Name);
                            }
                        }
                    }
                }
            }
            catch { }

            // 提取视口图层覆盖 (VPOVERRIDE: 颜色/线型/线宽/透明度)
            try
            {
                LayerTable srcLt = (LayerTable)srcTr.GetObject(srcDb.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId layId in srcLt)
                {
                    LayerTableRecord ltr = (LayerTableRecord)srcTr.GetObject(layId, OpenMode.ForRead);
                    if (ltr != null && ltr.HasViewportOverrides(srcVp.Id))
                    {
                        LayerViewportProperties ov = ltr.GetViewportOverrides(srcVp.Id);
                        if (ov != null && (ov.IsColorOverridden || ov.IsLinetypeOverridden || ov.IsLineWeightOverridden || ov.IsTransparencyOverridden))
                        {
                            ViewportLayerOverrideMemento om = new ViewportLayerOverrideMemento();
                            om.SourceLayerName = ltr.Name;
                            om.IsColorOverridden = ov.IsColorOverridden;
                            om.Color = ov.Color;
                            om.IsLinetypeOverridden = ov.IsLinetypeOverridden;
                            if (ov.IsLinetypeOverridden && ov.LinetypeObjectId != ObjectId.Null)
                            {
                                try
                                {
                                    LinetypeTableRecord ltRec = (LinetypeTableRecord)srcTr.GetObject(ov.LinetypeObjectId, OpenMode.ForRead);
                                    om.LinetypeName = ltRec.Name;
                                }
                                catch { }
                            }
                            om.IsLineWeightOverridden = ov.IsLineWeightOverridden;
                            om.LineWeight = ov.LineWeight;
                            om.IsTransparencyOverridden = ov.IsTransparencyOverridden;
                            om.Transparency = ov.Transparency;
                            m.LayerOverrides.Add(om);
                        }
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
    /// 视口克隆与移植高级配置项
    /// </summary>
    public class ViewportCloneOptions
    {
        public string SourceDwgPath { get; set; }
        public string SourceLayoutName { get; set; }
        public ObjectId TargetLayoutId { get; set; }
        public Dictionary<string, string> LayerMappingRules { get; set; }
        public ModelAlignmentMode AlignMode { get; set; }
        public Vector2d CustomOffset { get; set; }

        // 底图框选几何包围盒 (用于彻底消除裁切截断)
        public bool HasModelBaseBox { get; set; }
        public Point3d ModelBaseCenter { get; set; }
        public double ModelBaseWidth { get; set; }
        public double ModelBaseHeight { get; set; }

        // 视口比例 (-1: 保持源视口比例; 0: 自适应充满Fit; >0: 指定比例如 0.01 对应 1:100)
        public double TargetCustomScale { get; set; }

        // 多视口协同模式 (true: 保持同一个底图，所有视口同心对齐底图; false: 保持源视口相对偏移分布)
        public bool MultiViewportSyncCenter { get; set; }

        public ViewportCloneOptions()
        {
            TargetLayoutId = ObjectId.Null;
            LayerMappingRules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AlignMode = ModelAlignmentMode.Center;
            CustomOffset = new Vector2d(0, 0);
            HasModelBaseBox = false;
            ModelBaseCenter = Point3d.Origin;
            ModelBaseWidth = 0.0;
            ModelBaseHeight = 0.0;
            TargetCustomScale = -1.0;
            MultiViewportSyncCenter = true;
        }
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
            ViewportCloneOptions opt = new ViewportCloneOptions();
            opt.SourceDwgPath = sourceDwgPath;
            opt.SourceLayoutName = sourceLayoutName;
            opt.TargetLayoutId = targetLayoutId;
            opt.LayerMappingRules = layerMappingRules;
            opt.AlignMode = alignMode;
            opt.CustomOffset = customOffset;
            return CloneViewportsToLayout(opt);
        }

        /// <summary>
        /// 移植外部 DWG 布局中的所有视口 (基于高级配置，支持模型底图框选、视口比例调节与多视口同心套合)
        /// </summary>
        public CloneResult CloneViewportsToLayout(ViewportCloneOptions options)
        {
            CloneResult result = new CloneResult();
            if (options == null)
            {
                result.Message = "配置参数为空";
                return result;
            }

            string sourceDwgPath = options.SourceDwgPath;
            string sourceLayoutName = options.SourceLayoutName;
            ObjectId targetLayoutId = options.TargetLayoutId;
            Dictionary<string, string> layerMappingRules = options.LayerMappingRules;
            ModelAlignmentMode alignMode = options.AlignMode;
            Vector2d customOffset = options.CustomOffset;

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
                        Point3d srcModelCenter = Point3d.Origin;
                        Point3d srcMin, srcMax, tgtMin, tgtMax;
                        bool srcOk = TryGetModelSpaceBounds(sourceDb, srcTr, out srcMin, out srcMax);
                        bool tgtOk = TryGetModelSpaceBounds(targetDb, tgtTr, out tgtMin, out tgtMax);
                        if (srcOk)
                        {
                            srcModelCenter = new Point3d((srcMin.X + srcMax.X) * 0.5, (srcMin.Y + srcMax.Y) * 0.5, 0);
                        }

                        // 计算源与目标模型空间的几何偏移量
                        Vector2d modelOffset = new Vector2d(0, 0);
                        if (alignMode == ModelAlignmentMode.CustomDelta)
                        {
                            modelOffset = customOffset;
                        }
                        else if (srcOk && tgtOk)
                        {
                            if (alignMode == ModelAlignmentMode.Center)
                            {
                                Point3d tgtCenter = new Point3d((tgtMin.X + tgtMax.X) * 0.5, (tgtMin.Y + tgtMax.Y) * 0.5, 0);
                                modelOffset = new Vector2d(tgtCenter.X - srcModelCenter.X, tgtCenter.Y - srcModelCenter.Y);
                            }
                            else if (alignMode == ModelAlignmentMode.BottomLeft)
                            {
                                modelOffset = new Vector2d(tgtMin.X - srcMin.X, tgtMin.Y - srcMin.Y);
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
                        ObjectId targetLayoutObjId = targetLayoutId;
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
                            targetLayoutObjId = curLayoutId;
                        }

                        BlockTableRecord tgtBtr = (BlockTableRecord)tgtTr.GetObject(targetBtrId, OpenMode.ForWrite);
                        LayerTable tgtLt = (LayerTable)tgtTr.GetObject(targetDb.LayerTableId, OpenMode.ForRead);

                        // 3. 筛选并提取源视口 (精准排除图纸空间自身默认全景背景视口，只保留真实浮动视口)
                        ObjectId overallVpId = ObjectId.Null;
                        try
                        {
                            ObjectIdCollection vpIds = srcLayout.GetViewports();
                            if (vpIds != null && vpIds.Count > 0)
                            {
                                overallVpId = vpIds[0];
                            }
                        }
                        catch { }

                        List<Viewport> srcViewports = new List<Viewport>();
                        foreach (ObjectId entId in srcBtr)
                        {
                            if (overallVpId != ObjectId.Null && entId == overallVpId)
                            {
                                continue; // 坚决排除图纸空间自身的默认全幅背景视口！
                            }

                            Viewport vp = srcTr.GetObject(entId, OpenMode.ForRead, false, false) as Viewport;
                            if (vp != null)
                            {
                                if (overallVpId == ObjectId.Null && vp.Number == 1 && !vp.NonRectClipOn)
                                {
                                    continue;
                                }

                                srcViewports.Add(vp);
                            }
                        }

                        if (srcViewports.Count == 0)
                        {
                            result.Message = "源布局中未检索到有效视口实体";
                            return result;
                        }

                        // 4. 清理目标布局中已存在的旧浮动视口 (保留图纸空间自身默认背景全景视口，彻底消除重叠重影)
                        ObjectId tgtOverallVpId = ObjectId.Null;
                        try
                        {
                            Layout curLayout = (Layout)tgtTr.GetObject(targetLayoutObjId, OpenMode.ForRead);
                            ObjectIdCollection curVpIds = curLayout.GetViewports();
                            if (curVpIds != null && curVpIds.Count > 0)
                            {
                                tgtOverallVpId = curVpIds[0];
                            }
                        }
                        catch { }

                        List<ObjectId> oldVpsToErase = new List<ObjectId>();
                        List<ObjectId> oldClipsToErase = new List<ObjectId>();
                        foreach (ObjectId entId in tgtBtr)
                        {
                            if (tgtOverallVpId != ObjectId.Null && entId == tgtOverallVpId) continue;
                            Viewport vp = tgtTr.GetObject(entId, OpenMode.ForRead, false, false) as Viewport;
                            if (vp != null)
                            {
                                oldVpsToErase.Add(entId);
                                if (vp.NonRectClipOn && vp.NonRectClipEntityId != ObjectId.Null)
                                {
                                    oldClipsToErase.Add(vp.NonRectClipEntityId);
                                }
                            }
                        }
                        foreach (ObjectId oldId in oldVpsToErase)
                        {
                            try
                            {
                                DBObject obj = tgtTr.GetObject(oldId, OpenMode.ForWrite);
                                obj.Erase();
                            }
                            catch { }
                        }
                        foreach (ObjectId clipId in oldClipsToErase)
                        {
                            try
                            {
                                DBObject obj = tgtTr.GetObject(clipId, OpenMode.ForWrite);
                                obj.Erase();
                            }
                            catch { }
                        }

                        foreach (Viewport srcVp in srcViewports)
                        {
                            // 提取备忘录快照 (包含几何、全图元属性、冻结层列表、视口层覆盖 VPOVERRIDE)
                            ViewportDataMemento memento = ViewportDataMemento.CreateFrom(srcTr, sourceDb, srcVp);

                            // 5. 在目标布局实例化新视口
                            Viewport tgtVp = new Viewport();
                            tgtVp.SetDatabaseDefaults();
                            tgtBtr.AppendEntity(tgtVp);
                            tgtTr.AddNewlyCreatedDBObject(tgtVp, true);

                            // 还原几何核心参数 (自动叠加模型空间几何中心位移)
                            memento.ApplyGeometryTo(tgtVp, modelOffset);

                            // 智能解析并还原视口图元自身属性 (图层、颜色、线宽、线型缩放、显示锁定)
                            try
                            {
                                ObjectId resolvedVpLayerId = ResolveTargetLayerId(memento.SourceLayer, layerMappingRules, tgtLt, tgtTr, targetDb, sourceDb, srcTr);
                                if (resolvedVpLayerId != ObjectId.Null)
                                {
                                    tgtVp.LayerId = resolvedVpLayerId;
                                }

                                tgtVp.Color = memento.Color;
                                tgtVp.LineWeight = memento.LineWeight;
                                tgtVp.LinetypeScale = memento.LinetypeScale;
                                tgtVp.Locked = memento.Locked;
                                tgtVp.ShadePlot = memento.ShadePlot;
                                tgtVp.GridOn = memento.GridOn;
                                tgtVp.SnapOn = memento.SnapOn;
                                tgtVp.DefaultLightingOn = memento.DefaultLightingOn;
                                tgtVp.DefaultLightingType = memento.DefaultLightingType;
                                tgtVp.UcsFollowModeOn = memento.UcsFollowModeOn;
                            }
                            catch { }

                            // ---- 核心强化：底图框选与视口比例精准协同套合 (多个视口看同一个底图) ----
                            if (options.HasModelBaseBox)
                            {
                                // A. 视心对齐计算 (支持多个视口保持同一个底图)
                                if (options.MultiViewportSyncCenter)
                                {
                                    // 保持同一个底图：所有视口同心对齐框选的底图几何中心
                                    tgtVp.ViewCenter = new Point2d(options.ModelBaseCenter.X, options.ModelBaseCenter.Y);
                                }
                                else
                                {
                                    // 保持源图纸视口相对偏置分布
                                    double relDx = srcVp.ViewCenter.X - srcModelCenter.X;
                                    double relDy = srcVp.ViewCenter.Y - srcModelCenter.Y;
                                    tgtVp.ViewCenter = new Point2d(options.ModelBaseCenter.X + relDx, options.ModelBaseCenter.Y + relDy);
                                }

                                // B. 视口比例与视野高度适配
                                if (options.TargetCustomScale > 0)
                                {
                                    // 用户显式指定了比例（如 1:100 -> 0.01, 1:50 -> 0.02）
                                    tgtVp.CustomScale = options.TargetCustomScale;
                                    tgtVp.ViewHeight = tgtVp.Height / options.TargetCustomScale;
                                }
                                else if (options.TargetCustomScale == 0)
                                {
                                    // 智能自适应充满 (Fit)
                                    if (options.ModelBaseWidth > 0 && options.ModelBaseHeight > 0)
                                    {
                                        double scaleX = tgtVp.Width / (options.ModelBaseWidth * 1.05);
                                        double scaleY = tgtVp.Height / (options.ModelBaseHeight * 1.05);
                                        double fitScale = Math.Min(scaleX, scaleY);
                                        if (fitScale > 0)
                                        {
                                            tgtVp.CustomScale = fitScale;
                                            tgtVp.ViewHeight = tgtVp.Height / fitScale;
                                        }
                                    }
                                }
                            }
                            else if (options.TargetCustomScale > 0)
                            {
                                tgtVp.CustomScale = options.TargetCustomScale;
                                tgtVp.ViewHeight = tgtVp.Height / options.TargetCustomScale;
                            }

                            // 6. 跨图纸深度克隆非矩形剪裁多段线 (WblockCloneObjects)
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

                            // 7. 多级智能重定向并执行视口图层冻结设置 (VP Freeze)
                            if (memento.FrozenLayerNames.Count > 0)
                            {
                                ObjectIdCollection freezeLayers = new ObjectIdCollection();
                                foreach (string srcLayName in memento.FrozenLayerNames)
                                {
                                    ObjectId resolvedLayId = ResolveTargetLayerId(srcLayName, layerMappingRules, tgtLt, tgtTr, targetDb, sourceDb, srcTr);
                                    if (resolvedLayId != ObjectId.Null && !freezeLayers.Contains(resolvedLayId))
                                    {
                                        freezeLayers.Add(resolvedLayId);
                                    }
                                }

                                if (freezeLayers.Count > 0)
                                {
                                    tgtVp.FreezeLayersInViewport(freezeLayers.GetEnumerator());
                                }
                            }

                            // 8. 移植并还原视口图层覆盖属性 (VPOVERRIDE: 颜色/线型/线宽/透明度)
                            if (memento.LayerOverrides != null && memento.LayerOverrides.Count > 0)
                            {
                                foreach (ViewportLayerOverrideMemento ovMem in memento.LayerOverrides)
                                {
                                    ObjectId resolvedLayId = ResolveTargetLayerId(ovMem.SourceLayerName, layerMappingRules, tgtLt, tgtTr, targetDb, sourceDb, srcTr);
                                    if (resolvedLayId != ObjectId.Null)
                                    {
                                        try
                                        {
                                            LayerTableRecord ltr = (LayerTableRecord)tgtTr.GetObject(resolvedLayId, OpenMode.ForWrite);
                                            LayerViewportProperties vpProps = ltr.GetViewportOverrides(tgtVp.Id);
                                            if (vpProps != null)
                                            {
                                                if (ovMem.IsColorOverridden)
                                                {
                                                    vpProps.Color = ovMem.Color;
                                                    vpProps.IsColorOverridden = true;
                                                }
                                                if (ovMem.IsLineWeightOverridden)
                                                {
                                                    vpProps.LineWeight = ovMem.LineWeight;
                                                    vpProps.IsLineWeightOverridden = true;
                                                }
                                                if (ovMem.IsTransparencyOverridden)
                                                {
                                                    vpProps.Transparency = ovMem.Transparency;
                                                    vpProps.IsTransparencyOverridden = true;
                                                }
                                                if (ovMem.IsLinetypeOverridden && !string.IsNullOrEmpty(ovMem.LinetypeName))
                                                {
                                                    LinetypeTable tgtLtt = (LinetypeTable)tgtTr.GetObject(targetDb.LinetypeTableId, OpenMode.ForRead);
                                                    if (tgtLtt.Has(ovMem.LinetypeName))
                                                    {
                                                        vpProps.LinetypeObjectId = tgtLtt[ovMem.LinetypeName];
                                                        vpProps.IsLinetypeOverridden = true;
                                                    }
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }

                            // 9. 激活视口、更新显示及锁定
                            try
                            {
                                tgtVp.On = true;
                                tgtVp.UpdateDisplay();
                                tgtVp.Locked = memento.Locked;
                            }
                            catch { }

                            result.ViewportsCloned++;
                        }

                        tgtTr.Commit();
                        srcTr.Commit();

                        // 自动平滑切换至目标图纸空间布局并重绘，确保视口图层冻结与显示立即在视窗中刷新
                        try
                        {
                            if (targetLayoutObjId != ObjectId.Null)
                            {
                                using (Transaction switchTr = targetDb.TransactionManager.StartTransaction())
                                {
                                    Layout tgtLayObj = (Layout)switchTr.GetObject(targetLayoutObjId, OpenMode.ForRead);
                                    if (tgtLayObj != null && !string.IsNullOrEmpty(tgtLayObj.LayoutName))
                                    {
                                        LayoutManager.Current.CurrentLayout = tgtLayObj.LayoutName;
                                    }
                                    switchTr.Commit();
                                }
                            }
                            if (doc != null && doc.Editor != null)
                            {
                                doc.Editor.Regen();
                            }
                        }
                        catch { }

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

        private static readonly System.Text.RegularExpressions.Regex BracketRegex =
            new System.Text.RegularExpressions.Regex(@"[【\[\(（].*?[】\]\)）]", System.Text.RegularExpressions.RegexOptions.Compiled);

        public static string CleanBracketAnnotation(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string res = BracketRegex.Replace(name, "").Trim();
            return string.IsNullOrEmpty(res) ? name.Trim() : res;
        }

        private static readonly Dictionary<string, string[]> LayerConceptDictionary = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "WALL", new string[] { "WALL", "墙", "墙体", "QT", "WA", "BRK", "CONC", "土建墙柱", "平面墙体" } },
            { "DOOR", new string[] { "DOOR", "门", "门窗", "MC", "DR", "ML" } },
            { "WINDOW", new string[] { "WINDOW", "窗", "窗户", "CK", "WIN", "CL" } },
            { "AXIS", new string[] { "AXIS", "轴", "轴线", "轴网", "ZHOU", "AX", "GRID" } },
            { "COLUMN", new string[] { "COLUMN", "柱", "柱子", "ZHU", "COL" } },
            { "BEAM", new string[] { "BEAM", "梁", "结构梁", "LIANG", "BM" } },
            { "HATCH", new string[] { "HATCH", "填充", "图案", "TC", "PAT" } },
            { "TEXT", new string[] { "TEXT", "文字", "文本", "WZ", "TXT" } },
            { "DIM", new string[] { "DIM", "标注", "尺寸", "BZ" } },
            { "FURN_FIXED", new string[] { "固定家具", "固定高柜", "固定", "FF-Fixed", "E_WINDOW" } },
            { "FURN_MOVE", new string[] { "活动家具", "成品家具", "活动", "家具", "布置", "JJ", "FUR", "FF-FURN", "TREE" } },
            { "FLOOR", new string[] { "FLOOR", "地面", "地坪", "铺装", "DM", "DP", "分割线", "FC-Material", "FC_TEMP", "EVAC_PATH" } },
            { "CEILING", new string[] { "CEILING", "天花", "顶面", "吊顶", "ZM", "TH", "RCP", "造型线", "RC-Modelling" } },
            { "PLUMB", new string[] { "PLUMB", "给排水", "水施", "水管", "GPS", "洁具", "五金", "配件", "FF-FIXT", "DIM_IDEN" } },
            { "ELEC", new string[] { "ELEC", "电气", "照明", "强电", "弱电", "DQ", "ZM" } },
            { "HVAC", new string[] { "HVAC", "暖通", "空调", "通风", "NT" } }
        };

        public static string ExtractConceptFromLayerName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (KeyValuePair<string, string[]> kv in LayerConceptDictionary)
            {
                foreach (string kw in kv.Value)
                {
                    if (name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return kv.Key;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 多级智能图层解析引擎 (5级逐层穿透，确保视口冻结与覆盖 100% 成功映射到目标图纸)：
        /// 级 1: 映射规则表精准匹配 (目标层存在)
        /// 级 2: 清洗括号中文注解后规则表匹配 (目标层存在)
        /// 级 3: 目标图纸直接已有同名或清洗同名直通
        /// 级 4: 语义概念词根加权打分匹配目标图纸实际图元承载层 (准确率极高)
        /// 级 5: 兜底自动按源属性创建目标图层
        /// </summary>
        public static ObjectId ResolveTargetLayerId(
            string srcLayName,
            Dictionary<string, string> rules,
            LayerTable tgtLt,
            Transaction tgtTr,
            Database tgtDb,
            Database srcDb,
            Transaction srcTr)
        {
            if (string.IsNullOrEmpty(srcLayName) || tgtLt == null || tgtTr == null) return ObjectId.Null;

            string cleanSrc = CleanBracketAnnotation(srcLayName);

            // 级 1: 精准规则匹配
            if (rules != null && rules.ContainsKey(srcLayName))
            {
                string rTarget = rules[srcLayName];
                if (!string.IsNullOrEmpty(rTarget) && tgtLt.Has(rTarget))
                {
                    return tgtLt[rTarget];
                }
                string cleanRTarget = CleanBracketAnnotation(rTarget);
                if (!string.IsNullOrEmpty(cleanRTarget) && tgtLt.Has(cleanRTarget))
                {
                    return tgtLt[cleanRTarget];
                }
            }

            // 级 2: 清洗源图层名称后的规则匹配
            if (rules != null && !string.IsNullOrEmpty(cleanSrc) && rules.ContainsKey(cleanSrc))
            {
                string rTarget = rules[cleanSrc];
                if (!string.IsNullOrEmpty(rTarget) && tgtLt.Has(rTarget))
                {
                    return tgtLt[rTarget];
                }
                string cleanRTarget = CleanBracketAnnotation(rTarget);
                if (!string.IsNullOrEmpty(cleanRTarget) && tgtLt.Has(cleanRTarget))
                {
                    return tgtLt[cleanRTarget];
                }
            }

            // 级 3: 目标图纸直接已有同名或清洗同名直通
            if (tgtLt.Has(srcLayName))
            {
                return tgtLt[srcLayName];
            }
            if (!string.IsNullOrEmpty(cleanSrc) && tgtLt.Has(cleanSrc))
            {
                return tgtLt[cleanSrc];
            }

            // 级 4: 语义概念特征打分推断 (优先映射到目标图纸中已有的、承载实际图元的图层)
            string bestMatchName = null;
            int bestScore = 0;

            string srcConcept = ExtractConceptFromLayerName(srcLayName);
            if (string.IsNullOrEmpty(srcConcept) && !string.IsNullOrEmpty(cleanSrc))
            {
                srcConcept = ExtractConceptFromLayerName(cleanSrc);
            }

            foreach (ObjectId id in tgtLt)
            {
                try
                {
                    LayerTableRecord ltr = (LayerTableRecord)tgtTr.GetObject(id, OpenMode.ForRead);
                    string tName = ltr.Name;
                    if (string.Equals(tName, "0", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tName, "Defpoints", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int score = 0;
                    string tConcept = ExtractConceptFromLayerName(tName);
                    if (!string.IsNullOrEmpty(srcConcept) && !string.IsNullOrEmpty(tConcept) &&
                        string.Equals(srcConcept, tConcept, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 50;
                        if (srcLayName.IndexOf("固定", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            tName.IndexOf("固定", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            score += 30;
                        }
                        else if ((srcLayName.IndexOf("活动", StringComparison.OrdinalIgnoreCase) >= 0 || srcLayName.IndexOf("成品", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                 (tName.IndexOf("活动", StringComparison.OrdinalIgnoreCase) >= 0 || tName.IndexOf("成品", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            score += 30;
                        }
                    }

                    double sim = LayerAlignmentEngine.CalculateLevenshteinSimilarity(CleanBracketAnnotation(srcLayName), CleanBracketAnnotation(tName));
                    score += (int)(sim * 30.0);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMatchName = tName;
                    }
                }
                catch { }
            }

            if (bestScore >= 50 && !string.IsNullOrEmpty(bestMatchName) && tgtLt.Has(bestMatchName))
            {
                return tgtLt[bestMatchName];
            }

            // 级 5: 兜底自动创建目标图层
            string fallbackTargetName = srcLayName;
            if (rules != null && rules.ContainsKey(srcLayName) && !string.IsNullOrEmpty(rules[srcLayName]))
            {
                fallbackTargetName = rules[srcLayName];
            }
            else if (rules != null && !string.IsNullOrEmpty(cleanSrc) && rules.ContainsKey(cleanSrc) && !string.IsNullOrEmpty(rules[cleanSrc]))
            {
                fallbackTargetName = rules[cleanSrc];
            }

            try
            {
                if (!tgtLt.Has(fallbackTargetName))
                {
                    tgtLt.UpgradeOpen();
                    LayerTableRecord newLtr = new LayerTableRecord();
                    newLtr.Name = fallbackTargetName;
                    try
                    {
                        if (srcDb != null && srcTr != null)
                        {
                            LayerTable srcLt = (LayerTable)srcTr.GetObject(srcDb.LayerTableId, OpenMode.ForRead);
                            if (srcLt.Has(srcLayName))
                            {
                                LayerTableRecord srcLtr = (LayerTableRecord)srcTr.GetObject(srcLt[srcLayName], OpenMode.ForRead);
                                newLtr.Color = srcLtr.Color;
                                newLtr.IsPlottable = srcLtr.IsPlottable;
                                newLtr.LineWeight = srcLtr.LineWeight;
                            }
                        }
                    }
                    catch { }
                    ObjectId newId = tgtLt.Add(newLtr);
                    tgtTr.AddNewlyCreatedDBObject(newLtr, true);
                    tgtLt.DowngradeOpen();
                    return newId;
                }
                else
                {
                    return tgtLt[fallbackTargetName];
                }
            }
            catch
            {
                return ObjectId.Null;
            }
        }
    }
}
