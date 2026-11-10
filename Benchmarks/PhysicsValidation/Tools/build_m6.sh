#!/bin/zsh
# M7 정식 빌드(M6-r1 동결 기준, 복제 공간 project-H7B 전용). 사용: build_m6.sh <S0a|S0b|S0p|S1|S2|S3|BASE|CURBFIX>
#   씬은 매번 백업 원본(9d0bb583…)으로 되돌린 뒤 변형을 적용·저장하고, 씬 SHA가 동결 기대값과 다르면 빌드하지 않고 중단한다.
#   S0p·S2는 바닥 메시 Read/Write를 켠 상태로 빌드하고, 빌드 뒤(실패 포함) 끈다.
set -e
V=$1
B=/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks
P=$B/.physics-work/project-H7B
E=$B/PhysicsValidation/Evidence/M6/build
U=/Applications/Unity/Hub/Editor/6000.3.15f1/Unity.app/Contents/MacOS/Unity
FREEZE=$B/PhysicsValidation/Evidence/M6/M6-r1-freeze.json
ID=M6-$V-win-01
OUT=$B/.physics-work/builds/$ID
mkdir -p $E
[ -e "$OUT" ] && { echo "exists $OUT"; exit 1; }
cp $B/.physics-work/H7B-AntDemoScene.unity.orig $P/Assets/Scenes/AntDemoScene.unity

unread() {
  $U -batchmode -quit -nographics -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchVariants.SetReadable -benchReadable false -benchVariantRecord $E/readable-false-$ID.json -logFile $E/readable-false-$ID.log
  cp $B/.physics-work/H7B-AntDemoScene.unity.orig $P/Assets/Scenes/AntDemoScene.unity
}
if [ $V = S0p ] || [ $V = S2 ]; then
  $U -batchmode -quit -nographics -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchVariants.SetReadable -benchReadable true -benchVariantRecord $E/readable-true-$ID.json -logFile $E/readable-true-$ID.log
  trap unread EXIT
fi

case $V in
  S0a|S0b|S0p|S1|S2|S3)
    EXP=$(python3 -I -c "import json,sys;print(json.load(open(sys.argv[1]))['builds_M7']['H7B-floor'][sys.argv[2]]['expectedSceneSha256'])" $FREEZE $V)
    ROOTSHA=b8efce3526d3ef85eb0c85a580884369f3f5f48a
    $U -batchmode -quit -nographics -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchVariants.ApplyAndSave -benchVariant $V -benchVariantRecord $E/variant-$ID.json -logFile $E/variant-$ID.log ;;
  BASE)
    EXP=9d0bb583cc4f4f0e5fe6185012204e9e2d0ac60623f3e4fec660dc390eacfddb
    ROOTSHA=ceb0bd9515f356831108435494fd78790b78d783 ;;
  CURBFIX)
    EXP=12ba8273bf267fd773693cab49d70ae6eaaaa6bfb1d55f0315998dbf0807d043
    ROOTSHA=6c26d2a52f81dece5fb2da7a62d634a4c6e3cc6c
    $U -batchmode -quit -nographics -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchCurbFix.ApplyAndSave -benchVariantRecord $E/variant-$ID.json -logFile $E/variant-$ID.log ;;
  *) echo "unknown $V"; exit 1 ;;
esac

GOT=$(shasum -a 256 $P/Assets/Scenes/AntDemoScene.unity | cut -d' ' -f1)
echo "$GOT  $ID scene (expected $EXP)" > $E/variant-$ID.scene.sha256
case $V in
  S0a|S0b|S0p|S1|S2)
    # addendum-scene: 컴포넌트 추가 변형은 fileID가 무작위 → 변형 기록(appliedUtc 제외)을 M5와 비교
    python3 -I -c "
import json,sys
a=json.load(open(sys.argv[1]));b=json.load(open(sys.argv[2]))
a.pop('appliedUtc',None);b.pop('appliedUtc',None)
sys.exit(0 if a==b else 4)" $B/PhysicsValidation/Evidence/M5/trackB/variant-M5-B-$V-win-01.json $E/variant-$ID.json || { echo "VARIANT RECORD MISMATCH $ID"; cp $B/.physics-work/H7B-AntDemoScene.unity.orig $P/Assets/Scenes/AntDemoScene.unity; exit 4; }
    echo "record matches M5 (scene sha not compared, addendum-scene)" >> $E/variant-$ID.scene.sha256
    EXP=$GOT ;;
esac
if [ "$GOT" != "$EXP" ]; then
  cp $B/.physics-work/H7B-AntDemoScene.unity.orig $P/Assets/Scenes/AntDemoScene.unity
  echo "SCENE SHA MISMATCH $ID got $GOT expected $EXP"; exit 3
fi

CASES=$B/PhysicsValidation/Harness/Cases/M6-r1/m6-curb.json
case $V in S0a|S0b|S0p|S1|S2|S3) CASES=$B/PhysicsValidation/Harness/Cases/M6-r1/m6-floor-$V.json ;; esac
$U -batchmode -quit -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchBuild.Build -benchTarget StandaloneWindows64 -benchBuildOut $OUT -benchBuildId $ID -benchVersion H7 -benchCasesSrc $CASES -benchProfileSrc $B/PhysicsValidation/Harness/Cases/profile-H7.json -benchRootSha $ROOTSHA -benchAssetSha a160f46591e2af970f47d05603d64ddad4781e59 -logFile $E/build-$ID.log
cp $OUT/build-result.json $E/$ID.build-result.json
if [ $V = S0p ] || [ $V = S2 ]; then trap - EXIT; unread; fi
cp $B/.physics-work/H7B-AntDemoScene.unity.orig $P/Assets/Scenes/AntDemoScene.unity
echo "built $ID"
