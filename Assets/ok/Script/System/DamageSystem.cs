using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageSystem : MonoBehaviour
{
    void OnEnable()
    {
        ActionSystem.AttachPerformer<DealDamageGA>(DealDamagePerformer);
    }
    void OnDisable()
    {
        ActionSystem.DetachPerformer<DealDamageGA>();
    }

    [SerializeField] private GameObject damageVFX;

    // Công thức sát thương tập trung ở một chỗ: Strength -> Weak -> Vulnerable
    public static int CalculateDamage(int baseAmount, CombatantView attacker, CombatantView target)
    {
        float damage = baseAmount;

        if (attacker != null)
        {
            damage += attacker.GetStatusEffectStacks(StatusEffectType.STRENGTH);
            if (attacker.GetStatusEffectStacks(StatusEffectType.WEAK) > 0)
                damage *= 0.75f;
        }
        if (target != null && target.GetStatusEffectStacks(StatusEffectType.VULNERABLE) > 0)
            damage *= 1.5f;

        return Mathf.Max(0, Mathf.FloorToInt(damage));
    }

    private IEnumerator DealDamagePerformer(DealDamageGA dealDamageGA)
    {
        foreach (var target in dealDamageGA.Targets)
        {
            // Chỉ bỏ qua mục tiêu đã bị Destroy (vd: đã chết và bị xóa khỏi bàn)
            if (target == null) continue;

            int finalDamage = CalculateDamage(dealDamageGA.Amount, dealDamageGA.Caster, target);
            target.Damage(finalDamage);

            GameObject vfx = Instantiate(damageVFX, target.transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
            yield return new WaitForSeconds(0.15f);

            if (target != null && target.CurrentHealth <= 0)
            {
                if (target is EnemyView enemyView)
                {
                    KillEnemyGA killEnemyGA = new(enemyView);
                    ActionSystem.Instance.AddReaction(killEnemyGA);
                }
                else
                {
                    // Do some game over logic here
                }
            }
        }
    }
}
