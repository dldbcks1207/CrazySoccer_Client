using UnityEngine;

public class PlayerObject : MonoBehaviour
{
    [SerializeField] private float lerpSpeed = 15f;
    public Vector2 playerTargetPosition;
    
    private void Update()
    {
        transform.position = Vector2.Lerp(transform.position, playerTargetPosition, Time.deltaTime * lerpSpeed);
    }
}
