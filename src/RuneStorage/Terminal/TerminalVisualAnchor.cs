using UnityEngine;

namespace RuneStorage
{
    internal sealed class TerminalVisualAnchor : MonoBehaviour
    {
        private const int AlignmentPasses = 3;

        [SerializeField]
        private Transform _visual;

        private int _passesRemaining;
        private static int _loggedAlignments;

        internal void Initialize(Transform visual)
        {
            _visual = visual;
            _passesRemaining = AlignmentPasses;
        }

        private void OnEnable()
        {
            _passesRemaining = AlignmentPasses;
        }

        private void LateUpdate()
        {
            if (_passesRemaining <= 0) return;

            if (_visual == null)
            {
                _visual = transform.Find("RuneStorage_Terminal_Visual");
                if (_visual == null)
                {
                    _passesRemaining = 0;
                    return;
                }
            }

            Renderer[] renderers = _visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                _passesRemaining = 0;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 correction = transform.position - bottomCenter;
            _visual.position += correction;
            _passesRemaining--;

            if (_loggedAlignments < 3)
            {
                _loggedAlignments++;
                Plugin.Log.LogInfo(
                    $"Runtime-centered RuneStorage terminal pass {_loggedAlignments}: " +
                    $"correction={correction}, finalLocalPosition={_visual.localPosition}.");
            }
        }
    }
}
