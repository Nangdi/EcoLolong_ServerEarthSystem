using System.Collections;
using TMPro;
using UnityEngine;

public class TcpClientConnectionGuide : MonoBehaviour
{
    [Header("동작 여부")]
    [SerializeField] private bool _enableGuide = true;

    [Header("TCP 연결")]
    [SerializeField] private TcpDataAggregator _aggregator;

    [Header("표시 GameObject")]
    [SerializeField] private GameObject _tcpLogPanel;
    [SerializeField] private GameObject _guidePanel;
    [SerializeField] private TMP_Text _guideText;

    [Header("표시 문구")]
    [SerializeField] private string _waitingFormat = "클라이언트가 접속중입니다 ({0}/{1})";
    [SerializeField] private string _completedMessage = "클라이언트 접속 완료";

    [Header("자동 숨김")]
    [SerializeField] private float _hideDelaySeconds = 3f;

    private Coroutine _hideCoroutine;
    private bool _hasReachedMax;
    private bool _isSubscribed;

    private void Awake()
    {
        if (_aggregator == null)
            _aggregator = TcpDataAggregator.Instance;
    }

    private void Start()
    {
        if (!_enableGuide)
        {
            SetPanelsActive(false);
            return;
        }

        SetPanelsActive(true);
        SubscribeAggregator();
        RefreshGuide();
    }

    private void OnDestroy()
    {
        UnsubscribeAggregator();
    }

    private void SubscribeAggregator()
    {
        if (_isSubscribed)
            return;

        if (_aggregator == null)
            _aggregator = TcpDataAggregator.Instance;

        if (_aggregator == null)
            return;

        _aggregator.DebugStateChanged += OnDebugStateChanged;
        _isSubscribed = true;
    }

    private void UnsubscribeAggregator()
    {
        if (!_isSubscribed || _aggregator == null)
            return;

        _aggregator.DebugStateChanged -= OnDebugStateChanged;
        _isSubscribed = false;
    }

    // 클라이언트 연결/해제 시 호출되어 안내 문구를 갱신합니다.
    private void OnDebugStateChanged()
    {
        if (_hasReachedMax)
            return;

        RefreshGuide();
    }

    // 현재 접속 수에 따라 텍스트를 바꾸고, 최대치 도달 시 자동 숨김 코루틴을 시작합니다.
    private void RefreshGuide()
    {
        if (_aggregator == null)
            return;

        int current = _aggregator.GetConnectedClientCount();
        int max = _aggregator.GetMaxClientCount();

        if (max > 0 && current >= max)
        {
            _hasReachedMax = true;

            if (_guideText != null)
                _guideText.text = _completedMessage;

            if (_hideCoroutine != null)
                StopCoroutine(_hideCoroutine);

            _hideCoroutine = StartCoroutine(HidePanelsAfterDelay());
            return;
        }

        if (_guideText != null)
            _guideText.text = string.Format(_waitingFormat, current, max);
    }

    private IEnumerator HidePanelsAfterDelay()
    {
        yield return new WaitForSeconds(_hideDelaySeconds);
        SetPanelsActive(false);
        UnsubscribeAggregator();
    }

    private void SetPanelsActive(bool isActive)
    {
        if (_tcpLogPanel != null)
            _tcpLogPanel.SetActive(isActive);

        if (_guidePanel != null)
            _guidePanel.SetActive(isActive);
    }
}
