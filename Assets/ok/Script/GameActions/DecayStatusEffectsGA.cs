using UnityEngine;

// Cuối vòng: giảm 1 stack của WEAK và VULNERABLE trên Target
public class DecayStatusEffectsGA : GameAction
{
    public CombatantView Target { get; private set; }
    public DecayStatusEffectsGA(CombatantView target)
    {
        Target = target;
    }
}
