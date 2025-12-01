using UnityEngine;
using System.Collections.Generic;

public class PuzzleBoardManager : MonoBehaviour
{
    [Header("Input")]
    public Texture2D sourceTexture;
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

    private void Start()
    {
        // Đưa tâm board đúng giữa camera
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

    private void InitBoard()
    {
        if (sourceTexture == null || piecePrefab == null)
        {
            Debug.LogError("Missing texture or piece prefab.");
            return;
        }

        _piecesBySlot = new PuzzlePiece[rows, cols];

        int piecePixelWidth = sourceTexture.width / cols;
        int piecePixelHeight = sourceTexture.height / rows;

        float pieceWorldWidth = piecePixelWidth / pixelsPerUnit;
        float pieceWorldHeight = piecePixelHeight / pixelsPerUnit;

        // boardSize = kích thước ảnh
        boardSize = new Vector2(pieceWorldWidth * cols, pieceWorldHeight * rows);

        _cellWidth = pieceWorldWidth;
        _cellHeight = pieceWorldHeight;

        // top-left của board (tâm đã là boardCenter)
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

                go.transform.position = cellCenter;

                piece.Init(this, coord, coord);
                _piecesBySlot[r, c] = piece;
            }
        }

        ShufflePieces();
        RepositionAllPiecesToCells();
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

    #region Shuffle & cluster

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
        if (!startCoords.ContainsKey(anchorPiece)) return false;

        Vector2Int startAnchor = startCoords[anchorPiece];
        Vector2Int delta = targetAnchorCoord - startAnchor;

        var clusterSet = new HashSet<PuzzlePiece>(cluster);
        var targetCoords = new Dictionary<PuzzlePiece, Vector2Int>();

        // 1) Tính toạ độ đích cho từng piece
        foreach (var p in cluster)
        {
            Vector2Int from = startCoords[p];
            Vector2Int to = from + delta;

            if (to.x < 0 || to.x >= rows || to.y < 0 || to.y >= cols)
                return false;

            targetCoords[p] = to;
        }

        // 2) Copy bảng slot
        var newSlots = new PuzzlePiece[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
                newSlots[r, c] = _piecesBySlot[r, c];
        }

        // 3) Clear slot cũ của cluster
        foreach (var p in cluster)
        {
            Vector2Int from = startCoords[p];
            if (newSlots[from.x, from.y] == p)
                newSlots[from.x, from.y] = null;
        }

        // 4) Xử lý swap với các mảnh ngoài cluster nếu cần
        var swappedPieces = new Dictionary<PuzzlePiece, Vector2Int>();

        foreach (var p in cluster)
        {
            Vector2Int from = startCoords[p];
            Vector2Int to = targetCoords[p];

            PuzzlePiece occupant = newSlots[to.x, to.y];

            if (occupant != null && !clusterSet.Contains(occupant))
            {
                if (newSlots[from.x, from.y] != null)
                {
                    return false;
                }

                newSlots[from.x, from.y] = occupant;
                swappedPieces[occupant] = from;
            }

            newSlots[to.x, to.y] = p;
        }

        // 5) Ghi lại slot chính thức
        _piecesBySlot = newSlots;

        // 6) Cập nhật CurrentCoord + position cho cluster
        foreach (var p in cluster)
        {
            Vector2Int coord = targetCoords[p];
            p.SetCurrentCoord(coord);
            p.transform.position = GetCellCenter(coord);
        }

        // 7) Cập nhật CurrentCoord + position cho các mảnh bị swap
        foreach (var kv in swappedPieces)
        {
            var p = kv.Key;
            var coord = kv.Value;
            p.SetCurrentCoord(coord);
            p.transform.position = GetCellCenter(coord);
        }

        // 8) CỰC QUAN TRỌNG: cập nhật lại border + bo góc theo vị trí mới
        UpdateAllBorders();

        return true;
    }


    #endregion
}
