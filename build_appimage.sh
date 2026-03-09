#!/usr/bin/env bash

set -euo pipefail

APP="DirtyDiana"
VERSION="${VERSION:-1.0.0}"
ARCH="${ARCH:-x86_64}"

SCRIPT_DIR="$(dirname "$(readlink -f "$0")")"
BINARY="$HOME/Desktop/DirtyDiana/DirtyDiana"

BUILD_DIR="build_app"
APPDIR="$BUILD_DIR/AppDir"

LINUXDEPLOY="$BUILD_DIR/linuxdeploy"
APPIMAGETOOL="$BUILD_DIR/appimagetool"

DESKTOP_FILE="$BUILD_DIR/${APP}.desktop"
ICON_FILE="$BUILD_DIR/${APP}.png"
LAUNCHER_SCRIPT="$BUILD_DIR/launch-${APP}.sh"
PUBLISH_DIR="$SCRIPT_DIR/AppImage"

mkdir -p "$BUILD_DIR"
mkdir -p "$PUBLISH_DIR"

echo "==== DirtyDiana AppImage Builder ===="

# ------------------------------------------------
# Verify binary
# ------------------------------------------------

if [ ! -f "$BINARY" ]; then
    echo "ERROR: Binary not found: $BINARY"
    exit 1
fi

# ------------------------------------------------
# Download tools if needed
# ------------------------------------------------

if [ ! -f "$LINUXDEPLOY" ]; then
    echo "Downloading linuxdeploy..."
    wget -O "$LINUXDEPLOY" \
        https://github.com/linuxdeploy/linuxdeploy/releases/download/continuous/linuxdeploy-${ARCH}.AppImage
    chmod +x "$LINUXDEPLOY"
fi

if [ ! -f "$APPIMAGETOOL" ]; then
    echo "Downloading appimagetool..."
    wget -O "$APPIMAGETOOL" \
        https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-${ARCH}.AppImage
    chmod +x "$APPIMAGETOOL"
fi

# ------------------------------------------------
# Create Desktop Entry
# ------------------------------------------------

cat > "$DESKTOP_FILE" <<EOF
[Desktop Entry]
Type=Application
Name=${APP}
Exec=${APP}
Icon=${APP}
Terminal=false
Categories=Utility;
EOF

# ------------------------------------------------
# Generate Icon
# ------------------------------------------------

if command -v magick >/dev/null 2>&1; then
    magick -size 256x256 xc:"#4e5a65" \
        -gravity Center \
        -weight Bold \
        -pointsize 80 \
        -fill "#f8f8f2" \
        -annotate 0 "${APP:0:1}" \
        "$ICON_FILE"
else
    echo "ImageMagick not found — generating fallback icon."
    base64 -d > "$ICON_FILE" <<'EOF'
iVBORw0KGgoAAAANSUhEUgAAAPAAAADwCAIAAAC4k6aVAAAAA3NCSVQICAjb4U/gAAAGhElEQVR4nO3cMW4bQRSG4Z+GiaQUIAGKItTJrVekQhYkCdSCScRhvDJ2LOApSIVJKLU6r6vZV3i1k6XmwBNEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB4n5/vS9P+cr+FWPz2zV1r+nxal67Tekqr+G6rtVImt2+6jo+j1TFzq7+pQoyh1Qf4y7YeZWBXQhHFuM98WdJ12wGRyA1K7ddQBRJcte5Hp6kVrZBVxWJke7I4DlE539vshTLNa9n7MH8y5PEO8/JkI8TMK8g0AxstnOTEwVbQ1wpUy5K1FPrtMZWhdQ4Iuk7JeLRvS8Iqrf/k6SCkt8K1loi4QtYyqK0Onhz2zw0/JjCDCFMvUGdM1VUIY+q6zmOp4kN6j1Y7yPAQyFtGGXeOQy9jC3pR3YAEOvin5pRe/FKkIeeXSGt4Hh4D4rHOSl1k8QOgu/D4IoSh7OsKzIavlZEl0KvK6U/Z2PVMzUvBdr2aesK9IbVCxRABw3gk1nqSS9Jhf4ZglFegIuG4nA7J4oNJ8AcmEY50yNe1nBqCyEXRpxmEULtStPvw/6BU0J/SnSBv34r7bJ4Beeaj1Goflt5RmKStCMLhiXL6Snsw2LSR5ZXm7UNJ7vB9yxMGeBe6hu+q1+s6Gl/jEb5+J0xhjvpk3USExincJUpE7mG3dyJORlOTEJEH/Eb3lYk/htEZfj1UI4p6elGDwRfnuph8vYW3BGppf2XN1enV+tV/eh5jMGZLloAkP3cC1Asfyg8IVObGJ7ktL0eNvjFvi+r5sZnLgOmJioJyO+HrwibjQPSiGHfD6q44nLUtY3TZDwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAALz3B1pA4+MWyaWAAAAAAElFTkSuQmCC
EOF
fi

# ------------------------------------------------
# Prepare AppDir
# ------------------------------------------------

rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"
cp "$BINARY" "$APPDIR/usr/bin/$APP"
chmod +x "$APPDIR/usr/bin/$APP"

# ------------------------------------------------
# Custom AppRun to force terminal fallback
# ------------------------------------------------

cat > "$APPDIR/AppRun" <<EOF
#!/usr/bin/env bash
HERE="\$(dirname "\$(readlink -f "\${0}")")"
APP="\$HERE/usr/bin/${APP}"
exec "\$APP"
EOF
chmod +x "$APPDIR/AppRun"

# ------------------------------------------------
# Run linuxdeploy
# ------------------------------------------------

echo "Running linuxdeploy..."
"$LINUXDEPLOY" \
    --appdir "$APPDIR" \
    -e "$BINARY" \
    -d "$DESKTOP_FILE" \
    -i "$ICON_FILE"

# ------------------------------------------------
# Build AppImage
# ------------------------------------------------

echo "Building AppImage..."
"$APPIMAGETOOL" \
    "$APPDIR" \
    "$BUILD_DIR/${APP}-${VERSION}-${ARCH}.AppImage"

# ------------------------------------------------
# Create launcher script with fallback
# ------------------------------------------------

cat > "$LAUNCHER_SCRIPT" <<EOF
#!/usr/bin/env bash
DIR="\$(dirname "\$0")"
"\$DIR/${APP}-${VERSION}-${ARCH}.AppImage" --appimage-extract-and-run "\$@"
EOF

chmod +x "$LAUNCHER_SCRIPT"

# ------------------------------------------------
# Move exec to publish dir
# ------------------------------------------------
APPIMAGE_SRC="$BUILD_DIR/${APP}-${VERSION}-${ARCH}.AppImage"

if [ -f "$APPIMAGE_SRC" ] && [ -f "$LAUNCHER_SCRIPT" ]; then
    echo "Moving AppImage and launcher to: $PUBLISH_DIR"
    mv -f "$APPIMAGE_SRC" "$LAUNCHER_SCRIPT" "$PUBLISH_DIR" || {
        echo "ERROR: Failed to move AppImage or launcher."
        exit 1
    }
else
    echo "ERROR: AppImage or launcher script not found."
    exit 1
fi

# ------------------------------------------------
# Cleanup
# ------------------------------------------------
if [ -d $BUILD_DIR ]; then
    echo "Cleaning up build directory..."
    find "$BUILD_DIR" -mindepth 1 \
        ! -name "linuxdeploy" \
        ! -name "appimagetool" \
        -exec rm -rf {} +
    echo "Cleanup complete. Tools preserved."
fi

# ------------------------------------------------
# Success message
# ------------------------------------------------

echo ""
echo "SUCCESS"
echo "AppImage created."
echo "Launcher script created."
echo "Published to: "$PUBLISH_DIR""
ls "$PUBLISH_DIR"
echo ""
echo "You can now:"
echo "  Double click launch-DirtyDiana.sh"
echo "  OR run:"
echo "  ./DirtyDiana-${VERSION}-${ARCH}.AppImage"
