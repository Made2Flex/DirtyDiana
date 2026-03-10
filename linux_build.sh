#!/usr/bin/env bash

# ============================================================
# DirtyDiana Linux Build Script
# ============================================================

set -Eeuo pipefail


VERSION="0.5.1"
AUTHOR=TWFkZTJGbGV4

SCRIPT_DIR="$(dirname "$(readlink -f "$0")")"
LOG_PATH="$SCRIPT_DIR/DirtyDiana.log"

CONFIG="Release"
RUNTIME="linux-x64"

PUBLISH_BIN="DirtyDiana/bin/$CONFIG/net8.0/$RUNTIME/publish/DirtyDiana"
PROJECT_PATH="DirtyDiana/DirtyDiana.csproj"
PUBLISH_PROJECT="DirtyDiana"
RUN_PATH="$HOME/Desktop/DirtyDiana"
BINARY_NAME="DirtyDiana"

# ------------------------------------------------------------
# Greeter
# ------------------------------------------------------------
greet_user() {
    echo "Hello $USER"
}

# ------------------------------------------------------------
# Version
# ------------------------------------------------------------
show_version() {
    echo "Version: $VERSION"
}

# ------------------------------------------------------------
# Clear utility
# ------------------------------------------------------------
clear_screen() {
    case "$(uname -s)" in
        Darwin)
            printf '\033[H\033[2J\033[3J'
            ;;
        *)
            clear >&2
            ;;
    esac
}

# ------------------------------------------------------------
# Determine package managers
# ------------------------------------------------------------
pkg_mgrs() {
    if command -v pacman >/dev/null 2>&1; then
        echo "arch"
    elif command -v apt-get >/dev/null 2>&1; then
        echo "debian"
    elif command -v brew >/dev/null 2>&1; then
        echo "macos"
    else
        echo "unknown"
    fi
}

# ------------------------------------------------------------
# Dependancy array
# ------------------------------------------------------------
deps() {
    case "$(pkg_mgrs)" in
        "arch")
            echo "dotnet-sdk"
            echo "clang"
            echo "gcc"
            echo "zlib"
            echo "openssl"
            ;;
        "debian")
            echo "dotnet-sdk-8.0"
            echo "build-essential"
            echo "clang"
            echo "zlib1g-dev"
            echo "libssl-dev"
            ;;
        "macos")
            echo "dotnet-sdk"
            ;;
    esac
}

# ------------------------------------------------------------
# Utility function to install dependancies
# ------------------------------------------------------------
install_deps() {

    local pkgmgr
    pkgmgr=$(pkg_mgrs)
    local missing=()

    for dep in $(deps); do
        case "$pkgmgr" in
            "arch")
                if ! pacman -Qi "$dep" >/dev/null 2>&1; then
                    missing+=("$dep")
                else
                    log_me "INFO" "$dep is already installed (pacman)."
                fi
                ;;
            "debian")
                if ! dpkg -s "$dep" >/dev/null 2>&1; then
                    missing+=("$dep")
                else
                    log_me "INFO" "$dep is already installed (apt)."
                fi
                ;;
            "macos")
                if ! brew list --cask "$dep" >/dev/null 2>&1 && ! brew list "$dep" >/dev/null 2>&1; then
                    missing+=("$dep")
                else
                    log_me "INFO" "$dep is already installed (brew)."
                fi
                ;;
        esac
    done

    # Nothing to install
    if [ ${#missing[@]} -eq 0 ]; then
        log_me "INFO" "All dependencies already installed."
        return
    fi

    log_me "INFO" "Missing dependencies: ${missing[*]}"

    case "$pkgmgr" in

        "arch")

            # Enable extra repo if needed
            if ! grep -Pzo "\[extra\][^\[]+?^\s*Include" /etc/pacman.conf | grep -q "Server"; then
                log_me "INFO" "Enabling [extra] repository in /etc/pacman.conf."
                sudo sed -i '/\[extra\]/,/^$/ s/^#\s*\(Include\|Server\)/\1/' /etc/pacman.conf
            fi

            log_me "INFO" "Installing dependencies via pacman."
            sudo pacman -Sy --needed --noconfirm "${missing[@]}"
            ;;

        "debian")

            if ! grep -qi microsoft /etc/apt/sources.list /etc/apt/sources.list.d/* 2>/dev/null; then
                log_me "INFO" "Adding Microsoft package repository."
                wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb || true
                sudo dpkg -i packages-microsoft-prod.deb || true
                rm -f packages-microsoft-prod.deb
            fi

            log_me "INFO" "Updating apt repository."
            sudo apt-get update

            log_me "INFO" "Installing dependencies via apt."
            sudo apt-get install -y "${missing[@]}"
            ;;

        "macos")

            log_me "INFO" "Installing dependencies via brew."
            brew install --cask "${missing[@]}"
            ;;

        *)

            log_me "ERROR" "Unsupported or unknown package manager."
            echo "[ERROR] Unsupported or unknown package manager."
            exit 1
            ;;

    esac
}

# ------------------------------------------------------------
# Detect .NET SDK
# ------------------------------------------------------------
detect_dotnet_sdk() {
    local sdk
    sdk=$(dotnet --info 2>/dev/null | awk '/^\.NET SDK:/ {getline; if ($1=="Version:") print $2; exit}')

    if [[ -z "$sdk" ]]; then
        echo "[!] Could not detect a .NET SDK"
        return 1
    fi

    export DOTNET_SDK="$sdk"
    return 0
}

# ------------------------------------------------------------
# Pre‑flight checks
# ------------------------------------------------------------
pre_flight() {
    if ! command -v dotnet >/dev/null 2>&1; then
        log_me "ERROR" "dotnet SDK not found in PATH, or is not installed."
        echo "[-] dotnet SDK not found in PATH, or is not installed."
        read -rp "Would you like to install dependencies now? [y/N] " answer
        answer_pf=$(printf '%s' "$answer" | tr '[:upper:]' '[:lower:]')

        case "$answer_pf" in
            y|yes)
                install_deps
                ;;
            *)
                echo "[!] Please install dependencies manually and re-run the script."
                exit 1
                ;;
        esac
    fi

    detect_dotnet_sdk

    if [[ ! -f "$PROJECT_PATH" ]]; then
        log_me "ERROR" "Project file was not found: $PROJECT_PATH."
        echo "[!] Project file was not found: $PROJECT_PATH"
        echo "[*] Make sure to place the script in the root directory"
        exit 1
    fi
}

# ------------------------------------------------------------
# Housekeeper
# ------------------------------------------------------------
clean_up() {
    clear_screen
    log_me "INFO" "Starting build process..."
    echo "[*] Starting build process..."
    echo "[*] Using .NET SDK: $DOTNET_SDK"
    log_me "INFO" "Cleaning leftovers with dotnet clean."
    echo "[*] Cleaning leftovers..."
    #sudo dotnet workload update
    if dotnet clean "$PROJECT_PATH" -c "$CONFIG"; then
        log_me "INFO" "dotnet clean completed."
    else
        log_me "ERROR" "dotnet clean failed."
        echo "[-] dotnet clean failed."
        return 1
    fi

    log_me "INFO" "Removing bin/obj artifacts."
    echo "[*] Removing bin/obj artifacts..."
    if find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +; then
        log_me "INFO" "bin/obj artifacts removed."
    else
        log_me "ERROR" "Failed to remove bin/obj artifacts."
        echo "[-] Failed to remove bin/obj artifacts."
        return 1
    fi

    log_me "INFO" "Clean complete."
    echo "[+] Clean complete."
}

wipe_clean() {
    folders_to_delete=(
        "$SCRIPT_DIR/DirtyDiana/bin"
        "$SCRIPT_DIR/DirtyDiana/obj"
        "$SCRIPT_DIR/DirtyDiana.Formatter/bin"
        "$SCRIPT_DIR/DirtyDiana.Formatter/obj"
    )

    echo "[+] Starting clean up.."

    found_any_folder=false

    for folder in "${folders_to_delete[@]}"; do
        if [ -d "$folder" ]; then
            found_any_folder=true
            log_me "INFO" "Deleting $folder"
            echo "[*] Deleting: $folder"
            rm -rf "$folder"
        else
            log_me "WARN" "Folder does not exist: $folder"
            echo "[-] Folder does not exist: $folder"
        fi
    done

    if ! $found_any_folder; then
        echo "[-] Nothing to clean"
        log_me "INFO" "No folders found to clean."
    else
        echo "[✔] Clean complete."
        log_me "INFO" "Clean wipe complete."
    fi
    log_divider
}

# ------------------------------------------------------------
# Restore
# ------------------------------------------------------------
restore() {
    log_me "INFO" "Starting restore of dependencies."
    echo "[*] Restoring dependencies..."
    if dotnet restore "$PROJECT_PATH"; then
        log_me "INFO" "Dependencies restored successfully."
        echo "[+] Restore complete."
    else
        log_me "ERROR" "Dependency restore failed."
        echo "[-] Restore failed."
        return 1
    fi
}

# ------------------------------------------------------------
# Build configuration
# ------------------------------------------------------------
configure_build() {
    log_me "INFO" "Started project configuration.."
    echo "[*] Configuring project..."

    if ! dotnet build "$PROJECT_PATH" \
        -c "$CONFIG" \
        --nologo \
        --no-restore \
        -v q \
        -p:WarningLevel=0 \
        /clp:ErrorsOnly; then
        log_me "ERROR" "Project failed to configure."
        log_divider
        return 1
    fi

    echo "[+] Build configuration complete."
    log_me "INFO" "Project configuration completed."
}

# ------------------------------------------------------------
# Build and Publish (AOT)
# ------------------------------------------------------------
publish_build() {
    log_me "INFO" "Starting self-contained AOT publish."
    echo "[*] Publishing self-contained AOT build..."

    if dotnet publish "$PUBLISH_PROJECT" \
        -c "$CONFIG" \
        -r "$RUNTIME" \
        -p:PublishSingleFile=false \
        -p:PublishAot=true \
        --self-contained true \
        -v q \
        -p:DebugType=none \
        -p:WarningLevel=0 \
        -p:Optimize=true; then
        echo "[+] Publish complete."
        log_me "INFO" "Publish complete."
    else
        echo "[!] Publishing failed."
        log_me "ERROR" "Publishing failed."
        return 1
    fi
}

# ------------------------------------------------------------
# Initialize log
# ------------------------------------------------------------
check_log() {
    local timestamp
    timestamp=$(date +"%Y-%m-%d %I:%M:%S %p")

    if [ -z "$LOG_PATH" ]; then
        echo "[ERROR] LOG_PATH is not defined."
        echo "Exiting.."
        return 1
    fi

    if [ ! -f "$LOG_PATH" ]; then
        if ! touch "$LOG_PATH" 2>/dev/null; then
            echo "[ERROR] Failed to create log file: $LOG_PATH"
            echo "Exiting.."
            return 1
        fi

        # Set permissions read/write
        if ! chmod 0644 "$LOG_PATH" 2>/dev/null; then
            echo "[WARN] Unable to set permissions (0644) on $LOG_PATH"
            echo "Continuing anyway.."
        fi

        {
            echo "[$timestamp] [INFO] Created new log file at: $LOG_PATH"
            echo "[$timestamp] [INFO] Logging Started.."
        } >> "$LOG_PATH"
    fi

    if [ ! -w "$LOG_PATH" ]; then
        echo "[ERROR] Log file not writable: $LOG_PATH"
        echo "Exiting.."
        return 1
    fi

    return 0
}

# ------------------------------------------------------------
# Logging function
# ------------------------------------------------------------
log_me() {
    # Parameters:
    #   $1 -> Log level (INFO, WARN, ERROR, DEBUG)
    #   $2 -> Log message
    #
    # Behavior:
    #   - Writes log entry to $LOG_PATH.
    #   - Rotates log file if > 5MB.
    #   - Returns 0 on success, 1 on failure.
    #   - Usage: log_me "LEVEL" "MESSAGE"
    #   - Levels: INFO, WARN, ERROR Ect..

    local level="$1"
    local message="$2"
    local timestamp max_size size rotated
    timestamp=$(date +"%Y-%m-%d %I:%M:%S %p")
    max_size=$((5 * 1024 * 1024))  # 5MB

    # Need both parameters
    if [ -z "$level" ] || [ -z "$message" ]; then
        echo "[ERROR] Missing parameters for propper logging. Both LEVEL and MESSAGE are needed"
        echo "Exiting.."
        return 1
    fi

    # Log rotation
    if [ -f "$LOG_PATH" ]; then
        size=$(stat -c%s "$LOG_PATH" 2>/dev/null || echo 0)
        if [ "$size" -ge "$max_size" ]; then
            rotated="${LOG_PATH}.1"
            mv -f "$LOG_PATH" "$rotated" 2>/dev/null
            echo "[$timestamp] [INFO] Log rotated: $rotated" >> "$LOG_PATH"
        fi
    fi

    {
        echo "[$timestamp] [$level] $message"
    } >> "$LOG_PATH" 2>/dev/null || return 1

    return 0
}

log_divider() {
    log_me "DONE" "--------------------------------------------------"
}

# ------------------------------------------------------------
# Check build and export program
# ------------------------------------------------------------
check_build() {
    if [[ -e "$PUBLISH_BIN" ]]; then
        log_me "INFO" "Build verified."
        echo "[✔] Build verified."
        echo "[*] Moving binary to Run Path."
        log_me "INFO" "Moving  binary to $RUN_PATH."
        mkdir -p "$RUN_PATH"
        if mv -f "$PUBLISH_BIN" "$RUN_PATH" \
            && cp -rf "$SCRIPT_DIR"/LICENSE "$SCRIPT_DIR"/README.md "$RUN_PATH"; then
            log_me "INFO" "Binary moved to $RUN_PATH."
            echo "[+] Binary moved to $RUN_PATH."
            log_me "INFO" "Build operation completed."
            echo "[✔] Process Completed!"
        else
            log_me "ERROR" "Something went wrong while moving Binary to $RUN_PATH."
            echo "[ERROR]! Something went wrong while moving Binary to $RUN_PATH."
        fi
    else
        log_me "ERROR" "Binary not found at: $PUBLISH_BIN."
        echo "[!] Binary not found: $PUBLISH_BIN"
    fi

    if [[ ! -x "$RUN_PATH/$BINARY_NAME" ]]; then
        log_me "ERROR" "Binary is not executable: $RUN_PATH/$BINARY_NAME."
        echo "[!] Binary is not executable: $RUN_PATH/$BINARY_NAME"
        echo "[*] Making binary executable..."
        log_me "INFO" "Making binary executable: $RUN_PATH/$BINARY_NAME."
        chmod -Rf +x "$RUN_PATH/$BINARY_NAME"
        log_me "INFO" "Binary made executable: $RUN_PATH/$BINARY_NAME."
        echo "[+] Binary made executable: $RUN_PATH/$BINARY_NAME."
    fi
    log_divider
}

# ------------------------------------------------------------
# Shows recent activity
# ------------------------------------------------------------
Show_me_log() {
    clear_screen

    if [ -z "$LOG_PATH" ]; then
        echo "[ERROR] LOG_PATH is not defined."
        log_me "ERROR" "LOG_PATH is not defined in the script."
        return 1
    fi

    if [ ! -e "$LOG_PATH" ]; then
        echo "[WARN] Log file has not been created yet."
        log_me "WARN" "User requested log. But it has not been created yet."
        return 1
    fi

    if [ ! -r "$LOG_PATH" ]; then
        echo "[ERROR] Log file is not readable."
        log_me "ERROR" "Log file is not readable."
        return 1
    fi

    if [ -f "$LOG_PATH" ] && [ -n "$LOG_PATH" ]; then
        if [ ! -s "$LOG_PATH" ]; then
            echo "[WARN] Log is empty. Nothing has been written yet."
            log_me "WARN" "User requested log. But file is empty."
            return 0
        fi
        if command -v bat >/dev/null 2>&1; then
            log_me "INFO" "Opened log with bat."
            bat --style=auto "$LOG_PATH"
        else
            log_me "INFO" "Opened log with cat."
            cat "$LOG_PATH"
        fi
    fi
}

# ------------------------------------------------------------
# Argument parser
# ------------------------------------------------------------
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -a|--author)
                show_author
                exit 0
                ;;
            -c|--clean)
                wipe_clean
                exit 0
                ;;
            -h|-H|--help)
                help_me
                exit 0
                ;;
            -v|-V|--version)
                show_version
                exit 0
                ;;
            -l|--log)
                Show_me_log
                exit 0
                ;;
            -*)
                log_me "ERROR" "Unknown option: $1"
                echo -e "Unknown option: $1"
                echo "Use --help for usage information"
                exit 1
                ;;
            *)
                log_me "ERROR" "Unexpected argument: $1"
                echo -e "Unexpected argument: $1"
                echo "Use --help for usage information"
                exit 1
                ;;
        esac
        shift
    done
}

# ------------------------------------------------------------
# Help function
# ------------------------------------------------------------
help_me() {
    clear_screen
    echo "DESCRIPTION:"
    echo "    This script will automate the building process of DirtyDiana for Linux."
    echo
    echo "USAGE:"
    echo "    $0 [OPTIONS]"
    echo
    echo "OPTIONS:"
    echo "    -h, --help          Show this help message"
    echo "    -v, --version       Show version information"
    echo "    -l, --log           Show the log"
    echo "    -c, --clean         Clean objects left behind after building"
    echo "    -a, --author        Show the author of this script"
    echo
    echo "EXAMPLES:"
    echo "    $0"
    echo "    $0 --log"
    echo "    $0 --help"
    echo
    echo "DEPENDENCIES:"
    echo "    • dotnet 8.0"
    echo "    • wine (if patching)"
}

# ------------------------------------------------------------
# Utility function
# ------------------------------------------------------------
show_author() {
    if [[ -n "$AUTHOR" ]]; then
        local decoded_author
        decoded_author=$(echo "$AUTHOR" | base64 --decode 2>/dev/null)
        if [[ $? -eq 0 ]]; then
            echo "${decoded_author}"
        else
            log_me "ERROR" "Failed to decode AUTHOR (not valid base64?)"
            echo "[ERROR] Failed to decode AUTHOR (not valid base64?)"
        fi
    else
        log_me "ERROR" "AUTHOR variable is unset."
        echo "[ERROR] AUTHOR variable is unset."
    fi
}

# ------------------------------------------------------------
# Main function
# ------------------------------------------------------------
main() {
    parse_args "$@"
    greet_user
    check_log
    pre_flight
    clean_up
    restore
    configure_build
    publish_build
    check_build
}

main "$@"
