using System;

// Nhãn của lá bài: một lá có thể có NHIỀU nhãn.
// QUAN TRỌNG: giá trị là lũy thừa của 2 và Unity lưu theo SỐ.
// Chỉ thêm nhãn mới ở CUỐI, không được đổi số hay xóa nhãn đã dùng, nếu không các thẻ cũ sẽ bị lệch nhãn.
// Tối đa 32 nhãn.
[Flags]
public enum CardTags
{
    None    = 0,
    Fire    = 1 << 0,
    Poison  = 1 << 1,
    Armor   = 1 << 2,
    Draw    = 1 << 3,
    Exhaust = 1 << 4,
    Buff    = 1 << 5,
    Debuff  = 1 << 6,
}
