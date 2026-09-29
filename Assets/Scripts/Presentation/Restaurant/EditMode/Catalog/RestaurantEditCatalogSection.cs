using System;

public enum RestaurantEditCatalogSection { Build, Surfaces, Walls, Decoration, Lighting, Services, Other }

/// <summary>Presentation taxonomy shared by the catalogue and the edit toolbar.</summary>
public static class RestaurantEditCatalogSections
{
    public static string Title(RestaurantEditCatalogSection section)
    {
        switch (section)
        {
            case RestaurantEditCatalogSection.Surfaces: return "Catálogo de superficies";
            case RestaurantEditCatalogSection.Walls: return "Herramientas de paredes";
            case RestaurantEditCatalogSection.Decoration: return "Catálogo de decoración";
            case RestaurantEditCatalogSection.Lighting: return "Catálogo de iluminación";
            case RestaurantEditCatalogSection.Services: return "Equipamiento de servicio";
            case RestaurantEditCatalogSection.Other: return "Otros elementos";
            default: return "Catálogo de artículos";
        }
    }
    public static string SearchHint(RestaurantEditCatalogSection section)
    {
        switch (section)
        {
            case RestaurantEditCatalogSection.Surfaces: return "Buscar superficies, materiales...";
            case RestaurantEditCatalogSection.Walls: return "Buscar paredes, puertas, ventanas...";
            case RestaurantEditCatalogSection.Decoration: return "Buscar plantas, cuadros, decoración...";
            case RestaurantEditCatalogSection.Lighting: return "Buscar lámparas, apliques, iluminación...";
            case RestaurantEditCatalogSection.Services: return "Buscar caja, recepción, equipamiento...";
            case RestaurantEditCatalogSection.Other: return "Buscar señalética, organización, auxiliares...";
            default: return "Buscar muebles, decoración...";
        }
    }
    public static string[] Tabs(RestaurantEditCatalogSection section)
    {
        switch (section)
        {
            case RestaurantEditCatalogSection.Surfaces: return new[] {"Todas", "Suelos", "Paredes", "Techos", "Zócalos", "Exterior"};
            case RestaurantEditCatalogSection.Walls: return new[] {"Crear", "Editar"};
            case RestaurantEditCatalogSection.Decoration: return new[] {"Todas", "Plantas", "Cuadros", "Separadores", "Textiles", "Accesorios"};
            case RestaurantEditCatalogSection.Lighting: return new[] {"Todas", "Techo", "Pared", "Pie", "Exterior", "Ambiental"};
            case RestaurantEditCatalogSection.Services: return new[] {"Todas", "Caja", "Sala", "Recepción", "Apoyo", "Seguridad"};
            case RestaurantEditCatalogSection.Other: return new[] {"Todas", "Señalética", "Organización", "Expositor", "Técnico", "Auxiliar"};
            default: return new[] {"Todas", "Mesas", "Sillas", "Cocina", "Decoración", "Iluminación"};
        }
    }
    public static RestaurantPlaceableItemCategory? Category(RestaurantEditCatalogSection section)
    {
        switch (section)
        {
            case RestaurantEditCatalogSection.Decoration: return RestaurantPlaceableItemCategory.Decoration;
            case RestaurantEditCatalogSection.Lighting: return RestaurantPlaceableItemCategory.Lighting;
            case RestaurantEditCatalogSection.Services: return RestaurantPlaceableItemCategory.ServiceEquipment;
            case RestaurantEditCatalogSection.Other: return RestaurantPlaceableItemCategory.Other;
            default: return null;
        }
    }
    public static string SubcategoryKey(RestaurantEditCatalogSection section, int index)
    {
        string[] keys;
        switch (section)
        {
            case RestaurantEditCatalogSection.Decoration: keys=new[]{"", "plants", "pictures", "dividers", "textiles", "accessories"}; break;
            case RestaurantEditCatalogSection.Lighting: keys=new[]{"", "ceiling", "wall", "floor", "exterior", "ambient"}; break;
            case RestaurantEditCatalogSection.Services: keys=new[]{"", "checkout", "dining", "reception", "support", "safety"}; break;
            case RestaurantEditCatalogSection.Other: keys=new[]{"", "signage", "organization", "display", "technical", "auxiliary"}; break;
            default: return "";
        }
        return index>=0&&index<keys.Length?keys[index]:"";
    }
    public static bool IsArchitecture(RestaurantEditCatalogSection section) => section==RestaurantEditCatalogSection.Surfaces||section==RestaurantEditCatalogSection.Walls;
    public static bool Matches(RestaurantEditCatalogSection section, int tab, RestaurantPlaceableItemDefinition item)
    {
        if(item==null||IsArchitecture(section))return false;
        var category=Category(section);
        if(category.HasValue&&item.Category!=category.Value)return false;
        if(section==RestaurantEditCatalogSection.Build)return tab<0||(int)item.Category==tab;
        return tab<0||string.Equals(item.CatalogSubcategory,SubcategoryKey(section,tab-1000),StringComparison.OrdinalIgnoreCase);
    }
}
