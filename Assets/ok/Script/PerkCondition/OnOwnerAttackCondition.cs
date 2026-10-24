using System;

// Kích hoạt khi CHÍNH Enemy sở hữu perk này tấn công Hero.
public class OnOwnerAttackCondition : PerkCondition
{
    public override bool SubConditionIsMet(GameAction gameAction) => true;

    public override bool OwnerMatches(GameAction gameAction, CombatantView owner)
    {
        return gameAction is AttackHeroGA attack && owner != null && attack.Attacker == owner;
    }

    public override void SubscribeCondition(Action<GameAction> reaction)
    {
        ActionSystem.SubscribeReaction<AttackHeroGA>(reaction, reactionTiming);
    }

    public override void UnsubscribeCondition(Action<GameAction> reaction)
    {
        ActionSystem.UnsubscribeReaction<AttackHeroGA>(reaction, reactionTiming);
    }
}
