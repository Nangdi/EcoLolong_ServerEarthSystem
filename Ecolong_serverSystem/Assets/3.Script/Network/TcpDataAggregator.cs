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
    private readonly List<TcpClient> connectedClients = new List<TcpClient>();
    private readonly Dictionary<TcpClient, ClientConnectionInfo> clientInfoByClient = new Dictionary<TcpClient, ClientConnectionInfo>();
    private readonly Queue<string> recentMessages = new Queue<string>();
    private readonly object clientLock = new object();

    private TcpListener listener;
    private CancellationTokenSource cancellationTokenSource;
    private bool isServerRunning;
    private bool connectionStatusChanged;
    private int nextClientId = 1;
    private readonly EnergyTotals energyTotals = new EnergyTotals();

    public event Action<EnergyTotals> TotalsChanged;
    public event Action<TcpDataReceivedInfo> DataReceived;
    public event Action DebugStateChanged;

    private struct DataPacket
    {
        public string RawName;
        public string CanonicalName;
        public string DisplayName;
        public int Count;
        public int ClientId;
        public string RemoteEndPoint;
        public string RawLine;
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

    // 씬 시작 시 설정값에 따라 TCP 서버를 자동으로 시작합니다.
    private void Start()
    {
        if (autoStart)
            StartServer();

        NotifyTotalsChanged();
        NotifyDebugStateChanged();
    }

    // 백그라운드에서 받은 TCP 데이터를 Unity 메인 스레드에서 합산하고 이벤트를 발생시킵니다.
    private void Update()
    {
        HandleKeyboardTestInput();

        bool totalsChanged = false;

        while (receivedQueue.TryDequeue(out DataPacket packet))
        {
            if (!energyTotals.AddValue(packet.CanonicalName, packet.Count))
            {
                AddRecentMessage($"[경고] 지원하지 않는 키 / Raw: {packet.RawName} / Canonical: {packet.CanonicalName} / Remote: {packet.RemoteEndPoint}");
                continue;
            }

            totalsChanged = true;
            NotifyDataReceived(packet);
        }

        if (connectionStatusChanged)
        {
            connectionStatusChanged = false;
            NotifyDebugStateChanged();
        }

        if (totalsChanged)
            NotifyTotalsChanged();
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
            AddRecentMessage($"[서버] 시작 / Port: {listenPort}");
            NotifyDebugStateChanged();
        }
        catch (Exception exception)
        {
            AddRecentMessage($"[오류] 서버 시작 실패 / {exception.Message}");
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

        NotifyTotalsChanged();
        NotifyDebugStateChanged();
        AddRecentMessage("[서버] 중지");
    }

    // TCP를 거치지 않고 데이터를 직접 추가합니다. 에디터 버튼이나 로컬 테스트에 사용합니다.
    public void AddData(string dataName, int count)
    {
        if (string.IsNullOrWhiteSpace(dataName))
            return;

        receivedQueue.Enqueue(new DataPacket
        {
            RawName = dataName,
            CanonicalName = NormalizeDataName(dataName),
            DisplayName = GetDisplayName(NormalizeDataName(dataName)),
            Count = count,
            ClientId = -1,
            RemoteEndPoint = "LOCAL_TEST",
            RawLine = dataName
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
            AddRecentMessage("[경고] 전송할 문자열이 비어 있습니다.");
            return;
        }

        List<TcpClient> clientsSnapshot;
        lock (clientLock)
            clientsSnapshot = new List<TcpClient>(connectedClients);

        if (clientsSnapshot.Count == 0)
        {
            AddRecentMessage("[경고] 현재 연결된 클라이언트가 없습니다.");
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
                AddRecentMessage($"[오류] 문자열 전송 실패 / {exception.Message}");
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

        AddRecentMessage($"[송신] {message}");
    }

    // 누적된 모든 합계를 초기화하고 변경 이벤트를 발생시킵니다.
    public void ClearTotals()
    {
        energyTotals.Clear();

        while (receivedQueue.TryDequeue(out _))
        {
        }

        lock (clientLock)
            recentMessages.Clear();

        NotifyTotalsChanged();
        NotifyDebugStateChanged();
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
                        AddRecentMessage($"[경고] 클라이언트 거부 / 최대 접속 수 {maxClientCount}");
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
                    AddRecentMessage($"[오류] 클라이언트 접속 처리 실패 / {exception.Message}");

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
                string canonicalName = NormalizeDataName(name);
                receivedQueue.Enqueue(new DataPacket
                {
                    RawName = name,
                    CanonicalName = canonicalName,
                    DisplayName = GetDisplayName(canonicalName),
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

    // 다른 스크립트가 총합 변경 시점을 받을 수 있도록 이벤트를 발생시킵니다.
    private void NotifyTotalsChanged()
    {
        TotalsChanged?.Invoke(energyTotals);
    }

    // 서버 상태나 디버그 표시 정보가 바뀌었을 때 갱신 이벤트를 발생시킵니다.
    private void NotifyDebugStateChanged()
    {
        DebugStateChanged?.Invoke();
    }

    // 데이터 한 건을 성공적으로 수신했을 때 외부 구독자에게 상세 정보를 전달합니다.
    private void NotifyDataReceived(DataPacket packet)
    {
        DataReceived?.Invoke(new TcpDataReceivedInfo
        {
            ClientId = packet.ClientId,
            RemoteEndPoint = packet.RemoteEndPoint,
            RawLine = packet.RawLine,
            RawName = packet.RawName,
            CanonicalName = packet.CanonicalName,
            DisplayName = packet.DisplayName,
            Count = packet.Count
        });
    }

    // 다른 스크립트에서 특정 데이터 이름의 현재 합계를 읽을 때 사용합니다.
    public int GetTotal(string dataName)
    {
        if (string.IsNullOrEmpty(dataName))
            return 0;

        string canonicalKey = NormalizeDataName(dataName);
        return energyTotals.GetValue(canonicalKey);
    }

    // 계산용으로 현재 누적 데이터를 전용 클래스 형태로 반환합니다.
    public EnergyTotals GetEnergyTotals()
    {
        return energyTotals;
    }

    // 디버그 UI에서 서버 실행 여부를 확인할 수 있도록 현재 상태를 반환합니다.
    public bool IsServerRunning()
    {
        return isServerRunning;
    }

    // 디버그 UI에서 현재 리슨 포트를 표시할 수 있도록 반환합니다.
    public int GetListenPort()
    {
        return listenPort;
    }

    // UI나 디버그용으로 현재 연결된 클라이언트 수를 안전하게 가져옵니다.
    public int GetConnectedClientCount()
    {
        lock (clientLock)
            return connectedClients.Count;
    }

    // 표준 데이터 키에 대응하는 화면 표시용 이름을 반환합니다.
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

    // 연결된 클라이언트의 디버그 정보를 안전하게 복사해서 반환합니다.
    public List<string> GetClientDebugLines()
    {
        lock (clientLock)
        {
            List<ClientConnectionInfo> snapshot = new List<ClientConnectionInfo>(clientInfoByClient.Values);
            snapshot.Sort((left, right) => left.ClientId.CompareTo(right.ClientId));

            List<string> lines = new List<string>();
            for (int i = 0; i < snapshot.Count; i++)
                lines.Add($"Client {snapshot[i].ClientId} / {snapshot[i].RemoteEndPoint} / Last: {snapshot[i].LastReceivedMessage}");

            return lines;
        }
    }

    // 최근 수신 메시지 로그를 UI 표시용으로 복사해서 반환합니다.
    public List<string> GetRecentMessagesSnapshot()
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
