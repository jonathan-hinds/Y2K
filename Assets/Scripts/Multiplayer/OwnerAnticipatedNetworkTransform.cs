using Unity.Netcode.Components;

namespace Race.Multiplayer
{
    public sealed class OwnerAnticipatedNetworkTransform : AnticipatedNetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
