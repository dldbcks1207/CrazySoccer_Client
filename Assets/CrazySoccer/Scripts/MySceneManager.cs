using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MySceneManager : MonoBehaviour
{
    public static MySceneManager Instance { get; private set; }

    [Header("Fake Loading Settings")]
    [Tooltip("로딩바가 차오르는 속도 (높을수록 빠름)")]
    [SerializeField] private float fakeLoadingSpeed = 2f; 

    private void Awake()
    {
        Application.targetFrameRate = 120;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void ChangeScene(string sceneName, bool isAdditive = false, Action onSceneLoaded = null)
    {
        LoadSceneMode mode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
        StartCoroutine(LoadSceneAsyncCoroutine(sceneName, mode, onSceneLoaded));
    }

    private IEnumerator LoadSceneAsyncCoroutine(string sceneName, LoadSceneMode mode, Action onSceneLoaded)
    {
        // 1. LoadingManager에게 로딩창을 켜라고 명령
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.ShowLoading();
        }

        // 2. 비동기 씬 로드 시작
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, mode);
        asyncLoad.allowSceneActivation = false;

        float currentProgress = 0f;

        // 3. 페이크 로딩 진행도 체크 루프
        while (!asyncLoad.isDone)
        {
            // 유니티 내부 로딩은 0.9f에서 멈추므로 이를 1.0f으로 환산한 진짜 목표치
            float targetProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            // currentProgress를 targetProgress를 향해 매 프레임 부드럽게 증가시킵니다.
            currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, Time.deltaTime * fakeLoadingSpeed);

            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.UpdateProgress(currentProgress);
            }

            // [핵심] 실제 로딩이 90% 이상 끝났고, 화면에 보여지는 페이크 로딩바도 100%(1.0f)에 도달했을 때만 씬을 전환합니다.
            if (asyncLoad.progress >= 0.9f && Mathf.Approximately(currentProgress, 1.0f))
            {
                yield return new WaitForSeconds(0.2f); // 연출을 위한 최종 대기 시간
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }

        // 4. 씬 전환이 완전히 끝났으므로 로딩창을 숨김 명령
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.HideLoading();
        }

        // 5. 씬전환 완료 후 콜백 실행
        if (onSceneLoaded != null)
        {
            onSceneLoaded.Invoke();
        }
    }
}