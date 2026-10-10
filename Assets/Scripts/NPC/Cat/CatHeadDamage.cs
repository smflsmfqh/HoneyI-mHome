using UnityEngine;

[RequireComponent(typeof(CapsuleCollider))]
public class CatHeadDamage : MonoBehaviour
{
    [SerializeField]
    private float _damage = 10f;

    [SerializeField]
    private float _damageCooldown = 5f;

    private float _lastDamageTime = -999f;
    private CatMovement _owner;

    private void Awake()
    {
        GetComponent<CapsuleCollider>().isTrigger = true;
        _owner = GetComponentInParent<CatMovement>();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other.GetComponent<PlayerHealth>());
    }

    public void TryDamage(PlayerHealth playerHealth)
    {
        if (playerHealth == null || Time.time - _lastDamageTime < _damageCooldown)
            return;
        // 일시정지 중에도 Eat 애니메이션은 계속 재생되므로, 공격 코루틴·머리 트리거 양쪽을 여기서 막음
        if (_owner != null && _owner.IsExternallyPaused)
            return;

        _lastDamageTime = Time.time;
        playerHealth.TakeDamage(_damage, CauseDeath.Cat);
    }
}
