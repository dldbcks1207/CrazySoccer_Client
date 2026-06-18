using System;
using System.Collections; // 필수 추가
using TMPro;
using UnityEngine;

public class NoticeManager : MonoBehaviour
{
    static public NoticeManager Instance;

    [SerializeField] private Animator animator;
    public TextMeshProUGUI textObj;

    private void Awake()
    {
        Instance = this;
    }

    public void Up(Action endEvent = null)
    {
        animator.SetTrigger("Up");
        StartCoroutine(ExecuteAfterDelay(0.6f, endEvent));
    }

    public void Down(string text = null)
    {
        if (text != null)
            textObj.text = text;
        animator.SetTrigger("Down");
    }

    private IEnumerator ExecuteAfterDelay(float delay, Action endEvent)
    {
        yield return new WaitForSeconds(delay);

        endEvent?.Invoke();
    }
}