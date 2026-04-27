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
    [SerializeField] private Material fillMaterial;
    [SerializeField] private Color fillColor = new Color(0.2f, 0.95f, 0.35f, 0.2f);

    [Header("값 범위")]
    [SerializeField] private float maxValue = 100f;
    [SerializeField] private float initialGraphValue = 0f;

    [SerializeField] private List<GraphPoint> points = new List<GraphPoint>();
    private List<GraphPoint> recordPoints = new List<GraphPoint>();

    [SerializeField] private Transform origin;
    [SerializeField] private Transform floor;
    [SerializeField] private Transform maxPoint;

    [Header("디버그")]
    [SerializeField] private bool isGraphMappingDebug = true;

    private int tempValue;
    private Coroutine recordCycleCoroutine;
    private Coroutine replayCoroutine;
    private Mesh fillMesh;
    private Material runtimeFillMaterial;
    private bool isSubscribedToGameEvents;

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

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
        UpdateGraphBounds();
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, origin.position);
        lineRenderer.SetPosition(1, maxPoint.position);
        ClearFillMesh();
    }

    private void Update()
    {
        TrySubscribeGameEvents();

        if (Input.GetKeyDown(KeyCode.Space))
            AddPoint(tempValue--);

        if (Input.GetKeyDown(KeyCode.R))
            replayCoroutine = StartCoroutine(recordCycleX15_co());

        if (isGraphMappingDebug)
        {
            UpdateGraphBounds();
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

    private void RedrawGraph(List<GraphPoint> currentPoints, int playBackSpeed = 1)
    {
        if (currentPoints == null || currentPoints.Count == 0)
        {
            lineRenderer.positionCount = 0;
            ClearFillMesh();
            return;
        }

        float currentTime = GameTimer.Instance.CurrentTime * playBackSpeed;
        Vector3 size = maxPoint.position - origin.position;
        List<Vector3> renderedPositions = new List<Vector3>();

        for (int i = 0; i < currentPoints.Count; i++)
        {
            if (currentPoints[i].time > currentTime)
                break;

            if (currentPoints[i].value > maxValue)
                maxValue = currentPoints[i].value;

            float normalizedTime = currentPoints[i].time / Mathf.Max(GameTimer.Instance.gameTime, 0.01f);
            float normalizedValue = currentPoints[i].value / Mathf.Max(maxValue, 0.01f);
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
        maxValue = 100f;
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
        while (true)
        {
            yield return new WaitForSeconds(1f);
            RedrawGraph(points);
        }
    }

    private IEnumerator recordCycleX15_co()
    {
        SoftClearGraph();
        GameManager.Instance.gameTimeScale = 1f;
        timer.isRePlay = true;
        timer.StartTimer();

        while (true)
        {
            yield return new WaitForSeconds(0.15f);
            RedrawGraph(recordPoints, 15);
        }
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

    // 인스펙터에 지정한 머티리얼과 색으로 채우기 렌더러를 맞춥니다.
    private void ApplyFillMaterial()
    {
        EnsureFillComponents();

        if (fillMeshRenderer == null)
            return;

        Material targetMaterial = fillMaterial;
        if (targetMaterial == null)
        {
            if (runtimeFillMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                runtimeFillMaterial = new Material(shader);
                runtimeFillMaterial.name = $"{name}_RuntimeFillMaterial";
            }

            targetMaterial = runtimeFillMaterial;
        }

        targetMaterial.color = fillColor;
        // Fill은 UI(BG) 위에 그려져야 하므로 Overlay 큐로 올리고, 항상 그려지도록 ZTest를 Always로 둡니다.
        targetMaterial.renderQueue = 4000;
        fillMeshRenderer.sharedMaterial = targetMaterial;
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
        isSubscribedToGameEvents = true;
    }

    // 비활성화될 때 이벤트를 정리해서 중복 구독을 막습니다.
    private void UnsubscribeGameEvents()
    {
        if (!isSubscribedToGameEvents || GameManager.Instance == null)
            return;

        GameManager.Instance.OnGameStart -= Graph_OnGameStart;
        GameManager.Instance.OnGameEnd -= Graph_OnGameEnd;
        isSubscribedToGameEvents = false;
    }
}
