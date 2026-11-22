$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'GreeControl.Native\GreeControl.Native.csproj'
$output = Join-Path $PSScriptRoot 'dist-features'
$app = Join-Path $output 'GreeControl.exe'

if (!(Get-Command node.exe -ErrorAction SilentlyContinue)) {
    throw 'Node.js não foi encontrado. Instale o Node.js para ativar a conexão local com o Gree.'
}

Get-Process -Name GreeControl -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet publish $project -c Release -r win-x64 --self-contained false -o $output
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $app)) {
    throw 'Não foi possível compilar o aplicativo nativo.'
}

Start-Process -FilePath $app -WorkingDirectory $output -Wait

