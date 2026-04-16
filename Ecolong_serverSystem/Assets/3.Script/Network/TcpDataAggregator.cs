using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class TcpDataAggregator : MonoBehaviour
{
    private const int MaxRecentMessages = 10;
    private static readonly EnergyDataDefinition[] SupportedDataDefinitions =
    {
        new EnergyDataDefinition("THERMAL_POWER", "화력", "화력", "thermal", "thermal_power"),
        new EnergyDataDefinition("HYDRO_POWER", "수력", "수력", "hydro", "hydro_power"),
        new EnergyDataDefinition("SOLAR_POWER", "태양광", "태양광", "solar", "solar_power"),
        new EnergyDataDefinition("WIND_POWER", "풍력", "풍력", "wind", "wind_power"),
        new EnergyDataDefinition("HYDROGEN", "수소", "수소", "hydrogen"),
        new EnergyDataDefinition("ELECTRIC_ENERGY", "전기에너지", "전기에너지", "전기애너지", "electric_energy", "electric"),
        new EnergyDataDefinition("CARBON", "탄소", "탄소", "carbon"),
        new EnergyDataDefinition("POWER_GENERATION", "발전", "발전", "generation", "power_generation"),
        new EnergyDataDefinition("CITY_ECO_SCORE", "도시친환경도", "도시친환경도", "city_eco_score", "eco_city"),
        new EnergyDataDefinition("CITY_BUILDING_COUNT", "도시 건물수", "도시건물수", "도시 건물수", "city_building_count", "building_count")
    };

    [Header("TCP Server")]
    [SerializeField] private int listenPort = 5000;
    [SerializeField] private int maxClientCount = 3;
    [SerializeField] private bool autoStart = true;

    [Header("UI")]
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private string emptyMessage = "No data received.";
    [SerializeField] private bool showClientDebugInfo = true;

    [Header("Keyboard Test")]
    [SerializeField] private bool enableKeyboardTest = true;
    [SerializeField] private KeyCode sendATestKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode sendBTestKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode clearTestKey = KeyCode.Alpha0;
    [SerializeField] private int testAddCount = 1;
    [SerializeField] private KeyCode sendMessageTestKey = KeyCode.T;

    [Header("문자열 전송")]
    [SerializeField] private string defaultOutgoingMessage = "PING";
    [SerializeField] private bool appendOutgoingLineEnding = true;
    [SerializeField] private TMP_InputField outgoingMessageInputField;

    private readonly ConcurrentQueue<DataPacket> receivedQueue = new ConcurrentQueue<DataPacket>();
    private readonly Dictionary<string, int> totals = new Dictionary<string, int>();
    private readonly List<TcpClient> connectedClients = new List<TcpClient>();
    private readonly Dictionary<TcpClient, ClientConnectionInfo> clientInfoByClient = new Dictionary<TcpClient, ClientConnectionInfo>();
    private readonly Queue<string> recentMessages = new Queue<string>();
    private readonly object clientLock = new object();

    private TcpListener listener;
    private CancellationTokenSource cancellationTokenSource;
    private bool isServerRunning;
    private bool connectionStatusChanged;
    private int nextClientId = 1;

    private struct DataPacket
    {
        public string Name;
        public int Count;
    }

    private struct ClientConnectionInfo
    {
        public int ClientId;
        public string RemoteEndPoint;
        public string LastReceivedMessage;
    }

    private readonly struct EnergyDataDefinition
    {
        public readonly string CanonicalKey;
        public readonly string DisplayName;
        public readonly string[] Aliases;

        public EnergyDataDefinition(string canonicalKey, string displayName, params string[] aliases)
        {
            CanonicalKey = canonicalKey;
            DisplayName = displayName;
            Aliases = aliases;
        }
    }

    // 부트스트랩이나 다른 씬 스크립트에서 결과 출력용 텍스트를 연결합니다.
    public void SetResultText(TMP_Text text)
    {
        resultText = text;
        RefreshResultText();
    }

    // 씬 시작 시 설정값에 따라 TCP 서버를 자동으로 시작합니다.
    private void Start()
    {
        if (autoStart)
            StartServer();

        RefreshResultText();
    }

    // 백그라운드에서 받은 TCP 데이터를 Unity 메인 스레드에서 합산하고 UI를 갱신합니다.
    private void Update()
    {
        HandleKeyboardTestInput();

        bool changed = false;

        while (receivedQueue.TryDequeue(out DataPacket packet))
        {
            if (!totals.ContainsKey(packet.Name))
                totals.Add(packet.Name, 0);

            totals[packet.Name] += packet.Count;
            changed = true;
        }

        if (connectionStatusChanged)
        {
            connectionStatusChanged = false;
            changed = true;
        }

        if (changed)
            RefreshResultText();
    }

    // TCP 클라이언트 없이도 수신/전송 기능을 테스트할 수 있도록 키보드 입력을 처리합니다.
    private void HandleKeyboardTestInput()
    {
        if (!enableKeyboardTest)
            return;

        if (Input.GetKeyDown(sendATestKey))
            AddData("화력", testAddCount);

        if (Input.GetKeyDown(sendBTestKey))
            AddData("수력", testAddCount);

        if (Input.GetKeyDown(clearTestKey))
            ClearTotals();
        if (Input.GetKeyDown(KeyCode.Alpha3))
            AddData("태양광", testAddCount);
        if (Input.GetKeyDown(sendMessageTestKey))
            SendCurrentMessageToAllClients();
    }

    // 오브젝트가 삭제될 때 TCP 연결을 정리합니다.
    private void OnDestroy()
    {
        StopServer();
    }

    // 앱 종료 시 포트가 열린 채로 남지 않도록 정리합니다.
    private void OnApplicationQuit()
    {
        StopServer();
    }

    // 설정된 포트에서 TCP 클라이언트 접속 대기를 시작합니다.
    public void StartServer()
    {
        if (isServerRunning)
            return;

        try
        {
            cancellationTokenSource = new CancellationTokenSource();
            listener = new TcpListener(IPAddress.Any, listenPort);
            listener.Start();
            isServerRunning = true;

            _ = AcceptClientsAsync(cancellationTokenSource.Token);
            Debug.Log($"TCP data aggregator started. Port: {listenPort}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to start TCP data aggregator. {exception.Message}");
            StopServer();
        }
    }

    // TCP 서버를 중지하고 연결된 모든 클라이언트를 닫습니다.
    public void StopServer()
    {
        if (!isServerRunning && listener == null)
            return;

        isServerRunning = false;

        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }

        if (listener != null)
        {
            listener.Stop();
            listener = null;
        }

        lock (clientLock)
        {
            foreach (TcpClient client in connectedClients)
                client.Close();

            connectedClients.Clear();
            clientInfoByClient.Clear();
        }

        RefreshResultText();
        Debug.Log("TCP data aggregator stopped.");
    }

    // TCP를 거치지 않고 데이터를 직접 추가합니다. 에디터 버튼이나 로컬 테스트에 사용합니다.
    public void AddData(string dataName, int count)
    {
        if (string.IsNullOrWhiteSpace(dataName))
            return;

        receivedQueue.Enqueue(new DataPacket
        {
            Name = NormalizeDataName(dataName),
            Count = count
        });
    }

    // 인스펙터에 입력한 기본 문자열을 현재 연결된 모든 클라이언트에게 전송합니다.
    public void SendDefaultMessageToAllClients()
    {
        SendStringToAllClients(defaultOutgoingMessage);
    }

    // 입력 필드가 연결되어 있으면 그 값을, 없으면 기본 문자열을 모든 클라이언트에게 전송합니다.
    public void SendCurrentMessageToAllClients()
    {
        string message = outgoingMessageInputField != null
            ? outgoingMessageInputField.text
            : defaultOutgoingMessage;

        SendStringToAllClients(message);
    }

    // 원하는 문자열을 현재 연결된 모든 TCP 클라이언트에게 브로드캐스트합니다.
    public void SendStringToAllClients(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Debug.LogWarning("전송할 문자열이 비어 있습니다.");
            return;
        }

        List<TcpClient> clientsSnapshot;
        lock (clientLock)
            clientsSnapshot = new List<TcpClient>(connectedClients);

        if (clientsSnapshot.Count == 0)
        {
            Debug.LogWarning("현재 연결된 클라이언트가 없습니다.");
            return;
        }

        string finalMessage = appendOutgoingLineEnding
            ? message + Environment.NewLine
            : message;

        byte[] data = Encoding.UTF8.GetBytes(finalMessage);
        List<TcpClient> disconnectedClients = new List<TcpClient>();

        foreach (TcpClient client in clientsSnapshot)
        {
            try
            {
                if (client == null || !client.Connected)
                {
                    disconnectedClients.Add(client);
                    continue;
                }

                NetworkStream stream = client.GetStream();
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch (Exception exception)
            {
                disconnectedClients.Add(client);
                Debug.LogWarning($"클라이언트 문자열 전송 실패: {exception.Message}");
            }
        }

        if (disconnectedClients.Count > 0)
        {
            lock (clientLock)
            {
                foreach (TcpClient disconnectedClient in disconnectedClients)
                    connectedClients.Remove(disconnectedClient);

                connectionStatusChanged = true;
            }
        }

        Debug.Log($"브로드캐스트 문자열 전송 완료: {message}");
    }

    // 누적된 모든 합계를 초기화하고 UI를 갱신합니다.
    public void ClearTotals()
    {
        totals.Clear();

        while (receivedQueue.TryDequeue(out _))
        {
        }

        lock (clientLock)
            recentMessages.Clear();

        RefreshResultText();
    }

    // 외부 PC 접속을 받고 클라이언트마다 처리 작업을 시작합니다.
    private async Task AcceptClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client = null;

            try
            {
                client = await listener.AcceptTcpClientAsync();

                lock (clientLock)
                {
                    if (connectedClients.Count >= maxClientCount)
                    {
                        client.Close();
                        Debug.LogWarning($"TCP client rejected. Max client count is {maxClientCount}.");
                        continue;
                    }

                    connectedClients.Add(client);
                    clientInfoByClient[client] = new ClientConnectionInfo
                    {
                        ClientId = nextClientId++,
                        RemoteEndPoint = client.Client.RemoteEndPoint != null
                            ? client.Client.RemoteEndPoint.ToString()
                            : "Unknown",
                        LastReceivedMessage = "-"
                    };
                    connectionStatusChanged = true;
                }

                _ = HandleClientAsync(client, token);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                    Debug.LogWarning($"TCP accept failed. {exception.Message}");

                client?.Close();
            }
        }
    }

    // 클라이언트 한 대에서 줄 단위 메시지를 읽고 파싱된 패킷을 큐에 넣습니다.
    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        string remoteEndPoint = GetClientRemoteEndPoint(client);
        int clientId = GetClientId(client);

        AddRecentMessage($"[Client {clientId}] connected: {remoteEndPoint}");
        Debug.Log($"TCP client connected: {remoteEndPoint}");

        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (!token.IsCancellationRequested && client.Connected)
                {
                    string line = await ReadLineAsync(reader, token);

                    if (line == null)
                        break;

                    UpdateClientLastMessage(client, line);
                    EnqueueParsedLine(line, remoteEndPoint);
                }
            }
        }
        catch (IOException)
        {
            AddRecentMessage($"[Client {clientId}] disconnected");
            Debug.Log($"TCP client disconnected: {remoteEndPoint}");
        }
        catch (ObjectDisposedException)
        {
            AddRecentMessage($"[Client {clientId}] disposed");
            Debug.Log($"TCP client disposed: {remoteEndPoint}");
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
                Debug.LogWarning($"TCP client read failed. {remoteEndPoint} / {exception.Message}");
        }
        finally
        {
            lock (clientLock)
            {
                connectedClients.Remove(client);
                clientInfoByClient.Remove(client);
                connectionStatusChanged = true;
            }
        }
    }

    // 취소 요청으로 클라이언트 읽기 루프를 멈출 수 있도록 ReadLineAsync를 감쌉니다.
    private async Task<string> ReadLineAsync(StreamReader reader, CancellationToken token)
    {
        Task<string> readTask = reader.ReadLineAsync();
        Task cancelTask = Task.Delay(Timeout.Infinite, token);
        Task completedTask = await Task.WhenAny(readTask, cancelTask);

        if (completedTask == cancelTask)
            return null;

        return await readTask;
    }

    // 한 줄에 여러 항목이 들어올 수 있도록 세미콜론 기준으로 나눠 처리합니다.
    private void EnqueueParsedLine(string line, string remoteEndPoint)
    {
        int clientId = GetClientIdByRemoteEndPoint(remoteEndPoint);
        AddRecentMessage($"[Client {clientId}] {line}");

        string[] entries = line.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string entry in entries)
        {
            if (TryParseEntry(entry, out string name, out int count))
            {
                receivedQueue.Enqueue(new DataPacket
                {
                    Name = NormalizeDataName(name),
                    Count = count
                });
            }
            else
            {
                Debug.LogWarning($"Invalid TCP data ignored. Remote: {remoteEndPoint}, Data: {entry}");
            }
        }
    }

    // A:2, A=2, A,2, A 2, A 형식의 메시지를 데이터 이름과 개수로 변환합니다.
    private bool TryParseEntry(string entry, out string name, out int count)
    {
        name = string.Empty;
        count = 1;

        string trimmed = entry.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        char[] separators = { ':', '=', ',', ' ' };
        string[] parts = trimmed.Split(separators, 2, StringSplitOptions.RemoveEmptyEntries);

        name = parts[0].Trim();
        if (string.IsNullOrEmpty(name))
            return false;

        if (parts.Length == 1)
            return true;

        return int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
    }

    // 현재 누적 결과와 서버 상태를 TMP UI 텍스트에 표시합니다.
    private void RefreshResultText()
    {
        if (resultText == null)
            return;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("TCP/IP Data Aggregate");
        builder.AppendLine($"Server: {(isServerRunning ? "Running" : "Stopped")} / Port: {listenPort}");
        builder.AppendLine($"Clients: {GetConnectedClientCount()} / {maxClientCount}");
        builder.AppendLine();

        builder.Append(BuildSupportedDataText());

        if (showClientDebugInfo)
        {
            builder.AppendLine();
            builder.AppendLine("Connected Clients");

            List<ClientConnectionInfo> clientInfos = GetClientInfosSnapshot();
            if (clientInfos.Count == 0)
            {
                builder.AppendLine("- none");
            }
            else
            {
                foreach (ClientConnectionInfo clientInfo in clientInfos)
                    builder.AppendLine($"- Client {clientInfo.ClientId} / {clientInfo.RemoteEndPoint} / Last: {clientInfo.LastReceivedMessage}");
            }

            builder.AppendLine();
            builder.AppendLine("Recent Messages");

            List<string> messages = GetRecentMessagesSnapshot();
            if (messages.Count == 0)
            {
                builder.AppendLine("- none");
            }
            else
            {
                foreach (string message in messages)
                    builder.AppendLine($"- {message}");
            }
        }

        resultText.text = builder.ToString();
    }

    // 다른 스크립트에서 특정 데이터 이름의 현재 합계를 읽을 때 사용합니다.
    public int GetTotal(string dataName)
    {
        if (string.IsNullOrEmpty(dataName))
            return 0;

        return totals.TryGetValue(dataName, out int total) ? total : 0;
    }

    // UI 표시용으로 현재 연결된 클라이언트 수를 안전하게 가져옵니다.
    private int GetConnectedClientCount()
    {
        lock (clientLock)
            return connectedClients.Count;
    }

    // 지원하는 데이터 키를 한글 이름과 고정 순서로 TMP 텍스트에 표시합니다.
    private string BuildSupportedDataText()
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < SupportedDataDefinitions.Length; i++)
        {
            EnergyDataDefinition definition = SupportedDataDefinitions[i];
            int value = totals.TryGetValue(definition.CanonicalKey, out int total) ? total : 0;
            builder.AppendLine($"{definition.DisplayName} : {value}");
        }

        List<string> unknownKeys = GetUnknownDataKeysSnapshot();
        if (unknownKeys.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("기타 데이터");

            foreach (string unknownKey in unknownKeys)
                builder.AppendLine($"{unknownKey} : {totals[unknownKey]}");
        }

        return builder.ToString();
    }

    // 수신한 데이터 이름을 미리 정한 표준 키로 통일합니다.
    private string NormalizeDataName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string trimmedName = rawName.Trim();

        for (int i = 0; i < SupportedDataDefinitions.Length; i++)
        {
            EnergyDataDefinition definition = SupportedDataDefinitions[i];

            if (string.Equals(trimmedName, definition.CanonicalKey, StringComparison.OrdinalIgnoreCase))
                return definition.CanonicalKey;

            for (int aliasIndex = 0; aliasIndex < definition.Aliases.Length; aliasIndex++)
            {
                if (string.Equals(trimmedName, definition.Aliases[aliasIndex], StringComparison.OrdinalIgnoreCase))
                    return definition.CanonicalKey;
            }
        }

        return trimmedName.ToUpperInvariant();
    }

    // 정의되지 않은 데이터 키를 별도 목록으로 추려서 반환합니다.
    private List<string> GetUnknownDataKeysSnapshot()
    {
        List<string> unknownKeys = new List<string>();

        foreach (string key in totals.Keys)
        {
            bool isSupported = false;

            for (int i = 0; i < SupportedDataDefinitions.Length; i++)
            {
                if (SupportedDataDefinitions[i].CanonicalKey == key)
                {
                    isSupported = true;
                    break;
                }
            }

            if (!isSupported)
                unknownKeys.Add(key);
        }

        unknownKeys.Sort(StringComparer.Ordinal);
        return unknownKeys;
    }

    // 연결된 클라이언트의 디버그 정보를 안전하게 복사해서 반환합니다.
    private List<ClientConnectionInfo> GetClientInfosSnapshot()
    {
        lock (clientLock)
        {
            List<ClientConnectionInfo> snapshot = new List<ClientConnectionInfo>(clientInfoByClient.Values);
            snapshot.Sort((left, right) => left.ClientId.CompareTo(right.ClientId));
            return snapshot;
        }
    }

    // 최근 수신 메시지 로그를 UI 표시용으로 복사해서 반환합니다.
    private List<string> GetRecentMessagesSnapshot()
    {
        lock (clientLock)
            return new List<string>(recentMessages);
    }

    // 클라이언트의 마지막 수신 메시지를 갱신합니다.
    private void UpdateClientLastMessage(TcpClient client, string message)
    {
        lock (clientLock)
        {
            if (!clientInfoByClient.TryGetValue(client, out ClientConnectionInfo clientInfo))
                return;

            clientInfo.LastReceivedMessage = message;
            clientInfoByClient[client] = clientInfo;
            connectionStatusChanged = true;
        }
    }

    // 최근 수신 로그를 최대 개수만 유지하면서 추가합니다.
    private void AddRecentMessage(string message)
    {
        lock (clientLock)
        {
            recentMessages.Enqueue(message);

            while (recentMessages.Count > MaxRecentMessages)
                recentMessages.Dequeue();

            connectionStatusChanged = true;
        }
    }

    // 특정 클라이언트의 ID를 반환합니다.
    private int GetClientId(TcpClient client)
    {
        lock (clientLock)
        {
            if (clientInfoByClient.TryGetValue(client, out ClientConnectionInfo clientInfo))
                return clientInfo.ClientId;
        }

        return -1;
    }

    // 특정 클라이언트의 원격 주소를 반환합니다.
    private string GetClientRemoteEndPoint(TcpClient client)
    {
        lock (clientLock)
        {
            if (clientInfoByClient.TryGetValue(client, out ClientConnectionInfo clientInfo))
                return clientInfo.RemoteEndPoint;
        }

        return client != null && client.Client.RemoteEndPoint != null
            ? client.Client.RemoteEndPoint.ToString()
            : "Unknown";
    }

    // 원격 주소 기준으로 현재 클라이언트 ID를 찾습니다.
    private int GetClientIdByRemoteEndPoint(string remoteEndPoint)
    {
        lock (clientLock)
        {
            foreach (ClientConnectionInfo clientInfo in clientInfoByClient.Values)
            {
                if (clientInfo.RemoteEndPoint == remoteEndPoint)
                    return clientInfo.ClientId;
            }
        }

        return -1;
    }
}
