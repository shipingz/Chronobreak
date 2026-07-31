using NUnit.Framework;

/// <summary>
/// RingBuffer 单元测试（T-018，Edit Mode）
///
/// 覆盖规划要求的四类场景：读写 / 覆盖 / 截断 / 空缓冲，外加异常路径与决策 4 专项。
/// 测试对象是纯 C# 数据结构（RingBuffer&lt;int&gt;），Edit Mode 即可运行，无需 Play Mode。
/// </summary>
public class RingBufferTests
{
    // ============================================================
    // 构造
    // ============================================================

    [Test]
    public void Constructor_InvalidCapacity_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => new RingBuffer<int>(0));
        Assert.Throws<System.ArgumentException>(() => new RingBuffer<int>(-1));
    }

    // ============================================================
    // 读写
    // ============================================================

    [Test]
    public void Write_Read_ReturnsLatestFirst()
    {
        var buffer = new RingBuffer<int>(8);

        buffer.Write(10);
        buffer.Write(20);
        buffer.Write(30);

        Assert.AreEqual(3, buffer.Count);
        Assert.AreEqual(30, buffer.Read(0)); // 最新帧
        Assert.AreEqual(20, buffer.Read(1)); // 前一帧
        Assert.AreEqual(10, buffer.Read(2)); // 最旧帧
    }

    [Test]
    public void Read_EmptyBuffer_Throws()
    {
        var buffer = new RingBuffer<int>(8);

        Assert.Throws<System.InvalidOperationException>(() => buffer.Read(0));
    }

    [Test]
    public void Read_OffsetOutOfRange_Throws()
    {
        var buffer = new RingBuffer<int>(8);
        buffer.Write(1);
        buffer.Write(2);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => buffer.Read(2)); // 越界（count=2）
        Assert.Throws<System.ArgumentOutOfRangeException>(() => buffer.Read(-1));
    }

    // ============================================================
    // 覆盖（写满后自动覆盖最旧帧）
    // ============================================================

    [Test]
    public void Write_Overflow_OverwritesOldest()
    {
        var buffer = new RingBuffer<int>(4);

        for (int i = 1; i <= 7; i++)
            buffer.Write(i); // 写入 1..7，capacity=4

        Assert.AreEqual(4, buffer.Count);
        Assert.AreEqual(7, buffer.Read(0)); // 最新
        Assert.AreEqual(6, buffer.Read(1));
        Assert.AreEqual(5, buffer.Read(2));
        Assert.AreEqual(4, buffer.Read(3)); // 最旧有效帧（1..3 已被覆盖）
    }

    [Test]
    public void Write_AfterFull_CountStaysAtCapacity()
    {
        var buffer = new RingBuffer<int>(4);

        for (int i = 0; i < 100; i++)
            buffer.Write(i);

        Assert.AreEqual(4, buffer.Count);
        Assert.AreEqual(4, buffer.Capacity);
    }

    // ============================================================
    // 截断（决策 4：清除被撤销的"未来"）
    // ============================================================

    [Test]
    public void Truncate_RemovesNewestFrames()
    {
        var buffer = new RingBuffer<int>(8);
        for (int i = 1; i <= 6; i++)
            buffer.Write(i); // 1..6，最新 = 6

        buffer.Truncate(2); // 丢弃 6、5（回溯消费掉的 2 帧）

        Assert.AreEqual(4, buffer.Count);
        Assert.AreEqual(4, buffer.Read(0)); // 停止帧 = 原来的 Read(2)
        Assert.AreEqual(3, buffer.Read(1));
        Assert.AreEqual(2, buffer.Read(2));
        Assert.AreEqual(1, buffer.Read(3));
    }

    [Test]
    public void Truncate_Zero_NoOp()
    {
        var buffer = new RingBuffer<int>(8);
        buffer.Write(1);
        buffer.Write(2);
        buffer.Write(3);

        buffer.Truncate(0);

        Assert.AreEqual(3, buffer.Count);
        Assert.AreEqual(3, buffer.Read(0));
    }

    [Test]
    public void Truncate_FullCount_Clears()
    {
        var buffer = new RingBuffer<int>(8);
        buffer.Write(1);
        buffer.Write(2);
        buffer.Write(3);

        buffer.Truncate(3);

        Assert.AreEqual(0, buffer.Count);
        Assert.Throws<System.InvalidOperationException>(() => buffer.Read(0));
    }

    [Test]
    public void Truncate_Invalid_Throws()
    {
        var buffer = new RingBuffer<int>(8);
        buffer.Write(1);
        buffer.Write(2);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => buffer.Truncate(-1));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => buffer.Truncate(3)); // 超过 count=2
    }

    [Test]
    public void Truncate_ThenWrite_NoGhostFuture()
    {
        // 决策 4 的核心场景：截断后继续写入，绝不能读到被撤销的"旧未来"帧
        var buffer = new RingBuffer<int>(4);

        buffer.Write(1); // a
        buffer.Write(2); // b
        buffer.Write(3); // c
        buffer.Write(4); // d（写满，head 已环绕回 0）

        buffer.Truncate(2); // 回溯消费 2 帧（丢弃 d、c），停止帧 = b（=2）

        buffer.Write(5); // e：新时间线第一帧，应覆盖被丢弃的 c 的位置

        Assert.AreEqual(3, buffer.Count);
        Assert.AreEqual(5, buffer.Read(0)); // 最新 = e
        Assert.AreEqual(2, buffer.Read(1)); // 停止帧 b
        Assert.AreEqual(1, buffer.Read(2)); // a
        // 关键：c(3)、d(4) 已被截断清除，任何 offset 都读不到
        Assert.Throws<System.ArgumentOutOfRangeException>(() => buffer.Read(3));
    }

    // ============================================================
    // 清空（决策 5：死亡重生 / 切场景时调用）
    // ============================================================

    [Test]
    public void Clear_ResetsBuffer()
    {
        var buffer = new RingBuffer<int>(8);
        buffer.Write(1);
        buffer.Write(2);
        buffer.Write(3);

        buffer.Clear();

        Assert.AreEqual(0, buffer.Count);
        Assert.Throws<System.InvalidOperationException>(() => buffer.Read(0));

        // 清空后从头写入正常
        buffer.Write(7);
        Assert.AreEqual(1, buffer.Count);
        Assert.AreEqual(7, buffer.Read(0));
    }
}
