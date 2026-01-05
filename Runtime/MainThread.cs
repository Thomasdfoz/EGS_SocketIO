using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace EGS.SocketIO 
{
    public static class MainThread
    {
        private static readonly ConcurrentQueue<Action> _queue = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            var go = new GameObject("[MainThread]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<MainThreadRunner>();
        }

        public static void Post(Action a) { if (a != null) _queue.Enqueue(a); }

        private sealed class MainThreadRunner : MonoBehaviour
        {
            void Update()
            {
                while (_queue.TryDequeue(out var a))
                    a?.Invoke();
            }
        }
    }
}
