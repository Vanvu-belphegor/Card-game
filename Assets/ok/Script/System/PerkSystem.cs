using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerkSystem : Singleton<PerkSystem>
{
    [SerializeField] private PerksUI perksUI; // UI perk của Hero
    private readonly List<Perk> perks = new();

    void OnEnable()
    {
        ActionSystem.AttachPerformer<AddPerkGA>(AddPerkPerformer);
    }

    void OnDisable()
    {
        ActionSystem.DetachPerformer<AddPerkGA>();
    }

    // Lá bài / effect thêm perk: mỗi mục tiêu nhận một Perk riêng.
    // Mục tiêu là Hero -> perk của Hero (như cũ). Mục tiêu khác -> perk thuộc về Enemy đó.
    private IEnumerator AddPerkPerformer(AddPerkGA ga)
    {
        if (ga.PerkData == null) yield break;
        foreach (CombatantView target in ga.Targets)
        {
            if (target == null || target.CurrentHealth <= 0) continue;
            CombatantView owner = target == HeroSystem.Instance.HeroView ? null : target;
            if (!ga.AllowDuplicate && HasPerk(owner, ga.PerkData)) continue;
            AddPerk(new Perk(ga.PerkData, owner));
            yield return null;
        }
    }

    private bool HasPerk(CombatantView owner, PerkData data)
    {
        return perks.Exists(p => p.Data == data && p.OwnerView == owner && p.HasOwner == (owner != null));
    }

    public void AddPerk(Perk perk)
    {
        perks.Add(perk);
        if (perk.HasOwner) perk.OwnerView.AddPerkIcon(perk);   // perk của Enemy: icon hiện trên Enemy đó
        else if (perksUI != null) perksUI.AddPerkUI(perk);      // perk của Hero: UI như cũ
        perk.OnAdd();
    }

    public void RemovePerk(Perk perk)
    {
        if (!perks.Remove(perk)) return;
        if (perk.HasOwner) { if (perk.OwnerView != null) perk.OwnerView.RemovePerkIcon(perk); }
        else if (perksUI != null) perksUI.RemovePerkUI(perk);
        perk.OnRemove();
    }

    // Gỡ toàn bộ perk của một Enemy (gọi khi Enemy chết)
    public void RemovePerksOf(CombatantView owner)
    {
        if (owner == null) return;
        foreach (Perk perk in perks.FindAll(p => p.OwnerView == owner))
            RemovePerk(perk);
    }
}
