using System.Collections;
using System.Collections.Generic;
using System.Net;
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

    [Header("References")]
    [SerializeField] private GameTimer timer;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Graph Size")]
    [SerializeField] private float graphWidth = 100f;
    [SerializeField] private float graphHeight = 50f;



    [Header("Value Range")]
    [SerializeField] private float maxValue = 0;

    [SerializeField]
    private List<GraphPoint> points = new List<GraphPoint>();
    private List<GraphPoint> recordPoints = new List<GraphPoint>();


    [SerializeField] Transform origin;
    [SerializeField] Transform maxPoint;

    int tempValue = 0;
    Coroutine recordCycleCoroutine;
    Coroutine rePlayCoroutine;

    [Header("�����")]
    public bool isGraphMappingDebug = true;
    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
    }
    private void OnEnable()
    {
        GameManager.Instance.OnGameStart += Graph_OnGameStart;
        GameManager.Instance.OnGameEnd += Graph_OnGameEnd;
    }
    private void OnDisable()
    {
        GameManager.Instance.OnGameStart -= Graph_OnGameStart;
        GameManager.Instance.OnGameEnd -= Graph_OnGameEnd;
    }
    private void Start()
    {
        graphWidth = maxPoint.position.x - origin.position.x;
        graphHeight = maxPoint.position.y - origin.position.y;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, origin.position);
        lineRenderer.SetPosition(1, maxPoint.position);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            AddPoint(tempValue++);
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            rePlayCoroutine = StartCoroutine(recordCycleX15_co());
        }

        if (isGraphMappingDebug)
        {
            graphWidth = maxPoint.position.x - origin.position.x;
            graphHeight = maxPoint.position.y - origin.position.y;
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

        //RedrawGraph();
    }

    private void RedrawGraph(List<GraphPoint> currentPoints, int playBackSpeed = 1)
    {
        if (currentPoints.Count == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        float time = GameTimer.Instance.CurrentTime * playBackSpeed;

        for (int i = 0; i < currentPoints.Count; i++)
        {
            if (currentPoints[i].time > time)
            {
                break;
            }
            if (currentPoints[i].value > maxValue)
            {
                maxValue = currentPoints[i].value;
            }
            Vector3 size = maxPoint.position - origin.position;

            float normalizedTime = currentPoints[i].time / GameTimer.Instance.gameTime;
            float normalizedValue = currentPoints[i].value / maxValue;
            Vector3 pos = origin.position + new Vector3(
                size.x * normalizedTime,
                size.y * normalizedValue,
                0f
            );
            lineRenderer.positionCount = i + 1;
            lineRenderer.SetPosition(i, pos);
        }
    }
    //�׷��� ���� �ʱ�ȭ
    public void HardClearGraph()
    {
        points.Clear();
        lineRenderer.positionCount = 0;
        maxValue = 100f;
        tempValue = 0;
    }
    //�׷��� �׷����������
    public void SoftClearGraph()
    {
        lineRenderer.positionCount = 0;
    }

    public List<GraphPoint> GetPoints()
    {
        return points;
    }
    IEnumerator recordCycle_co()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // 1�ʸ��� ���
            //AddPoint(tempValue);
            RedrawGraph(points);
        }

    }
    IEnumerator recordCycleX15_co()
    {
        SoftClearGraph();
        GameManager.Instance.gameTimeScale = 1f;
        timer.isRePlay = true;
        timer.StartTimer();
        while (true)
        {
            yield return new WaitForSeconds(1 * 0.15f); // 1�ʸ��� ���
            RedrawGraph(recordPoints, 15);
        }
    }
    private void Graph_OnGameStart()
    {
        isGraphMappingDebug = false;
        HardClearGraph();
        if (recordCycleCoroutine != null)
        {
            StopCoroutine(recordCycleCoroutine);
        }
        if (rePlayCoroutine != null)
        {
            StopCoroutine(rePlayCoroutine);
        }
        AddPoint(0);
        recordCycleCoroutine = StartCoroutine(recordCycle_co());
    }
    private void Graph_OnGameEnd()
    {
        if (recordCycleCoroutine != null)
        {
            StopCoroutine(recordCycleCoroutine);
        }
        if (rePlayCoroutine != null)
        {
            StopCoroutine(rePlayCoroutine);
        }
        recordPoints = new List<GraphPoint>(points);
        // HardClearGraph();
        //StartCoroutine(recordCycleX15_co());
    }
}