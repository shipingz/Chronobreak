using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
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

    /// <summary>是否正在退出 Play Mode / 应用退出（退出期间禁止重建单例，见 Instance getter）</summary>
    private static bool applicationIsQuitting;

    /// <summary>
    /// 单例访问：首次访问时自动创建 GameObject（场景无需手动挂载 RewindManager）。
    /// 退出保护：applicationIsQuitting 为 true（退出 Play Mode / 应用退出流程）时返回 null，
    /// 防止场景关闭期间其他对象的 OnDestroy 访问本属性触发"自愈重建"——
    /// 重建会制造场景关闭检查（"Some objects were not cleaned up"）能看到的残留对象。
    /// </summary>
    public static RewindManager Instance
    {
        get
        {
            if (applicationIsQuitting) return null;
            if (instance == null)
            {
                GameObject go = new GameObject("RewindManager");
                instance = go.AddComponent<RewindManager>();
                Object.DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    /// <summary>
    /// 每次进入 Play Mode 前重置静态状态（编辑器域重载/重复进入 Play Mode 时，
    /// 静态字段在编辑会话间保留，需重置保证干净创建）。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeStatics()
    {
        applicationIsQuitting = false;
        instance = null;
    }

    /// <summary>是否存在活跃实例（只读判断，不触发创建——供 OnDestroy 等退出流程安全使用）</summary>
    public static bool Exists => instance != null;

    /// <summary>
    /// 置位退出保护：供编辑器退出流程（ExitingPlayMode，早于 Application.quitting 事件）
    /// 调用，确保场景关闭期间 Instance getter 返回 null、绝不重建。
    /// </summary>
    public static void MarkApplicationQuitting()
    {
        applicationIsQuitting = true;
    }

    /// <summary>退出流程开始：置位退出保护（退出 Play Mode / 应用退出都会触发 Application.quitting）</summary>
    private static void OnApplicationQuitting()
    {
        applicationIsQuitting = true;
    }

    private readonly Dictionary<IRewindable, RingBuffer<FrameSnapshot>> buffers = new();

    /// <summary>已注册对象数（调试用）</summary>
    public int RegisteredCount => buffers.Count;

    // ============================================================
    // 回放状态（T-023）
    // ============================================================

    /// <summary>当前是否正在回溯（各组件查询此标记，如 PlayerHealth 免疫伤害 T-029）</summary>
    public bool IsRewinding { get; private set; }

    /// <summary>已回退的帧数（0 = 最新帧；每 RewindStep +1）</summary>
    private int rewindOffset;

    // ============================================================
    // 回放管线（T-023）
    // ============================================================

    /// <summary>
    /// 开始回溯：遍历全部对象调 OnRewindStart（PlayerRewind 冻结控制组件）。
    /// 防御：空缓冲 / 已在回溯中 → 直接返回。
    /// </summary>
    public void StartRewind()
    {
        if (IsRewinding) return;
        if (buffers.Count == 0) return;

        IsRewinding = true;
        rewindOffset = 0;

        foreach (KeyValuePair<IRewindable, RingBuffer<FrameSnapshot>> pair in buffers)
        {
            pair.Key.IsRewinding = true;
            pair.Key.OnRewindStart();
        }
    }

    /// <summary>
    /// 回放一步（决策 1/2：每 FixedUpdate 退一帧，1:1）：从最新往旧读一帧并应用。
    /// 各对象缓冲帧数可能不同，用 clamp 停在各自最旧帧（决策：允许部分回溯）。
    /// </summary>
    internal void RewindStep()
    {
        foreach (KeyValuePair<IRewindable, RingBuffer<FrameSnapshot>> pair in buffers)
        {
            RingBuffer<FrameSnapshot> buffer = pair.Value;
            if (buffer.Count == 0) continue;

            int offset = Mathf.Min(rewindOffset, buffer.Count - 1); // 退到最旧帧后停住
            pair.Key.ApplySnapshot(buffer.Read(offset));
        }
        rewindOffset++;
    }

    /// <summary>
    /// 停止回溯：恢复对象控制、截断被消费的"旧未来"帧（决策 4）、重置偏移。
    /// 停止帧血量恢复、重叠弹飞+无敌、SyncTransforms 属 T-028 收尾，本次不处理。
    /// </summary>
    public void StopRewind()
    {
        if (!IsRewinding) return;

        IsRewinding = false;

        foreach (KeyValuePair<IRewindable, RingBuffer<FrameSnapshot>> pair in buffers)
        {
            // 决策 4：丢弃被回溯消费的帧（旧时间线），防止二次回溯闪现到撤销前的位置。
            // min 防越界：回溯到底（clamp 最旧）的对象 count 小于全局 rewindOffset，截断全部。
            pair.Value.Truncate(Mathf.Min(rewindOffset, pair.Value.Count));
            pair.Key.IsRewinding = false;
            pair.Key.OnRewindEnd();
        }

        rewindOffset = 0;
    }

    /// <summary>输入检测（T-023）：按住 R 回溯，松开停止。直接读键盘，不改 .inputactions 资产。</summary>
    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[Key.R].isPressed)
        {
            if (!IsRewinding) StartRewind();
        }
        else if (IsRewinding)
        {
            StopRewind();
        }
    }

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

    /// <summary>每物理帧：回溯中 → 回放一步；否则 → 录制一步（决策 6：暂停时自然停摆）</summary>
    private void FixedUpdate()
    {
        if (IsRewinding)
            RewindStep();
        else
            RecordStep();
    }

    // ============================================================
    // 切场景清空（决策 5）
    // ============================================================

    private void OnEnable()
    {
        // 决策 5：切场景清空全部缓冲，避免跨场景残留旧时间线
        SceneManager.sceneLoaded += OnSceneLoaded;
        // 退出保护：退出 Play Mode / 应用退出时置位，禁止 Instance getter 重建
        Application.quitting += OnApplicationQuitting;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.quitting -= OnApplicationQuitting;
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
