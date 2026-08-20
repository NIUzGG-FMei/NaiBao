# 在 Windows 10 上构建 naibao。
# 需要安装 .NET 8 SDK：https://dotnet.microsoft.com/download/dotnet/8.0
$ErrorActionPreference = "Stop"

dotnet publish .\naibao.csproj -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o .\publish\win-x64

Write-Host "==> 打包绿色版压缩包..."
Compress-Archive -Path .\publish\win-x64\* -DestinationPath .\publish\naibao-portable-1.1.0.zip -Force

Write-Host "==> 产物：.\publish\win-x64\naibao.exe"
Write-Host "==> 产物：.\publish\naibao-portable-1.1.0.zip"
Write-Host "==> 如需安装包：安装 NSIS 后运行  makensis .\installer\naibao.nsi"
