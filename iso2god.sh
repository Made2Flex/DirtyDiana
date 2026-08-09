#!/usr/bin/env bash

# ------------------------------------------------------------
# Extract Xbox 360 game ISO to GOD files using iso2god-rs
# ------------------------------------------------------------

set -Eeuo pipefail

VERSION="0.1.1"
AUTHOR="TWFkZTJGbGV4"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ISO2GOD="$SCRIPT_DIR/iso2god"

get_arch_asset_name() {
    local arch uname_os asset=""
    arch="$(uname -m)"
    uname_os="$(uname -s | tr '[:upper:]' '[:lower:]')"

    if [[ "$uname_os" == "linux" ]]; then
        if [[ "$arch" == "x86_64" ]]; then
            asset="iso2god-x86_64-linux"
        elif [[ "$arch" == "aarch64" || "$arch" == "arm64" ]]; then
            asset="iso2god-aarch64-linux"
        fi
    elif [[ "$uname_os" == "darwin" ]]; then
        if [[ "$arch" == "x86_64" ]]; then
            asset="iso2god-x86_64-macos"
        elif [[ "$arch" == "arm64" || "$arch" == "aarch64" ]]; then
            asset="iso2god-aarch64-macos"
        fi
    elif [[ "$uname_os" =~ mingw|msys|cygwin ]]; then
        if [[ "$arch" == "x86_64" ]]; then
            asset="iso2god-x86_64-windows.exe"
        fi
    fi

    echo "$asset"
}

is_iso2god() {
    local asset_name binary_file download_url tmp_file

    asset_name="$(get_arch_asset_name)"
    if [[ -z "$asset_name" ]]; then
        echo "[ERROR] Unable to determine suitable iso2god binary for this architecture ($(uname -m), $(uname -s))."
        exit 1
    fi

    if [[ "$asset_name" == *.exe ]]; then
        ISO2GOD="$SCRIPT_DIR/iso2god.exe"
    else
        ISO2GOD="$SCRIPT_DIR/iso2god"
    fi

    if [[ -x "$ISO2GOD" ]]; then
        echo "[+] Found iso2god executable in script directory."
        return 0
    fi

    if command -v iso2god &>/dev/null; then
        ISO2GOD="$(command -v iso2god)"
        echo "[+] Found iso2god executable in PATH."
        return 0
    fi

    echo "[!] iso2god-rs not found. Downloading latest release..."
    echo "[i] Platform asset: $asset_name"

    if ! command -v curl &>/dev/null; then
        echo "[ERROR] curl is required to download iso2god, but was not found."
        exit 1
    fi

    binary_file="$(basename "$ISO2GOD")"
    download_url="https://github.com/iliazeus/iso2god-rs/releases/latest/download/$asset_name"
    tmp_file="$(mktemp "$SCRIPT_DIR/.iso2god-download.XXXXXX")"

    echo "[*] Downloading latest iso2god-rs binary..."

    if ! curl --fail --location --retry 3 --retry-all-errors --connect-timeout 10 --output "$tmp_file" "$download_url"; then
        rm -f "$tmp_file"
        echo "[ERROR] Download failed."
        echo "[ERROR] URL: $download_url"
        exit 1
    fi

    if [[ ! -s "$tmp_file" ]]; then
        rm -f "$tmp_file"
        echo "[ERROR] Download completed but the file is empty."
        exit 1
    fi

    if ! mv -f "$tmp_file" "$ISO2GOD"; then
        rm -f "$tmp_file"
        echo "[ERROR] Failed to install downloaded binary: $ISO2GOD"
        exit 1
    fi

    chmod +x "$ISO2GOD"

    if [[ ! -x "$ISO2GOD" ]]; then
        echo "[ERROR] Downloaded iso2god binary is not executable."
        exit 1
    fi

    echo "[+] iso2god is ready: $SCRIPT_DIR/$binary_file"
}

extract_game_iso() {
    if (( $# < 2 )); then
        echo "Usage: $0 [thread_count] <game.iso> <output-dir>"
        exit 1
    fi

    # default threads to 4
    if [[ "$1" =~ ^[0-9]+$ ]]; then
        THREADS="$1"
        GAME_ISO="$2"
        OUTDIR="$3"
        shift
    else
        THREADS="4"
        GAME_ISO="$1"
        OUTDIR="$2"
    fi

    if [[ -z "$GAME_ISO" || -z "$OUTDIR" ]]; then
        echo "Usage: $0 [thread_count] <game.iso> <output-dir>"
        exit 1
    fi

    if [[ ! -f "$GAME_ISO" ]]; then
        echo "[ERROR] ISO not found: $GAME_ISO"
        exit 1
    fi

    is_iso2god

    echo "[*] Extracting '$GAME_ISO' to '$OUTDIR' using $THREADS thread(s)..."
    "$ISO2GOD" -j "$THREADS" --trim "$GAME_ISO" "$OUTDIR"
    status=$?

    if [[ $status -eq 0 ]]; then
        echo "[+] Extraction complete."
    else
        echo "[ERROR] Extraction failed with status $status."
        exit $status
    fi
}

# Main
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    extract_game_iso "$@"
fi
