using UnityEngine;

// 여러 ResourceGraphs가 동일한 y축 스케일(maxValue)을 공유하도록 묶어주는 컴포넌트입니다.
// 같은 좌표 위에 겹쳐서 비교하려는 그래프들(예: 탄소·전기·발전 토큰 누적)의 sharedScale 필드에
// 동일한 GraphScaleSync 인스턴스를 연결하면, 셋 중 가장 큰 값을 기준으로 함께 normalize됩니다.
public class GraphScaleSync : MonoBehaviour
{
    [Tooltip("초기 / 최소 max 값. 아무 데이터도 없을 때 normalize에 쓰입니다.")]
    [SerializeField] private float minMax = 100f;

    public float CurrentMax { get; private set; }

    private void Awake()
    {
        CurrentMax = Mathf.Max(minMax, 0.01f);
    }

    // ResourceGraphs가 그래프 포인트마다 보고합니다. 더 크면 공유 max가 올라갑니다.
    public void Report(float value)
    {
        if (value > CurrentMax) CurrentMax = value;
    }

    // 새 게임 시작 등에서 minMax로 되돌립니다.
    public void ResetMax()
    {
        CurrentMax = Mathf.Max(minMax, 0.01f);
    }
}
