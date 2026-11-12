using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TextFilterWorkbench.Models;

namespace TextFilterWorkbench.Services
{
    /// <summary>
    /// 吸取采样的文字实体详情
    /// </summary>
    public class TextSampleInfo
    {
        public ObjectId ObjectId { get; set; }
        public string EntityTypeName { get; set; } // DBText / MText / AttributeReference
        public double Height { get; set; }
        public string TextStyleName { get; set; }
        public string FontName { get; set; }
        public string BigFontName { get; set; }
        public short ColorIndex { get; set; }          // 原始颜色索引 (256=ByLayer)
        public short EffectiveColorIndex { get; set; } // 穿透图层后的实际生效颜色
        public bool IsByLayer { get; set; }
        public string LayerName { get; set; }
        public string Content { get; set; }
        public string CleanContent { get; set; }

        public string GetColorDisplayName()
        {
            string baseName = TextFilterService.GetAciColorName(EffectiveColorIndex);
            if (IsByLayer)
            {
                return string.Format("{0} ({1}) [随层]", baseName, EffectiveColorIndex);
            }
            return string.Format("{0} ({1})", baseName, ColorIndex);
        }
    }

    /// <summary>
    /// 文字过滤与批量修改核心业务服务
    /// </summary>
    public class TextFilterService
    {
        private static readonly Regex MTextFormatRegex = new Regex(
            @"\\(?:[ACFHQSWacfhqsw][^;]*;|[LOPKMlopkm]|\\[{}]|P|~)",
            RegexOptions.Compiled);

        /// <summary>
        /// 清理 MText 富文本格式标签，提取纯文本字符
        /// </summary>
        public static string StripMTextFormatting(string mtext)
        {
            if (string.IsNullOrEmpty(mtext)) return string.Empty;
            string s = mtext;
            // 递归清理外层括号
            while (s.StartsWith("{") && s.EndsWith("}") && s.Length >= 2)
            {
                s = s.Substring(1, s.Length - 2);
            }
            s = MTextFormatRegex.Replace(s, string.Empty);
            s = s.Replace("{}", string.Empty);
            s = s.Replace("\\{", "{").Replace("\\}", "}").Replace("\\\\", "\\");
            return s.Trim();
        }

        /// <summary>
        /// 从指定的实体吸取文字样本属性
        /// </summary>
        public static TextSampleInfo ExtractSample(ObjectId entId, Database db, Transaction tr)
        {
            if (entId.IsNull || !entId.IsValid) return null;

            Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
            if (ent == null) return null;

            TextSampleInfo info = new TextSampleInfo();
            info.ObjectId = entId;
            info.LayerName = ent.Layer;

            // 1. 颜色与随层解析
            info.ColorIndex = (short)ent.Color.ColorIndex;
            info.IsByLayer = ent.Color.IsByLayer;
            if (info.IsByLayer)
            {
                // 打开图层读取图层颜色
                info.EffectiveColorIndex = GetLayerColorIndex(ent.LayerId, tr);
            }
            else
            {
                info.EffectiveColorIndex = info.ColorIndex;
            }

            // 2. 类型与字高、样式提取
            ObjectId textStyleId = ObjectId.Null;
            if (ent is DBText)
            {
                DBText dbText = (DBText)ent;
                info.EntityTypeName = "单行文字 (DBText)";
                info.Height = dbText.Height;
                info.Content = dbText.TextString;
                info.CleanContent = dbText.TextString;
                textStyleId = dbText.TextStyleId;
            }
            else if (ent is MText)
            {
                MText mText = (MText)ent;
                info.EntityTypeName = "多行文字 (MText)";
                info.Height = mText.TextHeight;
                info.Content = mText.Contents;
                info.CleanContent = StripMTextFormatting(mText.Contents);
                textStyleId = mText.TextStyleId;
            }
            else if (ent is AttributeReference)
            {
                AttributeReference att = (AttributeReference)ent;
                info.EntityTypeName = "块属性 (AttributeReference)";
                info.Height = att.Height;
                info.Content = att.TextString;
                info.CleanContent = att.TextString;
                textStyleId = att.TextStyleId;
            }
            else
            {
                return null; // 不是文字对象
            }

            // 3. 解析文字样式与物理字体
            if (!textStyleId.IsNull && textStyleId.IsValid)
            {
                TextStyleTableRecord tstr = tr.GetObject(textStyleId, OpenMode.ForRead) as TextStyleTableRecord;
                if (tstr != null)
                {
                    info.TextStyleName = tstr.Name;
                    info.BigFontName = tstr.BigFontFileName;
                    if (!string.IsNullOrEmpty(tstr.Font.TypeFace))
                    {
                        info.FontName = tstr.Font.TypeFace;
                    }
                    else
                    {
                        info.FontName = tstr.FileName;
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// 获取图层颜色号 (ACI)
        /// </summary>
        public static short GetLayerColorIndex(ObjectId layerId, Transaction tr)
        {
            if (layerId.IsNull || !layerId.IsValid) return 7;
            try
            {
                LayerTableRecord ltr = tr.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;
                if (ltr != null)
                {
                    return (short)ltr.Color.ColorIndex;
                }
            }
            catch
            {
            }
            return 7;
        }

        /// <summary>
        /// 执行文字检索过滤，返回符合条件的所有实体 ObjectId 列表
        /// </summary>
        public static List<ObjectId> FilterTexts(Database db, Transaction tr, TextFilterCriteria criteria, IEnumerable<ObjectId> scopeEntities = null)
        {
            List<ObjectId> matched = new List<ObjectId>();
            if (criteria == null) return matched;

            // 预先缓存图层颜色字典，避免海量遍历时反复查询图层表
            Dictionary<ObjectId, short> layerColorCache = new Dictionary<ObjectId, short>();

            // 预编译正则 (如果开启了正则匹配)
            Regex contentRegex = null;
            if (criteria.FilterByContent && !string.IsNullOrEmpty(criteria.ContentPattern))
            {
                if (criteria.ContentMatchType == ContentMatchType.WildcardOrRegex)
                {
                    try
                    {
                        string pattern = criteria.ContentPattern;
                        // 如果用户输入的是 AutoCAD 类似 *test* 的通配符，转为正则
                        if (pattern.Contains("*") || pattern.Contains("?"))
                        {
                            pattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                        }
                        RegexOptions options = criteria.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                        contentRegex = new Regex(pattern, options);
                    }
                    catch
                    {
                        contentRegex = null;
                    }
                }
            }

            IEnumerable<ObjectId> candidateIds = scopeEntities;
            if (candidateIds == null)
            {
                // 当前空间 (模型空间或当前布局视口所在空间)
                BlockTableRecord curSpace = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;
                if (curSpace == null) return matched;
                List<ObjectId> list = new List<ObjectId>();
                foreach (ObjectId id in curSpace)
                {
                    list.Add(id);
                }
                candidateIds = list;
            }

            foreach (ObjectId id in candidateIds)
            {
                if (id.IsNull || !id.IsValid || id.IsErased) continue;

                // 尝试快速检查是否为支持类型
                string dxfName = id.ObjectClass.DxfName.ToUpper();
                bool isText = (dxfName == "TEXT");
                bool isMText = (dxfName == "MTEXT");
                bool isInsert = (dxfName == "INSERT");
                bool isAttRef = (dxfName == "ATTRIB");

                if (!isText && !isMText && !isInsert && !isAttRef)
                {
                    continue;
                }

                if (isText && criteria.IncludeDBText)
                {
                    DBText txt = tr.GetObject(id, OpenMode.ForRead) as DBText;
                    if (txt != null && MatchDBText(txt, criteria, tr, layerColorCache, contentRegex))
                    {
                        matched.Add(id);
                    }
                }
                else if (isMText && criteria.IncludeMText)
                {
                    MText mtxt = tr.GetObject(id, OpenMode.ForRead) as MText;
                    if (mtxt != null && MatchMText(mtxt, criteria, tr, layerColorCache, contentRegex))
                    {
                        matched.Add(id);
                    }
                }
                else if (isAttRef && criteria.IncludeAttribute)
                {
                    AttributeReference att = tr.GetObject(id, OpenMode.ForRead) as AttributeReference;
                    if (att != null && MatchAttribute(att, criteria, tr, layerColorCache, contentRegex))
                    {
                        matched.Add(id);
                    }
                }
                else if (isInsert && criteria.IncludeAttribute)
                {
                    // 检查块参照内部的属性定义
                    BlockReference blk = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                    if (blk != null && blk.AttributeCollection != null && blk.AttributeCollection.Count > 0)
                    {
                        foreach (ObjectId attId in blk.AttributeCollection)
                        {
                            if (attId.IsNull || !attId.IsValid || attId.IsErased) continue;
                            AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                            if (att != null && MatchAttribute(att, criteria, tr, layerColorCache, contentRegex))
                            {
                                matched.Add(attId);
                            }
                        }
                    }
                }
            }

            return matched;
        }

        #region 实体匹配判定

        private static bool MatchDBText(DBText txt, TextFilterCriteria criteria, Transaction tr, Dictionary<ObjectId, short> layerColorCache, Regex contentRegex)
        {
            // 1. 图层匹配
            if (criteria.FilterByLayer && !string.IsNullOrEmpty(criteria.LayerName))
            {
                if (!string.Equals(txt.Layer, criteria.LayerName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // 2. 字高匹配
            if (criteria.FilterByHeight)
            {
                if (!MatchHeight(txt.Height, criteria))
                    return false;
            }

            // 3. 颜色匹配 (含随层穿透解析)
            if (criteria.FilterByColor)
            {
                if (!MatchColor(txt.Color, txt.LayerId, criteria, tr, layerColorCache))
                    return false;
            }

            // 4. 文字样式与字体匹配
            if (criteria.FilterByStyle || criteria.FilterByFont)
            {
                if (!MatchStyleAndFont(txt.TextStyleId, criteria, tr))
                    return false;
            }

            // 5. 文字内容匹配
            if (criteria.FilterByContent && !string.IsNullOrEmpty(criteria.ContentPattern))
            {
                if (!MatchContent(txt.TextString, criteria, contentRegex))
                    return false;
            }

            return true;
        }

        private static bool MatchMText(MText mtxt, TextFilterCriteria criteria, Transaction tr, Dictionary<ObjectId, short> layerColorCache, Regex contentRegex)
        {
            // 1. 图层匹配
            if (criteria.FilterByLayer && !string.IsNullOrEmpty(criteria.LayerName))
            {
                if (!string.Equals(mtxt.Layer, criteria.LayerName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // 2. 字高匹配 (MText 的基准字高 TextHeight)
            if (criteria.FilterByHeight)
            {
                if (!MatchHeight(mtxt.TextHeight, criteria))
                    return false;
            }

            // 3. 颜色匹配
            if (criteria.FilterByColor)
            {
                if (!MatchColor(mtxt.Color, mtxt.LayerId, criteria, tr, layerColorCache))
                    return false;
            }

            // 4. 文字样式与字体匹配
            if (criteria.FilterByStyle || criteria.FilterByFont)
            {
                if (!MatchStyleAndFont(mtxt.TextStyleId, criteria, tr))
                    return false;
            }

            // 5. 文字内容匹配 (清洗富文本内联标记后比对)
            if (criteria.FilterByContent && !string.IsNullOrEmpty(criteria.ContentPattern))
            {
                string cleanText = StripMTextFormatting(mtxt.Contents);
                if (!MatchContent(cleanText, criteria, contentRegex))
                    return false;
            }

            return true;
        }

        private static bool MatchAttribute(AttributeReference att, TextFilterCriteria criteria, Transaction tr, Dictionary<ObjectId, short> layerColorCache, Regex contentRegex)
        {
            if (criteria.FilterByLayer && !string.IsNullOrEmpty(criteria.LayerName))
            {
                if (!string.Equals(att.Layer, criteria.LayerName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (criteria.FilterByHeight)
            {
                if (!MatchHeight(att.Height, criteria))
                    return false;
            }

            if (criteria.FilterByColor)
            {
                if (!MatchColor(att.Color, att.LayerId, criteria, tr, layerColorCache))
                    return false;
            }

            if (criteria.FilterByStyle || criteria.FilterByFont)
            {
                if (!MatchStyleAndFont(att.TextStyleId, criteria, tr))
                    return false;
            }

            if (criteria.FilterByContent && !string.IsNullOrEmpty(criteria.ContentPattern))
            {
                if (!MatchContent(att.TextString, criteria, contentRegex))
                    return false;
            }

            return true;
        }

        private static bool MatchHeight(double entityHeight, TextFilterCriteria criteria)
        {
            double tol = criteria.HeightTolerance;
            if (tol < 0.0001) tol = 0.0001;

            switch (criteria.HeightOperator)
            {
                case HeightCompareOperator.Equal:
                    return Math.Abs(entityHeight - criteria.HeightValue) <= tol;

                case HeightCompareOperator.GreaterEqual:
                    return entityHeight >= (criteria.HeightValue - tol);

                case HeightCompareOperator.LessEqual:
                    return entityHeight <= (criteria.HeightValue + tol);

                case HeightCompareOperator.Range:
                    double min = Math.Min(criteria.HeightValue, criteria.HeightMax);
                    double max = Math.Max(criteria.HeightValue, criteria.HeightMax);
                    return entityHeight >= (min - tol) && entityHeight <= (max + tol);

                default:
                    return Math.Abs(entityHeight - criteria.HeightValue) <= tol;
            }
        }

        private static bool MatchColor(Color entColor, ObjectId layerId, TextFilterCriteria criteria, Transaction tr, Dictionary<ObjectId, short> layerColorCache)
        {
            short directColorIndex = (short)entColor.ColorIndex;

            if (criteria.ResolveByLayerEffectiveColor)
            {
                short effectiveIndex = directColorIndex;
                if (entColor.IsByLayer)
                {
                    if (!layerColorCache.TryGetValue(layerId, out effectiveIndex))
                    {
                        effectiveIndex = GetLayerColorIndex(layerId, tr);
                        layerColorCache[layerId] = effectiveIndex;
                    }
                }

                // 如果目标过滤是具体颜色 (1-255)
                if (criteria.ColorIndex != 256)
                {
                    return effectiveIndex == criteria.ColorIndex;
                }
                else
                {
                    // 目标也是 ByLayer
                    return entColor.IsByLayer;
                }
            }
            else
            {
                // 仅比对直接实体颜色
                return directColorIndex == criteria.ColorIndex;
            }
        }

        private static bool MatchStyleAndFont(ObjectId styleId, TextFilterCriteria criteria, Transaction tr)
        {
            if (styleId.IsNull || !styleId.IsValid) return false;

            TextStyleTableRecord tstr = tr.GetObject(styleId, OpenMode.ForRead) as TextStyleTableRecord;
            if (tstr == null) return false;

            if (criteria.FilterByStyle && !string.IsNullOrEmpty(criteria.TextStyleName))
            {
                if (!string.Equals(tstr.Name, criteria.TextStyleName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (criteria.FilterByFont && !string.IsNullOrEmpty(criteria.FontName))
            {
                string targetFont = criteria.FontName.Trim();
                bool fontMatch = false;

                if (!string.IsNullOrEmpty(tstr.Font.TypeFace) &&
                    tstr.Font.TypeFace.IndexOf(targetFont, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fontMatch = true;
                }
                else if (!string.IsNullOrEmpty(tstr.FileName) &&
                    tstr.FileName.IndexOf(targetFont, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fontMatch = true;
                }
                else if (!string.IsNullOrEmpty(tstr.BigFontFileName) &&
                    tstr.BigFontFileName.IndexOf(targetFont, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fontMatch = true;
                }

                if (!fontMatch) return false;
            }

            return true;
        }

        private static bool MatchContent(string entityText, TextFilterCriteria criteria, Regex contentRegex)
        {
            if (entityText == null) entityText = string.Empty;
            string pattern = criteria.ContentPattern;
            if (string.IsNullOrEmpty(pattern)) return true;

            StringComparison comp = criteria.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            switch (criteria.ContentMatchType)
            {
                case ContentMatchType.Contains:
                    return entityText.IndexOf(pattern, comp) >= 0;

                case ContentMatchType.Exact:
                    return string.Equals(entityText, pattern, comp);

                case ContentMatchType.StartsWith:
                    return entityText.StartsWith(pattern, comp);

                case ContentMatchType.EndsWith:
                    return entityText.EndsWith(pattern, comp);

                case ContentMatchType.WildcardOrRegex:
                    if (contentRegex != null)
                    {
                        return contentRegex.IsMatch(entityText);
                    }
                    return entityText.IndexOf(pattern, comp) >= 0;

                default:
                    return entityText.IndexOf(pattern, comp) >= 0;
            }
        }

        #endregion

        #region 批量修改

        /// <summary>
        /// 批量修改指定文字实体的属性
        /// </summary>
        public static int BatchModifyTexts(Database db, IEnumerable<ObjectId> targetIds, double? newHeight, string newStyleName, short? newColorIndex, string newLayerName)
        {
            if (targetIds == null) return 0;
            int modifiedCount = 0;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 解析文字样式 Id
                ObjectId targetStyleId = ObjectId.Null;
                if (!string.IsNullOrEmpty(newStyleName))
                {
                    TextStyleTable st = tr.GetObject(db.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
                    if (st != null && st.Has(newStyleName))
                    {
                        targetStyleId = st[newStyleName];
                    }
                }

                // 解析图层存在性
                bool layerValid = false;
                if (!string.IsNullOrEmpty(newLayerName))
                {
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (lt != null && lt.Has(newLayerName))
                    {
                        layerValid = true;
                    }
                }

                foreach (ObjectId id in targetIds)
                {
                    if (id.IsNull || !id.IsValid || id.IsErased) continue;

                    Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    if (ent == null) continue;

                    bool changed = false;

                    // 1. 修改字高
                    if (newHeight.HasValue && newHeight.Value > 0)
                    {
                        if (ent is DBText)
                        {
                            ((DBText)ent).Height = newHeight.Value;
                            changed = true;
                        }
                        else if (ent is MText)
                        {
                            ((MText)ent).TextHeight = newHeight.Value;
                            changed = true;
                        }
                        else if (ent is AttributeReference)
                        {
                            ((AttributeReference)ent).Height = newHeight.Value;
                            changed = true;
                        }
                    }

                    // 2. 修改文字样式
                    if (!targetStyleId.IsNull)
                    {
                        if (ent is DBText)
                        {
                            ((DBText)ent).TextStyleId = targetStyleId;
                            changed = true;
                        }
                        else if (ent is MText)
                        {
                            ((MText)ent).TextStyleId = targetStyleId;
                            changed = true;
                        }
                        else if (ent is AttributeReference)
                        {
                            ((AttributeReference)ent).TextStyleId = targetStyleId;
                            changed = true;
                        }
                    }

                    // 3. 修改颜色
                    if (newColorIndex.HasValue)
                    {
                        short c = newColorIndex.Value;
                        if (c == 256)
                        {
                            ent.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                        }
                        else if (c == 0)
                        {
                            ent.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);
                        }
                        else if (c >= 1 && c <= 255)
                        {
                            ent.Color = Color.FromColorIndex(ColorMethod.ByAci, c);
                        }
                        changed = true;
                    }

                    // 4. 修改图层
                    if (layerValid)
                    {
                        ent.Layer = newLayerName;
                        changed = true;
                    }

                    if (changed)
                    {
                        modifiedCount++;
                    }
                }

                tr.Commit();
            }

            return modifiedCount;
        }

        #endregion

        #region 颜色名称辅助

        public static string GetAciColorName(short colorIndex)
        {
            switch (colorIndex)
            {
                case 1: return "红 (Red)";
                case 2: return "黄 (Yellow)";
                case 3: return "绿 (Green)";
                case 4: return "青 (Cyan)";
                case 5: return "蓝 (Blue)";
                case 6: return "洋红 (Magenta)";
                case 7: return "白/黑 (White)";
                case 8: return "深灰 (Dark Grey)";
                case 9: return "浅灰 (Light Grey)";
                case 256: return "随层 (ByLayer)";
                case 0: return "随块 (ByBlock)";
                default:
                    return string.Format("颜色 {0}", colorIndex);
            }
        }

        #endregion
    }
}
