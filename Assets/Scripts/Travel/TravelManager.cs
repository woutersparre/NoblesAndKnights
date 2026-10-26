using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Orchestrates physical travel: spawns a MapPawn on departure, moves characters (or supplied ArmyFormations)
// along a path, and swaps logical state to the destination on arrival before despawning the pawn.
public class TravelManager : MonoBehaviour
{
    public static TravelManager instance;

    // Fires when any pawn departs, reroutes, or arrives so UI can refresh location panels
    public static event System.Action OnTravelStateChanged;

    [Header("Pawn Setup")]
    public GameObject PawnPrefab;              // Optional; if null, a plain GameObject with a MapPawn component is created
    public Transform PawnParent;               // Optional container for all pawns

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public void RequestTravel(Character character, Location destination)
    {
        if (character == null)
        {
            Debug.LogError("RequestTravel: character is null");
            return;
        }
        if (destination == null)
        {
            Debug.LogError("RequestTravel: destination is null");
            return;
        }
        if (character.TravelDestination == destination && character.IsTraveling)
            return; // already en route

        // Redirect an in-flight pawn to the new destination instead of despawning it and starting over
        if (character.IsTraveling && character.ActivePawn != null)
        {
            RedirectPawn(character.ActivePawn, destination);
            character.TravelDestination = destination;
            OnTravelStateChanged?.Invoke();
            return;
        }

        Location origin = character.CurrentLocation;
        if (origin == null)
        {
            Debug.LogError($"RequestTravel: {character.Name} has no CurrentLocation to depart from");
            return;
        }
        if (origin == destination)
        {
            Debug.LogWarning($"RequestTravel: {character.Name} is already at {destination.Name}");
            return;
        }

        List<Vector2> path = BuildPath(origin, destination);

        character.TravelDestination = destination;
        MapPawn pawn = SpawnPawn(origin, destination, path, new[] { character }, null);

        // Logical state: character is now "in transit" and its presence lives on the pawn
        character.ActivePawn = pawn;
        character.CurrentLocation = origin; // stays at origin until arrival

        OnTravelStateChanged?.Invoke();
    }

    // Re-aims an in-flight pawn at a new destination without interrupting movement
    private void RedirectPawn(MapPawn pawn, Location destination)
    {
        List<Vector2> path = BuildPathFromPoint(pawn.transform.position, destination);
        pawn.Reroute(destination, path);
    }

    private List<Vector2> BuildPathFromPoint(Vector2 start, Location destination)
    {
        if (TravelPlanner.instance != null)
            return TravelPlanner.instance.PlanPathFrom(start, destination);

        return new List<Vector2> { start, destination.Position };
    }

    // Sends a full ArmyFormation (no characters) from one location to another.
    public void RequestArmyTravel(Location origin, Location destination, Character commander, float troops)
    {
        if (origin == null || destination == null)
        {
            Debug.LogError("RequestArmyTravel: origin or destination is null");
            return;
        }
        if (origin == destination)
        {
            Debug.LogWarning($"RequestArmyTravel: army is already at {destination.Name}");
            return;
        }

        List<Vector2> path = BuildPath(origin, destination);
        SpawnPawn(origin, destination, path, null, new ArmyFormation(commander, troops));
    }

    private MapPawn SpawnPawn(Location origin, Location destination, List<Vector2> path, IEnumerable<Character> characters, ArmyFormation army)
    {
        MapPawn pawn;
        if (PawnPrefab != null)
        {
            GameObject go = Instantiate(PawnPrefab, PawnParent != null ? PawnParent : transform);
            pawn = go.GetComponent<MapPawn>();
            if (pawn == null)
                pawn = go.AddComponent<MapPawn>();
        }
        else
        {
            GameObject go = new GameObject($"MapPawn {destination.Name}");
            if (PawnParent != null)
                go.transform.SetParent(PawnParent, false);
            pawn = go.AddComponent<MapPawn>();

            // Default visible marker so the pawn can be seen without a prefab (use PawnPrefab for a real unit sprite)
            var sr = go.AddComponent<SpriteRenderer>();
            Character lead = characters != null ? characters.FirstOrDefault() : null;
            sr.sprite = lead != null && lead.Portrait != null ? lead.Portrait : CreateDefaultMarker();
            if (army != null)
                sr.color = new Color(0.85f, 0.25f, 0.25f); // red marker for armies
            sr.sortingOrder = 50;
            go.transform.localScale = Vector3.one * 3f;
        }

        pawn.Initialize(origin, destination, path, characters);
        if (army != null)
        {
            pawn.Armies.Clear();
            pawn.Armies.Add(army);
        }

        return pawn;
    }

    // Called by MapPawn when it reaches the destination
    public void CompleteArrival(MapPawn pawn)
    {
        if (pawn == null)
            return;

        foreach (var c in pawn.Characters)
        {
            c.ActivePawn = null;
            c.CurrentLocation = pawn.Destination;
            c.TravelDestination = null;

            // Physically arriving somewhere means you have visited it (unlocks surface discoveries)
            pawn.Destination?.Visit(c);

            // Notify the player when they arrive at a location
            if (c == MapManager.instance.characters.Player && MessageManager.instance != null)
            {
                MessageManager.instance.Send("{0} has arrived at {1}", c, pawn.Destination);
            }
        }
        pawn.Characters.Clear();

        OnTravelStateChanged?.Invoke();

        if (pawn.Armies.Count > 0)
            Debug.Log($"[TravelManager] Army arrived at {pawn.Destination?.Name}");
        pawn.Armies.Clear();

        // Pawn despawns once it carries no travellers
        if (pawn.Characters.Count == 0 && pawn.Armies.Count == 0)
        {
            Destroy(pawn.gameObject);
        }
    }

    private List<Vector2> BuildPath(Location origin, Location destination)
    {
        // Route through the land-only pathfinder when available, otherwise travel straight there.
        if (TravelPlanner.instance != null)
            return TravelPlanner.instance.PlanPath(origin, destination);

        var waypoints = new List<Vector2>();
        waypoints.Add(origin.Position);
        waypoints.Add(destination.Position);
        return waypoints;
    }

    // Generates a tiny white square Sprite for the fallback pawn marker (no assets required)
    private static Sprite CreateDefaultMarker()
    {
        var tex = new Texture2D(2, 2);
        tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
    }
}