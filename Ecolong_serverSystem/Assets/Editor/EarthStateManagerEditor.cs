using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EarthStateManager))]
public class EarthStateManagerEditor : Editor
{
    // 플레이 중 상태가 바뀔 때 인스펙터가 자동으로 다시 그려지도록 합니다.
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EarthStateManager manager = (EarthStateManager)target;
        EarthStateSnapshot snapshot = manager.CurrentState;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("현재 지구 상태", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("상태명", snapshot.StateName);
            EditorGUILayout.IntField("친환경도 단계", snapshot.EcoLevel);
            EditorGUILayout.IntField("발전도 단계", snapshot.DevelopmentLevel);
            EditorGUILayout.IntField("탄소 Count", snapshot.CarbonCount);
            EditorGUILayout.IntField("발전 Count", snapshot.PowerGenerationCount);
            EditorGUILayout.IntField("친환경도 보정", snapshot.EcoLevelOffset);
            EditorGUILayout.FloatField("탄소농도(ppm)", snapshot.CarbonPpm);
            EditorGUILayout.FloatField("탄소농도 변화량(ppm/s)", snapshot.CarbonPpmChangePerSecond);
            EditorGUILayout.FloatField("온도 상승(℃)", snapshot.TemperatureDeltaC);
            EditorGUILayout.FloatField("북극얼음(%)", snapshot.ArcticIcePercent);
            EditorGUILayout.FloatField("해수면 상승(m)", snapshot.SeaLevelRiseMeters);
        }
    }
}
