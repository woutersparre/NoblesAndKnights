using System.Collections.Generic;

// An army travelling on a MapPawn. Dispatched for now via Location.RaiseLocalLevy toward the player's capital.
public class ArmyFormation
{
    public Character Commander;
    public float Troops;

    public ArmyFormation(Character commander, float troops)
    {
        Commander = commander;
        Troops = troops;
    }
}