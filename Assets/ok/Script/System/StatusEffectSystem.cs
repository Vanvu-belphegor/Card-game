using System.Collections;
using UnityEngine;

public class StatusEffectSystem : MonoBehaviour
{
    private void OnEnable()
    {
        ActionSystem.AttachPerformer<AddStatusEffectGA>(AddStatusEffectPerformer);
        ActionSystem.AttachPerformer<DecayStatusEffectsGA>(DecayStatusEffectsPerformer);
    }
    private void OnDisable()
    {
        ActionSystem.DetachPerformer<AddStatusEffectGA>();
        ActionSystem.DetachPerformer<DecayStatusEffectsGA>();
    }

    private IEnumerator AddStatusEffectPerformer(AddStatusEffectGA addStatusEffectGA)
    {
        foreach (var target in addStatusEffectGA.Targets)
        {
            target.AddStatusEffect(addStatusEffectGA.StatusEffectType, addStatusEffectGA.StackCount);
            yield return null; // ADD VFX for adding status effects
        }
    }

    private IEnumerator DecayStatusEffectsPerformer(DecayStatusEffectsGA decayGA)
    {
        CombatantView target = decayGA.Target;
        if (target == null) yield break;

        if (target.GetStatusEffectStacks(StatusEffectType.WEAK) > 0)
            target.RemoveStatusEffect(StatusEffectType.WEAK, 1);
        if (target.GetStatusEffectStacks(StatusEffectType.VULNERABLE) > 0)
            target.RemoveStatusEffect(StatusEffectType.VULNERABLE, 1);

        yield return null;
    }
}
