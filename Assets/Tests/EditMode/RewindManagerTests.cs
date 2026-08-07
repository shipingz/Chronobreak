using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 测试用 IRewindable 假对象：每次 Capture 生成带递增序号位置（1,2,3...）的快照，
/// 用于断言录制帧序与覆盖行为。
/// </summary>
public class FakeRewindable : IRewindable
{
    public bool IsRewinding { get; set; }
    public int CaptureCount { get; private set; }
    private int sequence;

    public FrameSnapshot CaptureSnapshot()
    {
        CaptureCount++;
        return new FrameSnapshot(
            new Vector3(++sequence, 0f, 0f),
            new Vector2(sequence, 0f),
            100,
            true);
    }

    public void ApplySnapshot(FrameSnapshot snapshot) { }
    public void OnRewindStart() { }
    public void OnRewindEnd() { }
}

/// <summary>
/// RewindManager 单元测试（T-020，Edit Mode）
///
/// 验证 T-020 验收"每物理帧录制全部已注册对象"：
/// 录制循环 / 帧序 / 注册注销 / 幂等 / 清空 / 容量覆盖。
/// 测试创建独立实例（不走静态 Instance），避免污染场景与跨测试残留。
/// </summary>
public class RewindManagerTests
{
    private RewindManager manager;
    private GameObject go;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TestRewindManager");
        manager = go.AddComponent<RewindManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (go != null)
            Object.DestroyImmediate(go);
    }

    [Test]
    public void Register_ThenRecordStep_WritesOneFramePerObject()
    {
        var a = new FakeRewindable();
        var b = new FakeRewindable();

        manager.Register(a);
        manager.Register(b);

        manager.RecordStep();

        Assert.AreEqual(1, manager.GetBuffer(a).Count);
        Assert.AreEqual(1, manager.GetBuffer(b).Count);
        Assert.AreEqual(1f, manager.GetBuffer(a).Read(0).position.x);
        Assert.AreEqual(1f, manager.GetBuffer(b).Read(0).position.x);
        Assert.AreEqual(1, a.CaptureCount);
        Assert.AreEqual(1, b.CaptureCount);
    }

    [Test]
    public void RecordStep_MultipleSteps_FrameOrderCorrect()
    {
        var a = new FakeRewindable();
        manager.Register(a);

        manager.RecordStep();
        manager.RecordStep();
        manager.RecordStep();

        var buffer = manager.GetBuffer(a);
        Assert.AreEqual(3, buffer.Count);
        Assert.AreEqual(3f, buffer.Read(0).position.x); // 最新
        Assert.AreEqual(2f, buffer.Read(1).position.x);
        Assert.AreEqual(1f, buffer.Read(2).position.x); // 最旧
    }

    [Test]
    public void Unregister_StopsRecording()
    {
        var a = new FakeRewindable();
        manager.Register(a);
        manager.RecordStep();

        manager.Unregister(a);
        manager.RecordStep();

        Assert.IsNull(manager.GetBuffer(a));
        Assert.AreEqual(1, a.CaptureCount); // 注销后不再被录制
    }

    [Test]
    public void Register_Twice_IsIdempotent()
    {
        var a = new FakeRewindable();

        manager.Register(a);
        manager.Register(a);

        Assert.AreEqual(1, manager.RegisteredCount); // 不产生第二个缓冲
        manager.RecordStep();
        Assert.AreEqual(1, manager.GetBuffer(a).Count);
        Assert.AreEqual(1, a.CaptureCount); // 每步只录一帧
    }

    [Test]
    public void ClearAll_EmptiesAllBuffers()
    {
        var a = new FakeRewindable();
        var b = new FakeRewindable();
        manager.Register(a);
        manager.Register(b);
        manager.RecordStep();
        manager.RecordStep();

        manager.ClearAll();

        Assert.AreEqual(0, manager.GetBuffer(a).Count);
        Assert.AreEqual(0, manager.GetBuffer(b).Count);

        // 清空后录制恢复正常
        manager.RecordStep();
        Assert.AreEqual(1, manager.GetBuffer(a).Count);
    }

    [Test]
    public void RecordStep_Overflow_CapsAtCapacity()
    {
        var a = new FakeRewindable();
        manager.Register(a);

        for (int i = 0; i < RewindManager.BufferCapacity + 1; i++)
            manager.RecordStep();

        var buffer = manager.GetBuffer(a);
        Assert.AreEqual(RewindManager.BufferCapacity, buffer.Count); // 封顶不溢出
        Assert.AreEqual(RewindManager.BufferCapacity + 1f, buffer.Read(0).position.x); // 最新 = 第 301 帧
        Assert.AreEqual(2f, buffer.Read(RewindManager.BufferCapacity - 1).position.x); // 最旧 = 第 2 帧（第 1 帧被覆盖）
    }

    [Test]
    public void SceneLoaded_ClearsAllBuffers()
    {
        // 决策 5：切场景清空全部缓冲。Edit Mode 不真实加载场景，
        // 用反射触发私有 OnSceneLoaded，验证事件订阅链路存在且生效。
        var a = new FakeRewindable();
        var b = new FakeRewindable();
        manager.Register(a);
        manager.Register(b);
        manager.RecordStep();
        manager.RecordStep();

        var method = typeof(RewindManager).GetMethod(
            "OnSceneLoaded",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(method, "OnSceneLoaded 私有方法应存在（被 sceneLoaded 订阅）");
        method.Invoke(manager, new object[]
        {
            default(UnityEngine.SceneManagement.Scene),
            UnityEngine.SceneManagement.LoadSceneMode.Single
        });

        Assert.AreEqual(0, manager.GetBuffer(a).Count);
        Assert.AreEqual(0, manager.GetBuffer(b).Count);

        // 清空后录制恢复正常
        manager.RecordStep();
        Assert.AreEqual(1, manager.GetBuffer(a).Count);
    }
}
