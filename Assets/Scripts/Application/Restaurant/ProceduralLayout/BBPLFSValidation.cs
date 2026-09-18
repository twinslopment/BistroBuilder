using System;
using System.Collections.Generic;
using UnityEngine;

public interface IBBPLFSLayoutValidator
{
    int Priority { get; }

    BBPLFSValidationReport Validate(
        BBPLFSLayoutCandidate candidate,
        RestaurantArea area);
}
