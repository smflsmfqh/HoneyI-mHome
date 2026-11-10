#!/bin/zsh
# b·c 단계 빌드(복제 공간 project-H7B 전용). 사용: build_fix1.sh <BASE|CURBFIX|COOK30> <StandaloneWindows64|StandaloneOSX> <tag>
#   BASE   : 원본 씬 그대로(테스트용 통제 없음. NPC 등 끄기는 런타임 profile로만)
#   CURBFIX: 원본 씬 + 연석 Box 밑면 -0.3(PhysicsBenchCurbFix, 통제 없음)
#   COOK30 : 원본 씬 + 05-22 de8bfd48의 79개 프리팹 m_CookingOptions 26→30 복원(빌드 후 원래 파일로 되돌리고 원본 저장소와 대조)
set -e
V=$1; T=$2; TAG=$3
B=/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks
P=$B/.physics-work/project-H7B
E=$B/PhysicsValidation/Evidence/M5/fix
A=Assets/minigame-project-assets-smflsmfqh
ORIG_ASSETS=/Users/ihaneul/unityProject/HoneyI-mHome/$A
U=/Applications/Unity/Hub/Editor/6000.3.15f1/Unity.app/Contents/MacOS/Unity
ID=M5-F-$V-$TAG
OUT=$B/.physics-work/builds/$ID
[ -e "$OUT" ] && { echo "exists $OUT"; exit 1; }
cp $B/.physics-work/H7B-AntDemoScene.unity.orig $P/Assets/Scenes/AntDemoScene.unity
LIST=$E/cook79-prefabs.txt

restore_cook() {
  while read f; do
    [ -f "$B/.physics-work/cook79-backup/$f" ] || continue
    cp "$B/.physics-work/cook79-backup/$f" "$P/$f"
    cmp -s "$P/$f" "/Users/ihaneul/unityProject/HoneyI-mHome/$f" || { echo "RESTORE MISMATCH $f"; exit 2; }
  done < $LIST
  echo "cook79 restored and matches original"
}

case $V in
  BASE) ;;
  CURBFIX)
    $U -batchmode -quit -nographics -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchCurbFix.ApplyAndSave -benchVariantRecord $E/variant-$ID.json -logFile $E/variant-$ID.log ;;
  COOK30)
    [ -e $B/.physics-work/cook79-backup ] && { echo "backup exists; previous COOK30 build not cleaned"; exit 1; }
    while read f; do
      mkdir -p "$(dirname "$B/.physics-work/cook79-backup/$f")"
      cp "$P/$f" "$B/.physics-work/cook79-backup/$f"
      n=$(grep -c '^  m_CookingOptions: 26$' "$P/$f" || true)
      [ "$n" = "1" ] || { echo "unexpected 26 count $n in $f"; restore_cook; exit 1; }
      sed -i '' 's/^  m_CookingOptions: 26$/  m_CookingOptions: 30/' "$P/$f"
    done < $LIST
    trap restore_cook EXIT
    $U -batchmode -quit -nographics -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchCookAudit.Run -benchPrefabList $LIST -benchVariantRecord $E/cook-audit-$ID.json -logFile $E/cook-audit-$ID.log ;;
  *) echo "unknown $V"; exit 1 ;;
esac
shasum -a 256 $P/Assets/Scenes/AntDemoScene.unity > $E/variant-$ID.scene.sha256
$U -batchmode -quit -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchBuild.Build -benchTarget $T -benchBuildOut $OUT -benchBuildId $ID -benchVersion H7 -benchCasesSrc $B/PhysicsValidation/Harness/Cases/fix1/fix1-b.json -benchProfileSrc $B/PhysicsValidation/Harness/Cases/profile-H7.json -benchRootSha b8efce3526d3ef85eb0c85a580884369f3f5f48a -benchAssetSha a160f46591e2af970f47d05603d64ddad4781e59 -logFile $E/build-$ID.log
cp $OUT/build-result.json $E/$ID.build-result.json
cp $B/.physics-work/H7B-AntDemoScene.unity.orig $P/Assets/Scenes/AntDemoScene.unity
if [ $V = COOK30 ]; then
  trap - EXIT
  restore_cook
  rm -rf $B/.physics-work/cook79-backup
fi
echo "built $ID"
