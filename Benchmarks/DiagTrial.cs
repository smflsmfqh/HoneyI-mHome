using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DiagTrial
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var player = Object.FindFirstObjectByType<PlayerMovement>();
        var pcap = player.GetComponent<CapsuleCollider>();
        Vector3 scale = player.transform.lossyScale;
        sb.AppendLine($"default contact offset {Physics.defaultContactOffset}  player cap contactOffset {pcap.contactOffset}");

        var scene = EditorSceneManager.NewPreviewScene();
        var ps = scene.GetPhysicsScene();
        var wall = new GameObject("w"); SceneManager.MoveGameObjectToScene(wall, scene);
        var bc = wall.AddComponent<BoxCollider>(); bc.size = new Vector3(0.23f, 0.2f, 15f);   // road_009 연석과 같은 크기
        var probe = new GameObject("p"); SceneManager.MoveGameObjectToScene(probe, scene);
        probe.transform.localScale = scale;
        var cap = probe.AddComponent<CapsuleCollider>();
        cap.radius = pcap.radius; cap.height = pcap.height; cap.center = pcap.center; cap.direction = pcap.direction;
        cap.sharedMaterial = new PhysicsMaterial("nf") { dynamicFriction = 0, staticFriction = 0, frictionCombine = PhysicsMaterialCombine.Minimum };
        var rb = probe.AddComponent<Rigidbody>();
        rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        float r = pcap.radius * scale.x;
        float cyOff = pcap.center.y * scale.y;

        foreach (var mode in new[] { CollisionDetectionMode.Discrete, CollisionDetectionMode.ContinuousDynamic })
        foreach (float speed in new[] { 0.8f, 2.0f, 3.5f })
        {
            rb.collisionDetectionMode = mode;
            float face = 0.115f;
            Vector3 start = new Vector3(face + r + 0.07f, -cyOff, 0f);
            rb.position = start; probe.transform.position = start; rb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            float minX = 999f;
            var line = new StringBuilder();
            for (int i = 0; i < 25; i++)
            {
                rb.linearVelocity = Vector3.left * speed;
                ps.Simulate(0.01f);
                float x = rb.position.x;
                minX = Mathf.Min(minX, x);
                if (i < 12) line.Append($"{x - face:0.0000} ");
            }
            sb.AppendLine($"{mode} {speed}m/s  r={r:0.0000}  최소(중심-면) {minX - face:0.0000}  끝 {rb.position.x - face:0.0000}  | {line}");
        }
        EditorSceneManager.ClosePreviewScene(scene);
        return sb.ToString();
    }
}
