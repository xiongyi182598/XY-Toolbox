;; --- XY工具箱 本地动态引导器 (Auto-generated) ---
(vl-load-com)
(if (getvar "SECURELOAD") (setvar "SECURELOAD" 0))
(vl-catch-all-apply
  '(lambda ( / p )
     (if (setq p (findfile "autoload.lsp"))
       (load p)
     )
   )
)
;; --------------------------------------------------
