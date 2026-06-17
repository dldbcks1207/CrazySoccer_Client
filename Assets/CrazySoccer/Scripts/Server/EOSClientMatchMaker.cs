using UnityEngine;
using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;

public class EOSClientMatchmaker : MonoBehaviour
{
    private LobbyInterface lobbyInterface;

    void Start()
    {
        lobbyInterface = EOSManager.Instance.GetEOSPlatformInterface().GetLobbyInterface();
    }

    public void FindLobby()
    {
        Debug.Log("Finding Lobby...");
        var searchOptions = new CreateLobbySearchOptions { MaxResults = 10 };
        lobbyInterface.CreateLobbySearch(ref searchOptions, out LobbySearch searchHandle);

        var filterOptions = new LobbySearchSetParameterOptions
        {
            Parameter = new AttributeData { Key = "Version", Value = EOSClientManager.Instance.clientVersion },
            ComparisonOp = ComparisonOp.Equal
        };
        searchHandle.SetParameter(ref filterOptions);

        var findOptions = new LobbySearchFindOptions { LocalUserId = EOSClientManager.Instance.myUserId };

        searchHandle.Find(ref findOptions, null, (ref LobbySearchFindCallbackInfo data) =>
        {
            if (data.ResultCode != Result.Success)
            {
                Debug.LogError($"Failed Search Lobby : {data.ResultCode}");
                return;
            }

            var countOptions = new LobbySearchGetSearchResultCountOptions();
            uint resultCount = searchHandle.GetSearchResultCount(ref countOptions);

            if (resultCount > 0)
            {
                Debug.Log($"Found {resultCount}Room!");

                var copyOptions = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = 0 };
                searchHandle.CopySearchResultByIndex(ref copyOptions, out LobbyDetails lobbyDetails);
                JoinLobby(lobbyDetails);
            }
            else
            {
                Debug.Log("No Empty Room");
            }
        });
    }

    private void JoinLobby(LobbyDetails lobbyDetails)
    {
        var joinOptions = new JoinLobbyOptions
        {
            LobbyDetailsHandle = lobbyDetails,
            LocalUserId = EOSClientManager.Instance.myUserId, 
            PresenceEnabled = false
        };

        lobbyInterface.JoinLobby(ref joinOptions, null, (ref JoinLobbyCallbackInfo data) =>
        {
            if (data.ResultCode == Result.Success)
            {
                Debug.Log("Room joined! Get IPAddress...");

                var ipOptions = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = "ServerIP" };
                lobbyDetails.CopyAttributeByKey(ref ipOptions, out Epic.OnlineServices.Lobby.Attribute? ipAttr);

                var portOptions = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = "ServerPort" };
                lobbyDetails.CopyAttributeByKey(ref portOptions, out Epic.OnlineServices.Lobby.Attribute? portAttr);

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
                    Debug.Log($"Connect {serverIP}:{serverPort}...");
                }
            }
        });
    }
}