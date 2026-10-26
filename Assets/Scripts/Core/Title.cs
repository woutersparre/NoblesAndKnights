using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public abstract class Title
{
    public string Name; // Title name (e.g., "Duke of Normandy")
    public Character Holder; // The character holding this title
    public abstract Vector3 Location { get; }
    public Color Color;

    public Title Liege;
    public List<Title> Vassals = new();

    public Title(TitleRank _rank, string name = null, Character holder = null)
    {
        Name = name;
        Holder = holder;
        Color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1F);
        Rank = _rank;
    }

    public TitleRank Rank;

    public abstract Sprite Image { get; }
}
public enum TitleRank
{
    Holding,
    Province,
    Duchy,
    Kingdom,
    Empire
}
public class HoldingTitle : Title
{
    public override Vector3 Location => AssociatedLocation.Province.Center;
    public override Sprite Image => AssociatedLocation.Image;

    public Location AssociatedLocation;

    public HoldingTitle(string _name, Character _character, Location location) : base(TitleRank.Holding, _name, _character)
    {
        AssociatedLocation = location;
        Color = location.Province.MapColor;
    }
}

public class RegionTitle : Title
{
    public override Vector3 Location => AssociatedRegion.Capital.Province.Center;
    public override Sprite Image
        {get{return AssociatedRegion.Capital.Image;}}


    public Region AssociatedRegion; // The region tied to this title
    public List<Title> SubTitles;

    public RegionTitle(string _name, Character _character, Region _region,TitleRank _rank,List<Title> _subTitles) : base(_rank, _name, _character)
    {
        AssociatedRegion = _region;
        SubTitles = _subTitles;
        Rank = _rank;
        Color = _region.MapColor;
    }
}
