using UnityEngine;

public class PlayerObject : MonoBehaviour
{
    [SerializeField] private float lerpSpeed = 7f;
    [SerializeField] private SpriteRenderer shootRange;

    public Vector2 playerTargetPosition;

    [Header("로컬 플레이어 전용")]
    public bool isLocalPlayer = false;
    public float localInputX = 0f;

    [SerializeField] private Animator animator;
    private float originalScaleX;

    private float targetShootAlpha = 0f;
    private float currentShootAlpha = 0f;
    [SerializeField] private float alphaFadeSpeed = 15f;

    private void Awake()
    {
        originalScaleX = Mathf.Abs(transform.localScale.x);
    }


    public void SetShootRangeAlpha(float alpha)
    {
        targetShootAlpha = alpha;
    }

    public void PlayAnimation(byte animNum)
    {
        if (animator != null)
        {
            if (animNum == 0)
            {
                animator.SetTrigger("Kick"); // 0번이 들어오면 킥!
            }
            else
            {
                animator.SetTrigger($"Ceremony{animNum}"); // 1,2,3번은 세리머니
            }
        }
    }

    private void Update()
    {
        if (shootRange != null)
        {
            currentShootAlpha = Mathf.Lerp(currentShootAlpha, targetShootAlpha, Time.deltaTime * alphaFadeSpeed);
            Color color = shootRange.color;
            color.a = currentShootAlpha;
            shootRange.color = color;
        }

        float distance = Vector2.Distance(transform.position, playerTargetPosition);

        if (distance > 3f)
        {
            transform.position = playerTargetPosition;
        }
        else
        {
            transform.position = Vector2.Lerp(transform.position, playerTargetPosition, Time.deltaTime * lerpSpeed);
        }
        float moveDeltaX = playerTargetPosition.x - transform.position.x;
        bool isMoving = Mathf.Abs(moveDeltaX) > 0.15f;

        if (animator != null)
        {
            animator.SetBool("isMove", isMoving);
        }

        if (isLocalPlayer)
        {
            if (Mathf.Abs(localInputX) > 0.01f)
            {
                float sign = localInputX > 0 ? 1f : -1f;
                transform.localScale = new Vector3(originalScaleX * sign, transform.localScale.y, transform.localScale.z);
            }
        }
        else
        {
            if (isMoving)
            {
                float sign = moveDeltaX > 0 ? 1f : -1f;
                transform.localScale = new Vector3(originalScaleX * sign, transform.localScale.y, transform.localScale.z);
            }
        }
    }
}