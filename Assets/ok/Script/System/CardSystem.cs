using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

using UnityEngine;

public class CardSystem : Singleton<CardSystem>
{
    [SerializeField] private HandView handView;
    [SerializeField] private Transform drawPilePoint;
    [SerializeField] private Transform discardPilePoint;
    private readonly List<Card> drawPile = new();
    private readonly List<Card> discardPile = new();
    private readonly List<Card> hand = new();

    void OnEnable()
    {
        ActionSystem.AttachPerformer<DrawCardsGA>(DrawCardsPerformer);
        ActionSystem.AttachPerformer<DiscardAllCardsGA>(DiscardAllCardsPerformer);
        ActionSystem.AttachPerformer<PlayCardGA>(PlayCardPerformer);
        ActionSystem.AttachPerformer<MultiplyCardPowerGA>(MultiplyCardPowerPerformer);
        ActionSystem.AttachPerformer<FindAndDrawCardsGA>(FindAndDrawCardsPerformer);
        // Strength đổi thì số trong mô tả các lá đang ở tay cũng đổi theo
        ActionSystem.SubscribeReaction<AddStatusEffectGA>(RefreshHandDescriptions, ReactionTiming.POST);
    }

    void OnDisable()
    {
        ActionSystem.DetachPerformer<DrawCardsGA>();
        ActionSystem.DetachPerformer<DiscardAllCardsGA>();
        ActionSystem.DetachPerformer<PlayCardGA>();
        ActionSystem.DetachPerformer<MultiplyCardPowerGA>();
        ActionSystem.DetachPerformer<FindAndDrawCardsGA>();
        ActionSystem.UnsubscribeReaction<AddStatusEffectGA>(RefreshHandDescriptions, ReactionTiming.POST);
    }

    //Publics
    public void Setup(List<CardData> deckData)
    {
        foreach (var cardData in deckData)
        {
            Card card = new(cardData);
            drawPile.Add(card);
        }
    }

    // Tìm bài theo vùng + bộ lọc (filter == null: lấy tất cả). Trả về danh sách MỚI, sửa thoải mái.
    public List<Card> FindCards(CardZone zone, CardFilter filter = null)
    {
        List<Card> source = zone switch
        {
            CardZone.Hand => hand,
            CardZone.DrawPile => drawPile,
            CardZone.DiscardPile => discardPile,
            _ => hand
        };
        List<Card> result = new();
        foreach (Card card in source)
            if (filter == null || filter.Matches(card)) result.Add(card);
        return result;
    }

    // Performers
    private IEnumerator DrawCardsPerformer(DrawCardsGA drawCardsGA)
    {
        int actualAmount = Mathf.Min(drawCardsGA.Amount, drawPile.Count);
        int notDrawnAmount = drawCardsGA.Amount - actualAmount;
        for (int i = 0; i < actualAmount; i++)
        {
            yield return DrawCard();
        }
        if (notDrawnAmount > 0)
        {
            RefillDeck();
            for (int i = 0; i < notDrawnAmount; i++)
            {
                yield return DrawCard();
            }
        }
    }

    private IEnumerator DiscardAllCardsPerformer(DiscardAllCardsGA discardAllCardsGA)
    {
        // Hết lượt: dọn buff tạm của MỌI lá (tay, chồng rút, chồng bỏ), không chỉ lá còn trong tay
        ClearTemporaryModifiers(hand);
        ClearTemporaryModifiers(drawPile);
        ClearTemporaryModifiers(discardPile);

        foreach (var card in hand)
        {
            CardView cardView = handView.RemoveCard(card);
            yield return DiscardCard(cardView);
        }
        hand.Clear();
    }

    private IEnumerator PlayCardPerformer(PlayCardGA playCardGA)
    {
        
        hand.Remove(playCardGA.Card);
        CardView cardView = handView.RemoveCard(playCardGA.Card);
        yield return DiscardCard(cardView);

        SpendManaGA spendManaGA = new(playCardGA.Card.Mana);
        ActionSystem.Instance.AddReaction(spendManaGA);

        // Chụp bộ modifier của lá này (loại dùng một lần bị xóa ngay), chỉ có tác dụng cho lần chơi này.
        // Mọi Effect của lá dùng chung một context nên không Effect nào tiêu thụ trùng.
        EffectContext context = new(playCardGA.Card.Modifiers.ConsumeForPlay());

        if (playCardGA.Card.ManualTargetEffect != null)
        {
            PerformEffectGA performEffectGA = new(playCardGA.Card.ManualTargetEffect, new() { playCardGA.ManualTarget }, context);
            ActionSystem.Instance.AddReaction(performEffectGA);
        }
        foreach (var effectWrapper in playCardGA.Card.OtherEffects)
        {
            List<CombatantView> targets = effectWrapper.TargetMode.GetTargets();
            PerformEffectGA performEffectGA = new(effectWrapper.Effect, targets, context);
            ActionSystem.Instance.AddReaction(performEffectGA);
        }
    }

    // Nhân chỉ số các lá khác trong tay (lá đang chơi đã bị remove khỏi hand nên tự động bị loại)
    private IEnumerator MultiplyCardPowerPerformer(MultiplyCardPowerGA ga)
    {
        List<Card> candidates = FindCards(CardZone.Hand, ga.Filter);
        if (candidates.Count == 0) yield break;

        // Chọn ngẫu nhiên tối đa MaxCards lá (MaxCards <= 0 = lấy hết)
        List<Card> chosen = new();
        while (candidates.Count > 0 && (ga.MaxCards <= 0 || chosen.Count < ga.MaxCards))
        {
            int i = Random.Range(0, candidates.Count);
            chosen.Add(candidates[i]);
            candidates.RemoveAt(i);
        }

        foreach (Card card in chosen)
        {
            card.Modifiers.Add(StatModifier.Multiply(
                StatKey.All, Mathf.Max(1, ga.Factor), ModifierLifetime.UntilConsumed,
                sourceId: "MultiplyCardPower:" + System.Guid.NewGuid().ToString("N")));
            CardView view = handView.GetCardView(card);
            if (view != null)
            {
                Tween tween = view.Refresh();
                yield return tween.WaitForCompletion();
            }
        }
    }

    // Tìm bài trong các vùng được tick (bộ bài / bài bỏ) rồi đưa lên tay.
    // Chọn ngẫu nhiên giữa mọi lá khớp, tối đa MaxCards lá (<= 0 = tất cả). Không có lá khớp thì bỏ qua êm.
    private IEnumerator FindAndDrawCardsPerformer(FindAndDrawCardsGA ga)
    {
        List<(Card card, CardZone zone)> candidates = new();
        if ((ga.Zones & CardZoneMask.DrawPile) != 0)
            foreach (Card c in FindCards(CardZone.DrawPile, ga.Filter)) candidates.Add((c, CardZone.DrawPile));
        if ((ga.Zones & CardZoneMask.DiscardPile) != 0)
            foreach (Card c in FindCards(CardZone.DiscardPile, ga.Filter)) candidates.Add((c, CardZone.DiscardPile));

        if (candidates.Count == 0) yield break;

        // Người chơi tự chọn: hiện panel, chờ chọn xong rồi mới chuyển bài lên tay
        if (ga.Mode == CardSelectionMode.PlayerChoice)
        {
            // Xáo thứ tự hiển thị để người chơi không đoán được thứ tự chồng bài
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            CardSelectionSystem.SelectionResult result = new();
            yield return CardSelectionSystem.Get().Choose(candidates, ga.MaxCards, result);
            foreach (var picked in result.Picked)
                yield return MoveCardToHand(picked.card, picked.zone, picked.position);
            yield break;
        }

        List<(Card card, CardZone zone)> chosen = new();
        while (candidates.Count > 0 && (ga.MaxCards <= 0 || chosen.Count < ga.MaxCards))
        {
            int i = Random.Range(0, candidates.Count);
            chosen.Add(candidates[i]);
            candidates.RemoveAt(i);
        }

        foreach (var (card, zone) in chosen)
            yield return MoveCardToHand(card, zone);
    }

    private void RefreshHandDescriptions(AddStatusEffectGA ga)
    {
        foreach (Card card in hand)
        {
            CardView view = handView.GetCardView(card);
            if (view != null) view.UpdateDescription();
        }
    }

    //helpers
    private IEnumerator DrawCard()
    {
        Card card = drawPile.Draw();
        hand.Add(card);
        CardView cardView = CardViewCreator.Instance.CreateCardView(card, drawPilePoint.position, drawPilePoint.rotation);
        yield return handView.AddCard(cardView);
    }

    private static void ClearTemporaryModifiers(List<Card> cards)
    {
        foreach (Card card in cards) card.Modifiers.ClearOnTurnEnd();
    }

    // Chuyển CHÍNH đối tượng Card từ vùng nguồn lên tay (không tạo bản sao nên giữ nguyên modifier/nhãn).
    // Lá bay ra từ đúng chồng nguồn.
    private IEnumerator MoveCardToHand(Card card, CardZone sourceZone, Vector3? startPosition = null)
    {
        List<Card> source = sourceZone == CardZone.DiscardPile ? discardPile : drawPile;
        if (!source.Remove(card)) yield break; // lá không còn ở vùng nguồn: bỏ qua

        hand.Add(card);
        Transform point = sourceZone == CardZone.DiscardPile ? discardPilePoint : drawPilePoint;
        CardView cardView = CardViewCreator.Instance.CreateCardView(card, startPosition ?? point.position, point.rotation);
        yield return handView.AddCard(cardView);
    }

    private void RefillDeck()
    {
        drawPile.AddRange(discardPile);
        discardPile.Clear();
    }

    private IEnumerator DiscardCard(CardView cardView)
    {
        discardPile.Add(cardView.Card);
        cardView.transform.DOScale(Vector3.zero, 0.15f);
        Tween tween = cardView.transform.DOMove(discardPilePoint.position, 0.15f);
        yield return tween.WaitForCompletion();
        Destroy(cardView.gameObject);
    }
}
