using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SKYNET {
    
public static class NumericStepperMenu {
    private const string MenuPath = "GameObject/skynetUI/Numeric Stepper";

    [MenuItem(MenuPath, false, 10)]
    private static void CreateNumericStepper(MenuCommand menuCommand) {
        // 1) 找一个合适的父物体（优先用右键的上下文对象/当前选择）
        GameObject context = menuCommand.context as GameObject;
        Transform parent = null;

        if (context != null)
            parent = context.transform;

        // 如果父物体不在 Canvas 下，就确保场景里有 Canvas，并把父物体改成 Canvas
        Canvas canvas = FindParentCanvas(parent);
        if (canvas == null) {
            canvas = EnsureCanvas();
            parent = canvas.transform;
        }

        EnsureEventSystem();

        // 2) 创建根物体
        GameObject root = new("NumericStepper");
        Undo.RegisterCreatedObjectUndo(root, "Create NumericStepper");
        GameObjectUtility.SetParentAndAlign(root, parent.gameObject);
        root.SetActive(false);

        // 再补组件（inactive 状态下不会触发 OnEnable）
        root.AddComponent<RectTransform>();
        var stepper = root.AddComponent<NumericStepper>();

        RectTransform rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0.5f);
        rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.pivot = new Vector2(0.5f, 0.5f);
        rootRT.sizeDelta = new Vector2(260f, 40f);

        // 让 Selectable 有个 targetGraphic（不然一些状态过渡会显得怪）
        // var rootImage = root.GetComponent<Image>();
        // rootImage.raycastTarget = true;

        // 3) 创建三个子物体：lower, value, upper（注意顺序：lower -> value -> upper）
        GameObject lower = CreateChild(root.transform, "lower", addImage: true, addTMP: false);
        GameObject value = CreateChild(root.transform, "value", addImage: false, addTMP: true);
        GameObject upper = CreateChild(root.transform, "upper", addImage: true, addTMP: false);

        // 4) 按 0.2 / 0.6 / 0.2 的比例横向铺满，且高度撑满
        SetupChildRect(lower.GetComponent<RectTransform>(), 0.0f, 0.2f);
        SetupChildRect(value.GetComponent<RectTransform>(), 0.2f, 0.8f);
        SetupChildRect(upper.GetComponent<RectTransform>(), 0.8f, 1.0f);

        // 让 value 文本居中显示（可按需改字体大小/颜色）
        var tmp = value.GetComponent<TMP_Text>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        // 5) 自动绑定 NumericStepper 的序列化字段
        // var stepper = root.GetComponent<NumericStepper>();

        // 用 SerializedObject 才能设置 private [SerializeField] 字段
        SerializedObject so = new(stepper);

        // 字段名要和你脚本里 private 变量名一致（m_valueDisplay / m_DownHandleRect / m_UpHandleRect）
        so.FindProperty("m_valueDisplay").objectReferenceValue = tmp;
        so.FindProperty("m_DownHandleRect").objectReferenceValue = lower.GetComponent<RectTransform>();
        so.FindProperty("m_UpHandleRect").objectReferenceValue = upper.GetComponent<RectTransform>();

        so.ApplyModifiedPropertiesWithoutUndo();

        // 同时把 Selectable 的 targetGraphic 指到根 Image（可选）
        {
            SerializedObject soSelectable = new SerializedObject(stepper);
            // soSelectable.FindProperty("m_TargetGraphic").objectReferenceValue = rootImage;
            soSelectable.FindProperty("m_Transition").enumValueIndex = (int)Selectable.Transition.None;
            soSelectable.ApplyModifiedPropertiesWithoutUndo();
        }

        // 最后激活，让 OnEnable 在引用已绑定的情况下执行
        root.SetActive(true);

        // 选中创建出来的对象
        Selection.activeGameObject = root;
    }

    private static GameObject CreateChild(Transform parent, string name, bool addImage, bool addTMP) {
        GameObject go;

        if (addTMP) {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        }
        else if (addImage) {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        }
        else {
            go = new GameObject(name, typeof(RectTransform));
        }

        Undo.RegisterCreatedObjectUndo(go, "Create NumericStepper Child");
        go.transform.SetParent(parent, false);

        if (addImage) {
            var img = go.GetComponent<Image>();
            img.raycastTarget = true;
        }

        return go;
    }

    // xMin ~ xMax 是锚点比例：比如 lower (0,0.2)，value (0.2,0.8)，upper (0.8,1)
    private static void SetupChildRect(RectTransform rt, float xMin, float xMax) {
        rt.anchorMin = new Vector2(xMin, 0f);
        rt.anchorMax = new Vector2(xMax, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Canvas FindParentCanvas(Transform t) {
        if (t == null) return null;
        return t.GetComponentInParent<Canvas>();
    }

    private static Canvas EnsureCanvas() {
        Canvas existing = Object.FindAnyObjectByType<Canvas>();
        if (existing != null) return existing;

        GameObject canvasGO = new("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return canvas;
    }

    private static void EnsureEventSystem() {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        GameObject es = new("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }
}
}
