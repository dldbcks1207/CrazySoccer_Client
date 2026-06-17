using UnityEngine;
using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;

public class EOSClientManager : MonoBehaviour
{
    static public EOSClientManager Instance;

    public string clientVersion = "v1.0";
    public ProductUserId myUserId;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Invoke("LoginWithDeviceID", 1.0f);
    }

    private void LoginWithDeviceID()
    {
        Debug.Log("Try Login...");

        ConnectInterface connectInterface = EOSManager.Instance.GetEOSPlatformInterface().GetConnectInterface();

        var createDeviceOptions = new CreateDeviceIdOptions { DeviceModel = "UnityEditor" };

        connectInterface.CreateDeviceId(ref createDeviceOptions, null, (ref CreateDeviceIdCallbackInfo createData) =>
        {
            if (createData.ResultCode == Result.Success || createData.ResultCode == Result.DuplicateNotAllowed)
            {
                ProceedToConnectLogin(connectInterface);
            }
            else
            {
                Debug.LogError($"Failed Generate DeviceID : {createData.ResultCode}");
            }
        });
    }

    private void ProceedToConnectLogin(ConnectInterface connectInterface)
    {
        var loginOptions = new LoginOptions
        {
            Credentials = new Credentials
            {
                Token = null,
                Type = ExternalCredentialType.DeviceidAccessToken
            },

            UserLoginInfo = new UserLoginInfo
            {
                DisplayName = $"Player {Random.Range(10000, 99999)}"
            }
        };

        connectInterface.Login(ref loginOptions, null, (ref LoginCallbackInfo loginData) =>
        {
            if (loginData.ResultCode == Result.Success)
            {
                Debug.Log($"Login Successed My ID : {loginData.LocalUserId}");
                myUserId = loginData.LocalUserId;
            }
            else
            {
                Debug.LogError($"Login Failed : {loginData.ResultCode}");
            }
        });
    }
}