using UnityEngine;
using DG.Tweening;

public class CardMover : MonoBehaviour
{
    public void MoveToUI(RectTransform target)
    {
        RectTransform rect = GetComponent<RectTransform>();
        rect.DOAnchorPos(target.anchoredPosition, 0.4f)
        .SetEase(Ease.OutBack);
    }

    public void MoveToWorld(Transform target)
    {
        transform.DOMove(target.position, 0.4f)
        .SetEase(Ease.OutBack);
    }
}