using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ResourceGraphs : MonoBehaviour
{
    [System.Serializable]
    public class GraphPoint
    {
        public float time;
        public float value;

        public GraphPoint(float time, float value)
        {
            this.time = time;
            this.value = value;
        }
    }

    [Header("참조")]
    [SerializeField] private GameTimer timer;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private MeshFilter fillMeshFilter;
    [SerializeField] private MeshRenderer fillMeshRenderer;

    [Header("그래프 크기")]
    [SerializeField] private float graphWidth = 100f;
    [SerializeField] private float graphHeight = 50f;

    [Header("채우기")]
    [SerializeField] private bool useFillArea = true;

    [Header("값 범위")]
    [SerializeField] private float maxValue = 100f;
    [SerializeField] private float initialGraphValue = 0f;
    [Tooltip("여러 그래프가 동일한 y축 스케일을 공유하려면 같은 GraphScaleSync를 셋에 모두 연결합니다.")]
    [SerializeField] private GraphScaleSync sharedScale;

    [SerializeField] private List<GraphPoint> points = new List<GraphPoint>();
    private List<GraphPoint> recordPoints = new List<GraphPoint>();

    [SerializeField] private Transform origin;
    [SerializeField] private Transform floor;
    [SerializeField] private Transform maxPoint;

    [Header("디버그")]
    [SerializeField] private bool isGraphMappingDebug = true;
    [SerializeField] private bool isReplayGraph = false;

    private int tempValue;
    private Coroutine recordCycleCoroutine;
    private Coroutine replayCoroutine;
    private Mesh fillMesh;
    private bool isSubscribedToGameEvents;

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        // View alignment에선 segment마다 카메라를 향하는 별도 billboard가 만들어져
        // 짧은 segment·sharp corner 조합에서 코너마다 갭이 보입니다.
        // TransformZ로 두면 라인이 로컬 XY 평면의 연속 mesh로 그려져 갭이 사라집니다.
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.numCapVertices = 4;

        EnsureFillComponents();
        ApplyFillMaterial();
    }

    private void OnEnable()
    {
        TrySubscribeGameEvents();
    }

    private void OnDisable()
    {
        UnsubscribeGameEvents();
    }

    private void Start()
    {
        EnsureFillComponents();

        ClearFillMesh();
    }

    private void Update()
    {
        TrySubscribeGameEvents();

        if (isGraphMappingDebug)
        {
            UpdateGraphBounds();
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, origin.position);
            lineRenderer.SetPosition(1, maxPoint.position);
        }
    }

    public void AddPoint(float value)
    {
        if (timer == null)
        {
            Debug.LogError("GameTimer reference is missing.");
            return;
        }

        float currentTime = timer.GetCurrentTime();
        points.Add(new GraphPoint(currentTime, value));
    }

    // 외부 스크립트가 게임 시작 기본값을 바꿀 수 있게 합니다.
    public void SetInitialGraphValue(float value)
    {
        initialGraphValue = value;
    }

    private void RedrawGraph(List<GraphPoint> currentPoints)
    {

        if (currentPoints == null || currentPoints.Count == 0)
        {
            lineRenderer.positionCount = 0;
            ClearFillMesh();
            return;
        }

        float currentTime = GameTimer.Instance.CurrentTime;
        Vector3 size = maxPoint.position - origin.position;
        List<Vector3> renderedPositions = new List<Vector3>();

        for (int i = 0; i < currentPoints.Count; i++)
        {
            if (currentPoints[i].time > currentTime)
                break;

            // sharedScale이 있으면 공유 max에 보고, 없으면 로컬 max만 갱신
            if (sharedScale != null)
            {
                sharedScale.Report(currentPoints[i].value);
                if (currentPoints[i].value > maxValue)
                    maxValue = currentPoints[i].value;

            }

            float effectiveMax = sharedScale != null ? sharedScale.CurrentMax : maxValue;

            float normalizedTime = currentPoints[i].time / Mathf.Max(GameTimer.Instance.gameTime, 0.01f);
            float normalizedValue = currentPoints[i].value / Mathf.Max(effectiveMax, 0.01f);
            Vector3 position = origin.position + new Vector3(
                size.x * normalizedTime,
                size.y * normalizedValue,
                0f
            );
            lineRenderer.positionCount = renderedPositions.Count + 1;
            lineRenderer.SetPosition(renderedPositions.Count, position);
            renderedPositions.Add(position);
        }

        UpdateFillMesh(renderedPositions);
    }

    // 그래프를 완전히 초기화합니다.
    public void HardClearGraph()
    {
        points.Clear();
        lineRenderer.positionCount = 0;
        // maxValue = 100f;
        if (sharedScale != null) sharedScale.ResetMax();
        tempValue = 0;
        ClearFillMesh();
    }

    // 기록은 유지한 채 화면에 보이는 그래프만 지웁니다.
    public void SoftClearGraph()
    {
        lineRenderer.positionCount = 0;
        ClearFillMesh();
    }

    public List<GraphPoint> GetPoints()
    {
        return points;
    }

    private IEnumerator recordCycle_co()
    {
        while (!isReplayGraph)
        {
            yield return new WaitForSeconds(1f);
            RedrawGraph(points);
        }
    }

    // GameManager.OnReplay 핸들러에서 호출됩니다.
    // timer.isRePlay/StartTimer/SetTimerSpeed/gameTimeScale은 이미 GameManager가 일괄 처리한 상태입니다.
    private IEnumerator recordCycleX15_co()
    {
        SoftClearGraph();
        while (true)
        {
            yield return new WaitForSeconds(1f / 15f);
            RedrawGraph(recordPoints);
        }
    }

    // OnReplay 신호를 받으면 isReplayGraph가 켜진 그래프만 재생을 시작합니다.
    private void Graph_OnReplay()
    {
        if (!isReplayGraph)
            return;

        if (replayCoroutine != null)
            StopCoroutine(replayCoroutine);

        replayCoroutine = StartCoroutine(recordCycleX15_co());
    }

    private void Graph_OnGameStart()
    {
        isGraphMappingDebug = false;
        HardClearGraph();

        if (recordCycleCoroutine != null)
            StopCoroutine(recordCycleCoroutine);

        if (replayCoroutine != null)
            StopCoroutine(replayCoroutine);

        AddPoint(initialGraphValue);
        recordCycleCoroutine = StartCoroutine(recordCycle_co());
    }

    private void Graph_OnGameEnd()
    {
        if (recordCycleCoroutine != null)
            StopCoroutine(recordCycleCoroutine);

        if (replayCoroutine != null)
            StopCoroutine(replayCoroutine);

        recordPoints = new List<GraphPoint>(points);
    }

    // 색/머티리얼은 같은 GameObject의 GraphMaterialController가 전담합니다.
    // 여기서는 fill 렌더러를 등록하고 sortingOrder, enabled만 처리합니다.
    private void ApplyFillMaterial()
    {
        EnsureFillComponents();

        if (fillMeshRenderer == null || lineRenderer == null)
            return;

        var matCtrl = GetComponent<GraphMaterialController>();
        if (matCtrl != null)
            matCtrl.RegisterFillRenderer(fillMeshRenderer);

        fillMeshRenderer.sortingLayerID = lineRenderer.sortingLayerID;
        fillMeshRenderer.sortingOrder = lineRenderer.sortingOrder - 1;
        fillMeshRenderer.enabled = useFillArea && lineRenderer.positionCount > 1;
    }

    // 현재 보이는 선 아래를 같은 x축 기준으로 반투명하게 채웁니다.
    private void UpdateFillMesh(List<Vector3> renderedPositions)
    {
        EnsureFillComponents();

        if (!useFillArea || fillMesh == null || renderedPositions == null || renderedPositions.Count < 2)
        {
            ClearFillMesh();
            return;
        }

        ApplyFillMaterial();

        int pointCount = renderedPositions.Count;
        Vector3[] vertices = new Vector3[pointCount * 2];
        int[] triangles = new int[(pointCount - 1) * 6];

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 topWorld = renderedPositions[i];
            Vector3 bottomWorld = new Vector3(topWorld.x, floor.position.y, topWorld.z);

            vertices[i * 2] = transform.InverseTransformPoint(topWorld);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(bottomWorld);

            if (i >= pointCount - 1)
                continue;

            int vertexIndex = i * 2;
            int triangleIndex = i * 6;

            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex + 1;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        fillMesh.Clear();
        fillMesh.vertices = vertices;
        fillMesh.triangles = triangles;
        fillMesh.RecalculateBounds();
    }

    // 채우기 메쉬를 비우고 렌더링도 끕니다.
    private void ClearFillMesh()
    {
        EnsureFillComponents();

        if (fillMesh != null)
            fillMesh.Clear();

        if (fillMeshRenderer != null)
            fillMeshRenderer.enabled = false;
    }

    private void UpdateGraphBounds()
    {
        if (origin == null || maxPoint == null)
            return;

        graphWidth = maxPoint.position.x - origin.position.x;
        graphHeight = maxPoint.position.y - origin.position.y;
    }

    // 런타임 중 컴포넌트가 비어 있더라도 다시 만들어서 채우기 기능을 유지합니다.
    private void EnsureFillComponents()
    {
        if (fillMeshFilter == null || fillMeshRenderer == null)
        {
            Transform fillRoot = transform.Find("FillArea");
            if (fillRoot == null)
            {
                GameObject fillAreaObject = new GameObject("FillArea");
                fillAreaObject.transform.SetParent(transform, false);
                fillRoot = fillAreaObject.transform;
            }

            if (fillMeshFilter == null)
            {
                fillMeshFilter = fillRoot.GetComponent<MeshFilter>();
                if (fillMeshFilter == null)
                    fillMeshFilter = fillRoot.gameObject.AddComponent<MeshFilter>();
            }

            if (fillMeshRenderer == null)
            {
                fillMeshRenderer = fillRoot.GetComponent<MeshRenderer>();
                if (fillMeshRenderer == null)
                    fillMeshRenderer = fillRoot.gameObject.AddComponent<MeshRenderer>();
            }
        }

        if (fillMesh == null)
        {
            fillMesh = new Mesh();
            fillMesh.name = $"{name}_FillMesh";
        }

        if (fillMeshFilter != null && fillMeshFilter.sharedMesh != fillMesh)
            fillMeshFilter.sharedMesh = fillMesh;
    }

    // GameManager가 늦게 생성돼도 매 프레임 다시 확인해서 구독합니다.
    private void TrySubscribeGameEvents()
    {
        if (isSubscribedToGameEvents || GameManager.Instance == null)
            return;

        GameManager.Instance.OnGameStart += Graph_OnGameStart;
        GameManager.Instance.OnGameEnd += Graph_OnGameEnd;
        GameManager.Instance.OnReplay += Graph_OnReplay;
        isSubscribedToGameEvents = true;
    }

    // 비활성화될 때 이벤트를 정리해서 중복 구독을 막습니다.
    private void UnsubscribeGameEvents()
    {
        if (!isSubscribedToGameEvents || GameManager.Instance == null)
            return;

        GameManager.Instance.OnGameStart -= Graph_OnGameStart;
        GameManager.Instance.OnGameEnd -= Graph_OnGameEnd;
        GameManager.Instance.OnReplay -= Graph_OnReplay;
        isSubscribedToGameEvents = false;
    }
}
