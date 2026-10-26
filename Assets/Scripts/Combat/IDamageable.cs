using UnityEngine;

public interface IDamageable
{
    Vector3 Position { get; }
    bool IsDead { get; }
    void TakeDamage(float amount);
}