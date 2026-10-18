using UnityEngine;

namespace Comfort.Common
{
    public static class MonoBehaviourSingleton<T> where T : UnityEngine.Object
    {
        public static T Instance
        {
            get { return UnityEngine.Object.FindObjectOfType<T>(); }
        }

        public static bool Instantiated
        {
            get { return Instance != null; }
        }
    }
}
