;;; emacs-test.el --- Integration testing script          -*- lexical-binding: t; -*-

;; Copyright (c) Microsoft Corporation.
;; Licensed under the MIT License.

;; Author: Andy Jordan <andy.jordan@microsoft.com>
;; Keywords: PowerShell, LSP

;;; Code:

;; Avoid using old packages.
(setq load-prefer-newer t)

;; Improved TLS Security.
(with-eval-after-load 'gnutls
  (custom-set-variables
   '(gnutls-verify-error t)
   '(gnutls-min-prime-bits 3072)))

;; Package setup.
(require 'package)
(add-to-list 'package-archives
             '("melpa" . "https://melpa.org/packages/") t)
(package-initialize)
(package-refresh-contents)

(require 'ert)

(require 'flymake)

(unless (package-installed-p 'powershell)
  (package-install 'powershell))
(require 'powershell)

(unless (package-installed-p 'eglot)
  (package-install 'eglot))
(require 'eglot)

(ert-deftest powershell-editor-services ()
  "Eglot should connect to PowerShell Editor Services."
  (let* ((repo (project-root (project-current)))
         (start-script (expand-file-name "module/PowerShellEditorServices/Start-EditorServices.ps1" repo))
         (test-script (expand-file-name "test/PowerShellEditorServices.Test.Shared/Debugging/VariableTest.ps1" repo))
         (eglot-sync-connect t))
    (add-to-list
     'eglot-server-programs
     `(powershell-mode
       . ("pwsh" "-NoLogo" "-NoProfile" "-Command" ,start-script "-Stdio")))
    (with-current-buffer (find-file-noselect test-script)
      (should (eq major-mode 'powershell-mode))
      (should (apply #'eglot--connect (eglot--guess-contact)))
      (should (eglot-current-server))
      (let ((lsp (eglot-current-server)))
        (should (string= (eglot--project-nickname lsp) "PowerShellEditorServices"))
        (should (member (cons 'powershell-mode "powershell") (eglot--languages lsp))))
      (sleep-for 5) ; TODO: Wait for "textDocument/publishDiagnostics" instead
      (flymake-start)
      (goto-char (point-min))
      (flymake-goto-next-error)
      (should (eq 'flymake-warning (face-at-point))))))

(provide 'emacs-test)
;;; emacs-test.el ends here
