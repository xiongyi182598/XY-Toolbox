;; ========================================================
;; XY工具箱 - 快捷键直通映射表 (shortcuts.lsp)
;; 编码: ANSI / GBK (完美兼容各版本 AutoCAD)
;; ========================================================

(vl-load-com)

;; 1. 确保 autoload.lsp 先行加载就绪
(if (null execute_layer_merge)
  (progn
    (if (findfile "autoload.lsp")
      (load (findfile "autoload.lsp"))
    )
  )
)

;; 2. 图层合并指令映射 (统一调用 C# 现代面板，彻底杜绝旧版 DCL)
(defun c:XHB () (c:fast_merge))
(defun c:xhb () (c:fast_merge))
(defun c:HB2 () (c:fast_merge))
(defun c:hb2 () (c:fast_merge))

;; 3. 极速点选合并指令 (免弹窗)
(defun c:XJH () (c:fast_merge_quick))
(defun c:xjh () (c:fast_merge_quick))
(defun c:HB1 () (c:fast_merge_quick))
(defun c:hb1 () (c:fast_merge_quick))

;; 智能防冲突快捷键: LM (若未占用) 与 HC (若未占用)
(if (null (read "c:LM")) (defun c:LM () (c:fast_merge_quick)))
(if (null (read "c:lm")) (defun c:lm () (c:fast_merge_quick)))
(if (null (read "c:HC")) (defun c:HC () (c:fast_merge_quick)))
(if (null (read "c:hc")) (defun c:hc () (c:fast_merge_quick)))

;; 3.1 图块镜像反字智能穿透纠偏 (FZW / XFZ / CORRECTTEXT)
(defun c:FZW () (vl-cmdf "FZW"))
(defun c:fzw () (vl-cmdf "FZW"))
(defun c:XFZ () (vl-cmdf "FZW"))
(defun c:xfz () (vl-cmdf "FZW"))

;; 4. 原位编块免进编辑穿透拉伸与移动 (XKB / XLS / XSTRETCH)
(defun c:XKB () (vl-cmdf "XSTRETCH"))
(defun c:xkb () (vl-cmdf "XSTRETCH"))
(defun c:XLS () (vl-cmdf "XSTRETCH"))
(defun c:xls () (vl-cmdf "XSTRETCH"))

;; 5. 块内图元归0层
(defun c:B0  () (c:BLOCK_LAYER_ZERO))
(defun c:b0  () (c:BLOCK_LAYER_ZERO))
(defun c:0B  () (c:BLOCK_LAYER_ZERO))
(defun c:0b  () (c:BLOCK_LAYER_ZERO))
(defun c:XGL () (c:BLOCK_LAYER_ZERO))
(defun c:xgl () (c:BLOCK_LAYER_ZERO))

;; 6. 属性文字穿透移动
(defun c:MW  () (c:UNIVERSAL_MOVE_TEXT))
(defun c:mw  () (c:UNIVERSAL_MOVE_TEXT))
(defun c:MY  () (c:UNIVERSAL_MOVE_TEXT))
(defun c:my  () (c:UNIVERSAL_MOVE_TEXT))
(defun c:XSY () (c:UNIVERSAL_MOVE_TEXT))
(defun c:xsy () (c:UNIVERSAL_MOVE_TEXT))

;; 7. 实体智能检测
(defun c:TC  () (c:DETECT_ENTITY))
(defun c:tc  () (c:DETECT_ENTITY))
(defun c:XTC () (c:DETECT_ENTITY))
(defun c:xtc () (c:DETECT_ENTITY))

;; 8. 块内穿透删除 (快捷指令直通)
(defun c:XSC () (c:XY_BLOCK_ERASE))
(defun c:xsc () (c:XY_BLOCK_ERASE))
(defun c:KSC () (c:XY_BLOCK_ERASE))
(defun c:ksc () (c:XY_BLOCK_ERASE))
(defun c:BDE () (c:XY_BLOCK_ERASE))
(defun c:bde () (c:XY_BLOCK_ERASE))

;; 9. 视口冻结与恢复
(defun c:XDJ () (vl-cmdf "XDJ"))
(defun c:xdj () (vl-cmdf "XDJ"))
(defun c:XJD () (vl-cmdf "XJD"))
(defun c:xjd () (vl-cmdf "XJD"))
(defun c:XHF () (vl-cmdf "XHF"))
(defun c:xhf () (vl-cmdf "XHF"))

;; 10. 工具箱面板与设置
(defun c:XYSZ () (c:XYPANEL))
(defun c:xysz () (c:XYSZ))
(defun c:ZYSZ () (c:XYSZ))
(defun c:zysz () (c:XYSZ))
(defun c:XGJ  () (c:XYSZ))
(defun c:xgj  () (c:XYSZ))

;; 快捷键热重载
(defun c:REKEY () (c:rekey))
(defun c:rekey ( / sc )
  (setq sc (findfile "shortcuts.lsp"))
  (if (and sc (findfile sc))
    (load sc)
  )
  (princ "
[XY工具箱] 快捷键已全部刷新生效！
")
  (princ)
)


;; 3.2 视口图层样式覆盖 (SK / SKYS)
(defun c:SK () (vl-cmdf "_.SK"))
(defun c:sk () (c:SK))
(defun c:SKYS () (vl-cmdf "_.SKYS"))
(defun c:skys () (c:SKYS))
(defun c:SKTC () (vl-cmdf "_.SKTC"))
(defun c:sktc () (c:SKTC))

;; 3.3 商业级在线检测更新 (XGX / UPDATE)
(defun c:XGX () (vl-cmdf "_.XGX"))
(defun c:xgx () (c:XGX))
(defun c:UPDATE () (c:XGX))
(defun c:update () (c:XGX))
(princ)
