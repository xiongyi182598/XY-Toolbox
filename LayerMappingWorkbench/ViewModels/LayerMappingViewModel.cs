using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using LayerMappingWorkbench.Models;
using LayerMappingWorkbench.Services;
using XY_Deepening_Toolbox;

namespace LayerMappingWorkbench.ViewModels
{
    public class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute) : this(execute, null) { }

        public RelayCommand(Action execute, Func<bool> canExecute)
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
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }

    /// <summary>
    /// 双栏图层智能对位工作台主 ViewModel (驱动数据绑定、状态过滤、CAD交互拾取与深度合并)
    /// </summary>
    public class LayerMappingViewModel : INotifyPropertyChanged
    {
        private readonly Database _database;
        private ObservableCollection<LayerCompareItem> _mappingList;
        private ICollectionView _filteredView;

        private string _searchKeyword;
        private int _filterStatusIndex; // 0: 全部, 1: 待映射, 2: 智能推测, 3: 精准锁定

        private string _targetDwgPath;
        private bool _forceByLayer;
        private bool _keepZeroLayer;
        private bool _purgeEmptySource;

        private int _totalSourceCount;
        private int _matchedCount;
        private int _autoGuessedCount;
        private int _unmatchedCount;
        private int _totalEntitiesCount;
        private string _statusSummary;

        private List<LayerMetaInfo> _sourceLayersMeta;
        private Dictionary<string, LayerMetaInfo> _targetLayersMeta;
        private List<string> _availableTargetLayerNames;
        private readonly Dictionary<string, LayerMapRule> _masterRuleCache = new Dictionary<string, LayerMapRule>(StringComparer.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler PropertyChanged;
        public Action RequestCloseWindow;

        public LayerMappingViewModel(Database db)
        {
            _database = db ?? HostApplicationServices.WorkingDatabase;
            _mappingList = new ObservableCollection<LayerCompareItem>();
            _searchKeyword = string.Empty;
            _filterStatusIndex = 0;
            _targetDwgPath = string.Empty;
            _forceByLayer = true;
            _keepZeroLayer = true;
            _purgeEmptySource = true;
            _statusSummary = "就绪";

            InitCommands();
            LoadCurrentDrawingLayers();
        }

        #region 属性绑定

        public ObservableCollection<LayerCompareItem> MappingList
        {
            get { return _mappingList; }
            set
            {
                if (_mappingList != value)
                {
                    _mappingList = value;
                    OnPropertyChanged("MappingList");
                }
            }
        }

        public ICollectionView FilteredView
        {
            get { return _filteredView; }
            set
            {
                if (_filteredView != value)
                {
                    _filteredView = value;
                    OnPropertyChanged("FilteredView");
                }
            }
        }

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

        public int FilterStatusIndex
        {
            get { return _filterStatusIndex; }
            set
            {
                if (_filterStatusIndex != value)
                {
                    _filterStatusIndex = value;
                    OnPropertyChanged("FilterStatusIndex");
                    if (_filteredView != null) _filteredView.Refresh();
                }
            }
        }

        public string TargetDwgPath
        {
            get { return _targetDwgPath; }
            set
            {
                if (_targetDwgPath != value)
                {
                    _targetDwgPath = value;
                    OnPropertyChanged("TargetDwgPath");
                }
            }
        }

        public bool ForceByLayer
        {
            get { return _forceByLayer; }
            set
            {
                if (_forceByLayer != value)
                {
                    _forceByLayer = value;
                    OnPropertyChanged("ForceByLayer");
                }
            }
        }

        public bool KeepZeroLayer
        {
            get { return _keepZeroLayer; }
            set
            {
                if (_keepZeroLayer != value)
                {
                    _keepZeroLayer = value;
                    OnPropertyChanged("KeepZeroLayer");
                }
            }
        }

        public bool PurgeEmptySource
        {
            get { return _purgeEmptySource; }
            set
            {
                if (_purgeEmptySource != value)
                {
                    _purgeEmptySource = value;
                    OnPropertyChanged("PurgeEmptySource");
                }
            }
        }

        public int TotalSourceCount
        {
            get { return _totalSourceCount; }
            set { _totalSourceCount = value; OnPropertyChanged("TotalSourceCount"); }
        }

        public int MatchedCount
        {
            get { return _matchedCount; }
            set { _matchedCount = value; OnPropertyChanged("MatchedCount"); }
        }

        public int AutoGuessedCount
        {
            get { return _autoGuessedCount; }
            set { _autoGuessedCount = value; OnPropertyChanged("AutoGuessedCount"); }
        }

        public int UnmatchedCount
        {
            get { return _unmatchedCount; }
            set { _unmatchedCount = value; OnPropertyChanged("UnmatchedCount"); }
        }

        public int TotalEntitiesCount
        {
            get { return _totalEntitiesCount; }
            set { _totalEntitiesCount = value; OnPropertyChanged("TotalEntitiesCount"); }
        }

        public string StatusSummary
        {
            get { return _statusSummary; }
            set { _statusSummary = value; OnPropertyChanged("StatusSummary"); }
        }

        #endregion

        #region 命令

        public RelayCommand PickEntityCommand { get; private set; }
        public RelayCommand LoadTargetDwgCommand { get; private set; }
        public RelayCommand ImportCsvCommand { get; private set; }
        public RelayCommand ExportCsvCommand { get; private set; }
        public RelayCommand ExecuteMergeCommand { get; private set; }
        public RelayCommand RefreshCommand { get; private set; }

        private void InitCommands()
        {
            PickEntityCommand = new RelayCommand(OnPickEntity);
            LoadTargetDwgCommand = new RelayCommand(OnLoadTargetDwg);
            ImportCsvCommand = new RelayCommand(OnImportCsv);
            ExportCsvCommand = new RelayCommand(OnExportCsv);
            ExecuteMergeCommand = new RelayCommand(OnExecuteMerge);
            RefreshCommand = new RelayCommand(LoadCurrentDrawingLayers);
        }

        #endregion

        public void LoadCurrentDrawingLayers()
        {
            if (_database == null) return;

            // 1. 扫描源图层
            _sourceLayersMeta = LayerAlignmentEngine.ScanDatabaseLayers(_database);

            // 2. 目标层集：若未加载外部DWG（_targetDwgPath为空），始终用最新扫描结果刷新；
            // 若已加载外部DWG，保留外部元数据不被覆盖
            if (string.IsNullOrEmpty(_targetDwgPath))
            {
                _targetLayersMeta = new Dictionary<string, LayerMetaInfo>(StringComparer.OrdinalIgnoreCase);
                _availableTargetLayerNames = new List<string>();

                foreach (LayerMetaInfo meta in _sourceLayersMeta)
                {
                    _targetLayersMeta[meta.Name] = meta;
                    _availableTargetLayerNames.Add(meta.Name);
                }
            }

            // 注册全局图层视觉属性联动提供者，用户手动切换图层时瞬时联动右侧色块与线型！
            LayerCompareItem.TargetMetaProvider = delegate(string targetName)
            {
                if (string.IsNullOrEmpty(targetName)) return null;
                LayerMetaInfo info;
                if (_targetLayersMeta != null && _targetLayersMeta.TryGetValue(targetName, out info))
                {
                    return new TargetVisualMeta(info.Color, info.Linetype);
                }
                return null;
            };

            // 3. 读取默认规则库并建立主规则库缓存
            string csvPath = LayerAlignmentEngine.GetDefaultRuleFilePath(_database.Filename);
            List<LayerMapRule> persistedRules = LayerAlignmentEngine.LoadRulesFromCsv(csvPath);
            foreach (LayerMapRule r in persistedRules)
            {
                if (!string.IsNullOrEmpty(r.SourceLayer) && !string.IsNullOrEmpty(r.TargetLayer))
                {
                    _masterRuleCache[r.SourceLayer] = r;
                }
            }

            // 叠加本次 CAD 会话工作流中累加记录的所有规则 (包含中途通过 HB1 / HB2 合并的所有项)
            Dictionary<string, LayerMapRule> sessionRules = LayerAlignmentEngine.GetSessionRules();
            foreach (KeyValuePair<string, LayerMapRule> kv in sessionRules)
            {
                _masterRuleCache[kv.Key] = kv.Value;
            }

            // 4. 执行对齐流水线 (传入全量主规则缓存，确保中途 HB1/HB2 规则 100% 优先精准对齐)
            _mappingList = LayerAlignmentEngine.AlignLayers(_sourceLayersMeta, _availableTargetLayerNames, _targetLayersMeta, new List<LayerMapRule>(_masterRuleCache.Values));
            OnPropertyChanged("MappingList");

            // 5. 设置过滤视图
            _filteredView = CollectionViewSource.GetDefaultView(_mappingList);
            _filteredView.Filter = FilterItemPredicate;
            OnPropertyChanged("FilteredView");

            UpdateStatistics();
        }

        private bool FilterItemPredicate(object obj)
        {
            LayerCompareItem item = obj as LayerCompareItem;
            if (item == null) return false;

            // 状态过滤
            if (_filterStatusIndex == 1 && item.Status != MatchStatus.Unmatched) return false;
            if (_filterStatusIndex == 2 && item.Status != MatchStatus.AutoGuessed) return false;
            if (_filterStatusIndex == 3 && item.Status != MatchStatus.Matched) return false;

            // 关键字搜索过滤
            if (!string.IsNullOrEmpty(_searchKeyword))
            {
                string kw = _searchKeyword.Trim();
                bool hitSource = item.SourceLayerName != null && item.SourceLayerName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;
                bool hitTarget = item.SelectedTargetLayerName != null && item.SelectedTargetLayerName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hitSource && !hitTarget) return false;
            }

            return true;
        }

        public void UpdateStatistics()
        {
            int total = 0;
            int matched = 0;
            int guessed = 0;
            int unmatched = 0;
            int totalEnts = 0;

            if (_mappingList != null)
            {
                foreach (LayerCompareItem itm in _mappingList)
                {
                    total++;
                    totalEnts += itm.EntityCount;
                    if (itm.Status == MatchStatus.Matched) matched++;
                    else if (itm.Status == MatchStatus.AutoGuessed) guessed++;
                    else unmatched++;
                }
            }

            TotalSourceCount = total;
            MatchedCount = matched;
            AutoGuessedCount = guessed;
            UnmatchedCount = unmatched;
            TotalEntitiesCount = totalEnts;

            StatusSummary = string.Format("源图层: {0} 个 | 精准对齐: {1} | 智能推测: {2} | 待映射: {3} | 覆盖图元: {4} 个",
                total, matched, guessed, unmatched, totalEnts);
        }

        private void OnPickEntity()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            PromptEntityOptions opt = new PromptEntityOptions("\n请在 CAD 图面中点击要对位的图元: ");
            PromptEntityResult res = ed.GetEntity(opt);

            if (res.Status == PromptStatus.OK)
            {
                string pickedLayer = string.Empty;
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    Entity ent = tr.GetObject(res.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        pickedLayer = ent.Layer;
                    }
                    tr.Commit();
                }

                if (!string.IsNullOrEmpty(pickedLayer))
                {
                    SearchKeyword = pickedLayer;
                    ed.WriteMessage("\n★ 已定位到拾取图元所在图层: " + pickedLayer);
                }
            }
        }

        private void OnLoadTargetDwg()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "选择公司二目标标准图纸 (DWG)";
            dlg.Filter = "AutoCAD 图纸 (*.dwg)|*.dwg";

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                TargetDwgPath = dlg.FileName;
                try
                {
                    using (Database tgtDb = new Database(false, true))
                    {
                        tgtDb.ReadDwgFile(dlg.FileName, FileOpenMode.OpenForReadAndAllShare, true, "");
                        List<LayerMetaInfo> tgtLayers = LayerAlignmentEngine.ScanDatabaseLayers(tgtDb);

                        _targetLayersMeta = new Dictionary<string, LayerMetaInfo>(StringComparer.OrdinalIgnoreCase);
                        _availableTargetLayerNames = new List<string>();

                        foreach (LayerMetaInfo t in tgtLayers)
                        {
                            _targetLayersMeta[t.Name] = t;
                            _availableTargetLayerNames.Add(t.Name);
                        }

                        // 重新运行对齐
                        string csvPath = LayerAlignmentEngine.GetDefaultRuleFilePath(_database.Filename);
                        List<LayerMapRule> persistedRules = LayerAlignmentEngine.LoadRulesFromCsv(csvPath);

                        _mappingList = LayerAlignmentEngine.AlignLayers(_sourceLayersMeta, _availableTargetLayerNames, _targetLayersMeta, persistedRules);
                        OnPropertyChanged("MappingList");

                        _filteredView = CollectionViewSource.GetDefaultView(_mappingList);
                        _filteredView.Filter = FilterItemPredicate;
                        OnPropertyChanged("FilteredView");

                        UpdateStatistics();
                        StatusSummary = "已成功加载对标图纸: " + Path.GetFileName(dlg.FileName);
                    }
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show("读取目标 DWG 图纸失败: " + ex.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void OnImportCsv()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "导入图层映射规则 (CSV)";
            dlg.Filter = "CSV 映射表 (*.csv)|*.csv|所有文件 (*.*)|*.*";

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                List<LayerMapRule> imported = LayerAlignmentEngine.LoadRulesFromCsv(dlg.FileName);
                if (imported == null || imported.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "未能在所选文件中识别出有效的图层映射规则！\n\n请确保文件为逗号分隔的 CSV 格式，且包含源图层与目标图层列。",
                        "导入提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 1. 提取导入规则中的所有目标图层，动态注入至当前可用目标图层库 (即便未加载外部DWG也能立即使用)
                if (_availableTargetLayerNames == null)
                {
                    _availableTargetLayerNames = new List<string>();
                }
                foreach (LayerMapRule r in imported)
                {
                    if (!string.IsNullOrEmpty(r.TargetLayer) && !_availableTargetLayerNames.Contains(r.TargetLayer))
                    {
                        _availableTargetLayerNames.Add(r.TargetLayer);
                    }
                }

                // 同步扩充界面每一行的下拉选项列表
                if (_mappingList != null)
                {
                    foreach (LayerCompareItem itm in _mappingList)
                    {
                        if (itm.AvailableTargetLayers != null)
                        {
                            foreach (string tgt in _availableTargetLayerNames)
                            {
                                if (!itm.AvailableTargetLayers.Contains(tgt))
                                {
                                    itm.AvailableTargetLayers.Add(tgt);
                                }
                            }
                        }
                    }
                }

                // 2. 将导入规则更新至全量主规则库缓存
                Dictionary<string, LayerMapRule> importedDict = new Dictionary<string, LayerMapRule>(StringComparer.OrdinalIgnoreCase);
                foreach (LayerMapRule r in imported)
                {
                    if (!string.IsNullOrEmpty(r.SourceLayer) && !string.IsNullOrEmpty(r.TargetLayer))
                    {
                        importedDict[r.SourceLayer] = r;
                        _masterRuleCache[r.SourceLayer] = r;
                    }
                }

                // 3. 智能增量更新 (Smart Overlay)：精准匹配当前图纸中存在的源图层，保留其余行用户的手动配置
                int updatedRows = 0;
                if (_mappingList != null)
                {
                    foreach (LayerCompareItem itm in _mappingList)
                    {
                        LayerMapRule hit;
                        if (importedDict.TryGetValue(itm.SourceLayerName, out hit))
                        {
                            itm.SelectedTargetLayerName = hit.TargetLayer;
                            itm.Status = MatchStatus.Matched;
                            itm.MatchConfidence = 100;
                            itm.MatchNote = "规则库命中 (" + Path.GetFileName(dlg.FileName) + ")";

                            // 同步目标图层视觉属性 (颜色/线型)
                            if (_targetLayersMeta != null && _targetLayersMeta.ContainsKey(hit.TargetLayer))
                            {
                                LayerMetaInfo meta = _targetLayersMeta[hit.TargetLayer];
                                itm.TargetColor = meta.Color;
                                itm.TargetLinetype = meta.Linetype;
                            }
                            updatedRows++;
                        }
                    }
                }

                if (_filteredView != null) _filteredView.Refresh();
                UpdateStatistics();
                StatusSummary = string.Format("已导入 {0} 条规则，成功对齐当前图纸 {1} 个图层", imported.Count, updatedRows);

                System.Windows.MessageBox.Show(
                    string.Format("映射规则导入成功！\n\n" +
                                  "• 导入有效规则: {0} 条\n" +
                                  "• 成功对齐图层: {1} 个\n" +
                                  "• 目标标准库已同步扩充: {2} 个候选图层\n\n" +
                                  "提示: 外部标准图层已自动填入下拉列表，可随时在表格中切换或微调！",
                                  imported.Count, updatedRows, _availableTargetLayerNames.Count),
                    "导入成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        private void OnExportCsv()
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Title = "导出图层映射规则字典 (CSV)";
            dlg.Filter = "CSV 映射表 (*.csv)|*.csv";
            dlg.FileName = "layer_mapping_rules.csv";

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                // 将当前界面中配置好的所有有效映射沉淀合并至主规则库
                if (_mappingList != null)
                {
                    foreach (LayerCompareItem itm in _mappingList)
                    {
                        if (!string.IsNullOrEmpty(itm.SelectedTargetLayerName))
                        {
                            _masterRuleCache[itm.SourceLayerName] = new LayerMapRule(itm.SourceLayerName, itm.SelectedTargetLayerName, ForceByLayer);
                        }
                    }
                }

                List<LayerMapRule> rulesToExport = new List<LayerMapRule>(_masterRuleCache.Values);

                if (LayerAlignmentEngine.SaveRulesToCsv(dlg.FileName, rulesToExport))
                {
                    StatusSummary = string.Format("规则已成功导出至: {0}", Path.GetFileName(dlg.FileName));
                    System.Windows.MessageBox.Show(
                        string.Format("规则库导出成功！\n\n" +
                                      "• 保存路径: {0}\n" +
                                      "• 规则总数: {1} 条 (包含历史沉淀与当前图纸配置)\n" +
                                      "• 文件格式: UTF-8 带BOM (中文标准格式)\n\n" +
                                      "支持在 Microsoft Excel 或 WPS 中直接双击打开编辑维护，绝不乱码！",
                                      dlg.FileName, rulesToExport.Count),
                        "导出成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
        }

        private void OnExecuteMerge()
        {
            List<LayerMapRule> activeRules = new List<LayerMapRule>();
            foreach (LayerCompareItem item in _mappingList)
            {
                if (item.IsSelected && !string.IsNullOrEmpty(item.SelectedTargetLayerName))
                {
                    LayerMapRule r = new LayerMapRule(item.SourceLayerName, item.SelectedTargetLayerName, ForceByLayer);
                    activeRules.Add(r);
                    _masterRuleCache[item.SourceLayerName] = r;
                }
            }

            if (activeRules.Count == 0)
            {
                System.Windows.MessageBox.Show("请先指定至少一组源图层与目标图层的对应关系！", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 1. 保存到默认持久化规则库 (全量沉淀历史与当前规则)
            string defaultCsv = LayerAlignmentEngine.GetDefaultRuleFilePath(_database.Filename);
            LayerAlignmentEngine.SaveRulesToCsv(defaultCsv, _masterRuleCache.Values);

            // 2. 同步更新给极速翻图引擎 (XFT / QUICKFLIP)，实现多张图纸间一键秒级复用！
            try
            {
                FlipRulePreset preset = new FlipRulePreset();
                preset.PresetName = "工作台记忆方案_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
                preset.RefDwgPath = _targetDwgPath;
                preset.ResetEntitiesByLayer = ForceByLayer;
                preset.PurgeSourceLayersAfterMerge = PurgeEmptySource;
                foreach (LayerMapRule r in _masterRuleCache.Values)
                {
                    preset.LayerMappings[r.SourceLayer] = r.TargetLayer;
                }
                FlipRuleStorage.SaveLastPreset(preset);
            }
            catch { }

            // 3. 执行合并服务 (传入目标图层真实属性定义，彻底杜绝合并后全部变白)
            LayerMergeService mergeService = new LayerMergeService();
            MergeExecutionResult res = mergeService.ExecuteMerge(_database, activeRules, _targetLayersMeta, ForceByLayer, PurgeEmptySource);

            if (res.Success)
            {
                string msg = string.Format(
                    "图层深度穿透合并已顺利完成！\n\n" +
                    "• 成功映射图层: {0} 个\n" +
                    "• 穿透修改图元: {1} 个\n" +
                    "• 遍历审查图块: {2} 个\n" +
                    "• 属性文字同步: {3} 个\n" +
                    "• 视口覆盖同步: {4} 个\n" +
                    "• 清理空源图层: {5} 个\n" +
                    "• 执行耗时: {6} 毫秒\n\n" +
                    "（支持在 CAD 命令行敲 U 或按 Ctrl+Z 瞬间全量撤销）",
                    res.TotalLayersMapped, res.TotalEntitiesModified, res.BlocksInspected,
                    res.AttributesUpdated, res.ViewportsSynced, res.LayersPurged, res.ElapsedMilliseconds);

                System.Windows.MessageBox.Show(msg, "合并成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                // 重新加载图纸状态
                LoadCurrentDrawingLayers();
            }
            else
            {
                System.Windows.MessageBox.Show("合并失败: " + res.ErrorMessage, "执行失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

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
