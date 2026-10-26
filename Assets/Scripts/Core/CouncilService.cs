using System.Collections.Generic;
using System.Linq;

// Headless council rules. Both the player UI (CouncilorFinder / EmptyCouncilSlot) and
// the NPC AI go through here so player and AI appointments cannot drift apart.
// Deliberately free of UI concerns: refreshing the council screen is the caller's job.
public static class CouncilService
{
    public enum AppointResult
    {
        Success,
        Ineligible,
        NoCandidate
    }

    // The pool of characters this ruler may appoint to this post.
    public static List<Character> GetEligibleCandidates(Character ruler, CouncilPosition position)
    {
        if (ruler == null || position == null) return new List<Character>();

        // GetRealmCharacters() walks the realm from the ruler and includes the ruler
        // itself as the first entry; IsEligible drops them along with anyone else
        // who cannot hold the post.
        return ruler.GetRealmCharacters()
            .Where(c => CouncilRoleInfo.IsEligible(position.Role, ruler, c))
            .ToList();
    }

    public static bool CanAppoint(Character ruler, CouncilPosition position, Character candidate)
    {
        if (ruler == null || position == null || candidate == null) return false;
        return CouncilRoleInfo.IsEligible(position.Role, ruler, candidate);
    }

    // Fills a post, stopping any deployment it already had. Does not touch UI:
    // the caller owns the refresh, so the council screen is not rebuilt twice.
    public static AppointResult Appoint(Character ruler, CouncilPosition position, Character candidate)
    {
        if (position == null || candidate == null) return AppointResult.NoCandidate;
        if (!CanAppoint(ruler, position, candidate)) return AppointResult.Ineligible;

        if (position.IsWorking)
            CouncilIndicatorManager.Ensure().CancelDeployment(position, refreshUi: false);

        candidate.AssignToCouncil(position);
        return AppointResult.Success;
    }

    // Gives a post its role. Refuses roles the ruler already staffs elsewhere.
    public static bool ChooseRole(Character ruler, CouncilPosition position, CouncilRole role)
    {
        if (ruler == null || position == null) return false;
        if (!CouncilRoleInfo.IsUnlocked(role, ruler)) return false;
        if (CouncilRoleInfo.IsRoleInUse(role, ruler)) return false;

        position.Role = role;
        return true;
    }

    // Empties a post: drops the appointment and stops any deployment it had, so no
    // stale map indicator or day-tick countdown is left behind for an unfilled slot.
    // Callers that refresh the council screen themselves should pass refreshUi: false.
    public static void Vacate(CouncilPosition position, bool refreshUi = true)
    {
        if (position == null) return;

        // Cancel first, so the departure message still names the outgoing councilor.
        if (position.IsWorking)
            CouncilIndicatorManager.Ensure().CancelDeployment(position, refreshUi);

        if (position.Appointed != null && position.Appointed.ServesOnCouncil == position)
            position.Appointed.ServesOnCouncil = null;

        position.Appointed = null;
    }
}