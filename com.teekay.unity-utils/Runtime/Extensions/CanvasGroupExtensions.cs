using UnityEngine;

namespace TeekayUtils
{
    public static class CanvasGroupExtensions
    {
        /// <summary>Sets alpha, interactable and blocksRaycasts in one call.</summary>
        public static void SetVisible(this CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        /// <summary>Makes the group fully visible and interactive.</summary>
        public static void Show(this CanvasGroup group) => group.SetVisible(true);

        /// <summary>Hides the group and disables interaction and raycasts.</summary>
        public static void Hide(this CanvasGroup group) => group.SetVisible(false);
    }
}
