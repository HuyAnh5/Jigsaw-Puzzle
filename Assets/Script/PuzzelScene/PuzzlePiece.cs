using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class PuzzlePiece : MonoBehaviour
{
    public Vector2Int OriginalCoord { get; private set; }
    public Vector2Int CurrentCoord { get; private set; }

    [Header("Borders")]
    public GameObject borderTop;
    public GameObject borderBottom;
    public GameObject borderLeft;
    public GameObject borderRight;

    [Header("Border Settings")]
    public float borderThickness = 0.05f;   // (giữ lại để không lỗi, hiện tại viền vẽ bằng shader)

    [Header("Rounded Shader")]
    [SerializeField] private float baseCornerRadius = 0.22f; // 0..0.5 theo UV
    [SerializeField] private float edgeSmooth = 0.02f;

    [Header("Outline Settings (Shader)")]
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField, Range(0f, 0.2f)] private float outlineWidth = 0.02f;

    private Material _roundedMat;
    private static readonly int RadiusTL_ID = Shader.PropertyToID("_RadiusTL");
    private static readonly int RadiusTR_ID = Shader.PropertyToID("_RadiusTR");
    private static readonly int RadiusBL_ID = Shader.PropertyToID("_RadiusBL");
    private static readonly int RadiusBR_ID = Shader.PropertyToID("_RadiusBR");
    private static readonly int Smooth_ID = Shader.PropertyToID("_Smooth");
    private static readonly int UVMin_ID = Shader.PropertyToID("_UVMin");
    private static readonly int UVMax_ID = Shader.PropertyToID("_UVMax");
    private static readonly int OutlineColor_ID = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidth_ID = Shader.PropertyToID("_OutlineWidth");
    private static readonly int BorderMask_ID = Shader.PropertyToID("_BorderMask");

    private Vector4 _borderMask = new Vector4(1f, 1f, 1f, 1f);

    private PuzzleBoardManager _board;
    private Camera _cam;

    private bool _isDragging;
    private Vector3 _dragOffset;
    private Vector3 _startWorldPos;
    private Vector2Int _startCoord;

    // Drag theo cụm (cluster)
    private List<PuzzlePiece> _dragCluster = new List<PuzzlePiece>();
    private Dictionary<PuzzlePiece, Vector2Int> _dragStartCoords = new Dictionary<PuzzlePiece, Vector2Int>();
    private Dictionary<PuzzlePiece, Vector3> _dragStartWorldPos = new Dictionary<PuzzlePiece, Vector3>();

    #region Setup

    public void SetupBorders()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        // kích thước sprite trong local-space, đơn vị world
        Vector2 size = sr.sprite.bounds.size;
        float w = size.x;
        float h = size.y;
        float t = borderThickness;

        // cạnh trên
        if (borderTop)
        {
            borderTop.transform.localPosition = new Vector3(0f, h / 2f, 0f);
            borderTop.transform.localScale = new Vector3(w, t, 1f);
        }

        // cạnh dưới
        if (borderBottom)
        {
            borderBottom.transform.localPosition = new Vector3(0f, -h / 2f, 0f);
            borderBottom.transform.localScale = new Vector3(w, t, 1f);
        }

        // cạnh trái
        if (borderLeft)
        {
            borderLeft.transform.localPosition = new Vector3(-w / 2f, 0f, 0f);
            borderLeft.transform.localScale = new Vector3(t, h, 1f);
        }

        // cạnh phải
        if (borderRight)
        {
            borderRight.transform.localPosition = new Vector3(w / 2f, 0f, 0f);
            borderRight.transform.localScale = new Vector3(t, h, 1f);
        }
    }

    /// <summary>
    /// Gọi sau khi SpriteRenderer đã có sprite và prefab đã gán material RoundedSpriteCorners2D
    /// </summary>
    public void InitRoundedMaterial()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sharedMaterial == null || sr.sprite == null) return;

        // clone material để mỗi piece có radius + uv riêng
        _roundedMat = new Material(sr.sharedMaterial);
        sr.material = _roundedMat;

        // set độ mềm cạnh
        _roundedMat.SetFloat(Smooth_ID, edgeSmooth);

        // ❶ TÍNH UV MIN/MAX CỦA SPRITE TRONG TEXTURE
        var sprite = sr.sprite;
        var uvs = sprite.uv;

        if (uvs != null && uvs.Length > 0)
        {
            float minX = uvs[0].x;
            float maxX = uvs[0].x;
            float minY = uvs[0].y;
            float maxY = uvs[0].y;

            for (int i = 1; i < uvs.Length; i++)
            {
                var uv = uvs[i];
                if (uv.x < minX) minX = uv.x;
                if (uv.x > maxX) maxX = uv.x;
                if (uv.y < minY) minY = uv.y;
                if (uv.y > maxY) maxY = uv.y;
            }

            _roundedMat.SetVector(UVMin_ID, new Vector4(minX, minY, 0f, 0f));
            _roundedMat.SetVector(UVMax_ID, new Vector4(maxX, maxY, 0f, 0f));
        }

        // ❷ Mặc định bo 4 góc (sau đó UpdateCornerRadii sẽ chỉnh lại theo hàng xóm)
        SetCornerRadii(baseCornerRadius, baseCornerRadius, baseCornerRadius, baseCornerRadius);
    }

    private void SetCornerRadii(float tl, float tr, float bl, float br)
    {
        if (_roundedMat == null) return;

        _roundedMat.SetFloat(RadiusTL_ID, tl);
        _roundedMat.SetFloat(RadiusTR_ID, tr);
        _roundedMat.SetFloat(RadiusBL_ID, bl);
        _roundedMat.SetFloat(RadiusBR_ID, br);
    }

    private void SetBorderMask(float left, float right, float top, float bottom)
    {
        if (_roundedMat == null) return;

        _borderMask = new Vector4(left, right, top, bottom);
        _roundedMat.SetVector(BorderMask_ID, _borderMask);
    }

    /// <summary>
    /// Cập nhật bán kính bo + trạng thái viền theo hàng xóm (true = có hàng xóm đúng ở hướng đó)
    /// </summary>
    public void UpdateCornerRadii(bool hasUp, bool hasDown, bool hasLeft, bool hasRight)
    {
        // 1) Bo góc: có hàng xóm ở cạnh nào thì hai góc liên quan trở thành vuông (radius = 0)
        float tl = baseCornerRadius;
        float tr = baseCornerRadius;
        float bl = baseCornerRadius;
        float br = baseCornerRadius;

        if (hasLeft) { tl = 0f; bl = 0f; }
        if (hasRight) { tr = 0f; br = 0f; }
        if (hasUp) { tl = 0f; tr = 0f; }
        if (hasDown) { bl = 0f; br = 0f; }

        SetCornerRadii(tl, tr, bl, br);

        // 2) Viền: nếu có hàng xóm đúng ở cạnh nào thì cạnh đó không vẽ outline
        float leftBorder = hasLeft ? 0f : 1f;
        float rightBorder = hasRight ? 0f : 1f;
        float topBorder = hasUp ? 0f : 1f;
        float bottomBorder = hasDown ? 0f : 1f;

        SetBorderMask(leftBorder, rightBorder, topBorder, bottomBorder);
    }

    private void Awake()
    {
        if (_board == null)
        {
            _board = FindAnyObjectByType<PuzzleBoardManager>();
        }

        if (_cam == null)
        {
            _cam = Camera.main;
        }

        Debug.Log($"[PuzzlePiece:{name}] Awake. Board={_board != null}, Cam={_cam != null}");
    }

    private void Start()
    {
        Debug.Log($"[PuzzlePiece:{name}] Start. Original={OriginalCoord}, Current={CurrentCoord}");
    }

    public void Init(PuzzleBoardManager board, Vector2Int originalCoord, Vector2Int currentCoord)
    {
        _board = board;
        OriginalCoord = originalCoord;
        CurrentCoord = currentCoord;
        Debug.Log($"[PuzzlePiece:{name}] Init called. Original={originalCoord}, Current={currentCoord}");
    }

    public void SetCurrentCoord(Vector2Int coord)
    {
        CurrentCoord = coord;
    }

    #endregion

    #region Borders

    public void EnableAllBorders()
    {
        // Viền bây giờ vẽ bằng shader, không còn dùng GameObject con.
        // Hàm này giữ lại để BoardManager có thể gọi như cũ.
        SetBorderMask(1f, 1f, 1f, 1f);
    }

    public void DisableBorderTop()
    {
        SetBorderMask(_borderMask.x, _borderMask.y, 0f, _borderMask.w);
    }

    public void DisableBorderBottom()
    {
        SetBorderMask(_borderMask.x, _borderMask.y, _borderMask.z, 0f);
    }

    public void DisableBorderLeft()
    {
        SetBorderMask(0f, _borderMask.y, _borderMask.z, _borderMask.w);
    }

    public void DisableBorderRight()
    {
        SetBorderMask(_borderMask.x, 0f, _borderMask.z, _borderMask.w);
    }

    #endregion

    #region Drag & Cluster

    private void OnMouseDown()
    {

        SoundManager.PlaySFX(SoundType.pickCard);
        if (_cam == null) _cam = Camera.main;
        if (_board == null) _board = FindAnyObjectByType<PuzzleBoardManager>();

        Debug.Log($"[PuzzlePiece:{name}] OnMouseDown. Cam={_cam != null}, Board={_board != null}, Mouse={Input.mousePosition}");
        
        if (PuzzelSceneUIManager.IsSettingsOpen)
        {
            return;
        }
        
        if (_cam == null || _board == null)
        {
            Debug.LogWarning($"[PuzzlePiece:{name}] OnMouseDown but missing Camera or Board.");
            return;
        }

        _isDragging = true;

        // Xây cụm (cluster) từ mảnh được click – các mảnh đã ghép đúng hàng xóm
        _dragCluster.Clear();
        _dragStartCoords.Clear();
        _dragStartWorldPos.Clear();

        _dragCluster = _board.BuildClusterFrom(this);
        if (_dragCluster.Count == 0)
        {
            _dragCluster.Add(this);
        }

        foreach (var p in _dragCluster)
        {
            _dragStartCoords[p] = p.CurrentCoord;
            _dragStartWorldPos[p] = p.transform.position;
        }

        _startCoord = CurrentCoord;
        _startWorldPos = transform.position;

        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        _dragOffset = transform.position - mouseWorld;

        // Đưa cả cụm lên trên cùng
        foreach (var p in _dragCluster)
        {
            var sr2 = p.GetComponent<SpriteRenderer>();
            if (sr2 != null)
            {
                sr2.sortingOrder = 1000;
            }
        }
    }



    private void OnMouseDrag()
    {
        if (!_isDragging || _cam == null) return;

        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        Vector3 targetPos = mouseWorld + _dragOffset;
        Vector3 deltaMove = targetPos - transform.position;

        // Di chuyển cả cụm theo delta
        foreach (var p in _dragCluster)
        {
            p.transform.position += deltaMove;
        }
    }

    private void OnMouseUp()
    {
        if (!_isDragging) return;
        _isDragging = false;

        var clusterCopy = new List<PuzzlePiece>(_dragCluster);

        if (_cam == null || _board == null)
        {
            Debug.LogWarning($"[PuzzlePiece:{name}] OnMouseUp but missing Cam/Board. Reset cluster to start.");
            foreach (var kv in _dragStartWorldPos)
            {
                kv.Key.transform.position = kv.Value;
                kv.Key.SetCurrentCoord(_dragStartCoords[kv.Key]);
            }

            foreach (var p in clusterCopy)
            {
                if (p == null) continue;
                var sr2 = p.GetComponent<SpriteRenderer>();
                if (sr2 != null) sr2.sortingOrder = 0;
            }

            _dragCluster.Clear();
            _dragStartCoords.Clear();
            _dragStartWorldPos.Clear();
            return;
        }

        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        if (_board.TryGetCellFromWorld(mouseWorld, out var targetCoord))
        {
            Debug.Log($"[PuzzlePiece:{name}] OnMouseUp inside board. Anchor from={_startCoord} to={targetCoord}");

            bool moved = _board.MoveCluster(_dragCluster, _dragStartCoords, this, targetCoord);

            if (!moved)
            {
                // Move fail -> trả về vị trí cũ + hạ sorting
                foreach (var kv in _dragStartWorldPos)
                {
                    kv.Key.transform.position = kv.Value;
                    kv.Key.SetCurrentCoord(_dragStartCoords[kv.Key]);
                }
                _board.UpdateAllBorders();

                foreach (var p in clusterCopy)
                {
                    if (p == null) continue;
                    var sr2 = p.GetComponent<SpriteRenderer>();
                    if (sr2 != null) sr2.sortingOrder = 0;
                }
            }
            // moved == true -> KHÔNG hạ sorting ở đây.
            // MoveCluster sẽ tự hạ sau khi tween xong.
        }
        else
        {
            // Thả ngoài board -> trả về vị trí cũ + hạ sorting
            Debug.Log($"[PuzzlePiece:{name}] OnMouseUp outside board. Snap cluster back.");
            foreach (var kv in _dragStartWorldPos)
            {
                kv.Key.transform.position = kv.Value;
                kv.Key.SetCurrentCoord(_dragStartCoords[kv.Key]);
            }
            _board.UpdateAllBorders();

            foreach (var p in clusterCopy)
            {
                if (p == null) continue;
                var sr2 = p.GetComponent<SpriteRenderer>();
                if (sr2 != null) sr2.sortingOrder = 0;
            }
        }

        // ĐẢM BẢO: sau khi thả chuột, cụm luôn về layer 0
        foreach (var p in clusterCopy)
        {
            if (p == null) continue;
            var sr2 = p.GetComponent<SpriteRenderer>();
            if (sr2 != null)
            {
                sr2.sortingOrder = 0;
            }
        }


        _dragCluster.Clear();
        _dragStartCoords.Clear();
        _dragStartWorldPos.Clear();
    }


    #endregion

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[PuzzlePiece:{name}] Update saw MouseDown at screen {Input.mousePosition}");
        }
    }
#endif
}
