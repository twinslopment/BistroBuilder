# Marketing — definición funcional por secciones (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** **MARKETING CERRADO FUNCIONALMENTE el 09/11/2026**. APROBADAS las dos secciones definitivas: **Catálogo de campañas** y **Campañas activas**. Sin rediseño ni implementación de mejoras de Unity.
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

## Marketing → Catálogo de campañas — FUNCIONALIDADES APROBADAS

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

### Límites aprobados

- No permitir que el jugador cree/edite manualmente campañas, imágenes, anuncios, presupuestos libres o sus multiplicadores. No inventar una plataforma publicitaria externa.
- No descontar dinero al previsualizar; el cargo ocurre al aceptar e iniciar la campaña.
- No prometer resultados determinados ni asociar directamente un número exacto de nuevos clientes a un modificador de demanda.
- No establecer automatización recurrente de campañas, programación de campañas futuras ni reembolsos si aún no existen reglas aprobadas.
- No mezclar Marketing con Reputación: Marketing puede influir en ella mediante efectos; la autoridad de reputación permanece en su propio sistema.
- Mantener la identidad global premium de Bistro Builder en fase de diseño, sin alterar las barras superiores/inferiores definitivas.

## Marketing → Campañas activas — FUNCIONALIDADES APROBADAS

**Objetivo:** permitir al jugador **supervisar y cancelar las campañas que están vigentes**, con información clara sobre sus compromisos, duración y efectos configurados. Evitar fingir un retorno económico individual o resultados de campaña que el sistema no calcula.

1. **Mostrar las campañas que siguen activas en el día de juego actual**, con un contador total y estado vacío comprensible si no hay ninguna. No confundir campañas disponibles en el catálogo con contrataciones efectivas.
2. **Ordenar de forma útil:** próximas a terminar primero, como hace el código actual. La UI futura puede incluir filtros por familia y objetivo, y búsqueda por campaña, siempre que se implementen explícitamente; el buscador actual se oculta en esta pestaña.
3. **Identificar cada campaña** por nombre, familia, objetivo (restaurante general, plato o carta), coste ya pagado, día de inicio, último día activo y días restantes. Calcular las fechas con el calendario de juego: `endDayExclusive` no es un día activo.
4. **Consultar la ficha de campaña activa** con descripción y modificadores definidos en su catálogo: demanda, reservas, visitas espontáneas, reputación, ticket medio, repetición, promoción de plato/carta y presión operativa, cuando correspondan.
5. **Explicar efectos favorables y desfavorables** sin presentar los modificadores como ventas garantizadas, clientes captados efectivos ni rentabilidad demostrada. Los efectos reales dependen de la evaluación de sistemas propietarios y del contexto del servicio, segmento o franja.
6. **Mostrar su coste como pagado**, sin volver a deducir efectivo cada vez que se consulte y sin confundir gasto de Marketing con un coste por día salvo que el motor lo modele así. Enlace a Caja/Resultados cuando una relación trazable esté disponible, sin nuevo ledger.
7. **Permitir cancelar una campaña todavía activa**, con confirmación de segundo paso que informe claramente de las consecuencias: **se pierden sus efectos futuros, no se reembolsa el coste y no se revierten los efectos ya materializados**. La acción usará exclusivamente `BistroBuilderMarketingService.TryCancelCampaign`.
8. **No permitir pausa, edición de costes/duración/objetivo ni reanudación del contrato actual.** Si el jugador quiere una nueva campaña, deberá volver al Catálogo y contratar otra campaña válida con un nuevo gasto y comprobaciones.
9. **Actualizar automáticamente el tiempo restante y el estado** al avanzar el día de juego o variar el sistema canónico. Las campañas vencidas desaparecen de Activas por la depuración del motor; no debe quedar una etiqueta «activa» cuando haya expirado.
10. **Conservar estado tras guardar y cargar:** reconstruir la interfaz leyendo las campañas runtime de `marketing.state`, sin persistir un segundo registro de UI ni aplicar nuevamente el gasto al cargar.
11. **Enlace a Catálogo de campañas** para seleccionar otra acción publicitaria. Marketing puede afectar a Reputación, Reservas, Carta y Clientes, pero las pantallas de esos sistemas siguen siendo autoridades separadas.

### Estado comprobado del código y trabajo pendiente

- `BistroBuilderMarketingPlayerScreen.ShowActiveCampaigns` activa `ViewMode.Active` y muestra las campañas del snapshot de la fachada, con panel de detalle, días restantes, objetivo, coste abonado y resumen de efectos.
- `BistroBuilderMarketingPlayerFacade.BuildActiveRows` lee exclusivamente `CopyActiveCampaigns` para el día actual, resuelve definición y nombre del objetivo, y ordena por **fecha de finalización ascendente**.
- `BistroBuilderMarketingPlayerScreen.CancelSelectedActive` ya implementa **dos pulsaciones de confirmación**, con aviso de **sin reembolso** y de que los efectos anteriores no se revierten; el servicio vuelve a validar la existencia/actividad.
- `BistroBuilderMarketingEngine.TryCancelCampaign` elimina el registro de la campaña de `marketing.state`; `TryPruneExpired` también elimina las expiradas. **No hay actualmente un historial permanente de campañas finalizadas/canceladas en Marketing** ni una métrica canónica de ingresos atribuibles a cada campaña. Finanzas sí conserva el cargo real.
- `BistroBuilderMarketingPlayerScreen.ApplyTabState` oculta buscador y filtros en Activas; cualquier búsqueda/filtros en esta vista será nueva UI pendiente de integración y pruebas.
- Las referencias de objetivo, las fechas y el coste son datos existentes; **enlaces profundos** a Caja, Reputación o Carta requieren integración futura, nunca una segunda fuente de verdad.

### Límites aprobados

- No inventar ROI por campaña, gráficos de conversiones, clientes atribuidos o una estadística de «campaña más rentable» sin definir primero una métrica real y trazable.
- No prometer un **historial de campañas** finalizadas o canceladas: el estado actual elimina esos registros. Si se decide añadirlo, requerirá una mejora de dominio/persistencia específica y pruebas, no solo diseño.
- No permitir edición retroactiva, reintegros, pausas, prórrogas o cambios manuales de multiplicadores sin un mecanismo aprobado.
- No introducir una tercera pestaña de resultados o un módulo de Reputación dentro de Marketing por defecto.
- Las decisiones funcionales aprobadas no equivalen a una build Unity integrada con el rediseño premium ni a prueba visual de la navegación V21/E5.5.

## Próximo paso

**Marketing queda cerrado funcionalmente** con Catálogo de campañas y Campañas activas. Continuar con **Reputación → Valoración y opiniones**, dejando separados el rediseño visual y la programación posterior.
