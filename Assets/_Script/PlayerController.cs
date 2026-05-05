using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using System.Collections; // 코루틴 사용을 위해 추가

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : NetworkBehaviour
{
    [Header("조종간 연결 (References)")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Camera mainCamera;

    [Header("기체 스펙 (Movement Settings)")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 10f;

    [Header("회피 기동 스펙 (Dash Settings)")]
    [SerializeField] private float dashSpeedMultiplier = 3f; // 구르기 시 속도 증가량
    [SerializeField] private float dashDuration = 0.2f;      // 구르기 체공 시간

    private Rigidbody2D rb;
    private Vector2 currentMoveInput;
    private Vector2 currentMouseScreenPosition;
    private Vector2 currentVelocity;

    // 레이어 변속기용 변수
    private int normalLayer;
    private int dashLayer;
    private bool isDashing = false;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        if (mainCamera == null) mainCamera = Camera.main;

        // 레이어 번호 캐싱
        normalLayer = LayerMask.NameToLayer("Player");
        dashLayer = LayerMask.NameToLayer("Player_Dash");

        // 리모컨 무전 연결
        inputReader.MoveEvent += OnMove;
        inputReader.LookEvent += OnLook;
        inputReader.DashEvent += OnDash; // 대시 이벤트 구독 추가

        CinemachineCamera vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam != null) vcam.Follow = transform;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        inputReader.MoveEvent -= OnMove;
        inputReader.LookEvent -= OnLook;
        inputReader.DashEvent -= OnDash; // 대시 이벤트 해제
    }

    private void OnMove(Vector2 moveInput) => currentMoveInput = moveInput.normalized;
    private void OnLook(Vector2 mousePosition) => currentMouseScreenPosition = mousePosition;

    // 대시 무전을 받았을 때 실행
    private void OnDash()
    {
        // 쿨타임 중이 아니며, 이동 키를 누르고 있을 때만 작동
        if (!isDashing && currentMoveInput != Vector2.zero)
        {
            StartCoroutine(DashRoutine());
        }
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;

        // 1. 레이어 변속: 구덩이(Void)를 무시하는 레이어로 전환
        gameObject.layer = dashLayer;

        // 2. 지속 시간 대기
        yield return new WaitForSeconds(dashDuration);

        // 3. 기어 복구: 다시 구덩이에 막히는 일반 레이어로 복귀
        gameObject.layer = normalLayer;
        isDashing = false;
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        // 구르기 중이면 목표 속도를 증폭시킴
        float currentTargetSpeed = isDashing ? moveSpeed * dashSpeedMultiplier : moveSpeed;
        Vector2 targetVelocity = currentMoveInput * currentTargetSpeed;

        float accelRate = (currentMoveInput.magnitude > 0.01f) ? acceleration : deceleration;
        currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, accelRate * Time.fixedDeltaTime);

        rb.linearVelocity = currentVelocity; // 유니티 6 권장 API[cite: 3]
    }

    private void HandleRotation()
    {
        // 구르기 중에는 마우스 쪽으로 강제로 회전하지 않도록 락을 걸 수도 있음 (선택 사항)
        if (isDashing) return;

        Vector2 mouseWorldPosition = mainCamera.ScreenToWorldPoint(currentMouseScreenPosition);
        Vector2 lookDir = mouseWorldPosition - rb.position;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;

        rb.rotation = angle - 90f;
    }
}