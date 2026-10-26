using System.Collections.Generic;
using UnityEngine;

// A single polymorphic pawn that moves across the map holding one or more characters (and, in future, army formations).
// Movement is hybrid: the transform lerps in real time, but its effective speed is scaled by the TimeManager state
// (pause freezes, fast-forward accelerates) because Time.deltaTime already reflects Time.timeScale.
public class MapPawn : MonoBehaviour
{
    public List<Character> Characters { get; private set; } = new();
    public List<ArmyFormation> Armies { get; private set; } = new();

    public Location Origin;
    public Location Destination;

    // Waypoints being followed (world positions)
    private List<Vector2> waypoints = new();
    private int waypointIndex = 0;

    // Movement
    public float baseSpeed = 8f; // world units per second at normal time speed

    public bool IsMoving => waypointIndex < waypoints.Count;

    public void Initialize(Location origin, Location destination, List<Vector2> path, IEnumerable<Character> characters)
    {
        Origin = origin;
        Destination = destination;
        Characters.Clear();
        if (characters != null)
        {
            foreach (var c in characters)
                Characters.Add(c);
        }

        waypoints = new List<Vector2>(path);
        waypointIndex = 0;

        if (waypoints.Count == 0)
        {
            // No path segments: arrive immediately next frame
            transform.position = destination.Position;
        }
        else
        {
            transform.position = waypoints[0];
            waypointIndex = 1;
        }
    }

    // Points the pawn at a new destination without interrupting movement. newPath starts
    // from the pawn's current position (waypoints[0] == current transform position).
    public void Reroute(Location newDestination, List<Vector2> newPath)
    {
        Destination = newDestination;

        if (newPath == null || newPath.Count < 2)
        {
            // Degenerate path: head to the destination in a straight line
            waypoints = new List<Vector2> { transform.position, newDestination.Position };
            waypointIndex = 1;
            return;
        }

        waypoints = new List<Vector2>(newPath);
        waypointIndex = 1;
        transform.position = waypoints[0];
    }

    void Update()
    {
        if (!IsMoving)
            return;

        // Time.deltaTime is already scaled by Time.timeScale (0 when paused, 3x in fast-forward) so hybrid timing works automatically.
        float step = baseSpeed * Time.deltaTime;
        Vector2 target = waypoints[waypointIndex];
        Vector2 pos = transform.position;

        if (Vector2.Distance(pos, target) <= step)
        {
            // snap to waypoint and advance
            transform.position = target;
            waypointIndex++;
            if (!IsMoving)
            {
                Arrived();
                return;
            }
        }
        else
        {
            transform.position = Vector2.MoveTowards(pos, target, step);
        }
    }

    private void Arrived()
    {
        transform.position = Destination.Position;
        TravelManager.instance?.CompleteArrival(this);
    }

    // Adds a character to this pawn (shared pawns can carry several travellers)
    public void AddCharacter(Character c)
    {
        if (!Characters.Contains(c))
            Characters.Add(c);
    }

    public void RemoveCharacter(Character c)
    {
        Characters.Remove(c);
        if (Characters.Count == 0 && Armies.Count == 0)
        {
            Destroy(gameObject); // nobody left to carry
        }
    }

    void OnDestroy()
    {
        foreach (var c in Characters)
        {
            if (c.ActivePawn == this)
                c.ActivePawn = null;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        if (waypoints.Count > 0)
        {
            Vector3 prev = waypoints[0];
            for (int i = 1; i < waypoints.Count; i++)
            {
                Gizmos.DrawLine(prev, waypoints[i]);
                prev = waypoints[i];
            }
        }
    }
}