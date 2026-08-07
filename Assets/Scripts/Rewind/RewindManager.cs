using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

// 显式声明（与 asmdef 的 internalsVisibleTo 字段双保险）：
// 让 Edit Mode 测试程序集能访问 internal 成员（RecordStep / GetBuffer）。
[assembly: InternalsVisibleTo("Chronobreak.Tests")]

/// <summary>
/// 回溯管理器（T-020）
///
/// 全局单例：统一管理所有 IRewindable 的注册/注销与录制循环。
/// - 录制：每 FixedUpdate（决策 1：50Hz）对全部已注册对象调用 CaptureSnapshot 写入各自缓冲
/// - 缓冲：每对象一个 RingBuffer&lt;FrameSnapshot&gt;（决策 1：300 帧 = 50Hz × 6s，覆盖 5s 上限 + 余量）
/// - 决策 6（暂停不录制）天然成立：timeScale=0 时 FixedUpdate 停摆，无需额外代码
///
/// 调用方约定（对应后续任务）：
/// - T-021 PlayerRewind：Awake 里 Register
/// - T-022 清理规则：死亡重生/切场景 ClearAll；敌人死亡 Unregister
/// - T-023 回放管线：StartRewind / RewindStep / StopRewind 读缓冲
/// </summary>
public class RewindManager : MonoBehaviour
{
    /// <summary>单例缓冲容量（决策 1：50Hz × 6s = 300 帧，覆盖 5s 回溯上限 + 1s 余量）</summary>
    public const int BufferCapacity = 300;

    private static RewindManager instance;

    /// <summary>
    /// 单例访问：首次访问时自动创建 GameObject（场景无需手动挂载 RewindManager）。
    /// </summary>
    public static RewindManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("RewindManager");
                instance = go.AddComponent<RewindManager>();
                Object.DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private readonly Dictionary<IRewindable, RingBuffer<FrameSnapshot>> buffers = new();

    /// <summary>已注册对象数（调试用）</summary>
    public int RegisteredCount => buffers.Count;

    /// <summary>注册一个可回溯对象，为其分配独立缓冲（重复注册幂等）</summary>
    public void Register(IRewindable target)
    {
        if (target == null) return;
        if (buffers.ContainsKey(target)) return; // 幂等：重复注册不产生第二个缓冲
        buffers.Add(target, new RingBuffer<FrameSnapshot>(BufferCapacity));
    }

    /// <summary>注销一个可回溯对象并丢弃其缓冲（决策 5：敌人死亡后不参与回溯）</summary>
    public void Unregister(IRewindable target)
    {
        if (target == null) return;
        buffers.Remove(target);
    }

    /// <summary>清空所有缓冲（决策 5：死亡重生 / 切场景时调用，防止回溯到死亡之前）</summary>
    public void ClearAll()
    {
        foreach (RingBuffer<FrameSnapshot> buffer in buffers.Values)
            buffer.Clear();
    }

    /// <summary>每物理帧录制全部已注册对象（决策 1：FixedUpdate 50Hz；决策 6：暂停时自然停摆）</summary>
    private void FixedUpdate()
    {
        RecordStep();
    }

    // ============================================================
    // 切场景清空（决策 5）
    // ============================================================

    private void OnEnable()
    {
        // 决策 5：切场景清空全部缓冲，避免跨场景残留旧时间线
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearAll();
    }

    /// <summary>
    /// 销毁时清空静态引用，防止编辑器域重载/重复创建残留实例。
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// 退出 Play Mode / 应用退出前销毁自身：
    /// 本对象是运行中创建的 DontDestroyOnLoad 对象，Unity 6 场景关闭检查会对其报警
    /// （"Some objects were not cleaned up"），在退出前显式销毁可消除该警告。
    /// </summary>
    private void OnApplicationQuit()
    {
        if (instance == this)
            DestroyImmediate(gameObject);
    }

    /// <summary>
    /// 录制一步：遍历全部对象 CaptureSnapshot → 写入各自缓冲。
    /// internal：由 FixedUpdate 驱动，并暴露给 Edit Mode 单测（Chronobreak.Tests）直接驱动。
    /// </summary>
    internal void RecordStep()
    {
        foreach (KeyValuePair<IRewindable, RingBuffer<FrameSnapshot>> pair in buffers)
        {
            FrameSnapshot snapshot = pair.Key.CaptureSnapshot();
            pair.Value.Write(snapshot);
        }
    }

    /// <summary>获取指定对象的缓冲（回放管线 T-023 使用；测试用于断言录制结果）</summary>
    internal RingBuffer<FrameSnapshot> GetBuffer(IRewindable target)
    {
        buffers.TryGetValue(target, out RingBuffer<FrameSnapshot> buffer);
        return buffer;
    }
}
