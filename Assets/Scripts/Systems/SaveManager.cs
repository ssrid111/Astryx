using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    private const int CurrentVersion = 1;
    private const string CableId = "repair_cable";

    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private RepairCablePickup cablePickup;
    [SerializeField] private RepairPanel repairPanel;
    [SerializeField] private ExitDoor exitDoor;

    public string SavePath => Path.Combine(Application.persistentDataPath, "astryx-demo-save.json");

    private void Start()
    {
        Debug.Log($"Guardado demo: F5 guardar / F9 cargar. Archivo: {SavePath}", this);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!Application.isFocused || keyboard == null)
            return;
        if (keyboard.f5Key.wasPressedThisFrame)
            SaveGame();
        else if (keyboard.f9Key.wasPressedThisFrame)
            LoadGame();
    }

    private bool IsConfigured()
    {
        if (playerInventory != null && cablePickup != null && repairPanel != null
            && exitDoor != null && repairPanel.CanRestore && exitDoor.CanRestore)
            return true;

        Debug.LogWarning("Configura las cuatro referencias de SaveManager y los sprites del panel y puerta.", this);
        return false;
    }

    public void SaveGame()
    {
        if (!IsConfigured())
            return;

        Vector3 position = playerInventory.transform.position;
        GameData data = new GameData
        {
            saveVersion = CurrentVersion,
            sceneName = SceneManager.GetActiveScene().name,
            playerPosition = new PlayerPositionData { x = position.x, y = position.y, z = position.z },
            inventory = playerInventory.HasRepairCable
                ? new[] { new InventoryItemData { itemId = CableId, quantity = 1 } }
                : Array.Empty<InventoryItemData>(),
            spaceship = new SpaceshipData { powerRepaired = repairPanel.IsRepaired },
            cableCollected = cablePickup.IsCollected,
            demoCompleted = exitDoor.IsDemoCompleted
        };

        try
        {
            Validate(data);
            Directory.CreateDirectory(Application.persistentDataPath);
            string temporaryPath = SavePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
            // Replace only after the complete temporary file has been written.
            // Keep the previous successful save as a backup.
            if (File.Exists(SavePath))
                File.Replace(temporaryPath, SavePath, SavePath + ".bak");
            else
                File.Move(temporaryPath, SavePath);
            Debug.Log($"Partida guardada: {SavePath}", this);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is ArgumentException || error is NotSupportedException)
        {
            Debug.LogWarning($"No se pudo guardar: {error.Message}", this);
        }
    }

    public void LoadGame()
    {
        if (!IsConfigured())
            return;

        try
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("No hay partida guardada. Se conserva la partida actual.", this);
                return;
            }

            string json = File.ReadAllText(SavePath);
            GameData data = JsonUtility.FromJson<GameData>(json);
            Validate(data); // Do not mutate the scene until all data is checked.

            bool hasCable = data.inventory.Length == 1;
            playerInventory.RestoreState(hasCable);
            cablePickup.RestoreState(data.cableCollected);
            repairPanel.RestoreState(data.spaceship.powerRepaired);
            exitDoor.RestoreState(data.demoCompleted);

            Vector3 position = new Vector3(data.playerPosition.x, data.playerPosition.y, data.playerPosition.z);
            Rigidbody2D body = playerInventory.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.position = new Vector2(position.x, position.y);
            }
            playerInventory.transform.position = position;
            Physics2D.SyncTransforms();
            Debug.Log("Partida cargada: posicion, inventario y progreso restaurados.", this);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is ArgumentException || error is NotSupportedException)
        {
            Debug.LogWarning($"No se pudo cargar. Se conserva la partida actual. {error.Message}", this);
        }
    }

    private static void Validate(GameData data)
    {
        if (data == null || data.saveVersion != CurrentVersion)
            throw new ArgumentException("Formato o version de guardado incompatible.");
        if (data.sceneName != SceneManager.GetActiveScene().name)
            throw new ArgumentException("El guardado pertenece a otra escena.");
        if (data.playerPosition == null || data.inventory == null || data.spaceship == null)
            throw new ArgumentException("Faltan datos obligatorios.");
        if (!IsFinite(data.playerPosition.x) || !IsFinite(data.playerPosition.y)
            || !IsFinite(data.playerPosition.z) || Mathf.Abs(data.playerPosition.z) > 0.001f)
            throw new ArgumentException("La posicion guardada no es valida para la demo 2D.");
        if (data.inventory.Length > 1 || (data.inventory.Length == 1
            && (data.inventory[0] == null || data.inventory[0].itemId != CableId
                || data.inventory[0].quantity != 1)))
            throw new ArgumentException("Inventario no compatible con esta demo.");

        bool hasCable = data.inventory.Length == 1;
        if (data.cableCollected != (hasCable || data.spaceship.powerRepaired)
            || (hasCable && data.spaceship.powerRepaired)
            || (data.demoCompleted && !data.spaceship.powerRepaired))
            throw new ArgumentException("El estado del cable, panel y salida es inconsistente.");
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
