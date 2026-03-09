#!/usr/bin/env bash

# ------------------------------------------------------------
# Extract Xbox 360 game ISO to GOD files using iso2god-rs
# ------------------------------------------------------------

set -Eeuo pipefail

VERSION="0.1.0"
AUTHOR="TWFkZTJGbGV4"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ISO2GOD="./iso2god"

is_iso2god() {
    # Check if iso2god is available in PATH
    if command -v "$ISO2GOD" &>/dev/null; then
        echo "[+] Found iso2god executable in PATH.."
        return 0
    fi
    # Check if executable exists in script dir
    if [[ -x "$SCRIPT_DIR/iso2god" ]]; then
        echo "[+] Found iso2god executable in tree.."
        return 0
    fi

    echo "[*] iso2god-rs not found. Downloading..."
    ZIP_URL="https://github.com/iliazeus/iso2god-rs/releases/latest/download/iso2god-x86_64-unknown-linux-gnu.zip"
    ZIP_FILE="iso2god.zip"

    cd "$SCRIPT_DIR" || exit 1

    # Download if not already present
    if ! command -v curl &>/dev/null; then
        echo "[ERROR] curl is required to download iso2god, but not found."
        exit 1
    fi

    curl -L -o "$ZIP_FILE" "$ZIP_URL" || {
        echo "[ERROR] Download failed."
        exit 1
    }

    # Extract the zip
    if ! command -v unzip &>/dev/null; then
        echo "[ERROR] unzip is required to extract iso2god, but not found."
        exit 1
    fi

    unzip -o "$ZIP_FILE" -d "$SCRIPT_DIR" || {
        echo "[ERROR] Extraction failed."
        exit 1
    }

    chmod +x "$SCRIPT_DIR/iso2god"
    rm -f "$ZIP_FILE"
    echo "[+] iso2god is ready."
}

extract_game_iso() {
    if (( $# < 2 )); then
        echo "Usage: $0 [thread_count] <game.iso> <output-dir>"
        exit 1
    fi

    THREADS="${1:-4}" # Default to 4 threads
    GAME_ISO="$2"
    OUTDIR="$3"


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
