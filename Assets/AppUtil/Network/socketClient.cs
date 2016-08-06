﻿using UnityEngine;
using Proto4z;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

enum SessionStatus
{
    SS_UNINIT,
    SS_INITED,
    SS_CONNECTING,
    SS_WORKING,
    SS_CLOSED,
}

class ProtoHeader : IProtoObject
{
    public const int HeadLen = 8;
    public int packLen;
    public ushort reserve;
    public ushort protoID;
    public System.Collections.Generic.List<byte> __encode()
    {
        var ret = new System.Collections.Generic.List<byte>();
        ret.AddRange(BaseProtoObject.encodeI32(packLen));
        ret.AddRange(BaseProtoObject.encodeUI16(reserve));
        ret.AddRange(BaseProtoObject.encodeUI16(protoID));
        return ret;
    }
    public System.Int32 __decode(byte[] binData, ref System.Int32 pos)
    {
        packLen = BaseProtoObject.decodeI32(binData, ref pos);
        reserve = BaseProtoObject.decodeUI16(binData, ref pos);
        protoID = BaseProtoObject.decodeUI16(binData, ref pos);
        return pos;
    }
}


class Session
{
    Socket _socket;
    SessionStatus _status = SessionStatus.SS_UNINIT;
    IPAddress _addr;
    ushort _port;
    bool _reconnect = true;
    float _lastConnectTime = 0.0f;
    const int MAX_BUFFER_SIZE = 200 * 1024;


    string _encrypt;

    RC4Encryption _rc4Send;
    private byte[] _sendBuffer;
    private int _sendBufferLen = 0;
    private System.Collections.Generic.Queue<byte[]> _sendQue;

    RC4Encryption _rc4Recv;
    private byte[] _recvBuffer;
    private int _recvBufferLen = 0;

    public Session()
    {
        _sendBuffer = new byte[MAX_BUFFER_SIZE];
        _recvBuffer = new byte[MAX_BUFFER_SIZE];
        _sendQue = new System.Collections.Generic.Queue<byte[]>();
    }
    public bool Init(string host, ushort port, string encrypt)
    {
        try
        {
            _encrypt = encrypt.Trim();
            host = host.Trim();
            if (host.Length == 0 || port == 0)
            {
                Debug.logger.Log(LogType.Error, "Session::Init Session param error. host=" + host + ", port=" + port + ", status=" + _status);
                return false;
            }
            if (_status != SessionStatus.SS_UNINIT)
            {
                Debug.logger.Log(LogType.Error, "Session::Init Session status error. host=" + host + ", port=" + port + ", status=" + _status);
                return false;
            }
            _status = SessionStatus.SS_INITED;
            _addr = null;
            _port = port;
            IPAddress[] addrs = Dns.GetHostEntry(host).AddressList;
            foreach (var addr in addrs)
            {
                if (_addr == null ||
                    (_addr.AddressFamily != AddressFamily.InterNetworkV6 && addr.AddressFamily == AddressFamily.InterNetworkV6))
                {
                    _addr = addr;
                }
                if (_addr.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    break;
                }
            }
            if (_addr == null)
            {
                _status = SessionStatus.SS_UNINIT;
                Debug.logger.Log(LogType.Error, "Session::Init can't resolve host. host=" + host + ", port=" + port);
                return false;
            }
        }
        catch (Exception e)
        {
            _status = SessionStatus.SS_UNINIT;
            Debug.logger.Log(LogType.Error, "Session::Init had except. host=" + host + ", port=" + port + ",e=" + e);
            return false;
        }
        return true;
    }
    public void Connect()
    {
        Debug.logger.Log("Session::Connect addr=" + _addr + ", port=" + _port);
        _lastConnectTime = Time.realtimeSinceStartup;
        if (_status != SessionStatus.SS_INITED)
        {
            Debug.logger.Log(LogType.Error, "BeginConnect Session status error. addr=" + _addr + ", port=" + _port +", status =" + _status);
            return;
        }

        try
        {
            _rc4Recv = new RC4Encryption();
            _rc4Recv.makeSBox(_encrypt);
            _rc4Send = new RC4Encryption();
            _rc4Send.makeSBox(_encrypt);
            _status = SessionStatus.SS_CONNECTING;
            _socket = new Socket(_addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.BeginConnect(_addr, _port, new AsyncCallback(OnConnect), _socket);
        }
        catch (Exception e)
        {
            _status = SessionStatus.SS_CLOSED;
            Debug.logger.Log(LogType.Error, "Session::Init had except. addr=" + _addr + ", port=" + _port + ",e=" + e);
        }
    }
    public void OnConnect(IAsyncResult result)
    {
        try
        {
            Socket socket = result.AsyncState as Socket;
            if(socket != _socket )
            {
                Debug.logger.Log(LogType.Warning, "Session::onConnect _socket not AsyncState. host=" + _addr + ", port=" + _port + ", status =" + _status);
                return;
            }
            if (_status != SessionStatus.SS_CONNECTING)
            {
                Debug.logger.Log(LogType.Warning, "Session::onConnect status error . host=" + _addr + ", port=" + _port + ", status =" + _status);
                return;
            }
            socket.EndConnect(result);
            socket.Blocking = false;
            _status = SessionStatus.SS_WORKING;
        }
        catch (Exception e)
        {
            Debug.logger.Log(LogType.Error, "Session::onConnect had except. host=" + _addr + ", port=" + _port + ",e=" + e);
            Close();
        }
    }
    public void Send(byte[] data)
    {
        _sendQue.Enqueue(data);
    }
    public void  Send<Proto>(Proto proto) where Proto : Proto4z.IProtoObject
    {
        ProtoHeader ph = new ProtoHeader();
        ph.reserve = 0;
        Type pType = proto.GetType();
        var mi = pType.GetMethod("getProtoID");
        if (mi == null)
        {
            Debug.logger.Log(LogType.Error, "Session::SendProto can not find method getProtoID. ");
            return;
        }
        ph.protoID = (ushort)mi.Invoke(proto, null);
        var bin = proto.__encode().ToArray();
        ph.packLen = ProtoHeader.HeadLen + bin.Length;
        var pack = ph.__encode();
        pack.AddRange(bin);
        Send(pack.ToArray());
    }


    public void OnRecv(ushort protoID, byte[] bin)
    {
        Debug.logger.Log("recv one pack len=" + bin.Length + ", protoID=" + protoID);
        if (protoID == ClientAuthResp.getProtoID())
        {
            ClientAuthResp resp = new ClientAuthResp();
            int offset = 0;
            resp.__decode(bin, ref offset);
        }
    }
    public void Close(bool reconnect = false)
    {
        if (_status == SessionStatus.SS_CLOSED)
        {
            return;
        }
        _recvBufferLen = 0;
        _sendBufferLen = 0;
        if(_socket != null)
        {
            _socket.Close();
            _socket = null;
        }
        if (reconnect && _status != SessionStatus.SS_UNINIT)
        {
            _status = SessionStatus.SS_INITED;
        }
        else
        {
            _status = SessionStatus.SS_CLOSED;
        }
    }
    public void Update()
    {
        try
        {
            //Debug.logger.Log("cur=" + Time.realtimeSinceStartup + ", last=" + _lastConnectTime);
            if (_status == SessionStatus.SS_INITED)
            {
                //两次Connect间隔最短不能小于3秒  
                if (Time.realtimeSinceStartup - _lastConnectTime > 3.0)
                {
                    Connect();
                }
                return;
            }
            if (_status == SessionStatus.SS_CONNECTING)
            {
                //Connect超过7秒还没成功就算超时.  
                if (Time.realtimeSinceStartup - _lastConnectTime > 7.0)
                {
                    Close(_reconnect);
                }
                return;
            }

            if (_status != SessionStatus.SS_WORKING)
            {
                return;
            }
            //Receive 每帧只读取一次, 每次都尽可能去读满缓冲.  
            if (_recvBufferLen < MAX_BUFFER_SIZE)
            {
                do
                {
                    int total = _socket.Available;
                    if (total == 0)
                    {
                        break; // 没有可读数据 
                    }
                    int ret = _socket.Receive(_recvBuffer, _recvBufferLen, MAX_BUFFER_SIZE - _recvBufferLen, SocketFlags.None);
                    if (ret <= 0)
                    {
                        Debug.logger.Log(LogType.Error, "!!!Unintended!!! remote closed socket. host=" + _addr + ", port=" + _port + ", status =" + _status);
                        Close();
                        return;
                    }
                    if (_encrypt.Length > 0)
                    {
                        _rc4Recv.encryption(_recvBuffer, _recvBufferLen, ret);
                    }
                    _recvBufferLen += ret;
                    //check message 
                    int offset = 0;
                    while (_recvBufferLen - offset >= ProtoHeader.HeadLen)
                    {
                        ProtoHeader ph = new ProtoHeader();
                        ph.__decode(_recvBuffer, ref offset);
                        if (ph.packLen <= _recvBufferLen - (offset - ProtoHeader.HeadLen))
                        {
                            var pack = new byte[ph.packLen - ProtoHeader.HeadLen];
                            Array.Copy(_recvBuffer, offset, pack, 0,  ph.packLen - ProtoHeader.HeadLen);
                            OnRecv(ph.protoID, pack);
                            offset += (ph.packLen - ProtoHeader.HeadLen);
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (offset > 0)
                    {
                        if (_recvBufferLen == offset)
                        {
                            _recvBufferLen = 0;
                        }
                        else
                        {
                            Array.Copy(_recvBuffer, offset, _recvBuffer, 0, _recvBufferLen - offset);
                            _recvBufferLen -= offset;
                        }
                    }
                    
                } while (true);
            }
            //send 
            if (_sendBufferLen > 0 || _sendQue.Count > 0)
            {
                while (_sendQue.Count > 0)
                {
                    if (_sendQue.Peek().Length <= MAX_BUFFER_SIZE - _sendBufferLen)
                    {
                        var pack = _sendQue.Dequeue();
                        pack.CopyTo(_sendBuffer, _sendBufferLen);
                        if (_encrypt.Length > 0)
                        {
                            _rc4Send.encryption(_sendBuffer, _sendBufferLen, pack.Length);
                        }
                        _sendBufferLen += pack.Length;
                    }
                    else
                    {
                        break;
                    }
                }
                if (_sendBufferLen > 0) // conditional  when invalid pack 
                {
                    int ret = _socket.Send(_sendBuffer, 0, _sendBufferLen, SocketFlags.None);
                    if (ret == _sendBufferLen)
                    {
                        _sendBufferLen = 0;
                    }
                    else if (ret > 0)
                    {
                        Array.Copy(_sendBuffer, ret, _sendBuffer, 0, _sendBufferLen - ret);
                        _sendBufferLen -= ret;
                    }
                }
            }
            
        }
        catch (Exception e)
        {
            Debug.logger.Log(LogType.Error, "Session::Update Receive or Send had except. host=" + _addr + ", port=" + _port + ",e=" + e);
            Close();
        }
    }
}

public class socketClient : MonoBehaviour
{
    Session _client;
    void Start ()
    {
        Debug.logger.Log("socketClient::Start ");
        _client = new Session();
        if (!_client.Init("127.0.0.1", 26001, ""))
        {
            //return;
        }
        
        ClientAuthReq req = new ClientAuthReq("test", "123");
        _client.Send(req);
    }
   
	// Update is called once per frame
	void Update ()
    {
        _client.Update();
	}



}
