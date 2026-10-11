using System;
using UnityEngine;

// Durable editor recovery data, independent of UI root names and removable controllers.
public sealed class RunUIBindings : MonoBehaviour
{
    [Serializable]
    public sealed class Binding
    {
        public string role;
        public string field;
        public UnityEngine.Object[] references;
    }
    public Binding[] bindings = Array.Empty<Binding>();
    public GameObject audioRoot;
}
