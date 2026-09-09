using System;
using System.Collections.Generic;

/// <summary>Intención individual derivada de una experiencia ya evaluada.</summary>
[Serializable]
public sealed class BistroBuilderAdvancedCustomerIndividualAdvocacy
{
    public string customerId = string.Empty;
    public int recommendationBasisPoints;
    public int returnIntentBasisPoints;

    public BistroBuilderAdvancedCustomerIndividualAdvocacy DeepClone() =>
        (BistroBuilderAdvancedCustomerIndividualAdvocacy)MemberwiseClone();
}

/// <summary>Lectura agregada consultiva de recomendación y fidelización.</summary>
[Serializable]
public sealed class BistroBuilderAdvancedCustomerAdvocacyResult
{
    public string cohortId = string.Empty;
    public int recommendationBasisPoints;
    public int returnIntentBasisPoints;
    public int returnPriority;
    public List<BistroBuilderAdvancedCustomerIndividualAdvocacy> individuals =
        new List<BistroBuilderAdvancedCustomerIndividualAdvocacy>();
}
