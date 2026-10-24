using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySystem : Singleton<EnemySystem>
{
    [SerializeField] private EnemyBoardView enemyBoardView;
    public List<EnemyView> Enemies => enemyBoardView.EnemyViews;
    void OnEnable()
    {
        ActionSystem.AttachPerformer<EnemyTurnGA>(EnemyTurnPerformer);
        ActionSystem.AttachPerformer<EnemyActionGA>(EnemyActionPerformer);
        ActionSystem.AttachPerformer<AttackHeroGA>(AttackHeroPerformer);
        ActionSystem.AttachPerformer<KillEnemyGA>(KillEnemyPerformer);
    }
    void OnDisable()
    {
        ActionSystem.DetachPerformer<EnemyTurnGA>();
        ActionSystem.DetachPerformer<EnemyActionGA>();
        ActionSystem.DetachPerformer<AttackHeroGA>();
        ActionSystem.DetachPerformer<KillEnemyGA>();
    }
    public void Setup(List<EnemyData> enemyDatas)
    {
        foreach (var enemyData in enemyDatas)
        {
            enemyBoardView.AddEnemy(enemyData);
        }
    }

    // Performers
    private IEnumerator EnemyTurnPerformer(EnemyTurnGA enemyTurnGA)
    {
        foreach (var enemy in enemyBoardView.EnemyViews)
        {
            ActionSystem.Instance.AddReaction(new EnemyActionGA(enemy));
        }

        // Poison của Hero tick sau khi các địch đã hành động
        CombatantView hero = HeroSystem.Instance.HeroView;
        int heroPoison = hero.GetStatusEffectStacks(StatusEffectType.POISON);
        if (heroPoison > 0)
            ActionSystem.Instance.AddReaction(new ApplyPoisonGA(heroPoison, hero));

        // Cuối vòng: giảm Weak/Vulnerable của tất cả
        foreach (var enemy in enemyBoardView.EnemyViews)
            ActionSystem.Instance.AddReaction(new DecayStatusEffectsGA(enemy));
        ActionSystem.Instance.AddReaction(new DecayStatusEffectsGA(hero));

        yield return null;
    }

    // Hành động của 1 địch: Burn -> Poison -> Tấn công (là con của EnemyActionGA)
    private IEnumerator EnemyActionPerformer(EnemyActionGA enemyActionGA)
    {
        EnemyView enemy = enemyActionGA.Enemy;
        if (enemy == null) yield break;

        int burnStacks = enemy.GetStatusEffectStacks(StatusEffectType.BURN);
        if (burnStacks > 0)
            ActionSystem.Instance.AddReaction(new ApplyBurnGA(burnStacks, enemy));

        int poisonStacks = enemy.GetStatusEffectStacks(StatusEffectType.POISON);
        if (poisonStacks > 0)
            ActionSystem.Instance.AddReaction(new ApplyPoisonGA(poisonStacks, enemy));

        ActionSystem.Instance.AddReaction(new AttackHeroGA(enemy));
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator AttackHeroPerformer(AttackHeroGA attackHeroGA)
    {
        EnemyView attacker = attackHeroGA.Attacker;
        // Địch đã chết vì Burn/Poison thì không được đánh nữa
        if (attacker == null || attacker.CurrentHealth <= 0) yield break;

        Tween tween = attacker.transform.DOMoveX(attacker.transform.position.x - 1f, 0.15f);
        yield return tween.WaitForCompletion();
        attacker.transform.DOMoveX(attacker.transform.position.x + 1f, 0.25f);
        DealDamageGA dealDamageGA = new(attacker.AttackPower, new() { HeroSystem.Instance.HeroView }, attackHeroGA.Caster);
        ActionSystem.Instance.AddReaction(dealDamageGA);
    }
    private IEnumerator KillEnemyPerformer(KillEnemyGA killEnemyGA)
    {
        // Tránh kill 2 lần cùng một địch (vd: Poison và Burn cùng giết)
        if (killEnemyGA.EnemyView == null || !enemyBoardView.EnemyViews.Contains(killEnemyGA.EnemyView)) yield break;
        PerkSystem.Instance.RemovePerksOf(killEnemyGA.EnemyView); // gỡ perk của địch đã chết
        yield return enemyBoardView.RemoveEnemy(killEnemyGA.EnemyView);
    }
}
