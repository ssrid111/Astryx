using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpaceBackgroundMotion : MonoBehaviour
{
    [SerializeField, Min(0f)] private float horizontalDistance = 0.2f;
    [SerializeField, Min(0f)] private float verticalDistance = 0.08f;
    [SerializeField, Min(1f)] private float cycleSeconds = 30f;
    [SerializeField, Min(1f)] private float baseZoom = 1.08f;
    [SerializeField, Range(0f, 0.1f)] private float zoomAmount = 0.015f;

    private Vector3 originalPosition;
    private Vector3 originalScale;
    private float elapsed;

    private void OnEnable()
    {
        originalPosition = transform.localPosition;
        originalScale = transform.localScale;
        elapsed = 0f;
        ApplyMotion();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        ApplyMotion();
    }

    private void ApplyMotion()
    {
        float phase = elapsed * (2f * Mathf.PI / Mathf.Max(1f, cycleSeconds));
        transform.localPosition = originalPosition + new Vector3(
            Mathf.Sin(phase) * horizontalDistance,
            Mathf.Sin(phase * 0.7f) * verticalDistance,
            0f);

        // Zoom only inward from the enlarged base to keep the image edges covered.
        float zoom = baseZoom + (0.5f - 0.5f * Mathf.Cos(phase)) * zoomAmount;
        transform.localScale = new Vector3(
            originalScale.x * zoom, originalScale.y * zoom, originalScale.z);
    }

    private void OnDisable()
    {
        transform.localPosition = originalPosition;
        transform.localScale = originalScale;
    }
}
