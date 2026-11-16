using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Visibility = System.Windows.Visibility;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AttributeWorkbench.Services;

namespace AttributeWorkbench.Views
{
    public class AttributeModifyDialog
    {
        private static readonly string DialogXaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""图块属性修改 (XY-Toolbox)""
        Width=""480"" SizeToContent=""Height""
        WindowStartupLocation=""CenterScreen""
        Background=""#181B24"" Foreground=""#ECEFF4""
        FontFamily=""Segoe UI, Microsoft YaHei, 微软雅黑"" FontSize=""12""
        ResizeMode=""NoResize""
        Topmost=""True"">
    <Window.Resources>
        <Style x:Key=""BtnBase"" TargetType=""Button"">
            <Setter Property=""Background"" Value=""#2A303C""/>
            <Setter Property=""Foreground"" Value=""#ECEFF4""/>
            <Setter Property=""BorderBrush"" Value=""#434C5E""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""Button"">
                        <Border Background=""{TemplateBinding Background}""
                                BorderBrush=""{TemplateBinding BorderBrush}""
                                BorderThickness=""{TemplateBinding BorderThickness}""
                                CornerRadius=""5"">
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key=""BtnPrimaryLarge"" TargetType=""Button"" BasedOn=""{StaticResource BtnBase}"">
            <Setter Property=""Background"" Value=""#2563EB""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""BorderBrush"" Value=""#3B82F6""/>
            <Setter Property=""FontWeight"" Value=""Bold""/>
        </Style>
        <Style x:Key=""BtnSuccess"" TargetType=""Button"" BasedOn=""{StaticResource BtnBase}"">
            <Setter Property=""Background"" Value=""#2E7D4E""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""BorderBrush"" Value=""#43A047""/>
            <Setter Property=""FontWeight"" Value=""Bold""/>
        </Style>
        <Style x:Key=""BtnScheme"" TargetType=""Button"" BasedOn=""{StaticResource BtnBase}"">
            <Setter Property=""Background"" Value=""#275638""/>
            <Setter Property=""Foreground"" Value=""#D8F3DC""/>
            <Setter Property=""BorderBrush"" Value=""#40916C""/>
            <Setter Property=""FontWeight"" Value=""Bold""/>
        </Style>
        <Style TargetType=""TextBox"">
            <Setter Property=""Background"" Value=""#10131A""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""BorderBrush"" Value=""#4C566A""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Padding"" Value=""6,2""/>
            <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
            <Setter Property=""CaretBrush"" Value=""#FFFFFF""/>
        </Style>
        <Style TargetType=""RadioButton"">
            <Setter Property=""Foreground"" Value=""#D8DEE9""/>
            <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
        </Style>
        <Style TargetType=""CheckBox"">
            <Setter Property=""Foreground"" Value=""#9AA5B8""/>
            <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
        </Style>
        <!-- 纯深色可编辑 ComboBox 模板 (彻底消灭系统 Aero2 默认白色 ToggleButton 白条) -->
        <ControlTemplate x:Key=""DarkEditableComboBoxTemplate"" TargetType=""{x:Type ComboBox}"">
            <Grid x:Name=""templateRoot"" SnapsToDevicePixels=""True"" TextElement.Foreground=""#FFFFFF"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition MinWidth=""24"" Width=""24""/>
                </Grid.ColumnDefinitions>

                <Border x:Name=""mainBorder"" Grid.ColumnSpan=""2""
                        Background=""#10131A""
                        BorderBrush=""#4C566A""
                        BorderThickness=""1""
                        CornerRadius=""4""/>

                <TextBox x:Name=""PART_EditableTextBox"" Grid.Column=""0""
                         Background=""Transparent""
                         Foreground=""#FFFFFF""
                         FontWeight=""Bold""
                         FontSize=""12""
                         BorderThickness=""0""
                         Padding=""8,4""
                         VerticalContentAlignment=""Center""
                         CaretBrush=""#FFFFFF""
                         SelectionBrush=""#88C0D0""/>

                <ToggleButton Grid.Column=""1"" Background=""Transparent"" BorderThickness=""0"" Focusable=""False""
                              IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"">
                    <Path Data=""M 0 0 L 4 4 L 8 0 Z"" Fill=""#88C0D0"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                </ToggleButton>

                <Popup x:Name=""PART_Popup"" AllowsTransparency=""True"" Grid.ColumnSpan=""2""
                       IsOpen=""{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}""
                       Placement=""Bottom"" PopupAnimation=""Slide"">
                    <Border Background=""#202534"" BorderBrush=""#4C566A"" BorderThickness=""1"" CornerRadius=""4"" MaxHeight=""260"" MinWidth=""{Binding ActualWidth, ElementName=templateRoot}"">
                        <ScrollViewer SnapsToDevicePixels=""True"" KeyboardNavigation.DirectionalNavigation=""Contained"">
                            <StackPanel IsItemsHost=""True"" KeyboardNavigation.DirectionalNavigation=""Contained""/>
                        </ScrollViewer>
                    </Border>
                </Popup>
            </Grid>

            <ControlTemplate.Triggers>
                <Trigger Property=""IsMouseOver"" Value=""True"">
                    <Setter TargetName=""mainBorder"" Property=""BorderBrush"" Value=""#88C0D0""/>
                </Trigger>
                <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
                    <Setter TargetName=""mainBorder"" Property=""BorderBrush"" Value=""#81A1C1""/>
                    <Setter TargetName=""mainBorder"" Property=""BorderThickness"" Value=""1.5""/>
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>

        <!-- 纯深色普通只读 ComboBox 模板 -->
        <ControlTemplate x:Key=""DarkComboBoxTemplate"" TargetType=""{x:Type ComboBox}"">
            <Grid x:Name=""templateRoot"" SnapsToDevicePixels=""True"" TextElement.Foreground=""#FFFFFF"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition MinWidth=""24"" Width=""24""/>
                </Grid.ColumnDefinitions>

                <Border x:Name=""mainBorder"" Grid.ColumnSpan=""2""
                        Background=""#10131A""
                        BorderBrush=""#4C566A""
                        BorderThickness=""1""
                        CornerRadius=""4""/>

                <ContentPresenter Grid.Column=""0""
                                  Content=""{TemplateBinding SelectionBoxItem}""
                                  ContentTemplate=""{TemplateBinding SelectionBoxItemTemplate}""
                                  ContentTemplateSelector=""{TemplateBinding ItemTemplateSelector}""
                                  VerticalAlignment=""Center""
                                  Margin=""8,0,0,0""/>

                <ToggleButton Grid.Column=""1"" Background=""Transparent"" BorderThickness=""0"" Focusable=""False""
                              IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"">
                    <Path Data=""M 0 0 L 4 4 L 8 0 Z"" Fill=""#88C0D0"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                </ToggleButton>

                <Popup x:Name=""PART_Popup"" AllowsTransparency=""True"" Grid.ColumnSpan=""2""
                       IsOpen=""{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}""
                       Placement=""Bottom"" PopupAnimation=""Slide"">
                    <Border Background=""#202534"" BorderBrush=""#4C566A"" BorderThickness=""1"" CornerRadius=""4"" MaxHeight=""240"" MinWidth=""{Binding ActualWidth, ElementName=templateRoot}"">
                        <ScrollViewer SnapsToDevicePixels=""True"" KeyboardNavigation.DirectionalNavigation=""Contained"">
                            <StackPanel IsItemsHost=""True"" KeyboardNavigation.DirectionalNavigation=""Contained""/>
                        </ScrollViewer>
                    </Border>
                </Popup>
            </Grid>

            <ControlTemplate.Triggers>
                <Trigger Property=""IsMouseOver"" Value=""True"">
                    <Setter TargetName=""mainBorder"" Property=""BorderBrush"" Value=""#88C0D0""/>
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>

        <!-- 全局下拉项样式 -->
        <Style TargetType=""ComboBoxItem"">
            <Setter Property=""Background"" Value=""#202534""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""Padding"" Value=""8,6""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
            <Style.Triggers>
                <Trigger Property=""IsHighlighted"" Value=""True"">
                    <Setter Property=""Background"" Value=""#2D3748""/>
                    <Setter Property=""Foreground"" Value=""#88C0D0""/>
                </Trigger>
                <Trigger Property=""IsSelected"" Value=""True"">
                    <Setter Property=""Background"" Value=""#3B4252""/>
                    <Setter Property=""Foreground"" Value=""#A3BE8C""/>
                </Trigger>
            </Style.Triggers>
        </Style>

        <Style TargetType=""ComboBox"">
            <Setter Property=""Background"" Value=""#10131A""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""BorderBrush"" Value=""#4C566A""/>
            <Setter Property=""Padding"" Value=""6,4""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
            <Setter Property=""KeyboardNavigation.DirectionalNavigation"" Value=""Contained""/>
            <Setter Property=""Template"" Value=""{StaticResource DarkComboBoxTemplate}""/>
            <Style.Triggers>
                <Trigger Property=""IsEditable"" Value=""True"">
                    <Setter Property=""Template"" Value=""{StaticResource DarkEditableComboBoxTemplate}""/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Window.Resources>
    <StackPanel Margin=""14"">
        <!-- 0. 顶部属性信息卡 -->
        <Border Background=""#202534"" BorderBrush=""#323B4E"" BorderThickness=""1"" CornerRadius=""6""
                Padding=""12,10"" Margin=""0,0,0,8"">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>
                <StackPanel>
                    <TextBlock x:Name=""txtBlockTitle"" Text=""图块: 【GC_DOOR】 | 标签: 【DOOR_NO】""
                               FontWeight=""Bold"" FontSize=""13"" Foreground=""#88C0D0""/>
                    <TextBlock x:Name=""txtAttrDetail"" Text=""当前内容: 'M01'""
                               FontSize=""11"" Foreground=""#94A3B8"" Margin=""0,4,0,0""/>
                </StackPanel>
                <Border Grid.Column=""1"" VerticalAlignment=""Top"" CornerRadius=""4"" Padding=""7,3""
                        Background=""#283548"" BorderBrush=""#3D516B"" BorderThickness=""1"">
                    <TextBlock x:Name=""txtPartCountBadge"" Text=""单段属性"" FontSize=""10"" Foreground=""#88C0D0""/>
                </Border>
            </Grid>
        </Border>

        <!-- 1. 字高调节 (纯手动输入 + 显示当前字高 + 大按钮) -->
        <Border Background=""#202534"" BorderBrush=""#323B4E"" BorderThickness=""1"" CornerRadius=""6""
                Padding=""12,10"" Margin=""0,0,0,8"">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""Auto""/>
                    <ColumnDefinition Width=""Auto""/>
                    <ColumnDefinition Width=""14""/>
                    <ColumnDefinition Width=""Auto""/>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""10""/>
                    <ColumnDefinition Width=""110""/>
                </Grid.ColumnDefinitions>
                <TextBlock Text=""当前字高:"" Foreground=""#8F9CB0"" FontSize=""12"" VerticalAlignment=""Center"" Grid.Column=""0""/>
                <Border Grid.Column=""1"" Background=""#141822"" BorderBrush=""#2D3748"" BorderThickness=""1"" CornerRadius=""4"" Padding=""8,4"" Margin=""6,0,0,0"">
                    <TextBlock x:Name=""txtCurHeightVal"" Text=""250.0"" FontWeight=""Bold"" Foreground=""#88C0D0"" FontSize=""13"" VerticalAlignment=""Center""/>
                </Border>

                <TextBlock Text=""目标字高:"" Foreground=""#ECEFF4"" FontWeight=""SemiBold"" FontSize=""12"" VerticalAlignment=""Center"" Grid.Column=""3""/>
                <TextBox x:Name=""txtNewHeight"" Text=""250.0"" Grid.Column=""4"" Height=""36""
                         FontWeight=""Bold"" FontSize=""14"" HorizontalContentAlignment=""Center"" VerticalContentAlignment=""Center""/>

                <Button x:Name=""btnApplyHeight"" Content=""⚡ 应用字高"" Grid.Column=""6"" Height=""36""
                        Style=""{StaticResource BtnSuccess}"" FontSize=""13""/>
            </Grid>
        </Border>

        <!-- 1.1 编号图层与颜色调节 -->
        <Border Background=""#202534"" BorderBrush=""#323B4E"" BorderThickness=""1"" CornerRadius=""6""
                Padding=""12,10"" Margin=""0,0,0,8"">
            <StackPanel>
                <Grid Margin=""0,0,0,8"">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""Auto""/>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""34""/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text=""编号图层:"" Foreground=""#ECEFF4"" FontWeight=""SemiBold"" FontSize=""12"" VerticalAlignment=""Center"" Grid.Column=""0"" Margin=""0,0,8,0""/>
                    <ComboBox x:Name=""cboLayer"" Grid.Column=""1"" Height=""30"" IsEditable=""True"" FontSize=""12"" VerticalContentAlignment=""Center""/>
                    <Button x:Name=""btnPickLayer"" Grid.Column=""2"" Content=""⌖"" ToolTip=""在图纸中吸取目标图层"" Height=""30"" Margin=""4,0,0,0"" Style=""{StaticResource BtnBase}"" FontSize=""14"" FontWeight=""Bold""/>
                </Grid>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""Auto""/>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""10""/>
                        <ColumnDefinition Width=""110""/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text=""编号颜色:"" Foreground=""#ECEFF4"" FontWeight=""SemiBold"" FontSize=""12"" VerticalAlignment=""Center"" Grid.Column=""0"" Margin=""0,0,8,0""/>
                    <ComboBox x:Name=""cboColor"" Grid.Column=""1"" Height=""32"" FontSize=""12"" VerticalContentAlignment=""Center""/>
                    <Button x:Name=""btnApplyLayerColor"" Content=""⚡ 应用层与色"" Grid.Column=""3"" Height=""32"" Style=""{StaticResource BtnSuccess}"" FontSize=""12""/>
                </Grid>
            </StackPanel>
        </Border>

        <!-- 2. 门窗复合编号排版 (仅在检测到物理相邻的复合多段时激活) -->
        <Border x:Name=""borderMultiPart"" Background=""#1B241E"" BorderBrush=""#304A38"" BorderThickness=""1""
                CornerRadius=""6"" Padding=""12,10"" Margin=""0,0,0,8"" Visibility=""Collapsed"">
            <StackPanel>
                <Grid Margin=""0,0,0,8"">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""Auto""/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text=""多段复合文字智能排版 (门窗编号/多段属性)"" FontWeight=""SemiBold"" Foreground=""#81C784"" FontSize=""12""/>
                    <TextBlock x:Name=""txtMultiPartPreview"" Grid.Column=""1"" FontSize=""10"" Foreground=""#88C0D0"" VerticalAlignment=""Center""/>
                </Grid>
                <RadioButton x:Name=""radScheme1"" Margin=""0,0,0,4"" IsChecked=""True"">
                    <StackPanel Orientation=""Horizontal"">
                        <TextBlock Text=""方案 1: 合并为单段完整编号"" FontWeight=""SemiBold"" Foreground=""#ECEFF4""/>
                        <TextBlock Text="" (推荐，彻底杜绝分散重叠)"" Foreground=""#7E8E9F"" FontSize=""10"" Margin=""6,0,0,0""/>
                    </StackPanel>
                </RadioButton>
                <RadioButton x:Name=""radScheme2"" Margin=""0,0,0,6"">
                    <StackPanel Orientation=""Horizontal"">
                        <TextBlock Text=""方案 2: 保持分段智能贴合重排"" FontWeight=""SemiBold"" Foreground=""#ECEFF4""/>
                        <TextBlock Text="" (紧凑贴合间距)"" Foreground=""#7E8E9F"" FontSize=""10"" Margin=""6,0,0,0""/>
                    </StackPanel>
                </RadioButton>
                <CheckBox x:Name=""chkAutoApplyOnHeightChange"" IsChecked=""True"" Margin=""0,0,0,8""
                          Content=""调节字高时自动排版"" Foreground=""#81C784"" FontSize=""11""/>
                <Button x:Name=""btnApplyScheme"" Height=""38""
                        Content=""⚡ 立即执行多段文字排版 / 合并"" Style=""{StaticResource BtnScheme}""
                        FontSize=""13""/>
            </StackPanel>
        </Border>

        <!-- 3. 文字位置调整 (醒目大按钮 + 单属性独立 / 复合整组可控切换) -->
        <Border Background=""#202534"" BorderBrush=""#323B4E"" BorderThickness=""1"" CornerRadius=""6""
                Padding=""12,10"" Margin=""0,0,0,8"">
            <StackPanel>
                <Button x:Name=""btnStartMoveJig""
                        Content=""🎯 拖拽平移文字位置 (指哪改哪)""
                        Style=""{StaticResource BtnPrimaryLarge}""
                        Height=""42"" FontSize=""13"" Margin=""0,0,0,8""/>
                <StackPanel x:Name=""pnlMovePartsOption"" Orientation=""Horizontal"" Margin=""0,0,0,6"" Visibility=""Collapsed"">
                    <TextBlock Text=""移动对象: "" Foreground=""#8F9CB0"" FontSize=""11"" VerticalAlignment=""Center""/>
                    <RadioButton x:Name=""radMoveCurrentOnly"" Content=""仅当前属性文字"" IsChecked=""True"" FontSize=""11"" Margin=""0,0,12,0""/>
                    <RadioButton x:Name=""radMoveAllParts"" Content=""同块复合文字整组移动"" FontSize=""11""/>
                </StackPanel>
                <CheckBox x:Name=""chkWindowSelect"" Content=""框选范围限定 (拖拽前先框选图块范围，默认同步全图)""
                          Foreground=""#7E8E9F"" FontSize=""11""/>
            </StackPanel>
        </Border>

        <!-- 4. 联动范围 -->
        <Border Background=""#1C1F2B"" BorderBrush=""#2F3647"" BorderThickness=""1"" CornerRadius=""6""
                Padding=""12,8"" Margin=""0,0,0,10"">
            <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                <TextBlock Text=""联动范围: "" FontWeight=""Bold"" Foreground=""#81A1C1"" Margin=""0,0,10,0"" VerticalAlignment=""Center""/>
                <RadioButton x:Name=""radSyncAll"" Content=""同步全图同名块"" IsChecked=""True"" Margin=""0,0,20,0""/>
                <RadioButton x:Name=""radSyncSingle"" Content=""仅当前单个图块""/>
            </StackPanel>
        </Border>

        <!-- 5. 底部流程控制 -->
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""*""/>
                <ColumnDefinition Width=""8""/>
                <ColumnDefinition Width=""80""/>
            </Grid.ColumnDefinitions>
            <Button x:Name=""btnPickNext"" Content=""🎯 点选下一个属性文字"" Style=""{StaticResource BtnBase}""
                    Height=""36"" Grid.Column=""0"" Background=""#26344A"" Foreground=""#88C0D0"" FontWeight=""Bold"" FontSize=""12""/>
            <Button x:Name=""btnClose"" Content=""关闭"" Style=""{StaticResource BtnBase}""
                    Height=""36"" Grid.Column=""2"" Padding=""12,0""/>
        </Grid>
    </StackPanel>
</Window>";

        private Window _window;
        private Document _doc;
        private AttributeTargetInfo _target;
        private Action _onPickNextRequested;

        private TextBlock _txtBlockTitle;
        private TextBlock _txtAttrDetail;
        private TextBlock _txtPartCountBadge;

        private TextBlock _txtCurHeightVal;
        private TextBox _txtNewHeight;
        private Button _btnApplyHeight;

        private ComboBox _cboLayer;
        private Button _btnPickLayer;
        private ComboBox _cboColor;
        private Button _btnApplyLayerColor;

        private Border _borderMultiPart;
        private TextBlock _txtMultiPartPreview;
        private RadioButton _radScheme1, _radScheme2;
        private CheckBox _chkAutoApplyOnHeightChange;
        private Button _btnApplyScheme;

        private StackPanel _pnlMovePartsOption;
        private RadioButton _radMoveCurrentOnly, _radMoveAllParts;
        private CheckBox _chkWindowSelect;
        private Button _btnStartMoveJig;
        private RadioButton _radSyncAll, _radSyncSingle;
        private Button _btnPickNext, _btnClose;

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

            _txtBlockTitle     = (TextBlock)_window.FindName("txtBlockTitle");
            _txtAttrDetail     = (TextBlock)_window.FindName("txtAttrDetail");
            _txtPartCountBadge = (TextBlock)_window.FindName("txtPartCountBadge");

            _txtCurHeightVal = (TextBlock)_window.FindName("txtCurHeightVal");
            _txtNewHeight    = (TextBox)_window.FindName("txtNewHeight");
            _btnApplyHeight  = (Button)_window.FindName("btnApplyHeight");

            _cboLayer           = (ComboBox)_window.FindName("cboLayer");
            _btnPickLayer       = (Button)_window.FindName("btnPickLayer");
            _cboColor           = (ComboBox)_window.FindName("cboColor");
            _btnApplyLayerColor = (Button)_window.FindName("btnApplyLayerColor");

            InitColorOptions();

            _borderMultiPart            = (Border)_window.FindName("borderMultiPart");
            _txtMultiPartPreview        = (TextBlock)_window.FindName("txtMultiPartPreview");
            _radScheme1                 = (RadioButton)_window.FindName("radScheme1");
            _radScheme2                 = (RadioButton)_window.FindName("radScheme2");
            _chkAutoApplyOnHeightChange = (CheckBox)_window.FindName("chkAutoApplyOnHeightChange");
            _btnApplyScheme             = (Button)_window.FindName("btnApplyScheme");

            _pnlMovePartsOption = (StackPanel)_window.FindName("pnlMovePartsOption");
            _radMoveCurrentOnly = (RadioButton)_window.FindName("radMoveCurrentOnly");
            _radMoveAllParts    = (RadioButton)_window.FindName("radMoveAllParts");
            _chkWindowSelect    = (CheckBox)_window.FindName("chkWindowSelect");
            _btnStartMoveJig    = (Button)_window.FindName("btnStartMoveJig");
            _radSyncAll         = (RadioButton)_window.FindName("radSyncAll");
            _radSyncSingle      = (RadioButton)_window.FindName("radSyncSingle");
            _btnPickNext        = (Button)_window.FindName("btnPickNext");
            _btnClose           = (Button)_window.FindName("btnClose");

            _btnApplyHeight.Click += delegate { ApplyInputHeight(); };
            _txtNewHeight.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    ApplyInputHeight();
                }
            };

            _btnPickLayer.Click       += BtnPickLayer_Click;
            _btnApplyLayerColor.Click += delegate { ApplyLayerAndColor(); };
            _cboColor.SelectionChanged += CboColor_SelectionChanged;

            _btnApplyScheme.Click    += delegate { ExecuteMultiPartScheme(); };
            _btnStartMoveJig.Click   += BtnStartMoveJig_Click;
            _btnPickNext.Click       += BtnPickNext_Click;
            _btnClose.Click          += delegate { _window.Close(); };
        }

        public void UpdateTarget(AttributeTargetInfo newTarget)
        {
            _target = newTarget;
            UpdateUiFromTarget();
        }

        private void UpdateUiFromTarget()
        {
            try { _window.Title = string.Format("图块属性修改 (XY-Toolbox Build {0})", XY_Deepening_Toolbox.Commands.CurrentBuildVersion); } catch { }
            _txtBlockTitle.Text = string.Format("图块: 【{0}】  |  标签: 【{1}】", _target.BlockName, _target.Tag);
            _txtCurHeightVal.Text = _target.CurrentHeight.ToString("0.###");
            _txtNewHeight.Text    = _target.CurrentHeight.ToString("0.###");

            if (_target.HasMultipleParts)
            {
                _txtPartCountBadge.Text = string.Format("🔗 {0} 段复合编号", _target.RelatedParts.Count);
                _txtAttrDetail.Text     = string.Format("复合编号: \"{0}\"", _target.CombinedText);
                List<string> tags = new List<string>();
                foreach (AttributePartInfo p in _target.RelatedParts) tags.Add(string.Format("\"{0}\"", p.TextString));
                _txtMultiPartPreview.Text  = string.Join(" + ", tags.ToArray());
                _borderMultiPart.Visibility = Visibility.Visible;
                _radScheme1.IsEnabled        = true;
                _radScheme2.IsEnabled        = true;
                _chkAutoApplyOnHeightChange.IsEnabled = true;
                _btnApplyScheme.IsEnabled    = true;

                if (_pnlMovePartsOption != null)
                {
                    _pnlMovePartsOption.Visibility = Visibility.Visible;
                    _radMoveCurrentOnly.Content = string.Format("仅当前属性【{0}】", _target.Tag);
                    _radMoveCurrentOnly.IsChecked = true; // 严格遵从指示：默认仅当前属性
                }
            }
            else
            {
                _txtPartCountBadge.Text     = "单段属性";
                _txtAttrDetail.Text         = string.Format("当前内容: \"{0}\"", _target.TextString);
                _borderMultiPart.Visibility = Visibility.Collapsed;
                if (_pnlMovePartsOption != null) _pnlMovePartsOption.Visibility = Visibility.Collapsed;
            }

            // 填充图层下拉列表
            if (_cboLayer != null && _doc != null)
            {
                string curLayer = _target.CurrentLayer ?? "";
                _cboLayer.Items.Clear();
                try
                {
                    List<string> layers = AttributeModifyService.GetAllLayerNames(_doc.Database);
                    foreach (string lyr in layers)
                    {
                        _cboLayer.Items.Add(lyr);
                    }
                }
                catch { }
                _cboLayer.Text = curLayer;
            }

            // 选中当前颜色
            if (_cboColor != null && _target.CurrentColor != null)
            {
                _isHandlingColorChange = true;
                try
                {
                    bool matched = false;
                    for (int i = 0; i < _cboColor.Items.Count; i++)
                    {
                        CadColorItem item = _cboColor.Items[i] as CadColorItem;
                        if (item != null && item.Color != null)
                        {
                            if (item.Color.ColorMethod == _target.CurrentColor.ColorMethod &&
                                item.Color.ColorIndex == _target.CurrentColor.ColorIndex)
                            {
                                _cboColor.SelectedIndex = i;
                                matched = true;
                                break;
                            }
                        }
                    }
                    if (!matched)
                    {
                        CadColorItem customItem = new CadColorItem
                        {
                            DisplayName = string.Format("🎨 自定义 ({0})", _target.CurrentColor.ColorNameForDisplay),
                            Color = _target.CurrentColor,
                            IsCustomTrigger = false
                        };
                        int insertIdx = Math.Max(0, _cboColor.Items.Count - 1);
                        _cboColor.Items.Insert(insertIdx, customItem);
                        _cboColor.SelectedItem = customItem;
                    }
                }
                catch { }
                finally
                {
                    _isHandlingColorChange = false;
                }
            }
        }

        #region 图层与颜色处理逻辑

        private class CadColorItem
        {
            public string DisplayName { get; set; }
            public Autodesk.AutoCAD.Colors.Color Color { get; set; }
            public bool IsCustomTrigger { get; set; }
            public override string ToString() { return DisplayName; }
        }

        private bool _isHandlingColorChange = false;

        private void InitColorOptions()
        {
            if (_cboColor == null) return;
            _cboColor.Items.Clear();

            _cboColor.Items.Add(new CadColorItem { DisplayName = "ByLayer (随层)", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256) });
            _cboColor.Items.Add(new CadColorItem { DisplayName = "ByBlock (随块)", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByBlock, 0) });
            _cboColor.Items.Add(new CadColorItem { DisplayName = "1 - 红色 (Red)", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1) });
            _cboColor.Items.Add(new CadColorItem { DisplayName = "2 - 黄色 (Yellow)", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 2) });
            _cboColor.Items.Add(new CadColorItem { DisplayName = "3 - 绿色 (Green)", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3) });
            _cboColor.Items.Add(new CadColorItem { DisplayName = "4 - 青色 (Cyan)", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 4) });
            _cboColor.Items.Add(new CadColorItem { DisplayName = "5 - 蓝色 (Blue)", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 5) });
            _cboColor.Items.Add(new CadColorItem { DisplayName = "6 - 洋红 (Magenta)", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 6) });
            _cboColor.Items.Add(new CadColorItem { DisplayName = "7 - 白/黑 (White)", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 7) });
            _cboColor.Items.Add(new CadColorItem { DisplayName = "251 - 灰度 (Gray)", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 251) });
            _cboColor.Items.Add(new CadColorItem { DisplayName = "🎨 更多颜色...", Color = null, IsCustomTrigger = true });

            _cboColor.SelectedIndex = 0;
        }

        private void CboColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isHandlingColorChange || _cboColor == null) return;
            CadColorItem item = _cboColor.SelectedItem as CadColorItem;
            if (item != null && item.IsCustomTrigger)
            {
                _isHandlingColorChange = true;
                try
                {
                    Autodesk.AutoCAD.Windows.ColorDialog dlg = new Autodesk.AutoCAD.Windows.ColorDialog();
                    if (_target != null && _target.CurrentColor != null)
                    {
                        try { dlg.Color = _target.CurrentColor; } catch { }
                    }
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        CadColorItem customItem = new CadColorItem
                        {
                            DisplayName = string.Format("🎨 自定义 ({0})", dlg.Color.ColorNameForDisplay),
                            Color = dlg.Color,
                            IsCustomTrigger = false
                        };
                        int insertIdx = Math.Max(0, _cboColor.Items.Count - 1);
                        _cboColor.Items.Insert(insertIdx, customItem);
                        _cboColor.SelectedItem = customItem;
                    }
                    else
                    {
                        _cboColor.SelectedIndex = 0;
                    }
                }
                catch { }
                finally
                {
                    _isHandlingColorChange = false;
                }
            }
        }

        private void BtnPickLayer_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            _window.Visibility = Visibility.Hidden;
            try
            {
                PromptEntityOptions peo = new PromptEntityOptions("\n>> 请在图纸中点选任意参考图元以提取目标图层 (按ESC取消): ");
                PromptEntityResult per = _doc.Editor.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    using (Transaction tr = _doc.Database.TransactionManager.StartTransaction())
                    {
                        Entity ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent != null)
                        {
                            string pickedLayer = ent.Layer;
                            _cboLayer.Text = pickedLayer;
                            _doc.Editor.WriteMessage(string.Format("\n[XY工具箱] 已成功提取目标图层: 【{0}】\n", pickedLayer));
                        }
                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                _doc.Editor.WriteMessage("\n[提示] 拾取图层取消或未完成: " + ex.Message + "\n");
            }
            finally
            {
                _window.Visibility = Visibility.Visible;
                _window.Activate();
            }
        }

        private void ApplyLayerAndColor()
        {
            if (_target == null || _doc == null) return;
            string newLayer = _cboLayer.Text != null ? _cboLayer.Text.Trim() : null;
            Autodesk.AutoCAD.Colors.Color newColor = null;
            CadColorItem selColor = _cboColor.SelectedItem as CadColorItem;
            if (selColor != null && !selColor.IsCustomTrigger)
            {
                newColor = selColor.Color;
            }

            if (string.IsNullOrEmpty(newLayer) && newColor == null)
            {
                MessageBox.Show("请选择或输入有效的图层名或颜色！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool syncAll = (_radSyncAll.IsChecked == true);
            bool useWindowSel = (_chkWindowSelect.IsChecked == true);
            HashSet<ObjectId> allowedIds = null;

            if (syncAll && useWindowSel)
            {
                _window.Visibility = Visibility.Hidden;
                try
                {
                    PromptSelectionOptions pso = new PromptSelectionOptions();
                    pso.MessageForAdding = "\n>> 请框选需要限定同步图层颜色的门窗图块范围 (回车同步全图): ";
                    PromptSelectionResult psr = _doc.Editor.GetSelection(pso);
                    if (psr.Status == PromptStatus.OK && psr.Value != null)
                    {
                        allowedIds = new HashSet<ObjectId>(psr.Value.GetObjectIds());
                    }
                }
                finally
                {
                    _window.Visibility = Visibility.Visible;
                    _window.Activate();
                }
            }

            try
            {
                int count = 0;
                using (_doc.LockDocument())
                {
                    count = AttributeModifyService.ModifyAttributeLayerAndColor(_doc.Database, _target, newLayer, newColor, syncAll, allowedIds);
                }
                _doc.Editor.Regen();
                string scopeNote = syncAll
                    ? string.Format("（同步更新了 {0} 处图块{1}）", count, useWindowSel && allowedIds != null ? "框选范围" : "")
                    : "（仅当前单图块）";
                string layerMsg = !string.IsNullOrEmpty(newLayer) ? string.Format("图层: [{0}] ", newLayer) : "";
                string colorMsg = (newColor != null && selColor != null) ? string.Format("颜色: [{0}] ", selColor.DisplayName) : "";
                _doc.Editor.WriteMessage(string.Format("\n[XY工具箱] 属性【{0}】{1}{2}已成功应用！{3}\n", _target.Tag, layerMsg, colorMsg, scopeNote));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("修改图层与颜色失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        private void ApplyInputHeight()
        {
            double h;
            if (double.TryParse(_txtNewHeight.Text, out h) && h > 0)
                ApplyHeightValue(h);
            else
                MessageBox.Show("请输入有效的正数字高！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    if (_target.HasMultipleParts && _chkAutoApplyOnHeightChange.IsChecked == true)
                    {
                        if (_radScheme1.IsChecked == true)
                        {
                            AttributeModifyService.MergeMultiPartAttributes(_doc.Database, _target, syncAll);
                            _target.RelatedParts = null;
                            _target.TextString = _target.CombinedText;
                            _txtBlockTitle.Text = string.Format("图块: 【{0}】 | 标签: 【{1}】", _target.BlockName, _target.Tag);
                            _txtPartCountBadge.Text = "已合并单段";
                            _borderMultiPart.Visibility = Visibility.Collapsed;
                            if (_pnlMovePartsOption != null) _pnlMovePartsOption.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            AttributeModifyService.CompactMultiPartAttributes(_doc.Database, _target, newH, syncAll);
                        }
                    }
                }
                _target.CurrentHeight = newH;
                _txtCurHeightVal.Text = newH.ToString("0.###");
                _txtNewHeight.Text    = newH.ToString("0.###");
                _txtAttrDetail.Text = _target.HasMultipleParts
                    ? string.Format("复合编号: \"{0}\"", _target.CombinedText)
                    : string.Format("当前内容: \"{0}\"", _target.TextString);
                _doc.Editor.Regen();
                _doc.Editor.WriteMessage(string.Format("\n[XY工具箱] 属性【{0}】字高已更新为 {1:0.###}（{2} 处图块联动）\n", _target.Tag, newH, count));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("修改字高失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteMultiPartScheme()
        {
            if (_target == null || _doc == null) return;
            if (!_target.HasMultipleParts) { MessageBox.Show("当前属性为单段完整编号，无需排版合并。", "提示"); return; }
            bool syncAll = (_radSyncAll.IsChecked == true);
            double curH;
            if (!double.TryParse(_txtNewHeight.Text, out curH) || curH <= 0.001) curH = _target.CurrentHeight;
            try
            {
                int count = 0;
                using (_doc.LockDocument())
                {
                    if (_radScheme1.IsChecked == true)
                    {
                        count = AttributeModifyService.MergeMultiPartAttributes(_doc.Database, _target, syncAll);
                        _target.RelatedParts = null;
                        _target.TextString = _target.CombinedText;
                        _txtBlockTitle.Text = string.Format("图块: 【{0}】 | 标签: 【{1}】", _target.BlockName, _target.Tag);
                        _txtPartCountBadge.Text = "已合并单段";
                        _borderMultiPart.Visibility = Visibility.Collapsed;
                        if (_pnlMovePartsOption != null) _pnlMovePartsOption.Visibility = Visibility.Collapsed;
                        _txtAttrDetail.Text = string.Format("编号已合并: \"{0}\"", _target.TextString);
                        _doc.Editor.WriteMessage(string.Format("\n[XY工具箱] 多段编号已合并为【{0}】（联动更新 {1} 处图块）\n", _target.TextString, count));
                    }
                    else
                    {
                        count = AttributeModifyService.CompactMultiPartAttributes(_doc.Database, _target, curH, syncAll);
                        _doc.Editor.WriteMessage(string.Format("\n[XY工具箱] 多段编号已智能紧凑重排（联动更新 {0} 处图块）\n", count));
                    }
                }
                _doc.Editor.Regen();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("执行排版失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnStartMoveJig_Click(object sender, RoutedEventArgs e)
        {
            if (_target == null || _doc == null) return;
            bool syncAll      = (_radSyncAll.IsChecked == true);
            bool useWindowSel = (_chkWindowSelect.IsChecked == true);
            bool moveAll      = (_target.HasMultipleParts && _radMoveAllParts != null && _radMoveAllParts.IsChecked == true);

            _window.Visibility = Visibility.Hidden;
            try
            {
                try { Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView(); } catch { }

                Database db = _doc.Database;
                Editor ed   = _doc.Editor;

                HashSet<ObjectId> allowedIds = null;
                if (useWindowSel && syncAll)
                {
                    PromptSelectionOptions selOpts = new PromptSelectionOptions();
                    selOpts.MessageForAdding = "\n>> 框选要同步调整的图块范围（直接回车=全图同名块）: ";
                    selOpts.AllowDuplicates  = false;
                    SelectionFilter filter = new SelectionFilter(new TypedValue[] { new TypedValue(0, "INSERT") });
                    PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
                    if (selRes.Status == PromptStatus.OK)
                    {
                        allowedIds = new HashSet<ObjectId>();
                        foreach (SelectedObject so in selRes.Value)
                            if (so != null) allowedIds.Add(so.ObjectId);
                    }
                    else if (selRes.Status == PromptStatus.Error) return;
                }

                List<AttributeReference> previewClones = new List<AttributeReference>();
                Point3d actualBasePoint = _target.PickedPoint;
                using (_doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        AttributeReference primaryAtt = tr.GetObject(_target.AttributeId, OpenMode.ForRead) as AttributeReference;
                        if (primaryAtt != null)
                        {
                            actualBasePoint = (primaryAtt.Justify == AttachmentPoint.BaseLeft)
                                ? primaryAtt.Position
                                : (primaryAtt.AlignmentPoint != Point3d.Origin ? primaryAtt.AlignmentPoint : primaryAtt.Position);
                        }

                        if (moveAll)
                        {
                            foreach (AttributePartInfo part in _target.RelatedParts)
                            {
                                AttributeReference att = tr.GetObject(part.AttributeId, OpenMode.ForRead) as AttributeReference;
                                if (att != null && !string.IsNullOrEmpty(att.TextString))
                                    previewClones.Add((AttributeReference)att.Clone());
                            }
                        }
                        else
                        {
                            if (primaryAtt != null) previewClones.Add((AttributeReference)primaryAtt.Clone());
                        }
                        tr.Commit();
                    }

                    XY_Deepening_Toolbox.Commands.AttributeTextMoveJig jig =
                        new XY_Deepening_Toolbox.Commands.AttributeTextMoveJig(actualBasePoint, previewClones, db, _target.TextString, _target.CurrentHeight);
                    PromptResult dragRes = ed.Drag(jig);

                    if (dragRes.Status == PromptStatus.OK)
                    {
                        Vector3d wcsDisp = jig.DisplacementVector;
                        if (!wcsDisp.IsZeroLength())
                        {
                            int moveCount = AttributeModifyService.MoveAttributePosition(db, _target, wcsDisp, syncAll, allowedIds, moveAll);
                            // 关键修复：即时更新 _target.PickedPoint 为平移后的最新绝对基点！
                            _target.PickedPoint = actualBasePoint + wcsDisp;
                            // 彻底消除 Regen 带来的全屏黑屏闪烁，采用轻量平滑的 UpdateScreen 瞬时无闪刷新
                            try { ed.UpdateScreen(); } catch { }
                            string scopeNote = syncAll
                                ? string.Format("同步更新了 {0} 处图块{1}", moveCount, useWindowSel && allowedIds != null ? "（框选范围）" : "（全图）")
                                : "仅当前单图块";
                            string targetDesc = moveAll ? "同块复合编号整组" : string.Format("【{0}】", _target.Tag);
                            ed.WriteMessage(string.Format("\n[XY工具箱] 属性{0}位置已平移！{1}\n", targetDesc, scopeNote));
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
            if (_onPickNextRequested != null) _onPickNextRequested();
        }
    }
}
