using System;
using System.ComponentModel;
using Autodesk.AutoCAD.DatabaseServices;

namespace FontWorkbench.Models
{
    public enum FontIssueType
    {
        MissingBigFont,        // 缺少大字体 (最典型：中文显示为 ?????)
        MissingPrimaryFont,    // 缺少主/西文字体 (例如 romand.shx 缺失)
        MissingBoth,           // 主字体与大字体均缺失
        MissingTTF,            // 缺少 TTF 字体 (例如 仿宋_GB2312)
        NoBigFontForChinese,   // 西文字体(如 txt.shx)但未配大字体，而图纸图元中包含中文
        Healthy                // 字体完整正常
    }

    public class FontIssueItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _targetPrimaryFont;
        private string _targetBigFont;
        private string _targetTtfFamily;
        private string _status = "待处理";

        public ObjectId TextStyleId { get; set; }
        public string StyleName { get; set; }

        // 原始配置
        public string OriginalPrimaryFont { get; set; }
        public string OriginalBigFont { get; set; }
        public bool IsTTF { get; set; }
        public string OriginalTtfFace { get; set; }

        // 诊断状态
        public bool IsPrimaryMissing { get; set; }
        public bool IsBigFontMissing { get; set; }
        public bool HasChineseEntities { get; set; }
        public FontIssueType IssueType { get; set; }
        public string IssueDescription { get; set; }
        public int AffectedEntityCount { get; set; }

        // 目标替换配置
        public string TargetPrimaryFont
        {
            get { return _targetPrimaryFont; }
            set
            {
                if (_targetPrimaryFont != value)
                {
                    _targetPrimaryFont = value;
                    OnPropertyChanged("TargetPrimaryFont");
                }
            }
        }

        public string TargetBigFont
        {
            get { return _targetBigFont; }
            set
            {
                if (_targetBigFont != value)
                {
                    _targetBigFont = value;
                    OnPropertyChanged("TargetBigFont");
                }
            }
        }

        public string TargetTtfFamily
        {
            get { return _targetTtfFamily; }
            set
            {
                if (_targetTtfFamily != value)
                {
                    _targetTtfFamily = value;
                    OnPropertyChanged("TargetTtfFamily");
                }
            }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        public string Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged("Status");
                }
            }
        }

        public string DisplaySummary
        {
            get
            {
                if (IsTTF)
                {
                    return string.Format("[TTF] {0}", string.IsNullOrEmpty(OriginalTtfFace) ? OriginalPrimaryFont : OriginalTtfFace);
                }
                string pri = string.IsNullOrEmpty(OriginalPrimaryFont) ? "(无主字体)" : OriginalPrimaryFont;
                string big = string.IsNullOrEmpty(OriginalBigFont) ? "(无大字体)" : OriginalBigFont;
                return string.Format("{0} + {1}", pri, big);
            }
        }

        public string DisplayReplacement
        {
            get
            {
                if (IsTTF)
                {
                    return string.Format("TTF -> {0}", TargetTtfFamily);
                }
                return string.Format("{0} + {1}", TargetPrimaryFont, string.IsNullOrEmpty(TargetBigFont) ? "(无)" : TargetBigFont);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propName));
        }
    }
}
