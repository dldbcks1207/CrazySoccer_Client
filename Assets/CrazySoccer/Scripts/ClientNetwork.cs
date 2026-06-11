using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;
using System.Collections.Concurrent;

public class ClientNetwork : MonoBehaviour
{
    private TcpClient socket;
    private InputSystem_Actions inputActions;
    private float lastSentHorizontal = 0f;
    [SerializeField] private float lerpSpeed = 15f;
    private Vector2 playerTargetPosition;
    private Vector2 soccerBallTargetPosition;
    [SerializeField] private Transform player;
    [SerializeField] private Transform soccerBall;
    private ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void Start()
    {
        socket = new TcpClient();
        Debug.Log("서버 연걸 시도");
        socket.BeginConnect("127.0.0.1", 9000, OnConnect, null);
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    private void OnConnect(IAsyncResult ar)
    {
        socket.EndConnect(ar);
        Debug.Log("서버와 연결되었습니다!");

        ReceiveLoop();
    }

    private void ReceiveLoop()
    {
        if (socket == null || !socket.Connected) return;

        NetworkStream stream = socket.GetStream();
        byte[] buffer = new byte[18];

        stream.BeginRead(buffer, 0, buffer.Length, OnReadComplete, new object[] { stream, buffer });
    }

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

    void Update()
    {
        while (mainThreadQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }

        player.position = Vector2.Lerp(player.transform.position, playerTargetPosition, Time.deltaTime * lerpSpeed);
        soccerBall.position = Vector2.Lerp(soccerBall.transform.position, soccerBallTargetPosition, Time.deltaTime * lerpSpeed);
        if (socket == null || !socket.Connected) return;

        float currentHorizontal = inputActions.Player.Move.ReadValue<Vector2>().x;
        bool currentJump = inputActions.Player.Jump.WasPressedThisFrame();

        if (currentHorizontal != lastSentHorizontal || currentJump)
        {
            MovePacket myInput = new MovePacket();
            myInput.HorizontalInput = currentHorizontal;
            myInput.IsJump = currentJump;

            byte[] packetBytes = myInput.Serialize();
            NetworkStream stream = socket.GetStream();
            stream.Write(packetBytes, 0, packetBytes.Length);

            lastSentHorizontal = currentHorizontal;
        }
    }
    void OnApplicationQuit()
    {
        socket?.Close();
    }
}
