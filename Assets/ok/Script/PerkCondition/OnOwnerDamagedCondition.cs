using System;

// Kích hoạt khi Enemy sở hữu perk này BỊ gây sát thương.
public class OnOwnerDamagedCondition : PerkCondition
{
    public override bool SubConditionIsMet(GameAction gameAction) => true;

    public override bool OwnerMatches(GameAction gameAction, CombatantView owner)
    {
        return gameAction is DealDamageGA damage && owner != null && damage.Targets.Contains(owner);
    }

    public override void SubscribeCondition(Action<GameAction> reaction)
    {
        ActionSystem.SubscribeReaction<DealDamageGA>(reaction, reactionTiming);
    }

    public override void UnsubscribeCondition(Action<GameAction> reaction)
    {
        ActionSystem.UnsubscribeReaction<DealDamageGA>(reaction, reactionTiming);
    }
}
