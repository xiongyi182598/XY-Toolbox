;; ========================================================
;; XY������ȫ���Զ������� (autoload.lsp)
;; ������: ԭ�� AutoCAD (2020-2026) / �콨 (T20/T30 ȫϵ) / ��������
;; ========================================================

(vl-load-com)

;; �Ը� AutoCAD ������Ϊ�������״̬
(vl-catch-all-apply '(lambda () (vla-put-windowstate (vlax-get-acad-object) 3)))

;; ����߰� AutoCAD ��ȫ���ػ��Ʋ�����ȫ�ĵ����� (���������ԴȪ)
(if (getvar "SECURELOAD") (setvar "SECURELOAD" 0))
(if (getvar "ACADLSPASDOC") (setvar "ACADLSPASDOC" 1))

;; ȫ�ֶ���λ������ʵ�� (��ȫ���̷���汾���ž�Ӳ��������)
(defun xy:get-toolbox-dir ( / p )
  (cond
    ((setq p (findfile "autoload.lsp"))
     (vl-filename-directory (vl-string-translate "\\" "/" p)))
    ((setq p (findfile "XY_Toolbox.dll"))
     (vl-filename-directory (vl-string-translate "\\" "/" p)))
    (t "")
  )
)

;; ��ע�ᵱǰĿ¼����ǰ��Ծ Profile �������ξ��������·����ǰ
(vl-catch-all-apply
  '(lambda ( / acad-obj prefs files cur-sup cur-tru my-dir)
     (setq my-dir (xy:get-toolbox-dir))
     (if (and my-dir (/= my-dir ""))
       (progn
         (setq acad-obj (vlax-get-acad-object))
         (setq prefs (vla-get-preferences acad-obj))
         (setq files (vla-get-files prefs))
         (setq cur-sup (vla-get-supportpath files))
         (if (not (vl-string-search (strcase my-dir) (strcase cur-sup)))
           (vla-put-supportpath files (strcat cur-sup ";" my-dir))
         )
         (setq cur-tru (vla-get-trustedpaths files))
         (if (not (vl-string-search (strcase my-dir) (strcase cur-tru)))
           (vla-put-trustedpaths files (strcat cur-tru ";" my-dir "/..."))
         )
       )
     )
   )
)

;; ���Ļ��� 1������δ�����κ� DLL ǰ���Զ���Ⲣ�޸�Ӧ�� cache ��ʷ�����������߸����ݴ�� (ȫ���������ݴ�ջ�)
(vl-catch-all-apply
  '(lambda ( / dir cache-dir c-files f src dst pend-dir p-files bat-f )
     (setq dir (xy:get-toolbox-dir))
     (if (and dir (/= dir ""))
       (progn
         ;; A. �Զ�����ƽ����ʷ cache ����Ŀ¼����ɰ��ѹ����������ļ�
         (setq cache-dir (strcat dir "/cache"))
         (if (vl-file-directory-p cache-dir)
           (progn
             (setq c-files (vl-directory-files cache-dir "*.*" 1))
             (foreach f c-files
               (if (and (not (member f '("." ".."))) (not (vl-string-search "Run.dll" f)))
                 (progn
                   (setq src (strcat cache-dir "/" f))
                   (setq dst (strcat dir "/" f))
                   (if (findfile dst) (vl-file-delete dst))
                   (vl-file-copy src dst)
                 )
               )
             )
             (vl-catch-all-apply 'vl-file-delete (list (strcat cache-dir "/XY_Toolbox_Run.dll")))
           )
         )

         ;; B. �Զ��� pending_update �еĴ��滻�ļ�������ǵ���Ŀ¼
         (setq pend-dir (strcat dir "/pending_update"))
         (if (vl-file-directory-p pend-dir)
           (progn
             (setq p-files (vl-directory-files pend-dir "*.*" 1))
             (foreach f p-files
               (if (not (member f '("." "..")))
                 (progn
                   (setq src (strcat pend-dir "/" f))
                   (setq dst (strcat dir "/" f))
                   (if (findfile dst) (vl-file-delete dst))
                   (vl-file-copy src dst)
                   (vl-file-delete src)
                 )
               )
             )
           )
         )

         ;; ����ɸ���������
         (foreach bat-f '("apply_update.bat" "run_updater.bat" "restart_apply.bat" "_apply_restart.bat")
           (if (findfile (strcat dir "/" bat-f))
             (vl-catch-all-apply 'vl-file-delete (list (strcat dir "/" bat-f)))
           )
         )
       )
     )
   )
)

;; ��ҵ��������̬�汾���ؼܹ� (Version-Ranked Dynamic Loader)��
;; ɨ�����а汾�ź�׺ DLL (�� XY_Toolbox_105.dll)����׼ȷ��ȡȫ��λ���汾���� (�� 105)
(defun xy:extract-dll-ver (fname / len base num-str i ch in-num)
  (setq base (vl-filename-base fname))
  (setq len (strlen base))
  (setq num-str "")
  (setq in-num nil)
  (setq i len)
  (while (> i 0)
    (setq ch (substr base i 1))
    (if (and (>= (ascii ch) 48) (<= (ascii ch) 57))
      (progn
        (setq num-str (strcat ch num-str))
        (setq in-num t)
      )
      (if in-num (setq i 0))
    )
    (setq i (1- i))
  )
  (if (/= num-str "") (atoi num-str) 0)
)

(defun xy:load-latest-dll ( / dir all-files best-dll best-ver f v target-path cache-dir )
  (if (or (not *XY-TOOLBOX-LOADED*) (and *CURRENT-LOADED-VER* (> best-ver *CURRENT-LOADED-VER*)))
    (progn
      (setq dir (xy:get-toolbox-dir))
      (if (and dir (/= dir ""))
        (progn
          ;; 1. ɨ�����к�ѡ DLL ��ѡ���汾�����
          (setq all-files (vl-directory-files dir "XY_Toolbox*.dll" 1))
          (setq best-dll nil best-ver -1)
          (foreach f all-files
            (if (not (vl-string-search "run" (strcase f)))
              (progn
                (setq v (xy:extract-dll-ver f))
                (if (> v best-ver)
                  (setq best-ver v best-dll f)
                )
              )
            )
          )
          (if (not best-dll) (setq best-dll "XY_Toolbox.dll"))
          (setq target-path (strcat dir "/" best-dll))

          ;; 2. �����´��� DLL ���������ļ�δ��������ͬ��ĸ���ļ�
          (if (and (/= best-dll "XY_Toolbox.dll") (findfile (strcat dir "/" best-dll)))
            (vl-catch-all-apply
              '(lambda ()
                 (if (findfile (strcat dir "/XY_Toolbox.dll"))
                   (vl-file-delete (strcat dir "/XY_Toolbox.dll"))
                 )
                 (vl-file-copy (strcat dir "/" best-dll) (strcat dir "/XY_Toolbox.dll"))
               )
            )
          )

          ;; 3. ִ�� netload �������
          (if (findfile target-path)
            (progn
              (setq target-path (vl-string-translate "\\" "/" target-path))
              (if (not (vl-cmdf "_.netload" target-path))
                (vl-catch-all-apply 'vl-cmdf (list "netload" target-path))
              )
              (setq *XY-TOOLBOX-LOADED* t)
              (setq *CURRENT-LOADED-VER* best-ver)

              ;; 4. ��Ĭ������ʷ�ɰ汾�ļ�
              (foreach f all-files
                (if (and (/= f best-dll) (/= f "XY_Toolbox.dll"))
                  (vl-catch-all-apply 'vl-file-delete (list (strcat dir "/" f)))
                )
              )
              target-path
            )
          )
        )
      )
    )
  )
)
(vl-catch-all-apply 'xy:load-latest-dll)

;; �����ض�������ը���ڴ�ɾ��ģ (KSC/XSC)
(vl-catch-all-apply
  '(lambda ( / dir f )
     (setq dir (xy:get-toolbox-dir))
     (if (and dir (/= dir ""))
       (progn
         (setq f (strcat dir "/xy_block_erase.lsp"))
         (if (findfile f) (load f))
       )
     )
     (if (findfile "xy_block_erase.lsp") (load (findfile "xy_block_erase.lsp")))
   )
)
(defun c:KSC () (if c:XY_BLOCK_ERASE (c:XY_BLOCK_ERASE) (princ "\n[��] ���ȼ��� xy_block_erase.lsp")))
(defun c:XSC () (c:KSC))
(defun c:BDE () (c:KSC))






(defun pick_layer_from_entity (prompt-msg / ent lyr)

  (setvar "ERRNO" 0)

  (setq ent (entsel prompt-msg))

  (cond

    (ent (cdr (assoc 8 (entget (car ent)))))

    (t nil)

  )

)



(defun add_to_lay_dict (dict lay vp / entry)

  (setq entry (assoc lay dict))

  (if entry

    (subst (cons lay (cons vp (cdr entry))) entry dict)

    (cons (list lay vp) dict)

  )

)



(defun sync_vp_freeze_via_vplayer (src tar / ss i vp elist id lay 

                                    src-frozen tar-frozen acad-list xdata

                                    freeze-dict thaw-dict old-tab old-tilemode

                                    lay-name vp-list ss-cur total-updated)

  (setq total-updated 0 freeze-dict nil thaw-dict nil)

  (setq ss (ssget "X" '((0 . "VIEWPORT"))))

  (if ss

    (progn

      (repeat (setq i (sslength ss))

        (setq vp (ssname ss (setq i (1- i))))

        (setq elist (entget vp '("ACAD")))

        (setq id (cdr (assoc 69 elist)))

        (setq lay (cdr (assoc 410 elist)))

        (if (and id (> id 1) lay)

          (progn

            (setq xdata (assoc -3 elist))

            (setq src-frozen nil tar-frozen nil)

            (if xdata

              (progn

                (setq acad-list (assoc "ACAD" (cdr xdata)))

                (if acad-list

                  (foreach item (cdr acad-list)

                    (if (= (car item) 1003)

                      (progn

                        (if (= (strcase (cdr item)) (strcase src)) (setq src-frozen T))

                        (if (= (strcase (cdr item)) (strcase tar)) (setq tar-frozen T))

                      )

                    )

                  )

                )

              )

            )

            (if (/= src-frozen tar-frozen)

              (if src-frozen

                (setq freeze-dict (add_to_lay_dict freeze-dict lay vp))

                (setq thaw-dict (add_to_lay_dict thaw-dict lay vp))

              )

            )

          )

        )

      )

      (setq old-tab (getvar "CTAB"))

      (setq old-tilemode (getvar "TILEMODE"))

      (foreach item freeze-dict

        (setq lay-name (car item) vp-list (cdr item))

        (setvar "CTAB" lay-name)

        (command "_.pspace")

        (setq ss-cur (ssadd))

        (foreach vp_item vp-list (ssadd vp_item ss-cur))

        (command "_.vplayer" "_freeze" tar "_select" ss-cur "" "")

        (setq total-updated (+ total-updated (length vp-list)))

      )

      (foreach item thaw-dict

        (setq lay-name (car item) vp-list (cdr item))

        (setvar "CTAB" lay-name)

        (command "_.pspace")

        (setq ss-cur (ssadd))

        (foreach vp_item vp-list (ssadd vp_item ss-cur))

        (command "_.vplayer" "_thaw" tar "_select" ss-cur "" "")

        (setq total-updated (+ total-updated (length vp-list)))

      )

      (setvar "CTAB" old-tab)

      (setvar "TILEMODE" old-tilemode)

    )

  )

  total-updated

)



(defun get_or_create_layer_dict (lyr_name / lyr_ent lyr_dxf dict_id vla_lyr)

  (setq lyr_ent (tblobjname "LAYER" lyr_name))

  (if lyr_ent

    (progn

      (setq lyr_dxf (entget lyr_ent))

      (setq dict_id (cdr (assoc 360 lyr_dxf)))

      (if (not dict_id)

        (vl-catch-all-apply

          '(lambda ()

             (setq vla_lyr (vlax-ename->vla-object lyr_ent))

             (setq dict_id (vlax-vla-object->ename (vla-GetExtensionDictionary vla_lyr)))

           )

        )

      )

      dict_id

    )

  )

)



(defun clean_xrecord_dxf (dxf / res in_reactor)

  (setq res nil in_reactor nil)

  (foreach item dxf

    (cond

      ((and (= (car item) 102) (= (cdr item) "{ACAD_REACTORS")) (setq in_reactor T))

      ((and in_reactor (= (car item) 102) (= (cdr item) "}")) (setq in_reactor nil))

      (in_reactor nil)

      ((member (car item) '(-1 5 330)) nil)

      (T (setq res (cons item res)))

    )

  )

  (reverse res)

)



(defun sync_vp_dictionary_overrides (src tar / src_dict tar_dict 

                                       ovr_keys key src_xrec_dxf clean_dxf new_xrec cloned_names res_add)

  (setq cloned_names nil)

  (setq src_dict (get_or_create_layer_dict src))

  (if src_dict

    (progn

      (setq tar_dict (get_or_create_layer_dict tar))

      (if tar_dict

        (progn

          (setq ovr_keys '(

            ("ADSK_XREC_LAYER_COLOR_OVR" . "")

            ("ADSK_XREC_LAYER_LINETYPE_OVR" . "")

            ("ADSK_XREC_LAYER_LINEWT_OVR" . "")

            ("ADSK_XREC_LAYER_ALPHA_OVR" . "")

            ("ADSK_XREC_LAYER_PLOTSTYLE_OVR" . "")

          ))

          (foreach item ovr_keys

            (setq key (car item))

            (setq src_xrec_dxf (dictsearch src_dict key))

            (if src_xrec_dxf

              (progn

                (setq clean_dxf (clean_xrecord_dxf src_xrec_dxf))

                (setq new_xrec (entmakex clean_dxf))

                (if new_xrec

                  (progn

                    (vl-catch-all-apply '(lambda () (dictremove tar_dict key)))

                    (setq res_add (dictadd tar_dict key new_xrec))

                    (if res_add (setq cloned_names (cons (cdr item) cloned_names)))

                  )

                )

              )

              (vl-catch-all-apply '(lambda () (dictremove tar_dict key)))

            )

          )

        )

      )

    )

  )

  (reverse cloned_names)

)



(defun execute_layer_merge (src tar bylayer_flag inherit_vp_flag / 

                            *error* doc blks layers count blk_count 

                            vp_count cloned_ovr_list ss i ent obj 

                            vla_tar old_echo old_regen old_layoutctl)

  (setq doc (if (vlax-get-acad-object) (vla-get-activedocument (vlax-get-acad-object))))

  (defun *error* (msg)

    (if old_echo (setvar "CMDECHO" old_echo))

    (if old_regen (princ))

    (if old_layoutctl (princ))

    (if doc (vla-endundomark doc))

    (if (and msg (not (member msg '("Function cancelled" "quit / exit abort"))))

      (princ (strcat "\n[��]: " msg))

    )

    (princ)

  )



  (setq old_echo (getvar "CMDECHO"))

  (setq old_regen (getvar "REGENMODE"))

  (setq old_layoutctl (getvar "LAYOUTREGENCTL"))

  (setvar "CMDECHO" 0)

  (princ)

  (princ)



  (if doc (vla-startundomark doc))

  (if doc (setq layers (vla-get-layers doc)))

  (if doc (setq blks (vla-get-blocks doc)))

  ;; ��Ŀ��ͼ��棬��������򶯴������̳�Դͼ����
  (if (null (tblsearch "LAYER" tar))
    (vl-catch-all-apply
      '(lambda ( / src-data src-ent)
         (setq src-ent (tblobjname "LAYER" src))
         (setq src-data (if src-ent (entget src-ent)))
         (entmake
           (vl-remove-if 'null
             (list
               '(0 . "LAYER")
               '(100 . "AcDbSymbolTableRecord")
               '(100 . "AcDbLayerTableRecord")
               (cons 2 tar)
               '(70 . 0)
               (cons 62 (if (assoc 62 src-data) (cdr (assoc 62 src-data)) 7))
               (if (assoc 6 src-data) (assoc 6 src-data))
               (if (assoc 370 src-data) (assoc 370 src-data))
               (if (assoc 290 src-data) (assoc 290 src-data))
             )
           )
         )
       )
    )
  )



  ;; ��

  (if (= (strcase (getvar "CLAYER")) (strcase src))

    (setvar "CLAYER" tar)

  )



  ;; 

  (setq vp_count 0 cloned_ovr_list nil)

  (if inherit_vp_flag

    (progn

      (setq vp_count (sync_vp_freeze_via_vplayer src tar))

      (setq cloned_ovr_list (sync_vp_dictionary_overrides src tar))

    )

  )



  ;; 

  (setq count 0)

  (setq ss (ssget "X" (list (cons 8 src))))

  (if ss

    (repeat (setq i (sslength ss))

      (setq ent (ssname ss (setq i (1- i))))

      (vl-catch-all-apply

        '(lambda ()

           (setq obj (vlax-ename->vla-object ent))

           (vla-put-layer obj tar)

           (if bylayer_flag

             (progn

               (vla-put-color obj 256)

               (vla-put-linetypename obj "ByLayer")

             )

           )

           (setq count (1+ count))

         )

      )

    )

  )



  ;; 

  (setq blk_count 0)

  (if blks (vlax-for blk blks

    (vl-catch-all-apply

      '(lambda ()

         (if (and (= (vla-get-IsXRef blk) :vlax-false)

                  (= (vla-get-IsLayout blk) :vlax-false))

           (vlax-for subobj blk

             (if (= (strcase (vla-get-layer subobj)) (strcase src))

               (progn

                 (vla-put-layer subobj tar)

                 (if bylayer_flag

                   (progn

                     (vla-put-color subobj 256)

                     (vla-put-linetypename subobj "ByLayer")

                   )

                 )

                 (setq blk_count (1+ blk_count))

               )

             )

           )

         )

  )
  )

    )

  )



  ;; 

  (vl-catch-all-apply '(lambda () (vla-delete (if layers (vla-item layers src)))))



  ;; 

  (vl-catch-all-apply

    '(lambda ()

       (setq vla_tar (vlax-ename->vla-object (tblobjname "LAYER" tar)))

       (vla-put-description vla_tar (vla-get-description vla_tar))

       (entupd (tblobjname "LAYER" tar))

     )

  )



  (princ)

  (princ)

  (setvar "CMDECHO" old_echo)

  (vla-endundomark doc)

  ;; (vla-regen doc acAllViewports) �Ƴ�ȫ�������ɣ��ž�����



  (princ (strcat "\n======================================================="

                 "\n>>> : [ " src " ]  [ " tar " ]"

                 "\n    - : " (itoa count) "  ( ByLayer)"

                 "\n    - : " (itoa blk_count) " "

                 "\n    - : " (itoa vp_count) " "

                 "\n=======================================================\n"))

  (princ (strcat "\n [XY������] ͼ�� [" src "] �ѳɹ��ϲ��� [" tar "]�������� " (itoa count) " ͼԪ��\n"))

  (princ)

)



;; ========================================================

;;  3:  ( + )

;; ========================================================



;; ========================================================
;; ģ�� 3: ͼ��ϲ��ӻ�������� DCL
;; ========================================================

(if (null *last_tar_layer*) (setq *last_tar_layer* nil))
(if (null *last_src_layer*) (setq *last_src_layer* nil))

;; ========================================================
;;  :     (HB2 / XHB / fast_merge)
;;  ��  DCL��     C#  
;; ========================================================
(defun c:fast_merge ( / )
  (xy:load-latest-dll)
  (cond
    ;; 1.  C#   LISP      
    ((vl-position "XY_SHOW_LAYERMERGE" (atoms-family 1))
     (xy_show_layermerge)
    )
    ;; 2.     _.HBDLG  (  HB2  )
    (t
     (if (vl-catch-all-error-p (vl-catch-all-apply 'vl-cmdf (list "_.HBDLG")))
       (if (vl-catch-all-error-p (vl-catch-all-apply 'vl-cmdf (list "_.XHB")))
         (c:XYSZ)
       )
     )
    )
  )
  (princ)
)

(defun c:HB2 () (c:fast_merge))
(defun c:hb2 () (c:fast_merge))
(defun c:XHB () (c:fast_merge))
(defun c:xhb () (c:fast_merge))

(defun c:fast_merge_quick ()
  ;; ����ʹ�� .NET ԭ���ڴ漶���ٺ� (0.005�룬��ռ��У������)
  (xy:load-latest-dll)
  (if (not (vl-catch-all-error-p (vl-catch-all-apply 'vl-cmdf (list "_.FASTMERGE"))))
    (princ)
    (progn
      (setvar "CMDECHO" 0)
      (princ "\n=======================================================")
      (princ "\n>>> ��ͼ��� (�ⵯ����ѡ��) <<<")
      (setq src-lyr (pick_layer_from_entity "\n1. ����ͼ���ϵ�ѡԴͼ�㡿ͼ: "))
      (if src-lyr
        (progn
          (princ (strcat " -> ��Դͼ��: [" src-lyr "]"))
          (setq *last_src_layer* src-lyr)
          (setq tar-lyr (pick_layer_from_entity 
                          (if *last_tar_layer*
                            (strcat "\n2. ���ѡĿ��ͼ��ͼ (ֱ�ӻس�Ĭ: [" *last_tar_layer* "]): ")
                            "\n2. ����ͼ���ϵ�ѡĿ��ͼ��ͼ: ")))
          (if (and (null tar-lyr) *last_tar_layer*)
            (setq tar-lyr *last_tar_layer*)
          )
          (if tar-lyr
            (if (= (strcase src-lyr) (strcase tar-lyr))
              (princ "\n[��ʾ] Դͼ�����ͼ����ͬ������ϲ�")
              (progn
                (setq *last_tar_layer* tar-lyr)
                (execute_layer_merge src-lyr tar-lyr T T)
              )
            )
            (princ "\n[��ʾ] ָ����ͼ�㣬������ȡ��")
          )
        )
      )
      (princ)
    )
  )
)


(defun c:BLOCK_LAYER_ZERO ( / ss i ent blk-ref true-name eff-name processed-names doc blks blk-def count)

  (setq doc (if (vlax-get-acad-object) (vla-get-activedocument (vlax-get-acad-object))))

  (if doc (setq blks (vla-get-blocks doc)))

  ;; ��Ŀ��ͼ��棬��������򶯴������̳�Դͼ����
  (if (null (tblsearch "LAYER" tar))
    (vl-catch-all-apply
      '(lambda ( / src-data src-ent)
         (setq src-ent (tblobjname "LAYER" src))
         (setq src-data (if src-ent (entget src-ent)))
         (entmake
           (vl-remove-if 'null
             (list
               '(0 . "LAYER")
               '(100 . "AcDbSymbolTableRecord")
               '(100 . "AcDbLayerTableRecord")
               (cons 2 tar)
               '(70 . 0)
               (cons 62 (if (assoc 62 src-data) (cdr (assoc 62 src-data)) 7))
               (if (assoc 6 src-data) (assoc 6 src-data))
               (if (assoc 370 src-data) (assoc 370 src-data))
               (if (assoc 290 src-data) (assoc 290 src-data))
             )
           )
         )
       )
    )
  )

  (princ "\n=======================================================")

  (princ "\n0: B0 / 0B")

  (princ "\n>>  (/): ")

  (setq ss (ssget '((0 . "INSERT"))))

  (if (null ss) (setq ss (ssget "X" '((0 . "INSERT")))))

  (if ss

    (progn

      (if doc (vla-startundomark doc))

      (setq processed-names nil count 0)

      (repeat (setq i (sslength ss))

        (setq ent (ssname ss (setq i (1- i))))

        (setq blk-ref (vlax-ename->vla-object ent))

        (setq true-name (vla-get-name blk-ref))

        (setq eff-name (vla-get-effectivename blk-ref))

        (foreach bname (list true-name eff-name)

          (if (and bname (not (member bname processed-names)))

            (progn

              (setq processed-names (cons bname processed-names))

              (vl-catch-all-apply

                '(lambda ()

                   (setq blk-def (vla-item blks bname))

                   (vlax-for subobj blk-def

                     (vl-catch-all-apply '(lambda () (vla-put-layer subobj "0")))

                   )

                 )

              )

            )

          )

        )

        (entupd ent)

        (setq count (1+ count))

      )

      (vla-endundomark doc)

      ;; (vla-regen doc acAllViewports) �Ƴ�ȫ�������ɣ��ž�����

      (princ (strcat "\n======================================================="

                     "\n>>> 0:"

                     "\n    : " (itoa count) " "

                     "\n    (): " (itoa (length processed-names)) " "

                     "\n     0  100% "

                     "\n=======================================================\n"))

    )

    (princ "\n>>> �Ħ�\n")

  )

  (princ)

)



;; ========================================================

;;  5: 

;; ========================================================



;; ========================================================
;; ģ�� 5: ������ִ�ƽ (MW / MY / UNIVERSAL_MOVE_TEXT)
;; ���ȵ��� .NET ԭ�� DrawJig��֧������ڱ�����Ӧ����������ʵʱ��ק�������룩
;; ========================================================

(defun c:UNIVERSAL_MOVE_TEXT ( / )
  (vl-catch-all-apply 'xy:load-latest-dll)

;; �����ض�������ը���ڴ�ɾ��ģ (KSC/XSC)
(vl-catch-all-apply
  '(lambda ( / dir f )
     (setq dir (xy:get-toolbox-dir))
     (if (and dir (/= dir ""))
       (progn
         (setq f (strcat dir "/xy_block_erase.lsp"))
         (if (findfile f) (load f))
       )
     )
     (if (findfile "xy_block_erase.lsp") (load (findfile "xy_block_erase.lsp")))
   )
)
(defun c:KSC () (if c:XY_BLOCK_ERASE (c:XY_BLOCK_ERASE) (princ "\n[��] ���ȼ��� xy_block_erase.lsp")))
(defun c:XSC () (c:KSC))
(defun c:BDE () (c:KSC))

  (vl-cmdf "_.MW")
  (princ)
)


(defun c:DETECT_ENTITY ( / ent obj obj-name blk-name atts)

  (princ "\n: TC")

  (setq ent (entsel "\n>> : "))

  (if ent

    (progn

      (setq obj (vlax-ename->vla-object (car ent)))

      (setq obj-name (vla-get-objectname obj))

      (princ (strcat "\n : [" obj-name "]"))

      (if (= obj-name "AcDbBlockReference")

        (progn

          (setq blk-name (vla-get-effectivename obj))

          (princ (strcat "\n : [" blk-name "]"))

          (if (= (vla-get-hasattributes obj) :vlax-true)

            (progn

              (setq atts (vlax-invoke obj 'getattributes))

              (princ (strcat "\n  " (itoa (length atts)) " :"))

              (foreach att atts

                (princ (strcat "\n   -> : [" (vla-get-tagstring att) "] | : [" (vla-get-textstring att) "]"))

              )

            )

            (princ "\n  ()")

          )

        )

      )

    )

  )

  (princ)

)



;; ========================================================

;;  6:  INI  (DCL )

;; ========================================================



(defun str_split_tokens (s delim / i len ch cur res)

  (setq res nil cur "")

  (if (and s (= (type s) 'STR))

    (progn

      (setq len (strlen s) i 1)

      (while (<= i len)

        (setq ch (substr s i 1))

        (if (vl-string-search ch delim)

          (progn

            (if (/= cur "") (setq res (cons cur res) cur ""))

          )

          (setq cur (strcat cur ch))

        )

        (setq i (1+ i))

      )

      (if (/= cur "") (setq res (cons cur res)))

    )

  )

  (reverse res)

)



(defun read_shortcuts_config ( / ini_path f line k val res)

  (setq ini_path (findfile "shortcuts.ini"))
  (if (null ini_path) (setq ini_path (strcat (xy:get-toolbox-dir) "/shortcuts.ini")))

  (setq res nil)

  (if (findfile ini_path)

    (progn

      (setq f (open ini_path "r"))

      (if f

        (progn

          (while (setq line (read-line f))

            (setq line (vl-string-trim " \t\r\n" line))

            ;; ���� UTF-8 BOM ������������

            (while (and (> (strlen line) 0) 

                        (< (ascii (substr line 1 1)) 32))

              (setq line (substr line 2))

            )

            (if (and (/= line "") (/= (substr line 1 1) ";") (/= (substr line 1 1) "#") (/= (substr line 1 1) "["))

              (if (vl-string-search "=" line)

                (progn

                  (setq k (substr line 1 (vl-string-search "=" line)))

                  (setq val (substr line (+ (vl-string-search "=" line) 2)))

                  ;; ȥ������ BOM �򲻼���

                  (setq k (vl-string-trim " \t\r\n" k))

                  (if (vl-string-search "CMD_MERGE_QUICK" k) (setq k "CMD_MERGE_QUICK"))

                  (if (and (vl-string-search "CMD_MERGE" k) (/= k "CMD_MERGE_QUICK")) (setq k "CMD_MERGE"))

                  (if (vl-string-search "CMD_BLOCK_ZERO" k) (setq k "CMD_BLOCK_ZERO"))

                  (if (vl-string-search "CMD_MOVE_TEXT" k) (setq k "CMD_MOVE_TEXT"))

                  (if (vl-string-search "CMD_DETECT" k) (setq k "CMD_DETECT"))

                  (if (vl-string-search "CMD_PANEL" k) (setq k "CMD_PANEL"))

                  (setq res (cons (cons k (vl-string-trim " \t\r\n" val)) res))

                )

              )

            )

          )

          (close f)

        )

      )

    )

  )

  res

)



(defun save_shortcuts_config (cfg_alist / ini_path f)

  (setq ini_path (findfile "shortcuts.ini"))
  (if (null ini_path) (setq ini_path (strcat (xy:get-toolbox-dir) "/shortcuts.ini")))

  (setq f (open ini_path "w"))

  (if f

    (progn

      (write-line "; ========================================================" f)

      (write-line "; XY���� - �ݼ������ļ� (shortcuts.ini)" f)

      (write-line "; �ֺŸ����������ĺ����� REKEY ��������ˢ����Ч" f)

      (write-line "; ========================================================" f)

      (write-line "" f)

      (write-line "[Shortcuts]" f)

      (setq _val (cdr (assoc "CMD_MERGE_QUICK" cfg_alist)))
      (if (null _val) (setq _val "HB1"))
      (write-line (strcat "CMD_MERGE_QUICK=" _val) f)

      (setq _val (cdr (assoc "CMD_MERGE" cfg_alist)))
      (if (null _val) (setq _val "HB2"))
      (write-line (strcat "CMD_MERGE=" _val) f)

      (setq _val (cdr (assoc "CMD_BLOCK_ZERO" cfg_alist)))
      (if (null _val) (setq _val "B0"))
      (write-line (strcat "CMD_BLOCK_ZERO=" _val) f)

      (setq _val (cdr (assoc "CMD_MOVE_TEXT" cfg_alist)))
      (if (null _val) (setq _val "MW"))
      (write-line (strcat "CMD_MOVE_TEXT=" _val) f)

      (setq _val (cdr (assoc "CMD_DETECT" cfg_alist)))
      (if (null _val) (setq _val "TC"))
      (write-line (strcat "CMD_DETECT=" _val) f)

      (setq _val (cdr (assoc "CMD_PANEL" cfg_alist)))
      (if (null _val) (setq _val "XYSZ,ZYSZ"))
      (write-line (strcat "CMD_PANEL=" _val) f)

      (write-line "ENABLE_00_LEGACY=0" f)

      (close f)

      T

    )

    nil

  )

)



(defun bind_alias_to_command (alias_str target_sym / tokens tok cmd_sym)

  (if (and alias_str (/= alias_str "") target_sym)

    (progn

      (setq tokens (str_split_tokens alias_str ",; "))

      (foreach tok tokens

        (setq tok (strcase (vl-string-trim " \t\r\n" tok)))

        (if (/= tok "")

          (progn

            (setq cmd_sym (read (strcat "c:" tok)))

            (eval (list 'defun cmd_sym '() (list target_sym)))

          )

        )

      )

    )

  )

)



(defun apply_all_toolbox_shortcuts ( / cfg cmd_quick cmd_merge cmd_b0 cmd_my cmd_tc cmd_panel)

  (setq cfg (read_shortcuts_config))

  (setq cmd_quick (cdr (assoc "CMD_MERGE_QUICK" cfg)))
  (if (or (null cmd_quick) (= cmd_quick "")) (setq cmd_quick "HB1,LM,HC"))

  (setq cmd_merge (cdr (assoc "CMD_MERGE" cfg)))
  (if (or (null cmd_merge) (= cmd_merge "")) (setq cmd_merge "HB2"))

  (setq cmd_b0    (cdr (assoc "CMD_BLOCK_ZERO" cfg)))
  (if (or (null cmd_b0) (= cmd_b0 "")) (setq cmd_b0 "B0,0B"))

  (setq cmd_my    (cdr (assoc "CMD_MOVE_TEXT" cfg)))
  (if (or (null cmd_my) (= cmd_my "")) (setq cmd_my "MW,MY"))

  (setq cmd_tc    (cdr (assoc "CMD_DETECT" cfg)))
  (if (or (null cmd_tc) (= cmd_tc "")) (setq cmd_tc "TC"))

  (setq cmd_panel (cdr (assoc "CMD_PANEL" cfg)))
  (if (or (null cmd_panel) (= cmd_panel "")) (setq cmd_panel "XGJ,XYSZ"))

  (setq cmd_freeze (cdr (assoc "CMD_FREEZE_DEEP" cfg)))
  (if (or (null cmd_freeze) (= cmd_freeze "")) (setq cmd_freeze "XDJ,XDK,XDF"))
  (setq cmd_ime   (cdr (assoc "CMD_IME" cfg)))
  (if (or (null cmd_ime) (= cmd_ime "")) (setq cmd_ime "XSR,XSRF,IME"))



  ;; �������п�ݼ� (�ֱ������Сд�����)

  (bind_alias_to_command cmd_quick 'c:fast_merge_quick)

  (bind_alias_to_command cmd_merge 'c:fast_merge)

  (bind_alias_to_command cmd_b0    'c:BLOCK_LAYER_ZERO)

  (bind_alias_to_command cmd_my    'c:UNIVERSAL_MOVE_TEXT)

  (bind_alias_to_command cmd_tc    'c:DETECT_ENTITY)

  (bind_alias_to_command cmd_panel 'c:XYSZ)
  (bind_alias_to_command cmd_freeze 'c:DEEP_FREEZE)
  (bind_alias_to_command cmd_ime   'c:IME)



  ;; ���û��Զ���ݼ��볣�ÿ�ݼ�����ʹ��

  (if (null (read "c:HB1")) (defun c:HB1 () (c:fast_merge_quick)))

  (if (null (read "c:HB2")) (defun c:HB2 () (c:fast_merge)))

  (if (null (read "c:MW"))  (defun c:MW  () (c:UNIVERSAL_MOVE_TEXT)))

  (if (null (read "c:B0"))  (defun c:B0  () (c:BLOCK_LAYER_ZERO)))

  (if (null (read "c:TC"))  (defun c:TC  () (c:DETECT_ENTITY)))
  (defun c:DEEP_FREEZE () (command "XDJ") (princ))
  (if (null (read "c:XDJ")) (defun c:XDJ () (command "XDJ") (princ))
(defun c:XJD () (command "XJD") (princ))
(defun c:XHF () (command "XHF") (princ)))
  (if (null (read "c:XDK")) (defun c:XDK () (command "XDJ") (princ)))
  (if (null (read "c:XDF")) (defun c:XDF () (command "XDJ") (princ)))
  (if (null (read "c:IME")) (defun c:IME () (command "IME") (princ)))
  (if (null (read "c:XSR"))  (defun c:XSR  () (command "XSR") (princ)))
  (if (null (read "c:XSRF")) (defun c:XSRF () (command "XSR") (princ)))

  T

)



;; ========================================================

;; ���ʲ����������� (XYSZ / ZYSZ)

;; ========================================================

(defun c:XYSZ ()
  (if (null (vl-position "C:XYPANEL" (atoms-family 1)))
    (xy:load-latest-dll)
  )
  (vl-cmdf "XYPANEL")
  (princ)
)

(defun c:xysz () (c:XYSZ))

(defun c:ZYSZ () (c:XYSZ))

(defun c:zysz () (c:XYSZ))



;; ========================================================

;; �ݼ������������� (SETKEY / KJSZ / CFG)

;; ========================================================

(defun c:SETKEY ()

  (xy:load-latest-dll)

  (if (vl-position "C:SETKEY" (atoms-family 1))

    (vl-cmdf "SETKEY")

    (progn

      (setq ini_path (findfile "shortcuts.ini"))
  (if (null ini_path) (setq ini_path (strcat (xy:get-toolbox-dir) "/shortcuts.ini")))

      (if (findfile ini_path)

        (progn

          (startapp "notepad.exe" ini_path)

          (princ "\n [XY������] ���ڼ��´򿪽ݼ��ļ� shortcuts.ini")

          (princ "\n �Ĳ�������������� REKEY ��������ˢ����Ч��\n")

        )

        (alert "�ҵ��ݼ������ļ� shortcuts.ini")

      )

    )

  )

  (princ)

)

(defun c:setkey () (c:SETKEY))

(defun c:KJSZ () (c:SETKEY))

(defun c:kjsz () (c:SETKEY))

(defun c:CFG () (c:SETKEY))

(defun c:cfg () (c:SETKEY))



;; ========================================================

;; �ݼ��������� (REKEY / RELOADKEY)

;; ========================================================

(defun c:REKEY ( / cfg _mq _m _b0 _my _tc _p)

  (apply_all_toolbox_shortcuts)

  (setq cfg (read_shortcuts_config))

  (setq _mq (cdr (assoc "CMD_MERGE_QUICK" cfg)))
  (if (null _mq) (setq _mq "HB1"))

  (setq _m  (cdr (assoc "CMD_MERGE" cfg)))
  (if (null _m) (setq _m "HB2"))

  (setq _b0 (cdr (assoc "CMD_BLOCK_ZERO" cfg)))
  (if (null _b0) (setq _b0 "B0"))

  (setq _my (cdr (assoc "CMD_MOVE_TEXT" cfg)))
  (if (null _my) (setq _my "MW"))

  (setq _tc (cdr (assoc "CMD_DETECT" cfg)))
  (if (null _tc) (setq _tc "TC"))

  (setq _p  (cdr (assoc "CMD_PANEL" cfg)))
  (if (null _p) (setq _p "XYSZ,ZYSZ"))

  (princ "\n========================================================")

  (princ "\n   [XY������] �ݼ��ѳɹ����¼�����Ч��")

  (princ (strcat "\n  1. ͼ�㼫�� (�ⵯ)   :  " _mq " "))

  (princ (strcat "\n  2. ͼ��ϲ���� (ѡ��)   :  " _m  " "))

  (princ (strcat "\n  3. ͼ�����0       :  " _b0 " "))

  (princ (strcat "\n  4. �������ͬƽ      :  " _my " "))

  (princ (strcat "\n  5. �ײ�ͼԪ��̽        :  " _tc " "))

  (princ (strcat "\n  6. ����������ݼ�      :  " _p  " "))

  (princ "\n========================================================\n")

  (princ)

)

(defun c:rekey () (c:REKEY))



;; ========================================================

;; ��ʱӦ�ýݼ�����ӡ����ʾ

;; ========================================================

(apply_all_toolbox_shortcuts)



(setq _cfg (read_shortcuts_config))

(setq _mq (cdr (assoc "CMD_MERGE_QUICK" _cfg)))
(if (null _mq) (setq _mq "HB1"))

(setq _m  (cdr (assoc "CMD_MERGE" _cfg)))
(if (null _m) (setq _m "HB2"))

(setq _b0 (cdr (assoc "CMD_BLOCK_ZERO" _cfg)))
(if (null _b0) (setq _b0 "B0"))

(setq _my (cdr (assoc "CMD_MOVE_TEXT" _cfg)))
(if (null _my) (setq _my "MW"))

(setq _tc (cdr (assoc "CMD_DETECT" _cfg)))
(if (null _tc) (setq _tc "TC"))

(setq _p  (cdr (assoc "CMD_PANEL" _cfg)))
(if (null _p) (setq _p "XYSZ,ZYSZ"))



(princ "\n========================================================")

(princ "\n   XY������ȫ�׹��߼� - ���سɹ�")

(princ (strcat "\n  1. ͼ�㼫�� (�ⵯ)   :  " _mq " "))

(princ (strcat "\n  2. ͼ��ϲ���� (ѡ��)   :  " _m  " "))

(princ (strcat "\n  3. ͼ�����0       :  " _b0 " "))

(princ (strcat "\n  4. �������ͬƽ      :  " _my " "))

(princ (strcat "\n  5. �ײ�ͼԪ��̽        :  " _tc " "))

(princ "\n  --------------------------------------------------------")

(princ (strcat "\n   ����ʲ��������  :  " _p " "))

(princ "\n   ����ݼ�����        :  SETKEY / KJSZ ")

(princ "\n   �ݼ����¼�����Ч      :  REKEY ")

(princ "\n========================================================\n")

(setq _cfg nil _mq nil _m nil _b0 nil _my nil _tc nil _p nil)

(princ)


;; ========================================================
;; �����û�����ݼ� (shortcuts.lsp)
;; ========================================================
(if (findfile "shortcuts.lsp")
  (load (findfile "shortcuts.lsp"))
    (let ((sc (findfile "shortcuts.lsp")))
    (if (null sc)
      (setq sc (findfile (strcat (xy:get-toolbox-dir) "/shortcuts.lsp")))
    )
    (if (and sc (findfile sc))
      (load sc)
    )
  )
)
(princ)

;; ========================================================
;; �������� (������临�Ӿ����������� RELOAD �����뼶ˢ��)
;; ========================================================
;; �����ӿ�ͼ�㸲�� (VPR)
(defun c:VPR ( / doc lt ltr )
  (vl-load-com)
  (setq doc (if (vlax-get-acad-object) (vla-get-activedocument (vlax-get-acad-object))))
  (if doc
    (vl-catch-all-apply
      '(lambda ()
         (vla-startundomark doc)
         (vlax-for ltr (vla-get-layers doc)
           (vl-catch-all-apply
             '(lambda ()
                (vla-put-description ltr (vla-get-description ltr))
              )
           )
         )
         (vla-endundomark doc)
         ;; (vla-regen doc acAllViewports) �Ƴ�ȫ�������ɣ��ž�����
       )
    )
  )
  (princ "\n [XY������] �ӿ�ͼ�㸲���ѳɹ��أ�\n")
  (princ)
)
(defun c:reset_vp_overrides () (c:VPR))

;; ��������������� (PU)
(defun c:PU ( / )
  (setvar "CMDECHO" 0)
  (command "_.purge" "_all" "*" "_no")
  (command "_.audit" "_y")
  (setvar "CMDECHO" 1)
  (princ "\n [XY������] ͼֽ����������޸���ɣ�\n")
  (princ)
)


;; ========================================================
;; XY���� - Xǰ׺ƴ������ĸ��׼���ָ� (�溣/��)
;; ========================================================
(defun c:XGJ ()
  (if (not *XY-TOOLBOX-LOADED*) (xy:load-latest-dll))
  (if (not (vl-cmdf "XGJ"))
    (if (not (vl-cmdf "XYPANEL"))
      (if c:XYSZ (c:XYSZ) (princ "\n[XY������] �������̨ʧ�ܣ����ȼ��� XY�����䣡"))
    )
  )
  (princ)
)
(defun c:xgj () (c:XGJ))
(defun c:XYGJ () (c:XGJ))
(defun c:xygj () (c:XGJ))
(defun c:XHB () (c:fast_merge))
(defun c:xhb () (c:fast_merge))
(defun c:HB2 () (c:fast_merge))
(defun c:hb2 () (c:fast_merge))
(defun c:XJH () (if c:fast_merge_quick (c:fast_merge_quick) (princ "\n���ȼ��� XY�����䣡")))
(defun c:XDJ () (command "XDJ") (princ))
(defun c:XJD () (command "XJD") (princ))
(defun c:XHF () (command "XHF") (princ))
(defun c:XDK () (command "XDJ") (princ))
(defun c:XTC () (if c:detect_sub_entity (c:detect_sub_entity) (princ "\n���ȼ��� XY�����䣡")))
(defun c:XSY () (if c:MW (c:MW) (princ "\n���ȼ��� XY�����䣡")))
(defun c:xsy () (c:XSY))
(defun c:XGA () (c:XSY))
(defun c:xga () (c:XSY))
(defun c:XGL () (if c:B0 (c:B0) (princ "\n���ȼ��� XY�����䣡")))
(defun c:XKL () (c:XGL))
(defun c:FZW () (vl-cmdf "FZW"))
(defun c:fzw () (vl-cmdf "FZW"))
(defun c:XFZ () (vl-cmdf "FZW"))
(defun c:xfz () (vl-cmdf "FZW"))
(defun c:XKB () (vl-cmdf "XSTRETCH"))
(defun c:XLS () (vl-cmdf "XSTRETCH"))
(defun c:XSK () (vl-cmdf "_.XSK") (princ))
(defun c:xsk () (c:XSK))
(defun c:XQL () (c:PU))
(defun c:XKJ () (if c:SETKEY (c:SETKEY) (princ "\n���ȼ��� XY�����䣡")))

(defun c:RELOAD ( / path dir erase_f )
  (setq dir (xy:get-toolbox-dir))
  (if (and dir (/= dir ""))
    (setq path (strcat dir "/autoload.lsp"))
    (setq path (findfile "autoload.lsp"))
  )
  (if (and path (findfile path))
    (progn
      (load path)
      (if (and dir (/= dir ""))
        (progn
          (setq erase_f (strcat dir "/xy_block_erase.lsp"))
          (if (findfile erase_f) (load erase_f))
        )
      )
      (defun c:KSC () (if c:XY_BLOCK_ERASE (c:XY_BLOCK_ERASE) (princ "\n[��ʾ] ���������͸ɾ��ģ��...")))
      (defun c:XSC () (c:KSC))
      (defun c:BDE () (c:KSC))
      (if apply_all_toolbox_shortcuts
        (apply_all_toolbox_shortcuts)
      )
      ;; ��ȫ���� C# ����̨���ˢ�£�������Ѵ�����
      (vl-catch-all-apply 'xy:load-latest-dll)
      (vl-catch-all-apply 'vl-cmdf (list "XY_UPDATE_PANEL"))
      (princ "\n========================================================\n")
      (princ "�� [XY������] ����������ȫ�׿�ݼ������سɹ� (XCZ)��\n")
      (princ "�� ���������ȫ�׹�����ˢ�£�\n")
      (princ "  - SK  / SKYS : �ӿ�ͼ����ʽ�������� (��͸ʰȡ/��ɫ/������)\n")
      (princ "  - FZW / XFZ  : ͼ�龵������ԭλ��͸���� (���������ϸ�����)\n")
      (princ "  - XKB / XLS  : ԭλ����λ�༭��͸ͼ��������ƽ��\n")
      (princ "  - XHB / XJH  : �淶��ͼ��ϲ����뼫�ٵ�ѡ�ϲ�\n")
      (princ "  - KSC / XSC  : ��ը����͸ɾ������ͼԪ (֧��M������/��ռ)\n")
      (princ "  - XGX / UPDATE: ��ҵ�����߼�����ϵͳ\n")
      (princ "  - XGJ / XYGJ : ��߹���̨���\n")
      (princ "========================================================\n")
    )
    (princ "\n[����] δ�ҵ� autoload.lsp �ļ�\n")
  )
  (princ)
)
(defun c:XCZ () (c:RELOAD))
(defun c:XYLOAD () (c:RELOAD))
;; (c:XY removed to avoid conflict with Hailong)

;; Ĭ�ͼ��أ�ȷ CAD ��������ʱ����̨���ڶ���ʾ״ (�뺣/��һ)
(vl-catch-all-apply
  '(lambda ()
       (princ "
[XY������] ���ĳ����Ѿ�Ĭ�ͣ��� XGJ �򿪹���̨�� XKJ ���ýݼ�
")
   )
)
(princ)
(defun c:SK () (vl-cmdf "_.SK") (princ))
(defun c:sk () (c:SK))
(defun c:SKYS () (vl-cmdf "_.SKYS") (princ))
(defun c:skys () (c:SKYS))
(defun c:SKTC () (vl-cmdf "_.SKTC") (princ))
(defun c:sktc () (c:SKTC))
(defun c:XGX () (vl-cmdf "_.XGX") (princ))
(defun c:xgx () (c:XGX))
(defun c:UPDATE () (c:XGX))
(defun c:update () (c:XGX))

;; --- 智能文字选择与批量过滤 (XTX / XSW / XTXD / XTXH / XTXC) ---
(defun c:XTX () (vl-cmdf "_.XTX") (princ))
(defun c:xtx () (c:XTX))
(defun c:XSW () (vl-cmdf "_.XSW") (princ))
(defun c:xsw () (c:XSW))
(defun c:XTXD () (vl-cmdf "_.XTXD") (princ))
(defun c:xtxd () (c:XTXD))
(defun c:XTXH () (vl-cmdf "_.XTXH") (princ))
(defun c:xtxh () (c:XTXH))
(defun c:XTXC () (vl-cmdf "_.XTXC") (princ))
(defun c:xtxc () (c:XTXC))
