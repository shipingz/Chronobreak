using System;

/// <summary>
/// 环形缓冲区（T-017）
///
/// 回溯系统的核心存储：固定容量，写满后覆盖最旧帧，保证总是保留最近 N 帧。
/// 容量由 RewindManager 决定（决策 1：300 帧 = 50Hz × 6s，覆盖 5 秒回溯上限 + 余量）。
///
/// 技术决策（依据：项目规划/项目时间规划.md §1）：
/// - 决策 4：StopRewind 必须截断缓冲区。head/count 退到停止帧，否则"旧未来"的帧残留，
///   二次回溯会读到被撤销的时间线导致瞬移——这是回溯系统最常见的坑。
///
/// 语义约定：
/// - Read(offset)：offset=0 为最新帧，offset=1 为前一帧，依此类推
/// - Truncate(discardCount)：丢弃最新的 discardCount 帧（回溯消费掉的帧），保留其余历史
/// - fail fast：空缓冲读取或 offset 越界抛异常（回放管线不该在空缓冲时读，启动门槛会先拦住）
/// </summary>
public class RingBuffer<T> where T : struct
{
    private readonly T[] buffer;
    private int head;   // 下次写入位置
    private int count;  // 当前有效帧数

    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("capacity 必须大于 0", nameof(capacity));
        buffer = new T[capacity];
    }

    public int Count => count;
    public int Capacity => buffer.Length;
    public int Head => head;

    /// <summary>写最新帧；写满后自动覆盖最旧帧</summary>
    public void Write(T item)
    {
        buffer[head] = item;
        head = (head + 1) % buffer.Length;
        if (count < buffer.Length) count++;
    }

    /// <summary>从最新帧往前回读：offset=0 为最新帧，offset=1 为前一帧，依此类推</summary>
    /// <exception cref="InvalidOperationException">缓冲为空时</exception>
    /// <exception cref="ArgumentOutOfRangeException">offset 超出 [0, count-1] 时</exception>
    public T Read(int offset)
    {
        if (count == 0)
            throw new InvalidOperationException("RingBuffer 为空，无法读取");
        if (offset < 0 || offset >= count)
            throw new ArgumentOutOfRangeException(nameof(offset), $"offset={offset} 超出有效范围 [0, {count - 1}]");

        int index = (head - 1 - offset + buffer.Length) % buffer.Length;
        return buffer[index];
    }

    /// <summary>
    /// 截断（决策 4）：丢弃最新的 discardCount 帧（回溯消费掉的帧），head/count 退回停止帧。
    /// 回溯停止时直接传回放消费的帧数 Truncate(rewindOffset)，清除被撤销的"未来"，
    /// 保证二次回溯不会读到旧时间线。
    /// Truncate(0) 为无操作；Truncate(Count) 等价清空。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">discardCount 超出 [0, count] 时</exception>
    public void Truncate(int discardCount)
    {
        if (discardCount < 0 || discardCount > count)
            throw new ArgumentOutOfRangeException(nameof(discardCount), $"discardCount={discardCount} 超出有效范围 [0, {count}]");

        head = (head - discardCount + buffer.Length) % buffer.Length;
        count -= discardCount;
    }

    /// <summary>清空全部帧（死亡重生 / 切场景时调用，决策 5）</summary>
    public void Clear()
    {
        count = 0;
        head = 0;
    }
}
