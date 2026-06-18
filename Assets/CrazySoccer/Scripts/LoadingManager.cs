using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    [Header("UI Canvas References")]
    [SerializeField] private GameObject loadingObj;
    [SerializeField] private Image spinner;
    [SerializeField] private TextMeshProUGUI progressText;

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
        if (spinner != null) spinner.fillAmount = value;
        if (progressText != null) progressText.text = $"{(value * 100f):F0}%";
    }
}