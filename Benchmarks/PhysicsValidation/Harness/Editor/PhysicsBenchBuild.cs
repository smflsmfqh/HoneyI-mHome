// 에디터 전용 하네스 빌드 도구. 복원 작업 공간의 Assets/PhysicsBench/Editor에만 복사한다.
// 씬·프리팹·PlayerSettings를 저장하거나 바꾸지 않고, PHYSICS_BENCH define을 이 빌드에만 추가한다.
//
// 사용 예 (배치 모드):
//   Unity -batchmode -quit -projectPath <복원본> -executeMethod PhysicsBench.EditorTools.PhysicsBenchBuild.Build
//     -benchTarget StandaloneOSX -benchBuildOut <출력 폴더> -benchBuildId <id> -benchVersion H1
//     -benchCasesSrc <cases.json> -benchProfileSrc <profile.json> -benchRootSha <sha> -benchAssetSha <sha>
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchBuild
    {
        private const string STREAMING_DIR = "Assets/StreamingAssets/PhysicsBench";
        private const string HARNESS_DIR = "Assets/PhysicsBench";
        private const string SCENE = "Assets/Scenes/AntDemoScene.unity";

        public static void Build()
        {
            int code = 1;
            try
            {
                code = BuildInternal();
            }
            catch (Exception e)
            {
                Debug.LogError("[PhysicsBenchBuild] " + e);
            }
            EditorApplication.Exit(code);
        }

        private static int BuildInternal()
        {
            string targetName = Arg("-benchTarget") ?? "StandaloneOSX";
            var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), targetName);
            string outDir = Require("-benchBuildOut");
            string buildId = Require("-benchBuildId");
            string versionId = Require("-benchVersion");
            string casesSrc = Require("-benchCasesSrc");
            string profileSrc = Require("-benchProfileSrc");

            if (Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
                throw new Exception("build output exists, refusing to overwrite: " + outDir);
            Directory.CreateDirectory(outDir);

            Directory.CreateDirectory(STREAMING_DIR);
            File.Copy(casesSrc, Path.Combine(STREAMING_DIR, "cases.json"), true);
            File.Copy(profileSrc, Path.Combine(STREAMING_DIR, "profile.json"), true);

            var named = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            // H0는 공개 API가 없는 초기 프로토타입이라 관측 어댑터를 별도 define으로 고른다.
            string[] defines = versionId == "H0" ? new[] { "PHYSICS_BENCH", "PHYSICS_BENCH_H0" }
                : versionId == "Hm1" ? new[] { "PHYSICS_BENCH", "PHYSICS_BENCH_HM1" }
                : new[] { "PHYSICS_BENCH" };
            var info = new BuildInfo
            {
                buildId = buildId,
                versionId = versionId,
                target = target.ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(named).ToString(),
                apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(named).ToString(),
                managedStripping = PlayerSettings.GetManagedStrippingLevel(named).ToString(),
                projectDefines = PlayerSettings.GetScriptingDefineSymbols(named),
                extraDefines = string.Join(";", defines),
                development = true,
                unityVersion = Application.unityVersion,
                rootSha = Arg("-benchRootSha"),
                assetSha = Arg("-benchAssetSha"),
                scene = SCENE,
                sceneSha256 = Sha(SCENE),
                casesSha256 = Sha(casesSrc),
                profileSha256 = Sha(profileSrc),
                harnessFiles = Directory.GetFiles(HARNESS_DIR, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => p.Replace('\\', '/') + " " + Sha(p)).ToArray(),
                fixedDeltaTimeProject = Time.fixedDeltaTime,
                runInBackgroundProject = PlayerSettings.runInBackground,
                fullScreenModeProject = PlayerSettings.fullScreenMode.ToString(),
                builtUtc = DateTime.UtcNow.ToString("O"),
            };
            File.WriteAllText(Path.Combine(STREAMING_DIR, "build-info.json"), JsonUtility.ToJson(info, true));
            AssetDatabase.Refresh();

            string exeName = target == BuildTarget.StandaloneOSX ? "PhysicsBench.app" : "PhysicsBench.exe";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SCENE },
                target = target,
                locationPathName = Path.Combine(outDir, exeName),
                options = BuildOptions.Development,
                extraScriptingDefines = defines,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            var result = new BuildResultInfo
            {
                buildId = buildId,
                result = summary.result.ToString(),
                totalErrors = summary.totalErrors,
                totalWarnings = summary.totalWarnings,
                totalSizeBytes = (long)summary.totalSize,
                outputPath = summary.outputPath,
                durationSec = (float)summary.totalTime.TotalSeconds,
                buildInfo = JsonUtility.ToJson(info),
            };
            File.WriteAllText(Path.Combine(outDir, "build-result.json"), JsonUtility.ToJson(result, true));
            Debug.Log("[PhysicsBenchBuild] " + summary.result + " errors=" + summary.totalErrors + " out=" + summary.outputPath);
            return summary.result == BuildResult.Succeeded ? 0 : 2;
        }

        private static string Sha(string path)
        {
            using var sha = SHA256.Create();
            using var f = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(f).Select(b => b.ToString("x2")));
        }

        private static string Require(string name) => Arg(name) ?? throw new Exception("missing argument " + name);

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        [Serializable]
        private class BuildInfo
        {
            public string buildId;
            public string versionId;
            public string target;
            public string scriptingBackend;
            public string apiCompatibility;
            public string managedStripping;
            public string projectDefines;
            public string extraDefines;
            public bool development;
            public string unityVersion;
            public string rootSha;
            public string assetSha;
            public string scene;
            public string sceneSha256;
            public string casesSha256;
            public string profileSha256;
            public string[] harnessFiles;
            public float fixedDeltaTimeProject;
            public bool runInBackgroundProject;
            public string fullScreenModeProject;
            public string builtUtc;
        }

        [Serializable]
        private class BuildResultInfo
        {
            public string buildId;
            public string result;
            public int totalErrors;
            public int totalWarnings;
            public long totalSizeBytes;
            public string outputPath;
            public float durationSec;
            public string buildInfo;
        }
    }
}
