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

    PortJson portJson = new PortJson();


    SerialPort serialPort;
    private CancellationTokenSource cancellationTokenSource; // CancellationTokenSource 추가
    private StringBuilder serialBuffer = new StringBuilder();
    private Queue<string> dataQueue = new Queue<string>();
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
        portJson = JsonManager.instance.portJson;
        Debug.Log($"포트 데이터 로드됨: COM={portJson.com}, Baud={portJson.baudLate}");
        serialPort = new SerialPort(portJson.com, portJson.baudLate, Parity.None, 8, StopBits.One);

        Debug.Log("포트연결시도");
        //serialPort.ReadTimeout = 500;
        serialPort.Open();
        if (serialPort.IsOpen)
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
        cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        while (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                // 데이터를 수신

                string input = await Task.Run(() => ReadSerialData(), token);
                //string data = GetData(input);

                if (!string.IsNullOrEmpty(input) /*&& input.Length > 0*/)
                {
                    Debug.Log("받은데이터 : " + input);
                    ReceivedData(input);
                }

            }
            catch (TimeoutException ex)
            {
                // 데이터가 없을 때는 무시
                Debug.LogWarning("데이터 수신 시간 초과: " + ex.Message);
            }
        }
    }
    private string ReadSerialData()
    {
        try
        {

            string input = serialPort.ReadExisting(); // 데이터 읽기
            if (!string.IsNullOrEmpty(input))
            {
                serialBuffer.Append(input); // (1)

                string processed = TryGetCompleteMessage(serialBuffer.ToString()); // (2)
                if (processed != null) // (3)
                {
                    Debug.Log("완전한 데이터 수신: " + processed); // (4)
                    serialBuffer.Clear(); // (5)
                }
                return processed;
            }
            return "";
            //return serialPort.ReadLine(); // 데이터 읽기
        }
        catch (TimeoutException)
        {
            return null; // 시간 초과 시 null 반환
        }
    }
    private string TryGetCompleteMessage(string buffer)
    {
        int newlineIndex = buffer.IndexOf('\r');
        //Debug.Log(newlineIndex);
        if (newlineIndex >= 0)
        {
           
            string complete = buffer.Substring(0, newlineIndex).Trim();
            return complete;
        }

        return null; // 아직 끝나지 않은 메시지
    }
  

    public void SendData(string message)
    {
        if (serialPort.IsOpen)
        {
            try
            {
                serialPort.WriteLine(message); // 메시지 송신 (줄 바꿈 추가)
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
        //Debug.Log($"{data} 신호보내기");

    }

    void OnApplicationQuit()
    {
        // 포트 닫기

        // 종료 시 쓰레드 정리 및 포트 닫기

        if (cancellationTokenSource != null)
        {
            Debug.Log("Task 종료");
            cancellationTokenSource.Cancel(); // 작업 취소
        }
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }

    }
   




}
