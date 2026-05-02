using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

// 유니티 에디터에서 우클릭으로 이 중계기 데이터 파일을 생성할 수 있게 해줌
[CreateAssetMenu(fileName = "InputReader", menuName = "FusionPunk/Input/Input Reader")]
public class InputReader : ScriptableObject, PlayerControls.IPlayerActions
{
    // 캐릭터 컨트롤러가 수신할 무전(이벤트)들
    public event UnityAction<Vector2> MoveEvent;
    public event UnityAction<Vector2> LookEvent;
    public event UnityAction DashEvent;
    public event UnityAction InteractEvent;
    public event UnityAction InventoryEvent;

    private PlayerControls _playerControls;

    // 중계기 전원 ON
    private void OnEnable()
    {
        if (_playerControls == null)
        {
            _playerControls = new PlayerControls();
            _playerControls.Player.SetCallbacks(this); // 내가 신호를 직접 받겠다고 선언
        }
        _playerControls.Player.Enable();
    }

    // 중계기 전원 OFF (메모리 누수 방지)
    private void OnDisable()
    {
        _playerControls.Player.Disable();
    }

    // --- 아래는 Input System이 신호를 보낼 때 자동으로 실행되는 콜백 함수들 ---

    public void OnMove(InputAction.CallbackContext context)
    {
        // 이동 신호(Vector2)를 받아서 뿌림
        MoveEvent?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        // 마우스 위치(Vector2)를 받아서 뿌림
        LookEvent?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        // 스페이스바를 '누르는 순간(Performed)'에만 신호 발생
        if (context.phase == InputActionPhase.Performed)
            DashEvent?.Invoke();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
            InteractEvent?.Invoke();
    }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
            InventoryEvent?.Invoke();
    }
}