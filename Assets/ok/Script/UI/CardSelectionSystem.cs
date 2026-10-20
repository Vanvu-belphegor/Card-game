using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// Panel chọn bài: hiện danh sách lá bài (kéo ngang / lăn chuột để xem), bấm để chọn, Xác nhận hoặc Hủy.
// Tự dựng bằng code lúc chạy, tái dùng prefab CardView của CardViewCreator -> không cần setup trong scene.
//  - Max = 1: bấm lá là chọn ngay.   - Max > 1: chọn 1..Max lá rồi bấm Xác nhận (hoặc Enter).
//  - Hủy (nút hoặc Esc): không lấy lá nào.
public class CardSelectionSystem : MonoBehaviour
{
    public class SelectionResult
    {
        public readonly List<(Card card, CardZone zone, Vector3 position)> Picked = new();
    }

    private static CardSelectionSystem instance;
    public static CardSelectionSystem Get()
    {
        if (instance == null) instance = FindObjectOfType<CardSelectionSystem>();
        if (instance == null) instance = new GameObject("CardSelectionSystem").AddComponent<CardSelectionSystem>();
        return instance;
    }

    public IEnumerator Choose(List<(Card card, CardZone zone)> options, int maxPick, SelectionResult result)
    {
        if (options == null || options.Count == 0) yield break;
        Camera cam = Camera.main;
        CardView prefab = CardViewCreator.Instance != null ? CardViewCreator.Instance.Prefab : null;
        if (cam == null || prefab == null) yield break;

        Session session = new(cam, prefab, options, maxPick);
        session.Build();
        while (!session.Done)
        {
            session.Tick();
            yield return null;
        }
        if (session.Cancelled == false && session.Picked.Count > 0) yield return new WaitForSeconds(0.1f);
        session.Close(result);
    }

    // ─────────────────────────────────────────────────────────────
    private class Option
    {
        public Card card;
        public CardZone zone;
        public CardView view;
        public Collider collider;
        public SpriteRenderer overlay;
        public TextMeshPro badge;
        public float baseY;
        public bool selected;
    }

    private class Session
    {
        public bool Done { get; private set; }
        public bool Cancelled { get; private set; }
        public List<Option> Picked { get; } = new();

        private readonly Camera cam;
        private readonly CardView prefab;
        private readonly List<(Card card, CardZone zone)> source;
        private readonly int limit;

        private readonly List<Option> options = new();
        private readonly Dictionary<Collider, Option> optionByCollider = new();
        private readonly Dictionary<Collider, System.Action> buttons = new();

        private GameObject rootGO;
        private Transform root, content;
        private TextMeshPro counterText;
        private SpriteRenderer confirmBg;

        private float halfW, halfH, cardW, cardH, scrollRange, worldPerPixel, rowY;
        private bool pressing, dragged;
        private Vector2 pressStart, lastMouse;

        private static Sprite whiteSprite;
        private static Sprite White()
        {
            if (whiteSprite == null)
            {
                Texture2D tex = new(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return whiteSprite;
        }

        public Session(Camera cam, CardView prefab, List<(Card card, CardZone zone)> source, int maxPick)
        {
            this.cam = cam;
            this.prefab = prefab;
            this.source = source;
            limit = maxPick <= 0 ? source.Count : Mathf.Min(maxPick, source.Count);
        }

        public void Build()
        {
            // Đặt panel trên mặt phẳng z = -1 (cùng mặt phẳng game dùng khi kéo lá bài), hướng về camera
            Plane plane = new(cam.transform.forward, new Vector3(0, 0, -1));
            Ray centerRay = new(cam.transform.position, cam.transform.forward);
            float dist = plane.Raycast(centerRay, out float d) && d > 0.1f ? d : 10f;
            halfH = cam.orthographic ? cam.orthographicSize : dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            halfW = halfH * cam.aspect;
            worldPerPixel = halfW * 2f / Mathf.Max(1, Screen.width);

            rootGO = new GameObject("CardSelectionPanel");
            root = rootGO.transform;
            root.SetPositionAndRotation(cam.transform.position + cam.transform.forward * dist, cam.transform.rotation);
            content = new GameObject("Content").transform;
            content.SetParent(root, false);

            // Nền mờ che phía sau
            GameObject back = new("Backdrop");
            back.transform.SetParent(root, false);
            back.transform.localScale = new Vector3(halfW * 2.3f, halfH * 2.3f, 1);
            back.transform.localPosition = new Vector3(0, 0, 0.5f);
            SpriteRenderer backSR = back.AddComponent<SpriteRenderer>();
            backSR.sprite = White();
            backSR.color = new Color(0, 0, 0, 0.78f);
            backSR.sortingOrder = 100;

            // Kích thước lá (lấy từ BoxCollider của prefab) và tỉ lệ hiển thị cho vừa màn hình
            BoxCollider prefabCol = prefab.GetComponent<BoxCollider>();
            Vector3 ps = prefab.transform.localScale;
            cardW = (prefabCol != null ? prefabCol.size.x : 3.6f) * ps.x;
            cardH = (prefabCol != null ? prefabCol.size.y : 4.8f) * ps.y;
            float scale = Mathf.Min(1.25f, halfH * 2f * 0.5f / cardH, halfW * 2f * 0.8f / cardW);
            float cw = cardW * scale, ch = cardH * scale;
            float step = cw + cw * 0.15f;
            rowY = halfH * 0.02f;

            float totalW = (source.Count - 1) * step + cw;
            float viewW = halfW * 2f * 0.92f;
            scrollRange = Mathf.Max(0f, (totalW - viewW) / 2f);

            for (int i = 0; i < source.Count; i++)
            {
                Option o = new() { card = source[i].card, zone = source[i].zone };
                float x = -(totalW - cw) / 2f + i * step;

                CardView view = Object.Instantiate(prefab, content);
                view.Setup(o.card);
                view.InteractionLocked = true;
                view.transform.localRotation = Quaternion.identity;
                view.transform.localPosition = new Vector3(x, rowY, -0.5f);
                view.transform.localScale = Vector3.zero;
                view.transform.DOScale(Vector3.one * scale, 0.2f).SetDelay(0.02f * Mathf.Min(i, 10)).SetLink(rootGO);

                SortingGroup group = view.GetComponent<SortingGroup>();
                if (group == null) group = view.gameObject.AddComponent<SortingGroup>();
                group.sortingOrder = 200;

                // Lớp phủ vàng nhạt khi được chọn
                GameObject ov = new("SelectedOverlay");
                ov.transform.SetParent(view.transform, false);
                ov.transform.localPosition = new Vector3(0, prefabCol != null ? prefabCol.center.y : 0, -0.05f);
                ov.transform.localScale = new Vector3(cardW / ps.x, cardH / ps.y, 1);
                o.overlay = ov.AddComponent<SpriteRenderer>();
                o.overlay.sprite = White();
                o.overlay.color = new Color(1f, 0.85f, 0.2f, 0.22f);
                o.overlay.sortingOrder = 80;
                o.overlay.enabled = false;

                // Số thứ tự chọn (phía trên lá) và nguồn lá (phía dưới lá)
                o.badge = MakeText(content, "", cw * 0.5f, halfH * 0.14f, new Vector3(x, rowY + ch / 2f + halfH * 0.1f, -0.6f), 320, new Color(1f, 0.85f, 0.2f));
                MakeText(content, o.zone == CardZone.DiscardPile ? "Bài bỏ" : "Bộ bài", cw * 0.8f, halfH * 0.08f,
                         new Vector3(x, rowY - ch / 2f - halfH * 0.07f, -0.6f), 320, new Color(0.8f, 0.8f, 0.8f));

                o.view = view;
                o.baseY = rowY;
                o.collider = view.GetComponent<Collider>();
                if (o.collider != null) optionByCollider[o.collider] = o;
                options.Add(o);
            }

            // Tiêu đề, bộ đếm, gợi ý
            MakeText(root, limit == 1 ? "Chọn 1 lá" : $"Chọn tối đa {limit} lá", halfW * 1.2f, halfH * 0.14f, new Vector3(0, halfH * 0.84f, -0.6f), 320, Color.white);
            counterText = MakeText(root, "", halfW * 0.8f, halfH * 0.1f, new Vector3(0, halfH * 0.7f, -0.6f), 320, new Color(1f, 0.85f, 0.2f));
            if (scrollRange > 0f)
                MakeText(root, "Kéo ngang hoặc lăn chuột để xem thêm", halfW * 1.0f, halfH * 0.07f, new Vector3(0, -halfH * 0.58f, -0.6f), 320, new Color(0.7f, 0.7f, 0.7f));

            // Nút
            float bw = halfH * 0.6f, bh = halfH * 0.17f, by = -halfH * 0.76f;
            if (limit > 1)
            {
                Collider ok = MakeButton("Xác nhận", new Color(0.2f, 0.65f, 0.3f), bw, bh, new Vector3(bw * 0.6f, by, -0.6f), out confirmBg);
                buttons[ok] = TryConfirm;
                Collider cancel = MakeButton("Hủy", new Color(0.65f, 0.25f, 0.25f), bw, bh, new Vector3(-bw * 0.6f, by, -0.6f), out _);
                buttons[cancel] = Cancel;
            }
            else
            {
                Collider cancel = MakeButton("Hủy", new Color(0.65f, 0.25f, 0.25f), bw, bh, new Vector3(0, by, -0.6f), out _);
                buttons[cancel] = Cancel;
            }
            Refresh();
        }

        public void Tick()
        {
            if (Done) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
            if (limit > 1 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) { TryConfirm(); return; }

            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0f) Scroll(wheel * cardW * 0.4f);

            if (Input.GetMouseButtonDown(0))
            {
                pressing = true; dragged = false;
                pressStart = lastMouse = Input.mousePosition;
            }
            if (pressing && Input.GetMouseButton(0))
            {
                Vector2 now = Input.mousePosition;
                if (!dragged && (now - pressStart).magnitude > 12f) dragged = true;
                if (dragged) Scroll((now.x - lastMouse.x) * worldPerPixel);
                lastMouse = now;
            }
            if (pressing && Input.GetMouseButtonUp(0))
            {
                pressing = false;
                if (!dragged) Click(Input.mousePosition);
            }
        }

        private void Scroll(float dx)
        {
            if (scrollRange <= 0f) return;
            Vector3 p = content.localPosition;
            p.x = Mathf.Clamp(p.x + dx, -scrollRange, scrollRange);
            content.localPosition = p;
        }

        private void Click(Vector2 screenPos)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (buttons.TryGetValue(h.collider, out System.Action act)) { act(); return; }
                if (optionByCollider.TryGetValue(h.collider, out Option o)) { Toggle(o); return; }
            }
        }

        private void Toggle(Option o)
        {
            if (o.selected)
            {
                o.selected = false;
                Picked.Remove(o);
            }
            else
            {
                if (limit > 1 && Picked.Count >= limit)
                {
                    o.view.transform.DOPunchScale(Vector3.one * 0.06f, 0.2f).SetLink(rootGO); // đã đủ: bỏ chọn lá khác trước
                    return;
                }
                o.selected = true;
                Picked.Add(o);
                if (limit == 1) { Refresh(); Done = true; return; } // Max = 1: chọn xong luôn
            }
            Refresh();
        }

        private void TryConfirm()
        {
            if (Picked.Count >= 1) Done = true;
        }

        private void Cancel()
        {
            Cancelled = true;
            Done = true;
        }

        private void Refresh()
        {
            foreach (Option o in options)
            {
                o.overlay.enabled = o.selected;
                float y = o.baseY + (o.selected ? halfH * 0.06f : 0f);
                o.view.transform.DOLocalMoveY(y, 0.12f).SetLink(rootGO);
                int idx = Picked.IndexOf(o);
                o.badge.text = idx >= 0 && limit > 1 ? (idx + 1).ToString() : (idx >= 0 ? "✓" : "");
            }
            counterText.text = $"{Picked.Count} / {limit}";
            if (confirmBg != null)
                confirmBg.color = Picked.Count >= 1 ? new Color(0.2f, 0.65f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
        }

        public void Close(SelectionResult result)
        {
            if (!Cancelled)
                foreach (Option o in Picked)
                    result.Picked.Add((o.card, o.zone, o.view.transform.position));
            if (rootGO != null) Object.Destroy(rootGO);
        }

        // ── Helper tạo chữ / nút trong thế giới 2D ──
        private TextMeshPro MakeText(Transform parent, string text, float w, float h, Vector3 localPos, int order, Color color)
        {
            GameObject go = new("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            TextMeshPro t = go.AddComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = new Vector2(w, h);
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true;
            t.fontSizeMin = 1f;
            t.fontSizeMax = 100f;
            t.enableWordWrapping = false;
            t.fontStyle = FontStyles.Bold;
            t.color = color;
            t.text = text;
            t.sortingOrder = order;
            return t;
        }

        private Collider MakeButton(string label, Color color, float w, float h, Vector3 localPos, out SpriteRenderer bg)
        {
            GameObject go = new("Button_" + label);
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(w, h, 1);
            bg = go.AddComponent<SpriteRenderer>();
            bg.sprite = White();
            bg.color = color;
            bg.sortingOrder = 300;
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1, 1, 0.1f);
            MakeText(root, label, w * 0.85f, h * 0.65f, localPos + new Vector3(0, 0, -0.05f), 310, Color.white);
            return col;
        }
    }
}
