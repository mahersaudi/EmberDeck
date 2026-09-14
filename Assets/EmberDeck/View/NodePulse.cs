using UnityEngine;

namespace EmberDeck.View
{
    /// <summary>
    /// A slow breathing pulse on the map nodes that can be entered now. "Where can I go?" is the first
    /// question the map asks, and motion answers it before any reading does. Every reachable node pulses
    /// in step, so the pulse reads as one signal rather than as several things competing for attention.
    /// </summary>
    public sealed class NodePulse : MonoBehaviour
    {
        void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.06f;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
