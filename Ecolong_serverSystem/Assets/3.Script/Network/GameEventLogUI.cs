using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// TCP로 수신된 데이터(발전/토큰/건물 등)를 사람이 읽기 좋은 한 줄 메시지로 변환해
// ScrollRect 안에 누적 표시합니다. 새 라인은 아래에, 가장 오래된 라인은 위로 밀려 사라집니다.
public class GameEventLogUI : MonoBehaviour
{
    [Header("연결")]
    [FormerlySerializedAs("aggregator")]
    [SerializeField] private TcpDataAggregator _aggregator;
    [Tooltip("새 로그가 추가되면 verticalNormalizedPosition을 0(아래)으로 끌어내릴 ScrollRect")]
    [FormerlySerializedAs("scrollRect")]
    [SerializeField] private ScrollRect _scrollRect;
    [Tooltip("VerticalLayoutGroup + ContentSizeFitter(Vertical=PreferredSize)가 붙은 Content 트랜스폼")]
    [FormerlySerializedAs("content")]
    [SerializeField] private RectTransform _content;
    [Tooltip("한 줄 로그로 복제될 TMP_Text 템플릿. 같은 부모에 둬도 되고, 별도 prefab으로 둬도 됩니다.")]
    [FormerlySerializedAs("lineTemplate")]
    [SerializeField] private TMP_Text _lineTemplate;

    [Header("동작")]
    [Tooltip("최대 보관할 로그 라인 수. 초과 시 가장 오래된(가장 위) 라인이 삭제됩니다.")]
    [Min(1)]
    [FormerlySerializedAs("maxLines")]
    [SerializeField] private int _maxLines = 100;
    [Tooltip("플레이 중(Playing) 상태에서만 로그를 받을지 여부")]
    [FormerlySerializedAs("onlyWhilePlaying")]
    [SerializeField] private bool _onlyWhilePlaying = true;

    [Header("발전 효율 (count × 효율 = TWh)")]
    [Tooltip("TcpDataTextBinder의 동일 효율 값과 맞춰 두면 화면 표기와 로그가 일치합니다.")]
    [Range(0f, 100f)]
    [SerializeField] private float _thermalEfficiency = 40f;
    [Range(0f, 100f)]
    [SerializeField] private float _hydroEfficiency = 85f;
    [Range(0f, 100f)]
    [SerializeField] private float _solarEfficiency = 20f;
    [Range(0f, 100f)]
    [SerializeField] private float _windEfficiency = 35f;
    [Range(0f, 100f)]
    [SerializeField] private float _hydrogenEfficiency = 60f;

    private readonly Queue<TMP_Text> _lines = new Queue<TMP_Text>();
    private bool _isSubscribed;
    // BUILDING / CARBON_CAPTURE는 누적값으로 전송되므로 증분만 로그에 띄우기 위해 직전 값을 캐싱합니다.
    private int _lastBuildingCount;
    private int _lastCaptureCarbon;

    private void Awake()
    {
        // 템플릿은 복제용이므로 본체는 항상 비활성화하여 라인처럼 보이지 않게 합니다.
        if (_lineTemplate != null)
            _lineTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!_isSubscribed)
            TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (_isSubscribed)
            return;

        if (_aggregator == null)
            _aggregator = TcpDataAggregator.Instance;

        if (_aggregator == null)
            return;

        _aggregator.DataReceived += OnDataReceived;
        _aggregator.TotalsChanged += OnTotalsChanged;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _aggregator == null)
            return;

        _aggregator.DataReceived -= OnDataReceived;
        _aggregator.TotalsChanged -= OnTotalsChanged;
        _isSubscribed = false;
    }

    // 게임 리셋 등으로 누적값이 캐시보다 작아지면 캐시도 같이 내려서 다음 증분 로그가 정확하게 잡히게 합니다.
    private void OnTotalsChanged(EnergyTotals totals)
    {
        if (totals == null)
            return;

        if (totals.totalCityBuildingCount < _lastBuildingCount)
            _lastBuildingCount = totals.totalCityBuildingCount;
        if (totals.captureCarbon < _lastCaptureCarbon)
            _lastCaptureCarbon = totals.captureCarbon;
    }

    private void OnDataReceived(TcpDataReceivedInfo info)
    {
        if (_onlyWhilePlaying && GameManager.Instance != null && GameManager.Instance.CurrentGameState != GameState.Playing)
            return;

        string line = FormatLine(info);
        if (string.IsNullOrEmpty(line))
            return;

        AppendLine(line);
    }

    // CanonicalName별로 사용자가 지정한 표기를 만들어 반환합니다. 지원하지 않는 타입은 null.
    private string FormatLine(TcpDataReceivedInfo info)
    {
        int count = info.Count;

        switch (info.CanonicalName)
        {
            case "THERMAL":
                return count > 0 ? $"- 화력발전 + {ToProduction(count, _thermalEfficiency)}TWh" : null;
            case "HYDRO":
                return count > 0 ? $"- 수력발전 + {ToProduction(count, _hydroEfficiency)}TWh" : null;
            case "SOLAR":
                return count > 0 ? $"- 태양광발전 + {ToProduction(count, _solarEfficiency)}TWh" : null;
            case "WIND":
                return count > 0 ? $"- 풍력발전 + {ToProduction(count, _windEfficiency)}TWh" : null;
            case "HYDROGEN":
                return count > 0 ? $"- 수소발전 + {ToProduction(count, _hydrogenEfficiency)}TWh" : null;

            case "ELECTRIC":
                return count > 0 ? $"- 전기 토큰 + {count}개" : null;
            case "CARBON":
                return count > 0 ? $"- 탄소 토큰 + {count}개" : null;
            case "POWER_GENERATION":
                return count > 0 ? $"- 발달 토큰 + {count}개" : null;

            case "BUILDING":
            {
                // BUILDING은 누적값으로 전송되므로 직전 캐시와 비교해 늘어난 만큼만 로그에 띄웁니다.
                int delta = count - _lastBuildingCount;
                _lastBuildingCount = count;
                if (delta <= 0)
                    return null;

                return $"- 도시 건물 +{delta}채 건설";
            }

            case "CARBON_CAPTURE":
            {
                // CARBON_CAPTURE도 누적값으로 전송되므로 증분만 로그에 표시합니다. 부호는 차감을 의미하는 '-'.
                int delta = count - _lastCaptureCarbon;
                _lastCaptureCarbon = count;
                if (delta <= 0)
                    return null;

                return $"- 탄소 포집 + {delta}개";
            }

            default:
                return null;
        }
    }

    private void AppendLine(string text)
    {
        if (_content == null || _lineTemplate == null)
            return;

        TMP_Text line = Instantiate(_lineTemplate, _content);
        line.gameObject.SetActive(true);
        line.text = text;
        line.transform.SetAsLastSibling();
        _lines.Enqueue(line);

        // 최대 보관 개수를 초과하면 가장 위에 있는(가장 오래된) 라인을 제거합니다.
        while (_lines.Count > _maxLines)
        {
            TMP_Text oldest = _lines.Dequeue();
            if (oldest != null)
                Destroy(oldest.gameObject);
        }

        // 레이아웃이 적용된 후에 스크롤을 끌어내려야 정확히 아래에 붙습니다.
        if (_scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // count × efficiency 를 정수 TWh로 환산합니다 (TcpDataTextBinder.UpdateProductionText와 동일 로직).
    private static int ToProduction(int count, float efficiency)
    {
        return Mathf.RoundToInt(count * efficiency);
    }

    // 외부에서 수동으로 로그를 비울 때 사용합니다.
    public void Clear()
    {
        while (_lines.Count > 0)
        {
            TMP_Text line = _lines.Dequeue();
            if (line != null)
                Destroy(line.gameObject);
        }
    }
}
