public class FindAndDrawCardsGA : GameAction
{
    public CardFilter Filter { get; private set; }
    public CardZoneMask Zones { get; private set; }
    public int MaxCards { get; private set; }
    public CardSelectionMode Mode { get; private set; }

    public FindAndDrawCardsGA(CardFilter filter, CardZoneMask zones, int maxCards,
                              CardSelectionMode mode = CardSelectionMode.Random)
    {
        Filter = filter;
        Zones = zones;
        MaxCards = maxCards;
        Mode = mode;
    }
}
