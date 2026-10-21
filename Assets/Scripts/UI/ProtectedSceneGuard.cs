using Overlink.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlink.UI
{
    /// <summary>
    /// Keeps a scene unreachable without a signed-in player and a complete profile.
    /// <para>
    /// Attach it to any root object in a protected scene. It reacts to
    /// <see cref="AuthFlowController"/> rather than deciding anything on its own: the flow controller
    /// already knows whether the session is valid and whether the profile has loaded, so asking the
    /// guard to judge that itself on <c>Awake</c> would race the profile fetch and bounce legitimately
    /// signed-in players straight back to the login screen.
    /// </para>
    /// <para>
    /// The case it actually covers is opening a protected scene directly from the Editor, bypassing
    /// the Auth scene the build settings put first. In normal play the flow controller has already
    /// routed the player and this component does nothing.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProtectedSceneGuard : MonoBehaviour
    {
        [Tooltip("Turn off to exempt this scene from the login requirement — useful for the Net demo " +
                 "scenes while they are still being iterated on.")]
        [SerializeField] bool requireLogin = true;

        AuthFlowController flow;

        void Awake()
        {
            if (!requireLogin) return;

            flow = AuthFlowController.EnsureInstance();
            flow.StateChanged += OnStateChanged;
            Evaluate();
        }

        void OnDestroy()
        {
            if (flow != null) flow.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(AuthState _) => Evaluate();

        void Evaluate()
        {
            // A guard in an additively loaded scene must not pull the active scene away from whatever
            // else is on screen; only the scene the player is actually looking at can be redirected.
            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (SceneManager.GetActiveScene().name != scene.name) return;
            if (scene.name == SceneIndex.Auth) return;

            switch (flow.CurrentState)
            {
                case AuthState.SignedOut:
                case AuthState.Error:
                case AuthState.NeedsProfileSetup:
                    Debug.Log($"[ProtectedSceneGuard] '{scene.name}' requires sign-in; returning to Auth ({flow.CurrentState}).");
                    SceneManager.LoadScene(SceneIndex.Auth);
                    break;
            }
        }
    }
}
