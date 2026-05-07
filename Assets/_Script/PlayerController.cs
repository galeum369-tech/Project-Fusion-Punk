using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))] // 이 스크립트를 넣으면 물리 엔진(Rigidbody2D)이 자동 장착됨
public class PlayerController : NetworkBehaviour
{
    [Header("조종간 연결 (References)")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Camera mainCamera; // 마우스 좌표 변환용 렌즈

    [Header("시각 부품 (Visuals)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite spriteUp;    // 마우스가 위 (뒷모습)
    [SerializeField] private Sprite spriteDown;  // 마우스가 아래 (앞모습)
    [SerializeField] private Sprite spriteRight; // 마우스가 오른쪽 (좌측은 이걸 반전해서 씀)

    [Header("기체 스펙 (Movement Settings)")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 10f; // 가속도 (엔진 점화 속도)
    [SerializeField] private float deceleration = 10f; // 감속도 (바닥 마찰력/브레이크)

    [Header("회피 및 안전 장치 (Dash & Safety)")]
    [SerializeField] private float dashSpeedMultiplier = 3f; // 구르기 시 속도 증가량
    [SerializeField] private float dashDuration = 0.2f;      // 구르기 체공 시간
    [SerializeField] private float dashCooldown = 0.5f;      // 대시 쿨다운 (과부하 방지)
    [SerializeField] private LayerMask voidLayer;            // 구덩이 판별용 레이어
    [SerializeField] private LayerMask groundLayer;          // 땅 판별용 레이어

    [Header("아이템 습득 (Auto Pickup)")]
    [SerializeField] private float pickupRadius = 1.5f;      // 아이템을 빨아들이는 반경
    [SerializeField] private LayerMask itemLayer;            // 아이템 전용 레이어

    private Rigidbody2D rb;
    private Vector2 currentMoveInput;
    private Vector2 currentMouseScreenPosition; // 모니터 상의 마우스 픽셀 위치
    private Vector2 currentVelocity;

    private Vector2 lastSafePosition; // 마지막으로 안전했던 땅의 좌표
    private float nextDashTime = 0f;  // 다음 대시 가능 시간
    private int normalLayer;
    private int dashLayer;
    private bool isDashing = false;

    // Start() 대신 NGO 멀티플레이 환경에서 생성될 때 호출되는 엔진 시동 함수
    public override void OnNetworkSpawn()
    {
        // [핵심 방어막] 오직 '내 PC의 캐릭터'만 리모컨 주파수를 수신한다!
        if (!IsOwner) return;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        // 물리적인 회전을 완전히 잠가서 콜라이더가 엇나가는 것을 방지
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (mainCamera == null) mainCamera = Camera.main;

        // 레이어 번호 캐싱
        normalLayer = LayerMask.NameToLayer("Player");
        dashLayer = LayerMask.NameToLayer("Player_Dash");

        // 리모컨(InputReader) 무전 연결
        inputReader.MoveEvent += OnMove;
        inputReader.LookEvent += OnLook;
        inputReader.DashEvent += OnDash;

        // 카메라 추적 권한 가져오기
        CinemachineCamera vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam != null)
        {
            vcam.Follow = transform;
        }

        // 시작 위치를 첫 번째 안전 좌표로 설정
        lastSafePosition = transform.position;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        // 기체가 파괴되거나 서버에서 나갈 때 무전 연결 해제 (합선/메모리 누수 방지)
        inputReader.MoveEvent -= OnMove;
        inputReader.LookEvent -= OnLook;
        inputReader.DashEvent -= OnDash;
    }

    // InputReader에서 "WASD 눌림!" 무전이 오면 실행
    private void OnMove(Vector2 moveInput) => currentMoveInput = moveInput.normalized;

    // InputReader에서 "마우스 움직임!" 무전이 오면 실행
    private void OnLook(Vector2 mousePosition) => currentMouseScreenPosition = mousePosition;

    // 대시 무전을 받았을 때 실행
    private void OnDash()
    {
        // 쿨타임 중이 아니며, 이동 키를 누르고 있을 때만 작동
        if (!isDashing && Time.time >= nextDashTime && currentMoveInput != Vector2.zero)
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

        // [핵심] 대시 종료 후 추락 판정
        CheckForPitfall();

        // 3. 기어 복구: 다시 구덩이에 막히는 일반 레이어로 복귀
        gameObject.layer = normalLayer;
        isDashing = false;

        // 쿨다운 타이머 세팅
        nextDashTime = Time.time + dashCooldown;
    }

    private void CheckForPitfall()
    {
        // 발밑에 Void 레이어가 있는지 확인
        Collider2D hitVoid = Physics2D.OverlapPoint(transform.position, voidLayer);

        if (hitVoid != null)
        {
            Debug.Log("<color=red>[경고]</color> 구덩이 추락! 안전 좌표로 견인합니다.");

            // 물리 속도 초기화 및 강제 견인
            rb.linearVelocity = Vector2.zero;
            currentVelocity = Vector2.zero;
            transform.position = lastSafePosition;
        }
    }

    // 물리 연산은 반드시 FixedUpdate에서 처리
    private void FixedUpdate()
    {
        if (!IsOwner) return; // 남의 캐릭터 물리 연산을 내 PC가 대신 해주지 않음

        HandleMovement();
        HandleRotation();
        HandleItemPickup(); // 아이템 자동 습득 로직 추가

        // 대시 중이 아닐 때만 안전 좌표 기록 (발밑이 Ground일 때만)
        if (!isDashing)
        {
            Collider2D hitGround = Physics2D.OverlapPoint(transform.position, groundLayer);
            if (hitGround != null)
            {
                lastSafePosition = transform.position;
            }
        }
    }

    private void HandleMovement()
    {
        // 구르기 중이면 목표 속도를 증폭시킴
        float currentTargetSpeed = isDashing ? moveSpeed * dashSpeedMultiplier : moveSpeed;
        Vector2 targetVelocity = currentMoveInput * currentTargetSpeed;

        // 묵직한 엑소 슈트 느낌을 내기 위한 선형 보간(Lerp) 가감속 로직
        float accelRate = (currentMoveInput.magnitude > 0.01f) ? acceleration : deceleration;
        currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, accelRate * Time.fixedDeltaTime);

        // 유니티 6 권장 API인 linearVelocity 사용
        rb.linearVelocity = currentVelocity;
    }

    private void HandleRotation()
    {
        // 구르기 중에는 강제로 시선이 바뀌지 않도록 락을 걺 (액션의 무게감을 위함)
        if (isDashing) return;

        // [핵심] 렌즈(카메라)가 이동 중이므로, 매 프레임마다 픽셀 좌표를 월드 좌표로 실시간 재계산!
        Vector2 mouseWorldPosition = mainCamera.ScreenToWorldPoint(currentMouseScreenPosition);

        // 마우스 방향으로 각도 계산
        Vector2 lookDir = mouseWorldPosition - rb.position;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;

        // [핵심] 4방향 섹터 판별 및 스프라이트 교체 로직
        if (angle > -45f && angle <= 45f)
        {
            // 우측 섹터
            spriteRenderer.sprite = spriteRight;
            spriteRenderer.flipX = false;
        }
        else if (angle > 45f && angle <= 135f)
        {
            // 상단 섹터 (뒷모습)
            spriteRenderer.sprite = spriteUp;
        }
        else if (angle > 135f || angle <= -135f)
        {
            // 좌측 섹터 (우측 이미지를 반전시켜서 사용)
            spriteRenderer.sprite = spriteRight;
            spriteRenderer.flipX = true;
        }
        else if (angle > -135f && angle <= -45f)
        {
            // 하단 섹터 (앞모습)
            spriteRenderer.sprite = spriteDown;
        }
    }

    // 아이템 자동 습득 로직
    private void HandleItemPickup()
    {
        // 기체 주변의 아이템 레이어를 가진 물체들을 수색
        Collider2D[] items = Physics2D.OverlapCircleAll(transform.position, pickupRadius, itemLayer);

        foreach (var item in items)
        {
            // 아이템에 습득 가능 인터페이스나 컴포넌트가 있는지 확인 후 호출
            // 예: item.GetComponent<IItem>()?.OnPickup();
            Debug.Log($"[아이템 습득] {item.name}을(를) 자동 획득했습니다.");

            // 임시로 오브젝트 파괴 처리 (나중에 인벤토리 시스템과 연결)
            Destroy(item.gameObject);
        }
    }

    // 에디터에서 습득 반경을 시각적으로 확인하기 위함
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}