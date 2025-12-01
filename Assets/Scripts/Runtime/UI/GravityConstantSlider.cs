// using TMPro;
// using UnityEngine;
// using UnityEngine.UI;

// public class GravityConstantSlider : MonoBehaviour
// {
//     public GravityMan gravityManager;
//     public Slider gravitySlider;
//     public TextMeshProUGUI valueText;

//     void Start()
//     {
//         if (gravityManager == null)
//             gravityManager = GravityMan.Instance;
//         if (gravitySlider != null)
//         {
//             gravitySlider.minValue = 0.1f;
//             gravitySlider.maxValue = 2000f;
//             gravitySlider.value = gravityManager.GravityConstant;
//             gravitySlider.onValueChanged.AddListener(OnSliderChanged);
//             UpdateValueText(gravitySlider.value);
//         }
//     }

//     void OnSliderChanged(float value)
//     {
//         gravityManager.GravityConstant = value;
//         UpdateValueText(value);
//     }

//     void UpdateValueText(float value)
//     {
//         if (valueText != null)
//             valueText.text = $"Gravity Constant: {value:F1}";
//     }
// }