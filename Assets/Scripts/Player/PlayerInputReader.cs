using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputReader : MonoBehaviour
{
    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintPressed { get; private set; }
    public bool SprintHeld { get; private set; }

    public void OnMove(InputValue value)
    {
        Move = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        Look = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
            JumpPressed = true;
    }

    public void OnSprint(InputValue value)
    {
        if (value.isPressed)
            SprintPressed = value.isPressed;
        SprintHeld = value.isPressed;
    }

    public void ConsumeSprint()
    {
        SprintPressed = false;
    }
    public bool InteractPressed { get; private set; }

public void OnInteract(InputValue value)
{
    if (value.isPressed) InteractPressed = true;
}

public void ConsumeInteract()
{
    InteractPressed = false;
}

    public void ConsumeJump()
    {
        JumpPressed = false;
    }

    void OnDisable()
    {
        Move = Vector2.zero;
        Look = Vector2.zero;
        SprintHeld = false;
        JumpPressed = false;
        InteractPressed = false;
    }
}