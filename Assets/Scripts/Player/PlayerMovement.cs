using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 3f;

    private Rigidbody2D body;
    private Vector2 movement;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
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

    private void OnDisable()
    {
        movement = Vector2.zero;
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }
}
