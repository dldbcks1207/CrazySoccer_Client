using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;
using System.Collections.Concurrent;
using System.IO;

public class ClientNetwork : MonoBehaviour
{
    private NetworkStream networkStream;
    private TcpClient socket;
    private InputSystem_Actions inputActions;
    private float lastSentHorizontal = 0f;
    [SerializeField] private float lerpSpeed = 15f;
    private Vector2 playerTargetPosition;
    private Vector2 soccerBallTargetPosition;
    [SerializeField] private Transform player;
    [SerializeField] private Transform soccerBall;
    private ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
    private Vector3 lastSoccerBallPosition;
    [SerializeField] private float ballRotationSpeed = 200f;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void Start()
    {
        socket = new TcpClient();
        Debug.Log("서버 연걸 시도");
        socket.BeginConnect("127.0.0.1", 9000, (ar) =>
        {
            socket.EndConnect(ar);
            networkStream = socket.GetStream();
            ReceiveLoop();
        }, null);
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    private void ReceiveLoop()
    {
        if (socket == null || !socket.Connected) return;

        NetworkStream stream = networkStream;
        byte[] headerBuffer = new byte[4];
        //stream.BeginRead(buffer, 0, buffer.Length, OnReadComplete, new object[] { stream, buffer });
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

            byte[] bodyBuffer = new byte[packetSize - 4];

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
                switch (packetType)
                {
                    case PacketType.SyncPosition:
                        float playerX = br.ReadSingle(); float playerY = br.ReadSingle();
                        float ballX = br.ReadSingle(); float ballY = br.ReadSingle();

                        mainThreadQueue.Enqueue(() =>
                        {
                            playerTargetPosition = new Vector2(playerX, playerY);
                            soccerBallTargetPosition = new Vector2(ballX, ballY);
                        });
                        break;
                    case PacketType.GoalEvent:
                        short scoredTeam = br.ReadInt16();

                        mainThreadQueue.Enqueue(() =>
                        {
                            Debug.Log($"Goal {scoredTeam}");
                        });
                        break;
                }
            }

            ReceiveLoop();
        }
        catch (Exception ex) { Debug.LogError($"Error: {ex.Message}"); }
    }
    /*
    private void OnReadComplete(IAsyncResult ar)
    {
        object[] state = (object[])ar.AsyncState;
        NetworkStream stream = (NetworkStream)state[0];
        byte[] buffer = (byte[])state[1];

        try
        {
            int bytesRead = stream.EndRead(ar);
            if (bytesRead == 0) return;

            SyncPacket receivedPacket = SyncPacket.Deserialize(buffer);

            switch (receivedPacket.Type)
            {
                case PacketType.SyncPosition:
                    mainThreadQueue.Enqueue(() =>
                    {
                        playerTargetPosition = new Vector2(receivedPacket.PlayerX, receivedPacket.PlayerY);
                        soccerBallTargetPosition = new Vector2(receivedPacket.BallX, receivedPacket.BallY);
                    });
                    break;
            }
            stream.BeginRead(buffer, 0, buffer.Length, OnReadComplete, state);
        }
        catch (Exception ex)
        {
            Debug.Log($"Error: {ex.Message}");
        }
    }
    */

    void Update()
    {
        while (mainThreadQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }

        player.position = Vector2.Lerp(player.transform.position, playerTargetPosition, Time.deltaTime * lerpSpeed);
        Vector3 ballPrevPos = soccerBall.position;
        soccerBall.position = Vector2.Lerp(soccerBall.transform.position, soccerBallTargetPosition, Time.deltaTime * lerpSpeed);

        float moveDeltaX = soccerBall.position.x - ballPrevPos.x;
        float rotationAmount = moveDeltaX * ballRotationSpeed;
        soccerBall.Rotate(Vector3.forward, -rotationAmount);

        if (socket == null || !socket.Connected) return;

        float currentHorizontal = inputActions.Player.Move.ReadValue<Vector2>().x;
        bool currentJump = inputActions.Player.Jump.WasPressedThisFrame();

        if (currentHorizontal != lastSentHorizontal || currentJump)
        {
            MovePacket myInput = new MovePacket();
            myInput.HorizontalInput = currentHorizontal;
            myInput.IsJump = currentJump;

            byte[] packetBytes = myInput.Serialize();
            NetworkStream stream = networkStream;
            stream.Write(packetBytes, 0, packetBytes.Length);

            lastSentHorizontal = currentHorizontal;
        }
    }
    void OnApplicationQuit()
    {
        socket?.Close();
    }
}
