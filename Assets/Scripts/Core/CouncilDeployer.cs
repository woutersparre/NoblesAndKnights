using UnityEngine;

// Coordinates the map-click deploy flow: hides the council screen, asks the player to
// click a location on the map, then spawns the councilor indicator there.
public static class CouncilDeployer
{
    private static CouncilPosition pendingPosition;
    private static CouncilRoleAction pendingAction;

    // Called when the player picks a deployment action for an appointed councilor.
    public static void StartDeploy(CouncilPosition position, CouncilRoleAction action)
    {
        if (position == null || action == null) return;
        if (position.Appointed == null) return;

        pendingPosition = position;
        pendingAction = action;

        Debug.Log($"[CouncilDeployer] StartDeploy: {action.Name} on {position.PositionName} for {position.Appointed.Name}.");

        if (CouncilUI.instance != null)
            CouncilUI.instance.HideWindow();

        UnifiedSelectionManager.RequestLocationClick(OnLocationChosen);
    }

    // Runs on the next map click. location is null when empty land was clicked (cancels).
    static void OnLocationChosen(Location location)
    {
        CouncilPosition position = pendingPosition;
        CouncilRoleAction action = pendingAction;
        pendingPosition = null;
        pendingAction = null;

        UnifiedSelectionManager.CancelLocationClick();

        if (position == null || action == null)
        {
            Debug.Log("[CouncilDeployer] OnLocationChosen: no pending deploy, reopening council.");
            ReopenCouncil();
            return;
        }

        if (location == null)
        {
            Debug.Log("[CouncilDeployer] OnLocationChosen: empty land clicked, deployment cancelled.");
            ReopenCouncil();
            return;
        }

        position.BeginDeployment(action, location);
        CouncilIndicatorManager.Ensure().CreateIndicator(position);

        Debug.Log($"[CouncilDeployer] Deployed {position.Appointed?.Name} to {location.Name} ({action.Name}).");
        ReopenCouncil();
    }

    // Manual cancel from the council screen action list.
    public static void CancelDeployment(CouncilPosition position)
    {
        CouncilIndicatorManager.Ensure().CancelDeployment(position);
    }

    static void ReopenCouncil()
    {
        if (MapManager.instance != null && MapManager.instance.characters != null && GameUIManager.instance != null)
            GameUIManager.instance.OpenCouncilScreen();
    }
}