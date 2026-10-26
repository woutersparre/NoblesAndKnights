using UnityEngine;
public class MapModeController : MonoBehaviour
{
    public void ShowProvinces()
        => MapManager.instance.MapModes.ShowProvinces();

    public void ShowDuchies()
        => MapManager.instance.MapModes.ShowDuJureDuchies();

    public void ShowKingdoms()
        => MapManager.instance.MapModes.ShowDuJureKingdoms();

    public void ShowEmpires()
        => MapManager.instance.MapModes.ShowDuJureEmpires();

public void ShowRealms()
        => MapManager.instance.MapModes.ShowRealms();

    public void ShowTerrain()
        => MapManager.instance.MapModes.ShowTerrain();

    public void ShowCultures()
        => MapManager.instance.MapModes.ShowCultures();

    public void ShowReligions()
        => MapManager.instance.MapModes.ShowReligions();
}
