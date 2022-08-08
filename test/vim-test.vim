let s:suite = themis#suite('pses')
let s:assert = themis#helper('assert')

function s:suite.before()
  let l:pses_path = g:repo_root . '/module'
  let g:LanguageClient_serverCommands = {
    \ 'ps1': ['pwsh', '-NoLogo', '-NoProfile', '-Command',
    \   l:pses_path . '/PowerShellEditorServices/Start-EditorServices.ps1',
    \   '-HostName', 'vim', '-HostProfileId', 'vim', '-HostVersion', '1.0.0',
    \   '-BundledModulesPath', l:pses_path, '-Stdio',
    \   '-LogPath', g:repo_root . '/pses.log', '-LogLevel', 'Diagnostic',
    \   '-SessionDetailsPath', g:repo_root . '/pses_session.json' ]
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
  view test/vim-test.ps1 " This must not use quotes!

  let l:bufnr = bufnr('vim-test.ps1$')
  call s:assert.not_equal(l:bufnr, -1)
  let l:bufinfo = getbufinfo(l:bufnr)[0]

  call s:assert.equal(l:bufinfo.name, g:repo_root . '/test/vim-test.ps1')
  call s:assert.includes(getbufline(l:bufinfo.name, 1), 'function Do-Work {}')
  " TODO: This shouldn't be necessary, vim-ps1 works locally but not in CI.
  call setbufvar(l:bufinfo.bufnr, '&filetype', 'ps1')
  call s:assert.equal(getbufvar(l:bufinfo.bufnr, '&filetype'), 'ps1')

  execute 'LanguageClientStart'
  execute 'sleep' 5
  call s:assert.equal(getbufvar(l:bufinfo.name, 'LanguageClient_isServerRunning'), 1)
  call s:assert.equal(getbufvar(l:bufinfo.name, 'LanguageClient_projectRoot'), g:repo_root)
  call s:assert.equal(getbufvar(l:bufinfo.name, 'LanguageClient_statusLineDiagnosticsCounts'), {'E': 0, 'W': 1, 'H': 0, 'I': 0})
endfunction
