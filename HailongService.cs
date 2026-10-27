using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace XY_Deepening_Toolbox
{
    public class HailongItem
    {
        public string Name { get; set; }
        public string EnName { get; set; }
        public string Command { get; set; }
        public string Shortcut { get; set; }
        public string IconName { get; set; }
        public string Tooltip { get; set; }
        public bool IsCustomEnhanced { get; set; }
        public bool IsSeparator { get; set; }

        public HailongItem() { }
        public HailongItem(string name, string enName, string cmd, string sc, string icon, string tip = "", bool isEnhanced = false, bool isSep = false)
        {
            Name = name;
            EnName = enName;
            Command = cmd;
            Shortcut = sc;
            IconName = icon;
            Tooltip = tip;
            IsCustomEnhanced = isEnhanced;
            IsSeparator = isSep;
        }
    }

    public class HailongCategory
    {
        public string Title { get; set; }
        public string IconName { get; set; }
        public List<HailongItem> Items { get; set; }

        public HailongCategory()
        {
            Items = new List<HailongItem>();
        }
        public HailongCategory(string title, string icon = "")
        {
            Title = title;
            IconName = icon;
            Items = new List<HailongItem>();
        }
    }

    public static class HailongDefaultData
    {
        public static List<HailongCategory> GetDefaultCategories()
        {
            var list = new List<HailongCategory>();

            // 分类 [01]: 图形系统
            var cat_0 = new HailongCategory("图形系统", "ssy.bmp");
            cat_0.Items.Add(new HailongItem("切换快捷键", "切换快捷键", "kjj", "kjj", "kjj.bmp", "循环切换打开或关闭软件的快捷命令"));
            cat_0.Items.Add(new HailongItem("开启软件命令", "开启软件命令", "zhl_kj", "", "ZP.bmp", "开启海龙软件的快捷命令"));
            cat_0.Items.Add(new HailongItem("恢复原有命令", "恢复原有命令", "zhl_gj", "", "ZP.bmp", "恢复CAD软件原有的快捷命令"));
            cat_0.Items.Add(new HailongItem("切换配置", "切换配置", "zhl_cg", "cg", "YY.BMP", "切换不同的用户配置"));
            cat_0.Items.Add(new HailongItem("切换语言", "切换语言", "zhl_lll", "lll", "lll.bmp", "切换软件不同的提示语言"));
            cat_0.Items.Add(new HailongItem("改快捷键", "改快捷键", "zhl_y", "y", "Y.bmp", "重新定义快捷键"));
            cat_0.Items.Add(new HailongItem("系统配置", "系统配置", "zhl_yy", "yy", "YY.BMP", "设置软件运行环境参数。"));
            cat_0.Items.Add(new HailongItem("命令查找", "命令查找", "zhl_yyy", "yyy", "yyy.bmp", "搜索软件的简化命令，查找的内容可以是中文也可以是命令字母。"));
            cat_0.Items.Add(new HailongItem("符号修改", "符号修改", "zhl_ssy", "ssy", "SSY.bmp", "统计门窗表(可分层统计)"));
            cat_0.Items.Add(new HailongItem("设置比例", "设置比例", "zhl_de", "de", "DE.bmp", "设置比例或比例更新物体，可以更新文字、标注、线型、填充、视口及相关系统符号，点击类型按钮可进行相关设置"));
            cat_0.Items.Add(new HailongItem("模拟显示", "模拟显示", "zhl_z11", "z11", "Z11.bmp", "屏幕按图纸打印出来的实际大小1:1显示（由此可以看出图纸打印出来后的详细程度和线型大小）"));
            cat_0.Items.Add(new HailongItem("菜单开关", "菜单开关", "zhl_cdkg", "cdkg", "cdkg.bmp", "循环切换打开或关闭软件菜单"));
            cat_0.Items.Add(new HailongItem("归层开关", "归层开关", "zhl_gf", "gf", "GF.bmp", "循环切换打开或关闭自动归层"));
            cat_0.Items.Add(new HailongItem("设置归层", "设置归层", "zhl_gff", "gff", "gff.BMP", "设置自动切换的默认图层"));
            cat_0.Items.Add(new HailongItem("编组开关", "编组开关", "zhl_ggf", "ggf", "GGF.bmp", "循环切换打开或关闭编组"));
            cat_0.Items.Add(new HailongItem("填充捕捉", "填充捕捉", "zhl_hg", "hg", "HG.bmp", "循环切换打开或关闭填充捕捉"));
            cat_0.Items.Add(new HailongItem("设置单位", "设置单位", "zhl_ui", "ui", "ui.bmp", "设定图纸绘图单位(毫米/厘米/分米/米/英寸)"));
            cat_0.Items.Add(new HailongItem("自定图框", "自定图框", "zhl_tk1", "tk1", "TK1.bmp", "先把新图框缩放成1:1，①点取框图块，②框取外框范围，③框取内框范围，④保存到文件"));
            cat_0.Items.Add(new HailongItem("插入图框", "插入图框", "zhl_tk", "tk", "TK.bmp", "插入图框"));
            cat_0.Items.Add(new HailongItem("图框排列", "图框排列", "zhl_tkk", "tkk", "TK.bmp", "图框排列"));
            cat_0.Items.Add(new HailongItem("图纸目录", "图纸目录", "zhl_ml", "ml", "ML.bmp", "读取属性块图框内容制作图纸目录"));
            cat_0.Items.Add(new HailongItem("遮罩边框", "遮罩边框", "zhl_wu", "wu", "WU.bmp", "循环切换打开或关闭遮罩边框"));
            cat_0.Items.Add(new HailongItem("批量打印", "批量打印", "zhl_bp", "bp", "BP.bmp", "第一步：点取“打印设置”进行打印设置，设置正确的话可忽略此步；第二步：图框是属性块的话，点取“图块”按钮；第三步：点取“从图中指定图块或图层”设置图块名称；第四步：直接输出到打印机的话，点取“直接批量打印”进行打印，打印成PDF的话，点取“打印到文件”进行输出打印；第五步：点取“选择要处理的图纸”进行打印"));
            cat_0.Items.Add(new HailongItem("智能打印", "智能打印", "zhl_hlp", "hlp", "BP.bmp", "智能识别图框（加长）尺寸打印，打印方便可按图块或图层打印，方便对生成的图纸进行命名生成"));
            cat_0.Items.Add(new HailongItem("超级打印", "超级打印", "zhl_qp", "qp", "QP.bmp", "可添加多个文件批量打印，添加图框后，软件自动打开文件并自动打印然后关闭文件"));
            cat_0.Items.Add(new HailongItem("图形加锁", "图形加锁", "zhl_lk", "lk", "LK.bmp", "将当前文件的图形制作成一个炸不开的图块"));
            cat_0.Items.Add(new HailongItem("当前目录", "当前目录", "zhl_rf", "rf", "RF.bmp", "打开当前图纸所在的文件夹"));
            cat_0.Items.Add(new HailongItem("全屏切换", "全屏切换", "zhl_sr", "sr", "SR.bmp", "循环切换绘图区是否全屏显示"));
            cat_0.Items.Add(new HailongItem("空间转换", "空间转换", "zhl_1`", "1`", "1`.bmp", "循环切换图形空间和模型空间"));
            cat_0.Items.Add(new HailongItem("旋转视图", "旋转视图", "zhl_rv", "rv", "RV.bmp", "旋转绘图区的绘图角度"));
            cat_0.Items.Add(new HailongItem("查杀病毒", "查杀病毒", "zhl_ki", "ki", "KI.bmp", "查杀CAD病毒(建议关闭CAD再查杀这样才杀得彻底)"));
            cat_0.Items.Add(new HailongItem("菜单设置", "菜单设置", "cdxs", "cdxs", "cdxs.bmp", "设置侧栏菜单颜色字体等参数"));
            cat_0.Items.Add(new HailongItem("经典模式", "经典模式", "zhl_jdms", "jdms", "cdxs.bmp", "切换到早期的经典模式"));
            cat_0.Items.Add(new HailongItem("菜单开关", "菜单开关", "zdcd", "zdcd", "cdxs.bmp", "Alt+Z(侧栏菜单开关)、Alt+X(顶部菜单开关)"));
            cat_0.Items.Add(new HailongItem("重要提示", "重要提示", "zhl_it", "it", "it.bmp", "显示海龙软件的关键提示"));
            cat_0.Items.Add(new HailongItem("关于软件", "关于软件", "zhl_about", "abt", "zhl.bmp", "显示关于软件的信息"));
            cat_0.Items.Add(new HailongItem("查看帮助", "查看帮助", "zhl_lp", "lp", "lp.bmp", "查看软件的帮助"));
            cat_0.Items.Add(new HailongItem("卸载软件", "卸载软件", "zhl_unload", "unhl", "UNLOAD.bmp", ""));
            list.Add(cat_0);

            // 分类 [02]: 平面门窗
            var cat_1 = new HailongCategory("平面门窗", "mm.bmp");
            cat_1.Items.Add(new HailongItem("平面门窗", "平面门窗", "zhl_mm", "mm", "MM.bmp", "从对话框选门窗样式进行插入"));
            cat_1.Items.Add(new HailongItem("墙开套门", "墙开套门", "zhl_pk", "pk", "pk.bmp", "墙或轴线上自动断开墙体并插入门套门"));
            cat_1.Items.Add(new HailongItem("插入套门", "插入套门", "zhl_mm2", "mm2", "pk1.bmp", "两点插入套门"));
            cat_1.Items.Add(new HailongItem("转为套门", "转为套门", "zhl_htm", "htm", "HTM.bmp", "门窗替换为门套门"));
            cat_1.Items.Add(new HailongItem("开无套门", "开无套门", "zhl_wtm", "wtm", "wtm.bmp", "墙上开无套门"));
            cat_1.Items.Add(new HailongItem("插无套门", "插无套门", "zhl_wtm1", "wtm1", "wtm1.bmp", "两点插入无套门"));
            cat_1.Items.Add(new HailongItem("墙上开洞", "墙上开洞", "zhl_kd", "kd", "KD.bmp", "墙上开门洞"));
            cat_1.Items.Add(new HailongItem("门窗置换", "门窗置换", "zhl_mc", "mc", "MC.bmp", "门窗置换(从门窗图库中选择)"));
            cat_1.Items.Add(new HailongItem("墙上开窗", "墙上开窗", "zhl_kc", "kc", "KC.bmp", "墙或轴线上自动断开墙体并插入窗户"));
            cat_1.Items.Add(new HailongItem("两点画窗", "两点画窗", "zhl_kc1", "kc1", "KC1.bmp", "两点插入窗户"));
            cat_1.Items.Add(new HailongItem("转成窗户", "转成窗户", "zhl_hc", "hc", "HC.bmp", "替代为窗户"));
            cat_1.Items.Add(new HailongItem("开转角窗", "开转角窗", "zhl_zjc", "zjc", "ZJC.bmp", "墙或轴线上自动断开墙体并插入转角窗"));
            cat_1.Items.Add(new HailongItem("三点角窗", "三点角窗", "zhl_jcc", "jcc", "JCC.bmp", "三点绘制转角窗"));
            cat_1.Items.Add(new HailongItem("换转角窗", "换转角窗", "zhl_hzjc", "hzjc", "HZJC.bmp", "转角窗替换样式"));
            cat_1.Items.Add(new HailongItem("门槛处理", "门槛处理", "zhl_mk", "mk", "MK.bmp", "选取平面门添加门槛线"));
            cat_1.Items.Add(new HailongItem("画洞口线", "画洞口线", "zhl_dkx", "dkx", "DKX.bmp", "添加或删除天花洞口线"));
            cat_1.Items.Add(new HailongItem("线变窗户", "线变窗户", "zhl_xbc", "xbc", "XBC.bmp", "把线转变为平面窗"));
            cat_1.Items.Add(new HailongItem("门窗宽度", "门窗宽度", "zhl_mmk", "mmk", "MMK.bmp", "修改门窗宽度"));
            cat_1.Items.Add(new HailongItem("门窗移动", "门窗移动", "zhl_mmm", "mmm", "MMM.bmp", "沿着墙线移动门窗并自动修被墙线"));
            cat_1.Items.Add(new HailongItem("门窗修正", "门窗修正", "zhl_mmx", "mmx", "MMX.bmp", "修正门窗(修正位置、厚度并兼容天正3)和柱子"));
            cat_1.Items.Add(new HailongItem("移动定位", "移动定位", "zhl_mmd", "mmd", "MMD.bmp", "移动并修正门窗柱子"));
            cat_1.Items.Add(new HailongItem("门窗复制", "门窗复制", "zhl_mmc", "mmc", "MMC.bmp", "复制并修正门窗柱子"));
            cat_1.Items.Add(new HailongItem("门窗匹配", "门窗匹配", "zhl_mmp", "mmp", "MMP.bmp", "刷子.匹配图块(属性不刷"));
            cat_1.Items.Add(new HailongItem("翻转门窗", "翻转门窗", "zhl_fz", "fz", "FZ1.bmp", "随意翻转(可用于翻转门窗)"));
            cat_1.Items.Add(new HailongItem("门窗编号", "门窗编号", "zhl_mmb", "mmb", "MMB.bmp", "门窗编号管理"));
            cat_1.Items.Add(new HailongItem("删墙门窗", "删墙门窗", "zhl_mme", "mme", "MME.bmp", "删除墙体、柱子、门窗，自动修补删后墙体"));
            cat_1.Items.Add(new HailongItem("统计门窗", "统计门窗", "zhl_tj", "tj", "tj.bmp", "统计门窗表(可分层统计)"));
            list.Add(cat_1);

            // 分类 [03]: 平面功能
            var cat_2 = new HailongCategory("平面功能", "cq.bmp");
            cat_2.Items.Add(new HailongItem("图纸系统", "图纸系统", "zhl_ttt", "ttt", "ttt.bmp", "智能图纸系统注意：平面外围标注不要组块，每个房间画好【区域轮廓线】插入区域字，轮廓线、天花线、灯具、家私、地坪分割线等图层需对应到系统层，生成地面铺装图或天花系列图纸前，先用DD命令插入材料标记图块，家具尺寸图要注意图块插入点，引线标注出来就是按图块名称来的。"));
            cat_2.Items.Add(new HailongItem("平面排图", "平面排图", "zhl_ppp", "ppp", "ppp.bmp", "可以自定义图框、新建多个列表配置方案、设置排图方式、布局页面设置"));
            cat_2.Items.Add(new HailongItem("拆墙新建", "拆墙新建", "zhl_cq", "cq", "CQ.bmp", "注意：墙体线必须是围合封闭线，且图层在系统墙体层，执行命令前最好优化一下图纸"));
            cat_2.Items.Add(new HailongItem("墙面尺寸", "墙面尺寸", "zhl_qcc", "qcc", "QCC.bmp", "快速绘制墙面完成面尺寸图(提示：此功能需要区域轮廓线和天花造型线对应到系统层)"));
            cat_2.Items.Add(new HailongItem("家具尺寸", "家具尺寸", "zhl_pbb", "pbb", "PBB.bmp", "使用前需要绘制每个区域的轮廓线，家私图块对应到系统家具层，是注意图块插入点，引线标注出来就是按图块名称来的"));
            cat_2.Items.Add(new HailongItem("天花布置", "天花布置", "zhl_ptt", "ptt", "PTT.bmp", "生成天花图前先用DD命令插入天花标记图块，也可以进行智能批量插入并替换材料，然后框取平面图（包含平面图的外围标注）自动往两侧引线标注天花材料"));
            cat_2.Items.Add(new HailongItem("天花尺寸", "天花尺寸", "zhl_pcc", "pcc", "PCC.bmp", "提示：区域轮廓线和天花造型线对应到系统层，绘制完成后自动在左下角生成灯具统计表"));
            cat_2.Items.Add(new HailongItem("灯具定位", "灯具定位", "zhl_pdd", "pdd", "PDD.bmp", "提示：区域轮廓线图层、天花造型线图层和灯具图层对应到系统层，注意灯具的插入点为灯具图块中心"));
            cat_2.Items.Add(new HailongItem("开关控制", "开关控制", "zhl_kg", "kg", "kg.bmp", "灯具或灯带连线并添加开关，输入“S”设置/“A”重新连线/“D”选连线加灯/“E”选连线减灯/“F”选连线加开关/“G”选灯具或开关加序号"));
            cat_2.Items.Add(new HailongItem("开关标注", "开关标注", "zhl_kgg", "kgg", "KGG.bmp", "标注开关点位定位尺寸+引线文字标注"));
            cat_2.Items.Add(new HailongItem("地面铺装", "地面铺装", "zhl_pz", "pz", "PZ.bmp", "先用DD命令插入材料并用ED命令进行修改，PZ命令框取整个平面图（包含平面图的外围标注）"));
            cat_2.Items.Add(new HailongItem("机电点位", "机电点位", "zhl_pjj", "pjj", "PJJ.bmp", "提示：区域轮廓线和家具图块对应到系统层，程序会按图块名称智能插入机电图块并标注定位尺寸，绘制完成后自动在左下角生成机电统计表"));
            cat_2.Items.Add(new HailongItem("给排水点", "给排水点", "zhl_pss", "pss", "PSS.bmp", "提示：区域轮廓线和家具图块对应到系统层，程序会按图块名称智能插入给排水图块并标注定位尺寸，绘制完成后自动在左下角生成统计表"));
            cat_2.Items.Add(new HailongItem("平面索引", "平面索引", "zhl_pmsy", "pmsy", "SYFX.bmp", "快速对平面布置图添加索引"));
            cat_2.Items.Add(new HailongItem("光照分析", "光照分析", "zhl_gzf", "gzf", "DGG.bmp", "光照分析"));
            cat_2.Items.Add(new HailongItem("淋浴地面", "淋浴地面", "zhl_ly", "ly", "ly.bmp", "绘制淋浴房地面"));
            cat_2.Items.Add(new HailongItem("区域轮廓", "区域轮廓", "zhl_qy", "qy", "QY.bmp", "绘制区域轮廓线，提示：如果先选物体再执行命令时，将会把选中的物体归到轮廓线层"));
            cat_2.Items.Add(new HailongItem("范围取图", "范围取图", "zhl_fwqt", "fwqt", "FWQT.bmp", "通过两点框取一个矩形范围，并把这个矩形范围提取出来"));
            cat_2.Items.Add(new HailongItem("天花造型", "天花造型", "zhl_th", "th", "th.bmp", "通过三点绘制石膏板天花剖面"));
            cat_2.Items.Add(new HailongItem("沿线布灯", "沿线布灯", "zhl_cd", "cd", "cd.bmp", "沿线布置灯具"));
            cat_2.Items.Add(new HailongItem("布置灯具", "布置灯具", "zhl_bd", "bd", "bd.bmp", "快速布置平面灯具"));
            cat_2.Items.Add(new HailongItem("定点布灯", "定点布灯", "zhl_ddbd", "ddbd", "ddbd.bmp", "通过两点在两点范围内布置灯具"));
            cat_2.Items.Add(new HailongItem("角线放线", "角线放线", "zhl_jxfx", "jxfx", "JXFX.bmp", "按距离多次偏移，并对节点进行连线"));
            cat_2.Items.Add(new HailongItem("节点连线", "节点连线", "zhl_jj", "jj", "jj.bmp", "输入 “R” 调整空间、“S” 调整沙发、“T” 调整参数"));
            cat_2.Items.Add(new HailongItem("墙面材料", "墙面材料", "zhl_qm", "qm", "QM.bmp", "通过点取点或选取平面墙体完成线后生成墙面材料标注"));
            cat_2.Items.Add(new HailongItem("画单轴线", "画单轴线", "zhl_ax", "ax", "AX.bmp", "输入 “A” 可绘制弧轴线，输入 “S” 把线条转轴线"));
            cat_2.Items.Add(new HailongItem("轴线网格", "轴线网格", "zhl_xi", "xi", "xi.bmp", "注意：各尺寸间用空格分格开，同一方向有多个一样尺寸的话，可以用“n*尺寸”输入"));
            cat_2.Items.Add(new HailongItem("加轴线号", "加轴线号", "zhl_axx", "axx", "AXX.bmp", "选取轴线自动添加轴号和二道尺寸线"));
            cat_2.Items.Add(new HailongItem("划线插轴", "划线插轴", "zhl_cz", "cz", "CZ.bmp", "两点划线交点插入轴号并标注尺寸"));
            cat_2.Items.Add(new HailongItem("连续轴号", "连续轴号", "zhl_abc", "abc", "ABC.bmp", "按轴线连续绘制轴号和尺寸标注"));
            cat_2.Items.Add(new HailongItem("简单轴号", "简单轴号", "zhl_bc", "bc", "bc.bmp", "插入一个简单轴号"));
            cat_2.Items.Add(new HailongItem("轴号避让", "轴号避让", "zhl_axq", "axq", "AXQ.BMP", "轴号向一侧避让偏移"));
            cat_2.Items.Add(new HailongItem("轴号线长", "轴号线长", "zhl_axc", "axc", "AXC.BMP", "统一修改图纸所有轴号线的长度"));
            cat_2.Items.Add(new HailongItem("双线墙体", "双线墙体", "zhl_xq", "xq", "xq.bmp", "选项 “W” 设置墙层，输入 “T” 设置墙厚"));
            cat_2.Items.Add(new HailongItem("墙线修剪", "墙线修剪", "zhl_wx", "wx", "wx.bmp", "智能墙剪，墙柱交叉处自动修剪"));
            cat_2.Items.Add(new HailongItem("墙厚修改", "墙厚修改", "zhl_wac", "wac", "wac.bmp", "修正墙体厚度(自动调节门窗厚度)"));
            cat_2.Items.Add(new HailongItem("双线厚度", "双线厚度", "zhl_xxh", "xxh", "wac.bmp", "批量修改双线厚度(厚度0变成单线)"));
            cat_2.Items.Add(new HailongItem("线转墙体", "线转墙体", "zhl_l2w", "l2w", "l2w.bmp", "线转墙体，墙柱交接处自动修剪(忽略墙偏心距)"));
            cat_2.Items.Add(new HailongItem("净距偏移", "净距偏移", "zhl_wo", "wo", "WO.bmp", "平面墙体净距偏移"));
            cat_2.Items.Add(new HailongItem("插入柱子", "插入柱子", "zhl_czz", "czz", "czz.bmp", "绘制方柱子，清空柱子内的墙体"));
            cat_2.Items.Add(new HailongItem("插转角柱", "插转角柱", "zhl_zjz", "zjz", "ZJZ.BMP", "绘制L形转角柱子"));
            cat_2.Items.Add(new HailongItem("插Ｔ形柱", "插Ｔ形柱", "zhl_txz", "txz", "TXZ.BMP", "绘制T形柱子"));
            cat_2.Items.Add(new HailongItem("墙柱填充", "墙柱填充", "zhl_wwf", "wwf", "wwf.bmp", "实心填充墙、柱"));
            cat_2.Items.Add(new HailongItem("删墙门窗", "删墙门窗", "zhl_mme", "mme", "MME.bmp", "删除墙体、柱子、门窗并自动修补墙体，加载软件右键菜单后，选中门窗按右键，再按键盘“E”可以快速删除"));
            cat_2.Items.Add(new HailongItem("墙中轴线", "墙中轴线", "zhl_wwx", "wwx", "wwx.bmp", "绘制墙体中轴线"));
            cat_2.Items.Add(new HailongItem("整体家具", "整体家具", "zhl_jjj", "jjj", "jjj.bmp", "输入 “R” 调整空间、“S” 调整沙发、“T” 调整参数"));
            cat_2.Items.Add(new HailongItem("衣柜平面", "衣柜平面", "zhl_ygg", "ygg", "YGG.bmp", "可以通过点取点进行生成，也可以通过选取多义线进行生成，输入“S”出现可设置参数"));
            cat_2.Items.Add(new HailongItem("柜子平面", "柜子平面", "zhl_gzz", "gzz", "gzz.bmp", "点取两点后移动鼠标选择翻转的方向"));
            cat_2.Items.Add(new HailongItem("玻璃隔墙", "玻璃隔墙", "zhl_blgq", "blgq", "BLGQ.bmp", "绘制玻璃隔墙"));
            cat_2.Items.Add(new HailongItem("画折叠门", "画折叠门", "zhl_zdm", "zdm", "zdm.bmp", "画折叠门"));
            cat_2.Items.Add(new HailongItem("楼梯平面", "楼梯平面", "zhl_ltpm", "ltpm", "LTPM.bmp", "绘制各种楼梯平面"));
            cat_2.Items.Add(new HailongItem("电梯轿厢", "电梯轿厢", "zhl_dtjx", "dtjx", "dtjx.bmp", "绘制电梯轿厢平面"));
            cat_2.Items.Add(new HailongItem("入口标识", "入口标识", "zhl_rk", "rk", "RK.bmp", "先确定起点，再确定尖点"));
            cat_2.Items.Add(new HailongItem("布置空调", "布置空调", "zhl_ktt", "ktt", "KTT.bmp", "空调将布置在点取线条的光标拾取框一侧"));
            cat_2.Items.Add(new HailongItem("插入地漏", "插入地漏", "zhl_dl", "dl", "DL.bmp", "输入 “S” 切换圆形或方形地漏，移动光标调方向时输入“S”设置坡度"));
            cat_2.Items.Add(new HailongItem("窗帘平面", "窗帘平面", "zhl_clpm", "clpm", "clpm.bmp", "绘制平面窗帘"));
            list.Add(cat_2);

            // 分类 [04]: 立面功能
            var cat_3 = new HailongCategory("立面功能", "lm.bmp");
            cat_3.Items.Add(new HailongItem("天地脚线", "天地脚线", "zhl_djx", "djx", "xt.bmp", "快速绘制天花或地脚线转折连线"));
            cat_3.Items.Add(new HailongItem("一键柜子", "一键柜子", "zhl_ggz", "ggz", "GZ.bmp", "一键生成柜子平立剖及三维图"));
            cat_3.Items.Add(new HailongItem("立面框架", "立面框架", "zhl_lm", "lm", "lm.bmp", "可以在模型空间或布局空间快速生成四个立面框架，可从图面上获取平面完成线生成展开立面（可设置生成单个立面），插入的同时也可输入参数 X 或 D 动态调整比例"));
            cat_3.Items.Add(new HailongItem("立面框线", "立面框线", "zhl_kk", "kk", "KK.bmp", "绘制立面框线"));
            cat_3.Items.Add(new HailongItem("立面排图", "立面排图", "zhl_lpt", "lpt", "LPT.bmp", "把立面图重新套图框排版"));
            cat_3.Items.Add(new HailongItem("三维造型", "三维造型", "zhl_3w", "3w", "3w.bmp", "快速画三维造型线"));
            cat_3.Items.Add(new HailongItem("三维洞口", "三维洞口", "zhl_3d", "3d", "DK.bmp", "快速画三维洞口线"));
            cat_3.Items.Add(new HailongItem("三维拉伸", "三维拉伸", "zhl_ssw", "ssw", "ssw.bmp", "三维拉伸"));
            cat_3.Items.Add(new HailongItem("画轴侧图", "画轴侧图", "zhl_zc", "zc", "zc.bmp", "画轴侧图"));
            cat_3.Items.Add(new HailongItem("窗帘立面", "窗帘立面", "zhl_cll", "cll", "CLL.bmp", "绘制窗帘立面"));
            cat_3.Items.Add(new HailongItem("点位投影", "点位投影", "zhl_dty", "dty", "dty.bmp", "选中平面机电、给排水点图块后直接在立面生成并标注好尺寸"));
            cat_3.Items.Add(new HailongItem("画立面块", "画立面块", "zhl_ll", "ll", "ll.bmp", "选平面块生成立面块"));
            cat_3.Items.Add(new HailongItem("画立面门", "画立面门", "zhl_lmm", "lmm", "LMM.bmp", "绘制立面门"));
            cat_3.Items.Add(new HailongItem("画立面窗", "画立面窗", "zhl_lmc", "lmc", "LMC.bmp", "绘制画立面窗"));
            cat_3.Items.Add(new HailongItem("立面柜子", "立面柜子", "zhl_gz", "gz", "GZ.bmp", "绘制立面柜子（可点取闭合空间内生成，也可以框选矩形范围生成）"));
            cat_3.Items.Add(new HailongItem("立面衣柜", "立面衣柜", "zhl_yg", "yg", "YG.bmp", "绘制立面衣柜（可点取闭合空间内生成，也可以框选矩形范围生成）"));
            cat_3.Items.Add(new HailongItem("内弧造型", "内弧造型", "zhl_nh", "nh", "nh.bmp", "绘制内弧造型（常用于欧式门花样或天花造型）"));
            cat_3.Items.Add(new HailongItem("立面线条", "立面线条", "zhl_xt", "xt", "xt.bmp", "插入立面线条并把节点用辅助线相连"));
            cat_3.Items.Add(new HailongItem("淋浴隔断", "淋浴隔断", "zhl_gd", "gd", "gd.bmp", "快速绘制淋浴隔断平面立面剖面"));
            cat_3.Items.Add(new HailongItem("卫浴立面", "卫浴立面", "zhl_wl", "wl", "wl.bmp", "快速绘制卫浴立面"));
            cat_3.Items.Add(new HailongItem("灯光布置", "灯光布置", "zhl_dgg", "dgg", "DGG.bmp", "快速灯光布置"));
            cat_3.Items.Add(new HailongItem("玻璃窗户", "玻璃窗户", "zhl_ws", "ws", "ws.bmp", "绘制玻璃窗户剖面"));
            cat_3.Items.Add(new HailongItem("百页窗户", "百页窗户", "zhl_per", "per", "per.bmp", "绘制百页窗户剖面"));
            cat_3.Items.Add(new HailongItem("楼板加梁", "楼板加梁", "zhl_jl", "jl", "jl.bmp", "楼板添加过梁"));
            cat_3.Items.Add(new HailongItem("画洞口线", "画洞口线", "zhl_dk", "dk", "DK.bmp", "提示：可输入“ S ”进行设置，可通过两点框取生成、点取封闭范围生成和选取线条或圆生成"));
            cat_3.Items.Add(new HailongItem("画开启线", "画开启线", "zhl_kx", "kx", "kx.bmp", "画开启线"));
            cat_3.Items.Add(new HailongItem("矩形变形", "矩形变形", "zhl_bx", "bx", "BX.bmp", "矩形图案变形成异形的图案"));
            cat_3.Items.Add(new HailongItem("拉线踏步", "拉线踏步", "zhl_ste", "ste", "STE.bmp", "绘制一条横向的线条，与竖向相交的点作为踏步点，自动生成楼梯踏步"));
            cat_3.Items.Add(new HailongItem("栏杆扶手", "栏杆扶手", "zhl_fs", "fs", "fs.bmp", "绘制栏杆扶手"));
            cat_3.Items.Add(new HailongItem("立面分格", "立面分格", "zhl_eg", "eg", "eg.bmp", "EG_立面分格.gif"));
            cat_3.Items.Add(new HailongItem("幕墙立面", "幕墙立面", "zhl_ec", "ec", "ec.bmp", "绘制幕墙立面"));
            cat_3.Items.Add(new HailongItem("画弧面线", "画弧面线", "zhl_hx", "hx", "HX.bmp", "注意：选取的两根直线段必需是平行的"));
            list.Add(cat_3);

            // 分类 [05]: 剖面功能
            var cat_4 = new HailongCategory("剖面功能", "thh.bmp");
            cat_4.Items.Add(new HailongItem("一键门详图", "一键门详图", "zhl_mxt", "mxt", "mxt.bmp", "一键门详图"));
            cat_4.Items.Add(new HailongItem("阳角工具", "阳角工具", "zhl_yj", "yj", "yj.bmp", "阳角工具"));
            cat_4.Items.Add(new HailongItem("弧形天花", "弧形天花", "zhl_hth", "hth", "THPM.bmp", "选择弧形天花造型线快速绘制内部结构"));
            cat_4.Items.Add(new HailongItem("天花结构", "天花结构", "zhl_tgg", "tgg", "THPM.bmp", "选择天花造型完成线快速绘制内部结构"));
            cat_4.Items.Add(new HailongItem("天花转换层", "天花转换层", "zhl_zhc", "zhc", "THPM.bmp", "绘制天花转换层结构"));
            cat_4.Items.Add(new HailongItem("空调造型", "空调造型", "zhl_kt", "kt", "KT.bmp", "绘制空调及风口造型"));
            cat_4.Items.Add(new HailongItem("大样排图", "大样排图", "zhl_dpt", "dpt", "DPT.bmp", "对大样图按比例进行排图，并对局部大样添加索引"));
            cat_4.Items.Add(new HailongItem("轻钢龙骨", "轻钢龙骨", "zhl_lg", "lg", "LG.bmp", "布置天花轻钢龙骨"));
            cat_4.Items.Add(new HailongItem("龙骨结构", "龙骨结构", "zhl_lgg", "lgg", "lgg.bmp", "绘制龙骨结构（可点取闭合空间内生成，也可以框选矩形范围生成）"));
            cat_4.Items.Add(new HailongItem("抽屉剖面", "抽屉剖面", "zhl_ctt", "ctt", "CTT.bmp", "通过两点框取矩形范围生成抽屉剖面"));
            cat_4.Items.Add(new HailongItem("层板剖面", "层板剖面", "zhl_cbb", "cbb", "CBB.bmp", "通过两点框取矩形范围生成层板剖面"));
            cat_4.Items.Add(new HailongItem("暗藏灯槽", "暗藏灯槽", "zhl_edc", "edc", "EDC.bmp", "绘制暗藏灯槽剖面"));
            cat_4.Items.Add(new HailongItem("天花详图", "天花详图", "zhl_thh", "thh", "THH.bmp", "快速生成各种组合的天花详图"));
            cat_4.Items.Add(new HailongItem("天花剖面", "天花剖面", "zhl_thpm", "thpm", "THPM.bmp", "通过三点绘制石膏板天花剖面"));
            cat_4.Items.Add(new HailongItem("平板天花", "平板天花", "zhl_pb", "pb", "pb.bmp", "两点矩形范围绘制平板天花剖面"));
            cat_4.Items.Add(new HailongItem("墙体剖面", "墙体剖面", "zhl_pm", "pm", "pm.bmp", "绘制墙体剖面详图，从列表中选到相应的造型，点击设置按键还有更详细的设置"));
            cat_4.Items.Add(new HailongItem("木方结构", "木方结构", "zhl_mf", "mf", "MF.bmp", "通过点取闭合空间内生成，也可以框选矩形范围生成"));
            cat_4.Items.Add(new HailongItem("干挂石材", "干挂石材", "zhl_ggs", "ggs", "ggs.bmp", "绘制干挂石材及海棠角剖面"));
            cat_4.Items.Add(new HailongItem("墙板大样", "墙板大样", "zhl_qb", "qb", "QB.bmp", "绘制墙面木饰面板剖面"));
            cat_4.Items.Add(new HailongItem("木板剖面", "木板剖面", "zhl_mb", "mb", "MB.bmp", "绘制木板剖面"));
            cat_4.Items.Add(new HailongItem("夹板剖面", "夹板剖面", "zhl_jbb", "jbb", "JBB.bmp", "绘制夹板剖面（提示：输入“ S ”进行参数设置）"));
            cat_4.Items.Add(new HailongItem("画木地板", "画木地板", "zhl_mdb", "mdb", "MDB.bmp", "绘制木地板剖面（提示：输入“ S ”进行参数设置）"));
            cat_4.Items.Add(new HailongItem("玻璃剖面", "玻璃剖面", "zhl_bll", "bll", "BLL.bmp", "绘制玻璃板剖面（提示：输入“ S ”进行参数设置）"));
            cat_4.Items.Add(new HailongItem("石膏板墙", "石膏板墙", "zhl_sgb", "sgb", "SGB.bmp", "绘制平面轻钢龙骨石膏板隔墙"));
            cat_4.Items.Add(new HailongItem("画保温棉", "画保温棉", "zhl_bwm", "bwm", "BWM.bmp", "绘制保温棉"));
            cat_4.Items.Add(new HailongItem("画不锈钢", "画不锈钢", "zhl_bxg", "bxg", "bxg.bmp", "绘制不锈钢"));
            cat_4.Items.Add(new HailongItem("橱柜详图", "橱柜详图", "zhl_cgxt", "cgxt", "cgxt.bmp", "绘制橱柜详图"));
            cat_4.Items.Add(new HailongItem("衣柜详图", "衣柜详图", "zhl_ygxt", "ygxt", "ygxt.bmp", "绘制衣柜详图"));
            cat_4.Items.Add(new HailongItem("楼梯剖面", "楼梯剖面", "zhl_ltt", "ltt", "ltt.bmp", "绘制楼梯剖面，可选取平面楼梯自动生成楼梯剖面，不选平面楼梯再次回车可设置参数生成"));
            cat_4.Items.Add(new HailongItem("结构连线", "结构连线", "zhl_jg", "jg", "jG.bmp", "对两根多段线的节点进行横向连线"));
            cat_4.Items.Add(new HailongItem("型钢结构", "型钢结构", "zhl_gjg", "gjg", "gjg.bmp", "绘制型钢截面（提示：可双击类型字或列表项生成）"));
            cat_4.Items.Add(new HailongItem("软包剖面", "软包剖面", "zhl_rbm", "rbm", "rbm.bmp", "当框选的矩形范围宽小于高时为竖向，当宽大于高时为横向"));
            cat_4.Items.Add(new HailongItem("硬包剖面", "硬包剖面", "zhl_ybm", "ybm", "ybm.bmp", "当框选的矩形范围宽小于高时为竖向，当宽大于高时为横向"));
            cat_4.Items.Add(new HailongItem("墙面封板", "墙面封板", "zhl_fb", "fb", "fb.bmp", "绘制墙面封板，当框选的矩形范围宽小于高时为竖向，当宽大于高时为横向"));
            cat_4.Items.Add(new HailongItem("C 形封板", "C 形封板", "zhl_cfb", "cfb", "cfb.bmp", "绘制墙面C形封板，当框选的矩形范围宽小于高时为竖向，当宽大于高时为横向"));
            cat_4.Items.Add(new HailongItem("L 形封板", "L 形封板", "zhl_lfb", "lfb", "lfb.bmp", "绘制墙面L形封板，当框选的矩形范围宽小于高时为竖向，当宽大于高时为横向"));
            cat_4.Items.Add(new HailongItem("剖面墙体", "剖面墙体", "zhl_qt", "qt", "qt.bmp", "绘制剖面墙体，当框选的矩形范围宽小于高时为竖向，当宽大于高时为横向"));
            cat_4.Items.Add(new HailongItem("石材剖面", "石材剖面", "zhl_sm", "sm", "SM.bmp", "绘制石材地面剖面，标注引线和文字与框选的方向同"));
            cat_4.Items.Add(new HailongItem("瓷砖剖面", "瓷砖剖面", "zhl_zpm", "zpm", "ZPM.bmp", "绘制瓷砖剖面，标注引线和文字与框选的方向同"));
            cat_4.Items.Add(new HailongItem("实木地板", "实木地板", "zhl_dmm", "dmm", "dm.bmp", "绘制实木地板剖面，标注引线和文字与框选的方向同"));
            cat_4.Items.Add(new HailongItem("地毯剖面", "地毯剖面", "zhl_tm", "tm", "TM.bmp", "绘制地毯剖面，标注引线和文字与框选的方向同"));
            cat_4.Items.Add(new HailongItem("楼板剖面", "楼板剖面", "zhl_lb", "lb", "LB.bmp", "通过两点框取矩形范围绘制楼板剖面"));
            list.Add(cat_4);

            // 分类 [06]: 整装功能
            var cat_5 = new HailongCategory("整装功能", "cb.bmp");
            cat_5.Items.Add(new HailongItem("画轴侧图", "画轴侧图", "zhl_zc", "zc", "zc.bmp", "画轴侧图"));
            cat_5.Items.Add(new HailongItem("三维标注", "三维标注", "zhl_daa", "daa", "DA.bmp", "三维标注"));
            cat_5.Items.Add(new HailongItem("三维直线", "三维直线", "zhl_zca", "zca", "zca.bmp", "三维直线"));
            cat_5.Items.Add(new HailongItem("三维造型", "三维造型", "zhl_3w", "3w", "3w.bmp", "三维造型"));
            cat_5.Items.Add(new HailongItem("三维洞口", "三维洞口", "zhl_3d", "3d", "DK.bmp", "三维造型"));
            cat_5.Items.Add(new HailongItem("三维拉伸", "三维拉伸", "zhl_ssw", "ssw", "zc.bmp", "三维拉伸"));
            cat_5.Items.Add(new HailongItem("绘制层板", "绘制层板", "zhl_cb", "cb", "cb.bmp", "快速绘制偏移层板或等分层板"));
            cat_5.Items.Add(new HailongItem("自动单线", "自动单线", "zhl_c1", "c1", "c1.bmp", "移动光标快速在光标所在区域绘制【偏移】的单线"));
            cat_5.Items.Add(new HailongItem("两点单线", "两点单线", "zhl_c11", "c11", "c11.bmp", "指定两点快速绘制【偏移】的单线"));
            cat_5.Items.Add(new HailongItem("自动层板", "自动层板", "zhl_c2", "c2", "c2.bmp", "移动光标快速在光标所在区域绘制【偏移】的双线层板"));
            cat_5.Items.Add(new HailongItem("两点层板", "两点层板", "zhl_c22", "c22", "c22.bmp", "指定两点快速绘制【偏移】的双线层板"));
            cat_5.Items.Add(new HailongItem("均分层板", "均分层板", "zhl_c3", "c3", "c3.bmp", "移动光标快速在光标所在区域绘制【均分】的双线层板"));
            cat_5.Items.Add(new HailongItem("画吊衣杆", "画吊衣杆", "zhl_dyg", "dyg", "dyg.bmp", "画吊衣杆"));
            cat_5.Items.Add(new HailongItem("抽屉立面", "抽屉立面", "zhl_ct", "ct", "ct.bmp", "快速绘制抽屉立面"));
            cat_5.Items.Add(new HailongItem("智能拉伸", "智能拉伸", "zhl_xyy", "xyy", "xyy.bmp", "智能拉伸"));
            list.Add(cat_5);

            // 分类 [07]: 常用符号
            var cat_6 = new HailongCategory("常用符号", "tk.bmp");
            cat_6.Items.Add(new HailongItem("贴图大师", "贴图大师", "zhl_ddd", "ddd", "DDD.bmp", "贴图大师"));
            cat_6.Items.Add(new HailongItem("插入图框", "插入图框", "zhl_tk", "tk", "TK.bmp", "插入图框"));
            cat_6.Items.Add(new HailongItem("插入图名", "插入图名", "zhl_af", "af", "AF.bmp", "执行命令后可以选取图面上的图名进行修改，不选回车就是插入"));
            cat_6.Items.Add(new HailongItem("标高复制", "标高复制", "zhl_bgf", "bgf", "bgf.bmp", "复制图面上的标高符号会自动根据原有标高值，自动增加或减少"));
            cat_6.Items.Add(new HailongItem("局部索引", "局部索引", "zhl_sy", "sy", "SY.bmp", "放大局部区域并添加索引符号"));
            cat_6.Items.Add(new HailongItem("索引符号", "索引符号", "zhl_syy", "syy", "syy.bmp", "插入平面索引符号（提示：输入“ S ”进行参数设置）"));
            cat_6.Items.Add(new HailongItem("剖面索引", "剖面索引", "zhl_pp", "pp", "PP.bmp", "插入剖面索引（提示：输入“ S ”进行参数设置）"));
            cat_6.Items.Add(new HailongItem("画指北针", "画指北针", "zhl_zbz", "zbz", "ZBZ.bmp", "通过两点插入指北针符号"));
            cat_6.Items.Add(new HailongItem("绘制断线", "绘制断线", "zhl_dx", "dx", "DX.bmp", "输入选项“A”：设置相关参数；选项“X”：可切换为双线模式；选项“S”：可以把图面上的线条转成断线"));
            cat_6.Items.Add(new HailongItem("对称符号", "对称符号", "zhl_dcfh", "dcfh", "DCFH.bmp", "线型与配置里的中心线型一致"));
            cat_6.Items.Add(new HailongItem("指向箭号", "指向箭号", "zhl_jt", "jt", "JT.bmp", "绘制指向箭号"));
            cat_6.Items.Add(new HailongItem("中心符号", "中心符号", "zhl_cl", "cl", "cl.bmp", "插入装修中心线符号（线型与配置里的中心线型一致）"));
            cat_6.Items.Add(new HailongItem("转折面符", "转折面符", "zhl_zzm", "zzm", "ZZM.bmp", "插入转折面符号"));
            cat_6.Items.Add(new HailongItem("交叉矩形", "交叉矩形", "zhl_rc", "rc", "RC.bmp", "绘制交叉矩形（提示：输入“ S ”进行参数设置）"));
            cat_6.Items.Add(new HailongItem("尺寸木方", "尺寸木方", "zhl_rt", "rt", "RT.bmp", "绘制尺寸木方"));
            cat_6.Items.Add(new HailongItem("竖向长线", "竖向长线", "zhl_xx", "xx", "XX.bmp", "绘制竖向长线（归层打开后辅助线层将会自动打开）"));
            cat_6.Items.Add(new HailongItem("水平长线", "水平长线", "zhl_ss", "ss", "SS.bmp", "绘制水平长线"));
            cat_6.Items.Add(new HailongItem("画波浪线", "画波浪线", "zhl_blx", "blx", "BLX.bmp", "绘制波浪线"));
            cat_6.Items.Add(new HailongItem("索引方向", "索引方向", "zhl_syfx", "syfx", "SYFX.bmp", "插入四个方向索引号"));
            cat_6.Items.Add(new HailongItem("索引向 A", "索引向 A", "zhl_fxa", "fxa", "FXA.bmp", "插入索引A方向符号"));
            cat_6.Items.Add(new HailongItem("索引向 B", "索引向 B", "zhl_fxb", "fxb", "FXB.bmp", "插入索引B方向符号"));
            cat_6.Items.Add(new HailongItem("索引向 C", "索引向 C", "zhl_fxc", "fxc", "FXC.bmp", "插入索引C方向符号"));
            cat_6.Items.Add(new HailongItem("索引向 D", "索引向 D", "zhl_fxd", "fxd", "FXD.bmp", "插入索引D方向符号"));
            cat_6.Items.Add(new HailongItem("空心箭头", "空心箭头", "zhl_jtt", "jtt", "JTT.bmp", "绘制空心箭头（一般用于入口标识）"));
            cat_6.Items.Add(new HailongItem("画弯箭头", "画弯箭头", "zhl_wjt", "wjt", "WJT.bmp", "三点绘制弯箭头"));
            cat_6.Items.Add(new HailongItem("标注坐标", "标注坐标", "zhl_dy", "dy", "dy.bmp", "标注二维坐标点"));
            cat_6.Items.Add(new HailongItem("坡度符号", "坡度符号", "zhl_pd", "pd", "pd.bmp", "插入坡度符号"));
            cat_6.Items.Add(new HailongItem("圆管截面", "圆管截面", "zhl_gjm", "gjm", "gjm.bmp", "绘制圆管截面符"));
            list.Add(cat_6);

            // 分类 [08]: 文字工具
            var cat_7 = new HailongCategory("文字工具", "ed.bmp");
            cat_7.Items.Add(new HailongItem("编辑插入", "编辑插入", "zhl_ed", "ed", "ED.bmp", "修改或插入文字，执行命后空格两次是插入文字功能"));
            cat_7.Items.Add(new HailongItem("文字齐线", "文字齐线", "zhl_wzq", "wzq", "wd.bmp", "文字对齐线条"));
            cat_7.Items.Add(new HailongItem("改对齐点", "改对齐点", "zhl_ta", "ta", "TA.bmp", "文字位置不变，修改文字的对齐点"));
            cat_7.Items.Add(new HailongItem("查找连线", "查找连线", "zhl_tg", "tg", "TG.bmp", "文字快速查找连线，可以查找完全匹配内容也可以查找包含的内容"));
            cat_7.Items.Add(new HailongItem("递增数字", "递增数字", "zhl_sbb", "sbb", "SBB.bmp", "递增数字文字(包括字母和大写数字)"));
            cat_7.Items.Add(new HailongItem("递增复制", "递增复制", "zhl_ad", "ad", "AD.bmp", "文字或属性块连续增量拷贝"));
            cat_7.Items.Add(new HailongItem("参考更新", "参考更新", "zhl_cx", "cx", "CX.bmp", "参考更新文字"));
            cat_7.Items.Add(new HailongItem("方格居中", "方格居中", "zhl_tz", "tz", "TZ.bmp", "单行文字方格内居中（MTEXT文字需要炸开再进行操作）"));
            cat_7.Items.Add(new HailongItem("圆圈居中", "圆圈居中", "zhl_ts", "ts", "TS.bmp", "单行文字圆圈内居中（MTEXT文字需要炸开再进行操作）"));
            cat_7.Items.Add(new HailongItem("刷字内容", "刷字内容", "zhl_at", "at", "AT.bmp", "匹配文字、属性块、块内文字或标注内容一致"));
            cat_7.Items.Add(new HailongItem("空格独立", "空格独立", "zhl_ttd", "ttd", "ttd.bmp", "将文字按空格分离为单个文字而保持文字位置不变"));
            cat_7.Items.Add(new HailongItem("文字打断", "文字打断", "zhl_ttb", "ttb", "ttb.bmp", "打断单行文字（提示：输入“ S ”进行全部打断）"));
            cat_7.Items.Add(new HailongItem("粘连文字", "粘连文字", "zhl_tta", "tta", "tta.bmp", "可一个一个选取，也可同时框选多个单行文字"));
            cat_7.Items.Add(new HailongItem("字宽调整", "字宽调整", "zhl_ttw", "ttw", "ttw.bmp", "调整单行文字的间隔（提示：输入“ A ”进行字宽调整）"));
            cat_7.Items.Add(new HailongItem("排版对齐", "排版对齐", "zhl_ttp", "ttp", "ttp.bmp", "选取调整选项：字形(S)/高度(H)/角度(A)/对正方式(J)/宽高比(W)/移位(M)/连接(N)/重排(R))/拉线对齐(L)/拉线均分对齐(E)/加方框(F)/加圆框(C)/加椭圆(EL)"));
            cat_7.Items.Add(new HailongItem("内容替换", "内容替换", "zhl_fd", "fd", "fd.BMP", "文字内容替换或计算处理"));
            cat_7.Items.Add(new HailongItem("字体打包", "字体打包", "zhl_fdb", "fdb", "FDB.bmp", "提取图面中所用到的字体"));
            cat_7.Items.Add(new HailongItem("字体替换", "字体替换", "zhl_fr", "fr", "FR.bmp", "替换图中系统缺少的字体"));
            cat_7.Items.Add(new HailongItem("字体合并", "字体合并", "zhl_ft", "ft", "FT.bmp", "替换文字样式的字体"));
            cat_7.Items.Add(new HailongItem("加减双线", "加减双线", "zhl_jxx", "jxx", "jxx.bmp", "文字底部添加或删除双线"));
            cat_7.Items.Add(new HailongItem("分解属性", "分解属性", "zhl_fxx", "fxx", "FXX.bmp", "炸开属性块后的属性文本转单行文本"));
            cat_7.Items.Add(new HailongItem("转成多行", "转成多行", "zhl_ttm", "ttm", "ttm.bmp", "单行文本TEXT转成多行文本MTEXT"));
            cat_7.Items.Add(new HailongItem("格式化MT", "格式化MT", "zhl_str", "str", "STR.bmp", "格式化多行文本MTEXT"));
            cat_7.Items.Add(new HailongItem("修复镜像", "修复镜像", "zhl_mit", "mit", "MIT.bmp", "修复左右镜像的单行文本"));
            cat_7.Items.Add(new HailongItem("改大小写", "改大小写", "zhl_dxx", "dxx", "DXX.bmp", "对单行文本进行大小写转换"));
            cat_7.Items.Add(new HailongItem("字加平方", "字加平方", "zhl_pf", "pf", "PF.bmp", "单行文本或标注文字加平方符号"));
            cat_7.Items.Add(new HailongItem("文字竖排", "文字竖排", "zhl_sp", "sp", "SP.bmp", "单行文本分割成竖向或弧形排列"));
            cat_7.Items.Add(new HailongItem("内容对换", "内容对换", "zhl_dh", "dh", "DH.bmp", "文字内容对换(支持块内文字)"));
            cat_7.Items.Add(new HailongItem("简繁转换", "简繁转换", "zhl_zh", "zh", "ZH.bmp", "简体字和繁体字相互转换(支持材料块)"));
            cat_7.Items.Add(new HailongItem("中英转换", "中英转换", "zhl_yw", "yw", "YW.bmp", "中文字和英文字相互转换(支持材料块)"));
            cat_7.Items.Add(new HailongItem("中英翻译", "中英翻译", "zhl_fy", "fy", "FY.bmp", "文字中英翻译(注意:此功能必须联网才能运行)"));
            list.Add(cat_7);

            // 分类 [09]: 尺寸标注
            var cat_8 = new HailongCategory("尺寸标注", "da.bmp");
            cat_8.Items.Add(new HailongItem("三维标注", "三维标注", "zhl_daa", "daa", "DA.bmp", "提示：可输入“ S ”进行设置，DA_对齐标注.gif"));
            cat_8.Items.Add(new HailongItem("对齐标注", "对齐标注", "zhl_da", "da", "DA.bmp", "提示：可输入“ S ”进行设置，先选中标注再执行命令可以在原标注基础进行连续标注，标注过程也可以对标注进行断开"));
            cat_8.Items.Add(new HailongItem("垂直标注", "垂直标注", "zhl_dc", "dc", "dc.bmp", "垂直标注，提示：可输入“ S ”进行设置，先选中标注再执行命令可以在原标注基础进行连续标注，标注过程也可以对标注进行断开"));
            cat_8.Items.Add(new HailongItem("标注整理", "标注整理", "zhl_zl", "zl", "ZL.bmp", "整理一直线上重叠标注，把标注起点按顺序分开"));
            cat_8.Items.Add(new HailongItem("标注转换", "标注转换", "zhl_dz", "dz", "dc.bmp", "垂直标注与对齐标注相互转换"));
            cat_8.Items.Add(new HailongItem("尺寸间隔", "尺寸间隔", "zhl_dee", "dee", "dee.bmp", "修正一组尺寸标注基线间距"));
            cat_8.Items.Add(new HailongItem("文字避让", "文字避让", "zhl_df", "df", "DF.bmp", "标注文字自动避让，避免重叠"));
            cat_8.Items.Add(new HailongItem("文字归位", "文字归位", "zhl_gw", "gw", "GW.bmp", "尺寸标注文字归回原位"));
            cat_8.Items.Add(new HailongItem("标注检查", "标注检查", "zhl_dj", "dj", "dj.bmp", "红色显示被修改过的标注"));
            cat_8.Items.Add(new HailongItem("标注取整", "标注取整", "zhl_dqz", "dqz", "dqz.bmp", "尺寸标注值取整"));
            cat_8.Items.Add(new HailongItem("标注图层", "标注图层", "zhl_dcc", "dcc", "DCC.bmp", "设置生成的标注所在的图层（自动归层须打开，命令：GF）"));
            cat_8.Items.Add(new HailongItem("标注样式", "标注样式", "zhl_dsa", "dsa", "DSA.bmp", "打开标注样式管理器"));
            cat_8.Items.Add(new HailongItem("标注pl线", "标注pl线", "zhl_dp", "dp", "DP.bmp", "批量标注Pline线"));
            cat_8.Items.Add(new HailongItem("标注线长", "标注线长", "zhl_dll", "dll", "DLL.bmp", "批量标注多段线、线段长度"));
            cat_8.Items.Add(new HailongItem("外围标注", "外围标注", "zhl_bz", "bz", "BZ.bmp", "批量标注线条外围尺寸"));
            cat_8.Items.Add(new HailongItem("标注角度", "标注角度", "zhl_bzj", "bzj", "BZJ.bmp", "标注多段线角度"));
            cat_8.Items.Add(new HailongItem("三边画角", "三边画角", "zhl_sbj", "sbj", "sbj.bmp", "已知三边自动画三角形(用于检查墙角或家具角度)"));
            cat_8.Items.Add(new HailongItem("弧长标注", "弧长标注", "zhl_dar", "dar", "DAR.bmp", "标注弧线的长度，标注过程也可以对标注进行断开或标注分段数"));
            cat_8.Items.Add(new HailongItem("半径标注", "半径标注", "zhl_dhr", "dhr", "DhR.bmp", "批量标注圆或圆弧半径"));
            cat_8.Items.Add(new HailongItem("交点连标", "交点连标", "zhl_qd", "qd", "QD.bmp", "标注与两点相交线条的交点"));
            cat_8.Items.Add(new HailongItem("数值分段", "数值分段", "zhl_fdd", "fdd", "FDD.bmp", "标注数值等分表达"));
            cat_8.Items.Add(new HailongItem("标注等分", "标注等分", "zhl_dff", "dff", "DFF.bmp", "动态分裂等分标注"));
            cat_8.Items.Add(new HailongItem("移动界线", "移动界线", "zhl_dde", "dde", "dde.bmp", "移动角点不对齐的尺寸标注界线(可同时移动多个标注)"));
            cat_8.Items.Add(new HailongItem("标注断开", "标注断开", "zhl_db", "db", "DB.bmp", "按指定点对标注进行断开"));
            cat_8.Items.Add(new HailongItem("批量断开", "批量断开", "zhl_dbb", "dbb", "DB.bmp", "选取标注后点取两点，在与其相交的线的交点批量断开"));
            cat_8.Items.Add(new HailongItem("标注合并", "标注合并", "zhl_dv", "dv", "DV.bmp", "合并标注(提示：单选一个标注会自动合并所有相连的标注)"));
            cat_8.Items.Add(new HailongItem("列表更新", "列表更新", "zhl_eq", "eq", "eq.bmp", "按列表值修改标注文字，输入“S”出现可设置列表更新文字"));
            cat_8.Items.Add(new HailongItem("统一线长", "统一线长", "zhl_dtt", "dtt", "DTT.bmp", "统一标注两端界线的长度"));
            cat_8.Items.Add(new HailongItem("标注对齐", "标注对齐", "zhl_dt", "dt", "DT.bmp", "当提示点取标注线对齐点或标注放置位置时，敲空格键后不改标注线位置"));
            cat_8.Items.Add(new HailongItem("标注偏移", "标注偏移", "zhl_tq", "tq", "TQ.bmp", "标注及文字偏移"));
            cat_8.Items.Add(new HailongItem("外包尺寸", "外包尺寸", "zhl_dqq", "dqq", "DQQ.bmp", "批量对各组尺寸生成外包总尺寸"));
            cat_8.Items.Add(new HailongItem("锁定数值", "锁定数值", "zhl_sd", "sd", "SD.bmp", "锁定标注数值(让其数值始终不变)"));
            cat_8.Items.Add(new HailongItem("解锁数值", "解锁数值", "zhl_zs", "zs", "ZS.bmp", "对固定数值的标注进行解锁还原为原始数值"));
            cat_8.Items.Add(new HailongItem("尺寸删除", "尺寸删除", "zhl_ccd", "ccd", "CCD.bmp", "批量删除一组尺寸的总尺寸或分尺寸"));
            cat_8.Items.Add(new HailongItem("房间尺寸", "房间尺寸", "zhl_fc", "fc", "fc.bmp", "标注闭合范围空间的尺寸"));
            cat_8.Items.Add(new HailongItem("图块定位", "图块定位", "zhl_dg", "dg", "DG.bmp", "标注图块定位尺寸(提示：<空格回车=只标内空尺寸>)"));
            cat_8.Items.Add(new HailongItem("标注图块", "标注图块", "zhl_dbl", "dbl", "DBL.bmp", "标注图块的轮廓尺寸"));
            cat_8.Items.Add(new HailongItem("智能标注", "智能标注", "zhl_dq", "dq", "dq.bmp", "智能标注物体尺寸"));
            cat_8.Items.Add(new HailongItem("标注关联", "标注关联", "dimassoc 2 _dimreassociate", "gl2", "GL.bmp", "标注与物体造形进行关联"));
            cat_8.Items.Add(new HailongItem("取消关联", "取消关联", "dimassoc 1 _dimdisassociate", "gl1", "GL.bmp", "标注与物体造形取消关联"));
            list.Add(cat_8);

            // 分类 [10]: 引线其他
            var cat_9 = new HailongCategory("引线其他", "dd.bmp");
            cat_9.Items.Add(new HailongItem("快速引线", "快速引线", "zhl_dd", "dd", "DD.bmp", "双击列表内容即可插入，“英文”按钮前打勾点取列表内容时会自动添加对应英文，插入时可切换多种类型"));
            cat_9.Items.Add(new HailongItem("材料引线", "材料引线", "zhl_cdd", "cdd", "DD.bmp", "快速引线标注大样图材料"));
            cat_9.Items.Add(new HailongItem("块名引线", "块名引线", "zhl_bdd", "bdd", "PJJ.bmp", "快速批量引线标注块名"));
            cat_9.Items.Add(new HailongItem("批量引线", "批量引线", "zhl_et", "et", "ET.bmp", "快速批量引线标注< 立面材料 >并自动避让"));
            cat_9.Items.Add(new HailongItem("材料替换", "材料替换", "zhl_ddg", "ddg", "DDG.bmp", "批量替换材料块文字"));
            cat_9.Items.Add(new HailongItem("变尖箭头", "变尖箭头", "zhl_jjt", "jjt", "JJT.bmp", "变尖箭头"));
            cat_9.Items.Add(new HailongItem("对齐引线", "对齐引线", "zhl_ds", "ds", "DS.bmp", "批量对齐引线或尺寸标注"));
            cat_9.Items.Add(new HailongItem("上下对齐", "上下对齐", "zhl_dds", "dds", "dds.bmp", "【上下】对齐的标注、引线、文字或多重引线"));
            cat_9.Items.Add(new HailongItem("左右对齐", "左右对齐", "zhl_ddz", "ddz", "ddz.bmp", "【左右】对齐的标注、引线、文字或多重引线"));
            cat_9.Items.Add(new HailongItem("平均分布", "平均分布", "zhl_ddf", "ddf", "DDF.bmp", "上下平均分布的引线标注"));
            cat_9.Items.Add(new HailongItem("参照引线", "参照引线", "zhl_ddc", "ddc", "DDC.bmp", "参照引线文字或材料块并进行标注"));
            cat_9.Items.Add(new HailongItem("替换引线", "替换引线", "zhl_ddt", "ddt", "DDT.bmp", "批量替换引线文字或材料块"));
            cat_9.Items.Add(new HailongItem("添加引线", "添加引线", "zhl_dda", "dda", "dda.bmp", "添加材料标注引线的多个标注点，可选择引线、图块或文字进行添加"));
            cat_9.Items.Add(new HailongItem("引线镜像", "引线镜像", "zhl_jx", "jx", "JX.bmp", "选中引线后，原点不变左右镜像引线、材料块和文字"));
            cat_9.Items.Add(new HailongItem("左右变换", "左右变换", "zhl_zy", "zy", "ZY.bmp", "选中引线后，引线和文字(或材料块)左右变换"));
            cat_9.Items.Add(new HailongItem("加减节点", "加减节点", "zhl_adp", "adp", "ADP.bmp", "多义线或引线增删顶点"));
            cat_9.Items.Add(new HailongItem("分解引线", "分解引线", "zhl_fj", "fj", "FJ.bmp", "多重引线分解成普通引线"));
            cat_9.Items.Add(new HailongItem("还原引线", "还原引线", "zhl_hy", "hy", "hy.bmp", "复原炸开的圆点标注引线"));
            cat_9.Items.Add(new HailongItem("矩形变形", "矩形变形", "zhl_bx", "bx", "BX.bmp", "矩形图案变形成异形的图案"));
            cat_9.Items.Add(new HailongItem("四边变形", "四边变形", "zhl_sbx", "sbx", "sbx.bmp", "四边形变形成其他形状的四边形"));
            cat_9.Items.Add(new HailongItem("双线变形", "双线变形", "zhl_xbx", "xbx", "xbx.bmp", "矩形物体变形成以两弧线为基线的形状造形"));
            cat_9.Items.Add(new HailongItem("三维变形", "三维变形", "zhl_wbx", "wbx", "wbx.bmp", "四边形图案变形成三维状形体"));
            cat_9.Items.Add(new HailongItem("沿线变形", "沿线变形", "zhl_ybx", "ybx", "YBX.bmp", "图案沿线展开变形"));
            cat_9.Items.Add(new HailongItem("投影变形", "投影变形", "zhl_tbx", "tbx", "TBX.bmp", "投影变形(可用于制作弧形楼梯立面)"));
            cat_9.Items.Add(new HailongItem("拉伸辅线", "拉伸辅线", "zhl_sf", "sf", "sf.bmp", "对图块或Pline(多义)线拉伸出辅助线，输入“ F ”拉伸框选范围内的节点"));
            cat_9.Items.Add(new HailongItem("分解型线", "分解型线", "zhl_xxx", "xxx", "xxx.bmp", "把有造型的线条分解成一段段的直线段"));
            cat_9.Items.Add(new HailongItem("厚线加框", "厚线加框", "zhl_ppl", "ppl", "ppl.bmp", "将有厚度的多段线添加线框"));
            cat_9.Items.Add(new HailongItem("角平分线", "角平分线", "zhl_pfx", "pfx", "pfx.bmp", "生成两根线条的平分线或生成弧线中分线"));
            cat_9.Items.Add(new HailongItem("半圆跨线", "半圆跨线", "zhl_by", "by", "BY.bmp", "选取的线可以是直线、弧或多义线，输入选项“R”可以设置半圆跨线的半径"));
            cat_9.Items.Add(new HailongItem("删除跨线", "删除跨线", "zhl_eby", "eby", "EBY.bmp", "点取时一定要点取圆弧部分"));
            cat_9.Items.Add(new HailongItem("修改图章", "修改图章", "zhl_ew", "ew", "ew.bmp", "点击“词库”按钮，可对列表中的内容进行添加或修改"));
            cat_9.Items.Add(new HailongItem("焊缝符号", "焊缝符号", "zhl_hf", "hf", "HF.bmp", "点取两点后，移动鼠标选择翻转的方向或输入 A -> 中线偏移"));
            cat_9.Items.Add(new HailongItem("制作云线", "制作云线", "zhl_uu", "uu", "UU.bmp", "绘制方式有->画圆(A)/逐点绘制(D)/选边线(X)，输入“ S ”进行参数设置"));
            cat_9.Items.Add(new HailongItem("增强分解", "增强分解", "zhl_xd", "xd", "xd.bmp", "炸开无法分解的图块或多重无名块"));
            cat_9.Items.Add(new HailongItem("边界轮廓", "边界轮廓", "zhl_bre", "bre", "BRE.bmp", "生成物体(可以是多个物体)边界轮廓线，默认为生成最大外边界线，输入选项“D”则生成内边界线"));
            cat_9.Items.Add(new HailongItem("最小包围", "最小包围", "zhl_bw", "bw", "BW.bmp", "默认为生成最小方形包围框，输入选项“D”则生成圆形框"));
            cat_9.Items.Add(new HailongItem("绘制管线", "绘制管线", "zhl_ggg", "ggg", "ggg.bmp", "绘制管线"));
            cat_9.Items.Add(new HailongItem("环境分析", "环境分析", "zhl_hjfx", "hjfx", "HJFX.bmp", "绘制环境分析符号"));
            cat_9.Items.Add(new HailongItem("成片树木", "成片树木", "zhl_cpsm", "cpsm", "CPSM.bmp", "绘制成片树(灌)木"));
            cat_9.Items.Add(new HailongItem("提取线型", "提取线型", "zhl_tl", "tl", "tl.bmp", "提取并保存有造型的线条的线型数据"));
            cat_9.Items.Add(new HailongItem("制作线型", "制作线型", "zhl_llt", "llt", "llt.bmp", "制作字符线型"));
            list.Add(cat_9);

            // 分类 [11]: 填充图案
            var cat_10 = new HailongCategory("填充图案", "tc.bmp");
            cat_10.Items.Add(new HailongItem("图案符号", "图案符号", "zhl_tc", "tc", "TC.bmp", "快速插入填充图案和常用符号"));
            cat_10.Items.Add(new HailongItem("常用图案", "常用图案", "zhl_tcc", "tcc", "TCC.bmp", "绘制常用填充(可自行添加修改)"));
            cat_10.Items.Add(new HailongItem("地材标记", "地材标记", "zhl_dm", "dm", "TCC.bmp", "快速填充并插入标记块"));
            cat_10.Items.Add(new HailongItem("制作填充", "制作填充", "zhl_hm", "hm", "hm.bmp", "制作矩形对齐的填充图案pat文件"));
            cat_10.Items.Add(new HailongItem("线变填充", "线变填充", "zhl_lh", "lh", "lh.bmp", "线条转成填充图案"));
            cat_10.Items.Add(new HailongItem("提取图案", "提取图案", "zhl_ht", "ht", "HT.bmp", "提取图面上的图案保存为独立的填充文件，便于下次填充用"));
            cat_10.Items.Add(new HailongItem("参照填充", "参照填充", "zhl_gh", "gh", "GH.bmp", "参照填充对新的区域进行填充"));
            cat_10.Items.Add(new HailongItem("创建边界", "创建边界", "zhl_hb", "hb", "HB.bmp", "添加填充边界线"));
            cat_10.Items.Add(new HailongItem("填充开洞", "填充开洞", "zhl_hl", "hl", "HL.bmp", "可通过选取封闭的PL线开洞，也可以通过框取矩形或圆形开洞"));
            cat_10.Items.Add(new HailongItem("填充补洞", "填充补洞", "zhl_hd", "hd", "hd.bmp", "选取图案后再选取要补洞的边线"));
            cat_10.Items.Add(new HailongItem("填充旋转", "填充旋转", "zhl_hr", "hr", "HR.bmp", "旋转图案角度"));
            cat_10.Items.Add(new HailongItem("填充移动", "填充移动", "zhl_hv", "hv", "HV.bmp", "移动填充图案"));
            cat_10.Items.Add(new HailongItem("填充缩放", "填充缩放", "zhl_hs", "hs", "HS.bmp", "对填充比例进行缩放"));
            cat_10.Items.Add(new HailongItem("填充拉伸", "填充拉伸", "zhl_sv", "sv", "sv.bmp", "拉伸填充图案"));
            cat_10.Items.Add(new HailongItem("填充合并", "填充合并", "zhl_hj", "hj", "HJ.bmp", "如果合并的图案不一样，将以第一个图案填充为准"));
            cat_10.Items.Add(new HailongItem("填充分开", "填充分开", "zhl_hk", "hk", "HK.bmp", "分开多个不相连的填充图案"));
            cat_10.Items.Add(new HailongItem("变混凝土", "变混凝土", "zhl_hnt", "hnt", "HNT.bmp", "把实体填充变成混凝土填充"));
            cat_10.Items.Add(new HailongItem("修改起点", "修改起点", "zhl_hn", "hn", "HN.bmp", "修改填充起点"));
            cat_10.Items.Add(new HailongItem("移动节点", "移动节点", "zhl_hnn", "hnn", "hnn.bmp", "移动填充节点到新的指定点"));
            cat_10.Items.Add(new HailongItem("渐变填充", "渐变填充", "zhl_zb", "zb", "ZB.bmp", "绘制渐变填充图案，常用于表达不锈钢饰面"));
            cat_10.Items.Add(new HailongItem("实体转线", "实体转线", "zhl_stx", "stx", "stx.bmp", "细长实体转直线"));
            cat_10.Items.Add(new HailongItem("实体填充", "实体填充", "zhl_bbb", "bbb", "BBB.bmp", "绘制实体填充"));
            cat_10.Items.Add(new HailongItem("镜子表面", "镜子表面", "zhl_jm", "jm", "JM.bmp", "点取封闭空间绘制镜子图案，不点取再次回车可进行选取边界生成"));
            cat_10.Items.Add(new HailongItem("车 边 镜", "车 边 镜", "zhl_cbj", "cbj", "CBJ.bmp", "绘制车边镜填充图案"));
            cat_10.Items.Add(new HailongItem("软包表面", "软包表面", "zhl_bm", "bm", "BM.bmp", "绘制软包表面填充图案，输入 “S”，可设置填充比例"));
            cat_10.Items.Add(new HailongItem("夹板填充", "夹板填充", "zhl_jb", "jb", "JB.bmp", "对光标交点围成的矩形范围填充夹板图案，输入 “W”，可通过选取矩形进行填充"));
            cat_10.Items.Add(new HailongItem("大 芯 板", "大 芯 板", "zhl_dxb", "dxb", "DXB.bmp", "绘制大芯板剖面填充"));
            cat_10.Items.Add(new HailongItem("玻璃剖面", "玻璃剖面", "zhl_bl", "bl", "BL.bmp", "绘制玻璃剖面填充"));
            cat_10.Items.Add(new HailongItem("瓷砖剖面", "瓷砖剖面", "zhl_zm", "zm", "zm.bmp", "绘制瓷砖剖面填充"));
            cat_10.Items.Add(new HailongItem("隔墙海绵", "隔墙海绵", "zhl_gq", "gq", "GQ.bmp", "绘制隔墙海绵填充"));
            cat_10.Items.Add(new HailongItem("楼板剖面", "楼板剖面", "zhl_lb", "lb", "LB.bmp", "通过两点框取矩形范围绘制楼板剖面"));
            cat_10.Items.Add(new HailongItem("砖墙剖面", "砖墙剖面", "zhl_zq", "zq", "ZQ.bmp", "绘制砖墙剖面填充"));
            cat_10.Items.Add(new HailongItem("石材结构", "石材结构", "zhl_sm", "sm", "SM.bmp", "绘制石材地面剖面，标注引线和文字与框选的方向同"));
            cat_10.Items.Add(new HailongItem("瓷砖结构", "瓷砖结构", "zhl_zpm", "zpm", "ZPM.bmp", "绘制瓷砖剖面，标注引线和文字与框选的方向同"));
            cat_10.Items.Add(new HailongItem("地板结构", "地板结构", "zhl_dmm", "dmm", "dm.bmp", "绘制实木地板剖面，标注引线和文字与框选的方向同"));
            cat_10.Items.Add(new HailongItem("地毯结构", "地毯结构", "zhl_tm", "tm", "TM.bmp", "绘制地毯剖面，标注引线和文字与框选的方向同"));
            list.Add(cat_10);

            // 分类 [12]: 图块属性
            var cat_11 = new HailongCategory("图块属性", "aa.bmp");
            cat_11.Items.Add(new HailongItem("插入图块", "插入图块", "zhl_aa", "aa", "AA.bmp", "提示：从图面上Ctrl+C，在图库中Ctrl+V可以添加图块入库，也可以批量添加入库"));
            cat_11.Items.Add(new HailongItem("快速建块", "快速建块", "zhl_b", "b", "B.bmp", "输入“A”指定基点，“E”建立的图块是不可炸开的，“W”建立的图块是不可炸开的并且没有块名的，“D”可定义前缀和基点，“F”可分批组块，注：如果先选中物体再执行命令不会出现这些提示"));
            cat_11.Items.Add(new HailongItem("块加属性", "块加属性", "zhl_asx", "asx", "B.bmp", "对图块快速批量添加属性"));
            cat_11.Items.Add(new HailongItem("转普通块", "转普通块", "zhl_zpt", "zpt", "B.bmp", "动态块转普通块"));
            cat_11.Items.Add(new HailongItem("块变虚线", "块变虚线", "zhl_bxx", "bxx", "BTX.bmp", "自动炸开物体，并把所选对象制作成虚线图块"));
            cat_11.Items.Add(new HailongItem("顺序编号", "顺序编号", "zhl_sb", "sb", "SB.bmp", "在递增处打勾，每个值加“1”时就输入“1”，如果排序有误时，例如，图框不对齐，想把这种误差设置为一行时，可以设置比这个数略大点的数"));
            cat_11.Items.Add(new HailongItem("去除后缀", "去除后缀", "zhl_hz", "hz", "HZ.bmp", "图块改名，去除图块名称数字后缀"));
            cat_11.Items.Add(new HailongItem("更新属性", "更新属性", "zhl_gb", "gb", "GB.bmp", "更新属性块文字位置"));
            cat_11.Items.Add(new HailongItem("分离属性", "分离属性", "zhl_fsx", "fsx", "BE.bmp", "分离块属性字出来(不炸图块)"));
            cat_11.Items.Add(new HailongItem("改块属性", "改块属性", "zhl_eda", "eda", "GB.bmp", "改块属性"));
            cat_11.Items.Add(new HailongItem("参照变块", "参照变块", "zhl_bk", "bk", "bk.bmp", "把外部参照转变成图块插入本图中"));
            cat_11.Items.Add(new HailongItem("改到0层", "改到0层", "zhl_0", "0", "0.bmp", "说明：在不改颜色的列表中设置的颜色，在处理图块时将不改变图块内的这些颜色"));
            cat_11.Items.Add(new HailongItem("块增物体", "块增物体", "zhl_ba", "ba", "BA.bmp", "先选图块，再选择要添加到块内的对象"));
            cat_11.Items.Add(new HailongItem("块减物体", "块减物体", "zhl_be", "be", "BE.bmp", "删除所有同名图块里的物体（按Esc键退出）"));
            cat_11.Items.Add(new HailongItem("块内复制", "块内复制", "zhl_ccc", "ccc", "CCC.bmp", "图块内物体复制出来(可多选)"));
            cat_11.Items.Add(new HailongItem("彻底清除", "彻底清除", "zhl_eb", "eb", "EB.bmp", "注意：清除后此文件中被清除的图块将全部删除"));
            cat_11.Items.Add(new HailongItem("图块裁剪", "图块裁剪", "zhl_xc", "xc", "xc.bmp", "裁剪外部参照或图块"));
            cat_11.Items.Add(new HailongItem("图块齐线", "图块齐线", "zhl_qxx", "qxx", "QXX.bmp", "图块对齐直线"));
            cat_11.Items.Add(new HailongItem("改名或点", "改名或点", "zhl_ab", "ab", "AB.bmp", "修改图块名称或插入点，按钮“改单个块名”可单独只对此图块进行修改名称的操作"));
            cat_11.Items.Add(new HailongItem("批量改名", "批量改名", "zhl_abb", "abb", "AB.bmp", "点击按钮“选取(&S)”可从图面上获取图块名称，并在列表中亮显"));
            cat_11.Items.Add(new HailongItem("批改基点", "批改基点", "zhl_bbp", "bbp", "BBP.bmp", "可同时修改多个不同名称的图块的基点"));
            cat_11.Items.Add(new HailongItem("改块特性", "改块特性", "zhl_btx", "btx", "BTX.bmp", "可对图块进行“图层”、“颜色”、“线型”、“线型比例”的修改，在要修改的相应项前面打勾进行修改"));
            cat_11.Items.Add(new HailongItem("图块翻转", "图块翻转", "zhl_fz", "fz", "FZ.bmp", "选择对象后移动鼠标选择翻转的方向"));
            cat_11.Items.Add(new HailongItem("图块变形", "图块变形", "zhl_xy", "xy", "XY.bmp", "图块X(横向)方向和Y(竖向)方向不等比缩放"));
            cat_11.Items.Add(new HailongItem("旋转图块", "旋转图块", "zhl_rb", "rb", "RB.bmp", "按空格键或回车键是修改角度，也可以输入“B”进行批量改块比例"));
            cat_11.Items.Add(new HailongItem("选取旋转", "选取旋转", "zhl_bbr", "bbr", "RB.bmp", "将选中的块以各自的插入点为中心旋转指定的角度"));
            cat_11.Items.Add(new HailongItem("边界轮廓", "边界轮廓", "zhl_bre", "bre", "BRE.bmp", "生成物体(可以是多个物体)边界轮廓线，默认为生成最大外边界线，输入选项“D”则生成内边界线"));
            cat_11.Items.Add(new HailongItem("图块替换", "图块替换", "zhl_bt", "bt", "BT.bmp", "把图中同一名称的图块全部替换成另一名称的图块"));
            cat_11.Items.Add(new HailongItem("选择替换", "选择替换", "zhl_btt", "btt", "BTT.bmp", "此功能只会替换掉所选中的图块(可以是多个不同名称的图块)"));
            cat_11.Items.Add(new HailongItem("同名连线", "同名连线", "zhl_lx", "lx", "LX.bmp", "点取要从某个图块的位置开始连线后，可输入相应选项->减少弯度(S)/加大弯度(D)/切换样式(F)"));
            cat_11.Items.Add(new HailongItem("炸各种块", "炸各种块", "zhl_xa", "xa", "XA.bmp", "炸属性块或嵌套块"));
            cat_11.Items.Add(new HailongItem("搜集图块", "搜集图块", "zhl_sjb", "sjb", "SJB.bmp", "把当前图纸中所有图块，输出为独立的图块文件"));
            cat_11.Items.Add(new HailongItem("图块转换", "图块转换", "zhl_nm", "nm", "NM.bmp", "输入选项“A”可切换有名块->无名块的相互转换"));
            cat_11.Items.Add(new HailongItem("多重转换", "多重转换", "ZHL_BN", "bn", "NM.bmp", "多重图块转换成正常图块"));
            cat_11.Items.Add(new HailongItem("转属性块", "转属性块", "zhl_zsx", "zsx", "zsx.bmp", "选取单行文字(可以多个)和其他物体一起组成属性块"));
            cat_11.Items.Add(new HailongItem("块变可炸", "块变可炸", "zhl_nnn", "nnn", "NNN.bmp", "使图中所有图块变成 <可以炸开>"));
            cat_11.Items.Add(new HailongItem("生成遮罩", "生成遮罩", "zhl_wq", "wq", "WQ.bmp", "选取边界或图块生成遮罩块(也可取消图块遮挡)"));
            cat_11.Items.Add(new HailongItem("属性隐藏", "属性隐藏", "zhl_ash", "ash", "ash.bmp", "显示或者隐藏图块中的属性文字"));
            cat_11.Items.Add(new HailongItem("匹配比例", "匹配比例", "zhl_bsc", "bsc", "BSC.bmp", "把同名图块的比例刷成一样"));
            cat_11.Items.Add(new HailongItem("匹配角度", "匹配角度", "zhl_bro", "bro", "bro.bmp", "把同名图块的角度刷成一样"));
            cat_11.Items.Add(new HailongItem("炸属性块", "炸属性块", "zhl_sxx", "sxx", "SXX.bmp", "炸开属性块(属性文字内容不变)"));
            cat_11.Items.Add(new HailongItem("前置图块", "前置图块", "zhl_bff", "bff", "BFF.bmp", "把图块放置到最前面"));
            cat_11.Items.Add(new HailongItem("后置图块", "后置图块", "zhl_bww", "bww", "BWW.bmp", "把图块放置到最后面"));
            cat_11.Items.Add(new HailongItem("属性管理", "属性管理", "battman", "", "BAT.bmp", ""));
            list.Add(cat_11);

            // 分类 [13]: 线条工具
            var cat_12 = new HailongCategory("线条工具", "tx.bmp");
            cat_12.Items.Add(new HailongItem("暗藏灯带", "暗藏灯带", "zhl_edd", "edd", "edd.bmp", "批量偏移线条为灯带"));
            cat_12.Items.Add(new HailongItem("斜线改正", "斜线改正", "zhl_xgz", "xgz", "XGZ.bmp", "直线或多义线的斜线改为水平或垂直"));
            cat_12.Items.Add(new HailongItem("修改线型", "修改线型", "zhl_gxx", "gxx", "gxx.bmp", "修改线型或选图案进行填充，设置与DE同步"));
            cat_12.Items.Add(new HailongItem("两线中线", "两线中线", "zhl_zz", "zz", "ZZ.bmp", "创建两平行线的中线"));
            cat_12.Items.Add(new HailongItem("画中心线", "画中心线", "zhl_zx", "zx", "zx.bmp", "生成物体十字中心线(可以是多个物体)"));
            cat_12.Items.Add(new HailongItem("等分线段", "等分线段", "zhl_fv", "fv", "FV.bmp", "选取直线段进行等分"));
            cat_12.Items.Add(new HailongItem("等分连线", "等分连线", "zhl_dfx", "dfx", "dfx.bmp", "选取直线段进行等分"));
            cat_12.Items.Add(new HailongItem("焊接线条", "焊接线条", "zhl_j", "j", "J.bmp", "输入模糊距离对线条进行焊接，只选一线条时，自动连接与此线条相连的线"));
            cat_12.Items.Add(new HailongItem("修改线宽", "修改线宽", "zhl_pw", "pw", "PW.bmp", "统一修改所选线的宽度"));
            cat_12.Items.Add(new HailongItem("虚实互换", "虚实互换", "zhl_xg", "xg", "xg.bmp", "线条虚实相互转换"));
            cat_12.Items.Add(new HailongItem("变短虚线", "变短虚线", "zhl_nn", "nn", "NN.bmp", "提示：选中的对象如果是图块，会自动对图块进行改名，不影响其他的同名图块"));
            cat_12.Items.Add(new HailongItem("线条比例", "线条比例", "zhl_chs", "chs", "CHS.bmp", "拖动光标调整线条比例，也可输入相应的选项修改线型"));
            cat_12.Items.Add(new HailongItem("动态圆角", "动态圆角", "zhl_dfr", "dfr", "dfr.bmp", "拖动光标或输入倒圆角半径"));
            cat_12.Items.Add(new HailongItem("双向偏移", "双向偏移", "zhl_qq", "qq", "QQ.bmp", "双向偏移或多重偏移复制"));
            cat_12.Items.Add(new HailongItem("边界偏移", "边界偏移", "zhl_bq", "bq", "bq.bmp", "偏移边界线，并把生成的线放置在当前层"));
            cat_12.Items.Add(new HailongItem("偏移椭圆", "偏移椭圆", "zhl_qe", "qe", "QE.bmp", "偏移椭圆为椭圆对象"));
            cat_12.Items.Add(new HailongItem("改线起点", "改线起点", "zhl_xqd", "xqd", "XQD.bmp", "改线起点"));
            cat_12.Items.Add(new HailongItem("反转节点", "反转节点", "zhl_fx", "fx", "xfx.bmp", "标注文字上下翻转（反向）"));
            cat_12.Items.Add(new HailongItem("加减节点", "加减节点", "zhl_adp", "adp", "ADP.bmp", "多义线或引线增删顶点"));
            cat_12.Items.Add(new HailongItem("优化节点", "优化节点", "zhl_yh", "yh", "YH.BMP", "优化多段线子线段多余的节点"));
            cat_12.Items.Add(new HailongItem("曲线闭合", "曲线闭合", "zhl_a2c", "a2c", "A2C.bmp", "对弧、曲线、椭圆弧进行封闭连接处理"));
            cat_12.Items.Add(new HailongItem("改子线段", "改子线段", "zhl_sed", "sed", "sed.bmp", "局部修改多义线的子线段"));
            cat_12.Items.Add(new HailongItem("交点打断", "交点打断", "zhl_bb", "bb", "BB.bmp", "在线条交点处断开"));
            cat_12.Items.Add(new HailongItem("连接断线", "连接断线", "zhl_lj", "lj", "LJ.bmp", "批量连接断线、断圆弧线"));
            cat_12.Items.Add(new HailongItem("批量倒角", "批量倒角", "zhl_wf", "wf", "WF.bmp", "同时连接两组线段进行批量倒角"));
            cat_12.Items.Add(new HailongItem("线条抖动", "线条抖动", "zhl_ha", "ha", "ha.bmp", "线条转换为手绘效果(线条抖动)"));
            cat_12.Items.Add(new HailongItem("曲线合并", "曲线合并", "zhl_jspl", "jspl", "JSPL.bmp", "合并两Spline曲线"));
            cat_12.Items.Add(new HailongItem("弧曲转线", "弧曲转线", "zhl_asp", "asp", "asp.bmp", "圆弧、椭圆或Spline线转成 “直线段” 组成的Pline线"));
            cat_12.Items.Add(new HailongItem("等弦分段", "等弦分段", "zhl_dfd", "dfd", "DFD.bmp", "等弦分段曲线"));
            cat_12.Items.Add(new HailongItem("椭圆转弧", "椭圆转弧", "zhl_e2p", "e2p", "E2P.bmp", "用圆弧组成的多段线模拟椭圆"));
            cat_12.Items.Add(new HailongItem("曲线转pl", "曲线转pl", "zhl_s2p", "s2p", "S2P.bmp", "曲线spline、椭圆ellipse转多义线pline"));
            cat_12.Items.Add(new HailongItem("直线转弧", "直线转弧", "zhl_l2a", "l2a", "l2a.bmp", "直线或多义线的直线段转弧线"));
            cat_12.Items.Add(new HailongItem("特殊线段", "特殊线段", "zhl_tsx", "tx", "TX.bmp", "绘制特殊线段"));
            cat_12.Items.Add(new HailongItem("改弧半径", "改弧半径", "zhl_cir", "cir", "CIR.bmp", "改圆或弧的半径"));
            cat_12.Items.Add(new HailongItem("画垂直线", "画垂直线", "zhl_czx", "czx", "CZX.bmp", "绘制线条的垂直长线"));
            cat_12.Items.Add(new HailongItem("两端延长", "两端延长", "zhl_exx", "exx", "exx.bmp", "对直线段进行两端延长"));
            cat_12.Items.Add(new HailongItem("双线厚度", "双线厚度", "zhl_xxh", "xxh", "wac.bmp", "对两线净宽进行修改"));
            list.Add(cat_12);

            // 分类 [14]: 编辑修改
            var cat_13 = new HailongCategory("编辑修改", "qz.bmp");
            cat_13.Items.Add(new HailongItem("类型选择", "类型选择", "zhl_fa", "fa", "fa.bmp", "按类型选取物体"));
            cat_13.Items.Add(new HailongItem("按色选择", "按色选择", "zhl_sz", "sz", "SZ.bmp", "按颜色选择物体"));
            cat_13.Items.Add(new HailongItem("快速选择", "快速选择", "zhl_az", "az", "AZ.bmp", "根据条件快速选择物体，选物体并弹出对话框后输入“S”(或“A”)可以快速选中物体"));
            cat_13.Items.Add(new HailongItem("坐标归零", "坐标归零", "zhl_z0", "z0", "Z0.bmp", "把立体的线条拍平到二维平面"));
            cat_13.Items.Add(new HailongItem("两点等分", "两点等分", "zhl_fff", "fff", "FFF.bmp", "点取两点，用无限长线对两点范围进行等分"));
            cat_13.Items.Add(new HailongItem("等分曲线", "等分曲线", "zhl_ffx", "ffx", "FFX.bmp", "提示：勾取对话框右下角“同时打断曲线”，可以添加等分线的同时对曲线进行打断"));
            cat_13.Items.Add(new HailongItem("边界剪切", "边界剪切", "zhl_er", "er", "ER.bmp", "以线、字或图像为界，对一侧的线条快速裁剪，提示：在第一步选取边界时，此边界也可以是一个图块内的任意线"));
            cat_13.Items.Add(new HailongItem("中间裁剪", "中间裁剪", "zhl_ea", "ea", "EA.bmp", "快速裁剪两根线之间的线条，这两根线可以是直线也可以是曲线"));
            cat_13.Items.Add(new HailongItem("井字修剪", "井字修剪", "zhl_tw", "tw", "TW.bmp", "修剪井字内的线条"));
            cat_13.Items.Add(new HailongItem("间隔修剪", "间隔修剪", "zhl_xj", "xj", "xj.bmp", "修剪间隔交点内的线条"));
            cat_13.Items.Add(new HailongItem("增强修剪", "增强修剪", "zhl_t", "t", "T.bmp", "自动修剪光标经过的子线段"));
            cat_13.Items.Add(new HailongItem("隐藏同色", "隐藏同色", "zhl_tts", "tts", "tts.bmp", "隐藏屏幕显示(或全图)的同色物体"));
            cat_13.Items.Add(new HailongItem("孤立同色", "孤立同色", "zhl_ttg", "ttg", "ttg.bmp", "隐藏屏幕显示(或全图)的同色物体"));
            cat_13.Items.Add(new HailongItem("隐藏其他", "隐藏其他", "zhl_ty", "ty", "TY.bmp", "选择要留下的图元，余下的将被临时隐藏"));
            cat_13.Items.Add(new HailongItem("显示隐藏", "显示隐藏", "zhl_yt", "yt", "YT.bmp", "显示所有隐藏了的物件"));
            cat_13.Items.Add(new HailongItem("消除重复", "消除重复", "zhl_ok", "ok", "ok.bmp", "可以是线条、文字、二维实体或图块，按键盘\"S\"=设置精度、\"D\"=线条快速消重"));
            cat_13.Items.Add(new HailongItem("排列对齐", "排列对齐", "zhl_ao", "ao", "AO.bmp", "排列对齐物体"));
            cat_13.Items.Add(new HailongItem("沿线布置", "沿线布置", "zhl_yx", "yx", "YX.bmp", "1、选中要布置对象 2、选中路径 3、移动光标确定排布位置"));
            cat_13.Items.Add(new HailongItem("重新生成", "重新生成", "zhl_re", "re", "RE.bmp", "重新生成屏幕上所见物体"));
            cat_13.Items.Add(new HailongItem("再次移动", "再次移动", "zhl_vf", "vf", "VF.BMP", "移动上次被选中过的物体"));
            cat_13.Items.Add(new HailongItem("多重复制", "多重复制", "zhl_cc", "cc", "CC.bmp", "多次复制所选取的对象"));
            cat_13.Items.Add(new HailongItem("阵列复制", "阵列复制", "zhl_cv", "cv", "CV.bmp", "按指定方向或双向复制所选取的对象，按键盘\"A\"=范围数量、\"S\"=延伸数量、\"X\"=双向复制、\"D\"=递增"));
            cat_13.Items.Add(new HailongItem("选择打断", "选择打断", "zhl_brr", "brr", "brr.bmp", "选择物体或绘制直线，将与其相交的所有物体打断"));
            cat_13.Items.Add(new HailongItem("矩形透视", "矩形透视", "zhl_lr", "lr", "LR.bmp", "把矩形转成45度透视平行四边形"));
            cat_13.Items.Add(new HailongItem("复制旋转", "复制旋转", "zhl_cr", "cr", "CR.bmp", "图块复制+旋转+镜像物体"));
            cat_13.Items.Add(new HailongItem("复制镜像", "复制镜像", "zhl_ca", "ca", "CA.bmp", "多对像复制+镜像+旋转"));
            cat_13.Items.Add(new HailongItem("框框剪除", "框框剪除", "zhl_tee", "tee", "TEE.bmp", "多边形及文字内的直线批量修剪"));
            cat_13.Items.Add(new HailongItem("选集居中", "选集居中", "zhl_zzz", "zzz", "zzz.bmp", "选择集在封闭区域内居中"));
            cat_13.Items.Add(new HailongItem("偏至当前", "偏至当前", "zhl_qa", "qa", "QA.bmp", "偏移线条并改为当前层"));
            list.Add(cat_13);

            // 分类 [15]: 群组布局
            var cat_14 = new HailongCategory("群组布局", "ggf.bmp");
            cat_14.Items.Add(new HailongItem("群组开关", "群组开关", "zhl_ggf", "ggf", "GGF.bmp", ""));
            cat_14.Items.Add(new HailongItem("建立群组", "建立群组", "zhl_gc", "gc", "GC.bmp", "建立一个群组集合，方便下次选取，群组只对当前文件有效"));
            cat_14.Items.Add(new HailongItem("组加实体", "组加实体", "zhl_ga", "ga", "GA.bmp", "一个群组添加物体进来"));
            cat_14.Items.Add(new HailongItem("组减实体", "组减实体", "zhl_gy", "gy", "GY.bmp", "一个群组剔除物体出去"));
            cat_14.Items.Add(new HailongItem("分解所有", "分解所有", "zhl_gt", "gt", "GT.bmp", "分解所有群组，所有群组都将失效"));
            cat_14.Items.Add(new HailongItem("组管理器", "组管理器", "zhl_gp", "gp", "GP.bmp", ""));
            cat_14.Items.Add(new HailongItem("视口模型", "视口模型", "zhl_vvc", "vvc", "vvc.bmp", "布局视口线复制到模型空间"));
            cat_14.Items.Add(new HailongItem("视口管理", "视口管理", "zhl_vg", "vg", "VG.BMP", ""));
            cat_14.Items.Add(new HailongItem("比例修改", "比例修改", "zhl_de", "de", "DE.bmp", ""));
            cat_14.Items.Add(new HailongItem("退出视口", "退出视口", "zhl_aq", "aq", "aq.bmp", ""));
            cat_14.Items.Add(new HailongItem("线变视口", "线变视口", "zhl_lw", "lw", "LW.bmp", "将封闭的多段线转成视口线"));
            cat_14.Items.Add(new HailongItem("布局存档", "布局存档", "zhl_bcd", "bcd", "BCD.bmp", "每个布局保存为单独的文件"));
            cat_14.Items.Add(new HailongItem("查询比例", "查询比例", "zhl_gv", "gv", "GV.bmp", ""));
            cat_14.Items.Add(new HailongItem("无缝转换", "无缝转换", "zhl_mx", "mx", "MX.bmp", "把视口模型空间物体位置不变移到布局中(或相反操作)"));
            cat_14.Items.Add(new HailongItem("新建视口", "新建视口", "zhl_vw", "vw", "vw.bmp", "新建矩形视口"));
            cat_14.Items.Add(new HailongItem("锁定视口", "锁定视口", "zhl_sw", "sw", "SW.bmp", "【锁定】视口比例(光标在视口内时直接执行即可)"));
            cat_14.Items.Add(new HailongItem("解锁视口", "解锁视口", "zhl_fw", "fw", "FW.bmp", "【解锁】视口比例(光标在视口内时直接执行即可)"));
            cat_14.Items.Add(new HailongItem("关闭视口", "关闭视口", "zhl_cw", "cw", "CW.bmp", "【关闭】所选择的视口(隐藏视口内物体)"));
            cat_14.Items.Add(new HailongItem("关闭所有", "关闭所有", "zhl_cww", "cww", "CWW.bmp", "【关闭】所有视口(所有视口内物体全部隐藏)"));
            cat_14.Items.Add(new HailongItem("显示视口", "显示视口", "zhl_aw", "aw", "AW.bmp", "【打开】所选择视口(显示视口内物体)"));
            cat_14.Items.Add(new HailongItem("显示所有", "显示所有", "zhl_aww", "aww", "AWW.bmp", "【打开】所有视口(所有视口内物体全部显示)"));
            cat_14.Items.Add(new HailongItem("切割视口", "切割视口", "zhl_qg", "qg", "QG.bmp", "切割矩形视口"));
            cat_14.Items.Add(new HailongItem("对齐视口", "对齐视口", "zhl_vq", "vq", "vq.bmp", "对齐多个视口内容(先选主视口)"));
            cat_14.Items.Add(new HailongItem("平移视口", "平移视口", "zhl_vp", "vp", "vp.bmp", "把视口内的点对齐布局对齐点(平移视口视图)"));
            cat_14.Items.Add(new HailongItem("图层变色", "图层变色", "zhl_vb", "vb", "vb.bmp", "改变视口内图层颜色(不影响其他视口图层颜色)"));
            cat_14.Items.Add(new HailongItem("还原颜色", "还原颜色", "zhl_vh", "vh", "vh.bmp", "还原视口内图层的颜色"));
            cat_14.Items.Add(new HailongItem("图框拆分", "图框拆分", "zhl_kf", "kf", "kf.bmp", "选图框拆分为布局(每个图框一个布局)"));
            cat_14.Items.Add(new HailongItem("图框布局", "图框布局", "zhl_bj", "bj", "bj.bmp", "选图框分为布局(每个图框一个布局)"));
            cat_14.Items.Add(new HailongItem("布局合并", "布局合并", "zhl_v1", "v1", "V1.bmp", "多个布局合并为一个布局"));
            cat_14.Items.Add(new HailongItem("布局改名", "布局改名", "zhl_vm", "vm", "vm.bmp", "批量改布局名"));
            list.Add(cat_14);

            // 分类 [16]: 图层工具
            var cat_15 = new HailongCategory("图层工具", "qw.bmp");
            cat_15.Items.Add(new HailongItem("穿透冻结", "Freeze Layer", "XDJ", "XDJ", "djc.bmp", "穿透视口与图块深度冻结", true));
            cat_15.Items.Add(new HailongItem("逐步解冻", "Thaw Layer Step", "XJD", "XJD", "jd.bmp", "按冻结历史逐步解冻", true));
            cat_15.Items.Add(new HailongItem("一键恢复", "Restore Layers", "XHF", "XHF", "t8.bmp", "恢复冻结前图层状态", true));
            cat_15.Items.Add(new HailongItem("极速图层合并", "Fast Merge Layer", "XJH", "XJH", "00.bmp", "秒级深度合并图层", true));
            cat_15.Items.Add(new HailongItem("图层合并面板", "Layer Merge Panel", "XHB", "XHB", "00.bmp", "可视化穿透合并面板", true));
            cat_15.Items.Add(new HailongItem("图块归0层", "Block to Layer 0", "B0", "B0", "0.bmp", "图块内部图元归0层", true));
            cat_15.Items.Add(new HailongItem("属性文字平移", "Move Att Text", "MW", "MW", "ed.bmp", "同名文字联动平移", true));
            cat_15.Items.Add(new HailongItem("--", "", "", "", "", "", false, true));
            cat_15.Items.Add(new HailongItem("图层改名", "图层改名", "zhl_gm", "gm", "AS.bmp", ""));
            cat_15.Items.Add(new HailongItem("图层配置", "图层配置", "zhl_gtc", "gtc", "edwg.bmp", ""));
            cat_15.Items.Add(new HailongItem("切换图层", "切换图层", "zhl_ttc", "ttc", "ttc.bmp", "切换到列表中的图层或把物体改到列表中的图层"));
            cat_15.Items.Add(new HailongItem("图层控制", "图层控制", "zhl_qw", "qw", "QW.bmp", ""));
            cat_15.Items.Add(new HailongItem("图层管理", "图层管理", "zhl_as", "as", "AS.bmp", ""));
            cat_15.Items.Add(new HailongItem("关闭图层", "关闭图层", "zhl_`", "`", "`.bmp", ""));
            cat_15.Items.Add(new HailongItem("关其它层", "关其它层", "zhl_d", "d", "D.bmp", ""));
            cat_15.Items.Add(new HailongItem("打开图层", "打开图层", "zhl_dkk", "dkk", "DKK.bmp", ""));
            cat_15.Items.Add(new HailongItem("打开所有", "打开所有", "zhl_dka", "dka", "dka.bmp", ""));
            cat_15.Items.Add(new HailongItem("锁定图层", "锁定图层", "zhl_sdc", "sdc", "SDC.bmp", ""));
            cat_15.Items.Add(new HailongItem("锁其他层", "锁其他层", "zhl_sqt", "sqt", "sqt.bmp", ""));
            cat_15.Items.Add(new HailongItem("解锁图层", "解锁图层", "zhl_js", "js", "JS.bmp", ""));
            cat_15.Items.Add(new HailongItem("解锁所有", "解锁所有", "zhl_jsa", "jsa", "jsa.bmp", ""));
            cat_15.Items.Add(new HailongItem("冻结图层", "冻结图层", "zhl_djc", "djc", "DJC.bmp", ""));
            cat_15.Items.Add(new HailongItem("冻其它层", "冻其它层", "zhl_dqtc", "dqtc", "DQTC.bmp", ""));
            cat_15.Items.Add(new HailongItem("解冻图层", "解冻图层", "zhl_jd", "jd", "JD.bmp", ""));
            cat_15.Items.Add(new HailongItem("解冻所有", "解冻所有", "zhl_t8", "t8", "T8.bmp", ""));
            cat_15.Items.Add(new HailongItem("视口冻结", "视口冻结", "zhl_k", "k", "K.bmp", ""));
            cat_15.Items.Add(new HailongItem("视口解冻", "视口解冻", "zhl_sjd", "sjd", "SJD.bmp", ""));
            cat_15.Items.Add(new HailongItem("冻结全部", "冻结全部", "zhl_dja", "dja", "DJA.bmp", ""));
            cat_15.Items.Add(new HailongItem("解冻全部", "解冻全部", "zhl_jda", "jda", "JDA.bmp", ""));
            cat_15.Items.Add(new HailongItem("视口管理", "视口管理", "zhl_vg", "vg", "VG.BMP", ""));
            cat_15.Items.Add(new HailongItem("全部解除", "全部解除", "zhl_`1", "`1", "t2.bmp", ""));
            cat_15.Items.Add(new HailongItem("设当前层", "设当前层", "zhl_sa", "sa", "SA.bmp", ""));
            cat_15.Items.Add(new HailongItem("改变图层", "改变图层", "zhl_vc", "vc", "VC.bmp", ""));
            cat_15.Items.Add(new HailongItem("转为当前", "转为当前", "zhl_zdq", "zdq", "zdq.bmp", ""));
            cat_15.Items.Add(new HailongItem("图层匹配", "图层匹配", "zhl_t9", "t9", "T9.bmp", ""));
            cat_15.Items.Add(new HailongItem("图层合并", "图层合并", "zhl_00", "00", "00.bmp", ""));
            cat_15.Items.Add(new HailongItem("删除图层", "删除图层", "zhl_000", "000", "000.bmp", ""));
            cat_15.Items.Add(new HailongItem("删冻关层", "删冻关层", "zhl_0000", "0000", "0000.bmp", ""));
            cat_15.Items.Add(new HailongItem("按色归层", "按色归层", "zhl_se", "se", "SE.bmp", ""));
            cat_15.Items.Add(new HailongItem("全部归层", "全部归层", "zhl_sea", "sea", "SEA.bmp", ""));
            cat_15.Items.Add(new HailongItem("设为随层", "设为随层", "zhl_scc", "scc", "scc.bmp", ""));
            cat_15.Items.Add(new HailongItem("按层输出", "按层输出", "zhl_edwg", "edwg", "edwg.bmp", ""));
            list.Add(cat_15);

            // 分类 [17]: 统计工具
            var cat_16 = new HailongCategory("统计工具", "jsq.bmp");
            cat_16.Items.Add(new HailongItem("计算重量", "计算重量", "zhl_jz", "jz", "jz.bmp", ""));
            cat_16.Items.Add(new HailongItem("统计内容", "统计内容", "zhl_ttj", "ttj", "KB.bmp", ""));
            cat_16.Items.Add(new HailongItem("统计属性", "统计属性", "zhl_tjj", "tjj", "KB.bmp", ""));
            cat_16.Items.Add(new HailongItem("快速计算", "快速计算", "'_quickcalc", "jsq", "JSQ.bmp", ""));
            cat_16.Items.Add(new HailongItem("区域面积", "区域面积", "zhl_are", "are", "ARE.bmp", ""));
            cat_16.Items.Add(new HailongItem("对象面积", "对象面积", "zhl_car", "car", "CAR.bmp", ""));
            cat_16.Items.Add(new HailongItem("统计灯具", "统计灯具", "zhl_tjd", "tjd", "KB.bmp", ""));
            cat_16.Items.Add(new HailongItem("图块统计", "图块统计", "zhl_kb", "kb", "KB.bmp", ""));
            cat_16.Items.Add(new HailongItem("材料统计", "材料统计", "zhl_clb", "clb", "CLB.bmp", ""));
            cat_16.Items.Add(new HailongItem("面积求和", "面积求和", "zhl_vz", "vz", "VZ.bmp", ""));
            cat_16.Items.Add(new HailongItem("线段总长", "线段总长", "zhl_av", "av", "AV.bmp", ""));
            cat_16.Items.Add(new HailongItem("传统测量", "传统测量", "area", "area", "AREA.bmp", ""));
            cat_16.Items.Add(new HailongItem("两点距离", "两点距离", "zhl_dr", "dr", "DR.bmp", ""));
            cat_16.Items.Add(new HailongItem("石材编号", "石材编号", "zhl_bh", "bh", "bh.bmp", ""));
            cat_16.Items.Add(new HailongItem("数字相加", "数字相加", "zhl_td", "td", "td.bmp", ""));
            cat_16.Items.Add(new HailongItem("数字相减", "数字相减", "zhl_tf", "tf", "tf.bmp", ""));
            cat_16.Items.Add(new HailongItem("数字相乘", "数字相乘", "zhl_ti", "ti", "ti.bmp", ""));
            cat_16.Items.Add(new HailongItem("数字相除", "数字相除", "zhl_tv", "tv", "tv.bmp", ""));
            list.Add(cat_16);

            // 分类 [18]: 其他工具
            var cat_17 = new HailongCategory("其他工具", "tzz.bmp");
            cat_17.Items.Add(new HailongItem("工具面板", "工具面板", "zhl_q1", "q1", "q1.bmp", ""));
            cat_17.Items.Add(new HailongItem("表格输入", "表格输入", "zhl_tti", "tti", "TTI.bmp", ""));
            cat_17.Items.Add(new HailongItem("表格输出", "表格输出", "zhl_tte", "tte", "tte.bmp", ""));
            cat_17.Items.Add(new HailongItem("裁剪图像", "裁剪图像", "zhl_xcc", "xcc", "xcc.bmp", ""));
            cat_17.Items.Add(new HailongItem("超级查找", "超级查找", "zhl_bf", "bf", "BF.bmp", ""));
            cat_17.Items.Add(new HailongItem("多种合并", "多种合并", "zhl_mg", "mg", "MG.bmp", ""));
            cat_17.Items.Add(new HailongItem("清理保存", "清理保存", "zhl_pu", "pu", "PU.bmp", ""));
            cat_17.Items.Add(new HailongItem("瘦身保存", "瘦身保存", "zhl_sav", "sav", "SAV.bmp", "彻底清理垃圾另存文件"));
            cat_17.Items.Add(new HailongItem("批量清理", "批量清理", "zhl_pur", "pur", "pur.bmp", ""));
            cat_17.Items.Add(new HailongItem("除教育版", "除教育版", "zhl_qc", "qc", "QC.bmp", ""));
            cat_17.Items.Add(new HailongItem("同步看图", "同步看图", "zhl_tb", "tb", "TB.bmp", ""));
            cat_17.Items.Add(new HailongItem("图形变色", "图形变色", "zhl_tbs", "tbs", "TBS.bmp", ""));
            cat_17.Items.Add(new HailongItem("背景颜色", "背景颜色", "zhl_bjs", "bjs", "BJS.bmp", ""));
            cat_17.Items.Add(new HailongItem("坐标原点", "坐标原点", "zhl_uo", "uo", "UO.bmp", ""));
            cat_17.Items.Add(new HailongItem("绘图角度", "绘图角度", "zhl_-", "—", "-.bmp", ""));
            cat_17.Items.Add(new HailongItem("标注坐标", "标注坐标", "zhl_dy", "dy", "dy.bmp", ""));
            cat_17.Items.Add(new HailongItem("输 入 法", "输 入 法", "zhl_srf", "srf", "SRF.BMP", ""));
            cat_17.Items.Add(new HailongItem("加载天正", "加载天正", "zhl_tzz", "tzz", "TZZ.bmp", ""));
            cat_17.Items.Add(new HailongItem("转存正常", "转存正常", "zhl_zzc", "zzc", "ZZC.bmp", ""));
            cat_17.Items.Add(new HailongItem("批量转存", "批量转存", "zhl_pzz", "pzz", "pzz.bmp", ""));
            cat_17.Items.Add(new HailongItem("炸天正体", "炸天正体", "zhl_ztz", "ztz", "ztz.bmp", ""));
            cat_17.Items.Add(new HailongItem("图纸保护", "图纸保护", "zhl_tbh", "tbh", "tbh.bmp", ""));
            cat_17.Items.Add(new HailongItem("查杀病毒", "查杀病毒", "zhl_ki", "ki", "KI.bmp", ""));
            cat_17.Items.Add(new HailongItem("图元信息", "图元信息", "zhl_lisp", "lisp", "lisp.bmp", ""));
            cat_17.Items.Add(new HailongItem("前置物体", "前置物体", "zhl_sq", "sq", "SQ.bmp", ""));
            cat_17.Items.Add(new HailongItem("后置物体", "后置物体", "zhl_wb", "wb", "WB.bmp", ""));
            cat_17.Items.Add(new HailongItem("参照管理", "参照管理", "zhl_xr", "xr", "xr.bmp", ""));
            cat_17.Items.Add(new HailongItem("裁剪参照", "裁剪参照", "zhl_xc", "xc", "xc.bmp", ""));
            cat_17.Items.Add(new HailongItem("打开参照", "打开参照", "zhl_``c", "``c", "``c.bmp", ""));
            cat_17.Items.Add(new HailongItem("中国象棋", "中国象棋", "zhl_zgxq", "zgxq", "zgxq.bmp", ""));
            cat_17.Items.Add(new HailongItem("五 子 棋", "五 子 棋", "zhl_wzq", "wzq", "wzq.bmp", ""));
            list.Add(cat_17);

            // 分类 [19]: 扩展工具
            var cat_18 = new HailongCategory("扩展工具", "as.bmp");
            cat_18.Items.Add(new HailongItem("图层状态", "图层状态", "zhl_as", "as", "AS.bmp", ""));
            cat_18.Items.Add(new HailongItem("文字转线", "文字转线", "zhl_rrr", "rrr", "RRR.bmp", ""));
            cat_18.Items.Add(new HailongItem("旋转文本", "旋转文本", "zhl_ttr", "ttr", "ttr.bmp", ""));
            cat_18.Items.Add(new HailongItem("字加外框", "字加外框", "zhl_jk", "jk", "JK.bmp", ""));
            cat_18.Items.Add(new HailongItem("图纸字高", "图纸字高", "zhl_zg", "zg", "zg.bmp", ""));
            cat_18.Items.Add(new HailongItem("对齐空间", "对齐空间", "zhl_rw", "rw", "rw.bmp", ""));
            cat_18.Items.Add(new HailongItem("同步视口", "同步视口", "zhl_vd", "vd", "vd.bmp", ""));
            cat_18.Items.Add(new HailongItem("图块填充", "图块填充", "zhl_btc", "btc", "btc.bmp", ""));
            cat_18.Items.Add(new HailongItem("多重拉伸", "多重拉伸", "zhl_ms", "ms", "ms.bmp", ""));
            cat_18.Items.Add(new HailongItem("消除重复", "消除重复", "zhl_ol", "ol", "ol.bmp", ""));
            cat_18.Items.Add(new HailongItem("刷动态块", "刷动态块", "zhl_vvd", "vvd", "VVD.bmp", ""));
            cat_18.Items.Add(new HailongItem("刷字样式", "刷字样式", "zhl_vvt", "vvt", "VVT.bmp", ""));
            cat_18.Items.Add(new HailongItem("刷字高度", "刷字高度", "zhl_vvh", "vvh", "VVH.bmp", ""));
            cat_18.Items.Add(new HailongItem("刷线宽度", "刷线宽度", "zhl_vvw", "vvw", "VVW.bmp", ""));
            cat_18.Items.Add(new HailongItem("匹配线型", "匹配线型", "zhl_vvl", "vvl", "VVL.bmp", ""));
            list.Add(cat_18);

            // 分类 [20]: 帮    助
            var cat_19 = new HailongCategory("帮    助", "zhl.bmp");
            cat_19.Items.Add(new HailongItem("备份配置", "备份配置", "hl_bconfig", "", "zhl.bmp", ""));
            cat_19.Items.Add(new HailongItem("导入配置", "导入配置", "hl_rconfig", "", "zhl.bmp", ""));
            cat_19.Items.Add(new HailongItem("升级教程", "升级教程", "sjjc", "", "zhl.bmp", ""));
            cat_19.Items.Add(new HailongItem("关于软件", "关于软件", "zhl_about", "", "zhl.bmp", ""));
            cat_19.Items.Add(new HailongItem("查看帮助", "查看帮助", "zhl_lp", "", "lp.bmp", ""));
            cat_19.Items.Add(new HailongItem("卸载软件", "卸载软件", "zhl_unload", "", "UNLOAD.bmp", ""));
            cat_19.Items.Add(new HailongItem("图层状态", "图层状态", "zhl_as", "as", "AS.bmp", ""));
            cat_19.Items.Add(new HailongItem("图层全开", "图层全开", "zhl_`1", "`1", "t2.bmp", ""));
            cat_19.Items.Add(new HailongItem("原始结构", "原始结构", "zhl_a1", "A1", "GF.bmp", ""));
            cat_19.Items.Add(new HailongItem("拆除墙体", "拆除墙体", "zhl_a2", "A2", "ttc.bmp", ""));
            cat_19.Items.Add(new HailongItem("新建墙体", "新建墙体", "zhl_a3", "A3", "QW.bmp", ""));
            cat_19.Items.Add(new HailongItem("完成面尺寸", "完成面尺寸", "zhl_a4", "A4", "SA.bmp", ""));
            cat_19.Items.Add(new HailongItem("平面布置", "平面布置", "zhl_a5", "A5", "SA.bmp", ""));
            cat_19.Items.Add(new HailongItem("家具尺寸", "家具尺寸", "zhl_a6", "A6", "SA.bmp", ""));
            cat_19.Items.Add(new HailongItem("地面铺装", "地面铺装", "zhl_a7", "A7", "SA.bmp", ""));
            cat_19.Items.Add(new HailongItem("天花布置", "天花布置", "zhl_a8", "A8", "SA.bmp", ""));
            cat_19.Items.Add(new HailongItem("天花尺寸", "天花尺寸", "zhl_a9", "A9", "SA.bmp", ""));
            cat_19.Items.Add(new HailongItem("灯具定位", "灯具定位", "zhl_a10", "A10", "SA.bmp", ""));
            cat_19.Items.Add(new HailongItem("开关控制", "开关控制", "zhl_a11", "A11", "SA.bmp", ""));
            cat_19.Items.Add(new HailongItem("机电点位", "机电点位", "zhl_a12", "A12", "SA.bmp", ""));
            cat_19.Items.Add(new HailongItem("给排水点位", "给排水点位", "zhl_a13", "A13", "SA.bmp", ""));
            cat_19.Items.Add(new HailongItem("立面索引", "立面索引", "zhl_a14", "A14", "SA.bmp", ""));
            list.Add(cat_19);
            return list;
        }
    }

    public static class HailongService
    {
        private static List<HailongCategory> _categories;
        private static readonly object _lock = new object();

        public static List<HailongCategory> GetCategories()
        {
            if (_categories != null) return _categories;
            lock (_lock)
            {
                if (_categories != null) return _categories;
                _categories = HailongDefaultData.GetDefaultCategories();
                return _categories;
            }
        }
    }
}
