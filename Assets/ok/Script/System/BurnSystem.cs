using System.Collections;
using UnityEngine;

public class BurnSystem : MonoBehaviour
{
    [SerializeField] private GameObject burnVFX;
   
    private void OnEnable()
    {
        ActionSystem.AttachPerformer<ApplyBurnGA>(ApplyBurnPerformer);
    }
    private void OnDisable()
    {
        ActionSystem.DetachPerformer<ApplyBurnGA>();
    }
    private IEnumerator ApplyBurnPerformer(ApplyBurnGA applyBurnGA)
    {
        CombatantView target = applyBurnGA.Target;
        Instantiate(burnVFX, target.transform.position, Quaternion.identity);
        target.Damage(applyBurnGA.BurnDamage);
        target.RemoveStatusEffect(StatusEffectType.BURN, 1);
        yield return new WaitForSeconds(1f);
    }
}