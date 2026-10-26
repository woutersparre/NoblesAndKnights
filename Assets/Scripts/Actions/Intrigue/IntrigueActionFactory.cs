using UnityEngine;

/// <summary>
/// Registers the placeholder Intrigue plots so they appear alongside other actions.
/// Mirrors LocationActionFactory: one call from ActionInitializer.
/// </summary>
public static class IntrigueActionFactory
{
    public static void AddAll(ActionManager manager)
    {
        if (manager == null) return;

        // (name, description, required intrigue, gold cost, days, failure chance)
        var plots = new (string name, string description, int req, float gold, float days, float risk)[]
        {
            ("Compromise", "Learn something damaging a target would pay to keep quiet.",
                4, 10f, 14f, 0.20f),
            ("Blackmail", "Convert a known secret into money, a post or a favour.",
                5, 0f, 3f, 0.20f),
            ("Seduce", "Bind someone to you through a relationship rather than a secret.",
                5, 10f, 28f, 0.20f),
            ("Spy On", "Infiltrate a household to learn what leverage does not yet cover.",
                4, 15f, 7f, 0.15f),
            ("Poison", "Remove a rival quietly. The least deniable of the set.",
                7, 40f, 10f, 0.35f),
            ("Assassinate", "Remove a rival definitively. Slower, deadlier, ruinous if caught.",
                8, 50f, 21f, 0.40f),
            ("Fabricate Claim", "Manufacture a document supporting a claim to a title.",
                6, 30f, 21f, 0.25f),
            ("Forge Document", "Manufacture a lineage or decree. Rare and dynasty-risking.",
                10, 100f, 45f, 0.45f),
            ("Abduct", "Seize a target and hold them for leverage.",
                7, 40f, 10f, 0.30f),
            ("Turn Against Liege", "Convince a vassal to oppose their liege.",
                7, 25f, 21f, 0.30f),
            ("Revoke Title (Plot)", "Build support to strip a title without tyranny.",
                8, 40f, 28f, 0.30f),
            ("Convert (Secret)", "Covertly shift a target's faith or culture.",
                6, 20f, 21f, 0.25f),
            ("Lower Crown Authority", "Undermine the crown's legal authority over time.",
                9, 60f, 30f, 0.35f),
            ("Discover Plots", "Actively hunt for schemes against you and your court.",
                4, 5f, 7f, 0.10f),
        };

        foreach (var p in plots)
        {
            manager.RegisterAction(new IntriguePlotAction(
                p.name, p.description, p.req, p.gold, p.days, p.risk));
        }
    }
}