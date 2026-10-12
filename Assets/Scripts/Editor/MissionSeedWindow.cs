using UnityEditor;
using UnityEngine;

// 미션 배분 재현용. 켜 두면 다음 플레이부터 MissionManager가 지정한 시드로 배분한다.
public class MissionSeedWindow : EditorWindow
{
    [MenuItem("Tools/Mission Seed")]
    private static void Open() => GetWindow<MissionSeedWindow>("Mission Seed");

    private void OnGUI()
    {
        bool enabled = EditorPrefs.GetBool(MissionManager.SEED_OVERRIDE_ENABLED_KEY, false);
        int value = EditorPrefs.GetInt(MissionManager.SEED_OVERRIDE_VALUE_KEY, 0);
        bool hasLast = EditorPrefs.HasKey(MissionManager.LAST_SEED_KEY);
        int last = EditorPrefs.GetInt(MissionManager.LAST_SEED_KEY, 0);

        EditorGUILayout.LabelField("마지막 판 시드", hasLast ? last.ToString() : "-");
        EditorGUILayout.Space();

        bool newEnabled = EditorGUILayout.Toggle("시드 덮어쓰기", enabled);
        int newValue = value;
        using (new EditorGUI.DisabledScope(!newEnabled))
        {
            newValue = EditorGUILayout.IntField("시드", newValue);
            using (new EditorGUI.DisabledScope(!hasLast))
                if (GUILayout.Button("마지막 판 시드 넣기"))
                    newValue = last;
        }

        // OnInspectorUpdate가 초당 약 10번 다시 그리므로 값이 바뀐 경우에만 저장
        if (newEnabled != enabled)
            EditorPrefs.SetBool(MissionManager.SEED_OVERRIDE_ENABLED_KEY, newEnabled);
        if (newValue != value)
            EditorPrefs.SetInt(MissionManager.SEED_OVERRIDE_VALUE_KEY, newValue);
        enabled = newEnabled;

        EditorGUILayout.HelpBox(
            enabled
                ? "다음 플레이부터 이 시드로 미션을 배분합니다. 재현이 끝나면 꺼 주세요."
                : "꺼져 있으면 판마다 새 시드를 씁니다. 빌드에는 영향이 없습니다.",
            enabled ? MessageType.Warning : MessageType.Info
        );
    }

    // 플레이 중 마지막 시드 표시 갱신
    private void OnInspectorUpdate() => Repaint();
}
