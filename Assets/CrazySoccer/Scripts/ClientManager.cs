using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class ClientManager : MonoBehaviour
{
    static public ClientManager Instance;
    public PlayerSession playerSession = new PlayerSession();
    public NetworkStream networkStream;

    private Dictionary<PacketType, Action<BinaryReader>> packetHandlers = new Dictionary<PacketType, Action<BinaryReader>>();
    public ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        packetHandlers.Add(PacketType.SendSession, HandleSessionPacket);
        packetHandlers.Add(PacketType.NewSessionConnect, GameManager.Instance.HandleNewSessionConnect);
        packetHandlers.Add(PacketType.SyncWorld, GameManager.Instance.HandleSyncWorld);
        packetHandlers.Add(PacketType.GoalEvent, GameManager.Instance.HandleGoalEvent);
        packetHandlers.Add(PacketType.GameWait, GameManager.Instance.HandleGameWait);
        packetHandlers.Add(PacketType.GameStart, GameManager.Instance.HandleGameStart);
        packetHandlers.Add(PacketType.GameEnd, GameManager.Instance.HandleGameEnd);
        packetHandlers.Add(PacketType.AnimationPacket, GameManager.Instance.HandleAnimationPacket);
        
        playerSession.Client = new TcpClient();
    }

    public void ConnectToServer(string serverIP, int serverPort)
    {
        Debug.Log("Try Server Connect..");
        playerSession.Client.BeginConnect(serverIP, serverPort, (ar) =>
        {
            playerSession.Client.EndConnect(ar);
            networkStream = playerSession.Client.GetStream();
            ReceiveLoop();
            Debug.Log("Server Coneected");
            mainThreadQueue.Enqueue(() =>
            {
                TransitionManager.Instance.Transition(true);
            });
        }, null);
    }

    private void ReceiveLoop()
    {
        if (playerSession.Client == null || !playerSession.Client.Connected) return;

        byte[] headerBuffer = new byte[NetworkConfig.HeaderSize];
        networkStream.BeginRead(headerBuffer, 0, headerBuffer.Length, OnReadHeader, headerBuffer);
    }

    private void OnReadHeader(IAsyncResult ar)
    {
        try
        {
            byte[] headerBuffer = (byte[])ar.AsyncState;
            int bytesRead = networkStream.EndRead(ar);
            if (bytesRead == 0) return;

            int totalRead = bytesRead;
            while (totalRead < NetworkConfig.HeaderSize)
            {
                int read = networkStream.Read(headerBuffer, totalRead, NetworkConfig.HeaderSize - totalRead);
                if (read == 0) return;
                totalRead += read;
            }

            short packetSize = BitConverter.ToInt16(headerBuffer, 0);
            PacketType packetType = (PacketType)BitConverter.ToInt16(headerBuffer, 2);

            int bodyLength = packetSize - NetworkConfig.HeaderSize;

            if (bodyLength > 0)
            {
                byte[] bodyBuffer = new byte[bodyLength];
                networkStream.BeginRead(bodyBuffer, 0, bodyBuffer.Length, OnReadBody, new object[] { bodyBuffer, packetType });
            }
            else
            {
                // ★ 바디가 없는 패킷(GameWait, GameStart 등)은 여기서 처리됩니다!
                byte[] emptyBuffer = new byte[0];
                using (MemoryStream ms = new MemoryStream(emptyBuffer))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    if (packetHandlers.TryGetValue(packetType, out var handler))
                    {
                        handler.Invoke(br);
                    }
                    else
                    {
                        // ★ 추가: 등록되지 않은 패킷이 오면 경고를 띄워줍니다.
                        Debug.LogError($"[클라이언트] {packetType}은(는) 등록되지 않은 패킷입니다.");
                    }
                }
                ReceiveLoop();
            }
        }
        catch (Exception ex) { Debug.LogError($"헤더 수신 에러: {ex.Message}"); }
    }

    private void OnReadBody(IAsyncResult ar)
    {
        try
        {
            object[] state = (object[])ar.AsyncState;
            byte[] bodyBuffer = (byte[])state[0];
            PacketType packetType = (PacketType)state[1];

            int bytesRead = networkStream.EndRead(ar);
            if (bytesRead == 0) return;

            int totalRead = bytesRead;
            while (totalRead < bodyBuffer.Length)
            {
                int read = networkStream.Read(bodyBuffer, totalRead, bodyBuffer.Length - totalRead);
                if (read == 0) return;
                totalRead += read;
            }

            using (MemoryStream ms = new MemoryStream(bodyBuffer))
            using (BinaryReader br = new BinaryReader(ms))
            {
                if (packetHandlers.TryGetValue(packetType, out var handler))
                {
                    handler.Invoke(br);
                }
                else
                {
                    // ★ 추가: 등록되지 않은 패킷이 오면 경고를 띄워줍니다.
                    Debug.LogError($"[클라이언트] {packetType}은(는) 등록되지 않은 패킷입니다.");
                }
            }

            ReceiveLoop();
        }
        catch (Exception ex) { Debug.LogError($"바디 수신 에러: {ex.Message}"); }
    }

    private void HandleSessionPacket(BinaryReader br)
    {
        ulong sessionid = br.ReadUInt64();
        ushort playerid = br.ReadUInt16();
        ushort playerNum = br.ReadUInt16();
        playerSession.SessionID = sessionid;
        playerSession.PlayerID = playerid;
        GameManager.Instance.InitializeSession(playerNum);
    }

    void Update()
    {
        while (mainThreadQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    void OnApplicationQuit()
    {
        playerSession.Client.Close();
    }
}
