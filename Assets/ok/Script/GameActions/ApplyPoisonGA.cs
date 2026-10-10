using UnityEngine;

public class ApplyPoisonGA : GameAction
{
    public int PoisonDamage { get; private set; }
    public CombatantView Target { get; private set; }
    public ApplyPoisonGA(int poisonDamage, CombatantView target)
    {
        PoisonDamage = poisonDamage;
        Target = target;
    }
}
