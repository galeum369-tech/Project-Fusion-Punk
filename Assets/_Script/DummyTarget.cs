using UnityEngine;

// MonoBehaviour와 IInteractable 규격을 동시에 상속
public class DummyTarget : MonoBehaviour, IInteractable
{
    [SerializeField] private string targetName = "깡통 보스 흔적";

    // 인터페이스에 약속된 작동 함수를 여기서 구체화함
    public void Interact()
    {
        Debug.Log($"[{targetName}]에서 엔진 부품을 회수했습니다!");
        // v0.0.1 확인용: 작동 후 즉시 파괴
        Destroy(gameObject);
    }
}