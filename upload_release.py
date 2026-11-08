# -*- coding: utf-8 -*-
import os
import sys
import json
import ctypes
from ctypes import wintypes
import urllib.request
import urllib.error

# 1. 从 Windows 凭据管理器安全读取 GitHub Token
class CREDENTIAL(ctypes.Structure):
    _fields_ = [
        ('Flags', wintypes.DWORD),
        ('Type', wintypes.DWORD),
        ('TargetName', wintypes.LPWSTR),
        ('Comment', wintypes.LPWSTR),
        ('LastWritten', wintypes.FILETIME),
        ('CredentialBlobSize', wintypes.DWORD),
        ('CredentialBlob', ctypes.POINTER(ctypes.c_char)),
        ('Persist', wintypes.DWORD),
        ('AttributeCount', wintypes.DWORD),
        ('Attributes', ctypes.c_void_p),
        ('TargetAlias', wintypes.LPWSTR),
        ('UserName', wintypes.LPWSTR)
    ]

advapi32 = ctypes.windll.advapi32
CredReadW = advapi32.CredReadW
CredReadW.argtypes = [wintypes.LPWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.POINTER(ctypes.POINTER(CREDENTIAL))]
CredReadW.restype = wintypes.BOOL

pcred = ctypes.POINTER(CREDENTIAL)()
if not CredReadW('git:https://github.com', 1, 0, ctypes.byref(pcred)):
    print('[-] 无法读取 GitHub 凭据')
    sys.exit(1)

raw_blob = ctypes.string_at(pcred.contents.CredentialBlob, pcred.contents.CredentialBlobSize)
token = raw_blob.decode('utf-16-le', errors='ignore') if b'\x00' in raw_blob[:4] else raw_blob.decode('utf-8')
token = token.strip('\x00').strip()

print(f'[+] 读取 GitHub 认证成功，用户: {pcred.contents.UserName}')

proxy_handler = urllib.request.ProxyHandler({'http': 'socks5h://127.0.0.1:10808', 'https': 'socks5h://127.0.0.1:10808'})
opener = urllib.request.build_opener(proxy_handler)

REPO = "xiongyi182598/XY-Toolbox"
TAG = "v1.0.5"
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(BASE_DIR, "发布成果物")

# 2. 查询或创建 Release
release_id = None
upload_url_template = None

print(f'[*] 正在检查 Release {TAG}...')
try:
    req = urllib.request.Request(
        f'https://api.github.com/repos/{REPO}/releases/tags/{TAG}',
        headers={'User-Agent': 'XY', 'Authorization': f'token {token}'}
    )
    with opener.open(req) as resp:
        rel_data = json.loads(resp.read().decode('utf-8'))
        release_id = rel_data['id']
        upload_url_template = rel_data['upload_url']
        print(f'[+] 发现已存在 Release ID: {release_id}')
except urllib.error.HTTPError as e:
    if e.code == 404:
        print(f'[*] 创建新 Release {TAG}...')
        post_data = json.dumps({
            "tag_name": TAG,
            "target_commitish": "main",
            "name": f"XY工具箱 {TAG} 侧边吸附记忆与智能输入法纯净版",
            "body": "### XY工具箱 v1.0.5 侧边吸附记忆与智能输入法纯净版\n\n1. **【面板吸附记忆】首次启动默认停靠吸附于 CAD 右侧**，彻底消除浮动遮挡；全面记忆用户停靠习惯（左侧/右侧固定吸附、吸附并折叠隐藏/自动收缩、浮动位置及尺寸），下次启动自动精准还原；\n2. **【智能输入法隔离】彻底修复输入法状态污染外部软件的Bug**：搜狗与微软输入法仅在 AutoCAD 进程窗口内部生效，切换到微信/浏览器/记事本等外部程序时保持原样，绝无干扰；\n3. **【极速盲打与文字秒切】画图命令100%纯英文盲打**，写字改字（T/MT/DT/ED/天正文字等）自动秒切中文，退出自动还原英文；\n4. **【在线升级管线】在线更新支持多镜像极速下载与多版本无缝热切换**。\n\n#### 下载说明：\n- **全新安装/远端部署**：请下载 `XY工具箱_v1.0.5_完整绿色独立版.zip`，解压即用；\n- **在线自动更新**：CAD 内部敲 `XGX` 会自动下载 `XY_Toolbox_Update.zip` 极速热更新。",
            "draft": False,
            "prerelease": False
        }).encode('utf-8')
        req = urllib.request.Request(
            f'https://api.github.com/repos/{REPO}/releases',
            data=post_data,
            headers={'User-Agent': 'XY', 'Authorization': f'token {token}', 'Content-Type': 'application/json'}
        )
        with opener.open(req) as resp:
            rel_data = json.loads(resp.read().decode('utf-8'))
            release_id = rel_data['id']
            upload_url_template = rel_data['upload_url']
            print(f'[+] 成功创建 Release ID: {release_id}')
    else:
        print(f'[-] 请求失败: {e}')
        sys.exit(1)

import time

def open_with_retry(request, retries=5):
    for i in range(retries):
        try:
            return opener.open(request, timeout=30)
        except Exception as ex:
            if i == retries - 1:
                raise
            print(f'[!] 网络抖动，第 {i+1} 次重试: {ex}')
            time.sleep(2)

# 3. 获取已有 assets 并清理同名 asset
req = urllib.request.Request(
    f'https://api.github.com/repos/{REPO}/releases/{release_id}/assets',
    headers={'User-Agent': 'XY', 'Authorization': f'token {token}'}
)
with open_with_retry(req) as resp:
    existing_assets = json.loads(resp.read().decode('utf-8'))

files_to_upload = [
    os.path.join(OUT_DIR, "XY_Toolbox_Update.zip"),
    os.path.join(OUT_DIR, f"XY工具箱_{TAG}_完整绿色独立版.zip")
]

for file_path in files_to_upload:
    if not os.path.exists(file_path):
        print(f'[-] 找不到文件: {file_path}')
        continue
    file_name = os.path.basename(file_path)
    file_size = os.path.getsize(file_path)
    
    # 删除旧资产
    for ea in existing_assets:
        if ea['name'] == file_name:
            print(f'[*] 删除已存在的旧文件: {file_name} (ID: {ea["id"]})...')
            del_req = urllib.request.Request(
                f'https://api.github.com/repos/{REPO}/releases/assets/{ea["id"]}',
                headers={'User-Agent': 'XY', 'Authorization': f'token {token}'},
                method='DELETE'
            )
            try:
                with open_with_retry(del_req) as r:
                    pass
            except Exception as e:
                print(f'[!] 删除提示: {e}')

    print(f'[*] 正在上传资产: {file_name} ({file_size / 1024:.1f} KB)...')
    upload_url = upload_url_template.split('{')[0] + f'?name={urllib.parse.quote(file_name)}'
    
    with open(file_path, 'rb') as f:
        file_bytes = f.read()

    up_req = urllib.request.Request(
        upload_url,
        data=file_bytes,
        headers={
            'User-Agent': 'XY',
            'Authorization': f'token {token}',
            'Content-Type': 'application/zip',
            'Content-Length': str(len(file_bytes))
        }
    )
    try:
        with open_with_retry(up_req) as resp:
            print(f'[OK] 上传成功: {file_name}')
    except Exception as e:
        print(f'[-] 上传失败 {file_name}: {e}')

print('=' * 60)
print(f'[SUCCESS] GitHub Release {TAG} 全流程自动化发布上传完成！')
print(f'[*] 下载主页: https://github.com/{REPO}/releases/tag/{TAG}')
print('=' * 60)
