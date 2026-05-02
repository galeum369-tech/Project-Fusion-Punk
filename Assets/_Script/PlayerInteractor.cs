using UnityEngine;
using Unity.Netcode;

public class PlayerInteractor : NetworkBehaviour
{
    [Header("센서 세팅")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private float interactRange = 1.5f; // 탐지 반경
    [SerializeField] private LayerMask interactableLayer; // 상호작용 레이어 필터

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        // 리모컨의 F키(Interact) 무전 수신 대기
        inputReader.InteractEvent += TryInteract;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        inputReader.InteractEvent -= TryInteract;
    }

    private void TryInteract()
    {
        // 내 주변(interactRange)에 상호작용 전용 레이어를 가진 물체가 있는지 스캔
        Collider2D col = Physics2D.OverlapCircle(transform.position, interactRange, interactableLayer);

        if (col != null)
        {
            // 찾은 물체에 220V 콘센트(IInteractable)가 달려있는지 확인
            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(); // 전류 송신! (작동)
            }
        }
    }

    // 에디터에서 센서 범위를 노란색 원으로 보기 위한 기즈모
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}