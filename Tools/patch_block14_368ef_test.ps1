$p = 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Editor\BistroBuilder\Inventory\BistroBuilderActiveServicePersistenceFunctionalTestWindow.cs'
$t = [IO.File]::ReadAllText($p).Replace("`r`n", "`n")
$old = @'
    private void ConfigureRealServiceDiagnostic()
    {
        SerializedObject spawnerSerialized = new SerializedObject(spawner);
'@
$new = @'
    private void ConfigureRealServiceDiagnostic()
    {
        // La prueba 368EF valida persistencia, no decisiones autónomas de sala 14.
        // Se aíslan temporalmente esas políticas para conservar un fixture determinista.
        BistroBuilderAdvancedFrontOfHouseService frontOfHouse =
            FindFirstObjectByType<BistroBuilderAdvancedFrontOfHouseService>();
        if (frontOfHouse != null)
        {
            SerializedObject frontSerialized = new SerializedObject(frontOfHouse);
            RequireProperty(frontSerialized, "enableAutomaticBarOffers").boolValue = false;
            RequireProperty(frontSerialized, "enableReservationProtection").boolValue = false;
            RequireProperty(frontSerialized, "enableAbandonment").boolValue = false;
            frontSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedObject spawnerSerialized = new SerializedObject(spawner);
'@
if (-not $t.Contains($old)) { throw 'diagnostic marker missing' }
$t = $t.Replace($old, $new)
[IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($false)))
Write-Output '368EF_BLOCK14_ISOLATION_PATCHED'