let s:suite = themis#suite('pses')
let s:assert = themis#helper('assert')

function s:wait_for_diagnostics(bufname, expected)
  let l:attempts = 20
  while l:attempts > 0
    if getbufvar(a:bufname, 'LanguageClient_statusLineDiagnosticsCounts') == a:expected
      return
    endif

    execute 'sleep 500m'
    let l:attempts -= 1
  endwhile
endfunction

function s:suite.before()
  let l:pses_path = g:repo_root . '/module'
  let g:LanguageClient_serverCommands = {
    \ 'ps1': [ 'pwsh', '-NoLogo', '-NoProfile', '-Command',
    \   l:pses_path . '/PowerShellEditorServices/Start-EditorServices.ps1', '-Stdio' ]
    \ }
  let g:LanguageClient_serverStderr = 'DEBUG'
  let g:LanguageClient_loggingFile = g:repo_root . '/LanguageClient.log'
  let g:LanguageClient_serverStderr = g:repo_root . '/LanguageServer.log'
endfunction

function s:suite.has_language_client()
  call s:assert.includes(&runtimepath, g:repo_root . '/LanguageClient-neovim')
  call s:assert.cmd_exists('LanguageClientStart')
  call s:assert.not_empty(g:LanguageClient_serverCommands)
  call s:assert.true(LanguageClient#HasCommand('ps1'))
endfunction

function s:suite.analyzes_powershell_file()
  let l:test_file = resolve(g:repo_root . '/test/vim-test.ps1')
  execute 'view ' . fnameescape(l:test_file)

  let l:bufnr = bufnr('vim-test.ps1$')
  call s:assert.not_equal(l:bufnr, -1)
  let l:bufinfo = getbufinfo(l:bufnr)[0]

  call s:assert.equal(resolve(l:bufinfo.name), l:test_file)
  call s:assert.includes(getbufline(l:bufinfo.bufnr, 1), 'function Do-Work {}')
  execute 'buffer ' . l:bufinfo.bufnr
  setlocal filetype=ps1
  call s:assert.equal(&filetype, 'ps1')

  execute 'LanguageClientStart'
  call LanguageClient#textDocument_didOpen()
  call LanguageClient#textDocument_didChange()

  let l:actual_diagnostics = {}
  for l:attempt in range(1, 30)
    let l:actual_diagnostics = getbufvar(l:bufinfo.bufnr, 'LanguageClient_statusLineDiagnosticsCounts')
    if type(l:actual_diagnostics) == v:t_dict
      break
    endif

    execute 'sleep!' 1
  endfor

  call s:assert.equal(type(l:actual_diagnostics), v:t_dict)
endfunction
