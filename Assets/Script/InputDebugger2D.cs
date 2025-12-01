using UnityEngine;

public class InputDebugger2D : MonoBehaviour
{
    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main;
        Debug.Log($"[InputDebugger2D] Awake. Cam={_cam != null}");
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (_cam == null) _cam = Camera.main;

            Vector3 mouseWorld = Vector3.zero;
            if (_cam != null)
            {
                mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;
            }

            Debug.Log($"[InputDebugger2D] MouseDown. Screen={Input.mousePosition}, World={mouseWorld}");

            if (_cam != null)
            {
                var hit = Physics2D.Raycast(mouseWorld, Vector2.zero);
                if (hit.collider != null)
                {
                    Debug.Log($"[InputDebugger2D] Raycast hit: {hit.collider.name} (layer {hit.collider.gameObject.layer})");
                }
                else
                {
                    Debug.Log("[InputDebugger2D] Raycast hit: NOTHING");
                }
            }
        }
    }


}
