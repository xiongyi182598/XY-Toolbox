using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Visibility = System.Windows.Visibility;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;
using TextFilterWorkbench.Models;
using TextFilterWorkbench.Services;

namespace TextFilterWorkbench.Views
{
    /// <summary>
    /// 智能文字选择与批量过滤工作台 WPF 交互界面
    /// </summary>
    public class TextFilterDialog
    {
        private static readonly string DialogXaml =
@"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""智能文字选择与批量过滤工作台 (XY-Toolbox)""
        Width=""720"" Height=""760"" MinWidth=""660"" MinHeight=""700""
        WindowStartupLocation=""CenterScreen""
        Background=""#1E222A"" Foreground=""#ECEFF4""
        FontFamily=""Segoe UI, Microsoft YaHei, 微软雅黑"" FontSize=""12""
        ResizeMode=""CanResizeWithGrip""
        KeyboardNavigation.DirectionalNavigation=""None""
        KeyboardNavigation.TabNavigation=""Local"">

    <Window.Resources>
        <!-- 基础按钮样式 -->
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

        <!-- 文本框样式 -->
        <Style TargetType=""TextBox"">
            <Setter Property=""Background"" Value=""#16191F""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""BorderBrush"" Value=""#5E6A82""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Padding"" Value=""6,3""/>
            <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
            <Setter Property=""CaretBrush"" Value=""#FFFFFF""/>
        </Style>

        <!-- 复选框样式 -->
        <Style TargetType=""CheckBox"">
            <Setter Property=""Foreground"" Value=""#ECEFF4""/>
            <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
        </Style>

        <!-- 单选框样式 -->
        <Style TargetType=""RadioButton"">
            <Setter Property=""Foreground"" Value=""#ECEFF4""/>
            <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
        </Style>

        <!-- 深色不可编辑 ComboBox 模板 -->
        <ControlTemplate x:Key=""DarkComboBoxTemplate"" TargetType=""{x:Type ComboBox}"">
            <Grid x:Name=""templateRoot"" SnapsToDevicePixels=""True"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition MinWidth=""24"" Width=""24""/>
                </Grid.ColumnDefinitions>

                <Border x:Name=""mainBorder"" Grid.ColumnSpan=""2""
                        Background=""#16191F""
                        BorderBrush=""#5E6A82""
                        BorderThickness=""1""
                        CornerRadius=""3""/>

                <ContentPresenter Grid.Column=""0""
                                  Content=""{TemplateBinding SelectionBoxItem}""
                                  ContentTemplate=""{TemplateBinding SelectionBoxItemTemplate}""
                                  ContentTemplateSelector=""{TemplateBinding ItemTemplateSelector}""
                                  Margin=""8,3""
                                  VerticalAlignment=""Center""
                                  HorizontalAlignment=""Left""
                                  IsHitTestVisible=""False"">
                    <ContentPresenter.Resources>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
                            <Setter Property=""FontWeight"" Value=""Bold""/>
                        </Style>
                    </ContentPresenter.Resources>
                </ContentPresenter>

                <ToggleButton Grid.Column=""1"" Background=""Transparent"" BorderThickness=""0"" Focusable=""False""
                              IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"">
                    <Path Data=""M 0 0 L 4 4 L 8 0 Z"" Fill=""#88C0D0"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                </ToggleButton>

                <Popup x:Name=""PART_Popup"" AllowsTransparency=""True"" Grid.ColumnSpan=""2""
                       IsOpen=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}""
                       Placement=""Bottom"" PopupAnimation=""Slide"">
                    <Border x:Name=""dropDownBorder"" Background=""#1E222A"" BorderBrush=""#5E6A82"" BorderThickness=""1""
                            CornerRadius=""3"" MaxHeight=""260"" MinWidth=""{Binding ActualWidth, ElementName=templateRoot}"">
                        <ScrollViewer SnapsToDevicePixels=""True"">
                            <ItemsPresenter KeyboardNavigation.DirectionalNavigation=""Contained""/>
                        </ScrollViewer>
                    </Border>
                </Popup>
            </Grid>
        </ControlTemplate>

        <Style TargetType=""ComboBoxItem"">
            <Setter Property=""Background"" Value=""#1E222A""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""Padding"" Value=""8,5""/>
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""ComboBoxItem"">
                        <Border x:Name=""itemBorder"" Background=""{TemplateBinding Background}""
                                Padding=""{TemplateBinding Padding}"" SnapsToDevicePixels=""True"">
                            <ContentPresenter/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter TargetName=""itemBorder"" Property=""Background"" Value=""#3B4252""/>
                            </Trigger>
                            <Trigger Property=""IsSelected"" Value=""True"">
                                <Setter TargetName=""itemBorder"" Property=""Background"" Value=""#4C566A""/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType=""ComboBox"">
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""Background"" Value=""#16191F""/>
            <Setter Property=""Template"" Value=""{StaticResource DarkComboBoxTemplate}""/>
        </Style>
    </Window.Resources>

    <Grid Margin=""16,14,16,16"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""Auto""/> <!-- 0: 顶部标题与吸管 -->
            <RowDefinition Height=""*""/>    <!-- 1: 过滤条件卡片列表 -->
            <RowDefinition Height=""Auto""/> <!-- 2: 范围选择 -->
            <RowDefinition Height=""Auto""/> <!-- 3: 批量修改折叠区 -->
            <RowDefinition Height=""Auto""/> <!-- 4: 底部操作栏 -->
        </Grid.RowDefinitions>

        <!-- 0. 顶部标题与吸管卡片 -->
        <Border Grid.Row=""0"" Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12"" Margin=""0,0,0,10"">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column=""0"">
                    <StackPanel Orientation=""Horizontal"">
                        <TextBlock Text=""🔤 智能文字过滤与批量选择看板"" FontWeight=""Bold"" FontSize=""14"" Foreground=""#ECEFF4""/>
                        <Border Background=""#4C566A"" CornerRadius=""3"" Padding=""6,1"" Margin=""10,0,0,0"">
                            <TextBlock Text=""XY-Toolbox v1.2"" FontSize=""10"" Foreground=""#A3BE8C""/>
                        </Border>
                    </StackPanel>
                    <TextBlock x:Name=""txtSampleInfo"" Text=""🎯 当前未吸取参考文字 (点击右侧吸管在图纸中直接点选文字自动填充所有参数)""
                               Foreground=""#D8DEE9"" FontSize=""11"" Margin=""0,6,0,0"" TextWrapping=""Wrap""/>
                </StackPanel>

                <Button x:Name=""btnEyedropper"" Grid.Column=""1"" Background=""#5E81AC"" BorderBrush=""#81A1C1"" Foreground=""#FFFFFF""
                        FontWeight=""Bold"" Padding=""14,8"" Margin=""12,0,0,0"" VerticalAlignment=""Center"">
                    <StackPanel Orientation=""Horizontal"">
                        <TextBlock Text=""🎯 吸取参考文字"" FontWeight=""Bold"" VerticalAlignment=""Center""/>
                    </StackPanel>
                </Button>
            </Grid>
        </Border>

        <!-- 1. 过滤条件主体 -->
        <ScrollViewer Grid.Row=""1"" VerticalScrollBarVisibility=""Auto"" Margin=""0,0,0,10"">
            <StackPanel>
                <!-- 卡片 1: 字高过滤 -->
                <Border Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12"" Margin=""0,0,0,10"">
                    <StackPanel>
                        <CheckBox x:Name=""chkHeight"" IsChecked=""True"" FontWeight=""Bold"" Foreground=""#88C0D0"" Content=""启用字高过滤 (Text Height)""/>
                        <Grid Margin=""0,8,0,0"">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""100""/>
                                <ColumnDefinition Width=""12""/>
                                <ColumnDefinition Width=""120""/>
                                <ColumnDefinition Width=""12""/>
                                <ColumnDefinition Width=""120""/>
                                <ColumnDefinition Width=""12""/>
                                <ColumnDefinition Width=""*""/>
                            </Grid.ColumnDefinitions>

                            <!-- 操作符 -->
                            <StackPanel Grid.Column=""0"">
                                <TextBlock Text=""比较模式:"" FontSize=""11"" Foreground=""#9AA5B8"" Margin=""0,0,0,3""/>
                                <ComboBox x:Name=""cmbHeightOp"" Height=""28"">
                                    <ComboBoxItem Content=""= (等于)"" IsSelected=""True""/>
                                    <ComboBoxItem Content=""&gt;= (大于等于)""/>
                                    <ComboBoxItem Content=""&lt;= (小于等于)""/>
                                    <ComboBoxItem Content=""范围 (Between)""/>
                                </ComboBox>
                            </StackPanel>

                            <!-- 基准字高 -->
                            <StackPanel Grid.Column=""2"">
                                <TextBlock Text=""目标字高:"" FontSize=""11"" Foreground=""#9AA5B8"" Margin=""0,0,0,3""/>
                                <TextBox x:Name=""txtHeightValue"" Text=""3.5"" Height=""28""/>
                            </StackPanel>

                            <!-- 上限字高 (用于范围) -->
                            <StackPanel Grid.Column=""4"">
                                <TextBlock Text=""上限字高 (范围):"" FontSize=""11"" Foreground=""#9AA5B8"" Margin=""0,0,0,3""/>
                                <TextBox x:Name=""txtHeightMax"" Text=""5.0"" Height=""28"" IsEnabled=""False""/>
                            </StackPanel>

                            <!-- 容差 -->
                            <StackPanel Grid.Column=""6"">
                                <TextBlock Text=""浮点容差 (±tol):"" FontSize=""11"" Foreground=""#9AA5B8"" Margin=""0,0,0,3""/>
                                <TextBox x:Name=""txtHeightTol"" Text=""0.05"" Height=""28""/>
                            </StackPanel>
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- 卡片 2: 文字样式与物理字体 -->
                <Border Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12"" Margin=""0,0,0,10"">
                    <StackPanel>
                        <CheckBox x:Name=""chkStyle"" IsChecked=""False"" FontWeight=""Bold"" Foreground=""#A3BE8C"" Content=""启用文字样式 / 字体过滤 (TextStyle / Font)""/>
                        <Grid Margin=""0,8,0,0"">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""*""/>
                                <ColumnDefinition Width=""16""/>
                                <ColumnDefinition Width=""*""/>
                            </Grid.ColumnDefinitions>

                            <StackPanel Grid.Column=""0"">
                                <TextBlock Text=""图面文字样式 (TextStyle):"" FontSize=""11"" Foreground=""#9AA5B8"" Margin=""0,0,0,3""/>
                                <ComboBox x:Name=""cmbTextStyle"" Height=""28""/>
                            </StackPanel>

                            <StackPanel Grid.Column=""2"">
                                <CheckBox x:Name=""chkFont"" Content=""匹配物理字体文件名/字型:"" FontSize=""11"" Foreground=""#9AA5B8"" Margin=""0,0,0,3""/>
                                <TextBox x:Name=""txtFontName"" Height=""28"" ToolTip=""例如: gbenor, simsun, hztxt, Arial 等""/>
                            </StackPanel>
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- 卡片 3: 颜色过滤 (含随层穿透解析) -->
                <Border Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12"" Margin=""0,0,0,10"">
                    <StackPanel>
                        <CheckBox x:Name=""chkColor"" IsChecked=""False"" FontWeight=""Bold"" Foreground=""#EBCB8B"" Content=""启用颜色过滤 (Color)""/>
                        <Grid Margin=""0,8,0,0"">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""240""/>
                                <ColumnDefinition Width=""20""/>
                                <ColumnDefinition Width=""*""/>
                            </Grid.ColumnDefinitions>

                            <StackPanel Grid.Column=""0"">
                                <TextBlock Text=""目标颜色 (ACI):"" FontSize=""11"" Foreground=""#9AA5B8"" Margin=""0,0,0,3""/>
                                <ComboBox x:Name=""cmbColor"" Height=""28""/>
                            </StackPanel>

                            <StackPanel Grid.Column=""2"" VerticalAlignment=""Center"">
                                <CheckBox x:Name=""chkResolveByLayer"" IsChecked=""True""
                                          Content=""穿透解析随层颜色 (ByLayer 视效穿透)"" FontWeight=""SemiBold"" Foreground=""#88C0D0""
                                          ToolTip=""勾选时，如果实体颜色为 ByLayer(随层)，将读取其所在图层的实际颜色参与匹配。例如选择红色时，红图层上的随层文字也会被精准选中！""/>
                                <TextBlock Text=""推荐保持勾选，确保所见即所得。取消后仅匹配实体硬编码颜色。"" FontSize=""10"" Foreground=""#6B7A94"" Margin=""20,2,0,0""/>
                            </StackPanel>
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- 卡片 4: 图层与实体类型过滤 -->
                <Border Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12"" Margin=""0,0,0,10"">
                    <StackPanel>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""*""/>
                                <ColumnDefinition Width=""16""/>
                                <ColumnDefinition Width=""*""/>
                            </Grid.ColumnDefinitions>

                            <!-- 图层 -->
                            <StackPanel Grid.Column=""0"">
                                <CheckBox x:Name=""chkLayer"" IsChecked=""False"" FontWeight=""Bold"" Foreground=""#D08770"" Content=""启用图层过滤 (Layer)""/>
                                <ComboBox x:Name=""cmbLayer"" Height=""28"" Margin=""0,6,0,0""/>
                            </StackPanel>

                            <!-- 实体类型 -->
                            <StackPanel Grid.Column=""2"">
                                <TextBlock Text=""包含文字实体类型:"" FontWeight=""Bold"" Foreground=""#ECEFF4"" Margin=""0,0,0,6""/>
                                <WrapPanel>
                                    <CheckBox x:Name=""chkIncludeDBText"" Content=""单行文字 (DBText)"" IsChecked=""True"" Margin=""0,0,12,4""/>
                                    <CheckBox x:Name=""chkIncludeMText"" Content=""多行文字 (MText)"" IsChecked=""True"" Margin=""0,0,12,4""/>
                                    <CheckBox x:Name=""chkIncludeAttrib"" Content=""块属性 (Attribute)"" IsChecked=""True"" Margin=""0,0,0,4""/>
                                </WrapPanel>
                            </StackPanel>
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- 卡片 5: 内容文字过滤 -->
                <Border Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12"">
                    <StackPanel>
                        <CheckBox x:Name=""chkContent"" IsChecked=""False"" FontWeight=""Bold"" Foreground=""#B48EAD"" Content=""启用文字内容过滤 (Text Content)""/>
                        <Grid Margin=""0,8,0,0"">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""*""/>
                                <ColumnDefinition Width=""12""/>
                                <ColumnDefinition Width=""130""/>
                                <ColumnDefinition Width=""12""/>
                                <ColumnDefinition Width=""Auto""/>
                            </Grid.ColumnDefinitions>

                            <TextBox x:Name=""txtContentPattern"" Height=""28"" Grid.Column=""0""/>

                            <ComboBox x:Name=""cmbContentMatchType"" Height=""28"" Grid.Column=""2"">
                                <ComboBoxItem Content=""包含 (Contains)"" IsSelected=""True""/>
                                <ComboBoxItem Content=""精确相等 (Exact)""/>
                                <ComboBoxItem Content=""开头匹配 (StartsWith)""/>
                                <ComboBoxItem Content=""结尾匹配 (EndsWith)""/>
                                <ComboBoxItem Content=""通配符/正则""/>
                            </ComboBox>

                            <CheckBox x:Name=""chkCaseSensitive"" Content=""区分大小写"" Grid.Column=""4"" VerticalAlignment=""Center""/>
                        </Grid>
                    </StackPanel>
                </Border>
            </StackPanel>
        </ScrollViewer>

        <!-- 2. 搜索范围 -->
        <Border Grid.Row=""2"" Background=""#262B35"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12,8"" Margin=""0,0,0,10"">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column=""0"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
                    <TextBlock Text=""搜索范围: "" FontWeight=""Bold"" VerticalAlignment=""Center"" Foreground=""#88C0D0"" Margin=""0,0,15,0""/>
                    <RadioButton x:Name=""radScopeSpace"" Content=""当前空间全图 (模型/布局)"" IsChecked=""True"" Margin=""0,0,24,0""/>
                    <RadioButton x:Name=""radScopeWindow"" Content=""手动框选范围 (CAD 框选)""/>
                </StackPanel>
                <Button x:Name=""btnToggleBatch"" Grid.Column=""1"" Content=""✏️ 展开批量修改面板 ▼"" Background=""Transparent"" BorderBrush=""#4C566A"" Foreground=""#A3BE8C""
                        Padding=""10,4"" VerticalAlignment=""Center""/>
            </Grid>
        </Border>

        <!-- 3. 批量修改折叠区 -->
        <Border x:Name=""borderBatchModify"" Grid.Row=""3"" Background=""#232832"" BorderBrush=""#434C5E"" BorderThickness=""1"" CornerRadius=""6""
                Padding=""12"" Margin=""0,0,0,10"" Visibility=""Collapsed"">
            <StackPanel>
                <TextBlock Text=""✏️ 批量修改选中文字属性 (勾选需要修改的项目，不勾选的项目保持原状):"" FontWeight=""Bold"" Foreground=""#A3BE8C"" Margin=""0,0,0,8""/>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""10""/>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""10""/>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""10""/>
                        <ColumnDefinition Width=""*""/>
                    </Grid.ColumnDefinitions>

                    <StackPanel Grid.Column=""0"">
                        <CheckBox x:Name=""chkBatchHeight"" Content=""新字高:"" Margin=""0,0,0,3""/>
                        <TextBox x:Name=""txtBatchHeight"" Text=""3.5"" Height=""26""/>
                    </StackPanel>

                    <StackPanel Grid.Column=""2"">
                        <CheckBox x:Name=""chkBatchStyle"" Content=""新文字样式:"" Margin=""0,0,0,3""/>
                        <ComboBox x:Name=""cmbBatchStyle"" Height=""26""/>
                    </StackPanel>

                    <StackPanel Grid.Column=""4"">
                        <CheckBox x:Name=""chkBatchColor"" Content=""新颜色:"" Margin=""0,0,0,3""/>
                        <ComboBox x:Name=""cmbBatchColor"" Height=""26""/>
                    </StackPanel>

                    <StackPanel Grid.Column=""6"">
                        <CheckBox x:Name=""chkBatchLayer"" Content=""新图层:"" Margin=""0,0,0,3""/>
                        <ComboBox x:Name=""cmbBatchLayer"" Height=""26""/>
                    </StackPanel>
                </Grid>
                <Button x:Name=""btnExecuteBatchModify"" Content=""🚀 确认对匹配的文字执行批量修改"" Background=""#A3BE8C"" Foreground=""#2E3440""
                        FontWeight=""Bold"" Height=""30"" Margin=""0,10,0,0""/>
            </StackPanel>
        </Border>

        <!-- 4. 底部主操作栏 -->
        <Grid Grid.Row=""4"" Margin=""0,4,0,0"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""*""/>
                <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column=""0"" Orientation=""Horizontal"">
                <!-- 立即选中 -->
                <Button x:Name=""btnSelectToCad"" Background=""#88C0D0"" Foreground=""#1E222A"" FontWeight=""Bold""
                        Padding=""20,8"" Height=""34"" Margin=""0,0,10,0"">
                    <StackPanel Orientation=""Horizontal"">
                        <TextBlock Text=""⚡ 立即选择到图面 (带夹点)"" FontSize=""12"" FontWeight=""Bold"" VerticalAlignment=""Center""/>
                    </StackPanel>
                </Button>

                <!-- 隔离文本 -->
                <Button x:Name=""btnIsolate"" Background=""#B48EAD"" Foreground=""#1E222A"" FontWeight=""Bold""
                        Padding=""16,8"" Height=""34"" Margin=""0,0,10,0"">
                    <StackPanel Orientation=""Horizontal"">
                        <TextBlock Text=""👁️ 仅隔离显示文字"" FontSize=""12"" FontWeight=""Bold"" VerticalAlignment=""Center""/>
                    </StackPanel>
                </Button>
            </StackPanel>

            <Button x:Name=""btnClose"" Grid.Column=""1"" Content=""关闭"" Background=""#2E3440"" Foreground=""#D8DEE9"" Height=""34"" Padding=""20,8""/>
        </Grid>
    </Grid>
</Window>";

        private Window _window;
        private Database _db;
        private Editor _ed;

        // UI 控件引用
        private TextBlock _txtSampleInfo;
        private Button _btnEyedropper;

        private CheckBox _chkHeight;
        private ComboBox _cmbHeightOp;
        private TextBox _txtHeightValue;
        private TextBox _txtHeightMax;
        private TextBox _txtHeightTol;

        private CheckBox _chkStyle;
        private ComboBox _cmbTextStyle;
        private CheckBox _chkFont;
        private TextBox _txtFontName;

        private CheckBox _chkColor;
        private ComboBox _cmbColor;
        private CheckBox _chkResolveByLayer;

        private CheckBox _chkLayer;
        private ComboBox _cmbLayer;
        private CheckBox _chkIncludeDBText;
        private CheckBox _chkIncludeMText;
        private CheckBox _chkIncludeAttrib;

        private CheckBox _chkContent;
        private TextBox _txtContentPattern;
        private ComboBox _cmbContentMatchType;
        private CheckBox _chkCaseSensitive;

        private RadioButton _radScopeSpace;
        private RadioButton _radScopeWindow;

        private Button _btnToggleBatch;
        private Border _borderBatchModify;
        private CheckBox _chkBatchHeight;
        private TextBox _txtBatchHeight;
        private CheckBox _chkBatchStyle;
        private ComboBox _cmbBatchStyle;
        private CheckBox _chkBatchColor;
        private ComboBox _cmbBatchColor;
        private CheckBox _chkBatchLayer;
        private ComboBox _cmbBatchLayer;
        private Button _btnExecuteBatchModify;

        private Button _btnSelectToCad;
        private Button _btnIsolate;
        private Button _btnClose;

        private static readonly short[] StandardColorIndices = new short[]
        {
            1,   // 红
            2,   // 黄
            3,   // 绿
            4,   // 青
            5,   // 蓝
            6,   // 洋红
            7,   // 白/黑
            8,   // 深灰
            9,   // 浅灰
            256, // 随层 ByLayer
            0    // 随块 ByBlock
        };

        public static void ShowDialog(Database db, Editor ed)
        {
            TextFilterDialog dialog = new TextFilterDialog(db, ed);
            Autodesk.AutoCAD.ApplicationServices.Application.ShowModelessWindow(dialog._window);
        }

        public TextFilterDialog(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;
            InitializeComponent();
            PopulateDrawingData();
        }

        private void InitializeComponent()
        {
            _window = (Window)XamlReader.Parse(DialogXaml);

            _txtSampleInfo = (TextBlock)_window.FindName("txtSampleInfo");
            _btnEyedropper = (Button)_window.FindName("btnEyedropper");

            _chkHeight = (CheckBox)_window.FindName("chkHeight");
            _cmbHeightOp = (ComboBox)_window.FindName("cmbHeightOp");
            _txtHeightValue = (TextBox)_window.FindName("txtHeightValue");
            _txtHeightMax = (TextBox)_window.FindName("txtHeightMax");
            _txtHeightTol = (TextBox)_window.FindName("txtHeightTol");

            _chkStyle = (CheckBox)_window.FindName("chkStyle");
            _cmbTextStyle = (ComboBox)_window.FindName("cmbTextStyle");
            _chkFont = (CheckBox)_window.FindName("chkFont");
            _txtFontName = (TextBox)_window.FindName("txtFontName");

            _chkColor = (CheckBox)_window.FindName("chkColor");
            _cmbColor = (ComboBox)_window.FindName("cmbColor");
            _chkResolveByLayer = (CheckBox)_window.FindName("chkResolveByLayer");

            _chkLayer = (CheckBox)_window.FindName("chkLayer");
            _cmbLayer = (ComboBox)_window.FindName("cmbLayer");
            _chkIncludeDBText = (CheckBox)_window.FindName("chkIncludeDBText");
            _chkIncludeMText = (CheckBox)_window.FindName("chkIncludeMText");
            _chkIncludeAttrib = (CheckBox)_window.FindName("chkIncludeAttrib");

            _chkContent = (CheckBox)_window.FindName("chkContent");
            _txtContentPattern = (TextBox)_window.FindName("txtContentPattern");
            _cmbContentMatchType = (ComboBox)_window.FindName("cmbContentMatchType");
            _chkCaseSensitive = (CheckBox)_window.FindName("chkCaseSensitive");

            _radScopeSpace = (RadioButton)_window.FindName("radScopeSpace");
            _radScopeWindow = (RadioButton)_window.FindName("radScopeWindow");

            _btnToggleBatch = (Button)_window.FindName("btnToggleBatch");
            _borderBatchModify = (Border)_window.FindName("borderBatchModify");
            _chkBatchHeight = (CheckBox)_window.FindName("chkBatchHeight");
            _txtBatchHeight = (TextBox)_window.FindName("txtBatchHeight");
            _chkBatchStyle = (CheckBox)_window.FindName("chkBatchStyle");
            _cmbBatchStyle = (ComboBox)_window.FindName("cmbBatchStyle");
            _chkBatchColor = (CheckBox)_window.FindName("chkBatchColor");
            _cmbBatchColor = (ComboBox)_window.FindName("cmbBatchColor");
            _chkBatchLayer = (CheckBox)_window.FindName("chkBatchLayer");
            _cmbBatchLayer = (ComboBox)_window.FindName("cmbBatchLayer");
            _btnExecuteBatchModify = (Button)_window.FindName("btnExecuteBatchModify");

            _btnSelectToCad = (Button)_window.FindName("btnSelectToCad");
            _btnIsolate = (Button)_window.FindName("btnIsolate");
            _btnClose = (Button)_window.FindName("btnClose");

            // 事件绑定
            _btnEyedropper.Click += BtnEyedropper_Click;
            _cmbHeightOp.SelectionChanged += CmbHeightOp_SelectionChanged;
            _btnToggleBatch.Click += BtnToggleBatch_Click;
            _btnExecuteBatchModify.Click += BtnExecuteBatchModify_Click;
            _btnSelectToCad.Click += BtnSelectToCad_Click;
            _btnIsolate.Click += BtnIsolate_Click;
            _btnClose.Click += delegate { _window.Close(); };
        }

        private void CmbHeightOp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cmbHeightOp == null || _txtHeightMax == null) return;
            // 选项索引 3 是 "范围 (Between)"
            _txtHeightMax.IsEnabled = (_cmbHeightOp.SelectedIndex == 3);
        }

        private void BtnToggleBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_borderBatchModify.Visibility == Visibility.Visible)
            {
                _borderBatchModify.Visibility = Visibility.Collapsed;
                _btnToggleBatch.Content = "✏️ 展开批量修改面板 ▼";
            }
            else
            {
                _borderBatchModify.Visibility = Visibility.Visible;
                _btnToggleBatch.Content = "✏️ 收起批量修改面板 ▲";
            }
        }

        /// <summary>
        /// 从当前图纸加载文字样式、图层、标准颜色下拉项
        /// </summary>
        private void PopulateDrawingData()
        {
            // 1. 加载标准颜色
            _cmbColor.Items.Clear();
            _cmbBatchColor.Items.Clear();
            foreach (short c in StandardColorIndices)
            {
                string name = TextFilterService.GetAciColorName(c);
                ComboBoxItem item = new ComboBoxItem { Content = name, Tag = c };
                _cmbColor.Items.Add(item);

                ComboBoxItem batchItem = new ComboBoxItem { Content = name, Tag = c };
                _cmbBatchColor.Items.Add(batchItem);
            }
            if (_cmbColor.Items.Count > 0) _cmbColor.SelectedIndex = 0; // 默认红色
            if (_cmbBatchColor.Items.Count > 0) _cmbBatchColor.SelectedIndex = 0;

            // 2. 加载文字样式与图层
            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                // 文字样式
                TextStyleTable st = tr.GetObject(_db.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
                _cmbTextStyle.Items.Clear();
                _cmbBatchStyle.Items.Clear();
                if (st != null)
                {
                    foreach (ObjectId id in st)
                    {
                        TextStyleTableRecord r = tr.GetObject(id, OpenMode.ForRead) as TextStyleTableRecord;
                        if (r != null && !string.IsNullOrEmpty(r.Name))
                        {
                            _cmbTextStyle.Items.Add(new ComboBoxItem { Content = r.Name, Tag = r.Name });
                            _cmbBatchStyle.Items.Add(new ComboBoxItem { Content = r.Name, Tag = r.Name });
                        }
                    }
                }
                if (_cmbTextStyle.Items.Count > 0) _cmbTextStyle.SelectedIndex = 0;
                if (_cmbBatchStyle.Items.Count > 0) _cmbBatchStyle.SelectedIndex = 0;

                // 图层
                LayerTable lt = tr.GetObject(_db.LayerTableId, OpenMode.ForRead) as LayerTable;
                _cmbLayer.Items.Clear();
                _cmbBatchLayer.Items.Clear();
                if (lt != null)
                {
                    foreach (ObjectId id in lt)
                    {
                        LayerTableRecord r = tr.GetObject(id, OpenMode.ForRead) as LayerTableRecord;
                        if (r != null && !string.IsNullOrEmpty(r.Name))
                        {
                            _cmbLayer.Items.Add(new ComboBoxItem { Content = r.Name, Tag = r.Name });
                            _cmbBatchLayer.Items.Add(new ComboBoxItem { Content = r.Name, Tag = r.Name });
                        }
                    }
                }
                if (_cmbLayer.Items.Count > 0) _cmbLayer.SelectedIndex = 0;
                if (_cmbBatchLayer.Items.Count > 0) _cmbBatchLayer.SelectedIndex = 0;

                tr.Commit();
            }
        }

        /// <summary>
        /// 吸取参考文字
        /// </summary>
        private void BtnEyedropper_Click(object sender, RoutedEventArgs e)
        {
            _window.Visibility = Visibility.Hidden;

            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                Editor ed = doc.Editor;
                PromptEntityOptions opt = new PromptEntityOptions("\n[XY-Toolbox] 请点选图纸中的参考文字 (TEXT / MTEXT / ATTRIB): ");
                opt.SetRejectMessage("\n只能点选单行文字、多行文字或块属性！");
                opt.AddAllowedClass(typeof(DBText), true);
                opt.AddAllowedClass(typeof(MText), true);
                opt.AddAllowedClass(typeof(AttributeReference), true);

                PromptEntityResult res = ed.GetEntity(opt);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n已取消吸取。");
                    return;
                }

                TextSampleInfo sample = null;
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    sample = TextFilterService.ExtractSample(res.ObjectId, doc.Database, tr);
                    tr.Commit();
                }

                if (sample != null)
                {
                    ApplySampleToUi(sample);
                    ed.WriteMessage(string.Format("\n[XY-Toolbox 已吸取] 类型: {0} | 字高: {1:0.###} | 样式: {2} | 颜色: {3} | 图层: {4}\n",
                        sample.EntityTypeName, sample.Height, sample.TextStyleName, sample.GetColorDisplayName(), sample.LayerName));
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("吸取文字属性失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _window.Visibility = Visibility.Visible;
                _window.Activate();
            }
        }

        private void ApplySampleToUi(TextSampleInfo s)
        {
            _txtSampleInfo.Text = string.Format("🎯 【已吸取参考】{0} | 字高: {1:0.###} | 样式: {2} | 颜色: {3} | 图层: {4} | 内容: '{5}'",
                s.EntityTypeName, s.Height, s.TextStyleName, s.GetColorDisplayName(), s.LayerName,
                s.CleanContent.Length > 20 ? s.CleanContent.Substring(0, 20) + "..." : s.CleanContent);

            // 1. 字高
            _chkHeight.IsChecked = true;
            _cmbHeightOp.SelectedIndex = 0; // 等于
            _txtHeightValue.Text = s.Height.ToString("0.###");

            // 2. 样式
            _chkStyle.IsChecked = true;
            for (int i = 0; i < _cmbTextStyle.Items.Count; i++)
            {
                ComboBoxItem item = _cmbTextStyle.Items[i] as ComboBoxItem;
                if (item != null && string.Equals(item.Tag as string, s.TextStyleName, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbTextStyle.SelectedIndex = i;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(s.FontName))
            {
                _txtFontName.Text = s.FontName;
            }

            // 3. 颜色
            _chkColor.IsChecked = true;
            short targetColor = s.IsByLayer ? s.EffectiveColorIndex : s.ColorIndex;
            bool colorFound = false;
            for (int i = 0; i < _cmbColor.Items.Count; i++)
            {
                ComboBoxItem item = _cmbColor.Items[i] as ComboBoxItem;
                if (item != null && item.Tag is short && (short)item.Tag == targetColor)
                {
                    _cmbColor.SelectedIndex = i;
                    colorFound = true;
                    break;
                }
            }
            if (!colorFound)
            {
                // 如果是特殊颜色号，临时加入
                ComboBoxItem newItem = new ComboBoxItem { Content = TextFilterService.GetAciColorName(targetColor), Tag = targetColor };
                _cmbColor.Items.Add(newItem);
                _cmbColor.SelectedItem = newItem;
            }
            _chkResolveByLayer.IsChecked = true;

            // 4. 图层
            _chkLayer.IsChecked = false; // 默认不限制图层，便于跨图层查同样字高的文字
            for (int i = 0; i < _cmbLayer.Items.Count; i++)
            {
                ComboBoxItem item = _cmbLayer.Items[i] as ComboBoxItem;
                if (item != null && string.Equals(item.Tag as string, s.LayerName, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbLayer.SelectedIndex = i;
                    break;
                }
            }
        }

        private TextFilterCriteria BuildCriteriaFromUi()
        {
            TextFilterCriteria c = new TextFilterCriteria();

            c.IncludeDBText = _chkIncludeDBText.IsChecked == true;
            c.IncludeMText = _chkIncludeMText.IsChecked == true;
            c.IncludeAttribute = _chkIncludeAttrib.IsChecked == true;

            // 字高
            c.FilterByHeight = _chkHeight.IsChecked == true;
            c.HeightOperator = (HeightCompareOperator)_cmbHeightOp.SelectedIndex;
            double hVal;
            if (double.TryParse(_txtHeightValue.Text, out hVal)) c.HeightValue = hVal;
            double hMax;
            if (double.TryParse(_txtHeightMax.Text, out hMax)) c.HeightMax = hMax;
            double tol;
            if (double.TryParse(_txtHeightTol.Text, out tol)) c.HeightTolerance = tol;

            // 样式与字体
            c.FilterByStyle = _chkStyle.IsChecked == true;
            if (_cmbTextStyle.SelectedItem is ComboBoxItem)
            {
                c.TextStyleName = (_cmbTextStyle.SelectedItem as ComboBoxItem).Tag as string;
            }
            c.FilterByFont = _chkFont.IsChecked == true;
            c.FontName = _txtFontName.Text.Trim();

            // 颜色
            c.FilterByColor = _chkColor.IsChecked == true;
            if (_cmbColor.SelectedItem is ComboBoxItem && (_cmbColor.SelectedItem as ComboBoxItem).Tag is short)
            {
                c.ColorIndex = (short)(_cmbColor.SelectedItem as ComboBoxItem).Tag;
            }
            c.ResolveByLayerEffectiveColor = _chkResolveByLayer.IsChecked == true;

            // 图层
            c.FilterByLayer = _chkLayer.IsChecked == true;
            if (_cmbLayer.SelectedItem is ComboBoxItem)
            {
                c.LayerName = (_cmbLayer.SelectedItem as ComboBoxItem).Tag as string;
            }

            // 内容
            c.FilterByContent = _chkContent.IsChecked == true;
            c.ContentPattern = _txtContentPattern.Text;
            c.ContentMatchType = (ContentMatchType)_cmbContentMatchType.SelectedIndex;
            c.CaseSensitive = _chkCaseSensitive.IsChecked == true;

            return c;
        }

        private List<ObjectId> ExecuteFilterWithScope(out string scopeDesc)
        {
            scopeDesc = "当前空间全图";
            TextFilterCriteria criteria = BuildCriteriaFromUi();

            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return new List<ObjectId>();

            IEnumerable<ObjectId> scopeEntities = null;
            if (_radScopeWindow.IsChecked == true)
            {
                // 手动框选
                _window.Visibility = Visibility.Hidden;
                try
                {
                    PromptSelectionOptions pso = new PromptSelectionOptions();
                    pso.MessageForAdding = "\n[XY-Toolbox] 请在图纸中框选查找范围: ";
                    PromptSelectionResult psr = doc.Editor.GetSelection(pso);
                    if (psr.Status == PromptStatus.OK && psr.Value != null)
                    {
                        scopeEntities = psr.Value.GetObjectIds();
                        scopeDesc = string.Format("框选范围 (包含 {0} 个对象)", scopeEntities != null ? psr.Value.Count : 0);
                    }
                    else
                    {
                        doc.Editor.WriteMessage("\n已取消框选。");
                        return new List<ObjectId>();
                    }
                }
                finally
                {
                    _window.Visibility = Visibility.Visible;
                    _window.Activate();
                }
            }

            List<ObjectId> results = new List<ObjectId>();
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                results = TextFilterService.FilterTexts(doc.Database, tr, criteria, scopeEntities);
                tr.Commit();
            }

            return results;
        }

        /// <summary>
        /// 立即选择到图面 (带夹点)
        /// </summary>
        private void BtnSelectToCad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string scopeDesc;
                List<ObjectId> matched = ExecuteFilterWithScope(out scopeDesc);
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                if (matched.Count == 0)
                {
                    MessageBox.Show("在 " + scopeDesc + " 未找到任何匹配的文字对象！\n请检查过滤条件 (如字高容差、颜色或图层)。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                doc.Editor.SetImpliedSelection(matched.ToArray());
                doc.Editor.WriteMessage(string.Format("\n[XY-Toolbox] 成功选中 {0} 个匹配文字！已置入 CAD 夹点选择集，可直接进行移动、删除或按 Ctrl+1 查看属性。\n", matched.Count));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("选择失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 仅隔离显示文字 (Isolate)
        /// </summary>
        private void BtnIsolate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string scopeDesc;
                List<ObjectId> matched = ExecuteFilterWithScope(out scopeDesc);
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                if (matched.Count == 0)
                {
                    MessageBox.Show("未找到匹配文字，无法执行隔离。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 选中并调用 CAD 内置 ISOLATEOBJECTS
                doc.Editor.SetImpliedSelection(matched.ToArray());
                doc.SendStringToExecute("_.ISOLATEOBJECTS ", true, false, false);
                doc.Editor.WriteMessage(string.Format("\n[XY-Toolbox] 已隔离显示 {0} 个匹配文字！(输入 UNISOLATEOBJECTS 可恢复全部显示)\n", matched.Count));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("隔离文字失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 批量修改属性
        /// </summary>
        private void BtnExecuteBatchModify_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double? newHeight = null;
                if (_chkBatchHeight.IsChecked == true)
                {
                    double h;
                    if (double.TryParse(_txtBatchHeight.Text, out h) && h > 0) newHeight = h;
                }

                string newStyle = null;
                if (_chkBatchStyle.IsChecked == true && _cmbBatchStyle.SelectedItem is ComboBoxItem)
                {
                    newStyle = (_cmbBatchStyle.SelectedItem as ComboBoxItem).Tag as string;
                }

                short? newColor = null;
                if (_chkBatchColor.IsChecked == true && _cmbBatchColor.SelectedItem is ComboBoxItem)
                {
                    newColor = (short)(_cmbBatchColor.SelectedItem as ComboBoxItem).Tag;
                }

                string newLayer = null;
                if (_chkBatchLayer.IsChecked == true && _cmbBatchLayer.SelectedItem is ComboBoxItem)
                {
                    newLayer = (_cmbBatchLayer.SelectedItem as ComboBoxItem).Tag as string;
                }

                if (!newHeight.HasValue && string.IsNullOrEmpty(newStyle) && !newColor.HasValue && string.IsNullOrEmpty(newLayer))
                {
                    MessageBox.Show("请至少勾选并指定一项需要批量修改的属性 (字高 / 样式 / 颜色 / 图层)！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string scopeDesc;
                List<ObjectId> matched = ExecuteFilterWithScope(out scopeDesc);
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                if (matched.Count == 0)
                {
                    MessageBox.Show("未检索到符合条件的文字，无法执行批量修改。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string confirmMsg = string.Format("共找到 {0} 个符合条件的文字。\n确定要对这 {0} 个文字执行批量属性修改吗？\n\n- 新字高: {1}\n- 新样式: {2}\n- 新颜色: {3}\n- 新图层: {4}",
                    matched.Count,
                    newHeight.HasValue ? newHeight.Value.ToString() : "(不修改)",
                    !string.IsNullOrEmpty(newStyle) ? newStyle : "(不修改)",
                    newColor.HasValue ? TextFilterService.GetAciColorName(newColor.Value) : "(不修改)",
                    !string.IsNullOrEmpty(newLayer) ? newLayer : "(不修改)");

                if (MessageBox.Show(confirmMsg, "确认批量修改", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                int modifiedCount = 0;
                using (DocumentLock loc = doc.LockDocument())
                {
                    modifiedCount = TextFilterService.BatchModifyTexts(doc.Database, matched, newHeight, newStyle, newColor, newLayer);
                }

                doc.Editor.SetImpliedSelection(matched.ToArray());
                doc.Editor.Regen();
                doc.Editor.WriteMessage(string.Format("\n[XY-Toolbox] 成功批量修改 {0} 个文字的属性！已刷新图面并保持选中。\n", modifiedCount));
                MessageBox.Show(string.Format("成功批量修改 {0} 个文字！\n图面已自动更新并保持选中。", modifiedCount), "批量修改完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("批量修改失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
