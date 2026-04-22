using System;

[Serializable]
public class TcpDataReceivedInfo
{
    public int ClientId;
    public string RemoteEndPoint;
    public string RawLine;
    public string RawName;
    public string CanonicalName;
    public string DisplayName;
    public int Count;
}
