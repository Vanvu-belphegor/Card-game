using UnityEngine;

public class MultiplyCardPowerGA : GameAction
{
    public int Factor { get; private set; }
    public CardFilter Filter { get; private set; }
    public int MaxCards { get; private set; }
    public MultiplyCardPowerGA(int factor, CardFilter filter, int maxCards)
    {
        Factor = factor;
        Filter = filter;
        MaxCards = maxCards;
    }
}
