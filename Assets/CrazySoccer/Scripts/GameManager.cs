using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public PlayerObject playerObjectPrefab;
    [SerializeField] private CameraMoveScript cameraMoveScript;
    [SerializeField] private SoccerBallObject soccerBall;
    [SerializeField] private ShootGaugeObject shootGaugeObjectPrefab;

    Dictionary<ushort, PlayerObject> playerObjects = new Dictionary<ushort, PlayerObject>();
    private InputSystem_Actions inputActions;
    private float lastSentHorizontal = 0f;

    [SerializeField] private TextMeshProUGUI leftScoreText;
    [SerializeField] private TextMeshProUGUI rightScoreText;
    [SerializeField] private TextMeshProUGUI timerText;

    private int score1P = 0;
    private int score2P = 0;

    private bool isShooting = false;
    private ShootGaugeObject shootGaugeObj;
    private float shootDelay = 0.1f;
    private bool isWaitingForDriven = false;
    private float drivenTimer = 0f;
    private byte lockedGaugeValue = 0;

    // ★ 추가: 클라이언트 타이머 변수
    private float matchTimer = 180f; // 3분
    private bool isMatchRunning = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();

        // 시작할 때 기본 텍스트 03:00 세팅
        if (timerText != null) timerText.text = "03:00";
    }

    public void InitializeSession(ushort playerNum)
    {
        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            ushort myID = ClientManager.Instance.playerSession.PlayerID;

            for (ushort i = 1; i <= playerNum; i++)
            {
                PlayerObject playerObj = Instantiate(playerObjectPrefab);

                if (i == myID)
                {
                    playerObj.isLocalPlayer = true;
                }

                playerObjects.Add(i, playerObj);
            }

            cameraMoveScript.target = playerObjects[myID].transform;

            // ★ 추가: 내가 접속했는데 이미 방에 2명이 꽉 찼다면 공지 띄움! (내가 두 번째로 들어온 유저일 때)
            if (playerObjects.Count == 2)
            {
                NoticeManager.Instance.Down("잠시 후\n시작됩니다");
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

            if (playerID == ClientManager.Instance.playerSession.PlayerID)
            {
                playerObj.isLocalPlayer = true;
            }

            playerObjects.Add(playerID, playerObj);

            // ★ 추가: 나 혼자 있었는데 누군가 들어와서 2명이 되었다면 공지 띄움! (내가 첫 번째로 들어온 유저일 때)
            if (playerObjects.Count == 2)
            {
                NoticeManager.Instance.Down("잠시 후\n시작됩니다");
            }
        });
    }

    // ... (기존 코드) ...

    // ★ 추가: 서버에서 온 애니메이션 재생 명령 처리
    public void HandleAnimationPacket(BinaryReader br)
    {
        ushort pID = br.ReadUInt16();
        byte animNum = br.ReadByte();

        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            if (playerObjects.TryGetValue(pID, out PlayerObject obj))
            {
                obj.PlayAnimation(animNum); // ★ 함수 이름 변경 적용!
            }
        });
    }

    // ★ 추가: 세리머니 패킷을 서버로 쏘는 헬퍼 함수
    private void SendAnimation(byte animNum)
    {
        SendAnimationPacket animPacket = new SendAnimationPacket();
        animPacket.animNum = animNum;

        byte[] packetBytes = animPacket.Serialize();
        ClientManager.Instance.networkStream.Write(packetBytes, 0, packetBytes.Length);
    }

    public void HandleSyncWorld(BinaryReader br)
    {
        float ballX = br.ReadSingle();
        float ballY = br.ReadSingle();

        // ★ 추가: 타이머 값을 읽어옵니다. (보낸 순서와 완벽히 일치해야 함!)
        float serverTimer = br.ReadSingle();

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

            // ★ 추가: 클라이언트의 타이머를 서버의 절대 타이머로 동기화!
            // (이제 Update문의 Time.deltaTime으로 깎이는 값이 서버의 값으로 계속 교정됩니다)
            matchTimer = serverTimer;

            foreach (var data in syncDataList)
            {
                if (playerObjects.TryGetValue(data.id, out PlayerObject obj))
                {
                    obj.playerTargetPosition = new Vector2(data.x, data.y);
                }
            }
        });
    }

    public void HandleGameEnd(BinaryReader br)
    {
        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            Debug.Log("게임 종료! EOS 세션 정리 및 타이틀 씬으로 돌아갑니다.");

            // 1. 소켓 연결 즉시 끊기 (서버에 Disconnect를 즉시 알리기 위함)
            if (ClientManager.Instance.playerSession.Client != null)
            {
                ClientManager.Instance.playerSession.Client.Close();
                ClientManager.Instance.networkStream = null;
            }

            // 2. ★ [핵심 추가] 에픽 온라인 서비스(EOS) 세션 깔끔하게 나가기 (DestroySession)
            // 이 작업을 해줘야 다음 판에 SessionsSessionAlreadyExists 에러가 안 납니다!
            var sessionsInterface = PlayEveryWare.EpicOnlineServices.EOSManager.Instance
                .GetEOSPlatformInterface().GetSessionsInterface();

            if (sessionsInterface != null)
            {
                var destroyOptions = new Epic.OnlineServices.Sessions.DestroySessionOptions
                {
                    SessionName = "CrazySoccer_Match" // 우리가 Join할 때 썼던 세션 이름과 완벽히 일치해야 합니다!
                };

                sessionsInterface.DestroySession(ref destroyOptions, null, (ref Epic.OnlineServices.Sessions.DestroySessionCallbackInfo data) =>
                {
                    if (data.ResultCode == Epic.OnlineServices.Result.Success)
                    {
                        Debug.Log("✅ EOS 세션 파괴 성공! 에픽 서버에서 완전히 퇴장했습니다.");
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ EOS 세션 파괴 실패 또는 이미 없음: {data.ResultCode}");
                    }
                });
            }

            // 3. MySceneManager를 이용해 우아하게 0.9초 연출 후 타이틀로 이동
            MySceneManager.Instance.ChangeScene("LobbyScene", true, false, () =>
            {
                Debug.Log("타이틀 씬 로드 완료 및 다음 매칭 준비 완.");
            });
        });
    }

    public void HandleGoalEvent(BinaryReader br)
    {
        short scoredTeam = br.ReadInt16();

        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            Debug.Log($"Goal in! ScoredTeam:{scoredTeam}");

            // 점수판 업데이트
            NoticeManager.Instance.Down("G O A L !");
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

            // ★ 중요: 여기에 있던 아래의 위치 초기화 코드들을 삭제했습니다!
            // (서버가 5초 뒤에 알아서 초기화 좌표를 내려줄 것이므로 클라이언트가 미리 움직이면 안 됩니다)
            // if (soccerBall != null) soccerBall.targetPosition = Vector2.zero;
            // foreach ... item.Value.playerTargetPosition = ...
        });
    }

    public void HandleGameWait(BinaryReader br)
    {
        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            TransitionManager.Instance.Transition(false);

            // ★ 골 세리머니나 대기 중일 때는 타이머를 멈춥니다.
            isMatchRunning = false;
        });
    }

    public void HandleGameStart(BinaryReader br)
    {
        ClientManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            TransitionManager.Instance.Transition(true);
            NoticeManager.Instance.Up();

            // ========================================================
            // ★ 추가: 0.1초의 핑 차이로 인한 게임종료 버그를 막기 위해
            // 시작하자마자 내 타이머를 180초로 멱살 잡고 끌어올립니다.
            // ========================================================
            matchTimer = 180f;

            // ★ 게임 시작(조작 가능)일 때 타이머를 흘러가게 켭니다.
            isMatchRunning = true;
        });
    }

    public bool GetPlayerDirection()
    {
        return playerObjects[ClientManager.Instance.playerSession.PlayerID].transform.localScale.x < 0f;
    }

    private void Update()
    {
        if (ClientManager.Instance.networkStream == null) return;

        // UI 타이머 흐르는 로직
        if (isMatchRunning)
        {
            matchTimer -= Time.deltaTime;
            if (matchTimer <= 0f)
            {
                matchTimer = 0f;
                isMatchRunning = false; // 시간이 다 되면 멈춤

                // ★ 추가: 시간이 0초가 되면 게임종료 공지를 딱! 띄워줍니다.
                NoticeManager.Instance.Down("게 임 종 료");
            }

            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(matchTimer / 60f);
                int seconds = Mathf.FloorToInt(matchTimer % 60f);
                // "00:00" 형식으로 텍스트 업데이트
                timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
        }

        float currentHorizontal = inputActions.Player.Move.ReadValue<Vector2>().x;
        bool currentJump = inputActions.Player.Jump.WasPressedThisFrame();

        ushort myID = ClientManager.Instance.playerSession.PlayerID;
        if (playerObjects.TryGetValue(myID, out PlayerObject myPlayerObj))
        {
            myPlayerObj.localInputX = currentHorizontal;

            if (isShooting && shootGaugeObj != null)
            {
                myPlayerObj.SetShootRangeAlpha(shootGaugeObj.CurrentGauge / 100f);
            }
            else
            {
                myPlayerObj.SetShootRangeAlpha(0f);
            }
        }

        if (currentHorizontal != lastSentHorizontal || currentJump)
        {
            MovePacket myInput = new MovePacket();
            myInput.HorizontalInput = currentHorizontal;
            myInput.IsJump = currentJump;

            byte[] packetBytes = myInput.Serialize();
            ClientManager.Instance.networkStream.Write(packetBytes, 0, packetBytes.Length);

            lastSentHorizontal = currentHorizontal;
        }

        if (inputActions.Player.Ceremony1.WasPressedThisFrame())
            SendAnimation(1);

        if (inputActions.Player.Ceremony2.WasPressedThisFrame())
            SendAnimation(2);

        // 슛관련
        // 슛관련
        // 슛관련
        if (isWaitingForDriven)
        {
            drivenTimer -= Time.deltaTime;

            if (inputActions.Player.Kick.WasPressedThisFrame())
            {
                KickPacket kickPacket = new KickPacket();
                kickPacket.Force = lockedGaugeValue;
                kickPacket.IsDriven = true;
                kickPacket.IsDirectionLeft = GetPlayerDirection();

                byte[] packetBytes = kickPacket.Serialize();
                ClientManager.Instance.networkStream.Write(packetBytes, 0, packetBytes.Length);

                // 안전장치: 혹시라도 딕셔너리에 내가 없으면 에러 나지 않게 확인 후 재생
                if (playerObjects.ContainsKey(myID))
                    playerObjects[myID].PlayAnimation(0);

                Debug.Log($"Driven Shoot : {lockedGaugeValue}");
                isWaitingForDriven = false;
            }
            else if (drivenTimer <= 0f)
            {
                KickPacket kickPacket = new KickPacket();
                kickPacket.Force = lockedGaugeValue;
                kickPacket.IsDriven = false;
                kickPacket.IsDirectionLeft = GetPlayerDirection();

                byte[] packetBytes = kickPacket.Serialize();
                ClientManager.Instance.networkStream.Write(packetBytes, 0, packetBytes.Length);

                if (playerObjects.ContainsKey(myID))
                    playerObjects[myID].PlayAnimation(0);

                Debug.Log($"Normal Shoot : {lockedGaugeValue}");
                isWaitingForDriven = false;
            }
        }
        else
        {
            if (inputActions.Player.Kick.WasPressedThisFrame())
            {
                if (!isShooting)
                {
                    isShooting = true;
                    shootGaugeObj = Instantiate(shootGaugeObjectPrefab);
                    shootGaugeObj.followTarget = playerObjects[myID].transform;

                    shootGaugeObj.StartGauge((value) =>
                    {
                        KickPacket kickPacket = new KickPacket();
                        kickPacket.Force = value;
                        kickPacket.IsDriven = false;
                        kickPacket.IsDirectionLeft = GetPlayerDirection();

                        byte[] packetBytes = kickPacket.Serialize();
                        ClientManager.Instance.networkStream.Write(packetBytes, 0, packetBytes.Length);

                        if (playerObjects.ContainsKey(myID))
                            playerObjects[myID].PlayAnimation(0);

                        Debug.Log($"Max Power Shoot : {value}");
                        isShooting = false;
                    });
                }
            }
            // ========================================================
            // ★ 잃어버렸던 그 코드, 완벽하게 복구!! (키를 뗐을 때 멈춤)
            // ========================================================
            else if (inputActions.Player.Kick.WasReleasedThisFrame())
            {
                if (isShooting && shootGaugeObj != null)
                {
                    lockedGaugeValue = shootGaugeObj.StopGauge();
                    isShooting = false;

                    isWaitingForDriven = true;
                    drivenTimer = shootDelay;
                }
            }
        }
    }
}