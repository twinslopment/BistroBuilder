$p = 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Scripts\Application\Service\Bar\BistroBuilderBarServiceSystem.cs'
$t = [IO.File]::ReadAllText($p)
$marker = @'
    private void HandleGroupStateChanged(
        CustomerGroup group,
        CustomerGroupState state
    )
'@
if (-not $t.Contains($marker)) { throw 'HandleGroupStateChanged marker missing' }
$method = @'
    /// <summary>
    /// Entrada pública del Bloque 14 para ofrecer barra temporal a un grupo
    /// que originalmente solicitó mesa. No altera su modalidad solicitada.
    /// </summary>
    public bool TryOfferWaitingAtBar(CustomerGroup group, out string error)
    {
        error = string.Empty;
        ResolveDependencies();
        if (group == null || group.HasAssignedTable ||
            group.CurrentState != CustomerGroupState.WaitingForTable ||
            group.RequestedServiceMode == BistroBuilderServiceMode.BarService)
        {
            error = "El grupo no puede usar barra como espera en su estado actual.";
            return false;
        }

        if (sessionsByGroup.ContainsKey(group))
            return group.IsOccupyingBar;
        if (!registeredGroups.Contains(group))
            RegisterCustomerGroup(group);

        if (!barRegistry.TryAllocateSpot(
                group, BistroBuilderServiceMode.WaitingAtBar, out BistroBuilderBarServiceSpot spot))
        {
            error = "No hay capacidad suficiente en barra para el grupo.";
            return false;
        }

        var session = new Session
        {
            Group = group,
            Spot = spot,
            Mode = BistroBuilderServiceMode.WaitingAtBar,
            State = SessionState.Allocated
        };
        sessionsByGroup.Add(group, session);
        waitingAreaSystem?.RefreshWaitingQueue();

        CustomerMovementView movement = group.GetComponent<CustomerMovementView>();
        if (movement == null || !movement.MoveToBarPoint(spot))
        {
            CancelSession(session, "No se pudo iniciar el movimiento manual a barra.");
            error = "No se pudo iniciar el movimiento del grupo hacia barra.";
            return false;
        }

        session.State = SessionState.CustomerWalking;
        Log("Bloque 14 envió manualmente el grupo " + group.GroupId +
            " a barra temporal " + spot.BarSpotId + ".");
        return true;
    }

'@
$t = $t.Replace($marker, $method + $marker)
[IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($false)))
Write-Output 'BAR_PATCHED'