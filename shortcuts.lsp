;; ========================================================
;; XY工具箱 - 快捷键直通映射与热重载 (shortcuts.lsp)
;; 编码: ANSI / GBK (完美适配各版本 AutoCAD)
;; ========================================================

(vl-load-com)

;; 1. 确保核心 autoload.lsp 正确就绪
(if (null execute_layer_merge)
  (progn
    (if (findfile "autoload.lsp")
      (load (findfile "autoload.lsp"))
    )
  )
)

;; 2. 快捷键强绑定映射 (大小写双向直通)
;; 2. 快捷键映射 (优先遵循 X 前缀规范，严格保护海龙/天正既有指令)
(defun c:XJH () (c:fast_merge_quick))
(defun c:xjh () (c:fast_merge_quick))
(defun c:HB1 () (c:fast_merge_quick))
(defun c:hb1 () (c:fast_merge_quick))

;; 保护海龙核心命令: LM (立面框架 zhl_lm) 与 HC (转成窗户 zhl_hc)
(if (null (read "c:LM")) (defun c:LM () (c:fast_merge_quick)))
(if (null (read "c:lm")) (defun c:lm () (c:fast_merge_quick)))
(if (null (read "c:HC")) (defun c:HC () (c:fast_merge_quick)))
(if (null (read "c:hc")) (defun c:hc () (c:fast_merge_quick)))

(defun c:HB2 () (c:fast_merge))
(defun c:hb2 () (c:fast_merge))

(defun c:B0  () (c:BLOCK_LAYER_ZERO))
(defun c:b0  () (c:BLOCK_LAYER_ZERO))
(defun c:0B  () (c:BLOCK_LAYER_ZERO))
(defun c:0b  () (c:BLOCK_LAYER_ZERO))

(defun c:MW  () (c:UNIVERSAL_MOVE_TEXT))
(defun c:mw  () (c:UNIVERSAL_MOVE_TEXT))
(defun c:MY  () (c:UNIVERSAL_MOVE_TEXT))
(defun c:my  () (c:UNIVERSAL_MOVE_TEXT))

(defun c:TC  () (c:DETECT_ENTITY))
(defun c:tc  () (c:DETECT_ENTITY))

;; 免炸开块内穿透删除 (快捷键直通)
(defun c:XSC () (c:XY_BLOCK_ERASE))
(defun c:KSC () (c:XY_BLOCK_ERASE))
(defun c:BDE () (c:XY_BLOCK_ERASE))

;; 工作台面板呼出
(defun c:XYSZ () (c:XYPANEL))
(defun c:xysz () (c:XYSZ))
(defun c:ZYSZ () (c:XYSZ))
(defun c:zysz () (c:XYSZ))

;; 快捷键配置与热重载
(defun c:REKEY () (c:rekey))
(defun c:rekey ( / sc )
  (setq sc (findfile "shortcuts.lsp"))
  (if (and sc (findfile sc))
    (load sc)
  )
  (princ "\n[XY工具箱] 快捷键配置已重新热重载生效！\n")
  (princ)
)

(princ)
