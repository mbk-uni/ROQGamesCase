using UnityEngine;

public class StickerHolder : MonoBehaviour
{
    [SerializeField] private StickerType stickerType;

    public StickerType StickerType => stickerType;
}
