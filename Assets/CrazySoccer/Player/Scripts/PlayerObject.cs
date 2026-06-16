using UnityEngine;

public class PlayerObject : MonoBehaviour
{
    [SerializeField] private float lerpSpeed = 15f;
    public Vector2 playerTargetPosition;

    [SerializeField] private Animator animator;
    private float originalScaleX;

    private void Awake()
    {
        originalScaleX = Mathf.Abs(transform.localScale.x);
    }

    private void Update()
    {
        float moveDeltaX = playerTargetPosition.x - transform.position.x;
        transform.position = Vector2.Lerp(transform.position, playerTargetPosition, Time.deltaTime * lerpSpeed);
        bool isMoving = Mathf.Abs(moveDeltaX) > 0.1f;
        
        if (animator != null)
        {
            animator.SetBool("isMove", isMoving);
        }

        if (isMoving)
        {
            float sign = moveDeltaX > 0 ? 1f : -1f;
            transform.localScale = new Vector3(originalScaleX * sign, transform.localScale.y, transform.localScale.z);
        }
    }
}
