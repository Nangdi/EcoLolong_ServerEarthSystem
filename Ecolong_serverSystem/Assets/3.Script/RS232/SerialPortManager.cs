using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
[Serializable]

public class SerialPortManager : MonoBehaviour
{
    public static SerialPortManager Instance { get; private set; }

    private PortJson _portJson = new PortJson();


    private SerialPort _serialPort;
    private CancellationTokenSource _cancellationTokenSource;
    private StringBuilder _serialBuffer = new StringBuilder();
    private Queue<string> _dataQueue = new Queue<string>();
    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);

        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void Start()
    {
        // 포트 열기
        _portJson = JsonManager.instance.portJson;
        Debug.Log($"포트 데이터 로드됨: COM={_portJson.com}, Baud={_portJson.baudLate}");
        _serialPort = new SerialPort(_portJson.com, _portJson.baudLate, Parity.None, 8, StopBits.One);

        Debug.Log("포트연결시도");
        _serialPort.Open();
        if (_serialPort.IsOpen)
        {

            Debug.Log("연결완료");
            StartSerialPortReader();
        }
    }


    // 데이터 읽기
    void Update()
    {

    }
    async void StartSerialPortReader()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        while (_serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                string input = await Task.Run(() => ReadSerialData(), token);

                if (!string.IsNullOrEmpty(input))
                {
                    Debug.Log("받은데이터 : " + input);
                    ReceivedData(input);
                }

            }
            catch (TimeoutException ex)
            {
                Debug.LogWarning("데이터 수신 시간 초과: " + ex.Message);
            }
        }
    }
    private string ReadSerialData()
    {
        try
        {

            string input = _serialPort.ReadExisting();
            if (!string.IsNullOrEmpty(input))
            {
                _serialBuffer.Append(input);

                string processed = TryGetCompleteMessage(_serialBuffer.ToString());
                if (processed != null)
                {
                    Debug.Log("완전한 데이터 수신: " + processed);
                    _serialBuffer.Clear();
                }
                return processed;
            }
            return "";
        }
        catch (TimeoutException)
        {
            return null;
        }
    }
    private string TryGetCompleteMessage(string buffer)
    {
        int newlineIndex = buffer.IndexOf('\r');
        if (newlineIndex >= 0)
        {

            string complete = buffer.Substring(0, newlineIndex).Trim();
            return complete;
        }

        return null;
    }


    public void SendData(string message)
    {
        if (_serialPort.IsOpen)
        {
            try
            {
                _serialPort.WriteLine(message);
                Debug.Log("Sent: " + message);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("송신 오류: " + ex.Message);
            }
        }
        else
        {
            Debug.LogWarning("포트가 열려 있지 않음 - 송신 실패");
        }
    }
    protected virtual void ReceivedData(string data)
    {
        //상속하고 받은데이터로 프로젝트에 맞는 기능 구현
    }

    void OnApplicationQuit()
    {
        if (_cancellationTokenSource != null)
        {
            Debug.Log("Task 종료");
            _cancellationTokenSource.Cancel();
        }
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
        }

    }




}
