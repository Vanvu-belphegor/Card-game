using UnityEngine;

// Đại diện cho TOÀN BỘ hành động của 1 địch trong lượt: Burn -> Poison -> Tấn công
public class EnemyActionGA : GameAction
{
    public EnemyView Enemy { get; private set; }
    public EnemyActionGA(EnemyView enemy)
    {
        Enemy = enemy;
    }
}
