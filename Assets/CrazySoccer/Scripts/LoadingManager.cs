using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingManager : MonoBehaviour
{
    // 어디서나 쉽게 접근할 수 있도록 싱글톤 구현
    public static LoadingManager Instance { get; private set; }

    [Header("UI Canvas References")]
    [SerializeField] private GameObject loadingObj; // 로딩창 최상위 오브젝트
    [SerializeField] private Image spinner;       // 로딩 게이지 슬라이더
    [SerializeField] private TextMeshProUGUI progressText;         // 로딩 퍼센트 텍스트

    private void Awake()
    {
        Instance = this;
        HideLoading();
    }

    public void ShowLoading()
    {
        if (loadingObj != null) loadingObj.SetActive(true);
        UpdateProgress(0f);
    }

    public void HideLoading()
    {
        if (loadingObj != null) loadingObj.SetActive(false);
    }

    public void UpdateProgress(float value)
    {
        // value는 0.0 ~ 1.0 사이의 값
        if (spinner != null) spinner.fillAmount = value;
        if (progressText != null) progressText.text = $"{(value * 100f):F0}%";
    }
}