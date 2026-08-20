#!/usr/bin/env bash
# 在 WSL/Linux 上交叉编译 naibao（.NET 8 SDK + NSIS）。
# 用法：bash build.sh
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
DOTNET="${DOTNET:-$DIR/.dotnet/dotnet}"

if [ ! -x "$DOTNET" ]; then
    echo "未找到 .NET SDK：$DOTNET"
    echo "可执行以下命令安装到本目录："
    echo "  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --install-dir \"$DIR/.dotnet\""
    exit 1
fi

export DOTNET_ROOT="$(dirname "$DOTNET")"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="$DIR/.dotnet-cli-home"
mkdir -p "$DOTNET_CLI_HOME"

echo "==> 发布 win-x64 自包含单文件..."
"$DOTNET" publish "$DIR/naibao.csproj" -c Release -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$DIR/publish/win-x64"

echo "==> 生成 NSIS 安装包..."
MAKENSIS="$DIR/.tools/nsis/root/usr/bin/makensis"
if [ -x "$MAKENSIS" ]; then
    export NSISDIR="$DIR/.tools/nsis/root/usr/share/nsis"
    (cd "$DIR/installer" && "$MAKENSIS" naibao.nsi)
else
    echo "未找到 makensis，跳过安装包生成（可单独运行 installer/naibao.nsi）。"
fi

echo "==> 打包绿色版压缩包..."
python3 - "$DIR" <<'PY'
import sys, zipfile, os
base = os.path.join(sys.argv[1], "publish", "win-x64")
out = os.path.join(sys.argv[1], "publish", "naibao-portable-1.1.3.zip")
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for name in sorted(os.listdir(base)):
        z.write(os.path.join(base, name), f"naibao/{name}")
PY

echo "==> 完成："
ls -lh "$DIR/publish/win-x64/naibao.exe" "$DIR/publish"/naibao-setup-*.exe "$DIR/publish"/naibao-portable-*.zip 2>/dev/null || true
