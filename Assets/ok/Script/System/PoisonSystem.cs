using System.Collections;
using UnityEngine;

public class PoisonSystem : MonoBehaviour
{
    [SerializeField] private GameObject poisonVFX;

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<ApplyPoisonGA>(ApplyPoisonPerformer);
    }
    private void OnDisable()
    {
        ActionSystem.DetachPerformer<ApplyPoisonGA>();
    }

    private IEnumerator ApplyPoisonPerformer(ApplyPoisonGA applyPoisonGA)
    {
        CombatantView target = applyPoisonGA.Target;
        if (target == null) yield break;

        if (poisonVFX != null)
        {
            GameObject vfx = Instantiate(poisonVFX, target.transform.position, Quaternion.identity);
            Destroy(vfx, 2f); // tránh rò rỉ object
        }

        target.Damage(applyPoisonGA.PoisonDamage, ignoreArmor: true);
        target.RemoveStatusEffect(StatusEffectType.POISON, 1);
        yield return new WaitForSeconds(0.5f);

        // Poison có thể giết mục tiêu -> phải xử lý chết (Burn hiện chưa làm bước này)
        if (target.CurrentHealth <= 0)
        {
            if (target is EnemyView enemyView)
            {
                ActionSystem.Instance.AddReaction(new KillEnemyGA(enemyView));
            }
            else
            {
                // Game over logic cho Hero (giống DamageSystem)
            }
        }
    }
}
