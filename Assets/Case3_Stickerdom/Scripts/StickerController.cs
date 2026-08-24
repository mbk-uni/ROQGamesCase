using UnityEngine;

public class StickerController : MonoBehaviour
{
    
    [SerializeField] private StickerType stickerType;
    
}

public enum StickerType
{
    Hayvan,
    Meyve,
    Arac
}
