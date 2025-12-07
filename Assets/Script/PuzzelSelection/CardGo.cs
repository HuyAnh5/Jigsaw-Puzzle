using DG.Tweening;
using UnityEngine;

public class CardGo : MonoBehaviour
{

    private bool flipped = false;
    [SerializeField] public float rotateSpeed = 1f;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) Flip();
    }

    private void Flip()
    {
        flipped = !flipped;

        float targetangel = 0f;
        if (flipped == true)
        {
            targetangel = 180;
        }
        else
        {
            targetangel = 0f;
        }

        transform.DORotate(new Vector3(0,targetangel,0f), rotateSpeed,RotateMode.Fast);

    }



}
