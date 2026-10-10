public class PlayCardGA : GameAction
{
    public EnemyView ManualTarget { get; private set; }
    public Card Card { get; set; }
    public PlayCardGA(Card card)
    {
        Card = card;
        ManualTarget = null;
    }
    public PlayCardGA(Card card, EnemyView target)
    {
        Card = card;
        ManualTarget = target;
    }
}