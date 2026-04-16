#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TransformPairRuntimeBinderEditorHook
{
    private static Vector3 cachedPosA;
    private static Vector3 cachedPosB;
    private static string binderObjectName;
    private static bool hasCachedData;

    static TransformPairRuntimeBinderEditorHook()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            TransformPairRuntimeBinder binder = Object.FindObjectOfType<TransformPairRuntimeBinder>();

            if (binder == null)
            {
                Debug.LogWarning("TransformPairRuntimeBinder를 찾지 못했습니다.");
                return;
            }

            if (binder.targetA == null || binder.targetB == null || binder.data == null)
            {
                Debug.LogWarning("targetA / targetB / data 중 비어있는 항목이 있습니다.");
                return;
            }

            cachedPosA = binder.targetA.localPosition;
            cachedPosB = binder.targetB.localPosition;
            binderObjectName = binder.gameObject.name;
            hasCachedData = true;

            binder.data.savedPositionA = cachedPosA;
            binder.data.savedPositionB = cachedPosB;

            EditorUtility.SetDirty(binder.data);
            AssetDatabase.SaveAssets();

            Debug.Log("플레이 종료 직전 위치를 저장했습니다.");
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (!hasCachedData) return;

            TransformPairRuntimeBinder binder = Object.FindObjectOfType<TransformPairRuntimeBinder>();

            if (binder == null)
            {
                Debug.LogWarning("에디터 복귀 후 TransformPairRuntimeBinder를 찾지 못했습니다.");
                hasCachedData = false;
                return;
            }

            if (binder.targetA != null && binder.targetA.localPosition != binder.data.savedPositionA)
            {
                Undo.RecordObject(binder.targetA, "Apply Saved Position A");
                binder.targetA.localPosition = binder.data.savedPositionA;
                EditorUtility.SetDirty(binder.targetA);
                EditorSceneManager.MarkSceneDirty(binder.gameObject.scene);
            }

            if (binder.targetB != null&& binder.targetB.localPosition != binder.data.savedPositionB)
            {
                Undo.RecordObject(binder.targetB, "Apply Saved Position B");
                binder.targetB.localPosition = binder.data.savedPositionB;
                EditorUtility.SetDirty(binder.targetB);
                EditorSceneManager.MarkSceneDirty(binder.gameObject.scene);
            }

           
            hasCachedData = false;

            Debug.Log("플레이 종료 후 저장된 위치를 자동 적용했습니다.");
        }
    }
}
#endif