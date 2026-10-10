// Loại lá bài: mỗi lá chỉ thuộc MỘT loại.
// None = chưa phân loại (mặc định cho các CardData cũ). Trong CardFilter, None nghĩa là "không lọc theo loại".
public enum CardType
{
    None = 0,
    Attack = 1,
    Skill = 2,
    Power = 3
}
