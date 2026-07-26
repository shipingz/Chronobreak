using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 死亡画面 UI — 占位版（T-014）
///
/// 从 Resources/Prefabs/DeathUI 加载 Prefab，首次调用时实例化，之后复用。
/// 第 3 周 T-043 会升级为正式版。
///
/// 使用方式：DeathScreenUI.Show(playerHealth) 静态调用
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [Header("引用（在 Prefab Inspector 中拖拽赋值）")]
    [SerializeField] private Button restartButton;

    private static DeathScreenUI instance;
    private PlayerHealth target;

    // ============================================================
    // 公开入口
    // ============================================================

    /// <summary>
    /// 显示死亡画面。首次调用时从 Resources/Prefabs/ 加载，之后复用。
    /// 由 PlayerHealth 死亡协程调用。
    /// </summary>
    public static void Show(PlayerHealth playerHealth)
    {
        if (instance == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/DeathUI");
            if (prefab == null)
            {
                Debug.LogError("[DeathScreenUI] 找不到 Prefabs/DeathUI！请确认 Prefab 在 Resources/Prefabs/ 下");
                return;
            }

            GameObject go = Object.Instantiate(prefab);
            go.name = "DeathUI_Instance";

            // 强制 Canvas 全屏覆盖（防止 Prefab RectTransform 未正确设置）
            Canvas canvas = go.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RectTransform rect = canvas.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }

            Object.DontDestroyOnLoad(go);

            instance = go.GetComponent<DeathScreenUI>();
            if (instance == null)
            {
                Debug.LogError("[DeathScreenUI] Prefab 上缺少 DeathScreenUI 组件！");
                return;
            }
        }
        else
        {
            // 复用已有实例，确保状态干净
            instance.StopAllCoroutines();
        }

        instance.target = playerHealth;
        instance.BindButton();
        instance.gameObject.SetActive(true);

        Time.timeScale = 0f;
    }

    /// <summary>绑定按钮点击事件（每次重新绑定，防止残留）</summary>
    private void BindButton()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);
        }
        else
        {
            Debug.LogError("[DeathScreenUI] restartButton 未赋值！请在 Prefab Inspector 中拖拽");
        }
    }

    // ============================================================
    // 按钮回调
    // ============================================================

    private void OnRestartClicked()
    {
        Debug.Log("[DeathScreenUI] 点击重新开始按钮");

        Time.timeScale = 1f;

        if (target != null)
            target.Revive();
        else
            Debug.LogError("[DeathScreenUI] target 为空，无法重生！");

        // 隐藏复用，不销毁
        gameObject.SetActive(false);
    }
}
