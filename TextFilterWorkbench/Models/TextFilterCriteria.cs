using System;

namespace TextFilterWorkbench.Models
{
    public enum HeightCompareOperator
    {
        Equal,          // 相等 (支持容差)
        GreaterEqual,   // 大于等于 (>= value - tol)
        LessEqual,      // 小于等于 (<= value + tol)
        Range           // 介于范围之间 [min - tol, max + tol]
    }

    public enum ContentMatchType
    {
        Contains,       // 包含关键字
        Exact,          // 完全相等
        StartsWith,     // 开头匹配
        EndsWith,       // 结尾匹配
        WildcardOrRegex // 正则表达式或通配符
    }

    public enum SelectionScope
    {
        CurrentSpace,   // 当前空间全图 (模型空间或当前布局)
        WindowSelection,// 用户手动框选范围
        CurrentSelection// 仅在当前已有选择集中过滤
    }

    /// <summary>
    /// 文字智能选择与过滤条件数据模型
    /// </summary>
    public class TextFilterCriteria
    {
        // 实体类型筛选
        public bool IncludeDBText { get; set; }
        public bool IncludeMText { get; set; }
        public bool IncludeAttribute { get; set; }

        // 字高筛选
        public bool FilterByHeight { get; set; }
        public HeightCompareOperator HeightOperator { get; set; }
        public double HeightValue { get; set; }
        public double HeightMax { get; set; }
        public double HeightTolerance { get; set; }

        // 文字样式与字体筛选
        public bool FilterByStyle { get; set; }
        public string TextStyleName { get; set; }

        public bool FilterByFont { get; set; }
        public string FontName { get; set; }

        // 颜色筛选
        public bool FilterByColor { get; set; }
        public short ColorIndex { get; set; } // 1~255 ACI, 256: ByLayer
        public bool ResolveByLayerEffectiveColor { get; set; } // 是否穿透解析图层随层颜色

        // 图层筛选
        public bool FilterByLayer { get; set; }
        public string LayerName { get; set; }

        // 内容筛选
        public bool FilterByContent { get; set; }
        public string ContentPattern { get; set; }
        public ContentMatchType ContentMatchType { get; set; }
        public bool CaseSensitive { get; set; }

        // 范围控制
        public SelectionScope Scope { get; set; }

        public TextFilterCriteria()
        {
            IncludeDBText = true;
            IncludeMText = true;
            IncludeAttribute = true;

            FilterByHeight = false;
            HeightOperator = HeightCompareOperator.Equal;
            HeightValue = 3.5;
            HeightMax = 5.0;
            HeightTolerance = 0.05;

            FilterByStyle = false;
            TextStyleName = string.Empty;

            FilterByFont = false;
            FontName = string.Empty;

            FilterByColor = false;
            ColorIndex = 256; // 默认随层
            ResolveByLayerEffectiveColor = true;

            FilterByLayer = false;
            LayerName = string.Empty;

            FilterByContent = false;
            ContentPattern = string.Empty;
            ContentMatchType = ContentMatchType.Contains;
            CaseSensitive = false;

            Scope = SelectionScope.CurrentSpace;
        }

        public TextFilterCriteria Clone()
        {
            return new TextFilterCriteria
            {
                IncludeDBText = this.IncludeDBText,
                IncludeMText = this.IncludeMText,
                IncludeAttribute = this.IncludeAttribute,
                FilterByHeight = this.FilterByHeight,
                HeightOperator = this.HeightOperator,
                HeightValue = this.HeightValue,
                HeightMax = this.HeightMax,
                HeightTolerance = this.HeightTolerance,
                FilterByStyle = this.FilterByStyle,
                TextStyleName = this.TextStyleName,
                FilterByFont = this.FilterByFont,
                FontName = this.FontName,
                FilterByColor = this.FilterByColor,
                ColorIndex = this.ColorIndex,
                ResolveByLayerEffectiveColor = this.ResolveByLayerEffectiveColor,
                FilterByLayer = this.FilterByLayer,
                LayerName = this.LayerName,
                FilterByContent = this.FilterByContent,
                ContentPattern = this.ContentPattern,
                ContentMatchType = this.ContentMatchType,
                CaseSensitive = this.CaseSensitive,
                Scope = this.Scope
            };
        }
    }
}
