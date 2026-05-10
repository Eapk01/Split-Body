using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalMultiplayerSpawner : MonoBehaviour
{
    [SerializeField] GameObject keyboardPlayerPrefab;
    [SerializeField] GameObject gamepadPlayerPrefab;
    public static Transform Transform;

    void Start()
    {
        SpawnPlayers();
        Transform = transform;
    }

    void SpawnPlayers()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        PlayerInput playerA = null;
        PlayerInput playerB = null;

        if (keyboard != null && mouse != null)
        {
            playerA = PlayerInput.Instantiate(
                keyboardPlayerPrefab,
                controlScheme: "Keyboard&Mouse",
                pairWithDevices: new InputDevice[] { keyboard, mouse }
            );
        }

        if (Gamepad.all.Count > 0)
        {
            playerB = PlayerInput.Instantiate(
                gamepadPlayerPrefab,
                controlScheme: "Gamepad",
                pairWithDevices: new InputDevice[] { Gamepad.all[0] }
            );
        }

        if(playerA == null || playerB == null)
        {
            print("Needs another controller!");
            return;
        }

        GameManager.Instance.RegisterPlayers(playerA.gameObject, playerB.gameObject);
    }
}