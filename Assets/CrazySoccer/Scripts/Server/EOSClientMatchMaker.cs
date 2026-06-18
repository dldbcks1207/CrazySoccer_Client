using UnityEngine;
using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Sessions;

public class EOSClientMatchmaker : MonoBehaviour
{
    private SessionsInterface sessionsInterface;

    void Start()
    {
        sessionsInterface = EOSManager.Instance.GetEOSPlatformInterface().GetSessionsInterface();
    }

    public void FindLobby()
    {
        Debug.Log("🔍 서버가 열어둔 빈 세션(게임 매치)을 검색합니다...");
        
        // [★수정1] MaxResults가 아니라 MaxSearchResults 입니다.
        var searchOptions = new CreateSessionSearchOptions { MaxSearchResults = 10 };
        sessionsInterface.CreateSessionSearch(ref searchOptions, out SessionSearch searchHandle);

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
            }
        });
    }

    private void JoinGameSession(SessionDetails sessionDetails)
    {
        var joinOptions = new JoinSessionOptions
        {
            SessionName = "CrazySoccer_Match", 
            // [★수정2] SessionDetailsHandle이 아니라 SessionHandle 입니다.
            SessionHandle = sessionDetails,
            LocalUserId = EOSClientManager.Instance.myUserId,
            PresenceEnabled = false
        };

        sessionsInterface.JoinSession(ref joinOptions, null, (ref JoinSessionCallbackInfo data) =>
        {
            if (data.ResultCode == Result.Success)
            {
                Debug.Log("✅ 에픽 게임 세션 접속 성공! 서버 IP 주소를 파싱합니다...");

                // [★수정3] 옵션과 함수 이름에 'Session'이 추가로 붙습니다.
                var ipOptions = new SessionDetailsCopySessionAttributeByKeyOptions { AttrKey = "ServerIP" };
                sessionDetails.CopySessionAttributeByKey(ref ipOptions, out SessionDetailsAttribute? ipAttr);

                var portOptions = new SessionDetailsCopySessionAttributeByKeyOptions { AttrKey = "ServerPort" };
                sessionDetails.CopySessionAttributeByKey(ref portOptions, out SessionDetailsAttribute? portAttr);

                string serverIP = "";
                string serverPort = "";

                // [★수정4] Lobby 때처럼 겹겹이 쌓인 마트료시카 구조를 완전히 벗겨냅니다 (.Value.Value)
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
            }
        });
    }
}