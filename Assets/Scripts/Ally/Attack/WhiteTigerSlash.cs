using UnityEngine;

// Visual only: the strike snapshot in WhiteTiger applies all damage immediately.
public sealed class WhiteTigerSlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.01f)] private float duration = 0.6f;
    private float elapsed, range;
    private Vector2 origin, direction;
    public bool IsPlaying { get; private set; }

    public void Play(Vector2 start, Vector2 forward, float radius)
    {
        if (visual == null || frames == null || frames.Length == 0) return;
        origin = start; direction = forward.normalized; range = radius; elapsed = 0f;
        IsPlaying = true; visual.enabled = true;
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        ShowFrame();
    }

    private void Update()
    {
        if (!IsPlaying || Time.timeScale <= 0f) return;
        elapsed += Time.deltaTime;
        if (elapsed >= duration) Stop();
        else ShowFrame();
    }

    private void ShowFrame()
    {
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
        visual.sprite = frames[Mathf.Min((int)(progress * frames.Length), frames.Length - 1)];
        transform.position = origin + direction * Mathf.Lerp(.3f, range * .75f, progress);
    }

    public void Stop() { IsPlaying = false; if (visual != null) visual.enabled = false; }
    private void OnDisable() => Stop();
}
