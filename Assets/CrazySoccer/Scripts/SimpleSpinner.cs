using UnityEngine;

public class SimpleSpinner : MonoBehaviour
{
    [Tooltip("초당 회전 속도 (양수는 시계 반대 방향, 음수는 시계 방향)")]
    [SerializeField] private float rotationSpeed = -200f;

    [Tooltip("2D UI인 경우 True, 일반 3D 오브젝트인 경우 False")]
    [SerializeField] private bool isUI = true;

    private void Update()
    {
        if (isUI)
        {
            // UI 컴포넌트(RectTransform)는 보통 Z축을 기준으로 회전합니다.
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }
        else
        {
            // 3D 오브젝트는 보통 Y축(위아래축)을 기준으로 회전합니다.
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
        }
    }
}