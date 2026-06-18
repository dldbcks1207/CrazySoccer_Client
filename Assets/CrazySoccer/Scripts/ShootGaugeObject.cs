using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public class ShootGaugeObject : MonoBehaviour
{
    private byte currentGauge = 0; // byte로 변경
    public byte CurrentGauge => currentGauge;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image gaugeImage;
    [SerializeField] private float fillSpeed = 100f;
    [SerializeField] private float fadeDuration = 0.5f;
    public Transform followTarget;

    private RectTransform rectTransform;
    private Coroutine gaugeCoroutine;
    private float internalGauge = 0f; // 계산을 위한 보조 변수

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void StartGauge(Action<byte> onComplete) // Action 파라미터도 byte로 변경
    {
        StopGaugeInternal();

        if (canvasGroup != null) canvasGroup.alpha = 1f;

        currentGauge = 0;
        internalGauge = 0f;
        gaugeCoroutine = StartCoroutine(GaugeRoutine(onComplete));
    }

    public byte StopGauge() // 반환 타입 byte로 변경
    {
        StopGaugeInternal();
        StartCoroutine(FadeOutRoutine());
        return currentGauge;
    }

    private void StopGaugeInternal()
    {
        if (gaugeCoroutine != null)
        {
            StopCoroutine(gaugeCoroutine);
            gaugeCoroutine = null;
        }
    }

    private IEnumerator GaugeRoutine(Action<byte> onComplete)
    {
        while (currentGauge < 100)
        {
            internalGauge += fillSpeed * Time.deltaTime;
            currentGauge = (byte)Mathf.Clamp(Mathf.RoundToInt(internalGauge), 0, 100);

            UpdateUI();

            if (currentGauge >= 100)
            {
                onComplete?.Invoke(currentGauge);
                StartCoroutine(FadeOutRoutine());
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator FadeOutRoutine()
    {
        if (canvasGroup == null) { Destroy(gameObject); yield break; }

        yield return new WaitForSeconds(0.5f);

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        Destroy(gameObject);
    }

    private void UpdateUI()
    {
        if (gaugeImage != null)
        {
            // fillAmount는 0~1 사이의 float이므로 여기서 변환
            gaugeImage.fillAmount = currentGauge / 100f;
        }
    }

    private void Update()
    {
        if (followTarget != null)
            rectTransform.anchoredPosition = new Vector2(followTarget.transform.position.x, followTarget.transform.position.y + 1.1f);
    }
}