using UnityEngine;

public class TransitionManager : MonoBehaviour
{
    static public TransitionManager Instance;
    [SerializeField] Animator animator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
    }

    public void Transition(bool isEnd)
    {
        if (isEnd)
            animator.SetTrigger("EndTransition");
        else
            animator.SetTrigger("Transition");
    }
}
