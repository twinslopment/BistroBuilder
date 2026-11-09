# Marketing — definición funcional por secciones (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** **Marketing → Catálogo de campañas: PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN**. **Marketing → Campañas activas** queda pendiente de definir y aprobar.
**Alcance:** definición funcional únicamente. No modificar código Unity ni el diseño en esta fase. La visualización futura debe cumplir `SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md` y las barras de Modo Juego V21/E5.5.

## Estructura real contrastada

`BistroBuilderMarketingPlayerScreen` incluye actualmente dos vistas internas:
1. **Catálogo de campañas** (`ViewMode.Catalog`) — campañas y contratación.
2. **Campañas activas** (`ViewMode.Active`) — seguimiento y cancelación de campañas contratadas.

El catálogo de contenido semilla `BistroBuilderMarketing7ASeedFactory` define **35 campañas en siete familias**; los datos publicados realmente corresponden al `BistroBuilderMarketingCampaignCatalog` activo de la partida. No hay tercera pestaña de resultados de marketing ni editor de campañas jugable en el código examinado.

Las siete familias de `BistroBuilderMarketingCampaignType` son:
- **Publicidad y notoriedad local** (`LocalAwareness`).
- **Promociones y descuentos** (`Promotions`).
- **Marketing digital** (`Digital`).
- **Influencers y prensa** (`InfluencersPress`).
- **Eventos y experiencias** (`EventsExperiences`).
- **Fidelización y recomendación** (`LoyaltyReferral`).
- **Promoción de cartas y platos** (`MenuDishPromotion`).

Autoridad: `BistroBuilderMarketingService` posee el estado `marketing.state` y las campañas contratadas. El gasto se contabiliza exclusivamente por `BistroBuilderDiscretionaryFinanceService`; las variaciones de demanda, reservas, ticket medio, reputación, repetición y presión operativa pertenecen a sus correspondientes sistemas de juego. `BistroBuilderMarketingPlayerFacade` es la fachada de la UI; **no crear otra fuente de datos**.

## Marketing → Catálogo de campañas — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** permitir al jugador elegir acciones promocionales para captar clientes, aumentar la demanda, fomentar reservas y fidelizar, comprendiendo **coste, duración, público objetivo y efectos/riesgos** antes de pagar.

1. **Consultar todas las campañas publicadas en el catálogo jugable**, incluyendo las bloqueadas por progresión, con nombre, categoría y descripción. No presentar campañas ficticias ni contenido de autoría no publicado.
2. **Buscar y filtrar campañas** por nombre, descripción y familia. Mantener los siete filtros ya existentes y uno «Todas»; evitar multiplicar pestañas solo para subcategorías.
3. **Consultar ficha comercial completa**: coste fijo en moneda de juego, duración en días de juego, nivel necesario, descripción, objetivos elegibles y efectos modificadores publicados en la definición.
4. **Distinguir campañas disponibles de bloqueadas**, explicando el requisito real de nivel o de objetivo válido. Un nivel superior no se puede saltar ni comprar manualmente desde esta pantalla.
5. **Elegir un objetivo cuando corresponda:** seleccionar un **plato** o una **carta** entre los disponibles según la autoridad de Carta. Algunas campañas son generales y no necesitan objetivo. No modificar recetas, escandallos ni elementos del catálogo maestro desde Marketing.
6. **Mostrar a qué puede afectar cada campaña**: demanda general, reservas, clientes espontáneos, reputación, ticket medio, repetición de visitas, demanda de plato/carta y presión de trabajo del restaurante. Mostrar también los **efectos desfavorables** (por ejemplo, menor ticket medio o mayor presión operativa), segmentos/franjas y duración cuando figuren en los modificadores.
7. **Comparar campañas de forma comprensible** por coste, duración, objetivo y efectos definidos, sin prometer un retorno de inversión exacto, número fijo de comensales o aumento garantizado de beneficios. La UI actual permite buscar y abrir fichas; la comparación lado a lado es una mejora de presentación pendiente.
8. **Avisar si una campaña idéntica ya está activa para el mismo objetivo** y bloquear ese duplicado durante su vigencia. La lógica canónica permite campañas distintas o dirigidas a objetivos diferentes cuando sean válidas: no imponer un bloqueo global que el motor no contempla.
9. **Contratar/iniciar una campaña con confirmación explícita**: mostrar nombre, objetivo, coste total, duración, efectos favorables/desfavorables y aclarar que el gasto es **inmediato y no reembolsable**, incluso si posteriormente se cancela. La contratación se acepta solo cuando el servicio de marketing valida el objetivo, la progresión, la no duplicidad y el servicio financiero autoriza el débito.
10. **Iniciar el efecto el día actual del juego** y conservar la duración exacta y el coste en la instancia contratada. Una contratación no crea clientes ni reservas artificiales de inmediato: los sistemas propietarios aplican los modificadores al evaluar su lógica.
11. **Acceso a Campañas activas** tras contratar para consultar su vigencia, objetivo y posibilidad de cancelar con las reglas del juego; nunca generar un segundo historial o un carrito de publicidad.
12. **Actualizar disponibilidad, objetivos y estado** cuando cambien progresión, Carta, campañas vigentes o hechos canónicos, sin fijar permanentemente valores obsoletos en la interfaz.

### Estado comprobado del código y diferencias

- `BistroBuilderMarketingPlayerScreen` ya implementa dos pestañas internas **Catálogo** y **Activas**, búsqueda por nombre/descripción/efectos, filtros por siete familias, selección de campaña y selector de objetivo plato/carta.
- `BistroBuilderMarketingPlayerFacade.BuildCampaignRows` expone coste, duración, nivel de progresión, resumen de modificadores, desbloqueo y razón de bloqueo. `BuildTargetOptions` lee objetivos de los sistemas canónicos de Carta y Platos.
- `BistroBuilderMarketingEngine.TryCreateCampaign` impide repetir el **mismo CampaignId + TargetId activo**. Revisa duración/objetivo y genera una instancia persistible; no es una creación libre de campañas personalizadas.
- `BistroBuilderMarketingService.TryStartCampaign` valida requisitos y **contabiliza un gasto al iniciar** mediante `BistroBuilderDiscretionaryFinanceService.TryPostExpense`, después consolida el estado; no concede una campaña si falla la operación financiera.
- **Hueco concreto de UX:** el botón actual `BistroBuilderMarketingPlayerScreen.StartSelectedCampaign` contrata inmediatamente, **sin modal o segundo clic de confirmación**. La confirmación previa y la previsualización completa de impacto favorable/desfavorable son **requisitos funcionales propuestos**, pendientes de implementación y pruebas.
- `BistroBuilderMarketingModifierKind` define los tipos de efecto; se expresan como puntos básicos o puntos planos de reputación, y pueden estar condicionados por segmento de cliente y franja. **No son una garantía numérica de ventas futuras**.
- El código semilla prevé **35 campañas (7 × 5)**, pero la UI debe leer exclusivamente el `BistroBuilderMarketingCampaignCatalog` instalado: no codificar «35» como condición ni mostrar campañas ausentes del asset real.

### Límites propuestos

- No permitir que el jugador cree/edite manualmente campañas, imágenes, anuncios, presupuestos libres o sus multiplicadores. No inventar una plataforma publicitaria externa.
- No descontar dinero al previsualizar; el cargo ocurre al aceptar e iniciar la campaña.
- No prometer resultados determinados ni asociar directamente un número exacto de nuevos clientes a un modificador de demanda.
- No establecer automatización recurrente de campañas, programación de campañas futuras ni reembolsos si aún no existen reglas aprobadas.
- No mezclar Marketing con Reputación: Marketing puede influir en ella mediante efectos; la autoridad de reputación permanece en su propio sistema.
- Mantener la identidad global premium de Bistro Builder en fase de diseño, sin alterar las barras superiores/inferiores definitivas.

## Próximo paso

Solicitar aprobación de **Marketing → Catálogo de campañas**. Después definir **Marketing → Campañas activas** y valorar solo entonces si se necesita una tercera sección funcional real, sin crearla por costumbre.
