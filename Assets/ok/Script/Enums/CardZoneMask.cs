using System;

// Vùng được phép tìm bài. Tick được nhiều vùng cùng lúc (flag).
[Flags]
public enum CardZoneMask
{
    None = 0,
    DrawPile = 1 << 0,
    DiscardPile = 1 << 1
}
