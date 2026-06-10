using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCol;
    private Animator anim;
    private float moveInput;

    [Header("Ground Check Settings")]
    public LayerMask groundLayer;
    private bool isGrounded;

    [Header("Jump Buffer Settings")]
    [SerializeField] private float jumpBufferTime = 0.2f; // 선입력을 인정해줄 시간 (0.2초가 가장 적당합니다)
    private float jumpBufferCounter;                      // 선입력 타이머 카운터

    private InputSystem_Actions controls;
    private Vector3 defaultScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCol = GetComponent<BoxCollider2D>();
        anim = GetComponent<Animator>();

        controls = new InputSystem_Actions();

        // 점프 키를 누르면 바로 Jump() 함수를 실행하는 대신, '점프 입력이 들어왔다'고 타이머를 채워줍니다.
        controls.Player.Jump.performed += ctx => OnJumpPressed();
        defaultScale = transform.localScale;
    }

    void OnEnable()
    {
        controls.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void Update()
    {
        // 1. 실시간 좌우 이동 입력값 읽어오기
        moveInput = controls.Player.Move.ReadValue<Vector2>().x;

        // 2. 애니메이션 상태 업데이트
        bool isMoving = Mathf.Abs(moveInput) > 0.01f;
        anim.SetBool("isMove", isMoving);

        // 3. BoxCast를 이용한 바닥 감지
        RaycastHit2D hit = Physics2D.BoxCast(boxCol.bounds.center, boxCol.bounds.size, 0f, Vector2.down, 0.1f, groundLayer);
        isGrounded = hit.collider != null;

        // 4. 점프 버퍼(선입력) 타이머 감소 처리
        if (jumpBufferCounter > 0)
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // 5. 실행 조건 체크: 선입력 타이머가 남아있고 + 바닥에 닿아있다면 즉시 점프!
        if (jumpBufferCounter > 0 && isGrounded)
        {
            ExecuteJump();
        }

        // 6. 캐릭터 좌우 반전
        if (moveInput > 0)
        {
            transform.localScale = new Vector3(defaultScale.x, defaultScale.y, defaultScale.z);
        }
        else if (moveInput < 0)
        {
            transform.localScale = new Vector3(-0.1f * defaultScale.x / 0.1f, defaultScale.y, defaultScale.z); // 간결하게 부호만 변경
            transform.localScale = new Vector3(-defaultScale.x, defaultScale.y, defaultScale.z);
        }
    }

    void FixedUpdate()
    {
        // 7. 물리 이동 적용
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    // 점프 키가 눌렸을 때 호출되는 함수
    private void OnJumpPressed()
    {
        // 점프 버퍼 타이머를 가득 채웁니다. (이제 0.2초 동안은 공중에 떠 있어도 점프 명령을 기억합니다)
        jumpBufferCounter = jumpBufferTime;
    }

    // 실제 점프를 수행하는 함수
    private void ExecuteJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        
        // 점프를 했으므로 버퍼 타이머를 즉시 0으로 초기화하여 연속 점프 버그를 방지합니다.
        jumpBufferCounter = 0f;
    }
}