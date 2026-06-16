using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public PlayerObject playerObjectPrefab;
    [SerializeField] private CameraMoveScript cameraMoveScript;
    [SerializeField] private SoccerBallObject soccerBall;
    Dictionary<ushort, PlayerObject> playerObjects = new Dictionary<ushort, PlayerObject>();
    private InputSystem_Actions inputActions;
    private float lastSentHorizontal = 0f;

    public TextMeshProUGUI leftScoreText;
    public TextMeshProUGUI rightScoreText;
    private int score1P = 0;
    private int score2P = 0;

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

            cameraMoveScript.target = playerObjects[ClientManager.Instance.playerSession.PlayerID].transform;
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

    public void HandleSyncWorld(BinaryReader br)
    {
        float ballX = br.ReadSingle();
        float ballY = br.ReadSingle();

        ushort playerCount = br.ReadUInt16();
        List<(ushort id, float x, float y)> syncDataList = new List<(ushort, float, float)>();

        for (int i = 0; i < playerCount; i++)
        {
            ushort pID = br.ReadUInt16();
            float pX = br.ReadSingle();
            float pY = br.ReadSingle();
            syncDataList.Add((pID, pX, pY));
        }

        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            if (soccerBall != null)
            {
                soccerBall.targetPosition = new Vector2(ballX, ballY);
            }

            foreach (var data in syncDataList)
            {
                if (playerObjects.TryGetValue(data.id, out PlayerObject obj))
                {
                    obj.playerTargetPosition = new Vector2(data.x, data.y);
                }
            }
        });
    }

    public void HandleGoalEvent(BinaryReader br)
    {
        short scoredTeam = br.ReadInt16();

        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            Debug.Log($"Goal in! ScoredTeam:{scoredTeam}");

            if (scoredTeam == 1)
            {
                score1P++;
                if (leftScoreText != null) leftScoreText.text = score1P.ToString();
            }
            else if (scoredTeam == 2)
            {
                score2P++;
                if (rightScoreText != null) rightScoreText.text = score2P.ToString();
            }


            if (soccerBall != null) soccerBall.targetPosition = Vector2.zero;

            foreach (KeyValuePair<ushort, PlayerObject> item in playerObjects)
            {
                item.Value.playerTargetPosition = (item.Key == 1) ? new Vector2(-5f, 0f) : new Vector2(5f, 0f);
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
