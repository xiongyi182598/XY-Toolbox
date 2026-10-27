;; ========================================================
;; XY工具箱 - 免炸开块内穿透删除引擎 (xy_block_erase.lsp)
;; 编码: ANSI / GBK (兼容全版本 AutoCAD、天正建筑与海龙环境)
;; 核心特性:
;;   1. 外部直接穿透删除图块内部任意深度的子图元，点选/框选即时消失生效
;;   2. 智能穿透锁定图层 (子图元在锁定图层亦可顺利删除并恢复锁定状态)
;;   3. 支持【全图联动 (修改定义)】与【仅改当前 (自动克隆独占)】双模式，按 M 键一秒热切换
;;   4. 纯数学 DXF 纳秒级边界解析，0警告0报警，杜绝任何形未定义刷屏与视口闪烁
;;   5. 完善的单步 Undo 事务回滚保护与 ESC 取消防灾机制
;; ========================================================

(vl-load-com)

;; 全局工作模式: 0 = 全图联动 (修改原图块定义), 1 = 仅改当前 (克隆生成独占图块)
(if (null *XY_BLOCK_ERASE_MODE*)
  (setq *XY_BLOCK_ERASE_MODE* 0)
)

;; --------------------------------------------------------
;; 1. 模式读取与持久化配置
;; --------------------------------------------------------
(defun xy:get-block-erase-mode ( / ini_path f line val )
  (setq ini_path (strcat (xy:get-toolbox-dir) "/cmd_config.ini"))
  (if (and (null *XY_BLOCK_ERASE_MODE_INITIALIZED*) (findfile ini_path))
    (progn
      (setq f (open ini_path "r"))
      (if f
        (progn
          (while (setq line (read-line f))
            (setq line (vl-string-trim " \t\r\n" line))
            (if (vl-string-search "BlockEraseMode=" line)
              (progn
                (setq val (substr line (+ (vl-string-search "BlockEraseMode=" line) 16)))
                (setq *XY_BLOCK_ERASE_MODE* (atoi (vl-string-trim " \t\r\n" val)))
              )
            )
          )
          (close f)
          (setq *XY_BLOCK_ERASE_MODE_INITIALIZED* T)
        )
      )
    )
  )
  *XY_BLOCK_ERASE_MODE*
)

(defun xy:set-block-erase-mode ( new-mode / ini_path f lines line found )
  (setq *XY_BLOCK_ERASE_MODE* new-mode)
  (setq ini_path (strcat (xy:get-toolbox-dir) "/cmd_config.ini"))
  (setq lines nil found nil)
  (if (findfile ini_path)
    (progn
      (setq f (open ini_path "r"))
      (if f
        (progn
          (while (setq line (read-line f))
            (if (vl-string-search "BlockEraseMode=" line)
              (setq lines (cons (strcat "BlockEraseMode=" (itoa new-mode)) lines)
                    found T)
              (setq lines (cons line lines))
            )
          )
          (close f)
        )
      )
    )
  )
  (if (not found)
    (setq lines (cons (strcat "BlockEraseMode=" (itoa new-mode)) lines))
  )
  (setq f (open ini_path "w"))
  (if f
    (progn
      (foreach l (reverse lines)
        (write-line l f)
      )
      (close f)
    )
  )
  *XY_BLOCK_ERASE_MODE*
)

;; --------------------------------------------------------
;; 2. 空间几何坐标反投影 (WCS -> MCS)
;; --------------------------------------------------------
(defun xy:wcs-to-mcs ( pt ins-pt rot sx sy / dx dy r-cos r-sin rx ry mx my )
  (setq dx (- (car pt) (car ins-pt))
        dy (- (cadr pt) (cadr ins-pt)))
  (setq r-cos (cos (- rot))
        r-sin (sin (- rot)))
  (setq rx (- (* dx r-cos) (* dy r-sin))
        ry (+ (* dx r-sin) (* dy r-cos)))
  (if (zerop sx) (setq sx 1.0))
  (if (zerop sy) (setq sy 1.0))
  (setq mx (/ rx sx)
        my (/ ry sy))
  (list mx my (if (caddr pt) (caddr pt) 0.0))
)

;; --------------------------------------------------------
;; 3. 极速纯数学包围盒计算 (纯DXF无COM调用，纳秒级比对)
;; --------------------------------------------------------
(defun xy:get-ent-mcs-box ( ent / dxf etype p10 p11 r pts xs ys h )
  (setq dxf (entget ent))
  (if dxf
    (progn
      (setq etype (cdr (assoc 0 dxf)))
      (cond
        ;; A. 直线 (LINE)
        ((= etype "LINE")
         (setq p10 (cdr (assoc 10 dxf))
               p11 (cdr (assoc 11 dxf)))
         (list (list (min (car p10) (car p11)) (min (cadr p10) (cadr p11)))
               (list (max (car p10) (car p11)) (max (cadr p10) (cadr p11)))))

        ;; B. 圆 (CIRCLE)
        ((= etype "CIRCLE")
         (setq p10 (cdr (assoc 10 dxf))
               r   (cdr (assoc 40 dxf)))
         (list (list (- (car p10) r) (- (cadr p10) r))
               (list (+ (car p10) r) (+ (cadr p10) r))))

        ;; C. 圆弧 (ARC)
        ((= etype "ARC")
         (setq p10 (cdr (assoc 10 dxf))
               r   (cdr (assoc 40 dxf)))
         (list (list (- (car p10) r) (- (cadr p10) r))
               (list (+ (car p10) r) (+ (cadr p10) r))))

        ;; D. 多段线 (LWPOLYLINE)
        ((= etype "LWPOLYLINE")
         (setq pts nil)
         (foreach item dxf
           (if (= (car item) 10)
             (setq pts (cons (cdr item) pts))
           )
         )
         (if pts
           (progn
             (setq xs (mapcar 'car pts)
                   ys (mapcar 'cadr pts))
             (list (list (apply 'min xs) (apply 'min ys))
                   (list (apply 'max xs) (apply 'max ys)))
           )
         ))

        ;; E. 文字 (TEXT / MTEXT)
        ((or (= etype "TEXT") (= etype "MTEXT"))
         (setq p10 (cdr (assoc 10 dxf)))
         (setq h (cdr (assoc 40 dxf)))
         (if (null h) (setq h 250.0))
         (list (list (- (car p10) (* h 2)) (- (cadr p10) (* h 2)))
               (list (+ (car p10) (* h 2)) (+ (cadr p10) (* h 2)))))

        ;; F. 点 (POINT)
        ((= etype "POINT")
         (setq p10 (cdr (assoc 10 dxf)))
         (list (list (car p10) (cadr p10)) (list (car p10) (cadr p10))))

        ;; G. 填充 (HATCH)
        ((= etype "HATCH")
         (setq p10 (cdr (assoc 10 dxf)))
         (if p10
           (list (list (- (car p10) 100.0) (- (cadr p10) 100.0))
                 (list (+ (car p10) 100.0) (+ (cadr p10) 100.0)))
         ))

        ;; H. 椭圆 (ELLIPSE)
        ((= etype "ELLIPSE")
         (setq p10 (cdr (assoc 10 dxf))
               p11 (cdr (assoc 11 dxf)))
         (setq r (max (distance '(0 0 0) p11) 100.0))
         (list (list (- (car p10) r) (- (cadr p10) r))
               (list (+ (car p10) r) (+ (cadr p10) r))))

        ;; I. 其他图元以基点为准
        (t
         (setq p10 (cdr (assoc 10 dxf)))
         (if p10
           (list (list (car p10) (cadr p10)) (list (car p10) (cadr p10)))
           nil
         ))
      )
    )
  )
)

(defun xy:box-intersect-p ( box1-min box1-max box2-min box2-max )
  (not (or (< (car box1-max) (car box2-min))
           (> (car box1-min) (car box2-max))
           (< (cadr box1-max) (cadr box2-min))
           (> (cadr box1-min) (cadr box2-max))))
)

(defun xy:box-contained-p ( box1-min box1-max box2-min box2-max )
  (and (>= (car box1-min) (car box2-min))
       (<= (car box1-max) (car box2-max))
       (>= (cadr box1-min) (car box2-min))
       (<= (cadr box1-max) (cadr box2-max)))
)

;; --------------------------------------------------------
;; 4. 核心物理删除器 (穿透锁定图层 + 双重强力删除 + 立即更新)
;; --------------------------------------------------------
(defun xy:real-delete-entity ( ent-name / dxf lay-name doc layers lay-obj is-locked vla-obj )
  (if (and ent-name (setq dxf (entget ent-name)))
    (progn
      ;; 1. 检查并临时解锁锁定图层 (确保锁定图层也能顺利穿透删除)
      (setq lay-name (cdr (assoc 8 dxf)))
      (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
      (setq layers (vla-get-Layers doc))
      (setq is-locked nil lay-obj nil)
      (if (not (vl-catch-all-error-p (setq lay-obj (vl-catch-all-apply 'vla-Item (list layers lay-name)))))
        (progn
          (setq is-locked (= (vla-get-Lock lay-obj) :vlax-true))
          (if is-locked (vla-put-Lock lay-obj :vlax-false))
        )
      )

      ;; 2. 双重物理彻底抹除 (entdel + vla-delete 互补强力删除)
      (vl-catch-all-apply 'entdel (list ent-name))
      (if (not (vl-catch-all-error-p (setq vla-obj (vl-catch-all-apply 'vlax-ename->vla-object (list ent-name)))))
        (vl-catch-all-apply 'vla-delete (list vla-obj))
      )

      ;; 3. 恢复图层原本的锁定状态
      (if (and is-locked lay-obj)
        (vla-put-Lock lay-obj :vlax-true)
      )
      T
    )
    nil
  )
)

;; --------------------------------------------------------
;; 5. 独占模式核心：后台克隆独立图块定义并重绑定
;; --------------------------------------------------------
(defun xy:isolate-and-erase-sub-entities ( blk-ref-ename del-ent-list / doc blks vla-ref orig-name
                                           new-blk-name orig-blk-def origin new-blk-def clone-objs sa )
  (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
  (setq blks (vla-get-Blocks doc))
  (setq vla-ref (vlax-ename->vla-object blk-ref-ename))
  
  (setq orig-name (vlax-get-property vla-ref 
                    (if (vlax-property-available-p vla-ref 'EffectiveName) 'EffectiveName 'Name)))
  
  (setq new-blk-name (strcat "XY_UID_" (rtos (* (getvar "CDATE") 1000000) 2 0) "_" (itoa (rem (getvar "MILLISECS") 1000))))
  (setq orig-blk-def (vla-Item blks orig-name))
  (setq origin (vla-get-Origin orig-blk-def))
  (setq new-blk-def (vla-Add blks origin new-blk-name))
  
  (setq clone-objs nil)
  (vlax-for obj orig-blk-def
    (if (not (vl-position (vlax-vla-object->ename obj) del-ent-list))
      (setq clone-objs (cons obj clone-objs))
    )
  )
  
  (if clone-objs
    (progn
      (setq sa (vlax-make-safearray vlax-vbObject (cons 0 (1- (length clone-objs)))))
      (vlax-safearray-fill sa (reverse clone-objs))
      (vla-CopyObjects doc sa new-blk-def)
    )
  )
  
  (vla-put-name vla-ref new-blk-name)
  (entupd blk-ref-ename)
  blk-ref-ename
)

;; --------------------------------------------------------
;; 6. 穿透点选执行 (即时生效 + 立即刷新父链条)
;; --------------------------------------------------------
(defun xy:erase-by-nentselp ( pick-res mode / sub-ent parent-chain top-ref-ename )
  (if (and pick-res (listp pick-res))
    (cond
      ;; A. 点选到图块内部的子图元
      ((>= (length pick-res) 4)
       (setq sub-ent (car pick-res))
       (setq parent-chain (cadddr pick-res))
       (setq top-ref-ename (last parent-chain))

       (if (= mode 0)
         ;; 模式 0: 全图联动模式 (直接删除块定义中的子实体，并刷新所有关联图块)
         (progn
           (xy:real-delete-entity sub-ent)
           ;; 核心：立即对所有层级的父图块执行 entupd，让图面即刻消失！
           (if parent-chain
             (foreach pref parent-chain
               (if (and pref (entget pref)) (entupd pref))
             )
           )
           (princ "\n[XY工具箱] 已穿透删除块内图元 [模式: 全图联动] √")
         )
         ;; 模式 1: 仅改当前 (独占模式)
         (progn
           (xy:isolate-and-erase-sub-entities top-ref-ename (list sub-ent))
           (princ "\n[XY工具箱] 已生成独占块并穿透删除 [模式: 仅改当前] √")
         )
       )
       T
      )

      ;; B. 点选到外部普通图元
      ((and (car pick-res) (entget (car pick-res)))
       (xy:real-delete-entity (car pick-res))
       (princ "\n[XY工具箱] 已删除所选外部图元 √")
       T
      )

      (t nil)
    )
  )
)

;; --------------------------------------------------------
;; 7. 穿透框选执行引擎 (纯数学DXF + 批量即时删除)
;; --------------------------------------------------------
(defun xy:erase-by-crossing-box ( pt1 pt2 mode / is-window-mode w-min-x w-max-x w-min-y w-max-y
                                  ss-norm i ent-norm ent-ref dxf ins rot sx sy c1 c2 c3 c4 m-pts
                                  mcs-min mcs-max blk-name blk-tbl sub-ent sub-box
                                  del-list total-deleted ss-insert )
  (setq total-deleted 0)
  (setq is-window-mode (> (car pt2) (car pt1)))

  (setq w-min-x (min (car pt1) (car pt2))
        w-max-x (max (car pt1) (car pt2))
        w-min-y (min (cadr pt1) (cadr pt2))
        w-max-y (max (cadr pt1) (cadr pt2)))

  ;; 1. 外部非块图元直接删除
  (setq ss-norm (ssget (if is-window-mode "W" "C") pt1 pt2 '((0 . "~INSERT"))))
  (if ss-norm
    (progn
      (setq i 0)
      (repeat (sslength ss-norm)
        (setq ent-norm (ssname ss-norm i))
        (if (xy:real-delete-entity ent-norm)
          (setq total-deleted (1+ total-deleted))
        )
        (setq i (1+ i))
      )
    )
  )

  ;; 2. 穿透图块内部图元
  (setq ss-insert (ssget "C" pt1 pt2 '((0 . "INSERT"))))
  (if ss-insert
    (progn
      (setq i 0)
      (repeat (sslength ss-insert)
        (setq ent-ref (ssname ss-insert i))
        (setq dxf (entget ent-ref))
        (setq ins (cdr (assoc 10 dxf))
              rot (cdr (assoc 50 dxf))
              sx  (cdr (assoc 41 dxf))
              sy  (cdr (assoc 42 dxf))
              blk-name (cdr (assoc 2 dxf)))
        (if (null rot) (setq rot 0.0))
        (if (null sx) (setq sx 1.0))
        (if (null sy) (setq sy 1.0))

        (setq c1 (xy:wcs-to-mcs (list w-min-x w-min-y) ins rot sx sy)
              c2 (xy:wcs-to-mcs (list w-max-x w-min-y) ins rot sx sy)
              c3 (xy:wcs-to-mcs (list w-max-x w-max-y) ins rot sx sy)
              c4 (xy:wcs-to-mcs (list w-min-x w-max-y) ins rot sx sy))
        (setq m-pts (list c1 c2 c3 c4))
        (setq mcs-min (list (apply 'min (mapcar 'car m-pts))
                            (apply 'min (mapcar 'cadr m-pts))))
        (setq mcs-max (list (apply 'max (mapcar 'car m-pts))
                            (apply 'max (mapcar 'cadr m-pts))))

        (setq blk-tbl (tblobjname "BLOCK" blk-name))
        (if blk-tbl
          (progn
            (setq del-list nil)
            (setq sub-ent (entnext blk-tbl))
            (while (and sub-ent (/= (cdr (assoc 0 (entget sub-ent))) "ENDBLK"))
              (setq sub-box (xy:get-ent-mcs-box sub-ent))
              (if sub-box
                (progn
                  (if (if is-window-mode
                        (xy:box-contained-p (car sub-box) (cadr sub-box) mcs-min mcs-max)
                        (xy:box-intersect-p (car sub-box) (cadr sub-box) mcs-min mcs-max)
                      )
                    (setq del-list (cons sub-ent del-list))
                  )
                )
              )
              (setq sub-ent (entnext sub-ent))
            )

            (if del-list
              (progn
                (setq total-deleted (+ total-deleted (length del-list)))
                (if (= mode 0)
                  (progn
                    (foreach e del-list
                      (xy:real-delete-entity e)
                    )
                    (entupd ent-ref)
                  )
                  (progn
                    (xy:isolate-and-erase-sub-entities ent-ref del-list)
                  )
                )
              )
            )
          )
        )
        (setq i (1+ i))
      )
    )
  )
  total-deleted
)

;; --------------------------------------------------------
;; 8. 用户主交互循环命令 (KSC / XSC / BDE)
;; --------------------------------------------------------
(defun c:XY_BLOCK_ERASE ( / doc cur-mode cur-mode-str prompt-str pick-res loop pt1 pt2 n-del err-val
                            old-regen old-cmdecho *error* old-err )
  (vl-load-com)
  (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
  (vla-StartUndoMark doc)

  (setq old-regen (getvar "REGENMODE"))
  (setvar "REGENMODE" 0)
  (setq old-cmdecho (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)

  ;; 专有错误保护处理器，确保按下 ESC 时系统变量安全复原与 Undo 闭合
  (setq old-err *error*)
  (defun *error* ( msg )
    (setvar "REGENMODE" old-regen)
    (setvar "CMDECHO" old-cmdecho)
    (if doc (vla-EndUndoMark doc))
    (setq *error* old-err)
    (princ "\n[XY工具箱] 穿透删除已退出。\n")
    (princ)
  )

  (setq loop T)
  (while loop
    (setq cur-mode (xy:get-block-erase-mode))
    (setq cur-mode-str (if (= cur-mode 0) "【全图联动】" "【仅改当前(独占)】"))
    (setq prompt-str (strcat "\n★ [XY工具箱] 块内穿透删除 | 当前模式: " cur-mode-str
                            " [点选/框选/M切模式/回车退出]: "))
    (initget "M m W w C c ")
    (setvar "ERRNO" 0)
    (setq pick-res (nentselp prompt-str))
    (setq err-val (getvar "ERRNO"))

    (cond
      ;; 1. 用户按 M 键切换模式
      ((or (= pick-res "M") (= pick-res "m"))
       (if (= cur-mode 0)
         (xy:set-block-erase-mode 1)
         (xy:set-block-erase-mode 0)
       )
       (princ (strcat "\n>>> 模式已切换为: " 
                      (if (= (xy:get-block-erase-mode) 0) "【全图联动 (所有同名块同步生效)】" "【仅改当前 (自动克隆独占图块)】")
                      "\n"))
      )

      ;; 2. 用户指定 W 或 C（精确框选模式）
      ((or (= pick-res "W") (= pick-res "w") (= pick-res "C") (= pick-res "c"))
       (setq pt1 (getpoint "\n指定选择第一角点: "))
       (if (and pt1 (listp pt1))
         (progn
           (setq pt2 (getcorner pt1 "\n指定对角点 (左拉包含/右拉相交): "))
           (if pt2
             (progn
               (setq n-del (xy:erase-by-crossing-box pt1 pt2 cur-mode))
               (if (> n-del 0)
                 (princ (strcat "\n[XY工具箱] 穿透框选成功删除 " (itoa n-del) " 个图元 √"))
                 (princ "\n[XY工具箱] 选中范围内未发现可删除图元。")
               )
             )
           )
         )
       )
      )

      ;; 3. 靶心直接点中图元 (即时穿透删除并刷新)
      ((and (listp pick-res) (car pick-res))
       (xy:erase-by-nentselp pick-res cur-mode)
      )

      ;; 4. 用户按下回车/空格/ESC (ERRNO == 52)
      ((= err-val 52)
       (setq loop nil)
      )

      ;; 5. 点到了空白处，自动进入对角框选！
      ((and (null pick-res) (= err-val 0))
       (setq pt1 (getpoint "\n指定穿透框选第一角点 [空格退出]: "))
       (if (and pt1 (listp pt1))
         (progn
           (setq pt2 (getcorner pt1 "\n指定对角点 (左拉包含/右拉相交): "))
           (if pt2
             (progn
               (setq n-del (xy:erase-by-crossing-box pt1 pt2 cur-mode))
               (if (> n-del 0)
                 (princ (strcat "\n[XY工具箱] 穿透框选成功删除 " (itoa n-del) " 个图元 √"))
                 (princ "\n[XY工具箱] 选中范围内未发现可删除图元。")
               )
             )
           )
         )
         (setq loop nil)
       )
      )

      (t
       (setq loop nil)
      )
    )
  )

  (setvar "REGENMODE" old-regen)
  (setvar "CMDECHO" old-cmdecho)
  (setq *error* old-err)

  (vla-EndUndoMark doc)
  (princ "\n[XY工具箱] 穿透删除已结束。\n")
  (princ)
)

(defun c:KSC () (c:XY_BLOCK_ERASE))
(defun c:XSC () (c:XY_BLOCK_ERASE))
(defun c:BDE () (c:XY_BLOCK_ERASE))

(princ "\n★ [XY工具箱] 极速免炸开穿透删除模块 (KSC/XSC) 已就绪！\n")
(princ)