;;; emacs-test.el --- Integration testing script          -*- lexical-binding: t; -*-

;; Copyright (c) Microsoft Corporation.
;; Licensed under the MIT License.

;; Author: Andy Schwartzmeyer <andschwa@microsoft.com>
;; Keywords: PowerShell, LSP

;;; Code:

(require 'ert)

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

(require 'flymake)

(unless (package-installed-p 'powershell)
  (package-refresh-contents)
  (package-install 'powershell))
(require 'powershell)

(unless (package-installed-p 'eglot)
  (package-refresh-contents)
  (package-install 'eglot))
(require 'eglot)

(ert-deftest powershell-editor-services ()
  "Eglot should connect to PowerShell Editor Services."
  (let* ((repo (project-root (project-current)))
         (start-script (expand-file-name "module/PowerShellEditorServices/Start-EditorServices.ps1" repo))
         (module-path (expand-file-name "module" repo))
         (log-path (expand-file-name "test/emacs-test.log" repo))
         (session-path (expand-file-name "test/emacs-session.json" repo))
         (test-script (expand-file-name "test/PowerShellEditorServices.Test.Shared/Debugging/VariableTest.ps1" repo))
         (eglot-sync-connect t))
    (add-to-list
     'eglot-server-programs
     `(powershell-mode
       . ("pwsh" "-NoLogo" "-NoProfile" "-Command" ,start-script
          "-HostName" "Emacs" "-HostProfileId" "Emacs" "-HostVersion" "1.0.0"
          "-BundledModulesPath" ,module-path
          "-LogPath" ,log-path "-LogLevel" "Diagnostic"
          "-SessionDetailsPath" ,session-path
          "-Stdio")))
    (with-current-buffer (find-file-noselect test-script)
      (should (eq major-mode 'powershell-mode))
      (should (apply #'eglot--connect (eglot--guess-contact)))
      (should (eglot-current-server))
      (let ((lsp (eglot-current-server)))
        (should (string= (oref lsp project-nickname) "PowerShellEditorServices"))
        (should (eq (oref lsp major-mode) 'powershell-mode))
        (should (string= (oref lsp language-id) "powershell")))
      (sleep-for 3) ; TODO: Wait for "textDocument/publishDiagnostics" instead
      (flymake-start)
      (goto-char (point-min))
      (flymake-goto-next-error)
      (should (eq 'flymake-warning (face-at-point))))))

(provide 'emacs-test)
;;; emacs-test.el ends here
