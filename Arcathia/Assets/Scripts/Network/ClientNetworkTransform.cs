using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    // Overrides Netcode permissions so the owning Client can move their own transform
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}