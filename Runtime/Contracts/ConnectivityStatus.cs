namespace Dreamy.Connectivity
{
    public enum ConnectivityStatus { Unknown, NoNetwork, NetworkAvailable, EndpointReachable, EndpointUnavailable, TimedOut, Misconfigured }
    public enum ConnectivityRequirement { NetworkHintOnly, EndpointRequired, RequestFirst }
}
