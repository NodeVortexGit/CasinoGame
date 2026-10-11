#!/usr/bin/env bash
# Android build with the standard tools, from the project Unity exports (Casino/Build/Android Gradle project):
#   Gradle      assembleRelease (APK per CPU type, to sideload) and bundleRelease (app bundle for Google Play)
#   bundletool  validates the bundle, builds the APK set a phone would get and reports download sizes
#   adb         installs on a connected phone (--install) and shows the game's log (--log)
# Usage: Tools/android_build.sh [--export] [--arm64] [--install] [--log]
#   --export  re-export the Unity project first (needs the Unity editor)
#   --arm64   64-bit ARM only (every phone since about 2017): half the native compile, for quick test builds
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
UNITY=${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor}
AP=$UNITY/Data/PlaybackEngines/AndroidPlayer
export JAVA_HOME=$AP/OpenJDK
export ANDROID_HOME=$AP/SDK ANDROID_SDK_ROOT=$AP/SDK
JAVA=$JAVA_HOME/bin/java
GRADLE_JAR=$(ls "$AP"/Tools/gradle/lib/gradle-launcher-*.jar | head -1)
BUNDLETOOL=$(ls "$AP"/Tools/bundletool-all-*.jar | head -1)
ADB=$(command -v adb || echo "$AP/SDK/platform-tools/adb")
PROJECT=$ROOT/Builds/Android/Gradle
OUT=$ROOT/Builds/Android
PACKAGE=com.luckyace.casino

export_project=0 install=0 log=0 unity_args=()
for a in "$@"; do
  case $a in
    --export) export_project=1 ;;
    --arm64) unity_args+=(-arm64) ;;
    --install) install=1 ;;
    --log) log=1 ;;
    *) echo "unknown option $a"; exit 2 ;;
  esac
done

if [ $export_project = 1 ]; then
  echo "== Unity: exporting the Gradle project"
  [ -d "$PROJECT" ] && find "$PROJECT" -delete   # start clean: no leftovers from an older export
  nice -n 10 "$UNITY/Unity" -batchmode -nographics -quit -projectPath "$ROOT" -buildTarget Android \
    -executeMethod Casino.EditorTools.CasinoBuild.AndroidGradleProject "${unity_args[@]}" -logFile "$OUT/unity_export.log"
  grep "Casino build" "$OUT/unity_export.log" || { echo "export failed, see $OUT/unity_export.log"; exit 1; }
fi
[ -d "$PROJECT/launcher" ] || { echo "No exported project in $PROJECT (run with --export)"; exit 1; }

echo "== Gradle: APKs and app bundle"
[ "${SKIP_GRADLE:-0}" = 1 ] || ( cd "$PROJECT/launcher" && nice -n 10 "$JAVA" -classpath "$GRADLE_JAR" org.gradle.launcher.GradleMain \
    "-Dorg.gradle.jvmargs=-Xmx2560m" --no-daemon --max-workers=2 assembleRelease bundleRelease )
APKS_DIR=$PROJECT/launcher/build/outputs/apk/release
AAB=$PROJECT/launcher/build/outputs/bundle/release/launcher-release.aab
for apk in launcher-arm64-v8a-release.apk launcher-release.apk; do
  if [ -f "$APKS_DIR/$apk" ]; then cp "$APKS_DIR/$apk" "$OUT/LuckyAceCasino-arm64.apk"; break; fi
done
if [ -f "$APKS_DIR/launcher-armeabi-v7a-release.apk" ]; then cp "$APKS_DIR/launcher-armeabi-v7a-release.apk" "$OUT/LuckyAceCasino-armv7.apk"; fi
cp "$AAB" "$OUT/LuckyAceCasino.aab"

echo "== bundletool: validate, APK set, sizes"
"$JAVA" -jar "$BUNDLETOOL" validate --bundle="$OUT/LuckyAceCasino.aab" > "$OUT/bundletool_validate.txt"
rm -f "$OUT/LuckyAceCasino.apks"
"$JAVA" -jar "$BUNDLETOOL" build-apks --bundle="$OUT/LuckyAceCasino.aab" --output="$OUT/LuckyAceCasino.apks"
"$JAVA" -jar "$BUNDLETOOL" get-size total --apks="$OUT/LuckyAceCasino.apks" --dimensions=ABI | tee "$OUT/bundletool_sizes.csv"
ls -l "$OUT"/LuckyAceCasino-*.apk "$OUT/LuckyAceCasino.aab"

if [ $install = 1 ]; then
  echo "== adb: install"
  if "$ADB" get-state >/dev/null 2>&1; then
    "$JAVA" -jar "$BUNDLETOOL" install-apks --apks="$OUT/LuckyAceCasino.apks" --adb="$ADB"
    "$ADB" shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >/dev/null
    [ $log = 1 ] && "$ADB" logcat -s Unity:V AndroidRuntime:E
  else
    echo "No phone connected (USB debugging, or: adb pair / adb connect for wireless debugging)."
  fi
fi
