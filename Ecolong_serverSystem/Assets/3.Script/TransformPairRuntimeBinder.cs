using UnityEngine;

public class TransformPairRuntimeBinder : MonoBehaviour
{
    [Header("Scene Targets")]
    public Transform targetA;
    public Transform targetB;

    [Header("Save Data")]
    public TransformPairData data;
}