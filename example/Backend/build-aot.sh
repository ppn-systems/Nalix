#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────
# Cross-platform Native AOT build script for Nalix Backend (Linux/macOS)
# Usage:
#   ./build-aot.sh [OPTIONS]
#
# Options:
#   -r, --runtime   RID: linux-x64 | linux-arm64 | win-x64  (default: auto)
#   -o, --output    Custom output directory
#   -c, --clean     Clean obj/bin/publish before building
#   --no-trim       Disable IL trimming
#   -h, --help      Show this help
# ─────────────────────────────────────────────────────────────────────
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/Backend.csproj"

# ── Defaults ─────────────────────────────────────────────────────────
RUNTIME=""
OUTPUT=""
CLEAN=false
TRIM=true

# ── Parse args ───────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        -r|--runtime)  RUNTIME="$2"; shift 2 ;;
        -o|--output)   OUTPUT="$2";  shift 2 ;;
        -c|--clean)    CLEAN=true;   shift   ;;
        --no-trim)     TRIM=false;   shift   ;;
        -h|--help)
            head -16 "$0" | tail -14
            exit 0 ;;
        *)
            echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

# ── Auto-detect RID ─────────────────────────────────────────────────
if [[ -z "$RUNTIME" ]]; then
    ARCH=$(uname -m)
    OS=$(uname -s)
    case "$OS-$ARCH" in
        Linux-x86_64)   RUNTIME="linux-x64"   ;;
        Linux-aarch64)  RUNTIME="linux-arm64"  ;;
        Darwin-arm64)   RUNTIME="osx-arm64"    ;;
        Darwin-x86_64)  RUNTIME="osx-x64"      ;;
        *)              RUNTIME="linux-x64"
                        echo "[warn] Unknown platform ($OS/$ARCH), defaulting to $RUNTIME" ;;
    esac
fi

PUBLISH_DIR="${OUTPUT:-$SCRIPT_DIR/publish/$RUNTIME}"

# ── Banner ───────────────────────────────────────────────────────────
cat <<EOF
===========================================================
  Nalix Backend — Native AOT Build
  Runtime  : $RUNTIME
  Output   : $PUBLISH_DIR
  Config   : Release
  Trim     : $( $TRIM && echo 'ON' || echo 'OFF' )
===========================================================
EOF

# ── Clean ────────────────────────────────────────────────────────────
if $CLEAN; then
    echo "[clean] Removing obj / bin / publish ..."
    rm -rf "$SCRIPT_DIR/obj" "$SCRIPT_DIR/bin" "$SCRIPT_DIR/publish"
fi

# ── Publish ──────────────────────────────────────────────────────────
START_TIME=$SECONDS

# Use /p:AotBuild=true — a LOCAL property consumed only by Backend.csproj.
# All AOT/trim/ILC knobs are declared in the csproj to avoid leaking global
# properties to netstandard2.0 analyzer projects (which causes NETSDK1124).
PUBLISH_ARGS=(
    "publish" "$PROJECT"
    "-c" "Release"
    "-r" "$RUNTIME"
    "--self-contained" "true"
    "-o" "$PUBLISH_DIR"
    "/p:AotBuild=true"
    "/p:PublishSingleFile=true"
    "/p:EnableCompressionInSingleFile=true"
)

# Optionally disable trimming (for AOT warning diagnostics)
if ! $TRIM; then
    PUBLISH_ARGS+=("/p:NoTrim=true")
fi

dotnet "${PUBLISH_ARGS[@]}"

ELAPSED=$(( SECONDS - START_TIME ))

# ── Summary ──────────────────────────────────────────────────────────
BINARY="$PUBLISH_DIR/Backend"
[[ "$RUNTIME" == win-* ]] && BINARY="$PUBLISH_DIR/Backend.exe"

if [[ -f "$BINARY" ]]; then
    SIZE=$(du -h "$BINARY" | cut -f1)
else
    SIZE="N/A"
fi

cat <<EOF

===========================================================
  BUILD SUCCEEDED  (${ELAPSED}s)
  Binary : $BINARY
  Size   : $SIZE
===========================================================
EOF

echo ""
echo "Done. The binary is fully self-contained — no .NET runtime needed on the target."