using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PuzzlePieceAnimator : MonoBehaviour
{
    public static PuzzlePieceAnimator Instance { get; private set; }

    [Header("Swap / Drag-Drop")]
    [Tooltip("Thời gian animation khi swap / drop nhóm mảnh")]
    public float swapDuration = 0.25f;
    public Ease swapEase = Ease.OutQuad;

    [Header("Deal At Start")]
    [Tooltip("Vị trí bộ bài ở góc màn hình, nơi tất cả PuzzlePiece xuất phát lúc bắt đầu game")]
    public Transform dealOrigin;

    [Tooltip("Thời gian 1 mảnh bay từ dealOrigin tới ô đích")]
    public float dealDuration = 0.3f;

    [Tooltip("Khoảng nghỉ giữa 2 mảnh liên tiếp khi phát")]
    public float dealInterval = 0.05f;

    public Ease dealEase = Ease.OutQuad;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // =========================================================================
    // 1. ANIMATION KHI KÉO – THẢ: SWAP 2 NHÓM (CLUSTER) PUZZLEPIECE
    // =========================================================================
    //
    // - draggedGroup: nhóm player đang kéo (sau khi thả, logic của bạn sẽ cho nó
    //   chiếm vị trí của ô target).
    // - replacedGroup: nhóm đang ở ô target, sẽ bay ngược về vị trí cũ của
    //   draggedGroup.
    //
    // Lưu ý: function NÀY CHỈ LO ANIMATION, còn logic grid / CurrentCoord bạn
    // vẫn cập nhật ở chỗ khác như cũ.
    //
    public void AnimateSwapGroups(List<PuzzlePiece> draggedGroup, List<PuzzlePiece> replacedGroup)
    {
        if (draggedGroup == null || draggedGroup.Count == 0)
        {
            Debug.LogWarning("[PuzzlePieceAnimator] AnimateSwapGroups: draggedGroup rỗng.");
            return;
        }

        if (replacedGroup == null || replacedGroup.Count == 0)
        {
            // Trường hợp này coi như ô target trống -> nên dùng AnimateDropToEmpty
            Debug.LogWarning("[PuzzlePieceAnalyzer] AnimateSwapGroups được gọi nhưng replacedGroup rỗng. Hãy dùng AnimateDropToEmpty nếu ô trống.");
            return;
        }

        // Tính “tâm” của mỗi nhóm để giữ shape
        Vector3 centerDragged = ComputeGroupCenter(draggedGroup);
        Vector3 centerReplaced = ComputeGroupCenter(replacedGroup);

        // dragged -> chỗ replaced
        Vector3 deltaDragged = centerReplaced - centerDragged;
        // replaced -> chỗ cũ của dragged
        Vector3 deltaReplaced = centerDragged - centerReplaced;

        Sequence seq = DOTween.Sequence();

        // Move draggedGroup
        foreach (var piece in draggedGroup)
        {
            if (piece == null) continue;

            Transform tr = piece.transform;
            Vector3 targetPos = tr.position + deltaDragged;

            seq.Join(
                tr.DOMove(targetPos, swapDuration)
                  .SetEase(swapEase)
            );
        }

        // Move replacedGroup
        foreach (var piece in replacedGroup)
        {
            if (piece == null) continue;

            Transform tr = piece.transform;
            Vector3 targetPos = tr.position + deltaReplaced;

            seq.Join(
                tr.DOMove(targetPos, swapDuration)
                  .SetEase(swapEase)
            );
        }

        seq.OnComplete(() =>
        {
            Debug.Log("[PuzzlePieceAnimator] Swap groups complete.");
        });
    }

    // =========================================================================
    // 2. ANIMATION DROP NHÓM VÀO Ô TRỐNG
    // =========================================================================
    //
    // - draggedGroup: nhóm đang kéo.
    // - targetAnchorWorldPos: vị trí “tâm” của ô mục tiêu (world-space).
    //
    public void AnimateDropToEmpty(List<PuzzlePiece> draggedGroup, Vector3 targetAnchorWorldPos)
    {
        if (draggedGroup == null || draggedGroup.Count == 0)
        {
            Debug.LogWarning("[PuzzlePieceAnimator] AnimateDropToEmpty: draggedGroup rỗng.");
            return;
        }

        Vector3 currentCenter = ComputeGroupCenter(draggedGroup);
        Vector3 delta = targetAnchorWorldPos - currentCenter;

        Sequence seq = DOTween.Sequence();

        foreach (var piece in draggedGroup)
        {
            if (piece == null) continue;

            Transform tr = piece.transform;
            Vector3 targetPos = tr.position + delta;

            seq.Join(
                tr.DOMove(targetPos, swapDuration)
                  .SetEase(swapEase)
            );
        }

        seq.OnComplete(() =>
        {
            Debug.Log("[PuzzlePieceAnimator] Drop group to empty cell complete.");
        });
    }

    // =========================================================================
    // 3. ANIMATION “PHÁT BÀI” LÚC BẮT ĐẦU GAME – PHÁT 9 MẢNH ĐẦU
    // =========================================================================
    //
    // - pieces: list toàn bộ PuzzlePiece trong level (hoặc ít nhất 9 cái).
    // - slots: 9 Transform là vị trí 9 ô được phân sẵn trên bàn.
    //
    public void DealPiecesToSlotsRandom(List<PuzzlePiece> pieces, List<Transform> slots)
    {
        if (dealOrigin == null)
        {
            Debug.LogWarning("[PuzzlePieceAnimator] dealOrigin chưa gán.");
            return;
        }

        if (pieces == null || slots == null)
        {
            Debug.LogWarning("[PuzzlePieceAnimator] pieces hoặc slots null.");
            return;
        }

        if (pieces.Count < 9 || slots.Count < 9)
        {
            Debug.LogWarning("[PuzzlePieceAnimator] Cần ít nhất 9 PuzzlePiece và 9 slots.");
            return;
        }

        // 1) Đưa TẤT CẢ mảnh về vị trí bộ bài ở góc
        foreach (var piece in pieces)
        {
            if (piece == null) continue;
            piece.transform.position = dealOrigin.position;
        }

        // 2) Copy và shuffle slots để random vị trí 9 mảnh đầu
        List<Transform> tempSlots = new List<Transform>(slots);
        Shuffle(tempSlots);

        Sequence seq = DOTween.Sequence();

        // 3) Lấy 9 mảnh đầu phát ra 9 ô
        for (int i = 0; i < 9; i++)
        {
            PuzzlePiece piece = pieces[i];
            Transform slot = tempSlots[i];

            if (piece == null || slot == null) continue;

            seq.Append(
                piece.transform.DOMove(slot.position, dealDuration)
                    .SetEase(dealEase)
            );

            seq.AppendInterval(dealInterval);
        }

        seq.OnComplete(() =>
        {
            Debug.Log("[PuzzlePieceAnimator] Deal 9 PuzzlePieces complete.");
        });
    }

    // =========================================================================
    // HELPER
    // =========================================================================

    private Vector3 ComputeGroupCenter(List<PuzzlePiece> group)
    {
        if (group == null || group.Count == 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (var piece in group)
        {
            if (piece == null) continue;
            sum += piece.transform.position;
            count++;
        }

        if (count == 0) return Vector3.zero;

        return sum / count;
    }

    private void Shuffle(List<Transform> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            Transform tmp = list[i];
            list[i] = list[rand];
            list[rand] = tmp;
        }
    }
}
