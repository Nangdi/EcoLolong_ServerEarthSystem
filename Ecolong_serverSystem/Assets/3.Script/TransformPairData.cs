using UnityEngine;

[CreateAssetMenu(fileName = "TransformPairData", menuName = "Tools/Transform Pair Data")]
public class TransformPairData : ScriptableObject
{
    public Vector3 savedPositionA;
    public Vector3 savedPositionB;
}