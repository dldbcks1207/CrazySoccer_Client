using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class GameMaanger : MonoBehaviour
{
    public static GameMaanger Instance;
    public PlayerObject playerObjectPrefab;

    [SerializeField] private SoccerBallObject soccerBall;
    Dictionary<ushort, PlayerObject> playerObjects = new Dictionary<ushort, PlayerObject>();
    private InputSystem_Actions inputActions;
    private float lastSentHorizontal = 0f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
    }

    public void InitializeSession(ushort playerNum)
    {
        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            for (ushort i = 1; i <= playerNum; i++)
            {
                PlayerObject playerObj = Instantiate(playerObjectPrefab);
                playerObjects.Add(i, playerObj);
            }
        });
    }

    public void HandleNewSessionConnect(BinaryReader br)
    {
        ushort playerID = br.ReadUInt16();

        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            if (playerObjects.ContainsKey(playerID)) return;

            PlayerObject playerObj = Instantiate(playerObjectPrefab);
            playerObjects.Add(playerID, playerObj);
        });
    }

    public void HandleSyncPosition(BinaryReader br)
    {
        ushort playerID = br.ReadUInt16();
        float playerX = br.ReadSingle();
        float playerY = br.ReadSingle();

        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            if (playerObjects.TryGetValue(playerID, out PlayerObject obj))
            {
                obj.playerTargetPosition = new Vector2(playerX, playerY);
            }
        });
    }

    private void Update()
    {
        if (ClientManager.Instance.networkStream == null) return;

        float currentHorizontal = inputActions.Player.Move.ReadValue<Vector2>().x;
        bool currentJump = inputActions.Player.Jump.WasPressedThisFrame();

        if (currentHorizontal != lastSentHorizontal || currentJump)
        {
            MovePacket myInput = new MovePacket();
            myInput.HorizontalInput = currentHorizontal;
            myInput.IsJump = currentJump;

            byte[] packetBytes = myInput.Serialize();
            NetworkStream stream = ClientManager.Instance.networkStream;
            stream.Write(packetBytes, 0, packetBytes.Length);

            lastSentHorizontal = currentHorizontal;
        }
    }
}
