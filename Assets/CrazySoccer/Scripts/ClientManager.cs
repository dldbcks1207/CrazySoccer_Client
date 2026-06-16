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
        packetHandlers.Add(PacketType.SyncPlayerPosition, GameManager.Instance.HandleSyncPosition);
        packetHandlers.Add(PacketType.SyncBallPosition, GameManager.Instance.HandleSyncBall);
        packetHandlers.Add(PacketType.GoalEvent, GameManager.Instance.HandleGoalEvent);
        
        playerSession.Client = new TcpClient();
        Debug.Log("Try Server Connect..");
        playerSession.Client.BeginConnect("127.0.0.1", NetworkConfig.ServerPort, (ar) =>
        {
            playerSession.Client.EndConnect(ar);
            networkStream = playerSession.Client.GetStream();
            ReceiveLoop();
            Debug.Log("Server Coneected");
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

            byte[] bodyBuffer = new byte[packetSize - NetworkConfig.HeaderSize];
            networkStream.BeginRead(bodyBuffer, 0, bodyBuffer.Length, OnReadBody, new object[] { bodyBuffer, packetType });
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
