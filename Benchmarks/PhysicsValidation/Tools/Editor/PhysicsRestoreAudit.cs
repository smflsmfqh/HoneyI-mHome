using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy into the isolated project's Assets/Editor, never the original project.
// Read-only scene/settings inspection: no Play Mode, Awake, SaveScene or SaveAssets.
public static class PhysicsRestoreAudit
{
    [Serializable] public class BodyRecord
    {
        public string path, mode, interpolation, constraints;
        public float mass, linearDamping, angularDamping;
        public bool isKinematic, useGravity;
    }

    [Serializable] public class ColliderRecord
    {
        public string path, type, tag, layer, material, attachedBody;
        public bool enabled, trigger, convex;
        public Vector3 center, size, scale;
        public float radius, height;
        public int direction;
    }

    [Serializable] public class Report
    {
        public int schemaVersion = 1;
        public string observedAtUtc, unity, projectPath, scene, standaloneBackend, activeBuildTarget;
        public string observation = "editor_resolved_before_runtime_Awake";
        public bool playMode, sceneDirty, physicsTestsRun;
        public float fixedDeltaTime, maximumDeltaTime, contactOffset;
        public Vector3 gravity;
        public int solverIterations, solverVelocityIterations;
        public string player, playerScript, playerSerializedFields;
        public string[] collisionLayers, buildScenes;
        public BodyRecord[] playerBodies;
        public ColliderRecord[] playerColliders;
        public int sceneMeshColliders, sceneBoxColliders, sceneCapsuleColliders;
    }

    private static string PathOf(Transform transform)
    {
        return transform.parent == null ? transform.name : PathOf(transform.parent) + "/" + transform.name;
    }

    public static void Run()
    {
        try
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Audit requires Edit Mode");
            string prefix = "--physics-audit-output=";
            string value = Environment.GetCommandLineArgs().FirstOrDefault(x => x.StartsWith(prefix, StringComparison.Ordinal));
            if (value == null) throw new ArgumentException("Missing " + prefix + "<path>");
            string output = Path.GetFullPath(value.Substring(prefix.Length));
            if (File.Exists(output)) throw new IOException("Existing evidence preserved: " + output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/AntDemoScene.unity", OpenSceneMode.Single);
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (player == null) throw new InvalidOperationException("Historical PlayerMovement missing");
            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var report = new Report
            {
                observedAtUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                scene = scene.path, playMode = EditorApplication.isPlaying, sceneDirty = scene.isDirty,
                standaloneBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString(),
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                fixedDeltaTime = Time.fixedDeltaTime, maximumDeltaTime = Time.maximumDeltaTime,
                contactOffset = Physics.defaultContactOffset, gravity = Physics.gravity,
                solverIterations = Physics.defaultSolverIterations,
                solverVelocityIterations = Physics.defaultSolverVelocityIterations,
                player = PathOf(player.transform), playerScript = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(player)),
                playerSerializedFields = EditorJsonUtility.ToJson(player, false),
                collisionLayers = Enumerable.Range(0, 32).Where(i => !Physics.GetIgnoreLayerCollision(player.gameObject.layer, i))
                    .Select(i => i + ":" + LayerMask.LayerToName(i)).ToArray(),
                buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                playerBodies = player.GetComponentsInChildren<Rigidbody>(true).Select(b => new BodyRecord
                {
                    path = PathOf(b.transform), mode = b.collisionDetectionMode.ToString(),
                    interpolation = b.interpolation.ToString(), constraints = b.constraints.ToString(), mass = b.mass,
                    linearDamping = b.linearDamping, angularDamping = b.angularDamping,
                    isKinematic = b.isKinematic, useGravity = b.useGravity
                }).ToArray(),
                playerColliders = player.GetComponentsInChildren<Collider>(true).Select(c => new ColliderRecord
                {
                    path = PathOf(c.transform), type = c.GetType().Name, tag = c.tag,
                    layer = c.gameObject.layer + ":" + LayerMask.LayerToName(c.gameObject.layer),
                    enabled = c.enabled, trigger = c.isTrigger, scale = c.transform.lossyScale,
                    attachedBody = c.attachedRigidbody == null ? "" : PathOf(c.attachedRigidbody.transform),
                    material = c.sharedMaterial == null ? "<default>" : AssetDatabase.GetAssetPath(c.sharedMaterial),
                    center = c is CapsuleCollider cap ? cap.center : c is BoxCollider box ? box.center : Vector3.zero,
                    size = c is BoxCollider boxSize ? boxSize.size : Vector3.zero,
                    radius = c is CapsuleCollider radius ? radius.radius : 0,
                    height = c is CapsuleCollider height ? height.height : 0,
                    direction = c is CapsuleCollider axis ? axis.direction : -1,
                    convex = c is MeshCollider mesh && mesh.convex
                }).ToArray(),
                sceneMeshColliders = colliders.Count(c => c is MeshCollider),
                sceneBoxColliders = colliders.Count(c => c is BoxCollider),
                sceneCapsuleColliders = colliders.Count(c => c is CapsuleCollider)
            };
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("[PhysicsRestoreAudit] " + output);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
