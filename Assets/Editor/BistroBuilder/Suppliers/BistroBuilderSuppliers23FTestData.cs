#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class BistroBuilderSuppliers23FTestData
{
    public static void Build(out List<BistroBuilderSmartPurchaseIngredientFact> facts, out List<BistroBuilderSmartPurchaseOfferFact> offers)
    {
        facts=new List<BistroBuilderSmartPurchaseIngredientFact>(); offers=new List<BistroBuilderSmartPurchaseOfferFact>();
        facts.Add(F("ingredient_tomate","Tomate",2000000,3000000,1500000,500000,0,0.9f));
        facts.Add(F("ingredient_merluza","Merluza",0,2000000,1000000,0,0,1f));
        facts.Add(F("ingredient_aceite_oliva","Aceite de oliva",9000000,2500000,3000000,1000000,2000000,0.8f));
        offers.Add(O("offer_tomate_cheap","supplier_norte","Distribuciones Norte","ingredient_tomate",5000000,1050,48f,.94f,12000,1000,false,0));
        offers.Add(O("offer_tomate_fast","supplier_express","Hostelería Express","ingredient_tomate",3000000,850,6f,.995f,2000,1200,true,800));
        offers.Add(O("offer_merluza_specialist","supplier_costa","Costa Fresca","ingredient_merluza",3000000,3625,24f,.97f,6500,900,false,0));
        offers.Add(O("offer_merluza_fast","supplier_express","Hostelería Express","ingredient_merluza",3000000,4300,6f,.995f,2000,1200,false,0));
        offers.Add(O("offer_aceite_bulk","supplier_norte","Distribuciones Norte","ingredient_aceite_oliva",5000000,3080,48f,.94f,12000,1000,false,0));
        offers.Add(O("offer_aceite_balanced","supplier_central","Mercado Central","ingredient_aceite_oliva",1000000,725,24f,.97f,3000,800,false,0));
    }
    private static BistroBuilderSmartPurchaseIngredientFact F(string id,string name,long avail,long daily,long min,long expiring,long incoming,float imp)
    { return new BistroBuilderSmartPurchaseIngredientFact{ingredientId=id,displayName=name,canonicalUnit="g",stockMicrounits=avail,reservedMicrounits=0,availableMicrounits=avail,minimumStockMicrounits=min,forecastDailyConsumptionMicrounits=daily,expiringSoonMicrounits=expiring,earliestExpiryGameDay=expiring>0?3:0,recipeImportance01=imp,incomingMicrounits=incoming,earliestIncomingGameDay=incoming>0?2:0,inventoryResolved=true,forecastResolved=true,policyResolved=true,expiryResolved=true}; }
    private static BistroBuilderSmartPurchaseOfferFact O(string id,string sid,string sn,string ing,long qty,long price,float lead,float rel,long min,long ship,bool promo,int discount)
    { return new BistroBuilderSmartPurchaseOfferFact{supplierOfferId=id,supplierId=sid,supplierDisplayName=sn,ingredientId=ing,packageFormatId="package_"+id,packageDisplayName="Formato",packageNetQuantityMicrounits=qty,minimumPackageCount=1,orderIncrement=1,effectiveUnitPriceCents=price,marketUnitPriceCents=promo?(long)Math.Ceiling(price/(1-discount/10000.0)):price,hasPromotion=promo,discountBasisPoints=discount,availability=BistroBuilderSupplierOfferAvailability.Disponible,availableForNewOrders=true,leadTimeGameHours=lead,reliability01=rel,supplierMinimumOrderCents=min,shippingCostCents=ship,freeShippingEnabled=true,freeShippingThresholdCents=min*2}; }
}
#endif
