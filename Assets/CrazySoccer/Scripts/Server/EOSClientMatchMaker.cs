using UnityEngine;
using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Sessions;
using TMPro;

public class EOSClientMatchmaker : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI matchingTimer;
    
    private SessionsInterface sessionsInterface;

    private bool isMatching = false;
    private float elapsedTime = 0f;

    void Start()
    {
        sessionsInterface = EOSManager.Instance.GetEOSPlatformInterface().GetSessionsInterface();
    }

    private void Update()
    {
        if (isMatching && matchingTimer != null)
        {
            elapsedTime += Time.deltaTime;
            
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            
            matchingTimer.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    public void FindLobby()
    {
        Debug.Log("🔍 서버가 열어둔 빈 세션(게임 매치)을 검색합니다...");
        
        // ★ 수정: 처음 버튼을 눌렀을 때(!isMatching)만 타이머를 0으로 초기화합니다.
        // 재도전(Invoke)으로 다시 들어왔을 때는 isMatching이 true이므로 이 부분을 건너뛰고 시간이 계속 흘러갑니다.
        if (!isMatching)
        {
            isMatching = true;
            elapsedTime = 0f;
            if (matchingTimer != null) matchingTimer.text = "00:00";
        }
        
        var searchOptions = new CreateSessionSearchOptions { MaxSearchResults = 10 };
        sessionsInterface.CreateSessionSearch(ref searchOptions, out SessionSearch searchHandle);

        // ... 이하 코드는 기존과 동일하게 유지 ...

        var filterOptions = new SessionSearchSetParameterOptions
        {
            Parameter = new AttributeData { Key = "Version", Value = EOSClientManager.Instance.clientVersion },
            ComparisonOp = ComparisonOp.Equal
        };
        searchHandle.SetParameter(ref filterOptions);

        var findOptions = new SessionSearchFindOptions { LocalUserId = EOSClientManager.Instance.myUserId };

        searchHandle.Find(ref findOptions, null, (ref SessionSearchFindCallbackInfo data) =>
        {
            if (data.ResultCode != Result.Success)
            {
                Debug.LogError($"❌ 세션 검색 실패 : {data.ResultCode}");
                isMatching = false; // 에러 시 타이머 정지
                return;
            }

            var countOptions = new SessionSearchGetSearchResultCountOptions();
            uint resultCount = searchHandle.GetSearchResultCount(ref countOptions);

            if (resultCount > 0)
            {
                Debug.Log($"✅ {resultCount}개의 매치 발견! 첫 번째 세션에 접속합니다.");

                var copyOptions = new SessionSearchCopySearchResultByIndexOptions { SessionIndex = 0 };
                searchHandle.CopySearchResultByIndex(ref copyOptions, out SessionDetails sessionDetails);
                JoinGameSession(sessionDetails);
            }
            else
            {
                Debug.Log("❌ 대기 중인 빈 서버 세션이 없습니다.");
                Invoke("FindLobby", 2.0f);
            }
        });
    }

    private void JoinGameSession(SessionDetails sessionDetails)
    {
        var joinOptions = new JoinSessionOptions
        {
            SessionName = "CrazySoccer_Match", 
            SessionHandle = sessionDetails,
            LocalUserId = EOSClientManager.Instance.myUserId,
            PresenceEnabled = false
        };

        sessionsInterface.JoinSession(ref joinOptions, null, (ref JoinSessionCallbackInfo data) =>
        {
            if (data.ResultCode == Result.Success)
            {
                Debug.Log("✅ 에픽 게임 세션 접속 성공! 서버 IP 주소를 파싱합니다...");

                var ipOptions = new SessionDetailsCopySessionAttributeByKeyOptions { AttrKey = "ServerIP" };
                sessionDetails.CopySessionAttributeByKey(ref ipOptions, out SessionDetailsAttribute? ipAttr);

                var portOptions = new SessionDetailsCopySessionAttributeByKeyOptions { AttrKey = "ServerPort" };
                sessionDetails.CopySessionAttributeByKey(ref portOptions, out SessionDetailsAttribute? portAttr);

                string serverIP = "";
                string serverPort = "";

                if (ipAttr.HasValue && ipAttr.Value.Data.HasValue)
                {
                    serverIP = ipAttr.Value.Data.Value.Value.AsUtf8;
                }

                if (portAttr.HasValue && portAttr.Value.Data.HasValue)
                {
                    serverPort = portAttr.Value.Data.Value.Value.AsUtf8;
                }

                if (!string.IsNullOrEmpty(serverIP) && !string.IsNullOrEmpty(serverPort))
                {
                    Debug.Log($"🚀 [최종 목적지] {serverIP}:{serverPort} 로 소켓 접속을 시작합니다!");
                    TransitionManager.Instance.Transition(false);
                    MySceneManager.Instance.ChangeScene("GameScene", false, false, () =>
                    {
                        ClientManager.Instance.ConnectToServer(serverIP, int.Parse(serverPort)); 
                    }); 
                }
            }
            else
            {
                Debug.LogError($"❌ 세션 진입 실패: {data.ResultCode}");
                isMatching = false; // 접속 실패 시 타이머 정지
            }
        });
    }
}