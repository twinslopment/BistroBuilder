/// <summary>
/// Contrato inverso que permite a StaffService proteger mutaciones persistentes
/// cuando 4D mantiene un EmployeeId ligado a un agente operativo. No expone
/// Waiter ni Presentation al dominio de plantilla.
/// </summary>
public interface IBistroBuilderStaffRuntimeMutationGuard
{
    bool CanDismissEmployee(string employeeId, out string error);

    bool CanChangeAvailability(
        string employeeId,
        BistroBuilderEmployeeAvailability requestedAvailability,
        out string error);
}
