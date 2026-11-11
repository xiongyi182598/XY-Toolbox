using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace LayerMappingWorkbench.Models
{
    public class TargetVisualMeta
    {
        public Color Color { get; set; }
        public string Linetype { get; set; }

        public TargetVisualMeta(Color color, string linetype)
        {
            Color = color;
            Linetype = string.IsNullOrEmpty(linetype) ? "Continuous" : linetype;
        }
    }

    /// <summary>
    /// 双栏图层智能对位看板行数据模型 (实现 INotifyPropertyChanged 驱动 WPF 响应式视图)
    /// </summary>
    public class LayerCompareItem : INotifyPropertyChanged
    {
        // 全局目标图层视觉属性查询提供者 (当用户手动切换目标层时触发联动更新)
        public static Func<string, TargetVisualMeta> TargetMetaProvider { get; set; }

        private string _sourceLayerName;
        private Color _sourceColor;
        private SolidColorBrush _sourceColorBrush;
        private string _sourceLinetype;
        private int _entityCount;

        private MatchStatus _status;
        private int _matchConfidence;
        private string _matchNote;

        private string _selectedTargetLayerName;
        private Color _targetColor;
        private SolidColorBrush _targetColorBrush;
        private string _targetLinetype;
        private ObservableCollection<string> _availableTargetLayers;

        private bool _isSelected;

        public event PropertyChangedEventHandler PropertyChanged;

        public LayerCompareItem()
        {
            _sourceLayerName = string.Empty;
            _sourceColor = Colors.White;
            _sourceColorBrush = new SolidColorBrush(Colors.White);
            _sourceLinetype = "Continuous";
            _entityCount = 0;

            _status = MatchStatus.Unmatched;
            _matchConfidence = 0;
            _matchNote = string.Empty;

            _selectedTargetLayerName = string.Empty;
            _targetColor = Color.FromRgb(100, 110, 125);
            _targetColorBrush = new SolidColorBrush(Color.FromRgb(100, 110, 125));
            _targetLinetype = "Continuous";
            _availableTargetLayers = new ObservableCollection<string>();
            _isSelected = true;
        }

        #region 左侧：公司一（当前图纸源图层）

        public string SourceLayerName
        {
            get { return _sourceLayerName; }
            set
            {
                if (_sourceLayerName != value)
                {
                    _sourceLayerName = value;
                    OnPropertyChanged("SourceLayerName");
                }
            }
        }

        public Color SourceColor
        {
            get { return _sourceColor; }
            set
            {
                if (_sourceColor != value)
                {
                    _sourceColor = value;
                    _sourceColorBrush = new SolidColorBrush(value);
                    if (_sourceColorBrush.CanFreeze) _sourceColorBrush.Freeze();
                    OnPropertyChanged("SourceColor");
                    OnPropertyChanged("SourceColorBrush");
                }
            }
        }

        public SolidColorBrush SourceColorBrush
        {
            get { return _sourceColorBrush; }
        }

        public string SourceLinetype
        {
            get { return _sourceLinetype; }
            set
            {
                if (_sourceLinetype != value)
                {
                    _sourceLinetype = value;
                    OnPropertyChanged("SourceLinetype");
                }
            }
        }

        public int EntityCount
        {
            get { return _entityCount; }
            set
            {
                if (_entityCount != value)
                {
                    _entityCount = value;
                    OnPropertyChanged("EntityCount");
                }
            }
        }

        #endregion

        #region 中间：对齐状态与导向标

        public MatchStatus Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged("Status");
                    OnPropertyChanged("StatusText");
                    OnPropertyChanged("StatusColorBrush");
                }
            }
        }

        public string StatusText
        {
            get
            {
                switch (_status)
                {
                    case MatchStatus.Matched:
                        return "精准锁定";
                    case MatchStatus.AutoGuessed:
                        return "智能推测";
                    case MatchStatus.TargetOnly:
                        return "仅目标";
                    default:
                        return "待映射";
                }
            }
        }

        public SolidColorBrush StatusColorBrush
        {
            get
            {
                switch (_status)
                {
                    case MatchStatus.Matched:
                        return new SolidColorBrush(Color.FromRgb(39, 174, 96));     // 翠绿
                    case MatchStatus.AutoGuessed:
                        return new SolidColorBrush(Color.FromRgb(243, 156, 18));    // 暖橙
                    case MatchStatus.TargetOnly:
                        return new SolidColorBrush(Color.FromRgb(41, 128, 185));    // 沉稳蓝
                    default:
                        return new SolidColorBrush(Color.FromRgb(108, 122, 137));   // 中性暗灰
                }
            }
        }

        public string ArrowSymbol
        {
            get { return "➔"; }
        }

        public int MatchConfidence
        {
            get { return _matchConfidence; }
            set
            {
                if (_matchConfidence != value)
                {
                    _matchConfidence = value;
                    OnPropertyChanged("MatchConfidence");
                }
            }
        }

        public string MatchNote
        {
            get { return _matchNote; }
            set
            {
                if (_matchNote != value)
                {
                    _matchNote = value;
                    OnPropertyChanged("MatchNote");
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

        #endregion

        #region 右侧：公司二（目标标准图层）

        public string SelectedTargetLayerName
        {
            get { return _selectedTargetLayerName; }
            set
            {
                if (_selectedTargetLayerName != value)
                {
                    _selectedTargetLayerName = value;
                    OnPropertyChanged("SelectedTargetLayerName");
                    OnTargetLayerChanged(value);
                }
            }
        }

        private void OnTargetLayerChanged(string newTargetName)
        {
            if (string.IsNullOrEmpty(newTargetName))
            {
                Status = MatchStatus.Unmatched;
                MatchConfidence = 0;
                MatchNote = "未指定目标层";
                TargetColor = Color.FromRgb(100, 110, 125);
                TargetLinetype = "Continuous";
                return;
            }

            // 触发动态元数据联动
            if (TargetMetaProvider != null)
            {
                TargetVisualMeta meta = TargetMetaProvider(newTargetName);
                if (meta != null)
                {
                    TargetColor = meta.Color;
                    TargetLinetype = meta.Linetype;
                    Status = MatchStatus.Matched;
                    MatchConfidence = 100;
                    MatchNote = "手动选定目标层";
                    return;
                }
            }

            // 若未找到先知元数据，标记为已匹配但保留默认颜色
            Status = MatchStatus.Matched;
            MatchNote = "手动输入指定";
        }

        public Color TargetColor
        {
            get { return _targetColor; }
            set
            {
                if (_targetColor != value)
                {
                    _targetColor = value;
                    _targetColorBrush = new SolidColorBrush(value);
                    if (_targetColorBrush.CanFreeze) _targetColorBrush.Freeze();
                    OnPropertyChanged("TargetColor");
                    OnPropertyChanged("TargetColorBrush");
                }
            }
        }

        public SolidColorBrush TargetColorBrush
        {
            get { return _targetColorBrush; }
        }

        public string TargetLinetype
        {
            get { return _targetLinetype; }
            set
            {
                if (_targetLinetype != value)
                {
                    _targetLinetype = value;
                    OnPropertyChanged("TargetLinetype");
                }
            }
        }

        public ObservableCollection<string> AvailableTargetLayers
        {
            get { return _availableTargetLayers; }
            set
            {
                if (_availableTargetLayers != value)
                {
                    _availableTargetLayers = value;
                    OnPropertyChanged("AvailableTargetLayers");
                }
            }
        }

        #endregion

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
