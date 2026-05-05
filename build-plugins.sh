#!/usr/bin/env bash
# Builds the topi native library for all three Unity-supported platforms
# (macOS, Linux, Windows) in ReleaseSafe and copies the artifacts into
# this package's Plugins/ folder.
#
# Default layout assumes the topiary repo is a sibling of this package:
#   ~/peartree-games/topiary
#   ~/peartree-games/topiary-unity
# Override with TOPIARY_REPO=/path/to/topiary if your layout differs.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOPIARY_REPO="${TOPIARY_REPO:-${SCRIPT_DIR}/../topiary}"
PLUGINS_DIR="${SCRIPT_DIR}/Plugins"
PLUGINS_X64="${PLUGINS_DIR}/x64"

if [[ ! -f "${TOPIARY_REPO}/build.zig" ]]; then
    echo "error: topiary repo not found at ${TOPIARY_REPO}" >&2
    echo "       set TOPIARY_REPO=/path/to/topiary to override" >&2
    exit 1
fi

mkdir -p "${PLUGINS_X64}"

# build_for <target-triple> <label> <src1> <dst1> [<src2> <dst2> ...]
# Wipes zig-out between builds so artifacts from one platform don't leak
# into the next; copies pairs of src/dst paths after the build succeeds.
build_for() {
    local target="$1" ; shift
    local label="$1" ; shift
    echo
    echo "==> [${label}] zig build -Dtarget=${target} -Doptimize=ReleaseSafe"
    (
        cd "${TOPIARY_REPO}"
        rm -rf zig-out
        zig build -Dtarget="${target}" -Doptimize=ReleaseSafe
    )
    while [[ $# -gt 0 ]]; do
        local src="$1" ; shift
        local dst="$1" ; shift
        if [[ ! -f "${TOPIARY_REPO}/${src}" ]]; then
            echo "error: expected ${src} not produced by ${target} build" >&2
            exit 1
        fi
        cp "${TOPIARY_REPO}/${src}" "${dst}"
        echo "    cp ${src#zig-out/} -> ${dst#${SCRIPT_DIR}/}"
    done
}

build_for x86_64-macos       macOS \
    zig-out/lib/libtopi.dylib "${PLUGINS_X64}/libtopi.dylib"

build_for x86_64-linux-gnu   Linux \
    zig-out/lib/libtopi.so    "${PLUGINS_X64}/libtopi.so"

build_for x86_64-windows-gnu Windows \
    zig-out/lib/topi.dll      "${PLUGINS_X64}/topi.dll" \
    zig-out/lib/topi.lib      "${PLUGINS_DIR}/topi.lib" \
    zig-out/lib/topi.pdb      "${PLUGINS_DIR}/topi.pdb"

echo
echo "Done. New artifacts in ${PLUGINS_DIR}/."
