using UnityEngine;

public class PlayerObject : MonoBehaviour
{
    [SerializeField] private float lerpSpeed = 7f; 
    
    public Vector2 playerTargetPosition;

    [SerializeField] private Animator animator;
    private float originalScaleX;

    private void Awake()
    {
        originalScaleX = Mathf.Abs(transform.localScale.x);
    }

    private void Update()
    {
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

        if (isMoving)
        {
            float sign = moveDeltaX > 0 ? 1f : -1f;
            transform.localScale = new Vector3(originalScaleX * sign, transform.localScale.y, transform.localScale.z);
        }
    }
}