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
using UnityEngine.Serialization;

public class TcpDataAggregator : MonoBehaviour
{
    private static TcpDataAggregator s_instance;

    // 다른 스크립트가 처음 접근하는 시점에 씬에서 한 번 찾아서 보완하는 lazy singleton getter입니다.
    public static TcpDataAggregator Instance
    {
        get
        {
            if (s_instance == null)
                s_instance = FindObjectOfType<TcpDataAggregator>();
            return s_instance;
        }
        private set { s_instance = value; }
    }

    private const int MaxRecentMessages = 200;
    private const string VideoReadyPrefix = "VIDEO_UPLOAD";
    private static readonly EnergyDataDefinition[] SupportedDataDefinitions =
    {
        new EnergyDataDefinition("THERMAL", "화력", "화력", "thermal", "thermal_power"),
        new EnergyDataDefinition("HYDRO", "수력", "수력", "hydro", "hydro_power"),
        new EnergyDataDefinition("SOLAR", "태양광", "태양광", "solar", "solar_power"),
        new EnergyDataDefinition("WIND", "풍력", "풍력", "wind", "wind_power"),
        new EnergyDataDefinition("HYDROGEN", "수소", "수소", "hydrogen"),
        new EnergyDataDefinition("ELECTRIC", "전기", "전기", "전기", "electric"),
        new EnergyDataDefinition("CARBON", "탄소", "탄소", "carbon"),
        new EnergyDataDefinition("POWER_GENERATION", "발전", "발전", "generation", "power_generation"),
        new EnergyDataDefinition("ECO", "도시친환경도", "도시친환경도", "city_eco_score", "eco_city"),
        new EnergyDataDefinition("BUILDING", "건물", "건물", "건물", "도시 건물수", "도시건물수", "city_building_count", "building_count", "BULDING", "building_add"),
        new EnergyDataDefinition("CARBON_CAPTURE", "탄소 포집", "탄소포집", "탄소 포집", "CARBON_CAPTURE", "capture_carbon", "remove_carbon", "carbon_remove")
    };

    [Header("TCP Server")]
    [FormerlySerializedAs("listenPort")]
    [SerializeField] private int _listenPort = 5000;
    [FormerlySerializedAs("maxClientCount")]
    [SerializeField] private int _maxClientCount = 3;
    [FormerlySerializedAs("autoStart")]
    [SerializeField] private bool _autoStart = true;

    [Header("Keyboard Test")]
    [FormerlySerializedAs("enableKeyboardTest")]
    [SerializeField] private bool _enableKeyboardTest = true;
    [FormerlySerializedAs("sendATestKey")]
    [SerializeField] private KeyCode _sendATestKey = KeyCode.Alpha1;
    [FormerlySerializedAs("sendBTestKey")]
    [SerializeField] private KeyCode _sendBTestKey = KeyCode.Alpha2;
    [FormerlySerializedAs("clearTestKey")]
    [SerializeField] private KeyCode _clearTestKey = KeyCode.Alpha0;
    [FormerlySerializedAs("testAddCount")]
    [SerializeField] private int _testAddCount = 1;
    [FormerlySerializedAs("sendMessageTestKey")]
    [SerializeField] private KeyCode _sendMessageTestKey = KeyCode.T;
    [Tooltip("디버그용: 누르면 'VIDEO_UPLOAD|test.mp4' TCP 수신을 시뮬레이션합니다.")]
    [SerializeField] private KeyCode _sendVideoTestKey = KeyCode.V;
    [Tooltip("디버그 비디오 키 입력 시 시뮬레이션할 파일명입니다.")]
    [SerializeField] private string _videoTestFileName = "test.mp4";

    [Header("문자열 전송")]
    [FormerlySerializedAs("defaultOutgoingMessage")]
    [SerializeField] private string _defaultOutgoingMessage = "PING";
    [FormerlySerializedAs("appendOutgoingLineEnding")]
    [SerializeField] private bool _appendOutgoingLineEnding = true;
    [FormerlySerializedAs("outgoingMessageInputField")]
    [SerializeField] private TMP_InputField _outgoingMessageInputField;

    private readonly ConcurrentQueue<TcpDataReceivedInfo> _receivedQueue = new ConcurrentQueue<TcpDataReceivedInfo>();
    private readonly ConcurrentQueue<string> _videoFileNameQueue = new ConcurrentQueue<string>();
    private readonly List<TcpClient> _connectedClients = new List<TcpClient>();
    private readonly Dictionary<TcpClient, ClientConnectionInfo> _clientInfoByClient = new Dictionary<TcpClient, ClientConnectionInfo>();
    private readonly Queue<string> _recentMessages = new Queue<string>();
    private readonly object _clientLock = new object();

    private TcpListener _listener;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isServerRunning;
    private bool _connectionStatusChanged;
    private int _nextClientId = 1;
    private readonly EnergyTotals _energyTotals = new EnergyTotals();

    public event Action<EnergyTotals> TotalsChanged;
    public event Action<TcpDataReceivedInfo> DataReceived;
    public event Action DebugStateChanged;
    public event Action<string> VideoReadyReceived;

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // 씬 시작 시 설정값에 따라 TCP 서버를 자동으로 시작합니다.
    private void Start()
    {
        // port.json에 저장된 TCP 설정값을 우선 적용합니다. JsonManager가 없으면 인스펙터 값을 그대로 사용합니다.
        ApplyTcpSettingsFromJson();

        if (_autoStart)
            StartServer();

        NotifyTotalsChanged();
        NotifyDebugStateChanged();
    }

    // 백그라운드에서 받은 TCP 데이터를 Unity 메인 스레드에서 합산하고 이벤트를 발생시킵니다.
    private void Update()
    {
        HandleKeyboardTestInput();

        ProcessVideoQueue();

        bool totalsChanged = ProcessReceivedQueue();

        if (_connectionStatusChanged)
        {
            _connectionStatusChanged = false;
            NotifyDebugStateChanged();
        }

        if (totalsChanged)
            NotifyTotalsChanged();
    }

    // 비디오 큐를 비우면서 메인 스레드에서 VideoReadyReceived 이벤트를 발생시킵니다.
    private void ProcessVideoQueue()
    {
        while (_videoFileNameQueue.TryDequeue(out string fileName))
            VideoReadyReceived?.Invoke(fileName);
    }

    // 수신 큐에 쌓인 패킷을 모두 꺼내서 누적값에 반영하고 변경 여부를 반환합니다.
    private bool ProcessReceivedQueue()
    {
        bool totalsChanged = false;

        while (_receivedQueue.TryDequeue(out TcpDataReceivedInfo packet))
        {
            if (!_energyTotals.AddValue(packet.CanonicalName, packet.Count))
            {
                AddRecentMessage($"[경고] 지원하지 않는 키 / Raw: {packet.RawName} / Canonical: {packet.CanonicalName} / Remote: {packet.RemoteEndPoint}");
                continue;
            }

            totalsChanged = true;
            DataReceived?.Invoke(packet);
        }

        return totalsChanged;
    }

    // TCP 클라이언트 없이도 수신/전송 기능을 테스트할 수 있도록 키보드 입력을 처리합니다.
    private void HandleKeyboardTestInput()
    {
        if (!_enableKeyboardTest)
            return;

        if (Input.GetKeyDown(_sendATestKey))
            AddData("화력", _testAddCount);

        if (Input.GetKeyDown(_sendBTestKey))
            AddData("수력", _testAddCount);

        if (Input.GetKeyDown(_clearTestKey))
            ClearTotals();
        if (Input.GetKeyDown(KeyCode.Alpha3))
            AddData("태양광", _testAddCount);
        if (Input.GetKeyDown(_sendMessageTestKey))
            SendCurrentMessageToAllClients();

        // 디버그용: VIDEO_UPLOAD|파일명 TCP 수신을 실제 파싱 경로 그대로 시뮬레이션합니다.
        if (Input.GetKeyDown(_sendVideoTestKey))
        {
            string fileName = string.IsNullOrWhiteSpace(_videoTestFileName) ? "test.mp4" : _videoTestFileName.Trim();
            EnqueueParsedLine($"{VideoReadyPrefix}|{fileName}", "LOCAL_TEST");
        }
    }

    private void OnDestroy()
    {
        StopServer();

        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }

    public void StartServer()
    {
        if (_isServerRunning)
            return;

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _listenPort);
            _listener.Start();
            _isServerRunning = true;

            _ = AcceptClientsAsync(_cancellationTokenSource.Token);
            AddRecentMessage($"[서버] 시작 / Port: {_listenPort}");
            NotifyDebugStateChanged();
        }
        catch (Exception exception)
        {
            AddRecentMessage($"[오류] 서버 시작 실패 / {exception.Message}");
            StopServer();
        }
    }

    public void StopServer()
    {
        if (!_isServerRunning && _listener == null)
            return;

        _isServerRunning = false;

        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        if (_listener != null)
        {
            _listener.Stop();
            _listener = null;
        }

        lock (_clientLock)
        {
            foreach (TcpClient client in _connectedClients)
                client.Close();

            _connectedClients.Clear();
            _clientInfoByClient.Clear();
        }

        NotifyTotalsChanged();
        NotifyDebugStateChanged();
        AddRecentMessage("[서버] 중지");
    }

    public void AddData(string dataName, int count)
    {
        if (string.IsNullOrWhiteSpace(dataName))
            return;

        _receivedQueue.Enqueue(new TcpDataReceivedInfo
        {
            RawName = dataName,
            CanonicalName = NormalizeDataName(dataName),
            Count = count,
            ClientId = -1,
            RemoteEndPoint = "LOCAL_TEST",
            RawLine = dataName
        });
    }

    public void SendDefaultMessageToAllClients()
    {
        SendStringToAllClients(_defaultOutgoingMessage);
    }

    public void SendCurrentMessageToAllClients()
    {
        string message = _outgoingMessageInputField != null
            ? _outgoingMessageInputField.text
            : _defaultOutgoingMessage;

        SendStringToAllClients(message);
    }

    public void SendStringToAllClients(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            AddRecentMessage("[경고] 전송할 문자열이 비어 있습니다.");
            return;
        }

        List<TcpClient> clientsSnapshot;
        lock (_clientLock)
            clientsSnapshot = new List<TcpClient>(_connectedClients);

        if (clientsSnapshot.Count == 0)
        {
            AddRecentMessage("[경고] 현재 연결된 클라이언트가 없습니다.");
            return;
        }

        string finalMessage = _appendOutgoingLineEnding
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
                AddRecentMessage($"[오류] 문자열 전송 실패 / {exception.Message}");
            }
        }

        if (disconnectedClients.Count > 0)
        {
            lock (_clientLock)
            {
                foreach (TcpClient disconnectedClient in disconnectedClients)
                    _connectedClients.Remove(disconnectedClient);

                _connectionStatusChanged = true;
            }
        }

        AddRecentMessage($"[송신] {message}");
    }

    public void ClearTotals()
    {
        _energyTotals.Clear();

        while (_receivedQueue.TryDequeue(out _))
        {
        }

        lock (_clientLock)
            _recentMessages.Clear();

        NotifyTotalsChanged();
        NotifyDebugStateChanged();
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client = null;

            try
            {
                client = await _listener.AcceptTcpClientAsync();

                lock (_clientLock)
                {
                    if (_connectedClients.Count >= _maxClientCount)
                    {
                        client.Close();
                        AddRecentMessage($"[경고] 클라이언트 거부 / 최대 접속 수 {_maxClientCount}");
                        continue;
                    }

                    _connectedClients.Add(client);
                    _clientInfoByClient[client] = new ClientConnectionInfo
                    {
                        ClientId = _nextClientId++,
                        RemoteEndPoint = client.Client.RemoteEndPoint != null
                            ? client.Client.RemoteEndPoint.ToString()
                            : "Unknown",
                        LastReceivedMessage = "-"
                    };
                    _connectionStatusChanged = true;
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
                    AddRecentMessage($"[오류] 클라이언트 접속 처리 실패 / {exception.Message}");

                client?.Close();
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        string remoteEndPoint = GetClientRemoteEndPoint(client);
        int clientId = GetClientId(client);

        AddRecentMessage($"[Client {clientId}] connected: {remoteEndPoint}");

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
        }
        catch (ObjectDisposedException)
        {
            AddRecentMessage($"[Client {clientId}] disposed");
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
                AddRecentMessage($"[오류] 클라이언트 읽기 실패 / {remoteEndPoint} / {exception.Message}");
        }
        finally
        {
            lock (_clientLock)
            {
                _connectedClients.Remove(client);
                _clientInfoByClient.Remove(client);
                _connectionStatusChanged = true;
            }
        }
    }

    private async Task<string> ReadLineAsync(StreamReader reader, CancellationToken token)
    {
        Task<string> readTask = reader.ReadLineAsync();
        Task cancelTask = Task.Delay(Timeout.Infinite, token);
        Task completedTask = await Task.WhenAny(readTask, cancelTask);

        if (completedTask == cancelTask)
            return null;

        return await readTask;
    }

    private void EnqueueParsedLine(string line, string remoteEndPoint)
    {
        if (TryEnqueueVideoReady(line, remoteEndPoint))
            return;

        if (GameManager.Instance.CurrentGameState != GameState.Playing)
        {
            AddRecentMessage($"[경고] 게임이 진행 중이 아닐 때 수신된 데이터 무시 / Remote: {remoteEndPoint} / Data: {line}");
            return;
        }
        int clientId = GetClientIdByRemoteEndPoint(remoteEndPoint);
        AddRecentMessage($"[Client {clientId}] {line}");

        string[] entries = line.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string entry in entries)
        {
            if (TryParseEntry(entry, out string name, out int count))
            {
                string canonicalName = NormalizeDataName(name);
                _receivedQueue.Enqueue(new TcpDataReceivedInfo
                {
                    RawName = name,
                    CanonicalName = canonicalName,
                    Count = count,
                    ClientId = clientId,
                    RemoteEndPoint = remoteEndPoint,
                    RawLine = line
                });
            }
            else
            {
                AddRecentMessage($"[경고] 잘못된 TCP 데이터 무시 / Remote: {remoteEndPoint} / Data: {entry}");
            }
        }
    }

    // VIDEO_UPLOAD|파일명 형식의 라인을 감지하면 비디오 큐로 enqueue하고 true를 반환합니다.
    // 게임 상태가 Ended일 때만 수신을 허용하며, 그 외 상태에서는 무시되 따라 일반 데이터 파서로 넘어가지 않습니다.
    private bool TryEnqueueVideoReady(string line, string remoteEndPoint)
    {
        if (string.IsNullOrEmpty(line))
            return false;

        string[] parts = line.Split(new[] { '|' }, 2);
        if (!string.Equals(parts[0].Trim(), VideoReadyPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        GameManager gameManager = GameManager.Instance;
        GameState currentState = gameManager != null ? gameManager.CurrentGameState : GameState.Ready;
        if (currentState != GameState.TimeOut)
        {
            AddRecentMessage($"[경고] VIDEO_UPLOAD는 TimeOut 상태에서만 수신합니다. 현재 상태: {currentState} / Remote: {remoteEndPoint}");
            return true;
        }

        string fileName = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        int clientId = GetClientIdByRemoteEndPoint(remoteEndPoint);
        AddRecentMessage($"[Client {clientId}] {VideoReadyPrefix}|{fileName}");
        _videoFileNameQueue.Enqueue(fileName);
        return true;
    }

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

    private void NotifyTotalsChanged()
    {
        TotalsChanged?.Invoke(_energyTotals);
    }

    private void NotifyDebugStateChanged()
    {
        DebugStateChanged?.Invoke();
    }

    public EnergyTotals GetEnergyTotals()
    {
        return _energyTotals;
    }

    public bool IsServerRunning()
    {
        return _isServerRunning;
    }

    public int GetListenPort()
    {
        return _listenPort;
    }

    // port.json의 TCP 설정값(tcpPort, maxClientCount, autoStart)을 검증한 뒤 유효하면 인스펙터 값을 덮어씁니다.
    private void ApplyTcpSettingsFromJson()
    {
        JsonManager jsonManager = JsonManager.instance;
        if (jsonManager == null || jsonManager.portJson == null)
            return;

        PortJson portJson = jsonManager.portJson;

        // tcpPort: 1~65535 범위의 유효한 값일 때만 적용합니다.
        int jsonPort = portJson.tcpPort;
        if (jsonPort <= 0 || jsonPort > 65535)
        {
            Debug.LogWarning($"port.json tcpPort 값이 유효 범위(1~65535)를 벗어났습니다: {jsonPort}. 인스펙터 값 {_listenPort}을 그대로 사용합니다.");
        }
        else if (_listenPort != jsonPort)
        {
            Debug.Log($"port.json tcpPort 적용: {_listenPort} -> {jsonPort}");
            _listenPort = jsonPort;
        }

        // maxClientCount: 1 이상일 때만 적용합니다.
        int jsonMaxClient = portJson.maxClientCount;
        if (jsonMaxClient < 1)
        {
            Debug.LogWarning($"port.json maxClientCount 값이 1 미만입니다: {jsonMaxClient}. 인스펙터 값 {_maxClientCount}을 그대로 사용합니다.");
        }
        else if (_maxClientCount != jsonMaxClient)
        {
            Debug.Log($"port.json maxClientCount 적용: {_maxClientCount} -> {jsonMaxClient}");
            _maxClientCount = jsonMaxClient;
        }

        // autoStart: 별도 범위 검증 없이 그대로 적용합니다.
        if (_autoStart != portJson.autoStart)
        {
            Debug.Log($"port.json autoStart 적용: {_autoStart} -> {portJson.autoStart}");
            _autoStart = portJson.autoStart;
        }
    }

    public int GetConnectedClientCount()
    {
        lock (_clientLock)
            return _connectedClients.Count;
    }

    public int GetMaxClientCount()
    {
        return _maxClientCount;
    }

    public string GetDisplayName(string dataName)
    {
        for (int i = 0; i < SupportedDataDefinitions.Length; i++)
        {
            EnergyDataDefinition definition = SupportedDataDefinitions[i];
            if (definition.CanonicalKey == dataName)
                return definition.DisplayName;
        }

        return dataName;
    }

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

    public List<string> GetClientDebugLines()
    {
        lock (_clientLock)
        {
            List<ClientConnectionInfo> snapshot = new List<ClientConnectionInfo>(_clientInfoByClient.Values);
            snapshot.Sort((left, right) => left.ClientId.CompareTo(right.ClientId));

            List<string> lines = new List<string>();
            for (int i = 0; i < snapshot.Count; i++)
                lines.Add($"Client {snapshot[i].ClientId} / {snapshot[i].RemoteEndPoint} / Last: {snapshot[i].LastReceivedMessage}");

            return lines;
        }
    }

    public List<string> GetRecentMessagesSnapshot()
    {
        lock (_clientLock)
            return new List<string>(_recentMessages);
    }

    private void UpdateClientLastMessage(TcpClient client, string message)
    {
        lock (_clientLock)
        {
            if (!_clientInfoByClient.TryGetValue(client, out ClientConnectionInfo clientInfo))
                return;

            clientInfo.LastReceivedMessage = message;
            _clientInfoByClient[client] = clientInfo;
            _connectionStatusChanged = true;
        }
    }

    public void AddRecentMessage(string message)
    {
        lock (_clientLock)
        {
            _recentMessages.Enqueue(message);

            while (_recentMessages.Count > MaxRecentMessages)
                _recentMessages.Dequeue();

            _connectionStatusChanged = true;
        }
    }

    private int GetClientId(TcpClient client)
    {
        lock (_clientLock)
        {
            if (_clientInfoByClient.TryGetValue(client, out ClientConnectionInfo clientInfo))
                return clientInfo.ClientId;
        }

        return -1;
    }

    private string GetClientRemoteEndPoint(TcpClient client)
    {
        lock (_clientLock)
        {
            if (_clientInfoByClient.TryGetValue(client, out ClientConnectionInfo clientInfo))
                return clientInfo.RemoteEndPoint;
        }

        return client != null && client.Client.RemoteEndPoint != null
            ? client.Client.RemoteEndPoint.ToString()
            : "Unknown";
    }

    private int GetClientIdByRemoteEndPoint(string remoteEndPoint)
    {
        lock (_clientLock)
        {
            foreach (ClientConnectionInfo clientInfo in _clientInfoByClient.Values)
            {
                if (clientInfo.RemoteEndPoint == remoteEndPoint)
                    return clientInfo.ClientId;
            }
        }

        return -1;
    }
}
