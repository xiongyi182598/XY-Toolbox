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

proxy_handler = urllib.request.ProxyHandler({'http': 'http://127.0.0.1:10808', 'https': 'http://127.0.0.1:10808'})
opener = urllib.request.build_opener(proxy_handler)

REPO = "xiongyi182598/XY-Toolbox"
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(BASE_DIR, "发布成果物")

ver_json = os.path.join(BASE_DIR, "version.json")
with open(ver_json, "r", encoding="utf-8") as f:
    ver_data = json.load(f)

TAG = f"v{ver_data['version']}"
TITLE = ver_data.get("title", f"XY工具箱 {TAG}")
changelog_items = ver_data.get("changelog", [])
body_md = f"### {TITLE}\n\n"
for itm in changelog_items:
    body_md += f"- {itm}\n"
body_md += "\n#### 下载与在线升级说明：\n"
body_md += "- **在线自动更新**：CAD 内部直接敲 `XGX`（或 `UPDATE`）即可极速在线热更新；\n"
body_md += f"- **全新安装/绿色部署**：请下载下方 `XY工具箱_{TAG}_完整绿色独立版.zip`，解压即用。\n"

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
            "name": TITLE,
            "body": body_md,
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
