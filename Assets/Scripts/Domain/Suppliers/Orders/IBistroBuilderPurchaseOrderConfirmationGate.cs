/// <summary>
/// Contrato opcional que 2.3E consulta justo antes de confirmar un pedido.
/// Permite a otros dominios bloquear la confirmación sin trasladar sus reglas
/// económicas al sistema de Proveedores.
/// </summary>
public interface IBistroBuilderPurchaseOrderConfirmationGate
{
    bool TryAuthorizeConfirmation(
        BistroBuilderPurchaseOrderConfirmationPreview preview,
        out string error
    );
}
