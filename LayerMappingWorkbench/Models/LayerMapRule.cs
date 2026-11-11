using System;

namespace LayerMappingWorkbench.Models
{
    public enum MatchStatus
    {
        Unmatched = 0,    // 未匹配（源图层无对应目标）
        AutoGuessed = 1,  // 智能推测匹配（语义词根或特征加权命中）
        Matched = 2,      // 精确锁定匹配（规则库命中或用户明确指定）
        TargetOnly = 3    // 仅目标层存在（用于对标补齐）
    }

    /// <summary>
    /// 图层映射持久化规则契约模型 (支持 CSV / JSON 导入导出)
    /// </summary>
    public class LayerMapRule
    {
        public string SourceLayer { get; set; }
        public string TargetLayer { get; set; }
        public bool ForceByLayer { get; set; }
        public bool KeepZeroLayer { get; set; }
        public bool PurgeSource { get; set; }

        public LayerMapRule()
        {
            SourceLayer = string.Empty;
            TargetLayer = string.Empty;
            ForceByLayer = true;
            KeepZeroLayer = true;
            PurgeSource = true;
        }

        public LayerMapRule(string source, string target, bool forceByLayer)
        {
            SourceLayer = source ?? string.Empty;
            TargetLayer = target ?? string.Empty;
            ForceByLayer = forceByLayer;
            KeepZeroLayer = true;
            PurgeSource = true;
        }
    }
}
