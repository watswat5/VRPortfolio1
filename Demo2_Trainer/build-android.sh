#!/usr/bin/env bash
set -Eeuo pipefail

PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/home/chris/Unity/Hub/Editor/6000.3.22f1/Editor/Unity}"
DEFAULT_APK="$PROJECT_DIR/Builds/Demo2_Trainer-$(date +%Y%m%d-%H%M%S).apk"
APK_PATH="${1:-$DEFAULT_APK}"

if [[ "$APK_PATH" != /* ]]; then
    APK_PATH="$PROJECT_DIR/$APK_PATH"
fi

if [[ ! -x "$UNITY_EDITOR" ]]; then
    echo "Unity editor not found or not executable: $UNITY_EDITOR" >&2
    echo "Set UNITY_EDITOR to the Unity executable path and try again." >&2
    exit 1
fi

mkdir -p -- "$(dirname -- "$APK_PATH")"

echo "Building Android APK without the Unity GUI..."
echo "Project: $PROJECT_DIR"
echo "Output:  $APK_PATH"

"$UNITY_EDITOR" \
    -batchmode \
    -nographics \
    -quit \
    -projectPath "$PROJECT_DIR" \
    -buildTarget Android \
    -executeMethod CommandLineAndroidBuild.BuildApk \
    -buildPath "$APK_PATH" \
    -logFile -

echo "Build complete: $APK_PATH"
