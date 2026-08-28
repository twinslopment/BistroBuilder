using System.Collections.Generic;

/// <summary>
/// Contenido semilla 7A: 7 familias x 5 campañas. Las cifras son valores
/// iniciales de balance y quedan encapsuladas como datos editables.
/// </summary>
public static class BistroBuilderMarketing7ASeedFactory
{
    public static List<BistroBuilderMarketingCampaignDefinition> CreateSeed()
    {
        var r = new List<BistroBuilderMarketingCampaignDefinition>(35);

        // 1 — Notoriedad local y marca.
        r.Add(C("marketing.local.flyers", "Reparto de flyers en el barrio", "Promoción física de corto alcance para captar vecinos cercanos.", BistroBuilderMarketingCampaignType.LocalAwareness, BistroBuilderMarketingTargetKind.None, 8000, 3, 1,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 600, 0, BistroBuilderMarketingCustomerSegment.LocalResidents),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 200)));
        r.Add(C("marketing.local.posters", "Cartelería por la zona", "Presencia sostenida en calles cercanas al restaurante.", BistroBuilderMarketingCampaignType.LocalAwareness, BistroBuilderMarketingTargetKind.None, 15000, 7, 1,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 700, 0, BistroBuilderMarketingCustomerSegment.LocalResidents)));
        r.Add(C("marketing.local.press", "Publicidad en prensa local", "Anuncio local orientado a público adulto y tradicional.", BistroBuilderMarketingCampaignType.LocalAwareness, BistroBuilderMarketingTargetKind.None, 35000, 7, 2,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 900, 0, BistroBuilderMarketingCustomerSegment.Traditional),
            M(BistroBuilderMarketingModifierKind.Reputation, 0, 1)));
        r.Add(C("marketing.local.radio", "Campaña de radio local", "Cobertura amplia con segmentación limitada.", BistroBuilderMarketingCampaignType.LocalAwareness, BistroBuilderMarketingTargetKind.None, 60000, 5, 3,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 1200)));
        r.Add(C("marketing.local.city", "Gran campaña de ciudad", "Campaña de notoriedad amplia para un restaurante consolidado.", BistroBuilderMarketingCampaignType.LocalAwareness, BistroBuilderMarketingTargetKind.None, 180000, 7, 5,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 1800),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 700)));

        // 2 — Promociones y descuentos.
        r.Add(C("marketing.promo.menu_day", "Menú del día promocionado", "Refuerza la captación de trabajadores durante el servicio de comida.", BistroBuilderMarketingCampaignType.Promotions, BistroBuilderMarketingTargetKind.None, 12000, 5, 1,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 1600, 0, BistroBuilderMarketingCustomerSegment.Workers, BistroBuilderMarketingDayPart.Lunch),
            M(BistroBuilderMarketingModifierKind.AverageTicket, -700, 0, BistroBuilderMarketingCustomerSegment.Workers, BistroBuilderMarketingDayPart.Lunch)));
        r.Add(C("marketing.promo.happy_hour", "Happy Hour", "Aumenta tráfico joven y social en una franja concreta.", BistroBuilderMarketingCampaignType.Promotions, BistroBuilderMarketingTargetKind.None, 10000, 5, 1,
            M(BistroBuilderMarketingModifierKind.WalkInDemand, 1400, 0, BistroBuilderMarketingCustomerSegment.YoungAdults, BistroBuilderMarketingDayPart.Dinner),
            M(BistroBuilderMarketingModifierKind.AverageTicket, -900, 0, BistroBuilderMarketingCustomerSegment.YoungAdults, BistroBuilderMarketingDayPart.Dinner)));
        r.Add(C("marketing.promo.two_for_one", "2x1 seleccionado", "Promoción agresiva para clientes sensibles al precio.", BistroBuilderMarketingCampaignType.Promotions, BistroBuilderMarketingTargetKind.None, 18000, 3, 2,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 2400, 0, BistroBuilderMarketingCustomerSegment.PriceSensitive),
            M(BistroBuilderMarketingModifierKind.AverageTicket, -1800, 0, BistroBuilderMarketingCustomerSegment.PriceSensitive),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 700)));
        r.Add(C("marketing.promo.weekday", "Descuento entre semana", "Ayuda a rellenar jornadas de baja demanda.", BistroBuilderMarketingCampaignType.Promotions, BistroBuilderMarketingTargetKind.None, 25000, 7, 2,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 1000),
            M(BistroBuilderMarketingModifierKind.AverageTicket, -600)));
        r.Add(C("marketing.promo.celebration_week", "Semana de celebración", "Promoción intensa de varios días con fuerte presión operativa.", BistroBuilderMarketingCampaignType.Promotions, BistroBuilderMarketingTargetKind.None, 70000, 7, 4,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 1800),
            M(BistroBuilderMarketingModifierKind.AverageTicket, -500),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 1000)));

        // 3 — Redes sociales y marketing digital.
        r.Add(C("marketing.digital.sponsored_local", "Publicaciones patrocinadas locales", "Anuncios sociales de proximidad con coste contenido.", BistroBuilderMarketingCampaignType.Digital, BistroBuilderMarketingTargetKind.None, 20000, 5, 1,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 900, 0, BistroBuilderMarketingCustomerSegment.YoungAdults)));
        r.Add(C("marketing.digital.menu_photos", "Campaña fotográfica de la carta", "Contenido visual patrocinado centrado en una carta concreta.", BistroBuilderMarketingCampaignType.Digital, BistroBuilderMarketingTargetKind.Menu, 30000, 7, 2,
            M(BistroBuilderMarketingModifierKind.TargetDemand, 1600, 0, BistroBuilderMarketingCustomerSegment.Foodies),
            M(BistroBuilderMarketingModifierKind.OverallDemand, 500, 0, BistroBuilderMarketingCustomerSegment.Foodies)));
        r.Add(C("marketing.digital.geo", "Anuncio geolocalizado", "Captación inmediata de personas próximas al restaurante.", BistroBuilderMarketingCampaignType.Digital, BistroBuilderMarketingTargetKind.None, 18000, 3, 2,
            M(BistroBuilderMarketingModifierKind.WalkInDemand, 1300)));
        r.Add(C("marketing.digital.online_reservations", "Campaña de reservas online", "Convierte planificación digital en reservas futuras.", BistroBuilderMarketingCampaignType.Digital, BistroBuilderMarketingTargetKind.None, 35000, 7, 3,
            M(BistroBuilderMarketingModifierKind.ReservationDemand, 1700, 0, BistroBuilderMarketingCustomerSegment.Planners)));
        r.Add(C("marketing.digital.viral_brand", "Campaña viral de marca", "Campaña de alcance alto cuyo aprovechamiento dependerá de sistemas posteriores.", BistroBuilderMarketingCampaignType.Digital, BistroBuilderMarketingTargetKind.None, 90000, 5, 5,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 2200, 0, BistroBuilderMarketingCustomerSegment.YoungAdults),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 900)));

        // 4 — Influencers, crítica y prensa gastronómica.
        r.Add(C("marketing.influencer.micro_local", "Invitar a microinfluencer local", "Colaboración pequeña con alcance local creíble.", BistroBuilderMarketingCampaignType.InfluencersPress, BistroBuilderMarketingTargetKind.None, 25000, 4, 2,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 800, 0, BistroBuilderMarketingCustomerSegment.YoungAdults)));
        r.Add(C("marketing.influencer.food_creator", "Colaboración con creador gastronómico", "Contenido de un creador centrado en un plato concreto.", BistroBuilderMarketingCampaignType.InfluencersPress, BistroBuilderMarketingTargetKind.Dish, 50000, 5, 3,
            M(BistroBuilderMarketingModifierKind.TargetDemand, 2200, 0, BistroBuilderMarketingCustomerSegment.Foodies),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 500)));
        r.Add(C("marketing.influencer.local_pack", "Campaña con varios creadores locales", "Varios perfiles coordinados amplifican la notoriedad social.", BistroBuilderMarketingCampaignType.InfluencersPress, BistroBuilderMarketingTargetKind.None, 85000, 5, 4,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 1600, 0, BistroBuilderMarketingCustomerSegment.YoungAdults)));
        r.Add(C("marketing.influencer.press_preview", "Presentación para prensa gastronómica", "Acción orientada a público exigente y reputación gastronómica.", BistroBuilderMarketingCampaignType.InfluencersPress, BistroBuilderMarketingTargetKind.None, 100000, 7, 5,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 1200, 0, BistroBuilderMarketingCustomerSegment.Foodies),
            M(BistroBuilderMarketingModifierKind.Reputation, 0, 2),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 600)));
        r.Add(C("marketing.influencer.major", "Gran influencer gastronómico", "Pico de exposición capaz de saturar un restaurante no preparado.", BistroBuilderMarketingCampaignType.InfluencersPress, BistroBuilderMarketingTargetKind.None, 220000, 4, 7,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 3000, 0, BistroBuilderMarketingCustomerSegment.Foodies),
            M(BistroBuilderMarketingModifierKind.ReservationDemand, 1800),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 1800)));

        // 5 — Eventos y experiencias especiales.
        r.Add(C("marketing.event.romantic_dinner", "Cena romántica", "Experiencia nocturna para parejas con mayor predisposición a reservar.", BistroBuilderMarketingCampaignType.EventsExperiences, BistroBuilderMarketingTargetKind.None, 25000, 1, 2,
            M(BistroBuilderMarketingModifierKind.ReservationDemand, 2000, 0, BistroBuilderMarketingCustomerSegment.Couples, BistroBuilderMarketingDayPart.Dinner),
            M(BistroBuilderMarketingModifierKind.AverageTicket, 500, 0, BistroBuilderMarketingCustomerSegment.Couples, BistroBuilderMarketingDayPart.Dinner),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 600)));
        r.Add(C("marketing.event.live_music", "Noche de música en directo", "Evento nocturno social con mayor permanencia y demanda.", BistroBuilderMarketingCampaignType.EventsExperiences, BistroBuilderMarketingTargetKind.None, 50000, 1, 3,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 1800, 0, BistroBuilderMarketingCustomerSegment.Groups, BistroBuilderMarketingDayPart.Dinner),
            M(BistroBuilderMarketingModifierKind.AverageTicket, 300, 0, BistroBuilderMarketingCustomerSegment.Groups, BistroBuilderMarketingDayPart.Dinner),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 900)));
        r.Add(C("marketing.event.tasting_menu", "Menú degustación especial", "Experiencia gastronómica temporal asociada a una carta concreta.", BistroBuilderMarketingCampaignType.EventsExperiences, BistroBuilderMarketingTargetKind.Menu, 30000, 2, 4,
            M(BistroBuilderMarketingModifierKind.TargetDemand, 1700, 0, BistroBuilderMarketingCustomerSegment.Foodies),
            M(BistroBuilderMarketingModifierKind.AverageTicket, 1500, 0, BistroBuilderMarketingCustomerSegment.Foodies),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 900)));
        r.Add(C("marketing.event.theme_night", "Noche temática", "Evento puntual atractivo para grupos y clientes sociales.", BistroBuilderMarketingCampaignType.EventsExperiences, BistroBuilderMarketingTargetKind.None, 45000, 1, 4,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 2200, 0, BistroBuilderMarketingCustomerSegment.Groups, BistroBuilderMarketingDayPart.Dinner),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 1200)));
        r.Add(C("marketing.event.food_festival", "Festival gastronómico del restaurante", "Evento de gran alcance con demanda y visibilidad elevadas.", BistroBuilderMarketingCampaignType.EventsExperiences, BistroBuilderMarketingTargetKind.None, 120000, 2, 6,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 3000),
            M(BistroBuilderMarketingModifierKind.Reputation, 0, 2),
            M(BistroBuilderMarketingModifierKind.OperationalPressure, 1800)));

        // 6 — Fidelización y recomendación.
        r.Add(C("marketing.loyalty.card", "Tarjeta de cliente habitual", "Incentivo sencillo para aumentar la frecuencia de retorno.", BistroBuilderMarketingCampaignType.LoyaltyReferral, BistroBuilderMarketingTargetKind.None, 18000, 14, 1,
            M(BistroBuilderMarketingModifierKind.RepeatVisit, 800)));
        r.Add(C("marketing.loyalty.return_week", "Programa Vuelve esta semana", "Activa la repetición de clientes recientes a corto plazo.", BistroBuilderMarketingCampaignType.LoyaltyReferral, BistroBuilderMarketingTargetKind.None, 15000, 7, 2,
            M(BistroBuilderMarketingModifierKind.RepeatVisit, 1000)));
        r.Add(C("marketing.loyalty.bring_friend", "Trae a un amigo", "Convierte clientes satisfechos en nuevos acompañantes.", BistroBuilderMarketingCampaignType.LoyaltyReferral, BistroBuilderMarketingTargetKind.None, 25000, 7, 3,
            M(BistroBuilderMarketingModifierKind.OverallDemand, 700),
            M(BistroBuilderMarketingModifierKind.RepeatVisit, 600)));
        r.Add(C("marketing.loyalty.club", "Club de clientes", "Programa estable que favorece retorno y planificación.", BistroBuilderMarketingCampaignType.LoyaltyReferral, BistroBuilderMarketingTargetKind.None, 45000, 21, 4,
            M(BistroBuilderMarketingModifierKind.RepeatVisit, 1200),
            M(BistroBuilderMarketingModifierKind.ReservationDemand, 500)));
        r.Add(C("marketing.loyalty.vip", "Programa VIP", "Retención de clientes de alto valor con mayor gasto potencial.", BistroBuilderMarketingCampaignType.LoyaltyReferral, BistroBuilderMarketingTargetKind.None, 60000, 21, 6,
            M(BistroBuilderMarketingModifierKind.RepeatVisit, 1400, 0, BistroBuilderMarketingCustomerSegment.HighValue),
            M(BistroBuilderMarketingModifierKind.AverageTicket, 700, 0, BistroBuilderMarketingCustomerSegment.HighValue)));

        // 7 — Carta y platos destacados.
        r.Add(C("marketing.menu.dish_week", "Plato de la semana", "Da visibilidad temporal a un plato elegido por el jugador.", BistroBuilderMarketingCampaignType.MenuDishPromotion, BistroBuilderMarketingTargetKind.Dish, 12000, 7, 1,
            M(BistroBuilderMarketingModifierKind.TargetDemand, 1800),
            M(BistroBuilderMarketingModifierKind.OverallDemand, 400)));
        r.Add(C("marketing.menu.house_specialty", "Especialidad de la casa", "Posiciona un plato como referencia gastronómica del local.", BistroBuilderMarketingCampaignType.MenuDishPromotion, BistroBuilderMarketingTargetKind.Dish, 25000, 10, 2,
            M(BistroBuilderMarketingModifierKind.TargetDemand, 2200, 0, BistroBuilderMarketingCustomerSegment.Foodies),
            M(BistroBuilderMarketingModifierKind.Reputation, 0, 1)));
        r.Add(C("marketing.menu.new_dish", "Nuevo plato en carta", "Comunica una novedad para atraer de nuevo a clientes habituales.", BistroBuilderMarketingCampaignType.MenuDishPromotion, BistroBuilderMarketingTargetKind.Dish, 15000, 5, 2,
            M(BistroBuilderMarketingModifierKind.TargetDemand, 1600),
            M(BistroBuilderMarketingModifierKind.RepeatVisit, 300)));
        r.Add(C("marketing.menu.star_menu", "Menú estrella promocionado", "Promociona una carta o menú concreto como oferta principal.", BistroBuilderMarketingCampaignType.MenuDishPromotion, BistroBuilderMarketingTargetKind.Menu, 35000, 7, 3,
            M(BistroBuilderMarketingModifierKind.TargetDemand, 1800),
            M(BistroBuilderMarketingModifierKind.OverallDemand, 600)));
        r.Add(C("marketing.menu.signature_product", "Producto insignia", "Convierte un plato elegido en reclamo distintivo del restaurante.", BistroBuilderMarketingCampaignType.MenuDishPromotion, BistroBuilderMarketingTargetKind.Dish, 60000, 14, 5,
            M(BistroBuilderMarketingModifierKind.TargetDemand, 2800, 0, BistroBuilderMarketingCustomerSegment.Foodies),
            M(BistroBuilderMarketingModifierKind.OverallDemand, 800, 0, BistroBuilderMarketingCustomerSegment.Foodies),
            M(BistroBuilderMarketingModifierKind.Reputation, 0, 2)));

        return r;
    }

    private static BistroBuilderMarketingCampaignDefinition C(
        string id,
        string name,
        string description,
        BistroBuilderMarketingCampaignType type,
        BistroBuilderMarketingTargetKind targetKind,
        long costCents,
        int durationDays,
        int minLevel,
        params BistroBuilderMarketingModifier[] modifiers)
    {
        return new BistroBuilderMarketingCampaignDefinition
        {
            campaignId = id,
            displayName = name,
            description = description,
            type = type,
            targetKind = targetKind,
            baseCostCents = costCents,
            durationDays = durationDays,
            minProgressionLevel = minLevel,
            modifiers = new List<BistroBuilderMarketingModifier>(modifiers)
        };
    }

    private static BistroBuilderMarketingModifier M(
        BistroBuilderMarketingModifierKind kind,
        int basisPoints = 0,
        int flatPoints = 0,
        BistroBuilderMarketingCustomerSegment segment =
            BistroBuilderMarketingCustomerSegment.Any,
        BistroBuilderMarketingDayPart dayPart =
            BistroBuilderMarketingDayPart.Any)
    {
        return new BistroBuilderMarketingModifier
        {
            kind = kind,
            basisPoints = basisPoints,
            flatPoints = flatPoints,
            segment = segment,
            dayPart = dayPart
        };
    }
}
