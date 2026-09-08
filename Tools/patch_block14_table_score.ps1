$p = 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Scripts\Application\Service\TableAssignmentSystem.cs'
$t = [IO.File]::ReadAllText($p)
$start = $t.IndexOf('    private RestaurantTable FindBestTableForGroup(')
$end = $t.IndexOf('    private void CacheDependenciesIfNeeded()', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'FindBestTableForGroup markers missing' }
$new = @'
    private RestaurantTable FindBestTableForGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null || registeredTables.Count == 0)
            return null;

        RestaurantTable bestTable = null;
        float bestAdvancedScore = float.MinValue;
        int lowestUnusedCapacity = int.MaxValue;
        float shortestDistanceSquared = float.MaxValue;

        foreach (RestaurantTable table in registeredTables)
        {
            if (table == null || reservedForBarTransitions.Contains(table) ||
                reservedForPreferredAssignments.Contains(table) ||
                !table.CanSeatGroup(customerGroup.GroupSize))
                continue;

            if (advancedFrontOfHouseService != null)
            {
                float score = advancedFrontOfHouseService.EvaluateTableScore(customerGroup, table);
                if (score == float.MinValue) continue;
                if (bestTable == null || score > bestAdvancedScore ||
                    (Mathf.Approximately(score, bestAdvancedScore) && table.TableId < bestTable.TableId))
                {
                    bestTable = table;
                    bestAdvancedScore = score;
                }
                continue;
            }

            int unusedCapacity = table.Capacity - customerGroup.GroupSize;
            Vector3 destinationPosition = table.CustomerApproachPoint != null
                ? table.CustomerApproachPoint.position
                : table.transform.position;
            float distanceSquared = (customerGroup.transform.position - destinationPosition).sqrMagnitude;
            bool better = bestTable == null || unusedCapacity < lowestUnusedCapacity ||
                (unusedCapacity == lowestUnusedCapacity && distanceSquared < shortestDistanceSquared) ||
                (unusedCapacity == lowestUnusedCapacity && Mathf.Approximately(distanceSquared, shortestDistanceSquared) &&
                 table.TableId < bestTable.TableId);
            if (!better) continue;
            bestTable = table;
            lowestUnusedCapacity = unusedCapacity;
            shortestDistanceSquared = distanceSquared;
        }

        return bestTable;
    }

'@
$t = $t.Substring(0, $start) + $new + $t.Substring($end)
[IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($false)))
Write-Output 'TABLE_SCORE_PATCHED'