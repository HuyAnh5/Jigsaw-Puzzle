using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class PuzzleBoardManager : MonoBehaviour
{
    [HideInInspector] public Texture2D sourceTexture;
    public int rows = 3;
    public int cols = 3;
    public GameObject piecePrefab;
    public float pixelsPerUnit = 100f;

    [Header("Board Layout")]
    public Vector2 boardCenter = Vector2.zero;  // vị trí giữa khung
    public Vector2 boardSize = new Vector2(4f, 4f); // width, height world units

    private PuzzlePiece[,] _piecesBySlot;
    private float _cellWidth;
    private float _cellHeight;

    [Header("Piece Layout")]
    [Range(0.5f, 1.0f)]
    public float pieceScale = 0.9f;   // 0.9 = mỗi mảnh nhỏ hơn cell 10%

    [Header("Auto Layout")]
    public bool autoCenterOnCamera = true;

    private static readonly Vector2Int GridUp = new Vector2Int(-1, 0); // row - 1
    private static readonly Vector2Int GridDown = new Vector2Int(1, 0);  // row + 1
    private static readonly Vector2Int GridLeft = new Vector2Int(0, -1); // col - 1
    private static readonly Vector2Int GridRight = new Vector2Int(0, 1);  // col + 1

    [Header("Level")]
    public int levelIndex = 1;                 // số màn hiện tại

    [Header("UI")]
    public GameObject levelCompletePanel;

    [Header("Move Animation")]
    public float moveDuration = 0.25f;
    public Ease moveEase = Ease.OutQuad;

    [Header("Dealing Animation")]
    [Tooltip("Vị trí bộ bài ở góc màn hình (Deck)")]
    public Transform deckOrigin;
    public bool dealPiecesOnStart = true;
    public float dealDuration = 0.3f;
    public float dealInterval = 0.05f;
    public Ease dealEase = Ease.OutQuad;

    public static readonly HashSet<int> HardLevels = new HashSet<int> { 5, 10, 15, 20, 25 };

    private readonly List<PuzzlePiece> _allPieces = new List<PuzzlePiece>();

    private bool _alreadyCompleted = false;

    private void Start()
    {
        levelIndex = CurrentLevel.Get();

        // Grid logic
        if (HardLevels.Contains(levelIndex))
        {
            rows = 4;
            cols = 4;
        }
        else
        {
            rows = 3;
            cols = 3;
        }

        // Load texture bằng Resources sử dụng FORMAT
        string path = string.Format(GameConstant.LEVEL_PATH_FORMAT, levelIndex);
        Texture2D tex = Resources.Load<Texture2D>(path);

        if (tex == null)
        {
            Debug.LogError($"Missing texture at Resources/{path}");
            return;
        }

        sourceTexture = tex;

        if (autoCenterOnCamera)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                boardCenter = cam.transform.position;
            }
        }

        InitBoard();
        AutoFitCamera();
    }




    private void AutoFitCamera()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float halfHeight = boardSize.y / 2f;
        float halfWidth = boardSize.x / 2f;

        float aspect = (float)Screen.width / Screen.height;

        float neededSizeByHeight = halfHeight;
        float neededSizeByWidth = halfWidth / aspect;

        cam.orthographicSize = Mathf.Max(neededSizeByHeight, neededSizeByWidth) + 0.2f;
    }

    private bool IsBoardCompleted()
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                PuzzlePiece piece = _piecesBySlot[r, c];
                if (piece == null) return false;

                if (piece.CurrentCoord != piece.OriginalCoord)
                    return false;
            }
        }
        return true;
    }


    private void InitBoard()
    {
        if (sourceTexture == null || piecePrefab == null)
        {
            Debug.LogError("Missing texture or piece prefab.");
            return;
        }

        _piecesBySlot = new PuzzlePiece[rows, cols];
        _allPieces.Clear();

        int piecePixelWidth = sourceTexture.width / cols;
        int piecePixelHeight = sourceTexture.height / rows;

        float pieceWorldWidth = piecePixelWidth / pixelsPerUnit;
        float pieceWorldHeight = piecePixelHeight / pixelsPerUnit;

        boardSize = new Vector2(pieceWorldWidth * cols, pieceWorldHeight * rows);

        _cellWidth = pieceWorldWidth;
        _cellHeight = pieceWorldHeight;

        Vector2 topLeft = boardCenter + new Vector2(
            -boardSize.x / 2f + _cellWidth / 2f,
             boardSize.y / 2f - _cellHeight / 2f
        );

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Rect rect = new Rect(
                    c * piecePixelWidth,
                    (rows - 1 - r) * piecePixelHeight,   // lật Y
                    piecePixelWidth,
                    piecePixelHeight
                );

                Sprite sprite = Sprite.Create(
                    sourceTexture,
                    rect,
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect
                );

                GameObject go = Instantiate(piecePrefab, transform);
                var sr = go.GetComponent<SpriteRenderer>();
                var piece = go.GetComponent<PuzzlePiece>();

                if (sr == null || piece == null)
                {
                    Debug.LogError("Piece prefab thiếu SpriteRenderer hoặc PuzzlePiece.");
                    Destroy(go);
                    continue;
                }

                sr.sprite = sprite;

                // collider theo sprite
                var col = go.GetComponent<BoxCollider2D>();
                if (col != null && sr.sprite != null)
                {
                    col.size = sr.sprite.bounds.size;
                    col.offset = sr.sprite.bounds.center;
                }

                // border theo sprite
                piece.SetupBorders();

                // bo góc bằng shader
                piece.InitRoundedMaterial();

                Vector2Int coord = new Vector2Int(r, c);
                Vector3 cellCenter = new Vector3(
                    topLeft.x + c * _cellWidth,
                    topLeft.y - r * _cellHeight,
                    0f
                );

                // tạm đặt đúng chỗ, nhưng sẽ được xử lý lại sau khi xáo + phát bài
                go.transform.position = cellCenter;

                piece.Init(this, coord, coord);
                _piecesBySlot[r, c] = piece;
                _allPieces.Add(piece);
            }
        }

        // Xáo vị trí logic của mảnh trên grid
        ShufflePieces();

        // Nếu có deckOrigin và bật flag -> phát bài từ góc
        if (dealPiecesOnStart && deckOrigin != null)
        {
            DealPiecesFromDeck();
        }
        else
        {
            // Không dùng animation phát bài -> đặt thẳng vào cell
            RepositionAllPiecesToCells();
        }
    }

    #region Board helpers

    public bool TryGetCellFromWorld(Vector3 worldPos, out Vector2Int coord)
    {
        Vector2 local = worldPos - (Vector3)boardCenter;

        float halfW = boardSize.x / 2f;
        float halfH = boardSize.y / 2f;

        if (local.x < -halfW || local.x > halfW ||
            local.y < -halfH || local.y > halfH)
        {
            coord = default;
            return false;
        }

        float x01 = (local.x + halfW) / boardSize.x; // 0..1
        float y01 = (halfH - local.y) / boardSize.y; // 0..1

        int c = Mathf.Clamp(Mathf.FloorToInt(x01 * cols), 0, cols - 1);
        int r = Mathf.Clamp(Mathf.FloorToInt(y01 * rows), 0, rows - 1);

        coord = new Vector2Int(r, c);
        return true;
    }

    public Vector3 GetCellCenter(Vector2Int coord)
    {
        Vector2 topLeft = boardCenter + new Vector2(
            -boardSize.x / 2f + _cellWidth / 2f,
             boardSize.y / 2f - _cellHeight / 2f
        );

        return new Vector3(
            topLeft.x + coord.y * _cellWidth,
            topLeft.y - coord.x * _cellHeight,
            0f
        );
    }

    public PuzzlePiece GetPieceAtCell(Vector2Int coord)
    {
        return _piecesBySlot[coord.x, coord.y];
    }

    public void SetPieceAtCell(Vector2Int coord, PuzzlePiece piece)
    {
        _piecesBySlot[coord.x, coord.y] = piece;
    }

    #endregion

    #region Swapping & borders

    public void SwapPieces(PuzzlePiece movingPiece, Vector2Int targetCoord)
    {
        Vector2Int from = movingPiece.CurrentCoord;
        PuzzlePiece other = GetPieceAtCell(targetCoord);

        SetPieceAtCell(from, other);
        SetPieceAtCell(targetCoord, movingPiece);

        movingPiece.SetCurrentCoord(targetCoord);
        if (other != null)
        {
            other.SetCurrentCoord(from);
        }

        movingPiece.transform.position = GetCellCenter(targetCoord);
        if (other != null)
        {
            other.transform.position = GetCellCenter(from);
        }

        UpdateBordersAroundCell(from);
        UpdateBordersAroundCell(targetCoord);
    }

    public void UpdateAllBorders()
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                UpdateBordersAroundCell(new Vector2Int(r, c));
            }
        }
    }

    public void UpdateBordersAroundCell(Vector2Int coord)
    {
        PuzzlePiece piece = GetPieceAtCell(coord);
        if (piece == null) return;

        // bật hết border
        piece.EnableAllBorders();

        // check hàng xóm đúng ở 4 hướng
        bool hasLeft = HasCorrectNeighbor(coord, GridLeft);
        bool hasRight = HasCorrectNeighbor(coord, GridRight);
        bool hasUp = HasCorrectNeighbor(coord, GridUp);
        bool hasDown = HasCorrectNeighbor(coord, GridDown);

        // tắt border cạnh chung
        TryUpdateBorderWithNeighbor(piece, coord, GridLeft);
        TryUpdateBorderWithNeighbor(piece, coord, GridRight);
        TryUpdateBorderWithNeighbor(piece, coord, GridUp);
        TryUpdateBorderWithNeighbor(piece, coord, GridDown);

        // cập nhật bo góc bằng shader
        piece.UpdateCornerRadii(hasUp, hasDown, hasLeft, hasRight);
    }

    private void TryUpdateBorderWithNeighbor(PuzzlePiece piece, Vector2Int coord, Vector2Int dir)
    {
        Vector2Int neighborCoord = new Vector2Int(coord.x + dir.x, coord.y + dir.y);

        if (neighborCoord.x < 0 || neighborCoord.x >= rows ||
            neighborCoord.y < 0 || neighborCoord.y >= cols)
        {
            return;
        }

        PuzzlePiece neighbor = GetPieceAtCell(neighborCoord);
        if (neighbor == null) return;

        Vector2Int expectedNeighborOriginal = piece.OriginalCoord + dir;
        bool correctlyAdjacent = neighbor.OriginalCoord == expectedNeighborOriginal;

        if (!correctlyAdjacent) return;

        // Đúng vị trí -> tắt border cạnh chung
        if (dir == GridLeft)
        {
            piece.DisableBorderLeft();
            neighbor.DisableBorderRight();
        }
        else if (dir == GridRight)
        {
            piece.DisableBorderRight();
            neighbor.DisableBorderLeft();
        }
        else if (dir == GridUp)
        {
            piece.DisableBorderTop();
            neighbor.DisableBorderBottom();
        }
        else if (dir == GridDown)
        {
            piece.DisableBorderBottom();
            neighbor.DisableBorderTop();
        }
    }

    private bool HasCorrectNeighbor(Vector2Int coord, Vector2Int dir)
    {
        Vector2Int neighborCoord = new Vector2Int(coord.x + dir.x, coord.y + dir.y);

        if (neighborCoord.x < 0 || neighborCoord.x >= rows ||
            neighborCoord.y < 0 || neighborCoord.y >= cols)
        {
            return false;
        }

        PuzzlePiece piece = GetPieceAtCell(coord);
        PuzzlePiece neighbor = GetPieceAtCell(neighborCoord);
        if (piece == null || neighbor == null) return false;

        Vector2Int expectedNeighborOriginal = piece.OriginalCoord + dir;
        return neighbor.OriginalCoord == expectedNeighborOriginal;
    }

    #endregion

    #region Shuffle, deal & cluster

    private void ShufflePieces()
    {
        System.Random rng = new System.Random();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int r2 = rng.Next(rows);
                int c2 = rng.Next(cols);

                var a = _piecesBySlot[r, c];
                var b = _piecesBySlot[r2, c2];

                _piecesBySlot[r, c] = b;
                _piecesBySlot[r2, c2] = a;
            }
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var p = _piecesBySlot[r, c];
                if (p != null)
                {
                    p.SetCurrentCoord(new Vector2Int(r, c));
                }
            }
        }
    }

    private void RepositionAllPiecesToCells()
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var p = _piecesBySlot[r, c];
                if (p != null)
                {
                    p.transform.position = GetCellCenter(new Vector2Int(r, c));
                }
            }
        }
    }

    /// <summary>
    /// Phát toàn bộ mảnh từ deckOrigin vào đúng cell (đã được ShufflePieces)
    /// </summary>
    private void DealPiecesFromDeck()
    {
        if (deckOrigin == null)
        {
            Debug.LogWarning("PuzzleBoardManager: deckOrigin is null, fallback to direct reposition.");
            RepositionAllPiecesToCells();
            return;
        }

        // Đưa tất cả mảnh về vị trí bộ bài
        foreach (var p in _allPieces)
        {
            if (p != null)
            {
                p.transform.position = deckOrigin.position;
            }
        }

        Sequence seq = DOTween.Sequence();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                PuzzlePiece piece = _piecesBySlot[r, c];
                if (piece == null) continue;
                Vector3 targetPos = GetCellCenter(new Vector2Int(r, c));
                seq.Append(
                    piece.transform.DOMove(targetPos, dealDuration)
                         .SetEase(dealEase)
                );
                seq.AppendInterval(dealInterval);
            }
        }
    }

    public List<PuzzlePiece> BuildClusterFrom(PuzzlePiece start)
    {
        var cluster = new List<PuzzlePiece>();
        var visited = new HashSet<PuzzlePiece>();
        var queue = new Queue<PuzzlePiece>();

        if (start == null) return cluster;

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            cluster.Add(current);

            Vector2Int coord = current.CurrentCoord;
            Vector2Int orig = current.OriginalCoord;

            TryEnqueueNeighbor(current, coord, orig, GridLeft, visited, queue);
            TryEnqueueNeighbor(current, coord, orig, GridRight, visited, queue);
            TryEnqueueNeighbor(current, coord, orig, GridUp, visited, queue);
            TryEnqueueNeighbor(current, coord, orig, GridDown, visited, queue);
        }

        return cluster;
    }

    private void TryEnqueueNeighbor(
        PuzzlePiece current,
        Vector2Int coord,
        Vector2Int orig,
        Vector2Int dir,
        HashSet<PuzzlePiece> visited,
        Queue<PuzzlePiece> queue)
    {
        Vector2Int neighborCoord = new Vector2Int(coord.x + dir.x, coord.y + dir.y);

        if (neighborCoord.x < 0 || neighborCoord.x >= rows ||
            neighborCoord.y < 0 || neighborCoord.y >= cols)
        {
            return;
        }

        PuzzlePiece neighbor = GetPieceAtCell(neighborCoord);
        if (neighbor == null || visited.Contains(neighbor)) return;

        Vector2Int expectedNeighborOriginal = orig + dir;
        if (neighbor.OriginalCoord != expectedNeighborOriginal) return;

        visited.Add(neighbor);
        queue.Enqueue(neighbor);
    }

    public bool MoveCluster(
    List<PuzzlePiece> cluster,
    Dictionary<PuzzlePiece, Vector2Int> startCoords,
    PuzzlePiece anchorPiece,
    Vector2Int targetAnchorCoord)
    {
        if (cluster == null || cluster.Count == 0) return false;
        if (anchorPiece == null || !startCoords.ContainsKey(anchorPiece)) return false;

        // Cụm hiện tại
        var clusterSet = new HashSet<PuzzlePiece>(cluster);
        var targetCoords = new Dictionary<PuzzlePiece, Vector2Int>();

        // Delta di chuyển dựa trên mảnh anchor
        Vector2Int startAnchor = startCoords[anchorPiece];
        Vector2Int delta = targetAnchorCoord - startAnchor;

        // 1) Tính toạ độ đích cho từng piece trong cụm + check biên
        foreach (var p in cluster)
        {
            if (!startCoords.TryGetValue(p, out var from))
            {
                Debug.LogWarning($"[MoveCluster] missing startCoord for {p.name}");
                return false;
            }

            Vector2Int to = from + delta;

            if (to.x < 0 || to.x >= rows || to.y < 0 || to.y >= cols)
            {
                Debug.LogWarning($"[MoveCluster] out of bounds: {p.name} from={from} to={to}");
                return false;
            }

            targetCoords[p] = to;
        }

        // 2) Gom các mảnh bị chiếm chỗ (occupant) ở các ô đích T
        var displacedPieces = new List<PuzzlePiece>();

        foreach (var kv in targetCoords)
        {
            Vector2Int to = kv.Value;
            PuzzlePiece occupant = _piecesBySlot[to.x, to.y];

            if (occupant != null && !clusterSet.Contains(occupant))
            {
                if (!displacedPieces.Contains(occupant))
                {
                    displacedPieces.Add(occupant);
                }
            }
        }

        // 3) Tính các ô "rỗng" mà cụm để lại: F = S \ T
        var freedSet = new HashSet<Vector2Int>();

        foreach (var p in cluster)
        {
            freedSet.Add(startCoords[p]);
        }

        foreach (var kv in targetCoords)
        {
            // Mảnh cụm sẽ chiếm lại ô này, nên không còn trống cho occupant
            freedSet.Remove(kv.Value);
        }

        if (displacedPieces.Count > freedSet.Count)
        {
            Debug.LogWarning($"[MoveCluster] not enough freed cells: displaced={displacedPieces.Count}, freed={freedSet.Count}");
            return false;
        }

        var freedList = new List<Vector2Int>(freedSet);

        // 4) Xây map piece -> newCoord cho cả cụm và các occupant
        var newCoords = new Dictionary<PuzzlePiece, Vector2Int>();

        // Cụm: luôn đi tới targetCoords
        foreach (var kv in targetCoords)
        {
            newCoords[kv.Key] = kv.Value;
        }

        // Occupant: phân vào các ô rỗng còn lại của cụm
        for (int i = 0; i < displacedPieces.Count; i++)
        {
            newCoords[displacedPieces[i]] = freedList[i];
        }

        // 5) Đảm bảo không có 2 piece muốn tới cùng 1 ô
        var usedCells = new HashSet<Vector2Int>();
        foreach (var kv in newCoords)
        {
            if (!usedCells.Add(kv.Value))
            {
                Debug.LogWarning($"[MoveCluster] destination conflict at {kv.Value}");
                return false;
            }
        }

        // 6) Clone bảng slot
        var newSlots = (PuzzlePiece[,])_piecesBySlot.Clone();

        // 7) Clear vị trí cũ của mọi piece có trong newCoords (cụm + occupant)
        foreach (var kv in newCoords)
        {
            PuzzlePiece piece = kv.Key;
            Vector2Int oldCoord;

            if (clusterSet.Contains(piece))
                oldCoord = startCoords[piece];
            else
                oldCoord = piece.CurrentCoord;

            if (oldCoord.x >= 0 && oldCoord.x < rows &&
                oldCoord.y >= 0 && oldCoord.y < cols &&
                newSlots[oldCoord.x, oldCoord.y] == piece)
            {
                newSlots[oldCoord.x, oldCoord.y] = null;
            }
        }

        // 8) Đặt mảnh vào vị trí mới + animate
        Sequence seq = DOTween.Sequence();

        foreach (var kv in newCoords)
        {
            PuzzlePiece piece = kv.Key;
            Vector2Int coord = kv.Value;

            newSlots[coord.x, coord.y] = piece;
            piece.SetCurrentCoord(coord);

            Vector3 targetPos = GetCellCenter(coord);

            seq.Join(
                piece.transform.DOMove(targetPos, moveDuration)
                     .SetEase(moveEase)
            );
        }

        // 9) Ghi lại bảng slot chính thức
        _piecesBySlot = newSlots;

        // 10) Khi tween xong: update viền + check hoàn thành + HẠ SORTING CỤM
        seq.OnComplete(() =>
        {
            UpdateAllBorders();

            if (IsBoardCompleted())
            {
                OnBoardCompleted();
            }

            // Cụm vừa kéo trở lại layer bình thường
            foreach (var p in cluster)
            {
                if (p == null) continue;
                var sr2 = p.GetComponent<SpriteRenderer>();
                if (sr2 != null) sr2.sortingOrder -= 1000;
            }
        });

        return true;

    }


    private void OnBoardCompleted()
    {
        if (_alreadyCompleted) return;
        _alreadyCompleted = true;

        // Lưu tiến trình (mở khoá level)
        LevelProgress.SaveLevelCompleted(levelIndex);

        // Hiện UI chúc mừng
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(true);
    }

    #endregion
}
