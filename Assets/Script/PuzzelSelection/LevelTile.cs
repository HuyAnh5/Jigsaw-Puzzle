using UnityEngine;
using TMPro;
using DG.Tweening;

public class LevelTile : MonoBehaviour
{
    [Header("Renderers")]
    public SpriteRenderer frontRenderer;   // mặt trước
    public SpriteRenderer backRenderer;    // mặt sau (úp)

    [Header("Sprites")]
    public Sprite hiddenSprite;           // hình úp (mặt sau)

    [Header("Text (đặt ở mặt sau)")]
    public TextMeshPro numberText;        // gán BackText vào đây

    [Header("Flip Settings")]
    [SerializeField] private float flipDuration = 0.5f;

    private int _levelIndex;
    private bool _isRevealed;
    private Sprite _pieceSprite;          // sprite thật của mảnh
    private float _currentY;              // góc Y hiện tại của LevelTile (parent)

    /// <summary>
    /// Khởi tạo ô level.
    /// isRevealed = true  -> đang lật sẵn (mặt trước).
    /// isRevealed = false -> đang úp (mặt sau).
    /// </summary>
    public void Setup(int levelIndex, bool isRevealed, Sprite pieceSprite)
    {
        _levelIndex = levelIndex;
        _isRevealed = isRevealed;
        _pieceSprite = pieceSprite;

        if (numberText != null)
        {
            numberText.text = levelIndex.ToString();
        }

        // Gán sprite cho 2 mặt
        if (frontRenderer != null)
        {
            frontRenderer.sprite = _pieceSprite;
        }

        if (backRenderer != null)
        {
            backRenderer.sprite = hiddenSprite;
        }

        // Đặt góc quay ban đầu của LevelTile (parent)
        if (_isRevealed)
        {
            _currentY = 0f;        // thấy mặt trước
        }
        else
        {
            _currentY = 180f;      // thấy mặt sau
        }

        transform.localEulerAngles = new Vector3(0f, _currentY, 0f);
    }

    public void RevealWithFlip()
    {
        if (_isRevealed)
            return;    // lật rồi thì không lật nữa

        _isRevealed = true;

        // Mỗi lần lật quay thêm 180 độ quanh trục Y
        _currentY += 180f;

        transform
            .DOLocalRotate(new Vector3(0f, _currentY, 0f), flipDuration, RotateMode.Fast)
            .SetEase(Ease.InOutQuad);
    }
}
