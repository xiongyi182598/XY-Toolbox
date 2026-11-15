using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.GraphicsInterface;
using FontWorkbench.Models;

namespace FontWorkbench.Services
{
    public class FontDiagnosisResult
    {
        public List<FontIssueItem> IssueItems { get; set; }
        public List<string> AvailableBigFonts { get; set; }
        public List<string> AvailableWesternFonts { get; set; }
        public List<string> AvailableTtfFonts { get; set; }
        public string RecommendedBigFont { get; set; }
        public string RecommendedWesternFont { get; set; }

        public FontDiagnosisResult()
        {
            IssueItems = new List<FontIssueItem>();
            AvailableBigFonts = new List<string>();
            AvailableWesternFonts = new List<string>();
            AvailableTtfFonts = new List<string>();
        }
    }

    public static class FontDiagnosisService
    {
        private static HashSet<string> _installedTtfFamilies;

        // 常见中文字体优先级候选
        private static readonly string[] CandidateBigFonts = new string[]
        {
            "gbcbig.shx", "hztxt.shx", "hz.shx", "china.shx", "bigfont.shx", "extfont.shx", "extfont2.shx", "cht.shx"
        };

        // 常见西文字体优先级候选
        private static readonly string[] CandidateWesternFonts = new string[]
        {
            "gbenor.shx", "txt.shx", "simplex.shx", "romans.shx", "isocp.shx", "complex.shx"
        };

        // 常见中文矢量 TTF 字体
        private static readonly string[] CandidateTtfFonts = new string[]
        {
            "仿宋", "仿宋_GB2312", "微软雅黑", "宋体", "黑体", "楷体", "Arial"
        };

        /// <summary>
        /// 检查 SHX 字体是否存在于本机 AutoCAD 字体搜索路径
        /// </summary>
        public static bool CheckShxFontExists(string fontFileName)
        {
            if (string.IsNullOrEmpty(fontFileName)) return false;
            string fontName = fontFileName.Trim();
            if (string.IsNullOrEmpty(fontName)) return false;

            if (!fontName.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
            {
                fontName += ".shx";
            }

            try
            {
                string path = HostApplicationServices.Current.FindFile(fontName, null, FindFileHint.FontFile);
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return true;
            }
            catch { }

            // 备用兜底：检查 AutoCAD 主程序 Fonts 目录及 ACAD 环境变量路径
            try
            {
                string acadEnv = HostApplicationServices.Current.GetEnvironmentVariable("ACAD");
                if (!string.IsNullOrEmpty(acadEnv))
                {
                    string[] dirs = acadEnv.Split(';');
                    foreach (string d in dirs)
                    {
                        if (string.IsNullOrEmpty(d)) continue;
                        string cand = Path.Combine(d.Trim(), fontName);
                        if (File.Exists(cand)) return true;
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// 检查 TTF 字体是否安装在 Windows 系统中
        /// </summary>
        public static bool CheckTtfFontExists(string typefaceName)
        {
            if (string.IsNullOrEmpty(typefaceName)) return false;
            string name = typefaceName.Trim();
            EnsureTtfFamiliesLoaded();
            return _installedTtfFamilies.Contains(name);
        }

        private static void EnsureTtfFamiliesLoaded()
        {
            if (_installedTtfFamilies != null) return;
            _installedTtfFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var fam in System.Drawing.FontFamily.Families)
                {
                    _installedTtfFamilies.Add(fam.Name);
                }
            }
            catch { }
        }

        /// <summary>
        /// 获取本机可用的大字体列表
        /// </summary>
        public static List<string> GetAvailableBigFonts()
        {
            List<string> list = new List<string>();
            foreach (string f in CandidateBigFonts)
            {
                if (CheckShxFontExists(f) && !list.Contains(f))
                {
                    list.Add(f);
                }
            }
            // 扫描 AutoCAD Fonts 目录补齐其他大字体
            try
            {
                string fontDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
                if (Directory.Exists(fontDir))
                {
                    foreach (string file in Directory.GetFiles(fontDir, "*.shx"))
                    {
                        string fn = Path.GetFileName(file).ToLower();
                        if ((fn.Contains("big") || fn.Contains("gb") || fn.Contains("hz")) && !list.Contains(fn))
                        {
                            list.Add(fn);
                        }
                    }
                }
            }
            catch { }

            if (list.Count == 0) list.Add("gbcbig.shx");
            return list;
        }

        /// <summary>
        /// 获取本机可用的西文 SHX 字体列表
        /// </summary>
        public static List<string> GetAvailableWesternFonts()
        {
            List<string> list = new List<string>();
            foreach (string f in CandidateWesternFonts)
            {
                if (CheckShxFontExists(f) && !list.Contains(f))
                {
                    list.Add(f);
                }
            }
            if (list.Count == 0) list.Add("gbenor.shx");
            return list;
        }

        /// <summary>
        /// 获取本机可用的 TTF 字体列表
        /// </summary>
        public static List<string> GetAvailableTtfFonts()
        {
            EnsureTtfFamiliesLoaded();
            List<string> list = new List<string>();
            foreach (string cand in CandidateTtfFonts)
            {
                if (_installedTtfFamilies.Contains(cand) && !list.Contains(cand))
                {
                    list.Add(cand);
                }
            }
            if (list.Count == 0) list.Add("仿宋");
            return list;
        }

        public static bool ContainsChinese(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= 0x4e00 && text[i] <= 0x9fa5) return true;
            }
            return false;
        }

        /// <summary>
        /// 全面对当前 DWG 图纸的文字样式及图元使用情况进行体检诊断
        /// </summary>
        public static FontDiagnosisResult ScanDatabase(Database db)
        {
            FontDiagnosisResult result = new FontDiagnosisResult();
            result.AvailableBigFonts = GetAvailableBigFonts();
            result.AvailableWesternFonts = GetAvailableWesternFonts();
            result.AvailableTtfFonts = GetAvailableTtfFonts();

            result.RecommendedBigFont = result.AvailableBigFonts.Count > 0 ? result.AvailableBigFonts[0] : "gbcbig.shx";
            result.RecommendedWesternFont = result.AvailableWesternFonts.Count > 0 ? result.AvailableWesternFonts[0] : "gbenor.shx";

            // 统计每个 TextStyleId 对应的文字实体数量以及是否包含中文字符
            Dictionary<ObjectId, int> styleCounts = new Dictionary<ObjectId, int>();
            Dictionary<ObjectId, bool> styleHasChinese = new Dictionary<ObjectId, bool>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt != null)
                {
                    foreach (ObjectId btrId in bt)
                    {
                        BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                        if (btr == null) continue;

                        foreach (ObjectId entId in btr)
                        {
                            Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            ObjectId styleId = ObjectId.Null;
                            string textContent = null;

                            DBText dbText = ent as DBText;
                            if (dbText != null)
                            {
                                styleId = dbText.TextStyleId;
                                textContent = dbText.TextString;
                            }
                            else
                            {
                                MText mText = ent as MText;
                                if (mText != null)
                                {
                                    styleId = mText.TextStyleId;
                                    textContent = mText.Contents;
                                }
                                else
                                {
                                    Dimension dim = ent as Dimension;
                                    if (dim != null)
                                    {
                                        ObjectId dimStyleId = dim.DimensionStyle;
                                        if (dimStyleId.IsValid && !dimStyleId.IsNull)
                                        {
                                            DimStyleTableRecord dstr = tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
                                            if (dstr != null) styleId = dstr.Dimtxsty;
                                        }
                                        textContent = dim.DimensionText;
                                    }
                                    else
                                    {
                                        Leader ldr = ent as Leader;
                                        if (ldr != null)
                                        {
                                            styleId = ldr.TextStyleId;
                                        }
                                        else
                                        {
                                            MLeader ml = ent as MLeader;
                                            if (ml != null)
                                            {
                                                styleId = ml.TextStyleId;
                                            }
                                        }
                                    }
                                }
                            }

                            if (!styleId.IsNull && styleId.IsValid)
                            {
                                if (!styleCounts.ContainsKey(styleId))
                                {
                                    styleCounts[styleId] = 0;
                                    styleHasChinese[styleId] = false;
                                }
                                styleCounts[styleId]++;
                                if (!string.IsNullOrEmpty(textContent) && ContainsChinese(textContent))
                                {
                                    styleHasChinese[styleId] = true;
                                }
                            }
                        }
                    }
                }

                // 诊断文字样式表
                TextStyleTable tst = tr.GetObject(db.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
                if (tst != null)
                {
                    foreach (ObjectId styleId in tst)
                    {
                        TextStyleTableRecord rec = tr.GetObject(styleId, OpenMode.ForRead) as TextStyleTableRecord;
                        if (rec == null) continue;

                        FontIssueItem item = new FontIssueItem();
                        item.TextStyleId = styleId;
                        item.StyleName = rec.Name;
                        item.IsDependent = rec.IsDependent;
                        item.OriginalPrimaryFont = rec.FileName ?? "";
                        item.OriginalBigFont = rec.BigFontFileName ?? "";

                        bool isTtf = !string.IsNullOrEmpty(rec.Font.TypeFace);
                        item.IsTTF = isTtf;
                        item.OriginalTtfFace = isTtf ? rec.Font.TypeFace : "";

                        int count = 0;
                        styleCounts.TryGetValue(styleId, out count);
                        item.AffectedEntityCount = count;

                        bool hasChinese = false;
                        styleHasChinese.TryGetValue(styleId, out hasChinese);
                        item.HasChineseEntities = hasChinese;

                        // 缺失诊断
                        if (isTtf)
                        {
                            bool ttfExists = CheckTtfFontExists(item.OriginalTtfFace);
                            item.IsPrimaryMissing = !ttfExists;
                            item.IsBigFontMissing = false;

                            if (!ttfExists)
                            {
                                item.IssueType = FontIssueType.MissingTTF;
                                item.IssueDescription = string.Format("缺少矢量字体: {0}", item.OriginalTtfFace);
                                // 推荐智能替换
                                string ttfName = item.OriginalTtfFace;
                                if (ttfName.Contains("仿宋") && CheckTtfFontExists("仿宋"))
                                {
                                    item.TargetTtfFamily = "仿宋";
                                }
                                else if (ttfName.Contains("黑体") && CheckTtfFontExists("黑体"))
                                {
                                    item.TargetTtfFamily = "黑体";
                                }
                                else if (ttfName.Contains("楷体") && CheckTtfFontExists("楷体"))
                                {
                                    item.TargetTtfFamily = "楷体";
                                }
                                else
                                {
                                    item.TargetTtfFamily = result.AvailableTtfFonts.Count > 0 ? result.AvailableTtfFonts[0] : "仿宋";
                                }
                                item.TargetPrimaryFont = "";
                                item.TargetBigFont = "";
                                item.IsSelected = true;
                            }
                            else
                            {
                                item.IssueType = FontIssueType.Healthy;
                                item.IssueDescription = "正常 (矢量字体已安装)";
                                item.TargetTtfFamily = item.OriginalTtfFace;
                                item.IsSelected = false;
                                item.Status = "正常";
                            }
                        }
                        else
                        {
                            // SHX 字体诊断
                            bool hasPrimary = !string.IsNullOrEmpty(item.OriginalPrimaryFont);
                            bool primaryExists = hasPrimary ? CheckShxFontExists(item.OriginalPrimaryFont) : true;
                            item.IsPrimaryMissing = hasPrimary && !primaryExists;

                            bool hasBig = !string.IsNullOrEmpty(item.OriginalBigFont);
                            bool bigExists = hasBig ? CheckShxFontExists(item.OriginalBigFont) : true;
                            item.IsBigFontMissing = hasBig && !bigExists;

                            // 默认目标保留原有值（如果存在）
                            item.TargetPrimaryFont = primaryExists && hasPrimary ? item.OriginalPrimaryFont : result.RecommendedWesternFont;
                            item.TargetBigFont = bigExists && hasBig ? item.OriginalBigFont : result.RecommendedBigFont;

                            if (item.IsPrimaryMissing && item.IsBigFontMissing)
                            {
                                item.IssueType = FontIssueType.MissingBoth;
                                item.IssueDescription = string.Format("主字体({0})和大字体({1})均缺失", item.OriginalPrimaryFont, item.OriginalBigFont);
                                item.IsSelected = true;
                            }
                            else if (item.IsBigFontMissing)
                            {
                                item.IssueType = FontIssueType.MissingBigFont;
                                item.IssueDescription = string.Format("缺少大字体: {0} (导致中文显示为 ?????)", item.OriginalBigFont);
                                item.TargetPrimaryFont = hasPrimary ? item.OriginalPrimaryFont : result.RecommendedWesternFont;
                                item.TargetBigFont = result.RecommendedBigFont;
                                item.IsSelected = true;
                            }
                            else if (item.IsPrimaryMissing)
                            {
                                item.IssueType = FontIssueType.MissingPrimaryFont;
                                item.IssueDescription = string.Format("缺少西文字体: {0}", item.OriginalPrimaryFont);
                                item.TargetPrimaryFont = result.RecommendedWesternFont;
                                item.TargetBigFont = hasBig ? item.OriginalBigFont : result.RecommendedBigFont;
                                item.IsSelected = true;
                            }
                            else if (!hasBig && hasChinese)
                            {
                                // 单字节西文字体无大字体，但实际有中文内容 -> 必然显示为 ?????
                                item.IssueType = FontIssueType.NoBigFontForChinese;
                                item.IssueDescription = "未配置大字体但包含中文 (单字节字体导致中文显示为 ?????)";
                                item.TargetPrimaryFont = hasPrimary ? item.OriginalPrimaryFont : result.RecommendedWesternFont;
                                item.TargetBigFont = result.RecommendedBigFont;
                                item.IsSelected = true;
                            }
                            else
                            {
                                item.IssueType = FontIssueType.Healthy;
                                item.IssueDescription = "正常 (字体完整)";
                                item.IsSelected = false;
                                item.Status = "正常";
                            }
                        }

                        result.IssueItems.Add(item);
                    }
                }

                tr.Commit();
            }

            // 排序：有问题的排在前面，按受影响图元数量降序排列
            result.IssueItems.Sort((a, b) =>
            {
                if (a.IssueType != FontIssueType.Healthy && b.IssueType == FontIssueType.Healthy) return -1;
                if (a.IssueType == FontIssueType.Healthy && b.IssueType != FontIssueType.Healthy) return 1;
                return b.AffectedEntityCount.CompareTo(a.AffectedEntityCount);
            });

            return result;
        }

        /// <summary>
        /// 执行批量字体修复 (带文档锁 DocumentLock 与外部参照 Xref 智能保护)
        /// </summary>
        public static void ApplyFontFixes(
            Document doc,
            Database db,
            IEnumerable<FontIssueItem> itemsToFix,
            bool updateAcadFmp,
            bool cleanMTextOverrides,
            out int stylesFixedCount,
            out int mtextsCleanedCount,
            out int fmpRulesAddedCount)
        {
            stylesFixedCount = 0;
            mtextsCleanedCount = 0;
            fmpRulesAddedCount = 0;

            if (db == null && doc != null) db = doc.Database;
            if (db == null) return;

            List<KeyValuePair<string, string>> fmpMappings = new List<KeyValuePair<string, string>>();

            DocumentLock docLock = null;
            if (doc != null)
            {
                try { docLock = doc.LockDocument(); } catch { }
            }

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (FontIssueItem item in itemsToFix)
                    {
                        if (item.TextStyleId.IsNull || !item.TextStyleId.IsValid) continue;

                        // 外部参照 (XREF) 依赖的文字样式在宿主图中属于只读实体，通过写入 acad.fmp 映射规则全局自动解决
                        if (item.IsDependent)
                        {
                            item.Status = "已映射(参照)";
                            stylesFixedCount++;

                            if (updateAcadFmp)
                            {
                                if (item.IsTTF && !string.IsNullOrEmpty(item.OriginalTtfFace) && !string.IsNullOrEmpty(item.TargetTtfFamily))
                                {
                                    fmpMappings.Add(new KeyValuePair<string, string>(item.OriginalTtfFace, item.TargetTtfFamily));
                                }
                                else
                                {
                                    if (item.IsBigFontMissing && !string.IsNullOrEmpty(item.OriginalBigFont) && !string.IsNullOrEmpty(item.TargetBigFont))
                                    {
                                        fmpMappings.Add(new KeyValuePair<string, string>(item.OriginalBigFont, item.TargetBigFont));
                                    }
                                    if (item.IsPrimaryMissing && !string.IsNullOrEmpty(item.OriginalPrimaryFont) && !string.IsNullOrEmpty(item.TargetPrimaryFont))
                                    {
                                        fmpMappings.Add(new KeyValuePair<string, string>(item.OriginalPrimaryFont, item.TargetPrimaryFont));
                                    }
                                }
                            }
                            continue;
                        }

                        // 宿主图本地文字样式：直接安全修改文字样式表记录
                        try
                        {
                            TextStyleTableRecord rec = tr.GetObject(item.TextStyleId, OpenMode.ForWrite) as TextStyleTableRecord;
                            if (rec == null) continue;

                            if (item.IsTTF && !string.IsNullOrEmpty(item.TargetTtfFamily))
                            {
                                rec.Font = new FontDescriptor(item.TargetTtfFamily, false, false, 134, 49);
                                rec.FileName = "";
                                rec.BigFontFileName = "";
                                item.Status = "已修复";
                                stylesFixedCount++;

                                if (updateAcadFmp && !string.IsNullOrEmpty(item.OriginalTtfFace))
                                {
                                    fmpMappings.Add(new KeyValuePair<string, string>(item.OriginalTtfFace, item.TargetTtfFamily));
                                }
                            }
                            else
                            {
                                // SHX 字体替换
                                if (!string.IsNullOrEmpty(item.TargetPrimaryFont))
                                {
                                    rec.FileName = item.TargetPrimaryFont;
                                }
                                rec.BigFontFileName = item.TargetBigFont ?? "";
                                rec.Font = new FontDescriptor("", false, false, 0, 0);

                                item.Status = "已修复";
                                stylesFixedCount++;

                                if (updateAcadFmp)
                                {
                                    if (item.IsBigFontMissing && !string.IsNullOrEmpty(item.OriginalBigFont) && !string.IsNullOrEmpty(item.TargetBigFont))
                                    {
                                        fmpMappings.Add(new KeyValuePair<string, string>(item.OriginalBigFont, item.TargetBigFont));
                                    }
                                    if (item.IsPrimaryMissing && !string.IsNullOrEmpty(item.OriginalPrimaryFont) && !string.IsNullOrEmpty(item.TargetPrimaryFont))
                                    {
                                        fmpMappings.Add(new KeyValuePair<string, string>(item.OriginalPrimaryFont, item.TargetPrimaryFont));
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    // 清理 MText 行内字体硬编码覆盖 (严格跳过外部参照块，杜绝 eLockViolation / eNotApplicable)
                    if (cleanMTextOverrides)
                    {
                        try
                        {
                            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                            if (bt != null)
                            {
                                foreach (ObjectId btrId in bt)
                                {
                                    BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                                    if (btr == null || btr.IsFromExternalReference || btr.IsDependent) continue;

                                    foreach (ObjectId entId in btr)
                                    {
                                        try
                                        {
                                            MText mt = tr.GetObject(entId, OpenMode.ForRead) as MText;
                                            if (mt == null) continue;

                                            string contents = mt.Contents;
                                            if (string.IsNullOrEmpty(contents)) continue;

                                            if (contents.IndexOf("\\f", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                contents.IndexOf("\\F", StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                string cleaned = Regex.Replace(contents, @"\\[fF][^;]*;", "");
                                                if (!string.Equals(contents, cleaned, StringComparison.Ordinal))
                                                {
                                                    mt.UpgradeOpen();
                                                    mt.Contents = cleaned;
                                                    mtextsCleanedCount++;
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    tr.Commit();
                }
            }
            finally
            {
                if (docLock != null)
                {
                    try { docLock.Dispose(); } catch { }
                }
            }

            // 同步写入 acad.fmp
            if (updateAcadFmp && fmpMappings.Count > 0)
            {
                fmpRulesAddedCount = AppendToAcadFmp(fmpMappings);
            }

            // 触发全图显式重生成，确保所有视口文字立即脱去问号恢复显示
            try
            {
                if (doc != null && doc.Editor != null)
                {
                    doc.Editor.Regen();
                }
                else
                {
                    Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                    if (ed != null) ed.Regen();
                }
            }
            catch { }
        }

        public static void ApplyFontFixes(
            Database db,
            IEnumerable<FontIssueItem> itemsToFix,
            bool updateAcadFmp,
            bool cleanMTextOverrides,
            out int stylesFixedCount,
            out int mtextsCleanedCount,
            out int fmpRulesAddedCount)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            ApplyFontFixes(doc, db, itemsToFix, updateAcadFmp, cleanMTextOverrides, out stylesFixedCount, out mtextsCleanedCount, out fmpRulesAddedCount);
        }

        /// <summary>
        /// 将缺失字体与替换字体的映射规则安全写入本机的 acad.fmp 映射表
        /// </summary>
        public static int AppendToAcadFmp(List<KeyValuePair<string, string>> mappings)
        {
            if (mappings == null || mappings.Count == 0) return 0;

            string fmpPath = GetAcadFmpPath();
            if (string.IsNullOrEmpty(fmpPath) || !File.Exists(fmpPath)) return 0;

            int addedCount = 0;
            try
            {
                string[] lines = File.ReadAllLines(fmpPath, Encoding.Default);
                HashSet<string> existingRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) continue;
                    int semi = trimmed.IndexOf(';');
                    if (semi > 0)
                    {
                        string src = trimmed.Substring(0, semi).Trim();
                        existingRules.Add(src);
                    }
                }

                List<string> appendLines = new List<string>();
                appendLines.Add("");
                appendLines.Add("; --- [XY工具箱] 智能字体修复自动映射规则 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ---");

                foreach (var pair in mappings)
                {
                    string src = pair.Key.Trim();
                    string dst = pair.Value.Trim();
                    if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst)) continue;

                    string srcNoExt = Path.GetFileNameWithoutExtension(src);
                    string dstNoExt = Path.GetFileNameWithoutExtension(dst);

                    // 写入完整名规则 (如 tssdchn.shx;gbcbig.shx)
                    if (!existingRules.Contains(src))
                    {
                        appendLines.Add(string.Format("{0};{1}", src, dst));
                        existingRules.Add(src);
                        addedCount++;
                    }

                    // 写入无后缀名规则 (如 tssdchn;gbcbig.shx)
                    if (!string.Equals(src, srcNoExt, StringComparison.OrdinalIgnoreCase) && !existingRules.Contains(srcNoExt))
                    {
                        appendLines.Add(string.Format("{0};{1}", srcNoExt, dst));
                        existingRules.Add(srcNoExt);
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                {
                    File.AppendAllLines(fmpPath, appendLines, Encoding.Default);
                }
            }
            catch { }

            return addedCount;
        }

        private static string GetAcadFmpPath()
        {
            try
            {
                object fontMapVar = Application.GetSystemVariable("FONTMAP");
                if (fontMapVar != null)
                {
                    string p = fontMapVar.ToString();
                    if (File.Exists(p)) return p;
                }
            }
            catch { }

            try
            {
                string p = HostApplicationServices.Current.FindFile("acad.fmp", null, FindFileHint.Default);
                if (File.Exists(p)) return p;
            }
            catch { }

            // 遍历 Roaming Autodesk 目录
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string autodeskDir = Path.Combine(appData, "Autodesk");
                if (Directory.Exists(autodeskDir))
                {
                    string[] matches = Directory.GetFiles(autodeskDir, "acad.fmp", SearchOption.AllDirectories);
                    if (matches != null && matches.Length > 0)
                    {
                        return matches[0];
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
