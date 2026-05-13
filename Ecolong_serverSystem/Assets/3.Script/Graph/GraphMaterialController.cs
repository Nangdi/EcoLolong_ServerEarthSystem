using UnityEngine;
using UnityEngine.Serialization;

// 그래프 한 개의 라인·fill 머티리얼을 한 곳에서 생성·관리합니다.
// baseColor 하나로 라인(alpha=1)과 fill(alpha=fillAlpha) 머티리얼이 자동 생성되어
// 두 렌더러에 동기되어 적용됩니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class GraphMaterialController : MonoBehaviour
{
    [Header("기본 색")]
    [Tooltip("LineRenderer와 Fill이 공유하는 RGB. Fill은 fillAlpha로 투명도가 조정됩니다.")]
    [FormerlySerializedAs("baseColor")]
    [SerializeField] private Color _baseColor = new Color(0.2f, 0.95f, 0.35f, 1f);

    [Range(0f, 1f)]
    [Tooltip("Fill 영역의 alpha. 1이면 라인과 같은 불투명, 0이면 완전 투명.")]
    [FormerlySerializedAs("fillAlpha")]
    [SerializeField] private float _fillAlpha = 0.2f;

    [Header("렌더 순서")]
    [Tooltip("Fill 머티리얼 renderQueue. Line과 같거나 작아야 라인이 위에 그려집니다.")]
    [FormerlySerializedAs("fillRenderQueue")]
    [SerializeField] private int _fillRenderQueue = 3000;

    [Tooltip("Line 머티리얼 renderQueue.")]
    [FormerlySerializedAs("lineRenderQueue")]
    [SerializeField] private int _lineRenderQueue = 3000;

    [FormerlySerializedAs("lineRenderer")]
    [SerializeField] private LineRenderer _lineRenderer;
    [FormerlySerializedAs("fillRenderer")]
    [SerializeField] private MeshRenderer _fillRenderer;

    private Material _lineMat;
    private Material _fillMat;

    public Material LineMaterial => _lineMat;
    public Material FillMaterial => _fillMat;
    private Color BaseColor => _baseColor;
    public float FillAlpha => _fillAlpha;

    private void Awake()
    {
        if (_lineRenderer == null) _lineRenderer = GetComponent<LineRenderer>();
        if (_fillRenderer == null) FindFillRenderer();
        EnsureMaterials();
        ApplyMaterials();
    }

    // 인스펙터에서 색을 바꾸면 에디터 플레이 중에도 즉시 반영됩니다.
    private void OnValidate()
    {
        if (_lineRenderer == null) _lineRenderer = GetComponent<LineRenderer>();
        if (_fillRenderer == null) FindFillRenderer();
        EnsureMaterials();
        ApplyMaterials();
    }

    private void OnDestroy()
    {
        // 런타임 생성 머티리얼은 직접 정리합니다.
        if (_lineMat != null) DestroySafe(_lineMat);
        if (_fillMat != null) DestroySafe(_fillMat);
    }

    // ResourceGraphs가 fillArea 자식을 동적으로 만들 수 있어서 사후에 등록할 수 있게 합니다.
    public void RegisterFillRenderer(MeshRenderer renderer)
    {
        if (_fillRenderer == renderer) return;
        _fillRenderer = renderer;
        if (_fillRenderer != null && _fillMat != null)
            _fillRenderer.sharedMaterial = _fillMat;
    }

    private void FindFillRenderer()
    {
        Transform fillArea = transform.Find("FillArea");
        if (fillArea != null)
            _fillRenderer = fillArea.GetComponent<MeshRenderer>();
    }

    private void EnsureMaterials()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        if (_lineMat == null)
            _lineMat = new Material(shader) { name = $"{name}_LineMaterial" };
        Color lineCol = _baseColor;
        lineCol.a = 1f;
        _lineMat.color = lineCol;
        _lineMat.renderQueue = _lineRenderQueue;

        if (_fillMat == null)
            _fillMat = new Material(shader) { name = $"{name}_FillMaterial" };
        Color fillCol = _baseColor;
        fillCol.a = _fillAlpha;
        _fillMat.color = fillCol;
        _fillMat.renderQueue = _fillRenderQueue;
    }

    private void ApplyMaterials()
    {
        if (_lineRenderer != null && _lineMat != null)
            _lineRenderer.sharedMaterial = _lineMat;
        if (_fillRenderer != null && _fillMat != null)
            _fillRenderer.sharedMaterial = _fillMat;
    }

    private static void DestroySafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
