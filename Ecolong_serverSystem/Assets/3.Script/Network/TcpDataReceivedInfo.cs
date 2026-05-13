using System;

// 큐에 들어가는 수신 패킷이자 외부 이벤트 페이로드. struct라 GC 부담이 없습니다.
[Serializable]
public struct TcpDataReceivedInfo
{
    // 이 데이터를 보낸 클라이언트의 ID. 로컬 테스트용 AddData()는 -1로 표시됩니다.
    public int ClientId;
    // 이 데이터를 보낸 클라이언트의 원격 주소. 로컬 테스트는 "LOCAL_TEST"로 표시됩니다.
    public string RemoteEndPoint;
    // 한 줄에 여러 데이터가 올 수 있어 구분자(예: 세미콜론)로 나누기 전의 원본 줄을 보관합니다.
    public string RawLine;
    // 원본 입력에서 받은 데이터 이름. 예: "화력", "thermal", "THERMAL".
    public string RawName;
    // 표준 키로 통일된 이름. 예: "THERMAL".
    public string CanonicalName;
    // A:2, A=2, A,2, A 2 형식에서 2에 해당하는 값. A 형식은 1로 간주됩니다.
    public int Count;
}
