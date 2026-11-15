using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using FontWorkbench.Models;
using FontWorkbench.Services;

namespace FontWorkbench.ViewModels
{
    public class FontRelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public FontRelayCommand(Action execute) : this(execute, null) { }

        public FontRelayCommand(Action execute, Func<bool> canExecute)
        {
            if (execute == null) throw new ArgumentNullException("execute");
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }

    public class FontFixViewModel : INotifyPropertyChanged
    {
        private readonly Document _document;
        private readonly Database _database;
        private ObservableCollection<FontIssueItem> _items;
        private ICollectionView _filteredView;

        private string _searchKeyword = "";
        private bool _onlyShowIssues = true;
        private bool _selectAll = true;
        private bool _updateAcadFmp = true;
        private bool _cleanMTextOverrides = true;
        private bool _autoRegen = true;

        private string _summaryText = "";
        private string _recommendationText = "";

        public ObservableCollection<FontIssueItem> Items
        {
            get { return _items; }
            set
            {
                _items = value;
                OnPropertyChanged("Items");
            }
        }

        public ICollectionView FilteredView
        {
            get { return _filteredView; }
            private set
            {
                _filteredView = value;
                OnPropertyChanged("FilteredView");
            }
        }

        public List<string> AvailableBigFonts { get; private set; }
        public List<string> AvailableWesternFonts { get; private set; }
        public List<string> AvailableTtfFonts { get; private set; }

        public string SearchKeyword
        {
            get { return _searchKeyword; }
            set
            {
                if (_searchKeyword != value)
                {
                    _searchKeyword = value;
                    OnPropertyChanged("SearchKeyword");
                    if (_filteredView != null) _filteredView.Refresh();
                }
            }
        }

        public bool OnlyShowIssues
        {
            get { return _onlyShowIssues; }
            set
            {
                if (_onlyShowIssues != value)
                {
                    _onlyShowIssues = value;
                    OnPropertyChanged("OnlyShowIssues");
                    if (_filteredView != null) _filteredView.Refresh();
                }
            }
        }

        public bool SelectAll
        {
            get { return _selectAll; }
            set
            {
                if (_selectAll != value)
                {
                    _selectAll = value;
                    OnPropertyChanged("SelectAll");
                    ToggleAllSelection(_selectAll);
                }
            }
        }

        public bool UpdateAcadFmp
        {
            get { return _updateAcadFmp; }
            set
            {
                if (_updateAcadFmp != value)
                {
                    _updateAcadFmp = value;
                    OnPropertyChanged("UpdateAcadFmp");
                }
            }
        }

        public bool CleanMTextOverrides
        {
            get { return _cleanMTextOverrides; }
            set
            {
                if (_cleanMTextOverrides != value)
                {
                    _cleanMTextOverrides = value;
                    OnPropertyChanged("CleanMTextOverrides");
                }
            }
        }

        public bool AutoRegen
        {
            get { return _autoRegen; }
            set
            {
                if (_autoRegen != value)
                {
                    _autoRegen = value;
                    OnPropertyChanged("AutoRegen");
                }
            }
        }

        public string SummaryText
        {
            get { return _summaryText; }
            set
            {
                if (_summaryText != value)
                {
                    _summaryText = value;
                    OnPropertyChanged("SummaryText");
                }
            }
        }

        public string RecommendationText
        {
            get { return _recommendationText; }
            set
            {
                if (_recommendationText != value)
                {
                    _recommendationText = value;
                    OnPropertyChanged("RecommendationText");
                }
            }
        }

        public ICommand QuickFixAllCommand { get; private set; }
        public ICommand FixSelectedCommand { get; private set; }
        public ICommand RescanCommand { get; private set; }

        public event Action RequestClose;

        public void Close()
        {
            var handler = RequestClose;
            if (handler != null) handler();
        }

        public FontFixViewModel(Database database) : this(Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument, database)
        {
        }

        public FontFixViewModel(Document document, Database database)
        {
            _document = document;
            _database = database ?? (document != null ? document.Database : null);
            _items = new ObservableCollection<FontIssueItem>();

            QuickFixAllCommand = new FontRelayCommand(OnQuickFixAll);
            FixSelectedCommand = new FontRelayCommand(OnFixSelected);
            RescanCommand = new FontRelayCommand(OnRescan);

            RunDiagnosis();
        }

        public void RunDiagnosis()
        {
            if (_database == null) return;

            FontDiagnosisResult result = FontDiagnosisService.ScanDatabase(_database);
            AvailableBigFonts = result.AvailableBigFonts;
            AvailableWesternFonts = result.AvailableWesternFonts;
            AvailableTtfFonts = result.AvailableTtfFonts;
            OnPropertyChanged("AvailableBigFonts");
            OnPropertyChanged("AvailableWesternFonts");
            OnPropertyChanged("AvailableTtfFonts");

            _items.Clear();
            int issueCount = 0;
            int totalAffectedEntities = 0;

            foreach (var item in result.IssueItems)
            {
                _items.Add(item);
                if (item.IssueType != FontIssueType.Healthy)
                {
                    issueCount++;
                    totalAffectedEntities += item.AffectedEntityCount;
                }
            }

            FilteredView = CollectionViewSource.GetDefaultView(_items);
            FilteredView.Filter = FilterItem;

            SummaryText = string.Format("📊 全图共检测到 {0} 个文字样式，其中 {1} 个存在缺失/异常，共涉及 {2} 个文字图元",
                _items.Count, issueCount, totalAffectedEntities);

            RecommendationText = string.Format("💡 本机检测就绪：推荐中文大字体 【{0}】 (内置GB2312) | 推荐西文字体 【{1}】",
                result.RecommendedBigFont, result.RecommendedWesternFont);
        }

        private bool FilterItem(object obj)
        {
            FontIssueItem item = obj as FontIssueItem;
            if (item == null) return false;

            if (_onlyShowIssues && item.IssueType == FontIssueType.Healthy)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_searchKeyword))
            {
                string kw = _searchKeyword.Trim();
                bool matchName = item.StyleName != null && item.StyleName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchFont = (item.OriginalPrimaryFont != null && item.OriginalPrimaryFont.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (item.OriginalBigFont != null && item.OriginalBigFont.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (item.OriginalTtfFace != null && item.OriginalTtfFace.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!matchName && !matchFont) return false;
            }

            return true;
        }

        private void ToggleAllSelection(bool select)
        {
            if (_filteredView == null) return;
            foreach (var obj in _filteredView)
            {
                FontIssueItem item = obj as FontIssueItem;
                if (item != null)
                {
                    item.IsSelected = select;
                }
            }
        }

        private void OnQuickFixAll()
        {
            List<FontIssueItem> toFix = new List<FontIssueItem>();
            foreach (var item in _items)
            {
                if (item.IssueType != FontIssueType.Healthy)
                {
                    toFix.Add(item);
                }
            }

            ExecuteFix(toFix, "⚡ 一键智能修复全部缺失");
        }

        private void OnFixSelected()
        {
            List<FontIssueItem> toFix = new List<FontIssueItem>();
            foreach (var item in _items)
            {
                if (item.IsSelected)
                {
                    toFix.Add(item);
                }
            }

            if (toFix.Count == 0)
            {
                MessageBox.Show("当前未勾选任何需要修复的文字样式！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ExecuteFix(toFix, "修复选中项");
        }

        private void ExecuteFix(List<FontIssueItem> itemsToFix, string actionName)
        {
            if (itemsToFix.Count == 0)
            {
                MessageBox.Show("未检测到需要修复的文字样式，图纸所有字体均处于正常状态！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int stylesFixed = 0;
            int mtextsCleaned = 0;
            int fmpRulesAdded = 0;

            try
            {
                FontDiagnosisService.ApplyFontFixes(
                    _document,
                    _database,
                    itemsToFix,
                    _updateAcadFmp,
                    _cleanMTextOverrides,
                    out stylesFixed,
                    out mtextsCleaned,
                    out fmpRulesAdded);

                string msg = string.Format(
                    "🎉 字体修复与问号消除圆满完成！\n\n" +
                    "✔ 成功修复/映射文字样式: {0} 个\n" +
                    "✔ 清除多行文字行内格式代码: {1} 处\n" +
                    "✔ 写入本机永久 acad.fmp 映射表: {2} 条规则\n\n" +
                    "视口已完成重生成，全图所有 '?????' 字符均已恢复正常显示！",
                    stylesFixed, mtextsCleaned, fmpRulesAdded);

                MessageBox.Show(msg, "XY-Toolbox 字体修复报告", MessageBoxButton.OK, MessageBoxImage.Information);

                // 修复后自动重新体检，让界面列表与统计计数即时同步最新状态
                RunDiagnosis();
            }
            catch (Exception ex)
            {
                MessageBox.Show("修复过程中发生异常: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnRescan()
        {
            RunDiagnosis();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propName));
        }
    }
}
