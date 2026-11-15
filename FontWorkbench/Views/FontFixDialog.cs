using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using FontWorkbench.ViewModels;

namespace FontWorkbench.Views
{
    public class FontFixDialog
    {
        private static readonly string DialogXaml =
@"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""🔤 图纸缺失字体诊断与极速修复工作台 (XY-Toolbox)""
        Width=""960"" Height=""650"" MinWidth=""820"" MinHeight=""520""
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
            <Setter Property=""Padding"" Value=""12,6""/>
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

        <!-- 输入框样式 -->
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
                    <ColumnDefinition MinWidth=""22"" Width=""22""/>
                </Grid.ColumnDefinitions>

                <Border x:Name=""mainBorder"" Grid.ColumnSpan=""2""
                        Background=""#16191F""
                        BorderBrush=""#5E6A82""
                        BorderThickness=""1""
                        CornerRadius=""3""/>

                <TextBox x:Name=""PART_EditableTextBox"" Grid.Column=""0""
                         Background=""Transparent""
                         Foreground=""#FFFFFF""
                         FontWeight=""Bold""
                         FontSize=""11.5""
                         BorderThickness=""0""
                         Padding=""5,2""
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
                <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
                    <Setter TargetName=""mainBorder"" Property=""BorderBrush"" Value=""#81A1C1""/>
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>

        <Style TargetType=""ComboBoxItem"">
            <Setter Property=""Background"" Value=""#242933""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""Padding"" Value=""8,4""/>
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
            <Setter Property=""Padding"" Value=""8,8""/>
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

    <Grid Margin=""14"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""Auto""/> <!-- 0: 顶部标题与状态卡片 -->
            <RowDefinition Height=""Auto""/> <!-- 1: 筛选与检索工具栏 -->
            <RowDefinition Height=""*""/>    <!-- 2: 数据表格 -->
            <RowDefinition Height=""Auto""/> <!-- 3: 底部自动化选项 -->
            <RowDefinition Height=""Auto""/> <!-- 4: 底部操作按钮栏 -->
        </Grid.RowDefinitions>

        <!-- 0: 顶部标题与状态卡片 -->
        <Border Grid.Row=""0"" Background=""#242933"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""6"" Padding=""14,10"" Margin=""0,0,0,10"">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column=""0"">
                    <TextBlock Text=""🔤 图纸缺失字体诊断与极速修复工作台"" FontSize=""15"" FontWeight=""Bold"" Foreground=""#88C0D0""/>
                    <TextBlock Text=""{Binding SummaryText}"" FontSize=""12"" Foreground=""#E5E9F0"" Margin=""0,4,0,2""/>
                    <TextBlock Text=""{Binding RecommendationText}"" FontSize=""11.5"" Foreground=""#A3BE8C""/>
                </StackPanel>

                <Button Grid.Column=""1"" Content=""🔄 重新诊断"" Command=""{Binding RescanCommand}""
                        Padding=""12,6"" Margin=""8,0,0,0"" VerticalAlignment=""Center"" FontWeight=""SemiBold""/>
            </Grid>
        </Border>

        <!-- 1: 筛选与检索工具栏 -->
        <Grid Grid.Row=""1"" Margin=""0,0,0,8"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""Auto""/>
                <ColumnDefinition Width=""Auto""/>
                <ColumnDefinition Width=""*""/>
                <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>

            <CheckBox Grid.Column=""0"" Content=""全选 / 全不选"" IsChecked=""{Binding SelectAll, Mode=TwoWay}""
                      FontWeight=""SemiBold"" Foreground=""#88C0D0"" VerticalAlignment=""Center"" Margin=""4,0,18,0""/>

            <CheckBox Grid.Column=""1"" Content=""仅显示存在缺失/异常的样式"" IsChecked=""{Binding OnlyShowIssues, Mode=TwoWay}""
                      FontWeight=""SemiBold"" Foreground=""#EBCB8B"" VerticalAlignment=""Center"" Margin=""0,0,18,0""/>

            <StackPanel Grid.Column=""3"" Orientation=""Horizontal"" HorizontalAlignment=""Right"">
                <TextBlock Text=""🔍 搜索样式/字体:"" VerticalAlignment=""Center"" Foreground=""#D8DEE9"" Margin=""0,0,6,0""/>
                <TextBox Width=""200"" Text=""{Binding SearchKeyword, UpdateSourceTrigger=PropertyChanged}"" Height=""26""/>
            </StackPanel>
        </Grid>

        <!-- 2: 数据表格 -->
        <DataGrid Grid.Row=""2"" AutoGenerateColumns=""False"" CanUserAddRows=""False""
                  ItemsSource=""{Binding FilteredView}""
                  KeyboardNavigation.DirectionalNavigation=""None""
                  KeyboardNavigation.TabNavigation=""Local""
                  VirtualizingPanel.IsVirtualizing=""True""
                  VirtualizingPanel.VirtualizationMode=""Recycling"">
            <DataGrid.Columns>
                <!-- 勾选列 -->
                <DataGridCheckBoxColumn Binding=""{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}"" Width=""38""/>

                <!-- 样式名称 -->
                <DataGridTextColumn Header=""样式名称"" Binding=""{Binding StyleName}"" IsReadOnly=""True"" Width=""120"">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""FontWeight"" Value=""Bold""/>
                            <Setter Property=""Foreground"" Value=""#88C0D0""/>
                            <Setter Property=""VerticalAlignment"" Value=""Center""/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>

                <!-- 当前字体配置 -->
                <DataGridTextColumn Header=""当前字体配置 (主字体 + 大字体)"" Binding=""{Binding DisplaySummary}"" IsReadOnly=""True"" Width=""180"">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""Foreground"" Value=""#E5E9F0""/>
                            <Setter Property=""VerticalAlignment"" Value=""Center""/>
                            <Setter Property=""TextTrimming"" Value=""CharacterEllipsis""/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>

                <!-- 诊断情况 -->
                <DataGridTextColumn Header=""诊断情况 (缺失识别)"" Binding=""{Binding IssueDescription}"" IsReadOnly=""True"" Width=""*"">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""Foreground"" Value=""#EBCB8B""/>
                            <Setter Property=""VerticalAlignment"" Value=""Center""/>
                            <Setter Property=""FontWeight"" Value=""SemiBold""/>
                            <Setter Property=""TextTrimming"" Value=""CharacterEllipsis""/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>

                <!-- 影响文字数 -->
                <DataGridTextColumn Header=""文字数量"" Binding=""{Binding AffectedEntityCount}"" IsReadOnly=""True"" Width=""75"">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""HorizontalAlignment"" Value=""Center""/>
                            <Setter Property=""VerticalAlignment"" Value=""Center""/>
                            <Setter Property=""FontWeight"" Value=""Bold""/>
                            <Setter Property=""Foreground"" Value=""#D8DEE9""/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>

                <!-- 替换大字体 -->
                <DataGridTemplateColumn Header=""替换大字体 (中文核心)"" Width=""165"">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <ComboBox IsEditable=""True""
                                      Text=""{Binding TargetBigFont, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}""
                                      ItemsSource=""{Binding DataContext.AvailableBigFonts, RelativeSource={RelativeSource AncestorType=Window}}""
                                      Template=""{StaticResource DarkEditableComboBoxTemplate}""
                                      Height=""26"" Margin=""2,0""/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <!-- 替换西文字体 -->
                <DataGridTemplateColumn Header=""替换西文字体"" Width=""145"">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <ComboBox IsEditable=""True""
                                      Text=""{Binding TargetPrimaryFont, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}""
                                      ItemsSource=""{Binding DataContext.AvailableWesternFonts, RelativeSource={RelativeSource AncestorType=Window}}""
                                      Template=""{StaticResource DarkEditableComboBoxTemplate}""
                                      Height=""26"" Margin=""2,0""/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <!-- 处理状态 -->
                <DataGridTextColumn Header=""状态"" Binding=""{Binding Status}"" IsReadOnly=""True"" Width=""65"">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType=""TextBlock"">
                            <Setter Property=""HorizontalAlignment"" Value=""Center""/>
                            <Setter Property=""VerticalAlignment"" Value=""Center""/>
                            <Setter Property=""FontWeight"" Value=""SemiBold""/>
                            <Setter Property=""Foreground"" Value=""#A3BE8C""/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>
            </DataGrid.Columns>
        </DataGrid>

        <!-- 3: 底部自动化选项 -->
        <Border Grid.Row=""3"" Background=""#242933"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""4"" Padding=""10,8"" Margin=""0,10,0,10"">
            <StackPanel Orientation=""Horizontal"">
                <CheckBox Content=""同步写入本机 acad.fmp 映射表 (永久生效，以后打开任何同类图纸自动映射)""
                          IsChecked=""{Binding UpdateAcadFmp, Mode=TwoWay}""
                          FontWeight=""SemiBold"" Foreground=""#A3BE8C"" Margin=""6,0,24,0""/>

                <CheckBox Content=""智能清除多行文字(MText)行内硬编码字体覆盖 (\f...; 格式代码)""
                          IsChecked=""{Binding CleanMTextOverrides, Mode=TwoWay}""
                          FontWeight=""SemiBold"" Foreground=""#88C0D0"" Margin=""0,0,24,0""/>

                <CheckBox Content=""自动全图重生成 (REGEN)""
                          IsChecked=""{Binding AutoRegen, Mode=TwoWay}""
                          FontWeight=""SemiBold"" Foreground=""#D8DEE9""/>
            </StackPanel>
        </Border>

        <!-- 4: 底部操作按钮栏 -->
        <Grid Grid.Row=""4"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""*""/>
                <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>

            <TextBlock Grid.Column=""0"" VerticalAlignment=""Center"" Foreground=""#7B88A1"" FontSize=""11""
                       Text=""提示: 修复后将即时更新图纸文字样式表。若勾选 acad.fmp，下次打开图纸时将免受缺失字体弹窗打扰。""/>

            <StackPanel Grid.Column=""1"" Orientation=""Horizontal"">
                <Button Content=""⚡ 一键智能修复全部缺失"" Command=""{Binding QuickFixAllCommand}""
                        Background=""#2D7D46"" BorderBrush=""#3CA35D"" Foreground=""#FFFFFF""
                        FontWeight=""Bold"" FontSize=""13"" Padding=""16,7"" Margin=""0,0,10,0""/>

                <Button Content=""修复选中项"" Command=""{Binding FixSelectedCommand}""
                        Background=""#0078D7"" BorderBrush=""#2B88D8"" Foreground=""#FFFFFF""
                        FontWeight=""SemiBold"" Padding=""14,7"" Margin=""0,0,10,0""/>

                <Button Content=""关闭"" x:Name=""BtnClose""
                        Background=""#2E3440"" BorderBrush=""#5E6A82""
                        Padding=""14,7""/>
            </StackPanel>
        </Grid>
    </Grid>
</Window>";

        public static void ShowDialog(Database db)
        {
            if (db == null) return;

            try
            {
                StringReader sr = new StringReader(DialogXaml);
                Window win = XamlReader.Parse(sr.ReadToEnd()) as Window;
                if (win == null) return;

                FontFixViewModel vm = new FontFixViewModel(db);
                win.DataContext = vm;

                Button btnClose = win.FindName("BtnClose") as Button;
                if (btnClose != null)
                {
                    btnClose.Click += delegate { win.Close(); };
                }

                vm.RequestClose += delegate { win.Close(); };

                Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(win);
            }
            catch (Exception ex)
            {
                Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog(
                    "启动字体修复工作台失败: " + ex.Message + "\n" + ex.StackTrace);
            }
        }
    }
}
