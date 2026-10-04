using UnityEngine;

[RequireComponent(typeof(CapsuleCollider))]
public class CatHeadDamage : MonoBehaviour
{
    [SerializeField]
    private float _damage = 10f;

    [SerializeField]
    private float _damageCooldown = 5f;

    private float _lastDamageTime = -999f;

    private void Awake()
    {
        GetComponent<CapsuleCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other.GetComponent<PlayerHealth>());
    }

    public void TryDamage(PlayerHealth playerHealth)
    {
        if (playerHealth == null || Time.time - _lastDamageTime < _damageCooldown)
            return;

        _lastDamageTime = Time.time;
        playerHealth.TakeDamage(_damage, CauseDeath.Cat);
    }
}
