using UnityEngine;

public class LevelSelectBoardManager : MonoBehaviour
{
    [Header("Input")]
    public Texture2D worldTexture;
    public int rows = 5;
    public int cols = 5;
    public GameObject levelTilePrefab;
    public float pixelsPerUnit = 100f;

    [Header("Layout")]
    public Vector2 boardCenter = Vector2.zero;
    public float spacing = 0.05f;

    [Header("Debug")]
    public bool resetProgressOnStart = false;

    private void Start()
    {
        if (resetProgressOnStart)
        {
            LevelProgress.ResetProgress();
            Debug.Log("[LevelSelectBoardManager] Progress reset");
        }

        InitBoard();
    }

    private void InitBoard()
    {
        if (worldTexture == null || levelTilePrefab == null)
        {
            Debug.LogError("Missing worldTexture or levelTilePrefab");
            return;
        }

        int piecePixelWidth = worldTexture.width / cols;
        int piecePixelHeight = worldTexture.height / rows;

        float pieceWorldWidth = piecePixelWidth / pixelsPerUnit;
        float pieceWorldHeight = piecePixelHeight / pixelsPerUnit;

        float cellW = pieceWorldWidth + spacing;
        float cellH = pieceWorldHeight + spacing;

        Vector2 topLeft = boardCenter + new Vector2(
            -cellW * (cols - 1) / 2f,
             cellH * (rows - 1) / 2f
        );

        int maxCleared = LevelProgress.GetMaxLevelCleared();
        Debug.Log("[LevelSelectBoardManager] maxCleared = " + maxCleared);

        int levelIndex = 1;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Rect rect = new Rect(
                    c * piecePixelWidth,
                    (rows - 1 - r) * piecePixelHeight,
                    piecePixelWidth,
                    piecePixelHeight
                );

                Sprite sprite = Sprite.Create(
                    worldTexture,
                    rect,
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect
                );

                Vector3 pos = new Vector3(
                    topLeft.x + c * cellW,
                    topLeft.y - r * cellH,
                    0f
                );

                GameObject go = Instantiate(levelTilePrefab, pos, Quaternion.identity, transform);
                var tile = go.GetComponent<LevelTile>();

                bool isRevealed = levelIndex < maxCleared;
                bool isJustCleared = levelIndex == maxCleared;

                tile.Setup(levelIndex, isRevealed, sprite);

                if (isJustCleared)
                {
                    tile.RevealWithFlip();
                }

                levelIndex++;
            }
        }
    }
}
