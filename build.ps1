$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $projectRoot 'src'
$distDir = Join-Path $projectRoot 'dist'
$compilerCandidates = @(
    'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not $compiler -and (Test-Path -LiteralPath $vswhere)) {
    $compiler = & $vswhere -latest -products '*' -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
}
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'

if (-not $compiler) {
    throw '找不到 Visual Studio 2022 Roslyn C# 编译器，请先安装 Visual Studio Build Tools 的 .NET 桌面生成工具。'
}

New-Item -ItemType Directory -Path $distDir -Force | Out-Null
$output = Join-Path $distDir 'WindowMemory.exe'
$sources = Get-ChildItem -LiteralPath $sourceDir -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName
$references = @(
    (Join-Path $framework 'mscorlib.dll'),
    (Join-Path $framework 'System.dll'),
    (Join-Path $framework 'System.Core.dll'),
    (Join-Path $framework 'System.Xml.dll'),
    (Join-Path $framework 'System.Xaml.dll'),
    (Join-Path $framework 'System.Runtime.Serialization.dll'),
    (Join-Path $framework 'System.Drawing.dll'),
    (Join-Path $framework 'System.Windows.Forms.dll'),
    (Join-Path $wpf 'WindowsBase.dll'),
    (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'PresentationFramework.dll')
)

$arguments = @(
    '/nologo',
    '/noconfig',
    '/nostdlib+',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    '/debug:pdbonly',
    '/langversion:latest',
    ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')),
    ('/out:' + $output)
)
foreach ($reference in $references) { $arguments += '/reference:' + $reference }
$arguments += $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $projectRoot 'portable.flag') -Destination (Join-Path $distDir 'portable.flag') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $distDir 'README.md') -Force
New-Item -ItemType Directory -Path (Join-Path $distDir 'Data') -Force | Out-Null

Write-Output "生成完成：$output"
