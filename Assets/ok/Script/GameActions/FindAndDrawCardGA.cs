public class FindAndDrawCardGA : GameAction
{
    public CardAcquisitionMode Mode { get; }
    public string ExactTitle { get; }
    public CardFilter Filter { get; }

    public FindAndDrawCardGA(CardAcquisitionMode mode, string exactTitle, CardFilter filter)
    {
        Mode = mode;
        ExactTitle = exactTitle;
        Filter = filter;
    }
}