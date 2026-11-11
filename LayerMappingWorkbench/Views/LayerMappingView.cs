using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using Autodesk.AutoCAD.DatabaseServices;
using LayerMappingWorkbench.ViewModels;

namespace LayerMappingWorkbench.Views
{
    /// <summary>
    /// 双栏图层智能对位工作台独立 WPF 窗体载体 (工业级高对比度深色工程美学，彻底根除系统Aero白条)
    /// </summary>
    public static class LayerMappingViewWindow
    {
        private static readonly string XamlTemplate = 
@"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""AutoCAD 施工图图层智能对位工作台 (XY-Toolbox)""
        Width=""1160"" Height=""720"" MinWidth=""960"" MinHeight=""560""
        WindowStartupLocation=""CenterScreen""
        KeyboardNavigation.DirectionalNavigation=""None""
        KeyboardNavigation.TabNavigation=""Local""
        Background=""#1E222A"" Foreground=""#FFFFFF""
        FontFamily=""Segoe UI, Microsoft YaHei, 微软雅黑"" FontSize=""12"">

    <Window.Resources>
        <!-- 全局高对比度按钮样式 -->
        <Style TargetType=""Button"">
            <Setter Property=""Background"" Value=""#2E3440""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""BorderBrush"" Value=""#4C566A""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Padding"" Value=""12,5""/>
            <Setter Property=""Margin"" Value=""3,0""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
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

        <!-- 全局高对比度输入框样式 (纯黑底色 + 纯白文字) -->
        <Style TargetType=""TextBox"">
            <Setter Property=""Background"" Value=""#16191F""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""BorderBrush"" Value=""#5E6A82""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Padding"" Value=""6,4""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
            <Setter Property=""CaretBrush"" Value=""#FFFFFF""/>
            <Setter Property=""SelectionBrush"" Value=""#88C0D0""/>
            <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
        </Style>

        <!-- 核心武器 1: 纯深色可编辑 ComboBox 模板 (彻底消灭系统 Aero2 默认白色 ToggleButton 白条！) -->
        <ControlTemplate x:Key=""DarkEditableComboBoxTemplate"" TargetType=""{x:Type ComboBox}"">
            <Grid x:Name=""templateRoot"" SnapsToDevicePixels=""True"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition MinWidth=""24"" Width=""24""/>
                </Grid.ColumnDefinitions>

                <!-- 1. 深度纯黑底色主边框 (100% 覆盖，彻底消除任何系统白条！) -->
                <Border x:Name=""mainBorder"" Grid.ColumnSpan=""2""
                        Background=""#16191F""
                        BorderBrush=""#5E6A82""
                        BorderThickness=""1""
                        CornerRadius=""3""/>

                <!-- 2. 可编辑文本输入框 (透明背景透出黑底，文字高亮加粗白，绝不泛白) -->
                <TextBox x:Name=""PART_EditableTextBox"" Grid.Column=""0""
                         Background=""Transparent""
                         Foreground=""#FFFFFF""
                         FontWeight=""Bold""
                         FontSize=""12""
                         BorderThickness=""0""
                         Padding=""6,3""
                         VerticalContentAlignment=""Center""
                         CaretBrush=""#FFFFFF""
                         SelectionBrush=""#88C0D0""/>

                <!-- 3. 下拉箭头按钮 (透明背景 + 极光蓝三角形) -->
                <ToggleButton Grid.Column=""1"" Background=""Transparent"" BorderThickness=""0"" Focusable=""False""
                              IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"">
                    <Path Data=""M 0 0 L 4 4 L 8 0 Z"" Fill=""#88C0D0"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                </ToggleButton>

                <!-- 4. 下拉弹出列表 (纯黑底色 + 纯白高对比文字) -->
                <Popup x:Name=""PART_Popup"" AllowsTransparency=""True"" Grid.ColumnSpan=""2""
                       IsOpen=""{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}""
                       Placement=""Bottom"" PopupAnimation=""Slide"">
                    <Border Background=""#242933"" BorderBrush=""#5E6A82"" BorderThickness=""1"" CornerRadius=""3"" MaxHeight=""260"" MinWidth=""{Binding ActualWidth, ElementName=templateRoot}"">
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

        <!-- 核心武器 2: 纯深色普通只读 ComboBox 模板 (用于右上角状态过滤) -->
        <ControlTemplate x:Key=""DarkComboBoxTemplate"" TargetType=""{x:Type ComboBox}"">
            <Grid x:Name=""templateRoot"" SnapsToDevicePixels=""True"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition MinWidth=""22"" Width=""22""/>
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
                                  VerticalAlignment=""Center""
                                  Margin=""8,0,0,0""/>

                <ToggleButton Grid.Column=""1"" Background=""Transparent"" BorderThickness=""0"" Focusable=""False""
                              IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"">
                    <Path Data=""M 0 0 L 4 4 L 8 0 Z"" Fill=""#88C0D0"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                </ToggleButton>

                <Popup x:Name=""PART_Popup"" AllowsTransparency=""True"" Grid.ColumnSpan=""2""
                       IsOpen=""{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}""
                       Placement=""Bottom"" PopupAnimation=""Slide"">
                    <Border Background=""#242933"" BorderBrush=""#5E6A82"" BorderThickness=""1"" CornerRadius=""3"" MaxHeight=""240"" MinWidth=""{Binding ActualWidth, ElementName=templateRoot}"">
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
            <Setter Property=""Background"" Value=""#242933""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""Padding"" Value=""8,5""/>
            <Setter Property=""FontWeight"" Value=""SemiBold""/>
            <Style.Triggers>
                <Trigger Property=""IsHighlighted"" Value=""True"">
                    <Setter Property=""Background"" Value=""#3B4252""/>
                    <Setter Property=""Foreground"" Value=""#88C0D0""/>
                </Trigger>
                <Trigger Property=""IsSelected"" Value=""True"">
                    <Setter Property=""Background"" Value=""#434C5E""/>
                    <Setter Property=""Foreground"" Value=""#A3BE8C""/>
                </Trigger>
            </Style.Triggers>
        </Style>

        <Style TargetType=""CheckBox"">
            <Setter Property=""Foreground"" Value=""#ECEFF4""/>
            <Setter Property=""Margin"" Value=""0,0,16,0""/>
            <Setter Property=""VerticalAlignment"" Value=""Center""/>
            <Setter Property=""FontWeight"" Value=""Normal""/>
        </Style>

        <!-- 主表格样式 -->
        <Style TargetType=""DataGrid"">
            <Setter Property=""Background"" Value=""#1E222A""/>
            <Setter Property=""BorderBrush"" Value=""#3B4252""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""RowBackground"" Value=""#222630""/>
            <Setter Property=""AlternatingRowBackground"" Value=""#282D39""/>
            <Setter Property=""GridLinesVisibility"" Value=""Horizontal""/>
            <Setter Property=""HorizontalGridLinesBrush"" Value=""#323744""/>
            <Setter Property=""HeadersVisibility"" Value=""Column""/>
            <Setter Property=""SelectionMode"" Value=""Extended""/>
            <Setter Property=""CanUserResizeRows"" Value=""False""/>
        </Style>

        <Style TargetType=""DataGridColumnHeader"">
            <Setter Property=""Background"" Value=""#2E3440""/>
            <Setter Property=""Foreground"" Value=""#88C0D0""/>
            <Setter Property=""FontWeight"" Value=""Bold""/>
            <Setter Property=""Padding"" Value=""8,9""/>
            <Setter Property=""BorderBrush"" Value=""#3B4252""/>
            <Setter Property=""BorderThickness"" Value=""0,0,1,1""/>
        </Style>

        <Style TargetType=""DataGridCell"">
            <Setter Property=""Padding"" Value=""6,4""/>
            <Setter Property=""BorderThickness"" Value=""0""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""Template"">
                <Setter.Value>
                    <ControlTemplate TargetType=""DataGridCell"">
                        <Border Padding=""{TemplateBinding Padding}"" Background=""{TemplateBinding Background}"">
                            <ContentPresenter VerticalAlignment=""Center""/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Grid Margin=""12"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""Auto""/>
            <RowDefinition Height=""*""/>
            <RowDefinition Height=""Auto""/>
            <RowDefinition Height=""Auto""/>
        </Grid.RowDefinitions>

        <!-- 顶部工具栏与筛选器 -->
        <Border Grid.Row=""0"" Background=""#242933"" Padding=""10,8"" CornerRadius=""6"" Margin=""0,0,0,10"">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""Auto""/>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>

                <!-- 左侧按钮簇 -->
                <StackPanel Grid.Column=""0"" Orientation=""Horizontal"">
                    <Button Content=""📂 选择目标标准DWG"" Command=""{Binding LoadTargetDwgCommand}"" Background=""#3B4252""/>
                    <Button Content=""🎯 CAD图面拾取图元"" Command=""{Binding PickEntityCommand}"" Background=""#434C5E""/>
                    <Button Content=""📥 导入CSV规则"" Command=""{Binding ImportCsvCommand}""/>
                    <Button Content=""📤 导出CSV规则"" Command=""{Binding ExportCsvCommand}""/>
                    <Button Content=""🔄 刷新图层"" Command=""{Binding RefreshCommand}""/>
                </StackPanel>

                <!-- 中间搜索框 (纯黑底白字高亮) -->
                <StackPanel Grid.Column=""1"" Orientation=""Horizontal"" HorizontalAlignment=""Center"">
                    <TextBlock Text=""🔍 搜索图层:"" VerticalAlignment=""Center"" Foreground=""#ECEFF4"" FontWeight=""SemiBold"" Margin=""0,0,6,0""/>
                    <TextBox Width=""220"" Text=""{Binding SearchKeyword, UpdateSourceTrigger=PropertyChanged}""
                             Background=""#16191F"" Foreground=""#FFFFFF"" BorderBrush=""#5E6A82""/>
                </StackPanel>

                <!-- 右侧对齐状态过滤下拉框 (应用纯深色模板，绝不泛白) -->
                <StackPanel Grid.Column=""2"" Orientation=""Horizontal"">
                    <TextBlock Text=""状态过滤:"" VerticalAlignment=""Center"" Foreground=""#ECEFF4"" FontWeight=""SemiBold"" Margin=""0,0,6,0""/>
                    <ComboBox Width=""120"" SelectedIndex=""{Binding FilterStatusIndex, UpdateSourceTrigger=PropertyChanged}""
                              Template=""{StaticResource DarkComboBoxTemplate}""
                              Foreground=""#FFFFFF"" FontWeight=""Bold"">
                        <ComboBoxItem Content="" 全部图层""/>
                        <ComboBoxItem Content="" 待映射项""/>
                        <ComboBoxItem Content="" 智能推测""/>
                        <ComboBoxItem Content="" 精准锁定""/>
                    </ComboBox>
                </StackPanel>
            </Grid>
        </Border>

        <!-- 中间双栏对位核心 DataGrid -->
        <DataGrid Grid.Row=""1"" x:Name=""GridMapping"" AutoGenerateColumns=""False"" CanUserAddRows=""False""
                  ItemsSource=""{Binding FilteredView}""
                  KeyboardNavigation.DirectionalNavigation=""None""
                  KeyboardNavigation.TabNavigation=""Local""
                  VirtualizingPanel.IsVirtualizing=""True""
                  VirtualizingPanel.VirtualizationMode=""Recycling"">
            <DataGrid.Columns>
                <!-- 勾选列 -->
                <DataGridCheckBoxColumn Binding=""{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}"" Width=""35""/>

                <!-- 匹配指示状态 -->
                <DataGridTemplateColumn Header=""状态"" Width=""95"">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <Border Background=""{Binding StatusColorBrush}"" CornerRadius=""3"" Padding=""6,2"" HorizontalAlignment=""Center"">
                                <TextBlock Text=""{Binding StatusText}"" Foreground=""#FFFFFF"" FontSize=""11"" FontWeight=""Bold""/>
                            </Border>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <!-- 公司一：源图层属性展示 (纯白文字 + 字体加粗) -->
                <DataGridTextColumn Header=""公司一图层 (源)"" Binding=""{Binding SourceLayerName}"" IsReadOnly=""True"" Width=""200"">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
                            <Setter Property=""FontWeight"" Value=""SemiBold""/>
                            <Setter Property=""VerticalAlignment"" Value=""Center""/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>
                
                <DataGridTemplateColumn Header=""颜色"" Width=""55"">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <Rectangle Width=""24"" Height=""14"" Stroke=""#1E222A"" StrokeThickness=""1""
                                       Fill=""{Binding SourceColorBrush}""
                                       ToolTip=""源图层RGB颜色""/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <DataGridTextColumn Header=""线型"" Binding=""{Binding SourceLinetype}"" IsReadOnly=""True"" Width=""95"">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""Foreground"" Value=""#D8DEE9""/>
                            <Setter Property=""VerticalAlignment"" Value=""Center""/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>

                <DataGridTextColumn Header=""图元数"" Binding=""{Binding EntityCount}"" IsReadOnly=""True"" Width=""65"">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""Foreground"" Value=""#88C0D0""/>
                            <Setter Property=""FontWeight"" Value=""Bold""/>
                            <Setter Property=""VerticalAlignment"" Value=""Center""/>
                            <Setter Property=""HorizontalAlignment"" Value=""Center""/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>

                <!-- 转换导向标 -->
                <DataGridTemplateColumn Header=""映射"" Width=""45"">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <TextBlock Text=""➔"" Foreground=""#81A1C1"" HorizontalAlignment=""Center"" FontWeight=""Bold"" FontSize=""14""/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <!-- 公司二：目标标准设置（应用自包含深色模板，彻底消灭系统 Aero 白条！） -->
                <DataGridTemplateColumn Header=""公司二对标图层 (目标标准)"" Width=""*"">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <ComboBox ItemsSource=""{Binding AvailableTargetLayers}"" 
                                      Text=""{Binding SelectedTargetLayerName, UpdateSourceTrigger=PropertyChanged}""
                                      IsEditable=""True""
                                      Template=""{StaticResource DarkEditableComboBoxTemplate}""
                                      Foreground=""#FFFFFF""
                                      FontWeight=""Bold""/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <DataGridTemplateColumn Header=""目标色"" Width=""55"">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <Rectangle Width=""24"" Height=""14"" Stroke=""#1E222A"" StrokeThickness=""1""
                                       Fill=""{Binding TargetColorBrush}""
                                       ToolTip=""目标标准层颜色""/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <DataGridTextColumn Header=""目标线型"" Binding=""{Binding TargetLinetype}"" IsReadOnly=""True"" Width=""95"">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""Foreground"" Value=""#D8DEE9""/>
                            <Setter Property=""VerticalAlignment"" Value=""Center""/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>

                <DataGridTextColumn Header=""匹配置信度/理由"" Binding=""{Binding MatchNote}"" IsReadOnly=""True"" Width=""190"">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""Foreground"" Value=""#EBCB8B""/>
                            <Setter Property=""VerticalAlignment"" Value=""Center""/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>
            </DataGrid.Columns>
        </DataGrid>

        <!-- 选项配置栏 -->
        <Border Grid.Row=""2"" Background=""#242933"" Padding=""10,8"" CornerRadius=""6"" Margin=""0,8,0,8"">
            <StackPanel Orientation=""Horizontal"">
                <CheckBox Content=""重置图元为 ByLayer (颜色/线型/线宽)"" IsChecked=""{Binding ForceByLayer}"" ToolTip=""勾选后穿透图元属性统一重设为随层""/>
                <CheckBox Content=""保护图块内 0 层图元不重定向"" IsChecked=""{Binding KeepZeroLayer}"" ToolTip=""避免通用家具/门窗块失去随层继承特性""/>
                <CheckBox Content=""合并后自动清理空出的源图层 (Purge)"" IsChecked=""{Binding PurgeEmptySource}"" ToolTip=""自动安全删除已无任何图元的源图层""/>
            </StackPanel>
        </Border>

        <!-- 底部状态汇总与操作确认栏 -->
        <Grid Grid.Row=""3"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""*""/>
                <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>

            <!-- 状态汇总 (清爽青蓝色高亮) -->
            <TextBlock Grid.Column=""0"" Text=""{Binding StatusSummary}"" 
                       VerticalAlignment=""Center"" Foreground=""#88C0D0"" FontSize=""12"" FontWeight=""Bold""/>

            <!-- 核心执行按钮 -->
            <StackPanel Grid.Column=""1"" Orientation=""Horizontal"">
                <Button Content=""⚡ 执行图层深度合并 (穿透图块)"" Command=""{Binding ExecuteMergeCommand}""
                        Background=""#A3BE8C"" Foreground=""#1E222A"" FontWeight=""Bold"" FontSize=""13"" Padding=""20,8""/>
            </StackPanel>
        </Grid>
    </Grid>
</Window>";

        public static Window CreateView(LayerMappingViewModel viewModel)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(XamlTemplate);
            Window win;
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                win = (Window)XamlReader.Load(ms);
            }

            win.DataContext = viewModel;
            return win;
        }

        public static void ShowWorkbench(Database db)
        {
            try
            {
                LayerMappingViewModel vm = new LayerMappingViewModel(db);
                Window win = CreateView(vm);

                // 绑定 AutoCAD 主窗体 HWND 保证模态居中置顶
                try
                {
                    IntPtr acadHwnd = Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle;
                    new System.Windows.Interop.WindowInteropHelper(win).Owner = acadHwnd;
                    win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                catch { }

                // 优先使用纯净 WPF ShowDialog()，杜绝 AutoCAD 内部 AcWindows 主题注入冲突
                try
                {
                    win.ShowDialog();
                }
                catch
                {
                    Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(win);
                }
            }
            catch (System.Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += "\n[内部细节] " + ex.InnerException.Message;
                System.Windows.MessageBox.Show("呼出图层智能对位工作台失败: " + msg, "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
