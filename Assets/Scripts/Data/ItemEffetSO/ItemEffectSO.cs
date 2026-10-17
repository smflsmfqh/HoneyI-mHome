using UnityEngine;

public enum EffectParticleType
{
    None,
    HealParticle,
    SpeedBoostParticle,
    InvincibleParticle,
}

public abstract class ItemEffectSO : ScriptableObject
{
    public abstract void Apply(PlayerController player);
}
