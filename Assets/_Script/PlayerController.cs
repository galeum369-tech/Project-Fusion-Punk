using Unity.Cinemachine; // 시네머신 API 접근용 (필수)
using Unity.Netcode; // NGO 멀티플레이 필수 코어
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))] // 이 스크립트를 넣으면 물리 엔진(Rigidbody2D)이 자동 장착됨
public class PlayerController : NetworkBehaviour
{
    [Header("조종간 연결 (References)")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Camera mainCamera; // 마우스 좌표 변환용 렌즈

    [Header("기체 스펙 (Movement Settings)")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 10f; // 가속도 (엔진 점화 속도)
    [SerializeField] private float deceleration = 10f; // 감속도 (바닥 마찰력/브레이크)

    private Rigidbody2D rb;
    private Vector2 currentMoveInput;
    private Vector2 currentMouseScreenPosition; // 모니터 상의 마우스 픽셀 위치
    private Vector2 currentVelocity;

    // Start() 대신 NGO 멀티플레이 환경에서 생성될 때 호출되는 엔진 시동 함수
    public override void OnNetworkSpawn()
    {
        // [핵심 방어막] 오직 '내 PC의 캐릭터'만 리모컨 주파수를 수신한다!
        if (!IsOwner) return;

        rb = GetComponent<Rigidbody2D>();

        // 쿼터뷰 가짜 3D를 위해 중력을 꺼버림
        rb.gravityScale = 0f;

        if (mainCamera == null) mainCamera = Camera.main;

        // 리모컨(InputReader) 무전 연결
        inputReader.MoveEvent += OnMove;
        inputReader.LookEvent += OnLook;

        // 카메라 추적 권한 가져오기 (인스펙터 연결 없이 맵에서 직접 수색)
        CinemachineCamera vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam != null)
        {
            // 시네머신 렌즈의 추적(Follow) 대상을 내 기체의 위치(transform)로 고정!
            vcam.Follow = transform;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        // 기체가 파괴되거나 서버에서 나갈 때 무전 연결 해제 (합선/메모리 누수 방지)
        inputReader.MoveEvent -= OnMove;
        inputReader.LookEvent -= OnLook;
    }

    // InputReader에서 "WASD 눌림!" 무전이 오면 실행
    private void OnMove(Vector2 moveInput)
    {
        // 대각선 이동 시 속도가 1.4배 빨라지는 버그를 막기 위해 정규화(normalized)
        currentMoveInput = moveInput.normalized;
    }

    // InputReader에서 "마우스 움직임!" 무전이 오면 실행
    private void OnLook(Vector2 mousePosition)
    {
        // 월드 좌표 변환을 여기서 하지 않고, 모니터 상의 X, Y 픽셀 위치만 기억해 둠!
        currentMouseScreenPosition = mousePosition;
    }

    // 물리 연산은 반드시 FixedUpdate에서 처리
    private void FixedUpdate()
    {
        if (!IsOwner) return; // 남의 캐릭터 물리 연산을 내 PC가 대신 해주지 않음

        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        // 목표 최고 속도 계산
        Vector2 targetVelocity = currentMoveInput * moveSpeed;

        // 묵직한 엑소 슈트 느낌을 내기 위한 선형 보간(Lerp) 가감속 로직
        float accelRate = (currentMoveInput.magnitude > 0.01f) ? acceleration : deceleration;
        currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, accelRate * Time.fixedDeltaTime);

        // 유니티 6 권장 API인 linearVelocity 사용 (구버전의 velocity)
        rb.linearVelocity = currentVelocity;
    }

    private void HandleRotation()
    {
        // [핵심] 렌즈(카메라)가 이동 중이므로, 매 프레임마다 픽셀 좌표를 월드 좌표로 실시간 재계산!
        Vector2 mouseWorldPosition = mainCamera.ScreenToWorldPoint(currentMouseScreenPosition);

        // 마우스 방향으로 기체 상체 회전
        Vector2 lookDir = mouseWorldPosition - rb.position;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;

        rb.rotation = angle - 90f;
    }
}