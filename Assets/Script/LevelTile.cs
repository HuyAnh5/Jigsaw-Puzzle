using UnityEngine;
using TMPro;

public class LevelTile : MonoBehaviour
{
    public SpriteRenderer imageRenderer;
    public Sprite hiddenSprite;
    public TextMeshPro numberText;

    private int _levelIndex;
    private bool _isRevealed;

    public void Setup(int levelIndex, bool isRevealed, Sprite pieceSprite)
    {
        _levelIndex = levelIndex;
        _isRevealed = isRevealed;

        numberText.text = levelIndex.ToString();

        if (isRevealed)
        {
            imageRenderer.sprite = pieceSprite;
        }
        else
        {
            imageRenderer.sprite = hiddenSprite;
        }
    }

    private void OnMouseUpAsButton()
    {
        // không cho chọn level bằng cách click ô
    }
}
