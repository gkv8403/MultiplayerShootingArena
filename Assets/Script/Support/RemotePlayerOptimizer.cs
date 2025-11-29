using UnityEngine;
using Fusion;

namespace Scripts.Gameplay
{
    /// <summary>
    /// Optimization: Disables expensive Update-heavy components on remote players
    /// Only local player needs full input processing
    /// </summary>
    public class RemotePlayerOptimizer : NetworkBehaviour
    {
        private MonoBehaviour[] updateHeavyScripts;
        private bool isOptimized = false;

        public override void Spawned()
        {
            // Wait a frame for all components to initialize
            Invoke(nameof(OptimizeRemotePlayer), 0.1f);
        }

        private void OptimizeRemotePlayer()
        {
            // Only optimize remote players (not local player)
            if (Object.HasInputAuthority)
            {
                Debug.Log("[Optimizer] This is local player - skipping optimization");
                return;
            }

            Debug.Log("[Optimizer] Optimizing remote player...");

            // Disable expensive scripts on remote players
            // These are just examples - add your expensive scripts here

            // 1. Disable custom Update-heavy scripts
            var customScripts = GetComponents<MonoBehaviour>();
            foreach (var script in customScripts)
            {
                // Skip NetworkBehaviour scripts (they need to stay enabled)
                if (script is NetworkBehaviour) continue;

                // Skip essential scripts
                if (script is RemotePlayerOptimizer) continue;

                // You can add more scripts to exclude here
                // For example, UI components, visual effects, etc.

                string scriptName = script.GetType().Name;

                // Example: Disable input managers on remote players
                if (scriptName.Contains("Input") || scriptName.Contains("Controller"))
                {
                    script.enabled = false;
                    Debug.Log($"[Optimizer] Disabled {scriptName} on remote player");
                }
            }

            // 2. Disable or reduce camera updates for remote players
            var cameras = GetComponentsInChildren<Camera>();
            foreach (var cam in cameras)
            {
                cam.enabled = false;
                Debug.Log("[Optimizer] Disabled camera on remote player");
            }

            // 3. Reduce animator update rate (if using animations)
            var animators = GetComponentsInChildren<Animator>();
            foreach (var anim in animators)
            {
                // Reduce update frequency
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                Debug.Log("[Optimizer] Optimized animator on remote player");
            }

            // 4. Disable audio listeners (only local player needs one)
            var audioListeners = GetComponentsInChildren<AudioListener>();
            foreach (var listener in audioListeners)
            {
                listener.enabled = false;
                Debug.Log("[Optimizer] Disabled audio listener on remote player");
            }

            // 5. Reduce LOD update frequency
            var lodGroups = GetComponentsInChildren<LODGroup>();
            foreach (var lod in lodGroups)
            {
                // Force to a specific LOD or disable
                lod.enabled = false;
            }

            isOptimized = true;
            Debug.Log("[Optimizer] ✓ Remote player optimization complete");
        }

        // Optional: Re-enable if this player becomes local (e.g., host migration)
        public void EnableFullFunctionality()
        {
            if (!isOptimized) return;

            Debug.Log("[Optimizer] Re-enabling full functionality...");

            var customScripts = GetComponents<MonoBehaviour>();
            foreach (var script in customScripts)
            {
                if (script != null)
                    script.enabled = true;
            }

            var cameras = GetComponentsInChildren<Camera>();
            foreach (var cam in cameras)
            {
                if (cam != null)
                    cam.enabled = true;
            }

            isOptimized = false;
        }
    }
}