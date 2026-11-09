using TMPro;
using UnityEngine;

public class HeadTrackedText : MonoBehaviour
{
    private static readonly Color DefaultMessageColor = Color.white;

    [SerializeField] private TMP_Text text;
    [SerializeField] private Transform head;
    [SerializeField] private Vector3 offset = new Vector3(0f, -0.15f, 1.5f);
    [SerializeField, Min(0.01f)] private float followSpeed = 8f;
    [Tooltip("Time fully visible, excluding the fades.")]
    private float displayDuration = 3f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.3f;
    [SerializeField] private bool showOnEnable = true;
    private float elapsed;
    private float originalAlpha;
    private bool showing;

    public bool IsShowing => showing;

    private void Awake()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
        if (text != null)
        {
            originalAlpha = text.alpha;
            text.alpha = 0f;
        }
    }

    private void OnEnable()
    {
        if (showOnEnable)
            Show();
    }

    // Can also be connected to a UnityEvent in the Inspector.
    public void Show()
    {
        if (text == null || head == null)
            return;

        elapsed = 0f;
        showing = true;
        text.alpha = fadeDuration > 0f ? 0f : originalAlpha;
        text.transform.SetPositionAndRotation(head.position + head.rotation * offset, head.rotation);
    }

    public void ShowMessage(string message)
    {
        if (text == null)
            return;
        text.text = message;
        text.color = DefaultMessageColor;
        Show();
    }

    public void ShowMessage(string message, Color? color = null, float time = 3f, float spacing = 1.5f)
    {
        if (text == null)
            return;
        text.text = message;
        text.color = color ?? DefaultMessageColor;
        displayDuration = time;
        offset = new Vector3(0f, -0.15f, spacing);
        Show();
    }

    private void LateUpdate()
    {
        if (!showing || text == null || head == null)
            return;

        float blend = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
        text.transform.position = Vector3.Lerp(text.transform.position, head.position + head.rotation * offset, blend);
        text.transform.rotation = Quaternion.Slerp(text.transform.rotation, head.rotation, blend);

        elapsed += Time.unscaledDeltaTime;
        float fade = Mathf.Max(0f, fadeDuration);
        float end = fade * 2f + Mathf.Max(0f, displayDuration);
        if (elapsed >= end)
        {
            Hide();
            return;
        }

        float alpha = fade > 0f
            ? Mathf.Min(Mathf.Clamp01(elapsed / fade), Mathf.Clamp01((end - elapsed) / fade))
            : 1f;
        text.alpha = originalAlpha * alpha;
    }

    public void Hide()
    {
        showing = false;
        if (text != null)
            text.alpha = 0f;
    }

    private void OnDisable()
    {
        Hide();
    }
}
