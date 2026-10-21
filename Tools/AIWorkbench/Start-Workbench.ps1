$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
Write-Host 'Open http://127.0.0.1:8765 in your browser. Keep this terminal running; Ctrl+C stops it.'
python server.py
