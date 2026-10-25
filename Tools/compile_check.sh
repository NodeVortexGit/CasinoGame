#!/bin/bash
# Compile-checks Casino.Core and Assembly-CSharp with the Editor's Roslyn, no Unity launch (~1 min, little RAM).
# Takes the compiler options from the last Unity build's Bee rsp files and the source lists fresh from disk.
# usage: Tools/compile_check.sh [out_dir]   (default /tmp/casino_compile_check)
set -e
ROOT=$(cd "$(dirname "$0")/.." && pwd)
OUT=${1:-/tmp/casino_compile_check}
D=Library/Bee/artifacts/2400b0aP.dag
E=$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Data
cd "$ROOT"
mkdir -p "$OUT"
[ -f $D/Assembly-CSharp.rsp ] || { echo "no $D/Assembly-CSharp.rsp: run one Unity build first"; exit 1; }
csc() { "$E/NetCoreRuntime/dotnet" exec "$E/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig "@$1"; }
strip() { grep -v -e '^-out:' -e '^-refout:' -e '^-pdb' -e '^-generatedfilesout' -e '\.cs"\?$' "$1"; }

strip $D/Casino.Core.rsp > "$OUT/core.rsp"
echo "-out:$OUT/Casino.Core.dll" >> "$OUT/core.rsp"
find Assets/Scripts/Core -name '*.cs' | sed 's/.*/"&"/' >> "$OUT/core.rsp"
core=0; csc "$OUT/core.rsp" > "$OUT/core.log" || core=1
grep -E 'error' "$OUT/core.log" || true
[ $core = 0 ] && echo "core: no errors" || { echo "core: FAILED"; exit 1; }

strip $D/Assembly-CSharp.rsp | sed "s|^-r:\"$D/Casino.Core.ref.dll\"|-r:\"$OUT/Casino.Core.dll\"|" > "$OUT/game.rsp"
echo "-out:$OUT/game.dll" >> "$OUT/game.rsp"
find Assets -name '*.cs' -not -path 'Assets/Scripts/Core/*' -not -path '*/Editor/*' | sed 's/.*/"&"/' >> "$OUT/game.rsp"
game=0; csc "$OUT/game.rsp" > "$OUT/game.log" || game=1
grep -E 'error' "$OUT/game.log" || true
[ $game = 0 ] && echo "game: no errors" || { echo "game: FAILED"; exit 1; }
