/// <summary>
/// Optional lifecycle policy. Eligibility is derived from the existing object registry;
/// BBSIS remains the only authority that registers subjects and grants spatial leases.
/// </summary>
public interface IBistroBuilderSpatialLifecycleOwner
{
    bool IsSpatialLifecycleActive { get; }
}
