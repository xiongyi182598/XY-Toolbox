using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Geometry;

namespace XY_Deepening_Toolbox
{
    public class LayerInfo
    {
        public string Name { get; set; }
        public int ColorIndex { get; set; }
        public System.Drawing.Color ColorValue { get; set; }
        public string ColorDescription { get; set; }
        public string LinetypeName { get; set; }
        public string LineWeightName { get; set; }
        public LineWeight LineWeightValue { get; set; }
        public bool IsPlottable { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsLocked { get; set; }
        public bool IsOff { get; set; }
        public int EntityCount { get; set; }
        public bool IsEmpty { get { return EntityCount == 0; } }
        public bool IsSpecial { get { return Name == "0" || Name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase); } }
        public bool IsCurrent { get; set; }

        public override string ToString()
        {
            return string.Format("{0} ({1}, {2}, {3}图元)", Name, ColorDescription, LinetypeName, EntityCount);
        }
    }

    public enum MatchConfidenceLevel
    {
        ExactMatch,     // 100% 完全精确一致
        High,           // 80% ~ 99% 高度推荐
        Medium,         // 60% ~ 79% 建议匹配
        Low,            // 30% ~ 59% 弱关联
        None            // 0% ~ 29% 无合适标准层
    }

    public class LayerMatchResult
    {
        public LayerInfo CurrentLayer { get; set; }
        public LayerInfo BestMatchRefLayer { get; set; }
        public int MatchScore { get; set; }
        public string MatchReason { get; set; }
        public MatchConfidenceLevel Level { get; set; }
        public bool IsSelectedForMerge { get; set; }
        public string TargetLayerName { get; set; }
    }

    public class PurgeReport
    {
        public int TotalLayersScanned { get; set; }
        public int UnusedLayersFound { get; set; }
        public int PurgedLayersCount { get; set; }
        public List<string> PurgedLayerNames { get; set; }
        public List<string> ProtectedLayerNames { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string SummaryMessage { get; set; }

        public PurgeReport()
        {
            PurgedLayerNames = new List<string>();
            ProtectedLayerNames = new List<string>();
        }
    }

    public static class LayerPurgeService
    {
        public static Dictionary<string, int> CountLayerEntities(Database db)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 先登记图层表中已有的所有图层，初始计数设为 0
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId lyrId in lt)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lyrId, OpenMode.ForRead);
                        counts[ltr.Name] = 0;
                    }

                    // 遍历块表中的每一个块（包括模型空间、图纸空间布局以及所有外部块/嵌套块定义）
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in bt)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        foreach (ObjectId entId in btr)
                        {
                            try
                            {
                                Entity ent = tr.GetObject(entId, OpenMode.ForRead, false, false) as Entity;
                                if (ent != null)
                                {
                                    string lyrName = ent.Layer;
                                    if (counts.ContainsKey(lyrName))
                                    {
                                        counts[lyrName]++;
                                    }
                                    else
                                    {
                                        counts[lyrName] = 1;
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    tr.Commit();
                }
            }
            catch { }

            return counts;
        }

        public static PurgeReport PurgeUnusedLayers(Database db)
        {
            Stopwatch sw = Stopwatch.StartNew();
            PurgeReport report = new PurgeReport();

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    report.TotalLayersScanned = 0;

                    ObjectId curLayerId = db.Clayer;
                    string curLayerName = "";
                    try
                    {
                        LayerTableRecord curLtr = (LayerTableRecord)tr.GetObject(curLayerId, OpenMode.ForRead);
                        curLayerName = curLtr.Name;
                    }
                    catch { }

                    ObjectIdCollection candidateIds = new ObjectIdCollection();
                    Dictionary<ObjectId, string> candidateNames = new Dictionary<ObjectId, string>();

                    foreach (ObjectId id in lt)
                    {
                        report.TotalLayersScanned++;
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        string name = ltr.Name;

                        // 绝对保护图层：0 图层、Defpoints、当前活动层
                        if (name == "0" || name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase) || id == curLayerId)
                        {
                            report.ProtectedLayerNames.Add(name);
                            continue;
                        }

                        candidateIds.Add(id);
                        candidateNames[id] = name;
                    }

                    // 核心算法：调用 AutoCAD 原生官方 Purge 引用图检测器
                    // db.Purge 自动深度探测所有实体、块引用、样式、字典引用，被引用的图层会自动从 candidateIds 移除！
                    db.Purge(candidateIds);

                    report.UnusedLayersFound = candidateIds.Count;

                    // 对真正未被引用的空图层安全抹除
                    foreach (ObjectId id in candidateIds)
                    {
                        try
                        {
                            LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                            string pName = ltr.Name;
                            ltr.Erase(true);
                            report.PurgedLayersCount++;
                            report.PurgedLayerNames.Add(pName);
                        }
                        catch { }
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                report.SummaryMessage = "清理失败: " + ex.Message;
            }

            sw.Stop();
            report.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            if (report.PurgedLayersCount > 0)
            {
                report.SummaryMessage = string.Format(
                    "本次共深度扫描 {0} 个图层，成功物理清理 {1} 个未使用废弃空图层！耗时 {2} ms",
                    report.TotalLayersScanned, report.PurgedLayersCount, report.ElapsedMilliseconds);
            }
            else
            {
                report.SummaryMessage = string.Format(
                    "图纸非常健康！扫描 {0} 个图层，未发现可清理的废弃空图层。耗时 {1} ms",
                    report.TotalLayersScanned, report.ElapsedMilliseconds);
            }

            return report;
        }
    }

    public static class LayerCompareEngine
    {
        public static List<LayerInfo> ExtractCurrentLayers(Database db)
        {
            List<LayerInfo> list = new List<LayerInfo>();
            Dictionary<string, int> entityCounts = LayerPurgeService.CountLayerEntities(db);

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    ObjectId curLayerId = db.Clayer;

                    foreach (ObjectId id in lt)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        LayerInfo info = ConvertToLayerInfo(tr, ltr, id == curLayerId);
                        if (entityCounts.ContainsKey(info.Name))
                        {
                            info.EntityCount = entityCounts[info.Name];
                        }
                        list.Add(info);
                    }

                    tr.Commit();
                }
            }
            catch { }

            return list;
        }

        public static List<LayerInfo> ExtractReferenceLayers(string dwgPath)
        {
            List<LayerInfo> list = new List<LayerInfo>();
            if (string.IsNullOrEmpty(dwgPath) || !File.Exists(dwgPath)) return list;

            Database refDb = new Database(false, true);
            try
            {
                refDb.ReadDwgFile(dwgPath, FileShare.Read, true, "");
                using (Transaction tr = refDb.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(refDb.LayerTableId, OpenMode.ForRead);
                    ObjectId curId = refDb.Clayer;

                    foreach (ObjectId id in lt)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        LayerInfo info = ConvertToLayerInfo(tr, ltr, id == curId);
                        list.Add(info);
                    }

                    tr.Commit();
                }
            }
            catch { }
            finally
            {
                refDb.Dispose();
            }

            return list;
        }

        private static LayerInfo ConvertToLayerInfo(Transaction tr, LayerTableRecord ltr, bool isCurrent)
        {
            LayerInfo info = new LayerInfo();
            info.Name = ltr.Name;
            info.ColorIndex = ltr.Color.ColorIndex;
            info.IsPlottable = ltr.IsPlottable;
            info.IsFrozen = ltr.IsFrozen;
            info.IsLocked = ltr.IsLocked;
            info.IsOff = ltr.IsOff;
            info.IsCurrent = isCurrent;
            info.LineWeightValue = ltr.LineWeight;
            info.LineWeightName = ltr.LineWeight == LineWeight.ByLineWeightDefault ? "默认" : ltr.LineWeight.ToString().Replace("LineWeight", "");

            // 转换颜色描述和 UI 预览色
            info.ColorValue = GetWinFormsColor(ltr.Color);
            if (ltr.Color.ColorMethod == ColorMethod.ByAci)
            {
                info.ColorDescription = GetAciColorName(ltr.Color.ColorIndex);
            }
            else
            {
                info.ColorDescription = string.Format("RGB({0},{1},{2})", info.ColorValue.R, info.ColorValue.G, info.ColorValue.B);
            }

            // 解析线型名称
            string ltName = "Continuous";
            if (ltr.LinetypeObjectId != ObjectId.Null)
            {
                try
                {
                    LinetypeTableRecord ltRec = tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
                    if (ltRec != null) ltName = ltRec.Name;
                }
                catch { }
            }
            info.LinetypeName = ltName;

            return info;
        }

        public static System.Drawing.Color GetWinFormsColor(Autodesk.AutoCAD.Colors.Color acColor)
        {
            if (acColor == null) return System.Drawing.Color.White;
            try
            {
                if (acColor.ColorMethod == ColorMethod.ByAci)
                {
                    int ci = acColor.ColorIndex;
                    switch (ci)
                    {
                        case 1: return System.Drawing.Color.Red;
                        case 2: return System.Drawing.Color.Yellow;
                        case 3: return System.Drawing.Color.Green;
                        case 4: return System.Drawing.Color.Cyan;
                        case 5: return System.Drawing.Color.Blue;
                        case 6: return System.Drawing.Color.Magenta;
                        case 7: return System.Drawing.Color.White;
                        case 8: return System.Drawing.Color.DarkGray;
                        case 9: return System.Drawing.Color.LightGray;
                        default:
                            return System.Drawing.Color.FromArgb(acColor.ColorValue.R, acColor.ColorValue.G, acColor.ColorValue.B);
                    }
                }
                return System.Drawing.Color.FromArgb(acColor.ColorValue.R, acColor.ColorValue.G, acColor.ColorValue.B);
            }
            catch
            {
                return System.Drawing.Color.White;
            }
        }

        private static string GetAciColorName(int colorIndex)
        {
            switch (colorIndex)
            {
                case 1: return "红 (1)";
                case 2: return "黄 (2)";
                case 3: return "绿 (3)";
                case 4: return "青 (4)";
                case 5: return "蓝 (5)";
                case 6: return "洋红 (6)";
                case 7: return "黑/白 (7)";
                case 8: return "灰 (8)";
                case 9: return "浅灰 (9)";
                case 256: return "ByLayer";
                default: return "索引色 " + colorIndex;
            }
        }

        // ========================================================
        // 智能语义打分匹配系统 (Levenshtein + 行业分类词根 + 属性权重)
        // ========================================================
        private static readonly Dictionary<string, string[]> DomainKeywords = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "WALL", new string[] { "墙", "墙体", "隔墙", "剪力墙", "砖墙", "外墙", "内墙", "wall", "qt", "q" } },
            { "COLUMN", new string[] { "柱", "构造柱", "结构柱", "立柱", "柱子", "col", "column", "zh" } },
            { "DOOR", new string[] { "门", "门扇", "防火门", "卷帘门", "推拉门", "移门", "door", "dr", "men" } },
            { "WINDOW", new string[] { "窗", "百叶窗", "落地窗", "飘窗", "天窗", "win", "window", "chuang" } },
            { "TEXT", new string[] { "文字", "文本", "注释", "说明", "字体", "名称", "内容", "标示", "text", "txt", "wz", "sm", "note" } },
            { "DIM", new string[] { "标注", "尺寸", "标高", "轴号", "索引", "引出", "dim", "dimension", "bz", "lead", "leader" } },
            { "AXIS", new string[] { "轴线", "轴网", "辅助线", "网格", "中心线", "axis", "grid", "zx" } },
            { "BEAM", new string[] { "梁", "梁截面", "过梁", "圈梁", "连梁", "beam", "liang" } },
            { "FLOOR", new string[] { "地", "地面", "地坪", "铺装", "地台", "楼面", "floor", "flor", "dm" } },
            { "CEILING", new string[] { "顶", "吊顶", "天花", "顶棚", "灯具", "天花造型", "ceil", "ceiling", "dd", "th" } },
            { "FURNITURE", new string[] { "家具", "设施", "办公桌", "洁具", "厨具", "设备", "活动家具", "furn", "furniture", "jj" } },
            { "HATCH", new string[] { "填充", "图案", "剖面", "纹理", "阴影", "hatch", "pat", "tc" } },
            { "STAIR", new string[] { "楼梯", "台阶", "坡道", "踏步", "扶手", "栏杆", "stair", "lt" } },
            { "PLUMBING", new string[] { "水", "给水", "排水", "雨水", "污水", "喷淋", "消火栓", "管道", "plmb", "pipe", "sp", "water" } },
            { "ELECTRICAL", new string[] { "电", "电气", "照明", "插座", "弱电", "强电", "动力", "ele", "elec", "pow", "light" } },
            { "HVAC", new string[] { "暖", "暖通", "空调", "风管", "排烟", "送风", "hvac", "duct", "kt" } },
            { "BORDER", new string[] { "图框", "标题栏", "会签栏", "图签", "border", "title", "tk" } }
        };

        public static List<LayerMatchResult> ComputeMatches(List<LayerInfo> currentLayers, List<LayerInfo> refLayers, int minThreshold = 60)
        {
            List<LayerMatchResult> results = new List<LayerMatchResult>();

            foreach (LayerInfo cur in currentLayers)
            {
                LayerMatchResult res = new LayerMatchResult();
                res.CurrentLayer = cur;
                res.MatchScore = 0;
                res.MatchReason = "未找到对应标准图层";
                res.Level = MatchConfidenceLevel.None;
                res.IsSelectedForMerge = false;
                res.TargetLayerName = cur.Name;

                if (refLayers == null || refLayers.Count == 0)
                {
                    results.Add(res);
                    continue;
                }

                int highestScore = -1;
                LayerInfo bestRef = null;
                string bestReason = "";

                foreach (LayerInfo r in refLayers)
                {
                    string reason;
                    int score = EvaluateSimilarity(cur, r, out reason);
                    if (score > highestScore)
                    {
                        highestScore = score;
                        bestRef = r;
                        bestReason = reason;
                    }
                }

                res.MatchScore = highestScore;
                res.BestMatchRefLayer = bestRef;
                res.MatchReason = bestReason;

                if (highestScore == 100)
                {
                    res.Level = MatchConfidenceLevel.ExactMatch;
                    res.TargetLayerName = bestRef.Name;
                    // 同名但颜色/线型不一致时需规范化
                    if (cur.ColorIndex != bestRef.ColorIndex || !string.Equals(cur.LinetypeName, bestRef.LinetypeName, StringComparison.OrdinalIgnoreCase))
                    {
                        res.MatchReason = "同名图层 (属性需规范化对齐)";
                    }
                    res.IsSelectedForMerge = true;
                }
                else if (highestScore >= 80)
                {
                    res.Level = MatchConfidenceLevel.High;
                    res.TargetLayerName = bestRef.Name;
                    res.IsSelectedForMerge = true;
                }
                else if (highestScore >= 60)
                {
                    res.Level = MatchConfidenceLevel.Medium;
                    res.TargetLayerName = bestRef.Name;
                    res.IsSelectedForMerge = (highestScore >= minThreshold);
                }
                else if (highestScore >= 35)
                {
                    res.Level = MatchConfidenceLevel.Low;
                    res.TargetLayerName = bestRef.Name;
                    res.IsSelectedForMerge = false;
                }
                else
                {
                    res.Level = MatchConfidenceLevel.None;
                    res.TargetLayerName = cur.Name;
                    res.IsSelectedForMerge = false;
                }

                results.Add(res);
            }

            return results;
        }

        private static int EvaluateSimilarity(LayerInfo cur, LayerInfo refLayer, out string reason)
        {
            reason = "";
            string name1 = cur.Name.Trim();
            string name2 = refLayer.Name.Trim();

            // 1. 完全精确一致
            if (string.Equals(name1, name2, StringComparison.OrdinalIgnoreCase))
            {
                reason = "图层名 100% 精确一致";
                return 100;
            }

            // 2. 剥离标准前缀比对 (如 A-WALL vs WALL, PUB-DOOR vs DOOR)
            string clean1 = StripCommonPrefixes(name1);
            string clean2 = StripCommonPrefixes(name2);
            if (!string.IsNullOrEmpty(clean1) && string.Equals(clean1, clean2, StringComparison.OrdinalIgnoreCase))
            {
                reason = string.Format("标准核心词根一致 [{0}]", clean1);
                return 92;
            }

            // 3. 行业专业分类字典特征匹对
            string cat1 = GetDomainCategory(name1);
            string cat2 = GetDomainCategory(name2);
            if (!string.IsNullOrEmpty(cat1) && cat1 == cat2)
            {
                int score = 85;
                // 若颜色或线型也相符，加分
                if (cur.ColorIndex == refLayer.ColorIndex) score += 5;
                if (string.Equals(cur.LinetypeName, refLayer.LinetypeName, StringComparison.OrdinalIgnoreCase)) score += 5;
                reason = string.Format("专业分类吻合 ({0})", cat1);
                return Math.Min(score, 95);
            }

            // 4. 字符串编辑距离 (Levenshtein)
            double sim = CalculateStringSimilarity(name1, name2);
            int simScore = (int)(sim * 75);

            if (cur.ColorIndex == refLayer.ColorIndex && cur.ColorIndex != 256) simScore += 10;
            if (string.Equals(cur.LinetypeName, refLayer.LinetypeName, StringComparison.OrdinalIgnoreCase) && cur.LinetypeName != "Continuous") simScore += 10;

            if (simScore >= 60)
            {
                reason = string.Format("字面相似度较高 ({0}%)", (int)(sim * 100));
            }
            else
            {
                reason = "部分特征相似";
            }

            return Math.Min(simScore, 79);
        }

        private static string StripCommonPrefixes(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string s = name.ToUpperInvariant();
            string[] prefixes = new string[] { "PUB-", "A-", "S-", "D-", "M-", "E-", "P-", "TCH_", "T20_", "T30_", "ZHL_", "XY_", "ARCH-", "STR-" };
            foreach (string p in prefixes)
            {
                if (s.StartsWith(p))
                {
                    s = s.Substring(p.Length);
                    break;
                }
            }
            return s.Replace("-", "").Replace("_", "").Trim();
        }

        private static string GetDomainCategory(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string s = name.ToUpperInvariant();

            foreach (var kvp in DomainKeywords)
            {
                foreach (string kw in kvp.Value)
                {
                    if (s.Contains(kw.ToUpperInvariant()))
                    {
                        return kvp.Key;
                    }
                }
            }
            return null;
        }

        private static double CalculateStringSimilarity(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 1.0 : 0.0;
            if (string.IsNullOrEmpty(t)) return 0.0;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            int maxLen = Math.Max(n, m);
            return 1.0 - ((double)d[n, m] / maxLen);
        }
    }
}
