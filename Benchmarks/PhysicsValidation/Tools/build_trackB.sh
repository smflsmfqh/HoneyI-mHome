#!/bin/zsh
# 트랙 B 변형 빌드(복제 공간 project-H7B 전용). 사용: build_trackB.sh <variant> <target: StandaloneWindows64|StandaloneOSX> <tag>
# 씬은 매번 백업 원본으로 되돌린 뒤 변형을 적용·저장하고 빌드한다. Read/Write 전환은 별도(SetReadable).
set -e
V=$1; T=$2; TAG=$3
B=/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks
P=$B/.physics-work/project-H7B
E=$B/PhysicsValidation/Evidence/M5/trackB
U=/Applications/Unity/Hub/Editor/6000.3.15f1/Unity.app/Contents/MacOS/Unity
ID=M5-B-$V-$TAG
OUT=$B/.physics-work/builds/$ID
[ -e "$OUT" ] && { echo "exists $OUT"; exit 1; }
cp $B/.physics-work/H7B-AntDemoScene.unity.orig $P/Assets/Scenes/AntDemoScene.unity
$U -batchmode -quit -nographics -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchVariants.ApplyAndSave -benchVariant $V -benchVariantRecord $E/variant-$ID.json -logFile $E/variant-$ID.log
shasum -a 256 $P/Assets/Scenes/AntDemoScene.unity > $E/variant-$ID.scene.sha256
CASES=$B/PhysicsValidation/Harness/Cases/trackB/trackB-floor-$V.json
[ -e $CASES ] || CASES=$B/PhysicsValidation/Harness/Cases/trackB/trackB-floor-S3.json
$U -batchmode -quit -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchBuild.Build -benchTarget $T -benchBuildOut $OUT -benchBuildId $ID -benchVersion H7 -benchCasesSrc $CASES -benchProfileSrc $B/PhysicsValidation/Harness/Cases/profile-H7.json -benchRootSha b8efce3526d3ef85eb0c85a580884369f3f5f48a -benchAssetSha a160f46591e2af970f47d05603d64ddad4781e59 -logFile $E/build-$ID.log
cp $OUT/build-result.json $E/$ID.build-result.json
echo "built $ID"
