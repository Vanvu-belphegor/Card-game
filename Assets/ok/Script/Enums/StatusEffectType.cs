// QUAN TRỌNG: chỉ thêm giá trị MỚI Ở CUỐI. Unity lưu enum theo số thứ tự,
// nếu chèn giữa thì các thẻ bài/asset cũ sẽ bị đổi sai hiệu ứng.
public enum StatusEffectType
{
    ARMOR,
    BURN,
    POISON,      // mỗi lượt mất máu = số stack, BỎ QUA armor, giảm 1 stack/lượt
    STRENGTH,    // +1 sát thương mỗi stack (vĩnh viễn)
    WEAK,        // gây ít sát thương hơn 25%, giảm 1 stack/lượt
    VULNERABLE   // nhận nhiều sát thương hơn 50%, giảm 1 stack/lượt
}
