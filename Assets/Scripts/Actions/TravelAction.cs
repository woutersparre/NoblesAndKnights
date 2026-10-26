using UnityEngine;

/// <summary>
/// Generic travel action. Moves the character to the target location and is always visible
/// (except when the character is already at the location). Arriving at a location is what
/// marks it visited, unlocking surface-level actions.
/// </summary>
public class TravelAction : BaseAction
{
    public override string Name => "Travel";
    public override string Description => "Travel to this location.";
    public override ActionType Type => ActionType.Travel;

    // Valid everywhere
    public override LocationType[] ValidLocationTypes => new LocationType[0];
    public override bool RequiresRuler => false;
    public override bool IsRepeatable => true;
    public override DiscoveryVisibility Visibility => DiscoveryVisibility.Any;

    public override bool IsAvailable(Character character, Location location)
    {
        if (character == null || location == null)
            return false;
        // Already physically at this location
        if (!character.IsTraveling && character.CurrentLocation == location)
            return false;
        // Already en route to this location
        if (character.IsTraveling && character.TravelDestination == location)
            return false;
        return true;
    }

    public override ActionResult Execute(Character character, Location location)
    {
        if (character == null || location == null)
            return new ActionResult(false, "Invalid target");

        // Only "at" the location if physically present (a traveling character is still en route,
        // so CurrentLocation pointing at the origin shouldn't block a redirect home)
        bool isAtLocation = !character.IsTraveling && character.CurrentLocation == location;
        if (!isAtLocation)
        {
            TravelManager.instance?.RequestTravel(character, location);
            return new ActionResult(true, $"Traveling to {location.Name}");
        }

        return new ActionResult(true, $"At {location.Name}");
    }
}