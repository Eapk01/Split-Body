using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    Slider slider;

    [SerializeField] Image fillImage;

    void Start()
    {
        slider = GetComponent<Slider>();
        slider.value = slider.maxValue;
        gameObject.SetActive(false);
    }

    public void UpdateSlider(float amount)
    {
        slider.value = amount;

        float normalized = slider.value / slider.maxValue;

        // Color
        fillImage.color = Color.Lerp(Color.red, Color.green, normalized);

        // Hide if full
        gameObject.SetActive(normalized < 1f);
    }
}