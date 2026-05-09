using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SplitScreenCameraManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] PlayerInputManager playerInputManager;
    [SerializeField] GameObject cameraRigPrefab;

    [Header("Target Settings")]
    [SerializeField] string cameraTargetName = "CameraTarget";

    readonly List<PlayerCameraEntry> playerCameras = new();

    class PlayerCameraEntry
    {
        public PlayerInput playerInput;
        public Camera camera;
        public ThirdPersonCameraFollow follow;
        public GameObject rigInstance;
    }

    void Awake()
    {
        if (playerInputManager == null)
            playerInputManager = PlayerInputManager.instance;
    }

    void OnEnable()
    {
        if (playerInputManager != null)
            playerInputManager.onPlayerJoined += OnPlayerJoined;
    }

    void OnDisable()
    {
        if (playerInputManager != null)
            playerInputManager.onPlayerJoined -= OnPlayerJoined;
    }

    void OnPlayerJoined(PlayerInput player)
    {
        if (cameraRigPrefab == null)
        {
            Debug.LogError("SplitScreenCameraManager: Camera rig prefab is not assigned.");
            return;
        }

        GameObject camRig = Instantiate(cameraRigPrefab);

        Camera cam = camRig.GetComponentInChildren<Camera>();
        ThirdPersonCameraFollow follow = camRig.GetComponentInChildren<ThirdPersonCameraFollow>();

        if (cam == null)
        {
            Debug.LogError("SplitScreenCameraManager: No Camera found in camera rig prefab.");
            Destroy(camRig);
            return;
        }

        if (follow == null)
        {
            Debug.LogError("SplitScreenCameraManager: No ThirdPersonCameraFollow found in camera rig prefab.");
            Destroy(camRig);
            return;
        }

        Transform target = player.transform.Find(cameraTargetName);
        if (target == null)
        {
            Debug.LogError($"SplitScreenCameraManager: Could not find '{cameraTargetName}' under player '{player.name}'.");
            Destroy(camRig);
            return;
        }

        PlayerInputReader input = player.GetComponent<PlayerInputReader>();
        if (input == null)
        {
            Debug.LogError($"SplitScreenCameraManager: Player '{player.name}' has no PlayerInputReader.");
            Destroy(camRig);
            return;
        }

        ThirdPersonMotor motor = player.GetComponent<ThirdPersonMotor>();
        if (motor == null)
        {
            Debug.LogError($"SplitScreenCameraManager: Player '{player.name}' has no ThirdPersonMotor.");
            Destroy(camRig);
            return;
        }

        follow.SetTarget(target);
        follow.SetInput(input, player.currentControlScheme);

        player.camera = cam;

        motor.SetCamera(cam.transform);
        motor.SetSchema(player.currentControlScheme);

        var entry = new PlayerCameraEntry
        {
            playerInput = player,
            camera = cam,
            follow = follow,
            rigInstance = camRig
        };

        playerCameras.Add(entry);

        DisableExtraAudioListeners();
        RefreshCameraLayout();

        Debug.Log($"Player joined: {player.playerIndex}, total players: {playerCameras.Count}");
    }

    void RefreshCameraLayout()
    {
        int count = playerCameras.Count;

        for (int i = 0; i < count; i++)
        {
            Camera cam = playerCameras[i].camera;
            if (cam == null)
                continue;

            cam.rect = GetSplitRect(i, count);
            cam.depth = i;
        }
    }

    Rect GetSplitRect(int index, int totalPlayers)
    {
        if (totalPlayers <= 1)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        if (totalPlayers == 2)
        {
            return new Rect(
                index == 0 ? 0f : 0.5f,
                0f,
                0.5f,
                1f
            );
        }

        if (totalPlayers == 3)
        {
            if (index == 0)
                return new Rect(0f, 0.5f, 1f, 0.5f);

            if (index == 1)
                return new Rect(0f, 0f, 0.5f, 0.5f);

            return new Rect(0.5f, 0f, 0.5f, 0.5f);
        }

        return new Rect(
            index % 2 == 0 ? 0f : 0.5f,
            index < 2 ? 0.5f : 0f,
            0.5f,
            0.5f
        );
    }

    void DisableExtraAudioListeners()
    {
        bool firstListenerKept = false;

        for (int i = 0; i < playerCameras.Count; i++)
        {
            Camera cam = playerCameras[i].camera;
            if (cam == null)
                continue;

            AudioListener listener = cam.GetComponent<AudioListener>();
            if (listener == null)
                continue;

            if (!firstListenerKept)
            {
                listener.enabled = true;
                firstListenerKept = true;
            }
            else
            {
                listener.enabled = false;
            }
        }
    }
}