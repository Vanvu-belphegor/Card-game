using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CombatantView : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private StatusEffectsUI statusEffectsUI;
    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    private Dictionary<StatusEffectType, int> statusEffects = new();

    protected void SetupBase(int health, Sprite image)
    {
        MaxHealth = CurrentHealth = health;
        spriteRenderer.sprite = image;
        UpdateHealthText();
    }
    private void UpdateHealthText()
    {
        healthText.text = "HP: " + CurrentHealth;
    }

    // ignoreArmor = true dùng cho Poison
    public void Damage(int damageAmount, bool ignoreArmor = false)
    {
        int remainingDamage = damageAmount;

        if (!ignoreArmor)
        {
            int currentArmor = GetStatusEffectStacks(StatusEffectType.ARMOR);
            if (currentArmor > 0)
            {
                int absorbed = Mathf.Min(currentArmor, remainingDamage);
                RemoveStatusEffect(StatusEffectType.ARMOR, absorbed);
                remainingDamage -= absorbed;
            }
        }

        if (remainingDamage > 0)
        {
            CurrentHealth -= remainingDamage;
            if (CurrentHealth < 0) CurrentHealth = 0;
        }
        transform.DOShakePosition(0.2f, 0.5f);
        UpdateHealthText();
    }

    public void AddStatusEffect(StatusEffectType type, int stackCount)
    {
        if (statusEffects.ContainsKey(type)) statusEffects[type] += stackCount;
        else statusEffects.Add(type, stackCount);
        statusEffectsUI.UpdateStatusEffectUI(type, GetStatusEffectStacks(type));
    }
    public void RemoveStatusEffect(StatusEffectType type, int stackCount)
    {
        if (statusEffects.ContainsKey(type))
        {
            statusEffects[type] -= stackCount;
            if (statusEffects[type] <= 0) statusEffects.Remove(type);
        }
        statusEffectsUI.UpdateStatusEffectUI(type, GetStatusEffectStacks(type));
    }
    // ── Icon perk (hiện phía trên nhân vật, tự tạo bằng code) ──
    private readonly List<(Perk perk, GameObject icon)> perkIcons = new();

    public void AddPerkIcon(Perk perk)
    {
        if (perk.Image == null || spriteRenderer == null) return;
        GameObject go = new("PerkIcon");
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = perk.Image;
        sr.sortingLayerID = spriteRenderer.sortingLayerID;
        sr.sortingOrder = spriteRenderer.sortingOrder + 5;
        perkIcons.Add((perk, go));
        LayoutPerkIcons();
    }

    public void RemovePerkIcon(Perk perk)
    {
        int i = perkIcons.FindIndex(p => p.perk == perk);
        if (i < 0) return;
        Destroy(perkIcons[i].icon);
        perkIcons.RemoveAt(i);
        LayoutPerkIcons();
    }

    private void LayoutPerkIcons()
    {
        const float size = 0.5f, gap = 0.6f; // kích thước icon và khoảng cách (đơn vị thế giới)
        Bounds b = spriteRenderer.bounds;
        float parentScale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        for (int i = 0; i < perkIcons.Count; i++)
        {
            GameObject go = perkIcons[i].icon;
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            float spriteSize = Mathf.Max(0.0001f, Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y));
            go.transform.localScale = Vector3.one * (size / spriteSize / parentScale);
            go.transform.position = new Vector3(b.center.x + (i - (perkIcons.Count - 1) / 2f) * gap, b.max.y + 0.4f, b.center.z);
        }
    }

    public int GetStatusEffectStacks(StatusEffectType type)
    {
        if (statusEffects.ContainsKey(type)) return statusEffects[type];
        else return 0;
    }
}
