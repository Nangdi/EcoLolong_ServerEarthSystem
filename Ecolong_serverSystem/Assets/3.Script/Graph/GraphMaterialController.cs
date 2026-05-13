using UnityEngine;

// 그래프 한 개의 라인·fill 머티리얼을 한 곳에서 생성·관리합니다.
// baseColor 하나로 라인(alpha=1)과 fill(alpha=fillAlpha) 머티리얼이 자동 생성되어
// 두 렌더러에 동기되어 적용됩니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class GraphMaterialController : MonoBehaviour
{
    [Header("기본 색")]
    [Tooltip("LineRenderer와 Fill이 공유하는 RGB. Fill은 fillAlpha로 투명도가 조정됩니다.")]
    [SerializeField] private Color baseColor = new Color(0.2f, 0.95f, 0.35f, 1f);

    [Range(0f, 1f)]
    [Tooltip("Fill 영역의 alpha. 1이면 라인과 같은 불투명, 0이면 완전 투명.")]
    [SerializeField] private float fillAlpha = 0.2f;

    [Header("렌더 순서")]
    [Tooltip("Fill 머티리얼 renderQueue. Line과 같거나 작아야 라인이 위에 그려집니다.")]
    [SerializeField] private int fillRenderQueue = 3000;

    [Tooltip("Line 머티리얼 renderQueue.")]
    [SerializeField] private int lineRenderQueue = 3000;

    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private MeshRenderer fillRenderer;

    private Material lineMat;
    private Material fillMat;

    public Material LineMaterial => lineMat;
    public Material FillMaterial => fillMat;
    private Color BaseColor => baseColor;
    public float FillAlpha => fillAlpha;

    private void Awake()
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        if (fillRenderer == null) FindFillRenderer();
        EnsureMaterials();
        ApplyMaterials();
    }

    // 인스펙터에서 색을 바꾸면 에디터 플레이 중에도 즉시 반영됩니다.
    private void OnValidate()
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        if (fillRenderer == null) FindFillRenderer();
        EnsureMaterials();
        ApplyMaterials();
    }

    private void OnDestroy()
    {
        // 런타임 생성 머티리얼은 직접 정리합니다.
        if (lineMat != null) DestroySafe(lineMat);
        if (fillMat != null) DestroySafe(fillMat);
    }

    // ResourceGraphs가 fillArea 자식을 동적으로 만들 수 있어서 사후에 등록할 수 있게 합니다.
    public void RegisterFillRenderer(MeshRenderer renderer)
    {
        if (fillRenderer == renderer) return;
        fillRenderer = renderer;
        if (fillRenderer != null && fillMat != null)
            fillRenderer.sharedMaterial = fillMat;
    }

    private void FindFillRenderer()
    {
        Transform fillArea = transform.Find("FillArea");
        if (fillArea != null)
            fillRenderer = fillArea.GetComponent<MeshRenderer>();
    }

    private void EnsureMaterials()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        if (lineMat == null)
            lineMat = new Material(shader) { name = $"{name}_LineMaterial" };
        Color lineCol = baseColor;
        lineCol.a = 1f;
        lineMat.color = lineCol;
        lineMat.renderQueue = lineRenderQueue;

        if (fillMat == null)
            fillMat = new Material(shader) { name = $"{name}_FillMaterial" };
        Color fillCol = baseColor;
        fillCol.a = fillAlpha;
        fillMat.color = fillCol;
        fillMat.renderQueue = fillRenderQueue;
    }

    private void ApplyMaterials()
    {
        if (lineRenderer != null && lineMat != null)
            lineRenderer.sharedMaterial = lineMat;
        if (fillRenderer != null && fillMat != null)
            fillRenderer.sharedMaterial = fillMat;
    }

    private static void DestroySafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
