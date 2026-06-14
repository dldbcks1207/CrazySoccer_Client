using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Collections.Generic;
using TMPro;

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
    private int leftScore = 0;
    private int rightScore = 0;
    [SerializeField] private TextMeshProUGUI scoreText;
    private Dictionary<PacketType, Action<BinaryReader>> packetHandlers = new Dictionary<PacketType, Action<BinaryReader>>();

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void Start()
    {
        packetHandlers.Add(PacketType.SyncPosition, HandleSyncPosition);
        packetHandlers.Add(PacketType.GoalEvent, HandleGoalEvent);

        socket = new TcpClient();
        Debug.Log("서버 연걸 시도");
        socket.BeginConnect("127.0.0.1", NetworkConfig.ServerPort, (ar) =>
        {
            socket.EndConnect(ar);
            networkStream = socket.GetStream();
            ReceiveLoop();
        }, null);
    }

    private void ReceiveLoop()
    {
        if (socket == null || !socket.Connected) return;

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

    private void HandleSyncPosition(BinaryReader br)
    {
        float playerX = br.ReadSingle(); float playerY = br.ReadSingle();
        float ballX = br.ReadSingle(); float ballY = br.ReadSingle();

        mainThreadQueue.Enqueue(() =>
        {
            playerTargetPosition = new Vector2(playerX, playerY);
            soccerBallTargetPosition = new Vector2(ballX, ballY);
        });
    }

    private void HandleGoalEvent(BinaryReader br)
    {
        short scoredTeam = br.ReadInt16();

        mainThreadQueue.Enqueue(() =>
        {
            if (scoredTeam == 1)
            {
                leftScore++;
                scoreText.text = $"{leftScore} : {rightScore}";
            }
            else if (scoredTeam == 2)
            {
                rightScore++;
                scoreText.text = $"{leftScore} : {rightScore}";
            }
        });
    }

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

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void OnApplicationQuit()
    {
        socket?.Close();
    }
}
