using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ResourceGraph : MonoBehaviour
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
    [SerializeField] private float graphWidth = 10f;
    [SerializeField] private float graphHeight = 5f;

    [Header("Time Range")]
    [SerializeField] private float maxDuration = 900f; // 15분 = 900초

    [Header("Value Range")]
    [SerializeField] private float maxValue = 100f;

    [SerializeField]
    private List<GraphPoint> points = new List<GraphPoint>();

    [SerializeField] Transform origin;
    [SerializeField] Transform maxPoint;

    int tempValue = 0;
    Coroutine recordCycleCoroutine;
    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
    }
    private void Start()
    {
       
        recordCycleCoroutine = StartCoroutine(recordCycle_co());
    }
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            AddPoint(tempValue++);
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

        RedrawGraph();
    }

    private void RedrawGraph()
    {
        if (points.Count == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        lineRenderer.positionCount = points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 size = maxPoint.position - origin.position;

            float normalizedTime = points[i].time / maxDuration;
            float normalizedValue = points[i].value / maxValue;

            Vector3 pos = origin.position + new Vector3(
                size.x * normalizedTime,
                size.y * normalizedValue,
                0f
            );
            //float x = (points[i].time / maxDuration) * graphWidth;
            //float y = (points[i].value / maxValue) * graphHeight;

            lineRenderer.SetPosition(i, pos);
        }
    }

    public void ClearGraph()
    {
        points.Clear();
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
            yield return new WaitForSeconds(1f); // 1초마다 기록
            //AddPoint(tempValue);
            RedrawGraph();
        }
        
    }
}