using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A history command may hold an inactive GameObject for Undo/Redo. When an
/// obsolete command is released, an instance shared with a still-live
/// command must not be destroyed. The newer command assumes final ownership.
/// </summary>
public interface IRestaurantEditHistoryResourceReleaseGuard
{
    void ProtectReleaseIfReferenced(ISet<Object> liveTargets);
}
