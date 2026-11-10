#!/bin/zsh
# 트랙 B-02 S3c 빌드(복제 공간 전용). 사용: build_trackB_S3c.sh <target> <tag>
set -e
T=$1; TAG=$2
B=/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks
P=$B/.physics-work/project-H7B
E=$B/PhysicsValidation/Evidence/M5/trackB
U=/Applications/Unity/Hub/Editor/6000.3.15f1/Unity.app/Contents/MacOS/Unity
ID=M5-B-S3c-$TAG
OUT=$B/.physics-work/builds/$ID
[ -e "$OUT" ] && { echo "exists $OUT"; exit 1; }
cp $B/.physics-work/H7B-AntDemoScene.unity.orig $P/Assets/Scenes/AntDemoScene.unity
$U -batchmode -quit -nographics -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchVariantCurbExt.ApplyAndSave -benchVariantRecord $E/variant-$ID.json -logFile $E/variant-$ID.log
shasum -a 256 $P/Assets/Scenes/AntDemoScene.unity > $E/variant-$ID.scene.sha256
$U -batchmode -quit -projectPath $P -executeMethod PhysicsBench.EditorTools.PhysicsBenchBuild.Build -benchTarget $T -benchBuildOut $OUT -benchBuildId $ID -benchVersion H7 -benchCasesSrc $B/PhysicsValidation/Harness/Cases/trackB/trackB-curb-S3.json -benchProfileSrc $B/PhysicsValidation/Harness/Cases/profile-H7.json -benchRootSha b8efce3526d3ef85eb0c85a580884369f3f5f48a -benchAssetSha a160f46591e2af970f47d05603d64ddad4781e59 -logFile $E/build-$ID.log
cp $OUT/build-result.json $E/$ID.build-result.json
cp $B/.physics-work/H7B-AntDemoScene.unity.orig $P/Assets/Scenes/AntDemoScene.unity
echo "built $ID"
