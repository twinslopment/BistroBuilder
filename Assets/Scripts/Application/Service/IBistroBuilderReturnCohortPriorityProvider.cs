/// <summary>
/// Contrato opcional para priorizar qué cohorte conocida debe volver.
/// No decide cuántos retornos existen ni crea clientes.
/// </summary>
public interface IBistroBuilderReturnCohortPriorityProvider
{
    int GetReturnPriority(string cohortId);
}
