# -*- coding: utf-8 -*-
import os
import shutil
import zipfile
import json

base_dir = os.path.dirname(os.path.abspath(__file__))
out_dir = os.path.join(base_dir, "发布成果物")
stage_dir = os.path.join(out_dir, "XY工具箱_纯净独立绿色版")

print("=" * 60)
print("  XY工具箱 · 商业级纯净发布与更新包一键生成")
print("=" * 60)

# 1. 确保核心 DLL 存在
dll_path = os.path.join(base_dir, "XY_Toolbox.dll")
if not os.path.exists(dll_path):
    print("[-] 错误：缺少核心 XY_Toolbox.dll，请先编译！")
    exit(1)

# 2. 读取版本号
version = "1.0.1"
ver_json = os.path.join(base_dir, "version.json")
if os.path.exists(ver_json):
    try:
        with open(ver_json, "r", encoding="utf-8") as f:
            data = json.load(f)
            version = data.get("version", data.get("Version", "1.0.2"))
    except Exception as e:
        print("[!] 读取版本号提示:", e)

print(f"[+] 当前发布版本: v{version}")

# 3. 准备目录
if os.path.exists(out_dir):
    try:
        shutil.rmtree(out_dir)
    except:
        pass
os.makedirs(stage_dir, exist_ok=True)

# 4. 白名单文件清单 (绝对隔离所有 .cs 源码与私有文件)
white_list = [
    "XY_Toolbox.dll",
    "autoload.lsp",
    "shortcuts.lsp",
    "shortcuts.ini",
    "xy_block_erase.lsp",
    "cmd_config.ini",
    "version.json",
    "app_logo_white.png",
    "app_pure_transparent.ico",
    "install.ps1",
    "uninstall.ps1",
    "【一键安装】双击即可永久使用.bat",
    "【一键卸载】彻底清理还原.bat",
    "使用说明与快捷键速查.txt",
    "README.txt"
]

copied = 0
for filename in white_list:
    src = os.path.join(base_dir, filename)
    if os.path.exists(src):
        shutil.copy2(src, os.path.join(stage_dir, filename))
        copied += 1

print(f"[OK] 成功提取 {copied} 个核心运行时文件 (源码100%安全隔离)")

# 5. 生成供初次分发的完整商业独立版 ZIP
full_zip_name = f"XY工具箱_v{version}_完整绿色独立版.zip"
full_zip_path = os.path.join(out_dir, full_zip_name)
with zipfile.ZipFile(full_zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
    for root, dirs, files in os.walk(stage_dir):
        for f in files:
            full_f = os.path.join(root, f)
            rel_f = os.path.relpath(full_f, out_dir)
            zf.write(full_f, rel_f)
print(f"[★] 完整独立分发包已生成: 发布成果物\\{full_zip_name}")

# 6. 生成供 GitHub Release 和 在线更新专用的纯净补丁包
update_zip_name = "XY_Toolbox_Update.zip"
update_zip_path = os.path.join(out_dir, update_zip_name)
with zipfile.ZipFile(update_zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
    for f in os.listdir(stage_dir):
        fp = os.path.join(stage_dir, f)
        if os.path.isfile(fp):
            zf.write(fp, f)
print(f"[★] 在线升级补丁包已生成: 发布成果物\\{update_zip_name}")

# 7. 清理暂存目录
try:
    shutil.rmtree(stage_dir)
except:
    pass

print("=" * 60)
print("  ★ 商业级发布构建圆满完成！★")
print(f"  成果物目录: {out_dir}")
print(f"  1. 发给新用户: {full_zip_name} (解压后双击即可永久使用)")
print(f"  2. 上传GitHub: {update_zip_name} (作为 Release 附件发布)")
print("=" * 60)
