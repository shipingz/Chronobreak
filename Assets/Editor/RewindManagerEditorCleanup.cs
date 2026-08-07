using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器专用清理（修复 Unity 6 退出 Play Mode 时的警告）：
/// "Some objects were not cleaned up ... RewindManager"
///
/// 原因：RewindManager 是运行中创建的 DontDestroyOnLoad 对象，Unity 6 的场景关闭检查
/// 在其销毁前扫描到它而报警（运行时 OnApplicationQuit 时序不可靠，拦不住）。
///
/// 方案：在 EditorApplication.playModeStateChanged 的 ExitingPlayMode 阶段
/// （早于场景关闭检查）主动销毁 RewindManager，让检查通过、警告消失。
/// 仅编辑器编译（Assets/Editor 目录），不影响游戏发布。
/// </summary>
public static class RewindManagerEditorCleanup
{
    /// <summary>编辑器加载时注册退出 Play Mode 回调（UnityEditor 的 InitializeOnLoadMethod）</summary>
    [InitializeOnLoadMethod]
    private static void RegisterPlayModeExitCleanup()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;

        // ① 先置位退出保护（早于 Application.quitting 事件）：场景关闭期间
        //    Instance getter 返回 null，其他对象的 OnDestroy 访问不会触发重建。
        RewindManager.MarkApplicationQuitting();

        // ② 销毁所有存活实例（正常只有一个，防御多实例）
        foreach (RewindManager rm in Object.FindObjectsByType<RewindManager>())
        {
            if (rm != null)
                Object.DestroyImmediate(rm.gameObject);
        }
    }
}
