#!/usr/bin/env bash
# Fetches prebuilt topi native binaries from a topiary GitHub release and
# copies them into this package's Plugins/ folder.
#
# Usage:
#   ./fetch-plugins.sh <version>
# where <version> is a topiary release tag, with or without the leading "v"
# (e.g. "0.21.2", "v0.21.2"). See https://github.com/peartreegames/topiary/releases.
#
# Companion to build-plugins.sh:
#   build-plugins.sh — cross-compiles current Zig sources locally. Required
#                      while iterating on Zig-side changes that aren't tagged.
#   fetch-plugins.sh — grabs an already-tagged release. Faster, no Zig
#                      toolchain needed, but only valid once the desired
#                      changes have been published as a topiary release.

set -euo pipefail

if [[ $# -lt 1 ]]; then
    echo "usage: $0 <version>" >&2
    echo "       e.g. $0 v0.21.2" >&2
    exit 2
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGINS_DIR="${SCRIPT_DIR}/Plugins"
PLUGINS_X64="${PLUGINS_DIR}/x64"

VERSION="$1"
[[ "${VERSION}" == v* ]] || VERSION="v${VERSION}"

REPO="peartreegames/topiary"
BASE_URL="https://github.com/${REPO}/releases/download/${VERSION}"

mkdir -p "${PLUGINS_X64}"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT

# fetch_and_extract <archive-name>
# Downloads to ${WORK_DIR} and extracts in place; the topiary release CI
# packages each platform under a top-level dir matching the target triple
# (e.g. topiary-x86_64-windows.zip extracts to ./x86_64-windows/{lib,bin}/).
fetch_and_extract() {
    local archive="$1"
    local dest="${WORK_DIR}/${archive}"
    local url="${BASE_URL}/${archive}"
    echo
    echo "==> ${archive}"
    curl --fail --location --progress-bar -o "${dest}" "${url}"
    case "${archive}" in
        *.zip)    unzip -q -o "${dest}" -d "${WORK_DIR}" ;;
        *.tar.gz) tar -xzf "${dest}" -C "${WORK_DIR}" ;;
        *)        echo "error: unknown archive type ${archive}" >&2; exit 1 ;;
    esac
}

# copy <relative-src-in-workdir> <absolute-dst>
copy() {
    local src="$1"
    local dst="$2"
    if [[ ! -f "${WORK_DIR}/${src}" ]]; then
        echo "error: expected ${src} not found in extracted archive" >&2
        exit 1
    fi
    cp "${WORK_DIR}/${src}" "${dst}"
    echo "    cp ${src} -> ${dst#${SCRIPT_DIR}/}"
}

echo "Fetching topi binaries from ${REPO} ${VERSION}"

fetch_and_extract "topiary-x86_64-macos.tar.gz"
copy "x86_64-macos/lib/libtopi.dylib"   "${PLUGINS_X64}/libtopi.dylib"

fetch_and_extract "topiary-x86_64-linux.tar.gz"
copy "x86_64-linux/lib/libtopi.so"      "${PLUGINS_X64}/libtopi.so"

fetch_and_extract "topiary-x86_64-windows.zip"
copy "x86_64-windows/lib/topi.dll"      "${PLUGINS_X64}/topi.dll"
copy "x86_64-windows/lib/topi.lib"      "${PLUGINS_DIR}/topi.lib"
copy "x86_64-windows/lib/topi.pdb"      "${PLUGINS_DIR}/topi.pdb"

echo
echo "Done. Artifacts from ${VERSION} in ${PLUGINS_DIR}/."
