using UnityEngine;
using UnityEngine.UI;

public sealed class AllyActionButtons : MonoBehaviour
{
    [SerializeField, Min(0f)] private float gapBelowAlly = 8f;
    [SerializeField] private YutActionController actionController;
    [SerializeField] private Button evolveButton;

    private Ally target;
    private SpriteRenderer targetRenderer;

    public void SetTarget(Ally ally)
    {
        target = ally;
        targetRenderer = ally != null ? ally.VisualRenderer : null;
        UpdatePosition();
        UpdateEvolutionButton();
    }

    private void LateUpdate()
    {
        if (target != null && !target.isActiveAndEnabled)
        {
            gameObject.SetActive(false);
            return;
        }
        UpdatePosition();
        UpdateEvolutionButton();
    }

    public void RequestEvolution()
    {
        if (actionController != null)
        {
            actionController.RequestEvolution(target);
        }
    }

    private void UpdateEvolutionButton()
    {
        if (evolveButton != null)
        {
            evolveButton.interactable = actionController != null && actionController.CanEvolve(target);
        }
    }

    private void OnDisable()
    {
        target = null;
        targetRenderer = null;
    }

    private void UpdatePosition()
    {
        Camera boardCamera = Camera.main;
        if (target == null || boardCamera == null ||
            !(transform.parent is RectTransform parentRect))
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Vector3 worldPosition = target.transform.position;
        if (targetRenderer != null)
        {
            Bounds bounds = targetRenderer.bounds;
            worldPosition = new Vector3(bounds.center.x, bounds.min.y, worldPosition.z);
        }

        Vector2 screenPosition = boardCamera.WorldToScreenPoint(worldPosition);
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, screenPosition, uiCamera, out Vector2 localPosition))
        {
            localPosition.y -= gapBelowAlly;
            transform.position = parentRect.TransformPoint(localPosition);
        }
    }
}
