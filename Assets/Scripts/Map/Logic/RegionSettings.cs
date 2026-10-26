using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RegionSettings
{
    [Header("Global Formation")]
    [Tooltip("Maximum number of optimization passes for province formation")]
    public int maximumPasses = 10;
        
    [Tooltip("Maximum restart attempts if constraint satisfaction fails")]
    public int maxRestartAttempts = 5;
        
    [Tooltip("Enable debug output during region formation")]
    public bool enableDebugOutput = false;

    [Header("Province Formation (Cell-Level)")]
    public ProvinceSettings province = new ProvinceSettings();

    [Header("Higher Tier Formation (Province-Level)")]
    [Tooltip("Duchy size range in number of provinces")]
    public RegionTierSettings duchy = new RegionTierSettings(2, 5);
        
    [Tooltip("Kingdom size range in number of duchies")]
    public RegionTierSettings kingdom = new RegionTierSettings(3, 5);
        
    [Tooltip("Empire size range in number of kingdoms")]
    public RegionTierSettings empire = new RegionTierSettings(2, 5);
}

[System.Serializable]
public class ProvinceSettings
{
    [Header("Expansion Parameters")]
    [Tooltip("River Penalty")]
    public float riverPenalty = 0.5f;

    [Tooltip("Mountain Expansion Penalty")]
    public float mountainExpansionPenalty = 0.5f;

    [Tooltip("Neigbour expansion bonus")]
    public float neighborBonus = 0.2f;

    [Header("Poisson Disk Sampling")]
    [Tooltip("Minimum distance between province seed points (in world units)")]
    public float minSeedDistance = 15f;

    [Header("Lloyd's Relaxation")]
    [Tooltip("Number of Lloyd's relaxation passes for province optimization")]
    public int lloydRelaxationPasses = 1;

}

    [System.Serializable]
public class RegionTierSettings
{
    [Tooltip("Minimum number of subregions per region")]
    public int minSubRegions;
        
    [Tooltip("Maximum number of subregions per region")]
    public int maxSubRegions;



    public RegionTierSettings(int minSubRegions, int maxSubRegions)
    {
        this.minSubRegions = minSubRegions;
        this.maxSubRegions = maxSubRegions;
    }
}
