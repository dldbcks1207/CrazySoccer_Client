using UnityEngine;

public class SoccerBallObject : MonoBehaviour
{
    public Vector2 targetPosition;
    [SerializeField] private float lerpSpeed = 15f;
    [SerializeField] private float ballRotationSpeed = 200f;

    void Update()
    {
        Vector3 prevPos = transform.position;
        transform.position = Vector2.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
        float moveDeltaX = transform.position.x - prevPos.x;
        float rotationAmount = moveDeltaX * ballRotationSpeed;
        transform.Rotate(Vector3.forward, -rotationAmount);
    }
}
