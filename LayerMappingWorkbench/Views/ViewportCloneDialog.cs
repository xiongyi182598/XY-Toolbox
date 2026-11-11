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
        Width=""620"" Height=""530"" MinWidth=""540"" MinHeight=""480""
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

        <Style TargetType=""ComboBox"">
            <Setter Property=""Background"" Value=""#2B303C""/>
            <Setter Property=""Foreground"" Value=""#ECEFF4""/>
            <Setter Property=""BorderBrush"" Value=""#4C566A""/>
            <Setter Property=""Padding"" Value=""6,4""/>
            <Setter Property=""KeyboardNavigation.DirectionalNavigation"" Value=""Contained""/>
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
        <Border Grid.Row=""0"" Background=""#2E3440"" Padding=""10"" CornerRadius=""5"" Margin=""0,0,0,12"">
            <TextBlock Text=""★ 跨图纸视口克隆引擎：自动重建几何、DCS视心、相机高度、剪裁多段线及图层覆盖。""
                       Foreground=""#88C0D0"" TextWrapping=""Wrap""/>
        </Border>

        <!-- 中部表单 -->
        <StackPanel Grid.Row=""1"">
            <TextBlock Text=""源 DWG 图纸路径:"" Foreground=""#D8DEE9"" Margin=""0,0,0,4""/>
            <Grid Margin=""0,0,0,10"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>
                <TextBox x:Name=""TxtSourcePath"" IsReadOnly=""True"" Background=""#242933"" Foreground=""#D8DEE9"" 
                         BorderBrush=""#4C566A"" Padding=""6"" VerticalContentAlignment=""Center""/>
                <Button x:Name=""BtnBrowse"" Grid.Column=""1"" Content=""浏览..."" Width=""70"" Margin=""6,0,0,0""/>
            </Grid>

            <TextBlock Text=""提取源图纸的布局 (图纸空间):"" Foreground=""#D8DEE9"" Margin=""0,0,0,4""/>
            <ComboBox x:Name=""CmbSourceLayout"" Margin=""0,0,0,12""/>

            <!-- 坐标对齐与定位校准控制区 -->
            <Border Background=""#252A34"" BorderBrush=""#3B4252"" BorderThickness=""1"" CornerRadius=""5"" Padding=""10"" Margin=""0,0,0,10"">
                <StackPanel>
                    <TextBlock Text=""对齐与定位校准模式 (精准解决视口套合问题):"" Foreground=""#EBCB8B"" FontWeight=""Bold"" Margin=""0,0,0,6""/>

                    <RadioButton x:Name=""RbCenter"" GroupName=""AlignMode"" Content=""智能几何中心对齐 (两图实体几何中心重合，自动居中)"" IsChecked=""True""/>
                    <RadioButton x:Name=""RbBottomLeft"" GroupName=""AlignMode"" Content=""图元左下角对齐 (以构件最外侧边界左下角为锚点)""/>
                    
                    <Grid Margin=""0,4,0,0"">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width=""Auto""/>
                            <ColumnDefinition Width=""*""/>
                        </Grid.ColumnDefinitions>
                        <RadioButton x:Name=""RbPickPoint"" GroupName=""AlignMode"" Content=""鼠标拾取基准点吸附对齐"" VerticalAlignment=""Center""/>
                        <Button x:Name=""BtnPickPoint"" Grid.Column=""1"" Content=""🎯 拾取模型点..."" Width=""110"" HorizontalAlignment=""Left"" Margin=""10,0,0,0""/>
                    </Grid>

                    <TextBlock x:Name=""TxtOffsetInfo"" Text=""正在分析模型坐标关系..."" Foreground=""#A3BE8C"" 
                               FontSize=""11"" TextWrapping=""Wrap"" Margin=""4,6,0,0""/>
                    <TextBlock x:Name=""TxtPickStatus"" Text="""" Foreground=""#88C0D0"" 
                               FontSize=""11"" TextWrapping=""Wrap"" Margin=""4,2,0,0""/>
                </StackPanel>
            </Border>

            <CheckBox x:Name=""ChkSyncLayers"" Content=""自动套用图层智能对位映射规则 (重定向视口冻结图层)"" 
                      IsChecked=""True"" Foreground=""#ECEFF4"" Margin=""0,0,0,6""/>
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
            RadioButton rbCenter = (RadioButton)win.FindName("RbCenter");
            RadioButton rbBottomLeft = (RadioButton)win.FindName("RbBottomLeft");
            RadioButton rbPickPoint = (RadioButton)win.FindName("RbPickPoint");
            Button btnPickPoint = (Button)win.FindName("BtnPickPoint");
            TextBlock txtOffsetInfo = (TextBlock)win.FindName("TxtOffsetInfo");
            TextBlock txtPickStatus = (TextBlock)win.FindName("TxtPickStatus");
            CheckBox chkSync = (CheckBox)win.FindName("ChkSyncLayers");
            Button btnCancel = (Button)win.FindName("BtnCancel");
            Button btnClone = (Button)win.FindName("BtnClone");

            Point3d srcMinPt = Point3d.Origin, srcMaxPt = Point3d.Origin;
            Point3d tgtMinPt = Point3d.Origin, tgtMaxPt = Point3d.Origin;
            Vector2d pickedOffset = new Vector2d(0, 0);
            bool hasPicked = false;

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
                    string rulePath = LayerAlignmentEngine.GetDefaultRuleFilePath(workingDb.Filename);
                    List<LayerMapRule> rules = LayerAlignmentEngine.LoadRulesFromCsv(rulePath);
                    foreach (LayerMapRule r in rules)
                    {
                        if (!string.IsNullOrEmpty(r.SourceLayer) && !string.IsNullOrEmpty(r.TargetLayer))
                        {
                            layerMap[r.SourceLayer] = r.TargetLayer;
                        }
                    }
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

                ViewportCloneService service = new ViewportCloneService();
                ViewportCloneService.CloneResult res = service.CloneViewportsToLayout(curPath, selectedLayout, ObjectId.Null, layerMap, alignMode, finalOffset);

                if (res.Success)
                {
                    System.Windows.MessageBox.Show(res.Message + "\n\n请在当前布局中查看移植视口结果。", "移植成功", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}
