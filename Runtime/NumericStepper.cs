using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SKYNET {
    
public class NumericStepper : Selectable {
    // 预先缓存不同 precision 的格式串，避免运行时拼 format
    private static readonly string[] formats =
    {
        "{0}",             // 0
        "{0}.{1:0}",       // 1
        "{0}.{1:00}",      // 2
        "{0}.{1:000}",     // 3
        "{0}.{1:0000}",    // 4
        "{0}.{1:00000}",   // 5
    };
    private static readonly string[] formats_neg =
    {
        "-{0}",             // 0
        "-{0}.{1:0}",       // 1
        "-{0}.{1:00}",      // 2
        "-{0}.{1:000}",     // 3
        "-{0}.{1:0000}",    // 4
        "-{0}.{1:00000}",   // 5
    };
    private static readonly int[] _pow10 =
    {1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000};

    [Serializable]
    public class NumericStepperEvent : UnityEvent<float> { }

    [SerializeField, InspectorLabel("数值显示")]
    private TMP_Text m_valueDisplay; // 数值回显

    [SerializeField, InspectorLabel("值(float)")]
    private float m_valueFloat = 0.0f; // 浮点数值

    [SerializeField, InspectorLabel("最小值(float)")]
    private float m_minValueFloat = 0.0f; // 最小值

    [SerializeField, InspectorLabel("最大值(float)")]
    private float m_maxValueFloat = 1.0f; // 最大值

    [SerializeField, InspectorLabel("小数位数"), Range(0, 5)]
    private int m_precision = 0;

    [SerializeField, InspectorLabel("步进")]
    private float m_stepsFloat = 1.0f;

    [SerializeField, InspectorLabel("OnValueChanged")]
    private NumericStepperEvent m_OnValueChanged = new();

    [SerializeField, InspectorLabel("降低")]
    private RectTransform m_DownHandleRect;

    [SerializeField, InspectorLabel("增加")]
    private RectTransform m_UpHandleRect;

    [SerializeField, InspectorLabel("长按阈值(单位:s)"), Range(0.2f, 0.4f)]
    private float m_longPressThreshhold = 0.2f;

    [SerializeField, InspectorLabel("长按步进间隔(单位:s)"), Range(0.1f, 0.3f)]
    private float m_longPressRepeatInterval = 0.1f;

    private int _valueInt; // 按整数存
    private int _minValueInt;
    private int _maxValueInt;
    private int _stepsInt;
    private int _currentPointerId = int.MinValue;
    private int _sgn; // 值变化的方向, 1增加, -1减少
    private bool _hasClamped; private int _clampedStep; // 是否撞到过边界, 因为撞到边界, 实际走了多久
    private Coroutine _longPressCo;

    public float Value {
        get { return _valueInt / (float)_pow10[m_precision]; } // 按浮点数读
        set {
            int intValue = Mathf.RoundToInt(value * _pow10[m_precision]);
            intValue = Mathf.Clamp(intValue, _minValueInt, _maxValueInt); // 超出范围直接钳制
            if (intValue == _valueInt)
                return;
            _valueInt = intValue;
            m_valueFloat = _valueInt / (float)_pow10[m_precision];
            RefreshUI();
            m_OnValueChanged.Invoke(_valueInt / (float)_pow10[m_precision]);
        }
    }

    public float MinValue {
        get => m_minValueFloat;
    }

    public float MaxValue {
        get => m_maxValueFloat;
    }

    public NumericStepperEvent OnValueChanged {
        get => m_OnValueChanged;
    }

    public void SetValueWithoutNotify(float value) {
        int intValue = Mathf.RoundToInt(value * _pow10[m_precision]);
        intValue = Mathf.Clamp(intValue, _minValueInt, _maxValueInt);
        if (intValue == _valueInt)
            return;
        _valueInt = intValue;
        m_valueFloat = _valueInt / (float)_pow10[m_precision];
        RefreshUI();
    }

    protected override void OnEnable() {
        base.OnEnable();

        ConfigStepper(m_minValueFloat, m_maxValueFloat, m_stepsFloat, m_precision, m_valueFloat);
        RefreshUI();
    }

    public void ConfigStepper(float min, float max, float step, int precision, float value) {
        int lastPrecision = m_precision;
        int lastValueInt = _valueInt;

        m_minValueFloat = min; m_maxValueFloat = max; m_valueFloat = value;
        m_precision = Mathf.Clamp(precision, 0, 5);
        _stepsInt = Mathf.RoundToInt(step * _pow10[m_precision]);

        if (_stepsInt < 1) {
            Debug.LogWarning("调节步长不能为0和负数, 已自动修正为1");
            _stepsInt = 1;
        }
        _minValueInt = Mathf.RoundToInt(m_minValueFloat * _pow10[m_precision]);
        _maxValueInt = Mathf.RoundToInt(m_maxValueFloat * _pow10[m_precision]);
        _valueInt = Mathf.RoundToInt(m_valueFloat * _pow10[m_precision]);
        if (_minValueInt > _maxValueInt) {
            Debug.LogError("最小值不能超过最大值, 已交换");
            (m_minValueFloat, m_maxValueFloat) = (m_maxValueFloat, m_minValueFloat);
            (_minValueInt, _maxValueInt) = (_maxValueInt, _minValueInt);
        }
        _valueInt = Mathf.Clamp(_valueInt, _minValueInt, _maxValueInt);
        m_valueFloat = _valueInt / (float)_pow10[m_precision];
        interactable = true;
        if (_minValueInt == _maxValueInt) {
            Debug.LogError("范围低于最小格数, 控件不可交互");
            interactable = false;
        }
        if (_valueInt != lastValueInt || m_precision != lastPrecision)
            RefreshUI();
    }

    protected override void OnDisable() {
        base.OnDisable();
        if (_longPressCo != null) {
            StopCoroutine(_longPressCo);
            _longPressCo = null;
        }
    }

    private void RefreshUI() {
        int intPart = _valueInt / _pow10[m_precision];
        if (intPart < 0) intPart = -intPart;
        int fracPart = _valueInt % _pow10[m_precision];
        if (fracPart < 0) fracPart = -fracPart;
        if (_valueInt >= 0)
            m_valueDisplay.SetText(formats[m_precision], intPart, fracPart);
        else
            m_valueDisplay.SetText(formats_neg[m_precision], intPart, fracPart);
    }
    private void UpdateValueInt() {
        if (_sgn > 0) {
            if (_valueInt == _maxValueInt) return;
            int delta = _stepsInt;
            if (_hasClamped) {
                delta = _clampedStep;
                _hasClamped = false;
            }
            if (delta > _maxValueInt - _valueInt) {
                delta = _maxValueInt - _valueInt;
                _hasClamped = true;
                _clampedStep = delta;
            }
            _valueInt += delta;
        }
        else if (_sgn < 0) {
            if (_valueInt == _minValueInt) return;
            int delta = _stepsInt;
            if (_hasClamped) {
                delta = _clampedStep;
                _hasClamped = false;
            }
            if (delta > _valueInt - _minValueInt) {
                delta = _valueInt - _minValueInt;
                _hasClamped = true;
                _clampedStep = delta;
            }
            _valueInt -= delta;
        }
        else return;
        RefreshUI();
        m_OnValueChanged.Invoke(_valueInt / (float)_pow10[m_precision]);
    }

    private bool MayClick(PointerEventData eventData) {
        if (IsActive() && IsInteractable()) {
            return eventData.button == PointerEventData.InputButton.Left;
        }
        return false;
    }
    public override void OnPointerDown(PointerEventData eventData) {
        if (!MayClick(eventData))
            return;
        base.OnPointerDown(eventData);

        // 点到了减少, 且当前不处于按下状态
        if (m_DownHandleRect != null
        && RectTransformUtility.RectangleContainsScreenPoint(m_DownHandleRect, eventData.pointerPressRaycast.screenPosition, eventData.enterEventCamera)
        && _longPressCo == null) {
            _sgn = -1; _currentPointerId = eventData.pointerId;
            UpdateValueInt(); // 按下瞬间立即更新
            _longPressCo = StartCoroutine(LongPress());
        }
        // 点到了增加, 且当前不处于按下状态
        else if (m_UpHandleRect != null
        && RectTransformUtility.RectangleContainsScreenPoint(m_UpHandleRect, eventData.pointerPressRaycast.screenPosition, eventData.enterEventCamera)
        && _longPressCo == null) {
            _sgn = 1; _currentPointerId = eventData.pointerId;
            UpdateValueInt(); // 按下瞬间立即更新
            _longPressCo = StartCoroutine(LongPress());
        }
    }
    public override void OnPointerUp(PointerEventData eventData) {
        base.OnPointerUp(eventData);

        // 按下减少/增加并成功启动减少/增加过程的那根手指抬起
        if (eventData.pointerId == _currentPointerId && _longPressCo != null) {
            StopCoroutine(_longPressCo);
            _longPressCo = null;
        }

    }
    private IEnumerator LongPress() {
        float t = 0f;
        while (t < m_longPressThreshhold) {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        while (true) {
            UpdateValueInt();
            t = 0f;
            while (t < m_longPressRepeatInterval) {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
}