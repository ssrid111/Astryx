using System;

// Plain data only: no scene object references or MonoBehaviours in JSON.
[Serializable]
public class GameData
{
    public int saveVersion;
    public string sceneName;
    public PlayerPositionData playerPosition;
    public InventoryItemData[] inventory;
    public SpaceshipData spaceship;
    public bool cableCollected;
    public bool demoCompleted;
}

[Serializable]
public class PlayerPositionData
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class InventoryItemData
{
    public string itemId;
    public int quantity;
}

[Serializable]
public class SpaceshipData
{
    public bool powerRepaired;
}
