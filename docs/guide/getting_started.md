# Getting Started
PowerShell Editor Services project provides a Language Server Protocol (LSP)
HTTP server that runs outside the editor.  The server supplies rich editor
functionality like code completion, syntax highlighting, and code annotation.
This document will guide you through getting a minimal setup working with
several editors.

## Editors
1. [Neovim](#neovim)

## Neovim

### Install the Server
Download and extract the PowerShell Editor Services [releases page](https://github.com/PowerShell/PowerShellEditorServices/releases)
server to a directory of your choice.  Remember the path that you extract the
project into.
```powershell
$DownloadUrl = 'https://github.com/PowerShell/PowerShellEditorServices/releases/latest/download/PowerShellEditorServices.zip';
$ZipPath = "$HOME/Desktop/PowerShellEditorServices.zip";
$InstallPath = "$HOME/Desktop/PowerShellEditorServices";
Invoke-WebRequest -Method 'GET' -Uri $DownloadUrl -OutFile $ZipPath;
Expand-Archive -Path $ZipPath -DestinationPath $InstallPath;
```

### Install Neovim's Basic LSP Configurations
Neovim has a repository of basic LSP configurations for a number of languages,
including PowerShell.  Install the LSP configuration into one of the package
directories inside `$XDG_CONFIG_HOME`.  The path `$XDG_CONFIG_HOME will vary
depending on which operating system you are on:

| OS         | Path                       |
| ---------- | -------------------------- |
| Windows    | `$HOME/AppData/local/nvim` |
| *nix/macOS | `$HOME/.config/nvim`       |

The easiest way is to install the configuration is to clone the repository
using git:
```powershell
git clone https://github.com/neovim/nvim-lspconfig.git "$HOME/AppData/local/nvim/pack/complete/start/nvim-lspconfig"
```

Alternatively, you can extract the zip file into the same place:
```powershell
$DownloadUrl = 'https://github.com/neovim/nvim-lspconfig/archive/refs/heads/master.zip';
$ZipPath = "$HOME/AppData/local/nvim/nvim-lspconfig.zip";
$InstallPath = "$HOME/AppData/local/nvim/nvim-lspconfig";
Invoke-WebRequest -Method 'GET' Uri $DownloadUrl -OutFile $ZipPath;
Expand-Archive -Path $ZipPath -DestinationPath "$HOME/AppData/local/nvim/pack/complete/start/nvim-lspconfig";
```

> NOTE: If the corresponding neovim configuration and package directories have
> not been created yet, create them before cloning the LSP configuration
> repository.

### Configure the Server
Once the basic language configurations have been installed, you should add this
to your init.lua:
```lua
local on_attach = function(client, bufnr)
	-- Enable completion triggered by <c-x><c-o>
	vim.api.nvim_buf_set_option(bufnr, 'omnifunc', 'v:lua.vim.lsp.omnifunc')

	local bufopts = { noremap = true, silent = true, buffer = bufnr }
	vim.keymap.set('n', '<C-k>', vim.lsp.buf.signature_help, bufopts)
	vim.keymap.set('n', 'gD', vim.lsp.buf.declaration, bufopts)
	vim.keymap.set('n', 'gd', vim.lsp.buf.definition, bufopts)
	vim.keymap.set('n', 'gi', vim.lsp.buf.implementation, bufopts)
	vim.keymap.set('n', 'gr', vim.lsp.buf.references, bufopts)
	vim.keymap.set('n', 'K', vim.lsp.buf.hover, bufopts)
	vim.keymap.set('n', '<Leader>ca', vim.lsp.buf.code_action, bufopts)
	vim.keymap.set('n', '<Leader>f', function() vim.lsp.buf.format { async = true } end, bufopts)
	vim.keymap.set('n', '<Leader>rn', vim.lsp.buf.rename, bufopts)
	vim.keymap.set('n', '<Leader>td', vim.lsp.buf.type_definition, bufopts)
end

local bundle_path = '<Path to where PowerShell Editor Services was installed>'

require('lspconfig')['powershell_es'].setup {
	bundle_path = bundle_path,
	on_attach = on_attach
}
```

> NOTE: Be sure to set the bundle_path variable, otherwise the server will not
> know the path to start the server.

To further configure the server, you can supply settings to the setup table.
For example, you can set the code formatting preset to one true brace style
(OTBS).
```lua
require('lspconfig')['powershell_es'].setup {
	bundle_path = bundle_path,
	on_attach = on_attach,
	settings = { powershell = { codeFormatting = { Preset = 'OTBS' } } }
}
```

As another example, you can set the bundled PSScriptAnalyzer's custom rule path
like so:
```lua
require('lspconfig')['powershell_es'].setup {
	bundle_path = bundle_path,
	on_attach = on_attach,
	settings = { powershell = { scriptAnalysis = { settingsPath = os.getenv('HOME') .. 'PSScriptAnalyzerSettings.psd1' } } }
}
```
