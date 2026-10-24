using System.Collections.Generic;
using UnityEngine;

public class Perk
{
    public Sprite Image => data.Image;

    // Chủ sở hữu: null = perk của Hero (như trước). Có giá trị = perk của Enemy đó.
    public CombatantView OwnerView => owner;
    public bool HasOwner => hasOwner;
    public PerkData Data => data;

    private readonly PerkData data;
    private readonly PerkCondition condition;
    private readonly AutoTargetEffect effect;
    private readonly CombatantView owner;
    private readonly bool hasOwner;

    public Perk(PerkData perkData, CombatantView ownerView = null)
    {
        data = perkData;
        condition = data.PerkCondition;
        effect = data.AutoTargetEffect;
        owner = ownerView;
        hasOwner = ownerView != null; // lưu riêng vì owner có thể bị Destroy (== null) khi địch chết
    }

    public void OnAdd()
    {
        condition.SubscribeCondition(Reaction);
    }
    public void OnRemove()
    {
        condition.UnsubscribeCondition(Reaction);
    }

    private void Reaction(GameAction gameAction)
    {
        // Chủ perk (Enemy) đã chết hoặc đã bị xóa thì perk không còn tác dụng
        if (hasOwner && (owner == null || owner.CurrentHealth <= 0)) return;

        // Người "sở hữu" perk chính là caster của effect (Hero nếu perk không có chủ)
        CombatantView caster = hasOwner ? owner : HeroSystem.Instance.HeroView;

        if (!condition.SubConditionIsMet(gameAction)) return;
        if (!condition.OwnerMatches(gameAction, caster)) return;

        List<CombatantView> targets = new();
        if (data.UseActionCasterAsTarget && gameAction is IHaveCaster haveCaster)
        {
            targets.Add(haveCaster.Caster);
        }
        if (data.UseOwnerAsTarget)
        {
            targets.Add(caster);
        }
        if (data.UseAutoTarget)
        {
            targets.AddRange(effect.TargetMode.GetTargets());
        }
        GameAction perkEffectAction = effect.Effect.GetGameAction(targets, caster);
        ActionSystem.Instance.AddReaction(perkEffectAction);
    }
}
