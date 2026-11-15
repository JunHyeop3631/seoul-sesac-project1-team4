using UnityEngine;

public sealed class AllyRangeDisplay : MonoBehaviour
{
    [SerializeField] private Sprite attackRangeSprite;

    private Ally rangeAlly;
    private SpriteRenderer attackRangeMarker;

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        if (attackRangeMarker != null)
        {
            Destroy(attackRangeMarker.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (rangeAlly == null || !rangeAlly.isActiveAndEnabled || rangeAlly.AttackRange <= 0f)
        {
            Hide();
            return;
        }

        UpdateAttackRangeMarker();
    }

    public void Show(Ally ally)
    {
        if (!isActiveAndEnabled || ally == null || !ally.isActiveAndEnabled ||
            attackRangeSprite == null)
        {
            return;
        }

        if (attackRangeMarker == null)
        {
            GameObject markerObject = new GameObject("Attack_Range");
            attackRangeMarker = markerObject.AddComponent<SpriteRenderer>();
            attackRangeMarker.sprite = attackRangeSprite;
            attackRangeMarker.color = new Color(1f, 1f, 1f, 0.3f);
        }

        rangeAlly = ally;
        UpdateAttackRangeMarker();
        attackRangeMarker.gameObject.SetActive(ally.AttackRange > 0f);
    }

    public void Hide()
    {
        rangeAlly = null;
        if (attackRangeMarker != null)
        {
            attackRangeMarker.gameObject.SetActive(false);
        }
    }

    private void UpdateAttackRangeMarker()
    {
        Bounds spriteBounds = attackRangeMarker.sprite.bounds;
        float diameter = rangeAlly.AttackRange * 2f;
        Vector3 scale = new Vector3(diameter / spriteBounds.size.x, diameter / spriteBounds.size.y, 1f);
        attackRangeMarker.transform.localScale = scale;
        // Account for the imported sprite pivot so its visual center matches the ally.
        attackRangeMarker.transform.position = rangeAlly.transform.position -
                                               Vector3.Scale(spriteBounds.center, scale);

        SpriteRenderer allyRenderer = rangeAlly.GetComponent<SpriteRenderer>();
        if (allyRenderer != null)
        {
            attackRangeMarker.sortingLayerID = allyRenderer.sortingLayerID;
            attackRangeMarker.sortingOrder = allyRenderer.sortingOrder - 1;
        }
    }

}
