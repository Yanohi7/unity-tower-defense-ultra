using System.Collections.Generic;

public static class EnemyRegistry
{
    // List of all active enemies currently in the scene.
    public static readonly List<Enemy> ActiveEnemies = new List<Enemy>();

    // Method to add an enemy to the active enemy list
    public static void Register(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (!ActiveEnemies.Contains(enemy))
            ActiveEnemies.Add(enemy);
    }

    // Method to remove an enemy from the active enemy list
    public static void Unregister(Enemy enemy)
    {
        if (enemy == null)
            return;

        ActiveEnemies.Remove(enemy);
    }
}