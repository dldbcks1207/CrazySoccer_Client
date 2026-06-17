using UnityEngine;
using UnityEngine.UI;

public class BallIndicator : MonoBehaviour
{
    public Transform targetBall;
    public RectTransform arrowUI;
    public CanvasGroup tvCanvasGroup;

    public float padding = 50f;
    public float fadeTime = 0.5f;
    public float hideDelay = 5.0f;
    public float hoverOffset = 80f; 

    private Camera mainCam;
    private float targetAlpha = 0f;
    private float delayTimer = 0f;
    private CanvasGroup arrowCanvasGroup;

    void Start()
    {
        mainCam = Camera.main;
        if (arrowUI != null)
        {
            arrowCanvasGroup = arrowUI.GetComponent<CanvasGroup>();
            if (arrowCanvasGroup == null)
            {
                arrowCanvasGroup = arrowUI.gameObject.AddComponent<CanvasGroup>();
            }
            arrowCanvasGroup.alpha = 0f;
            arrowUI.gameObject.SetActive(false);
        }
        if (tvCanvasGroup != null) tvCanvasGroup.alpha = 0f;
    }

    void Update()
    {
        if (targetBall == null || arrowUI == null || tvCanvasGroup == null || arrowCanvasGroup == null) return;

        Vector3 screenPos = mainCam.WorldToScreenPoint(targetBall.position);
        bool isOffScreen = screenPos.x <= 0 || screenPos.x >= Screen.width ||
                           screenPos.y <= 0 || screenPos.y >= Screen.height;

        if (isOffScreen)
        {
            targetAlpha = 1f;
            delayTimer = hideDelay;
        }
        else
        {
            if (delayTimer > 0f)
            {
                delayTimer -= Time.deltaTime;
                targetAlpha = 1f;
            }
            else
            {
                targetAlpha = 0f;
            }
        }

        if (tvCanvasGroup.alpha > 0f || targetAlpha > 0f)
        {
            if (!arrowUI.gameObject.activeSelf) arrowUI.gameObject.SetActive(true);

            if (isOffScreen)
            {
                Vector3 clampedPos = screenPos;
                clampedPos.x = Mathf.Clamp(clampedPos.x, padding, Screen.width - padding);
                clampedPos.y = Mathf.Clamp(clampedPos.y, padding, Screen.height - padding);
                clampedPos.z = 0f;
                arrowUI.position = clampedPos;

                Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
                Vector3 direction = (screenPos - screenCenter).normalized;

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                arrowUI.rotation = Quaternion.Euler(0f, 0f, angle);
            }
            else
            {
                Vector3 hoverPos = screenPos;
                hoverPos.y += hoverOffset;
                hoverPos.z = 0f;
                arrowUI.position = hoverPos;

                arrowUI.rotation = Quaternion.Euler(0f, 0f, -90f);
            }
        }

        if (tvCanvasGroup.alpha != targetAlpha)
        {
            float currentAlpha = Mathf.MoveTowards(tvCanvasGroup.alpha, targetAlpha, (1f / fadeTime) * Time.deltaTime);
            
            tvCanvasGroup.alpha = currentAlpha;
            arrowCanvasGroup.alpha = currentAlpha;

            if (currentAlpha <= 0f && targetAlpha == 0f && arrowUI.gameObject.activeSelf)
            {
                arrowUI.gameObject.SetActive(false);
            }
        }
    }
}