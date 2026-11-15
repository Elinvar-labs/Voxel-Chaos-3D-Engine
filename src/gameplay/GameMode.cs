namespace VoxelChaos.Gameplay;

public enum GameMode
{
    Spectator,  // свободный полёт, нет коллизий, нельзя ломать/ставить
    Creative,   // полёт, нет коллизий, мгновенно ломает/ставит
    Survival    // гравитация, коллизии, может ломать/ставить
}