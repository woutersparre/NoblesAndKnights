using UnityEngine;

namespace SphereVoronoiPrototype
{
    // Applies the authored settings panel to the generator. Kept separate from
    // SphereVoronoiSettingsUI so the view has no generation code in it.
    public class SphereVoronoiSettingsController : MonoBehaviour
    {
        [Header("References")]
        public SphereVoronoiGenerator generator;
        public SphereVoronoiPicker picker;
        public SphereVoronoiSettingsUI ui;

        private void Start()
        {
            if (generator == null)
                generator = FindFirstObjectByType<SphereVoronoiGenerator>();
            if (picker == null)
                picker = FindFirstObjectByType<SphereVoronoiPicker>();
            if (ui == null)
                ui = FindFirstObjectByType<SphereVoronoiSettingsUI>();

            if (generator == null)
            {
                Debug.LogError("[SphereVoronoiSettingsController] No SphereVoronoiGenerator found.");
                enabled = false;
                return;
            }

            if (ui == null || !ui.IsReady)
            {
                Debug.LogError("[SphereVoronoiSettingsController] No authored settings panel found. " +
                    "Rebuild it from Tools > Sphere Voronoi > Rebuild Settings UI.", this);
                enabled = false;
                return;
            }

            ui.SetValues(generator.seedCount);

            ui.RegenerateRequested += OnRegenerate;
            ui.ResetRequested += OnReset;

            if (generator.CombinedMesh == null)
                OnRegenerate();
            else
                UpdateStats();
            UpdateSelectionText();
        }

        private void OnDestroy()
        {
            if (ui == null)
                return;

            ui.RegenerateRequested -= OnRegenerate;
            ui.ResetRequested -= OnReset;
        }

        private void Update()
        {
            UpdateSelectionText();
        }

        private void OnRegenerate()
        {
            if (generator == null)
                return;

            generator.seedCount = ui.Cells;
            generator.patternSeed = ui.TryGetSeed(out int seed)
                ? seed
                : Random.Range(0, int.MaxValue);

            generator.Generate();
            RefreshPicker();
            UpdateStats();
        }

        private void OnReset()
        {
            ui.SetDefaultValues();
        }

        private void UpdateStats()
        {
            if (ui == null || generator == null)
                return;

            int cells = generator.LandCellCount;
            int triangles = generator.TriangleToCellId != null ? generator.TriangleToCellId.Length : 0;
            ui.SetStats(generator.PlayableCellCount + " playable / " + cells
                + " land cells  •  " + triangles + " triangles");
        }

        private void UpdateSelectionText()
        {
            if (ui == null)
                return;

            ui.SetSelection(picker == null || picker.selectedCellId < 0
                ? "Left-click a cell to select it"
                : "Selected cell: " + picker.selectedCellId);
        }

        private void RefreshPicker()
        {
            if (picker == null)
                return;

            picker.ClearSelection();
        }
    }
}
