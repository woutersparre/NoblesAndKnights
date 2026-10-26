using UnityEngine;

/// <summary>
/// Initializes and registers all actions at game start
/// Attach this to a GameObject in your scene
/// </summary>
public class ActionInitializer : MonoBehaviour
{
    private void Start()
    {
        RegisterAllActions();
        //Debug.Log("Action system initialized with all actions registered.");
    }
    
    private void RegisterAllActions()
    {
        if (ActionManager.instance == null)
        {
            Debug.LogError("ActionManager instance not found! Make sure ActionManager is in the scene.");
            return;
        }
        
        // Register movement action
        ActionManager.instance.RegisterAction(new TravelAction());
        
        // Register the generic surface (explore) and deep placeholder actions for every location type
        LocationActionFactory.AddAll(ActionManager.instance);

        // Register placeholder Intrigue plots/schemes (CK2-style). These are the
        // first slice of the Intrigue screen; targeting/backers/plot power follow.
        IntrigueActionFactory.AddAll(ActionManager.instance);

        // TODO: Add more actions as you create them
        // Example templates for different action types:
        
        // Military actions
        // ActionManager.instance.RegisterAction(new RaiseLevyAction());
        // ActionManager.instance.RegisterAction(new TrainTroopsAction());
        // ActionManager.instance.RegisterAction(new RaidAction());
        
        // Economic actions  
        // ActionManager.instance.RegisterAction(new TradeAction());
        // ActionManager.instance.RegisterAction(new CollectTaxesAction());
        // ActionManager.instance.RegisterAction(new DevelopLandAction());
        
        // Diplomatic actions
        // ActionManager.instance.RegisterAction(new DiplomaticMissionAction());
        // ActionManager.instance.RegisterAction(new ArrangeMarriageAction());
        // ActionManager.instance.RegisterAction(new FormAllianceAction());
        
        // Religious actions
        // ActionManager.instance.RegisterAction(new PilgrimageAction());
        // ActionManager.instance.RegisterAction(new BuildTempleAction());
        // ActionManager.instance.RegisterAction(new HoldFeastAction());
        
        // Intrigue actions
        // ActionManager.instance.RegisterAction(new SpyAction());
        // ActionManager.instance.RegisterAction(new SabotageAction());
        // ActionManager.instance.RegisterAction(new SpreadRumorsAction());
        
        //Debug.Log($"Registered {ActionManager.instance.GetAllActions().Count} actions.");
    }
    
    /// <summary>
    /// Helper method to register an action with error handling
    /// </summary>
    private void SafeRegisterAction(IAction action)
    {
        try
        {
            ActionManager.instance.RegisterAction(action);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to register action {action.Name}: {e.Message}");
        }
    }
}
