using UnityEngine;

public class HolderLayout : MonoBehaviour
{
    public float spacing = 1.5f;
    public float moveSpeed = 10f;
    public bool center = true;

    void Update()
    {
        AnimateLayout();
    }

    void AnimateLayout()
    {
        int count = transform.childCount;
        float startX = center ? -((count - 1) * spacing) / 2f : 0f;

        for (int i = 0; i < count; i++)
        {
            Transform child = transform.GetChild(i);
            Vector3 targetPos = new Vector3(startX + i * spacing, 0f, 0f);

            // Lerp toward target for smooth animation
            child.localPosition = Vector3.Lerp(child.localPosition, targetPos, Time.deltaTime * moveSpeed);
        }
    }
}



