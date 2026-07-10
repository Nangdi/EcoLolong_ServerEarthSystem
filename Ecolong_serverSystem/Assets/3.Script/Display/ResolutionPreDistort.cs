using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 물리 DID 월(예: 2160x1920)에 1개의 HDMI 신호(1920x1080)를 비균등 확대해 뿌릴 때
/// 생기는 세로 늘어남(찌그러짐)을 미리 반대로 압축(pre-distortion)해 보정한다.
///
/// 원리: 렌더 결과를 "디자인 해상도(2160x1920) 기준"으로 구성한 뒤 1920x1080 프레임에
///       비균등으로 눌러 담으면, 월이 화면 전체로 늘릴 때 원래 비율로 복원된다.
///
///   - 카메라 aspect 를 디자인 비율(2160/1920 = 1.125)로 고정
///     → 3D 지오메트리(그래프 메시/스프라이트)가 뷰포트를 채우며 세로로 눌린다.
///
///   - Screen Space-Overlay 캔버스: 자식을 "디자인 크기(2160x1920) 루트"로 감싼 뒤
///     (Screen.w/디자인.x, Screen.h/디자인.y) 비균등 스케일. 오버레이는 aspect 영향을
///     받지 않으므로 가로/세로를 모두 루트가 눌러야 한다.
///
///   - Screen Space-Camera 캔버스: aspect 고정으로 캔버스 자체가 이미 균등(Screen.h/디자인.y)
///     으로 줄고, 카메라가 가로를 뷰포트에 맞춰 늘린다. 따라서 루트는 "균등 스케일"만 주면
///     가로 stretch 와 합쳐져 정확한 pre-distortion 이 된다. (비균등을 주면 이중 압축)
///
/// 재부모화(디자인 루트)는 씬에 영구 반영해도 되고(에디터에서 보임), 없으면 런타임에 자동 생성한다.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(10000)]
public class ResolutionPreDistort : MonoBehaviour
{
    [Header("디자인(물리 화면) 해상도")]
    [Tooltip("작업 기준 물리 화면 해상도. 세로 DID 2대 합산 물리 픽셀 = 2160 x 1920")]
    [SerializeField] private Vector2 _designResolution = new Vector2(2160f, 1920f);

    [Header("대상")]
    [Tooltip("3D 및 Screen Space-Camera 캔버스를 렌더하는 카메라. 비우면 Camera.main 사용")]
    [SerializeField] private Camera _targetCamera;

    [Tooltip("보정할 캔버스들. 비워두면 씬의 모든 Overlay/Camera 루트 캔버스를 자동 탐색")]
    [FormerlySerializedAs("_overlayCanvases")]
    [SerializeField] private Canvas[] _targetCanvases;

    [Header("옵션")]
    [Tooltip("에디터 편집 모드(비플레이)에서도 보정 미리보기를 적용")]
    [SerializeField] private bool _previewInEditMode = true;

    private const string RootName = "__PreDistortRoot";
    private readonly List<Canvas> _targets = new List<Canvas>();

    private void OnEnable()
    {
        ResolveTargets();
        if (Application.isPlaying)
            BuildRoots();
        Apply();
    }

    private void OnDisable()
    {
        Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
        if (cam != null)
            cam.ResetAspect();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying && !_previewInEditMode)
            return;
        Apply();
    }

    private void Apply()
    {
        if (_designResolution.x <= 0f || _designResolution.y <= 0f)
            return;

        // 카메라 aspect 를 디자인 비율로 고정 → 3D 및 Camera 캔버스의 가로 압축 담당
        Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
        if (cam != null)
            cam.aspect = _designResolution.x / _designResolution.y;

        float sw = Mathf.Max(1, Screen.width);
        float sh = Mathf.Max(1, Screen.height);

        // 오버레이: 가로/세로 각각 (비균등). 카메라: 균등(세로 비율) — 가로는 카메라 stretch가 처리
        Vector3 overlayScale = new Vector3(sw / _designResolution.x, sh / _designResolution.y, 1f);
        float uni = sh / _designResolution.y;
        Vector3 cameraScale = new Vector3(uni, uni, 1f);

        for (int i = 0; i < _targets.Count; i++)
        {
            Canvas canvas = _targets[i];
            if (canvas == null)
                continue;
            Transform rootTf = canvas.transform.Find(RootName);
            if (rootTf == null)
                continue;
            rootTf.localScale = canvas.renderMode == RenderMode.ScreenSpaceCamera ? cameraScale : overlayScale;
        }
    }

    /// <summary>대상 캔버스마다 디자인 크기 루트를 만들고 기존 자식을 그 안으로 이동.</summary>
    private void BuildRoots()
    {
        foreach (Canvas canvas in _targets)
        {
            if (canvas == null)
                continue;

            Transform canvasTf = canvas.transform;
            RectTransform root = EnsureRoot(canvasTf);

            // 기존 자식(루트 제외)을 현재 순서대로 루트 안으로 이동해 그리기 순서를 보존
            var toMove = new List<Transform>();
            for (int c = 0; c < canvasTf.childCount; c++)
            {
                Transform child = canvasTf.GetChild(c);
                if (child != root.transform)
                    toMove.Add(child);
            }
            foreach (Transform child in toMove)
                child.SetParent(root, false);
        }
    }

    private RectTransform EnsureRoot(Transform canvasTf)
    {
        Transform existing = canvasTf.Find(RootName);
        RectTransform root = existing as RectTransform;
        if (root == null)
        {
            var go = new GameObject(RootName, typeof(RectTransform));
            root = go.GetComponent<RectTransform>();
            root.SetParent(canvasTf, false);
        }

        // 디자인 크기, 화면 중앙 정렬
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = _designResolution;
        root.anchoredPosition3D = Vector3.zero;
        root.localRotation = Quaternion.identity;
        return root;
    }

    /// <summary>대상 캔버스 목록. 인스펙터 지정이 없으면 씬의 Overlay/Camera 루트 캔버스를 자동 탐색.</summary>
    private void ResolveTargets()
    {
        _targets.Clear();
        if (_targetCanvases != null)
        {
            foreach (Canvas c in _targetCanvases)
                if (c != null)
                    _targets.Add(c);
        }
        if (_targets.Count > 0)
            return;

        Canvas[] all = FindObjectsOfType<Canvas>(true);
        foreach (Canvas c in all)
        {
            if (c == null || !c.isRootCanvas)
                continue;
            if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                _targets.Add(c);
        }
    }
}
