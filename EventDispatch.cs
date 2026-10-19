using System;

namespace WheresWolfgang.SaveScopedConfig
{
    internal static class EventDispatch
    {
        internal static void Invoke(Action listeners, Action<Exception> report)
        {
            if (listeners == null) return;
            foreach (Action listener in listeners.GetInvocationList())
            {
                try { listener(); }
                catch (Exception error) { report(error); }
            }
        }

        internal static void Invoke<T>(Action<T> listeners, T value, Action<Exception> report)
        {
            if (listeners == null) return;
            foreach (Action<T> listener in listeners.GetInvocationList())
            {
                try { listener(value); }
                catch (Exception error) { report(error); }
            }
        }
    }
}
