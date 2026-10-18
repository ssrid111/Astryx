using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField] private Sprite standSprite;
    [SerializeField] private Sprite walkSprite;
    [SerializeField] private Sprite walkSprite2;
    [SerializeField, Min(0.01f)] private float walkFrameDuration = 0.18f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Vector2 movement;
    private float animationTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (standSprite == null)
            standSprite = spriteRenderer.sprite;
    }

    private void Update()
    {
        movement = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !Application.isFocused)
            return;

        float horizontal = (keyboard.dKey.isPressed ? 1f : 0f)
            - (keyboard.aKey.isPressed ? 1f : 0f);
        float vertical = (keyboard.wKey.isPressed ? 1f : 0f)
            - (keyboard.sKey.isPressed ? 1f : 0f);
        movement = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    private void FixedUpdate()
    {
        body.linearVelocity = movement * moveSpeed;
    }

    private void LateUpdate()
    {
        if (movement.x != 0f)
            spriteRenderer.flipX = movement.x < 0f;

        // Animate actual motion so pushing against a wall does not walk in place.
        if (movement.sqrMagnitude == 0f || body.linearVelocity.sqrMagnitude < 0.001f)
        {
            animationTime = 0f;
            if (standSprite != null)
                spriteRenderer.sprite = standSprite;
            return;
        }

        animationTime += Time.deltaTime;
        int frame = Mathf.FloorToInt(animationTime / Mathf.Max(0.01f, walkFrameDuration)) % 2;
        Sprite sprite = frame == 0 ? walkSprite : walkSprite2;
        if (sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private void OnDisable()
    {
        movement = Vector2.zero;
        if (body != null)
            body.linearVelocity = Vector2.zero;
        animationTime = 0f;
        if (spriteRenderer != null && standSprite != null)
            spriteRenderer.sprite = standSprite;
    }
}
