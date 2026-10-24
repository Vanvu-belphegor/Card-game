using System;
using UnityEngine;

public abstract class PerkCondition
{
    [SerializeField] protected ReactionTiming reactionTiming;
    public abstract void SubscribeCondition(Action<GameAction> reaction);
    public abstract void UnsubscribeCondition(Action<GameAction> reaction);
    public abstract bool SubConditionIsMet(GameAction gameAction);

    // Sự kiện này có liên quan đến chủ perk không? (owner = Enemy sở hữu perk, hoặc Hero nếu perk không có chủ)
    // Mặc định: luôn đúng. Các điều kiện kiểu "khi CHỦ perk làm gì đó" override hàm này.
    public virtual bool OwnerMatches(GameAction gameAction, CombatantView owner) => true;
}
