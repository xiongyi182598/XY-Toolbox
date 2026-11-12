using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LayerMappingWorkbench.Models;
using LayerMappingWorkbench.Services;

namespace LayerMappingWorkbench.Views
{
    public class ViewportCloneDialog
    {
        private static readonly string DialogXaml =
@"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""外部 DWG 视口无损移植引擎 (XY-Toolbox)""
        Width=""640"" Height=""640"" MinWidth=""560"" MinHeight=""560""
        WindowStartupLocation=""CenterScreen""
        Background=""#1E222A"" Foreground=""#ECEFF4""
        FontFamily=""Segoe UI, Microsoft YaHei, 微软雅黑"" FontSize=""12""
        ResizeMode=""NoResize""
        KeyboardNavigation.DirectionalNavigation=""None""
        KeyboardNavigation.TabNavigation=""Local"">

    <Window.Resources>
        <Style TargetType=""Button"">
            <Setter Property=""Background"" Value=""#2E3440""/>
            <Setter Property=""Foreground"" Value=""#ECEFF4""/>
            <Setter Property=""BorderBrush"" Value=""#4C566A""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Padding"" Value=""12,5""/>
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

        <!-- 核心武器 1: 纯深色可编辑 ComboBox 模板 (彻底消灭系统 Aero2 默认白色 ToggleButton 白条！) -->
        <ControlTemplate x:Key=""DarkEditableComboBoxTemplate"" TargetType=""{x:Type ComboBox}"">
            <Grid x:Name=""templateRoot"" SnapsToDevicePixels=""True"" TextElement.Foreground=""#FFFFFF"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition MinWidth=""24"" Width=""24""/>
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
                         FontSize=""12""
                         BorderThickness=""0""
                         Padding=""6,3""
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

        <!-- 核心武器 2: 纯深色普通只读 ComboBox 模板 (用于布局选择) -->
        <ControlTemplate x:Key=""DarkComboBoxTemplate"" TargetType=""{x:Type ComboBox}"">
            <Grid x:Name=""templateRoot"" SnapsToDevicePixels=""True"" TextElement.Foreground=""#FFFFFF"">
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

        <Style TargetType=""ComboBox"">
            <Setter Property=""Background"" Value=""#16191F""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""BorderBrush"" Value=""#5E6A82""/>
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

        <Style TargetType=""RadioButton"">
            <Setter Property=""Foreground"" Value=""#ECEFF4""/>
            <Setter Property=""Margin"" Value=""0,3,0,3""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
        </Style>
    </Window.Resources>

    <Grid Margin=""16"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""Auto""/>
            <RowDefinition Height=""*""/>
            <RowDefinition Height=""Auto""/>
        </Grid.RowDefinitions>

        <!-- 头部提示 -->
        <Border Grid.Row=""0"" Background=""#2E3440"" Padding=""10"" CornerRadius=""5"" Margin=""0,0,0,10"">
            <TextBlock Text=""★ 跨图纸视口克隆引擎：多视口同心套合、自定义底图范围、指定工程比例与图层无损还原。""
                       Foreground=""#88C0D0"" TextWrapping=""Wrap""/>
        </Border>

        <!-- 中部表单 -->
        <StackPanel Grid.Row=""1"">
            <TextBlock Text=""源 DWG 图纸路径:"" Foreground=""#D8DEE9"" Margin=""0,0,0,3""/>
            <Grid Margin=""0,0,0,8"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>
                <TextBox x:Name=""TxtSourcePath"" IsReadOnly=""True"" Background=""#242933"" Foreground=""#D8DEE9"" 
                         BorderBrush=""#4C566A"" Padding=""5"" VerticalContentAlignment=""Center""/>
                <Button x:Name=""BtnBrowse"" Grid.Column=""1"" Content=""浏览..."" Width=""70"" Margin=""6,0,0,0""/>
            </Grid>

            <TextBlock Text=""提取源图纸的布局 (图纸空间):"" Foreground=""#D8DEE9"" FontWeight=""SemiBold"" Margin=""0,0,0,4""/>
            <ComboBox x:Name=""CmbSourceLayout"" Margin=""0,0,0,12"" Height=""28""/>

            <!-- 核心强化：模型底图范围与多视口协同套合 -->
            <Border Background=""#252A34"" BorderBrush=""#88C0D0"" BorderThickness=""1"" CornerRadius=""5"" Padding=""10"" Margin=""0,0,0,10"">
                <StackPanel>
                    <TextBlock Text=""★ 模型底图范围与视口比例套合 (保持同底图、杜绝截断):"" Foreground=""#88C0D0"" FontWeight=""Bold"" Margin=""0,0,0,6""/>

                    <!-- 框选与拾取按钮组 -->
                    <Grid Margin=""0,0,0,6"">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width=""Auto""/>
                            <ColumnDefinition Width=""Auto""/>
                            <ColumnDefinition Width=""*""/>
                        </Grid.ColumnDefinitions>
                        <Button x:Name=""BtnPickBox"" Content=""📐 框选模型底图 (对角两点)..."" Background=""#434C5E"" Foreground=""#ECEFF4"" FontWeight=""SemiBold"" Margin=""0,0,8,0""/>
                        <Button x:Name=""BtnPickEntity"" Grid.Column=""1"" Content=""🎯 拾取外框多段线/图框..."" Background=""#3B4252"" Foreground=""#ECEFF4""/>
                    </Grid>

                    <TextBlock x:Name=""TxtBaseBoxStatus"" Text=""未指定底图范围 (默认使用模型全图自动包围盒)"" Foreground=""#EBCB8B"" FontSize=""11"" TextWrapping=""Wrap"" Margin=""2,0,0,8""/>

                    <!-- 视口比例设置 -->
                    <TextBlock Text=""指定视口工程比例 (可选，默认保持源图纸比例):"" Foreground=""#D8DEE9"" FontWeight=""SemiBold"" Margin=""0,0,0,4""/>
                    <Grid Margin=""0,0,0,6"">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width=""240""/>
                            <ColumnDefinition Width=""*""/>
                        </Grid.ColumnDefinitions>
                        <ComboBox x:Name=""CmbViewportScale"" Grid.Column=""0"" IsEditable=""True"" Height=""28"" ToolTip=""可下拉选择常用比例或手动输入任意比例""/>
                        <TextBlock Grid.Column=""1"" Text=""💡 支持选择常用比例、自适应充满或手动输入"" Foreground=""#81A1C1"" FontSize=""11"" VerticalAlignment=""Center"" Margin=""10,0,0,0""/>
                    </Grid>

                    <!-- 多视口协同模式选项 -->
                    <TextBlock Text=""多视口协同对齐模式 (针对同一个底图的多个专业视口):"" Foreground=""#D8DEE9"" FontWeight=""SemiBold"" Margin=""0,4,0,3""/>
                    <RadioButton x:Name=""RbSyncAllCenter"" GroupName=""MultiVpMode"" Content=""保持同一个底图：所有视口同心对齐底图 (推荐：各视口仅图层不同)"" IsChecked=""True""/>
                    <RadioButton x:Name=""RbKeepRelative"" GroupName=""MultiVpMode"" Content=""继承源视口相对偏置分布 (按源图纸视口相对模型中心偏移)"" Margin=""0,2,0,0""/>
                </StackPanel>
            </Border>

            <!-- 坐标对齐与定位校准参考 (未框选底图时的备用策略) -->
            <Border Background=""#20242D"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""5"" Padding=""8"" Margin=""0,0,0,8"">
                <StackPanel>
                    <TextBlock Text=""全局对齐参考模式 (未指定底图范围时的自动定位策略):"" Foreground=""#D8DEE9"" FontSize=""11"" FontWeight=""Bold"" Margin=""0,0,0,4""/>
                    <StackPanel Orientation=""Horizontal"">
                        <RadioButton x:Name=""RbCenter"" GroupName=""AlignMode"" Content=""几何中心重合"" IsChecked=""True"" Margin=""0,0,14,0""/>
                        <RadioButton x:Name=""RbBottomLeft"" GroupName=""AlignMode"" Content=""左下角对齐"" Margin=""0,0,14,0""/>
                        <RadioButton x:Name=""RbPickPoint"" GroupName=""AlignMode"" Content=""参考点吸附"" Margin=""0,0,8,0""/>
                        <Button x:Name=""BtnPickPoint"" Content=""拾取点..."" Width=""70"" Height=""22"" Padding=""4,1"" FontSize=""11""/>
                    </StackPanel>
                    <TextBlock x:Name=""TxtOffsetInfo"" Text=""正在分析模型坐标关系..."" Foreground=""#A3BE8C"" FontSize=""10"" TextWrapping=""Wrap"" Margin=""2,4,0,0""/>
                    <TextBlock x:Name=""TxtPickStatus"" Text="""" Foreground=""#88C0D0"" FontSize=""10"" TextWrapping=""Wrap"" Margin=""2,1,0,0""/>
                </StackPanel>
            </Border>

            <CheckBox x:Name=""ChkSyncLayers"" Content=""自动套用图层智能对位映射规则 (重定向视口冻结图层)"" 
                      IsChecked=""True"" Foreground=""#ECEFF4"" Margin=""0,2,0,4""/>
        </StackPanel>

        <!-- 底部操作按钮 -->
        <StackPanel Grid.Row=""2"" Orientation=""Horizontal"" HorizontalAlignment=""Right"" Margin=""0,8,0,0"">
            <Button x:Name=""BtnCancel"" Content=""取消"" Width=""85"" Margin=""0,0,10,0""/>
            <Button x:Name=""BtnClone"" Content=""🚀 开始无损移植"" Background=""#A3BE8C"" Foreground=""#1E222A"" 
                    FontWeight=""Bold"" Width=""135""/>
        </StackPanel>
    </Grid>
</Window>";

        public static void ShowDialog(string sourceDwgPath)
        {
            StringReader sr = new StringReader(DialogXaml);
            System.Xml.XmlReader xr = System.Xml.XmlReader.Create(sr);
            Window win = (Window)XamlReader.Load(xr);

            TextBox txtSource = (TextBox)win.FindName("TxtSourcePath");
            Button btnBrowse = (Button)win.FindName("BtnBrowse");
            ComboBox cmbLayout = (ComboBox)win.FindName("CmbSourceLayout");

            Button btnPickBox = (Button)win.FindName("BtnPickBox");
            Button btnPickEntity = (Button)win.FindName("BtnPickEntity");
            TextBlock txtBaseBoxStatus = (TextBlock)win.FindName("TxtBaseBoxStatus");
            ComboBox cmbScale = (ComboBox)win.FindName("CmbViewportScale");
            RadioButton rbSyncAllCenter = (RadioButton)win.FindName("RbSyncAllCenter");
            RadioButton rbKeepRelative = (RadioButton)win.FindName("RbKeepRelative");

            RadioButton rbCenter = (RadioButton)win.FindName("RbCenter");
            RadioButton rbBottomLeft = (RadioButton)win.FindName("RbBottomLeft");
            RadioButton rbPickPoint = (RadioButton)win.FindName("RbPickPoint");
            Button btnPickPoint = (Button)win.FindName("BtnPickPoint");
            TextBlock txtOffsetInfo = (TextBlock)win.FindName("TxtOffsetInfo");
            TextBlock txtPickStatus = (TextBlock)win.FindName("TxtPickStatus");

            CheckBox chkSync = (CheckBox)win.FindName("ChkSyncLayers");
            Button btnCancel = (Button)win.FindName("BtnCancel");
            Button btnClone = (Button)win.FindName("BtnClone");

            // 初始化比例预设列表 (以“保持源图纸比例”为首选默认项，避免误改原图纸比例)
            cmbScale.Items.Add("保持源图纸比例 (推荐)");
            cmbScale.Items.Add("✨ 智能充满视口 (自适应Fit)");
            cmbScale.Items.Add("1:100 (常用标准)");
            cmbScale.Items.Add("1:50");
            cmbScale.Items.Add("1:30");
            cmbScale.Items.Add("1:20");
            cmbScale.Items.Add("1:150");
            cmbScale.Items.Add("1:200");
            cmbScale.Items.Add("1:1");
            cmbScale.SelectedIndex = 0;

            Point3d srcMinPt = Point3d.Origin, srcMaxPt = Point3d.Origin;
            Point3d tgtMinPt = Point3d.Origin, tgtMaxPt = Point3d.Origin;
            Vector2d pickedOffset = new Vector2d(0, 0);
            bool hasPicked = false;

            // 框选底图几何量
            bool hasBaseBox = false;
            Point3d baseCenterPt = Point3d.Origin;
            double baseW = 0.0;
            double baseH = 0.0;

            Action<string> loadDwg = delegate(string path)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return;
                }

                txtSource.Text = path;
                cmbLayout.Items.Clear();

                List<string> layouts = ViewportCloneService.GetLayoutNamesFromExternalDwg(path);
                foreach (string lay in layouts)
                {
                    cmbLayout.Items.Add(lay);
                }
                if (cmbLayout.Items.Count > 0)
                {
                    cmbLayout.SelectedIndex = 0;
                }

                bool ok = ViewportCloneService.DetectModelBounds(path, out srcMinPt, out srcMaxPt, out tgtMinPt, out tgtMaxPt);
                if (ok)
                {
                    Point3d srcCenter = new Point3d((srcMinPt.X + srcMaxPt.X) * 0.5, (srcMinPt.Y + srcMaxPt.Y) * 0.5, 0);
                    Point3d tgtCenter = new Point3d((tgtMinPt.X + tgtMaxPt.X) * 0.5, (tgtMinPt.Y + tgtMaxPt.Y) * 0.5, 0);
                    double cDx = tgtCenter.X - srcCenter.X;
                    double cDy = tgtCenter.Y - srcCenter.Y;

                    double bDx = tgtMinPt.X - srcMinPt.X;
                    double bDy = tgtMinPt.Y - srcMinPt.Y;

                    txtOffsetInfo.Text = string.Format("★ 探测位移: 中心偏移 ΔX={0:+0.00;-0.00;0.00}, ΔY={1:+0.00;-0.00;0.00} | 左下角偏移 ΔX={2:+0.00;-0.00;0.00}, ΔY={3:+0.00;-0.00;0.00}",
                        cDx, cDy, bDx, bDy);
                }
                else
                {
                    txtOffsetInfo.Text = "★ 两图模型坐标一致或未探测到有效外包围盒";
                }
            };

            if (!string.IsNullOrEmpty(sourceDwgPath) && File.Exists(sourceDwgPath))
            {
                loadDwg(sourceDwgPath);
            }

            btnBrowse.Click += delegate
            {
                System.Windows.Forms.OpenFileDialog dlg = new System.Windows.Forms.OpenFileDialog();
                dlg.Title = "选择源标准图纸 (提取视口)";
                dlg.Filter = "AutoCAD 图纸 (*.dwg)|*.dwg|所有文件 (*.*)|*.*";
                try
                {
                    if (!string.IsNullOrEmpty(txtSource.Text) && File.Exists(txtSource.Text))
                    {
                        dlg.InitialDirectory = Path.GetDirectoryName(txtSource.Text);
                    }
                }
                catch { }

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    loadDwg(dlg.FileName);
                }
            };

            // 1. 框选模型底图角点
            btnPickBox.Click += delegate
            {
                win.Hide();
                try
                {
                    Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        Editor ed = doc.Editor;
                        PromptPointOptions ppo1 = new PromptPointOptions("\n[视口移植] 请指定模型底图范围的第 1 个角点: ");
                        ppo1.AllowNone = false;
                        PromptPointResult ppr1 = ed.GetPoint(ppo1);
                        if (ppr1.Status == PromptStatus.OK)
                        {
                            PromptCornerOptions pco = new PromptCornerOptions("\n[视口移植] 请指定模型底图范围的对角角点: ", ppr1.Value);
                            pco.UseDashedLine = true;
                            PromptPointResult ppr2 = ed.GetCorner(pco);
                            if (ppr2.Status == PromptStatus.OK)
                            {
                                Point3d pt1 = ppr1.Value;
                                Point3d pt2 = ppr2.Value;
                                double minX = Math.Min(pt1.X, pt2.X);
                                double maxX = Math.Max(pt1.X, pt2.X);
                                double minY = Math.Min(pt1.Y, pt2.Y);
                                double maxY = Math.Max(pt1.Y, pt2.Y);

                                baseCenterPt = new Point3d((minX + maxX) * 0.5, (minY + maxY) * 0.5, 0);
                                baseW = maxX - minX;
                                baseH = maxY - minY;
                                hasBaseBox = true;

                                txtBaseBoxStatus.Text = string.Format("✓ 已锁定模型底图: 宽 {0:F0} × 高 {1:F0} | 中心 ({2:F1}, {3:F1})",
                                    baseW, baseH, baseCenterPt.X, baseCenterPt.Y);
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show("框选底图异常: " + ex.Message);
                }
                finally
                {
                    win.Show();
                    win.Activate();
                }
            };

            // 2. 直接拾取底图外框多段线
            btnPickEntity.Click += delegate
            {
                win.Hide();
                try
                {
                    Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        Editor ed = doc.Editor;
                        PromptEntityOptions peo = new PromptEntityOptions("\n[视口移植] 请点击模型空间中的底图外框多段线或图框: ");
                        peo.AllowNone = false;
                        PromptEntityResult per = ed.GetEntity(peo);
                        if (per.Status == PromptStatus.OK)
                        {
                            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                            {
                                Entity ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                                Extents3d ext = ent.GeometricExtents;
                                baseCenterPt = new Point3d((ext.MinPoint.X + ext.MaxPoint.X) * 0.5, (ext.MinPoint.Y + ext.MaxPoint.Y) * 0.5, 0);
                                baseW = ext.MaxPoint.X - ext.MinPoint.X;
                                baseH = ext.MaxPoint.Y - ext.MinPoint.Y;
                                hasBaseBox = true;
                                tr.Commit();
                            }

                            txtBaseBoxStatus.Text = string.Format("✓ 已锁定外框图元底图: 宽 {0:F0} × 高 {1:F0} | 中心 ({2:F1}, {3:F1})",
                                baseW, baseH, baseCenterPt.X, baseCenterPt.Y);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show("拾取图元异常: " + ex.Message);
                }
                finally
                {
                    win.Show();
                    win.Activate();
                }
            };

            btnPickPoint.Click += delegate
            {
                win.Hide();
                try
                {
                    Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        Editor ed = doc.Editor;
                        PromptPointOptions ppo = new PromptPointOptions("\n[视口移植] 请在当前图纸中点选对齐参考基准点 (如第1根柱角点或轴网交点): ");
                        ppo.AllowNone = false;
                        PromptPointResult ppr = ed.GetPoint(ppo);
                        if (ppr.Status == PromptStatus.OK)
                        {
                            Point3d pickedPt = ppr.Value;
                            hasPicked = true;
                            rbPickPoint.IsChecked = true;

                            // 优先以源图左下角作为参考基准计算相对偏移
                            double dx = pickedPt.X - srcMinPt.X;
                            double dy = pickedPt.Y - srcMinPt.Y;
                            pickedOffset = new Vector2d(dx, dy);

                            txtPickStatus.Text = string.Format("已拾取点 ({0:F1}, {1:F1}) -> 相对源基准偏移: ΔX={2:+0.0;-0.0}, ΔY={3:+0.0;-0.0}",
                                pickedPt.X, pickedPt.Y, dx, dy);
                        }
                    }
                }
                catch { }
                finally
                {
                    win.Show();
                    win.Activate();
                }
            };

            btnCancel.Click += delegate
            {
                win.Close();
            };

            btnClone.Click += delegate
            {
                string curPath = txtSource.Text;
                if (string.IsNullOrEmpty(curPath) || !File.Exists(curPath))
                {
                    System.Windows.MessageBox.Show("请先指定有效的源 DWG 文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string selectedLayout = cmbLayout.SelectedItem as string;
                if (string.IsNullOrEmpty(selectedLayout))
                {
                    System.Windows.MessageBox.Show("请选择要移植的源布局名称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Dictionary<string, string> layerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (chkSync.IsChecked == true)
                {
                    Database workingDb = HostApplicationServices.WorkingDatabase;
                    try
                    {
                        string pluginDir = Path.GetDirectoryName(typeof(LayerAlignmentEngine).Assembly.Location);
                        string pluginCsv = Path.Combine(pluginDir, "layer_mapping_rules.csv");
                        if (File.Exists(pluginCsv))
                        {
                            List<LayerMapRule> pRules = LayerAlignmentEngine.LoadRulesFromCsv(pluginCsv);
                            foreach (LayerMapRule r in pRules)
                            {
                                if (!string.IsNullOrEmpty(r.SourceLayer) && !string.IsNullOrEmpty(r.TargetLayer))
                                {
                                    layerMap[r.SourceLayer] = r.TargetLayer;
                                }
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        string localRulePath = LayerAlignmentEngine.GetDefaultRuleFilePath(workingDb != null ? workingDb.Filename : null);
                        if (File.Exists(localRulePath))
                        {
                            List<LayerMapRule> lRules = LayerAlignmentEngine.LoadRulesFromCsv(localRulePath);
                            foreach (LayerMapRule r in lRules)
                            {
                                if (!string.IsNullOrEmpty(r.SourceLayer) && !string.IsNullOrEmpty(r.TargetLayer))
                                {
                                    layerMap[r.SourceLayer] = r.TargetLayer;
                                }
                            }
                        }
                    }
                    catch { }
                }

                ModelAlignmentMode alignMode = ModelAlignmentMode.Center;
                Vector2d finalOffset = new Vector2d(0, 0);

                if (rbPickPoint.IsChecked == true && hasPicked)
                {
                    alignMode = ModelAlignmentMode.CustomDelta;
                    finalOffset = pickedOffset;
                }
                else if (rbBottomLeft.IsChecked == true)
                {
                    alignMode = ModelAlignmentMode.BottomLeft;
                }
                else
                {
                    alignMode = ModelAlignmentMode.Center;
                }

                // 组装全新视口移植高级配置
                ViewportCloneOptions opt = new ViewportCloneOptions();
                opt.SourceDwgPath = curPath;
                opt.SourceLayoutName = selectedLayout;
                opt.TargetLayoutId = ObjectId.Null;
                opt.LayerMappingRules = layerMap;
                opt.AlignMode = alignMode;
                opt.CustomOffset = finalOffset;

                if (hasBaseBox)
                {
                    opt.HasModelBaseBox = true;
                    opt.ModelBaseCenter = baseCenterPt;
                    opt.ModelBaseWidth = baseW;
                    opt.ModelBaseHeight = baseH;
                }

                // 解析视口比例
                opt.TargetCustomScale = ParseScaleText(cmbScale.Text);
                opt.MultiViewportSyncCenter = (rbSyncAllCenter.IsChecked == true);

                ViewportCloneService service = new ViewportCloneService();
                ViewportCloneService.CloneResult res = service.CloneViewportsToLayout(opt);

                if (res.Success)
                {
                    System.Windows.MessageBox.Show(res.Message + "\n\n各专业视口已完美协同套准底图，请切换至布局空间查看！", "移植成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    win.Close();
                }
                else
                {
                    System.Windows.MessageBox.Show("视口移植遇到问题: " + res.Message, "移植警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            try
            {
                IntPtr acadHwnd = Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle;
                new System.Windows.Interop.WindowInteropHelper(win).Owner = acadHwnd;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            catch { }

            try
            {
                win.ShowDialog();
            }
            catch
            {
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(win);
            }
        }

        public static double ParseScaleText(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            string t = text.Trim();
            if (t.Contains("自适应") || t.IndexOf("fit", StringComparison.OrdinalIgnoreCase) >= 0 || t.Contains("充满"))
            {
                return 0.0; // 0 代表自适应充满
            }
            if (t.Contains("保持") || t.Contains("不变") || t.Contains("原始") || t.Contains("推荐") || t.Contains("不改变"))
            {
                return -1.0; // -1 代表保留源视口比例
            }

            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(t, @"^(\d+(\.\d+)?)\s*[:/]\s*(\d+(\.\d+)?)");
            if (m.Success)
            {
                double num = double.Parse(m.Groups[1].Value);
                double den = double.Parse(m.Groups[3].Value);
                if (den > 0) return num / den;
            }

            double val;
            if (double.TryParse(t, out val))
            {
                if (val > 0) return val;
            }

            return -1.0;
        }
    }
}
