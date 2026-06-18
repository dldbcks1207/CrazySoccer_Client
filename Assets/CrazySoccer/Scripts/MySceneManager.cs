using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MySceneManager : MonoBehaviour
{
    public static MySceneManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); 
    }

    public void ChangeScene(string sceneName, bool isTransitionEnd = true, bool isAdditive = false, Action onSceneLoaded = null)
    {
        LoadSceneMode mode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
        StartCoroutine(LoadSceneAsyncCoroutine(sceneName, isTransitionEnd, mode, onSceneLoaded));
    }

    private IEnumerator LoadSceneAsyncCoroutine(string sceneName, bool isTransitionEnd, LoadSceneMode mode, Action onSceneLoaded)
    {
        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.Transition(false);

            yield return new WaitForSeconds(0.9f);
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, mode);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        if (isTransitionEnd && TransitionManager.Instance != null)
        {
            TransitionManager.Instance.Transition(true);
        }

        onSceneLoaded?.Invoke();
    }
}