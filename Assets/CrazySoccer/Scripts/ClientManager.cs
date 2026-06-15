using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class ClientManager : MonoBehaviour
{
    private PlayerSession playerSession = new PlayerSession() { Client = new TcpClient() };
    private NetworkStream networkStream;
    private Dictionary<PacketType, Action<BinaryReader>> packetHandlers = new Dictionary<PacketType, Action<BinaryReader>>();
    private 
    void Start()
    {
        Debug.Log("서버 연걸 시도");
        playerSession.Client.BeginConnect("127.0.0.1", NetworkConfig.ServerPort, (ar) =>
        {
            playerSession.Client.EndConnect(ar);
            networkStream = playerSession.Client.GetStream();
            ReceiveLoop();
        }, null);
    }

    private void ReceiveLoop()
    {
        if (playerSession.Client == null || !playerSession.Client.Connected) return;

        NetworkStream stream = networkStream;
        byte[] headerBuffer = new byte[NetworkConfig.HeaderSize];
        stream.BeginRead(headerBuffer, 0, headerBuffer.Length, OnReadHeader, headerBuffer);
    }

    private void OnReadHeader(IAsyncResult ar)
    {
        try
        {
            NetworkStream stream = networkStream;
            byte[] headerBuffer = (byte[])ar.AsyncState;

            int bytesRead = stream.EndRead(ar);
            if (bytesRead == 0) return;

            short packetSize = BitConverter.ToInt16(headerBuffer, 0);
            PacketType packetType = (PacketType)BitConverter.ToInt16(headerBuffer, 2);

            byte[] bodyBuffer = new byte[packetSize - NetworkConfig.HeaderSize];

            stream.BeginRead(bodyBuffer, 0, bodyBuffer.Length, OnReadBody, new object[] { bodyBuffer, packetType });
        }
        catch (Exception ex) { Debug.LogError($"헤더 수신 에러: {ex.Message}"); }
    }

    private void OnReadBody(IAsyncResult ar)
    {
        try
        {
            NetworkStream stream = networkStream;
            object[] state = (object[])ar.AsyncState;
            byte[] bodyBuffer = (byte[])state[0];
            PacketType packetType = (PacketType)state[1];

            int bytesRead = stream.EndRead(ar);
            if (bytesRead == 0) return;

            using (MemoryStream ms = new MemoryStream(bodyBuffer))
            using (BinaryReader br = new BinaryReader(ms))
            {
                if (packetHandlers.TryGetValue(packetType, out var handler))
                {
                    handler.Invoke(br);
                }
                else
                {
                    Debug.LogError($"{packetType}은 등록되지 않음");
                }
            }

            ReceiveLoop();
        }
        catch (Exception ex) { Debug.LogError($"Error: {ex.Message}"); }
    }


    void OnApplicationQuit()
    {
        playerSession.Client?.Close();
    }
}
