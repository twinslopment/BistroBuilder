using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Adaptador SOLO LECTURA entre 2.3F y las autoridades ya existentes de Inventario/2.2C.
/// Evita crear una segunda autoridad y no requiere acoplar 2.3F a nombres internos de 2.2.
/// </summary>
public static class BistroBuilderSupplierSmartPurchaseRuntimeResolver
{
    private static readonly string[] IngredientNames = { "ingredientId", "IngredientId", "ingredientID", "IngredientID" };
    private static readonly string[] StockMicroNames = { "stockMicrounits", "totalStockMicrounits", "onHandMicrounits", "quantityMicrounits", "physicalStockMicrounits" };
    private static readonly string[] ReservedMicroNames = { "reservedMicrounits", "reservedQuantityMicrounits", "activeReservedMicrounits" };
    private static readonly string[] AvailableMicroNames = { "availableMicrounits", "availableQuantityMicrounits", "freeMicrounits" };
    private static readonly string[] MinimumMicroNames = { "minimumStockMicrounits", "minimumMicrounits", "stockMinimumMicrounits", "minStockMicrounits" };
    private static readonly string[] ForecastMicroNames = { "forecastDailyConsumptionMicrounits", "dailyConsumptionMicrounits", "predictedDailyConsumptionMicrounits", "averageDailyConsumptionMicrounits", "consumptionPerDayMicrounits" };
    private static readonly string[] ExpiringMicroNames = { "expiringSoonMicrounits", "nearExpiryMicrounits", "nextExpiryQuantityMicrounits" };
    private static readonly string[] ExpiryDayNames = { "earliestExpiryGameDay", "nextExpiryGameDay", "expirationGameDay", "expiryGameDay" };

    public static bool TryCaptureFacts(
        BistroBuilderIngredientAuthoringDatabase ingredientDatabase,
        BistroBuilderSupplierPurchaseOrderService orderService,
        int currentGameDay,
        out List<BistroBuilderSmartPurchaseIngredientFact> facts,
        out List<string> diagnostics)
    {
        facts = new List<BistroBuilderSmartPurchaseIngredientFact>();
        diagnostics = new List<string>();
        if (ingredientDatabase == null)
        {
            diagnostics.Add("ingredient.authoring no está disponible.");
            return false;
        }

        Dictionary<string, BistroBuilderSmartPurchaseIngredientFact> byId = new Dictionary<string, BistroBuilderSmartPurchaseIngredientFact>(StringComparer.Ordinal);
        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> ingredients = ingredientDatabase.Ingredients;
        for (int i = 0; i < ingredients.Count; i++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients[i];
            if (ingredient == null || !ingredient.isActive || string.IsNullOrWhiteSpace(ingredient.IngredientId)) continue;
            BistroBuilderSmartPurchaseIngredientFact fact = new BistroBuilderSmartPurchaseIngredientFact
            {
                ingredientId = ingredient.IngredientId,
                displayName = string.IsNullOrWhiteSpace(ingredient.displayNameSnapshot)
                    ? ingredient.IngredientId
                    : ingredient.displayNameSnapshot,
                canonicalUnit = ingredient.canonicalUnitSnapshot ?? string.Empty,
                recipeImportance01 = 0.5f
            };
            byId.Add(fact.ingredientId, fact);
        }

        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int rootsInspected = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour component = behaviours[i];
            if (component == null) continue;
            string name = component.GetType().Name;
            if (!LooksRelevant(name)) continue;
            rootsInspected++;
            object snapshot = TryGetSnapshot(component);
            object root = snapshot ?? (object)component;
            ApplyObjectGraph(root, byId, currentGameDay, diagnostics, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        // También inspecciona ScriptableObjects ya cargados para políticas 2.2C sin asumir nombres de clases.
        ScriptableObject[] assets = Resources.FindObjectsOfTypeAll<ScriptableObject>();
        for (int i = 0; i < assets.Length; i++)
        {
            ScriptableObject asset = assets[i];
            if (asset == null || !LooksRelevant(asset.GetType().Name)) continue;
            ApplyObjectGraph(asset, byId, currentGameDay, diagnostics, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        ApplyIncomingOrders(orderService, currentGameDay, byId);
        foreach (KeyValuePair<string, BistroBuilderSmartPurchaseIngredientFact> pair in byId)
        {
            BistroBuilderSmartPurchaseIngredientFact f = pair.Value;
            if (f.inventoryResolved && f.availableMicrounits <= 0L && f.stockMicrounits > 0L)
                f.availableMicrounits = Math.Max(0L, f.stockMicrounits - f.reservedMicrounits);
            facts.Add(f);
        }
        facts.Sort((a,b) => string.CompareOrdinal(a.ingredientId,b.ingredientId));
        diagnostics.Add("Raíces runtime relacionadas con Inventario/2.2C inspeccionadas: " + rootsInspected + ".");
        diagnostics.Add("Ingredientes con stock resuelto: " + facts.FindAll(x => x.inventoryResolved).Count + "/" + facts.Count + ".");
        diagnostics.Add("Ingredientes con previsión resuelta: " + facts.FindAll(x => x.forecastResolved).Count + "/" + facts.Count + ".");
        diagnostics.Add("Ingredientes con mínimo resuelto: " + facts.FindAll(x => x.policyResolved).Count + "/" + facts.Count + ".");
        diagnostics.Add("Traversal seguro 2.3F5: getters de UnityEngine.Object y handles nativos excluidos.");
        return facts.Count > 0 && facts.Exists(x => x.inventoryResolved);
    }

    public static string CaptureReadOnlyFingerprint()
    {
        try
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<string> chunks = new List<string>();
            for (int i=0;i<behaviours.Length;i++)
            {
                MonoBehaviour c=behaviours[i]; if(c==null || !LooksInventoryOnly(c.GetType().Name)) continue;
                object snap=TryGetSnapshot(c); if(snap==null) continue;
                chunks.Add(c.GetType().FullName+":"+JsonUtility.ToJson(snap));
            }
            chunks.Sort(StringComparer.Ordinal);
            string joined=string.Join("|",chunks);
            return StableHash(joined);
        }
        catch { return "fingerprint_unavailable"; }
    }

    private static void ApplyIncomingOrders(BistroBuilderSupplierPurchaseOrderService orderService, int currentDay, Dictionary<string,BistroBuilderSmartPurchaseIngredientFact> byId)
    {
        if (orderService == null || !orderService.IsInitialized) return;
        List<BistroBuilderPurchaseOrderRecord> orders = new List<BistroBuilderPurchaseOrderRecord>();
        orderService.CopyOrders(orders);
        for (int i=0;i<orders.Count;i++)
        {
            BistroBuilderPurchaseOrderRecord order=orders[i];
            if(order==null) continue;
            if(order.status!=BistroBuilderPurchaseOrderStatus.Confirmed && order.status!=BistroBuilderPurchaseOrderStatus.PendingDelivery && order.status!=BistroBuilderPurchaseOrderStatus.InDelivery) continue;
            int arrival = order.plannedDeliveryGameDay > 0 ? order.plannedDeliveryGameDay : currentDay + Math.Max(1,(int)Math.Ceiling(Math.Max(0f,order.quotedLeadTimeGameHours)/24f));
            if(order.confirmedLines==null) continue;
            for(int l=0;l<order.confirmedLines.Count;l++)
            {
                BistroBuilderPurchaseOrderConfirmedLineSnapshot line=order.confirmedLines[l];
                BistroBuilderSmartPurchaseIngredientFact fact;
                if(line==null || !byId.TryGetValue(line.ingredientId,out fact)) continue;
                fact.incomingMicrounits = SafeAdd(fact.incomingMicrounits, Math.Max(0L,line.totalNetQuantityMicrounits));
                if(fact.earliestIncomingGameDay<=0 || arrival<fact.earliestIncomingGameDay) fact.earliestIncomingGameDay=arrival;
            }
        }
    }

    private static bool LooksRelevant(string name)
    {
        if(string.IsNullOrEmpty(name)) return false;
        string n=name.ToLowerInvariant();
        return n.Contains("inventory") || n.Contains("stock") || n.Contains("forecast") || n.Contains("prevision") || n.Contains("policy") || n.Contains("consumption");
    }
    private static bool LooksInventoryOnly(string name)
    {
        string n=(name??string.Empty).ToLowerInvariant(); return n.Contains("inventory") && !n.Contains("editor");
    }

    private static object TryGetSnapshot(object service)
    {
        if(service==null) return null;
        Type t=service.GetType();
        string[] methods={"CreateSnapshot","CaptureSnapshot","GetSnapshot","CreateStateSnapshot"};
        for(int i=0;i<methods.Length;i++)
        {
            MethodInfo m=t.GetMethod(methods[i],BindingFlags.Instance|BindingFlags.Public,null,Type.EmptyTypes,null);
            if(m==null || m.ReturnType==typeof(void)) continue;
            try { return m.Invoke(service,null); } catch { }
        }
        return null;
    }

    private static void ApplyObjectGraph(object obj, Dictionary<string,BistroBuilderSmartPurchaseIngredientFact> byId, int currentDay, List<string> diagnostics, int depth, HashSet<object> visited)
    {
        if(obj==null || depth>5) return;
        Type type=obj.GetType();
        if(IsScalar(type) || IsUnsafeTraversalType(type)) return;

        bool isUnityObject = obj is UnityEngine.Object;
        if(depth>0 && isUnityObject) return;

        if(!type.IsValueType)
        {
            if(visited.Contains(obj)) return;
            visited.Add(obj);
        }

        string ingredientId = ReadString(obj, IngredientNames);
        BistroBuilderSmartPurchaseIngredientFact fact;
        if(!string.IsNullOrWhiteSpace(ingredientId) && byId.TryGetValue(ingredientId.Trim(),out fact))
            ApplyRecord(obj,fact,currentDay);

        IEnumerable enumerable=obj as IEnumerable;
        if(enumerable!=null && !(obj is string) && IsSafeEnumerable(type))
        {
            int count=0;
            try
            {
                foreach(object item in enumerable)
                {
                    if(count++>=512) break;
                    ApplyObjectGraph(item,byId,currentDay,diagnostics,depth+1,visited);
                }
            }
            catch
            {
                // Un enumerable ajeno puede depender de estado nativo; la lectura inteligente
                // nunca debe romper Play Mode por inspeccionar una colección no canónica.
            }
            return;
        }

        // SEGURIDAD 2.3F5: en UnityEngine.Object solo atravesamos CAMPOS administrados.
        // Nunca evaluamos getters arbitrarios heredados de Unity (transform, matrices, handles,
        // childCount, etc.). PropertyInfo.GetValue sobre esos getters puede disparar ValidTRS()
        // o TransformHandle aunque la escena sea válida. Es el mismo principio usado por la
        // validación segura de Inventario 2.2D.
        FieldInfo[] fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        int traversed=0;
        for(int i=0;i<fields.Length && traversed<96;i++)
        {
            FieldInfo field=fields[i];
            if(field.IsStatic) continue;
            object value;
            try { value=field.GetValue(obj); } catch { continue; }
            if(!CanTraverseChild(value)) continue;
            traversed++;
            ApplyObjectGraph(value,byId,currentDay,diagnostics,depth+1,visited);
        }

        if(isUnityObject) return;

        // Para DTO/POCO administrados sí se permiten propiedades públicas sin índice.
        // Evitamos propiedades no públicas y cualquier valor Unity/native.
        PropertyInfo[] properties=type.GetProperties(BindingFlags.Instance|BindingFlags.Public);
        for(int i=0;i<properties.Length && traversed<96;i++)
        {
            PropertyInfo property=properties[i];
            MethodInfo getter=property.GetGetMethod(false);
            if(getter==null || getter.IsStatic || property.GetIndexParameters().Length!=0) continue;
            object value;
            try { value=property.GetValue(obj,null); } catch { continue; }
            if(!CanTraverseChild(value)) continue;
            traversed++;
            ApplyObjectGraph(value,byId,currentDay,diagnostics,depth+1,visited);
        }
    }

    private static bool CanTraverseChild(object value)
    {
        if(value==null || value is string) return false;
        Type type=value.GetType();
        if(IsScalar(type) || IsUnsafeTraversalType(type)) return false;
        if(value is UnityEngine.Object) return false;
        return true;
    }

    private static bool IsSafeEnumerable(Type type)
    {
        if(type==null || IsUnsafeTraversalType(type)) return false;
        string ns=type.Namespace??string.Empty;
        if(ns.StartsWith("UnityEngine",StringComparison.Ordinal) ||
           ns.StartsWith("UnityEditor",StringComparison.Ordinal) ||
           ns.StartsWith("Unity.Collections",StringComparison.Ordinal) ||
           ns.StartsWith("Unity.Jobs",StringComparison.Ordinal)) return false;
        return true;
    }

    private static bool IsUnsafeTraversalType(Type type)
    {
        if(type==null) return true;
        if(typeof(Delegate).IsAssignableFrom(type) || typeof(MemberInfo).IsAssignableFrom(type) || type==typeof(Type)) return true;
        if(type.IsPointer || type.IsByRef) return true;
        if(typeof(UnityEngine.Object).IsAssignableFrom(type)) return false; // raíz permitida; referencias hijas se filtran aparte.
        string ns=type.Namespace??string.Empty;
        return ns.StartsWith("UnityEngine",StringComparison.Ordinal) ||
               ns.StartsWith("UnityEditor",StringComparison.Ordinal) ||
               ns.StartsWith("Unity.Collections",StringComparison.Ordinal) ||
               ns.StartsWith("Unity.Jobs",StringComparison.Ordinal);
    }

    private static void ApplyRecord(object record, BistroBuilderSmartPurchaseIngredientFact fact, int currentDay)
    {
        long value;
        if(TryReadLong(record,AvailableMicroNames,out value)) { fact.availableMicrounits=Math.Max(0L,value); fact.inventoryResolved=true; }
        if(TryReadLong(record,StockMicroNames,out value)) { fact.stockMicrounits=Math.Max(0L,value); fact.inventoryResolved=true; }
        if(TryReadLong(record,ReservedMicroNames,out value)) { fact.reservedMicrounits=Math.Max(0L,value); fact.inventoryResolved=true; }
        if(TryReadLong(record,MinimumMicroNames,out value)) { fact.minimumStockMicrounits=Math.Max(fact.minimumStockMicrounits,Math.Max(0L,value)); fact.policyResolved=true; }
        if(TryReadLong(record,ForecastMicroNames,out value)) { fact.forecastDailyConsumptionMicrounits=Math.Max(fact.forecastDailyConsumptionMicrounits,Math.Max(0L,value)); fact.forecastResolved=true; }
        if(TryReadLong(record,ExpiringMicroNames,out value)) { fact.expiringSoonMicrounits=Math.Max(fact.expiringSoonMicrounits,Math.Max(0L,value)); fact.expiryResolved=true; }
        if(TryReadInt(record,ExpiryDayNames,out int day)) { if(day>0 && (fact.earliestExpiryGameDay<=0 || day<fact.earliestExpiryGameDay)) fact.earliestExpiryGameDay=day; fact.expiryResolved=true; }

        // Fallbacks en unidades base si la autoridad no expone microunits.
        if(!fact.inventoryResolved)
        {
            if(TryReadBaseUnits(record,new[]{"available","availableQuantity","stockAvailable"},out double d)) { fact.availableMicrounits=ToMicrounits(d); fact.inventoryResolved=true; }
            if(TryReadBaseUnits(record,new[]{"stock","quantity","onHand","totalStock"},out d)) { fact.stockMicrounits=ToMicrounits(d); fact.inventoryResolved=true; }
            if(TryReadBaseUnits(record,new[]{"reserved","reservedQuantity"},out d)) { fact.reservedMicrounits=ToMicrounits(d); fact.inventoryResolved=true; }
        }
        if(!fact.forecastResolved && TryReadBaseUnits(record,new[]{"forecastDailyConsumption","dailyConsumption","averageDailyConsumption","predictedDailyConsumption"},out double fd))
        { fact.forecastDailyConsumptionMicrounits=ToMicrounits(fd); fact.forecastResolved=true; }
        if(!fact.policyResolved && TryReadBaseUnits(record,new[]{"minimumStock","stockMinimum","minStock"},out double md))
        { fact.minimumStockMicrounits=ToMicrounits(md); fact.policyResolved=true; }
    }

    private static string ReadString(object obj,string[] names)
    {
        object v; return TryReadNamed(obj,names,out v) && v!=null ? v.ToString() : null;
    }
    private static bool TryReadLong(object obj,string[] names,out long value)
    {
        value=0L; object v; if(!TryReadNamed(obj,names,out v)||v==null) return false;
        try { value=Convert.ToInt64(v); return true; } catch { return false; }
    }
    private static bool TryReadInt(object obj,string[] names,out int value)
    {
        value=0; object v; if(!TryReadNamed(obj,names,out v)||v==null) return false;
        try { value=Convert.ToInt32(v); return true; } catch { return false; }
    }
    private static bool TryReadBaseUnits(object obj,string[] names,out double value)
    {
        value=0; object v; if(!TryReadNamed(obj,names,out v)||v==null) return false;
        try { value=Convert.ToDouble(v); return !double.IsNaN(value)&&!double.IsInfinity(value); } catch { return false; }
    }
    private static bool TryReadNamed(object obj,string[] names,out object value)
    {
        value=null; if(obj==null) return false; Type t=obj.GetType();
        bool isUnityObject=obj is UnityEngine.Object;
        for(int i=0;i<names.Length;i++)
        {
            FieldInfo f=t.GetField(names[i],BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.IgnoreCase);
            if(f!=null) { try { value=f.GetValue(obj); return true; } catch{} }

            // Nunca invoques getters arbitrarios de UnityEngine.Object. Para DTO/POCO
            // administrados sí se permite una propiedad pública explícita.
            if(isUnityObject) continue;
            PropertyInfo p=t.GetProperty(names[i],BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase);
            MethodInfo getter=p!=null?p.GetGetMethod(false):null;
            if(p!=null && p.GetIndexParameters().Length==0 && getter!=null && !getter.IsStatic)
            {
                try { value=p.GetValue(obj,null); return true; } catch{}
            }
        }
        return false;
    }
    private static bool IsScalar(Type t) { return t.IsPrimitive || t.IsEnum || t==typeof(string) || t==typeof(decimal) || t==typeof(DateTime) || t==typeof(Guid); }
    private static long ToMicrounits(double units) { if(units<=0.0) return 0L; double v=units*1000000.0; return v>=long.MaxValue?long.MaxValue:(long)Math.Round(v,MidpointRounding.AwayFromZero); }
    private static long SafeAdd(long a,long b) { if(b>0L && a>long.MaxValue-b) return long.MaxValue; return a+b; }
    private static string StableHash(string s) { unchecked { ulong h=1469598103934665603UL; for(int i=0;i<(s??"").Length;i++){ h^=(byte)(s[i]&255); h*=1099511628211UL; h^=(byte)(s[i]>>8); h*=1099511628211UL;} return h.ToString("X16"); } }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance=new ReferenceEqualityComparer();
        public new bool Equals(object x,object y){ return ReferenceEquals(x,y); }
        public int GetHashCode(object obj){ return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj); }
    }
}
