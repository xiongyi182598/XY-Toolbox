using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Visibility = System.Windows.Visibility;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AttributeWorkbench.Services;

namespace AttributeWorkbench.Views
{
    /// <summary>
    /// 图块属性文字可视化交互修改微窗 (WPF 悬浮微卡片)
    /// </summary>
    public class AttributeModifyDialog
    {
        private static readonly string DialogXaml =
@"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""🔤 图块属性修改 (XY-Toolbox)""
        Width=""480"" Height=""520"" MinWidth=""440"" MinHeight=""480""
        WindowStartupLocation=""CenterScreen""
        Background=""#1E222A"" Foreground=""#ECEFF4""
        FontFamily=""Segoe UI, Microsoft YaHei, 微软雅黑"" FontSize=""12""
        ResizeMode=""NoResize""
        Topmost=""True"">

    <Window.Resources>
        <!-- 按钮样式 -->
        <Style TargetType=""Button"">
            <Setter Property=""Background"" Value=""#2E3440""/>
            <Setter Property=""Foreground"" Value=""#ECEFF4""/>
            <Setter Property=""BorderBrush"" Value=""#4C566A""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Padding"" Value=""10,5""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border Background=""{TemplateBinding Background}""
                                BorderBrush=""{TemplateBinding BorderBrush}""
                                BorderThickness=""{TemplateBinding BorderThickness}""
                                CornerRadius=""4"">
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType=""TextBox"">
            <Setter Property=""Background"" Value=""#16191F""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""BorderBrush"" Value=""#5E6A82""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Padding"" Value=""6,3""/>
            <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
            <Setter Property=""CaretBrush"" Value=""#FFFFFF""/>
        </Style>

        <Style TargetType=""RadioButton"">
            <Setter Property=""Foreground"" Value=""#ECEFF4""/>
            <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
        </Style>
    </Window.Resources>

    <Grid Margin=""16"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""Auto""/> <!-- 0: 顶部信息卡 -->
            <RowDefinition Height=""Auto""/> <!-- 1: 字高修改区 -->
            <RowDefinition Height=""Auto""/> <!-- 2: 位置平移区 -->
            <RowDefinition Height=""Auto""/> <!-- 3: 同步范围 -->
            <RowDefinition Height=""*""/>    <!-- 4: 底部操作 -->
        </Grid.RowDefinitions>

        <!-- 0. 顶部属性信息卡 -->
        <Border Grid.Row=""0"" Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12"" Margin=""0,0,0,12"">
            <StackPanel>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""Auto""/>
                    </Grid.ColumnDefinitions>
                    <TextBlock x:Name=""txtBlockTitle"" Text=""图块: 【GC_DOOR】 | 标签: 【DOOR_NO】"" FontWeight=""Bold"" FontSize=""13"" Foreground=""#88C0D0""/>
                    <Border Grid.Column=""1"" Background=""#434C5E"" CornerRadius=""3"" Padding=""6,1"">
                        <TextBlock Text=""XY-Toolbox 属性修改"" FontSize=""10"" Foreground=""#A3BE8C""/>
                    </Border>
                </Grid>
                <TextBlock x:Name=""txtAttrDetail"" Text=""当前内容: 'M01' | 当前字高: 250.0"" FontSize=""11"" Foreground=""#D8DEE9"" Margin=""0,6,0,0""/>
            </StackPanel>
        </Border>

        <!-- 1. 字高可视化调节卡片 -->
        <Border Grid.Row=""1"" Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12"" Margin=""0,0,0,12"">
            <StackPanel>
                <TextBlock Text=""📏 属性字高调节 (Text Height):"" FontWeight=""Bold"" Foreground=""#ECEFF4"" Margin=""0,0,0,8""/>

                <!-- 常用规格胶囊按钮 -->
                <TextBlock Text=""常用工程规格一键设置:"" FontSize=""11"" Foreground=""#9AA5B8"" Margin=""0,0,0,4""/>
                <UniformGrid Columns=""6"" Margin=""0,0,0,8"">
                    <Button x:Name=""btnH150"" Content=""150"" Margin=""2"" Background=""#3B4252""/>
                    <Button x:Name=""btnH200"" Content=""200"" Margin=""2"" Background=""#3B4252""/>
                    <Button x:Name=""btnH250"" Content=""250"" Margin=""2"" Background=""#3B4252""/>
                    <Button x:Name=""btnH300"" Content=""300"" Margin=""2"" Background=""#3B4252""/>
                    <Button x:Name=""btnH350"" Content=""350"" Margin=""2"" Background=""#3B4252""/>
                    <Button x:Name=""btnH400"" Content=""400"" Margin=""2"" Background=""#3B4252""/>
                </UniformGrid>

                <!-- 精确微调栏 -->
                <Grid Margin=""0,4,0,0"">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""Auto""/>
                        <ColumnDefinition Width=""6""/>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""6""/>
                        <ColumnDefinition Width=""Auto""/>
                        <ColumnDefinition Width=""6""/>
                        <ColumnDefinition Width=""Auto""/>
                        <ColumnDefinition Width=""6""/>
                        <ColumnDefinition Width=""Auto""/>
                    </Grid.ColumnDefinitions>

                    <Button x:Name=""btnMinus25"" Content=""- 25"" Grid.Column=""0"" Padding=""8,3""/>
                    <TextBox x:Name=""txtNewHeight"" Text=""250.0"" Grid.Column=""2"" Height=""28"" FontWeight=""Bold"" FontSize=""13""/>
                    <Button x:Name=""btnPlus25"" Content=""+ 25"" Grid.Column=""4"" Padding=""8,3""/>
                    <Button x:Name=""btnScaleDown"" Content=""0.8x"" Grid.Column=""6"" Padding=""8,3"" ToolTip=""按0.8倍缩小""/>
                    <Button x:Name=""btnScaleUp"" Content=""1.25x"" Grid.Column=""8"" Padding=""8,3"" ToolTip=""按1.25倍放大""/>
                </Grid>

                <TextBlock Text=""💡 提示：字高缩放将自动锁死文字中心，门编号等文字在圆圈/门框内对称缩放不跑偏""
                           FontSize=""10"" Foreground=""#6B7A94"" Margin=""0,8,0,0""/>
            </StackPanel>
        </Border>

        <!-- 2. 位置平移区 -->
        <Border Grid.Row=""2"" Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12"" Margin=""0,0,0,12"">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column=""0"" VerticalAlignment=""Center"">
                    <TextBlock Text=""↔️ 属性文字位置调整:"" FontWeight=""Bold"" Foreground=""#ECEFF4""/>
                    <TextBlock Text=""在 CAD 中动态拖拽橡皮筋实时对齐"" FontSize=""11"" Foreground=""#9AA5B8"" Margin=""0,2,0,0""/>
                </StackPanel>
                <Button x:Name=""btnStartMoveJig"" Grid.Column=""1"" Content=""🎯 拖拽平移位置"" Background=""#5E81AC"" Foreground=""#FFFFFF""
                        FontWeight=""Bold"" Padding=""14,8""/>
            </Grid>
        </Border>

        <!-- 3. 同步范围 -->
        <Border Grid.Row=""3"" Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""10,8"" Margin=""0,0,0,12"">
            <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                <TextBlock Text=""同步范围: "" FontWeight=""Bold"" Foreground=""#88C0D0"" Margin=""0,0,10,0"" VerticalAlignment=""Center""/>
                <RadioButton x:Name=""radSyncAll"" Content=""同步全图所有同名块 (模型+所有布局)"" IsChecked=""True"" Margin=""0,0,14,0""/>
                <RadioButton x:Name=""radSyncSingle"" Content=""仅改当前这 1 处""/>
            </StackPanel>
        </Border>

        <!-- 4. 底部主操作按钮 -->
        <Grid Grid.Row=""4"" VerticalAlignment=""Bottom"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""*""/>
                <ColumnDefinition Width=""8""/>
                <ColumnDefinition Width=""Auto""/>
                <ColumnDefinition Width=""8""/>
                <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>

            <Button x:Name=""btnApplyHeight"" Content=""⚡ 立即应用新字高"" Background=""#88C0D0"" Foreground=""#1E222A""
                    FontWeight=""Bold"" Height=""34"" Grid.Column=""0""/>
            <Button x:Name=""btnPickNext"" Content=""🎯 选下一个属性"" Background=""#3B4252"" Foreground=""#ECEFF4""
                    Height=""34"" Grid.Column=""2"" Padding=""12,0""/>
            <Button x:Name=""btnClose"" Content=""关闭"" Background=""#2E3440"" Foreground=""#D8DEE9""
                    Height=""34"" Grid.Column=""4"" Padding=""16,0""/>
        </Grid>
    </Grid>
</Window>";

        private Window _window;
        private Document _doc;
        private AttributeTargetInfo _target;
        private Action _onPickNextRequested;

        private TextBlock _txtBlockTitle;
        private TextBlock _txtAttrDetail;
        private TextBox _txtNewHeight;

        private Button _btnH150;
        private Button _btnH200;
        private Button _btnH250;
        private Button _btnH300;
        private Button _btnH350;
        private Button _btnH400;

        private Button _btnMinus25;
        private Button _btnPlus25;
        private Button _btnScaleDown;
        private Button _btnScaleUp;

        private Button _btnStartMoveJig;
        private RadioButton _radSyncAll;
        private RadioButton _radSyncSingle;

        private Button _btnApplyHeight;
        private Button _btnPickNext;
        private Button _btnClose;

        public static void ShowDialog(Document doc, AttributeTargetInfo target, Action onPickNext)
        {
            AttributeModifyDialog dlg = new AttributeModifyDialog(doc, target, onPickNext);
            Autodesk.AutoCAD.ApplicationServices.Application.ShowModelessWindow(dlg._window);
        }

        public AttributeModifyDialog(Document doc, AttributeTargetInfo target, Action onPickNext)
        {
            _doc = doc;
            _target = target;
            _onPickNextRequested = onPickNext;

            InitializeComponent();
            UpdateUiFromTarget();
        }

        private void InitializeComponent()
        {
            _window = (Window)XamlReader.Parse(DialogXaml);

            _txtBlockTitle = (TextBlock)_window.FindName("txtBlockTitle");
            _txtAttrDetail = (TextBlock)_window.FindName("txtAttrDetail");
            _txtNewHeight = (TextBox)_window.FindName("txtNewHeight");

            _btnH150 = (Button)_window.FindName("btnH150");
            _btnH200 = (Button)_window.FindName("btnH200");
            _btnH250 = (Button)_window.FindName("btnH250");
            _btnH300 = (Button)_window.FindName("btnH300");
            _btnH350 = (Button)_window.FindName("btnH350");
            _btnH400 = (Button)_window.FindName("btnH400");

            _btnMinus25 = (Button)_window.FindName("btnMinus25");
            _btnPlus25 = (Button)_window.FindName("btnPlus25");
            _btnScaleDown = (Button)_window.FindName("btnScaleDown");
            _btnScaleUp = (Button)_window.FindName("btnScaleUp");

            _btnStartMoveJig = (Button)_window.FindName("btnStartMoveJig");
            _radSyncAll = (RadioButton)_window.FindName("radSyncAll");
            _radSyncSingle = (RadioButton)_window.FindName("radSyncSingle");

            _btnApplyHeight = (Button)_window.FindName("btnApplyHeight");
            _btnPickNext = (Button)_window.FindName("btnPickNext");
            _btnClose = (Button)_window.FindName("btnClose");

            // 快捷预设字高绑定
            _btnH150.Click += delegate { ApplyHeightValue(150.0); };
            _btnH200.Click += delegate { ApplyHeightValue(200.0); };
            _btnH250.Click += delegate { ApplyHeightValue(250.0); };
            _btnH300.Click += delegate { ApplyHeightValue(300.0); };
            _btnH350.Click += delegate { ApplyHeightValue(350.0); };
            _btnH400.Click += delegate { ApplyHeightValue(400.0); };

            // 微调按钮绑定
            _btnMinus25.Click += delegate { AdjustHeightByDelta(-25.0); };
            _btnPlus25.Click += delegate { AdjustHeightByDelta(25.0); };
            _btnScaleDown.Click += delegate { MultiplyHeightByFactor(0.8); };
            _btnScaleUp.Click += delegate { MultiplyHeightByFactor(1.25); };

            // 操作绑定
            _btnApplyHeight.Click += delegate { ApplyInputHeight(); };
            _btnStartMoveJig.Click += BtnStartMoveJig_Click;
            _btnPickNext.Click += BtnPickNext_Click;
            _btnClose.Click += delegate { _window.Close(); };
        }

        public void UpdateTarget(AttributeTargetInfo newTarget)
        {
            _target = newTarget;
            UpdateUiFromTarget();
        }

        private void UpdateUiFromTarget()
        {
            if (_target == null) return;
            _txtBlockTitle.Text = string.Format("图块: 【{0}】 | 标签: 【{1}】", _target.BlockName, _target.Tag);
            _txtAttrDetail.Text = string.Format("当前文字内容: \"{0}\" | 当前基准字高: {1:0.###}", _target.TextString, _target.CurrentHeight);
            _txtNewHeight.Text = _target.CurrentHeight.ToString("0.###");
        }

        private void AdjustHeightByDelta(double delta)
        {
            double cur;
            if (!double.TryParse(_txtNewHeight.Text, out cur)) cur = _target != null ? _target.CurrentHeight : 250.0;
            double n = cur + delta;
            if (n < 1.0) n = 1.0;
            _txtNewHeight.Text = n.ToString("0.###");
            ApplyHeightValue(n);
        }

        private void MultiplyHeightByFactor(double factor)
        {
            double cur;
            if (!double.TryParse(_txtNewHeight.Text, out cur)) cur = _target != null ? _target.CurrentHeight : 250.0;
            double n = Math.Round(cur * factor, 1);
            if (n < 1.0) n = 1.0;
            _txtNewHeight.Text = n.ToString("0.###");
            ApplyHeightValue(n);
        }

        private void ApplyInputHeight()
        {
            double h;
            if (double.TryParse(_txtNewHeight.Text, out h) && h > 0)
            {
                ApplyHeightValue(h);
            }
            else
            {
                MessageBox.Show("请输入有效的正数字高！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplyHeightValue(double newH)
        {
            if (_target == null || _doc == null) return;
            bool syncAll = (_radSyncAll.IsChecked == true);

            try
            {
                int count = 0;
                using (_doc.LockDocument())
                {
                    count = AttributeModifyService.ModifyAttributeHeight(_doc.Database, _target, newH, syncAll);
                }

                _txtNewHeight.Text = newH.ToString("0.###");
                _txtAttrDetail.Text = string.Format("当前文字内容: \"{0}\" | 当前字高: {1:0.###} (已更新)", _target.TextString, newH);
                _doc.Editor.Regen();
                _doc.Editor.WriteMessage(string.Format("\n[XY工具箱] 属性【{0}】字高已成功更新为 {1:0.###}！(同步更新了 {2} 处同名块)\n",
                    _target.Tag, newH, count));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("修改字高失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnStartMoveJig_Click(object sender, RoutedEventArgs e)
        {
            if (_target == null || _doc == null) return;

            _window.Visibility = Visibility.Hidden;
            try
            {
                Database db = _doc.Database;
                Editor ed = _doc.Editor;

                AttributeReference attClone = null;
                using (_doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        AttributeReference att = tr.GetObject(_target.AttributeId, OpenMode.ForRead) as AttributeReference;
                        if (att != null)
                        {
                            attClone = (AttributeReference)att.Clone();
                        }
                        tr.Commit();
                    }
                    if (attClone == null) return;

                    // 启动 DrawJig 拖拽
                    XY_Deepening_Toolbox.Commands.AttributeTextMoveJig jig = new XY_Deepening_Toolbox.Commands.AttributeTextMoveJig(_target.PickedPoint, attClone);
                    PromptResult dragRes = ed.Drag(jig);

                    if (dragRes.Status == PromptStatus.OK)
                    {
                        Vector3d wcsDisp = jig.DisplacementVector;
                        if (!wcsDisp.IsZeroLength())
                        {
                            bool syncAll = (_radSyncAll.IsChecked == true);
                            int moveCount = AttributeModifyService.MoveAttributePosition(db, _target, wcsDisp, syncAll);
                            ed.Regen();
                            ed.WriteMessage(string.Format("\n[XY工具箱] 属性【{0}】位置平移已完成！(同步更新了 {1} 处同名块)\n", _target.Tag, moveCount));
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _doc.Editor.WriteMessage(string.Format("\n[错误] 平移属性位置失败: {0}\n", ex.Message));
            }
            finally
            {
                _window.Visibility = Visibility.Visible;
                _window.Activate();
            }
        }

        private void BtnPickNext_Click(object sender, RoutedEventArgs e)
        {
            _window.Close();
            if (_onPickNextRequested != null)
            {
                _onPickNextRequested();
            }
        }
    }
}
