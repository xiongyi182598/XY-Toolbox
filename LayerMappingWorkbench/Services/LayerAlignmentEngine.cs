using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using LayerMappingWorkbench.Models;
using XY_Deepening_Toolbox;

namespace LayerMappingWorkbench.Services
{
    /// <summary>
    /// 图层信息轻量实体 (用于扫描提取与语义比对)
    /// </summary>
    public class LayerMetaInfo
    {
        public string Name { get; set; }
        public System.Windows.Media.Color Color { get; set; }
        public int ColorIndex { get; set; }
        public string Linetype { get; set; }
        public int EntityCount { get; set; }
        public bool IsLocked { get; set; }
        public bool IsFrozen { get; set; }

        public LayerMetaInfo()
        {
            Name = string.Empty;
            Color = Colors.White;
            ColorIndex = 7;
            Linetype = "Continuous";
            EntityCount = 0;
        }
    }

    /// <summary>
    /// 语义加权对齐引擎 (规则库持久化 + 语义清洗 + 词根归一 + 多维特征打分)
    /// </summary>
    public class LayerAlignmentEngine
    {
        // 通用行业概念同义词根库
        private static readonly Dictionary<string, string[]> ConceptDictionary = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "WALL", new string[] { "WALL", "墙", "墙体", "QT", "WA", "BRK", "CONC" } },
            { "DOOR", new string[] { "DOOR", "门", "门窗", "MC", "DR", "ML" } },
            { "WINDOW", new string[] { "WINDOW", "窗", "窗户", "CK", "WIN", "CL" } },
            { "AXIS", new string[] { "AXIS", "轴", "轴线", "轴网", "ZHOU", "AX", "GRID" } },
            { "COLUMN", new string[] { "COLUMN", "柱", "柱子", "ZHU", "COL" } },
            { "BEAM", new string[] { "BEAM", "梁", "结构梁", "LIANG", "BM" } },
            { "HATCH", new string[] { "HATCH", "填充", "图案", "TC", "PAT" } },
            { "TEXT", new string[] { "TEXT", "文字", "文本", "WZ", "TXT" } },
            { "DIM", new string[] { "DIM", "标注", "尺寸", "BZ" } },
            { "FURN", new string[] { "FURN", "家具", "布置", "JJ", "FUR" } },
            { "FLOOR", new string[] { "FLOOR", "地面", "地坪", "铺装", "DM", "DP" } },
            { "CEILING", new string[] { "CEILING", "天花", "顶面", "吊顶", "ZM", "TH", "RCP" } },
            { "STAIR", new string[] { "STAIR", "楼梯", "台阶", "LT", "STR" } },
            { "ELEC", new string[] { "ELEC", "电气", "照明", "强电", "弱电", "DQ", "ZM" } },
            { "PLUMB", new string[] { "PLUMB", "给排水", "水施", "水管", "GPS" } },
            { "HVAC", new string[] { "HVAC", "暖通", "空调", "通风", "NT" } }
        };

        private static readonly Regex PrefixRegex = new Regex(@"^([A-Z0-9]+[_\-]|PUB_|AR_|S_|A[\-_]|D[\-_]|E[\-_]|P[\-_]|M[\-_]|0[0-9][\-_])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SuffixRegex = new Regex(@"([\-_]NEW|[\-_]OLD|[\-_]DIM|[\-_]TXT|[\-_]TEXT|[\-_]HATCH|[\-_]FULL|[\-_]THIN)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 扫描数据库中的所有图层元信息与图元数量
        /// </summary>
        public static List<LayerMetaInfo> ScanDatabaseLayers(Database db)
        {
            List<LayerMetaInfo> list = new List<LayerMetaInfo>();
            if (db == null) return list;

            Dictionary<string, int> counts = CountEntitiesPerLayer(db);

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    LayerMetaInfo info = new LayerMetaInfo();
                    info.Name = ltr.Name;
                    info.IsLocked = ltr.IsLocked;
                    info.IsFrozen = ltr.IsFrozen;

                    // 颜色提取
                    Autodesk.AutoCAD.Colors.Color cadColor = ltr.Color;
                    if (cadColor != null)
                    {
                        info.ColorIndex = cadColor.ColorIndex;
                        System.Drawing.Color dwgColor = cadColor.ColorValue;
                        info.Color = System.Windows.Media.Color.FromRgb(dwgColor.R, dwgColor.G, dwgColor.B);
                    }

                    // 线型名称
                    if (ltr.LinetypeObjectId != ObjectId.Null)
                    {
                        try
                        {
                            LinetypeTableRecord ltrLt = (LinetypeTableRecord)tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead);
                            info.Linetype = ltrLt.Name;
                        }
                        catch
                        {
                            info.Linetype = "Continuous";
                        }
                    }

                    // 图元计数
                    int c;
                    if (counts.TryGetValue(ltr.Name, out c))
                    {
                        info.EntityCount = c;
                    }

                    list.Add(info);
                }
                tr.Commit();
            }

            return list;
        }

        /// <summary>
        /// 快速统计当前图纸各图层图元数量 (模型空间 + 图纸空间)
        /// </summary>
        private static Dictionary<string, int> CountEntitiesPerLayer(Database db)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                    // 扫描模型空间与布局空间
                    ObjectId[] targetSpaces = new ObjectId[]
                    {
                        bt[BlockTableRecord.ModelSpace],
                        bt[BlockTableRecord.PaperSpace]
                    };

                    foreach (ObjectId btrId in targetSpaces)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        foreach (ObjectId entId in btr)
                        {
                            try
                            {
                                Entity ent = tr.GetObject(entId, OpenMode.ForRead, false, false) as Entity;
                                if (ent != null)
                                {
                                    string lay = ent.Layer;
                                    int current;
                                    if (counts.TryGetValue(lay, out current))
                                    {
                                        counts[lay] = current + 1;
                                    }
                                    else
                                    {
                                        counts[lay] = 1;
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

        /// <summary>
        /// 执行多级语义流水线对齐比对，输出供 UI 绑定的看板行项
        /// </summary>
        public static ObservableCollection<LayerCompareItem> AlignLayers(
            List<LayerMetaInfo> sourceLayers,
            List<string> targetLayerNames,
            Dictionary<string, LayerMetaInfo> targetLayerMap,
            List<LayerMapRule> persistedRules)
        {
            ObservableCollection<LayerCompareItem> result = new ObservableCollection<LayerCompareItem>();
            if (sourceLayers == null) return result;

            // 1. 构建规则库高速检索缓存
            Dictionary<string, LayerMapRule> ruleDict = new Dictionary<string, LayerMapRule>(StringComparer.OrdinalIgnoreCase);
            if (persistedRules != null)
            {
                foreach (LayerMapRule r in persistedRules)
                {
                    if (!string.IsNullOrEmpty(r.SourceLayer) && !ruleDict.ContainsKey(r.SourceLayer))
                    {
                        ruleDict[r.SourceLayer] = r;
                    }
                }
            }

            // 预备所有可用目标图层列表
            ObservableCollection<string> availableTargets = new ObservableCollection<string>();
            if (targetLayerNames != null)
            {
                foreach (string t in targetLayerNames)
                {
                    availableTargets.Add(t);
                }
            }

            HashSet<string> matchedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 2. 遍历源图层进行比对
            foreach (LayerMetaInfo src in sourceLayers)
            {
                LayerCompareItem item = new LayerCompareItem();
                item.SourceLayerName = src.Name;
                item.SourceColor = src.Color;
                item.SourceLinetype = src.Linetype;
                item.EntityCount = src.EntityCount;
                item.AvailableTargetLayers = availableTargets;

                // Step 1: 规则库精准命中
                LayerMapRule hitRule;
                if (ruleDict.TryGetValue(src.Name, out hitRule))
                {
                    item.Status = MatchStatus.Matched;
                    item.MatchConfidence = 100;
                    item.MatchNote = "持久化规则命中";
                    item.SelectedTargetLayerName = hitRule.TargetLayer;
                    matchedTargets.Add(hitRule.TargetLayer);

                    // 补全目标图层外观属性
                    ApplyTargetVisualProps(item, hitRule.TargetLayer, targetLayerMap);
                    result.Add(item);
                    continue;
                }

                // 同名图层直通
                if (targetLayerMap != null && targetLayerMap.ContainsKey(src.Name))
                {
                    item.Status = MatchStatus.Matched;
                    item.MatchConfidence = 100;
                    item.MatchNote = "同名图层直通对齐";
                    item.SelectedTargetLayerName = src.Name;
                    matchedTargets.Add(src.Name);
                    ApplyTargetVisualProps(item, src.Name, targetLayerMap);
                    result.Add(item);
                    continue;
                }

                // Step 2 & 3 & 4: 规范化清洗 + 概念词根 + 综合特征打分推测
                string bestTarget = null;
                int maxScore = 0;
                string bestReason = string.Empty;

                if (targetLayerNames != null)
                {
                    foreach (string tgt in targetLayerNames)
                    {
                        LayerMetaInfo tgtInfo = null;
                        if (targetLayerMap != null) targetLayerMap.TryGetValue(tgt, out tgtInfo);

                        int score = CalculateSimilarityScore(src, tgt, tgtInfo, out bestReason);
                        if (score > maxScore)
                        {
                            maxScore = score;
                            bestTarget = tgt;
                        }
                    }
                }

                if (maxScore >= 55 && !string.IsNullOrEmpty(bestTarget))
                {
                    item.Status = MatchStatus.AutoGuessed;
                    item.MatchConfidence = maxScore;
                    item.MatchNote = string.Format("智能推测({0}分): {1}", maxScore, bestReason);
                    item.SelectedTargetLayerName = bestTarget;
                    matchedTargets.Add(bestTarget);
                    ApplyTargetVisualProps(item, bestTarget, targetLayerMap);
                }
                else
                {
                    item.Status = MatchStatus.Unmatched;
                    item.MatchConfidence = maxScore;
                    item.MatchNote = "无合适对标图层";
                    item.SelectedTargetLayerName = string.Empty;
                }

                result.Add(item);
            }

            return result;
        }

        private static void ApplyTargetVisualProps(LayerCompareItem item, string targetName, Dictionary<string, LayerMetaInfo> targetMap)
        {
            if (string.IsNullOrEmpty(targetName) || targetMap == null) return;
            LayerMetaInfo info;
            if (targetMap.TryGetValue(targetName, out info))
            {
                item.TargetColor = info.Color;
                item.TargetLinetype = info.Linetype;
            }
        }

        /// <summary>
        /// 计算综合相似度得分 (词根概念 50分 + 编辑距离 30分 + 颜色 15分 + 线型 5分)
        /// </summary>
        private static int CalculateSimilarityScore(LayerMetaInfo src, string targetName, LayerMetaInfo tgtInfo, out string reason)
        {
            reason = string.Empty;
            string cleanSrc = CleanLayerName(src.Name);
            string cleanTgt = CleanLayerName(targetName);

            // 1. 词根概念判定
            string srcConcept = ExtractConcept(cleanSrc);
            string tgtConcept = ExtractConcept(cleanTgt);

            int conceptScore = 0;
            if (!string.IsNullOrEmpty(srcConcept) && !string.IsNullOrEmpty(tgtConcept) &&
                string.Equals(srcConcept, tgtConcept, StringComparison.OrdinalIgnoreCase))
            {
                conceptScore = 50;
                reason = "概念同义词根命中 [" + srcConcept + "]";
            }

            // 2. 文本编辑距离相似度
            double textSim = CalculateLevenshteinSimilarity(cleanSrc, cleanTgt);
            int nameScore = (int)(textSim * 30.0);

            // 3. 颜色相似度 (最高 15分)
            int colorScore = 0;
            if (tgtInfo != null)
            {
                if (src.ColorIndex == tgtInfo.ColorIndex && src.ColorIndex != 7)
                {
                    colorScore = 15;
                }
                else
                {
                    double dist = Math.Sqrt(
                        Math.Pow(src.Color.R - tgtInfo.Color.R, 2) +
                        Math.Pow(src.Color.G - tgtInfo.Color.G, 2) +
                        Math.Pow(src.Color.B - tgtInfo.Color.B, 2));
                    if (dist < 40) colorScore = 10;
                    else if (dist < 90) colorScore = 5;
                }
            }

            // 4. 线型相似度 (最高 5分)
            int linetypeScore = 0;
            if (tgtInfo != null && string.Equals(src.Linetype, tgtInfo.Linetype, StringComparison.OrdinalIgnoreCase))
            {
                linetypeScore = 5;
            }

            int total = conceptScore + nameScore + colorScore + linetypeScore;
            if (string.IsNullOrEmpty(reason))
            {
                reason = string.Format("字面相似度 {0}%", (int)(textSim * 100));
            }

            return Math.Min(100, total);
        }

        public static string CleanLayerName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string clean = PrefixRegex.Replace(name, "");
            clean = SuffixRegex.Replace(clean, "");
            return clean.Trim();
        }

        private static string ExtractConcept(string cleanName)
        {
            if (string.IsNullOrEmpty(cleanName)) return null;

            foreach (KeyValuePair<string, string[]> kv in ConceptDictionary)
            {
                foreach (string keyword in kv.Value)
                {
                    if (cleanName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return kv.Key;
                    }
                }
            }
            return null;
        }

        public static double CalculateLevenshteinSimilarity(string s, string t)
        {
            if (string.IsNullOrEmpty(s) && string.IsNullOrEmpty(t)) return 1.0;
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(t)) return 0.0;

            s = s.ToUpperInvariant();
            t = t.ToUpperInvariant();

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            int maxLen = Math.Max(n, m);
            return 1.0 - ((double)d[n, m] / maxLen);
        }

        #region 规则库持久化 (CSV / JSON)

        private static readonly Dictionary<string, LayerMapRule> _sessionCumulativeRules = new Dictionary<string, LayerMapRule>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 获取本次 CAD 运行生命周期（或本次画图任务工作流）中累加沉淀的所有映射规则
        /// </summary>
        public static Dictionary<string, LayerMapRule> GetSessionRules()
        {
            lock (_sessionCumulativeRules)
            {
                return new Dictionary<string, LayerMapRule>(_sessionCumulativeRules, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 统一登记合并映射记录 (被 HB1、HB2、工作台与图层对比深度合并统一调用)
        /// 具备三级沉淀能力：
        /// 1. 内存级本次绘图任务会话流 (Session Workflow)
        /// 2. 本地磁盘 layer_mapping_rules.csv 原子级增量追加更新
        /// 3. 极速翻图记忆库 (FlipRuleStorage) 实时同步
        /// </summary>
        public static void RecordSessionRule(Database db, string srcLayer, string tarLayer, bool forceByLayer)
        {
            if (string.IsNullOrEmpty(srcLayer) || string.IsNullOrEmpty(tarLayer)) return;
            if (string.Equals(srcLayer, tarLayer, StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                LayerMapRule rule = new LayerMapRule(srcLayer, tarLayer, forceByLayer);

                // 1. 记录到本次 CAD 会话工作流缓存中
                lock (_sessionCumulativeRules)
                {
                    _sessionCumulativeRules[srcLayer] = rule;
                }

                // 2. 增量更新持久化到磁盘规则文件 (layer_mapping_rules.csv)
                string dwgPath = (db != null) ? db.Filename : null;
                string csvPath = GetDefaultRuleFilePath(dwgPath);

                List<LayerMapRule> currentDiskRules = LoadRulesFromCsv(csvPath);
                Dictionary<string, LayerMapRule> diskMap = new Dictionary<string, LayerMapRule>(StringComparer.OrdinalIgnoreCase);
                foreach (LayerMapRule r in currentDiskRules)
                {
                    if (!string.IsNullOrEmpty(r.SourceLayer) && !string.IsNullOrEmpty(r.TargetLayer))
                    {
                        diskMap[r.SourceLayer] = r;
                    }
                }
                diskMap[srcLayer] = rule;
                SaveRulesToCsv(csvPath, diskMap.Values);

                // 3. 同步至极速翻图方案记忆库 (last_flip_rules.json)
                try
                {
                    FlipRulePreset preset = FlipRuleStorage.LoadLastPreset();
                    if (preset == null) preset = new FlipRulePreset();
                    if (preset.LayerMappings == null) preset.LayerMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    preset.LayerMappings[srcLayer] = tarLayer;
                    FlipRuleStorage.SaveLastPreset(preset);
                }
                catch { }
            }
            catch { }
        }

        public static string GetDefaultRuleFilePath(string dwgPath)
        {
            if (!string.IsNullOrEmpty(dwgPath) && File.Exists(dwgPath))
            {
                string dir = Path.GetDirectoryName(dwgPath);
                string localCsv = Path.Combine(dir, "layer_mapping_rules.csv");
                if (File.Exists(localCsv)) return localCsv;
            }

            string pluginDir = Path.GetDirectoryName(typeof(LayerAlignmentEngine).Assembly.Location);
            return Path.Combine(pluginDir, "layer_mapping_rules.csv");
        }

        public static List<LayerMapRule> LoadRulesFromCsv(string csvPath)
        {
            List<LayerMapRule> rules = new List<LayerMapRule>();
            if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath)) return rules;

            try
            {
                string[] lines = ReadAllLinesAutoEncoding(csvPath);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string trimmed = line.Trim();

                    List<string> parts = ParseCsvLine(trimmed);
                    if (parts.Count < 2) continue;

                    // 检查是否为表头或注释行 (支持跳过源图层/目标图层中英文表头及 #//; 注释)
                    if (IsHeaderOrComment(trimmed, parts))
                    {
                        continue;
                    }

                    string src = CleanCsvField(parts[0]);
                    string tgt = CleanCsvField(parts[1]);

                    if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

                    LayerMapRule rule = new LayerMapRule();
                    rule.SourceLayer = src;
                    rule.TargetLayer = tgt;

                    if (parts.Count >= 3)
                    {
                        bool forceByLayer;
                        string flagStr = CleanCsvField(parts[2]);
                        if (bool.TryParse(flagStr, out forceByLayer))
                        {
                            rule.ForceByLayer = forceByLayer;
                        }
                        else if (flagStr == "1" || flagStr.Equals("是", StringComparison.OrdinalIgnoreCase) || flagStr.Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            rule.ForceByLayer = true;
                        }
                    }

                    rules.Add(rule);
                }
            }
            catch { }

            return rules;
        }

        /// <summary>
        /// 智能自适应编码读取全部文本行 (完美兼顾 Excel 保存的 GBK/ANSI、UTF-8 带/不带 BOM、UTF-16)
        /// </summary>
        public static string[] ReadAllLinesAutoEncoding(string filePath)
        {
            if (!File.Exists(filePath)) return new string[0];

            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes == null || bytes.Length == 0) return new string[0];

            Encoding enc = DetectEncoding(bytes);
            string content = enc.GetString(bytes);

            return content.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        private static Encoding DetectEncoding(byte[] bytes)
        {
            // 1. 检查常见 BOM
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return new UTF8Encoding(true);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode;
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }

            // 2. 检查是否为有效 UTF-8 (无 BOM)
            if (IsValidUtf8(bytes))
            {
                return new UTF8Encoding(false);
            }

            // 3. 回退到简体中文 GBK / GB2312 (代码页 936)
            try
            {
                return Encoding.GetEncoding(936);
            }
            catch
            {
                return Encoding.Default;
            }
        }

        private static bool IsValidUtf8(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte b = bytes[i];
                if (b <= 0x7F)
                {
                    i++;
                    continue;
                }

                int nBytes;
                if ((b & 0xE0) == 0xC0) nBytes = 2;
                else if ((b & 0xF0) == 0xE0) nBytes = 3;
                else if ((b & 0xF8) == 0xF0) nBytes = 4;
                else return false;

                if (i + nBytes > bytes.Length) return false;

                for (int j = 1; j < nBytes; j++)
                {
                    if ((bytes[i + j] & 0xC0) != 0x80)
                    {
                        return false;
                    }
                }
                i += nBytes;
            }
            return true;
        }

        private static List<string> ParseCsvLine(string line)
        {
            List<string> fields = new List<string>();
            if (string.IsNullOrEmpty(line)) return fields;

            StringBuilder current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        current.Append('\"');
                        i++; // 跳过转义双引号
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString().Trim());
                    current.Length = 0;
                }
                else
                {
                    current.Append(c);
                }
            }
            fields.Add(current.ToString().Trim());
            return fields;
        }

        private static string CleanCsvField(string val)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            string s = val.Trim();
            if (s.StartsWith("\"") && s.EndsWith("\"") && s.Length >= 2)
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }
            return s;
        }

        private static bool IsHeaderOrComment(string line, List<string> parts)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#") || trimmed.StartsWith("//") || trimmed.StartsWith(";")) return true;

            if (parts.Count >= 2)
            {
                string p0 = parts[0].ToLowerInvariant();
                string p1 = parts[1].ToLowerInvariant();
                if (p0.Contains("sourcelayer") || p0.Contains("源图层") || p0.Contains("原图层") || p0.Contains("旧图层") || p0 == "source" || p0 == "from")
                    return true;
                if (p1.Contains("targetlayer") || p1.Contains("目标图层") || p1.Contains("对标图层") || p1.Contains("新图层") || p1 == "target" || p1 == "to")
                    return true;
            }
            return false;
        }

        public static bool SaveRulesToCsv(string csvPath, IEnumerable<LayerMapRule> rules)
        {
            if (string.IsNullOrEmpty(csvPath) || rules == null) return false;

            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# XY-Toolbox 施工图图层映射规则字典 (支持在 Excel/WPS 中直接编辑维护)");
                sb.AppendLine("SourceLayer(源图层),TargetLayer(目标标准图层),ForceByLayer(强制颜色线型随层)");

                foreach (LayerMapRule r in rules)
                {
                    if (string.IsNullOrEmpty(r.SourceLayer) || string.IsNullOrEmpty(r.TargetLayer)) continue;
                    string src = EscapeCsvField(r.SourceLayer);
                    string tgt = EscapeCsvField(r.TargetLayer);
                    sb.AppendLine(string.Format("{0},{1},{2}", src, tgt, r.ForceByLayer));
                }

                // 强制附带 UTF-8 BOM (\uFEFF)，确保双击在 Excel/WPS 打开时中文 100% 绝不乱码
                File.WriteAllText(csvPath, sb.ToString(), new UTF8Encoding(true));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        #endregion
    }
}
