using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TcpDebugStatusBinder : MonoBehaviour
{
    [Header("TCP 연결")]
    [SerializeField] private TcpDataAggregator aggregator;

    [Header("디버그 UI")]
    [SerializeField] private TMP_Text serverStatusText;
    [SerializeField] private TMP_Text clientCountText;
    [SerializeField] private TMP_Text lastReceivedText;
    [SerializeField] private TMP_Text clientDetailsText;
    [SerializeField] private TMP_Text recentMessagesText;
    [SerializeField] private ScrollRect recentMessagesScrollRect;

    private string lastReceivedSummary = "아직 수신 데이터 없음";

    // 씬에 직접 연결하지 않았을 때도 집계기를 자동으로 찾아 연결합니다.
    private void Awake()
    {
        if (aggregator == null)
            aggregator = TcpDataAggregator.Instance;
    }

    // 서버 상태와 수신 이벤트를 구독해서 디버그 UI를 자동으로 갱신합니다.
    private void OnEnable()
    {
        SubscribeAggregator();
        RefreshDebugUI();
    }

    // 오브젝트가 비활성화되거나 제거될 때 이벤트를 정리합니다.
    private void OnDisable()
    {
        UnsubscribeAggregator();
    }

    // 외부에서 집계기를 다시 지정할 수 있도록 공개 메서드를 제공합니다.
    public void SetAggregator(TcpDataAggregator targetAggregator)
    {
        if (aggregator == targetAggregator)
            return;

        UnsubscribeAggregator();
        aggregator = targetAggregator;
        SubscribeAggregator();
        RefreshDebugUI();
    }

    // 집계기의 상태 이벤트와 데이터 수신 이벤트를 구독합니다.
    private void SubscribeAggregator()
    {
        if (aggregator == null)
            aggregator = TcpDataAggregator.Instance;

        if (aggregator == null)
            return;

        aggregator.DebugStateChanged += RefreshDebugUI;
        aggregator.TotalsChanged += OnTotalsChanged;
        aggregator.DataReceived += OnDataReceived;
    }

    // 중복 구독이나 파괴된 참조가 남지 않도록 이벤트를 해제합니다.
    private void UnsubscribeAggregator()
    {
        if (aggregator == null)
            return;

        aggregator.DebugStateChanged -= RefreshDebugUI;
        aggregator.TotalsChanged -= OnTotalsChanged;
        aggregator.DataReceived -= OnDataReceived;
    }

    // 총합이 바뀌었을 때도 디버그 패널 정보가 최신인지 다시 확인합니다.
    private void OnTotalsChanged(EnergyTotals totals)
    {
        RefreshDebugUI();
    }

    // 데이터 한 건을 성공적으로 받았을 때 마지막 수신 항목 표시를 갱신합니다.
    private void OnDataReceived(TcpDataReceivedInfo info)
    {
        string displayName = aggregator != null ? aggregator.GetDisplayName(info.CanonicalName) : info.CanonicalName;
        lastReceivedSummary = $"Client {info.ClientId} / {displayName} +{info.Count}";
        RefreshDebugUI();
    }

    // 현재 서버 상태, 접속 수, 최근 메시지 목록을 TMP_Text에 반영합니다.
    private void RefreshDebugUI()
    {
        if (aggregator == null)
            return;

        if (serverStatusText != null)
            serverStatusText.text = $"서버 상태 : {(aggregator.IsServerRunning() ? "실행 중" : "중지")}\n포트 : {aggregator.GetListenPort()}";

        if (clientCountText != null)
            clientCountText.text = $"접속 클라이언트 : {aggregator.GetConnectedClientCount()}";

        if (lastReceivedText != null)
            lastReceivedText.text = $"마지막 수신 데이터 : {lastReceivedSummary}";

        if (clientDetailsText != null)
            clientDetailsText.text = BuildLinesText("클라이언트 상태", aggregator.GetClientDebugLines());

        if (recentMessagesText != null)
        {
            recentMessagesText.text = BuildLinesText("TCP 로그", aggregator.GetRecentMessagesSnapshot());
            ScrollRecentMessagesToBottom();
        }
    }

    // 갱신 직후 최신 로그가 보이도록 스크롤뷰를 항상 맨 아래로 이동시킵니다.
    private void ScrollRecentMessagesToBottom()
    {
        if (recentMessagesScrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        recentMessagesScrollRect.verticalNormalizedPosition = 0f;
    }

    // 제목과 여러 줄 데이터를 TMP 표시용 문자열로 합칩니다.
    private string BuildLinesText(string title, System.Collections.Generic.List<string> lines)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(title);

        if (lines == null || lines.Count == 0)
        {
            builder.AppendLine("- 없음");
            return builder.ToString();
        }

        for (int i = 0; i < lines.Count; i++)
            builder.AppendLine($"- {lines[i]}");

        return builder.ToString();
    }
}
