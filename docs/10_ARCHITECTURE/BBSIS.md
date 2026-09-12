# BB Spatial Interaction System (BBSIS)

**Estado canónico:** V1 COMPLETO, VALIDADO Y CERRADO. No reabrir diseño salvo regresión demostrable.

## Autoridad
BBSIS es la única autoridad de **viabilidad y reserva espacial** para interacciones. No decide intención de gameplay, derechos lógicos, navegación ni animación.

## Conceptos vinculantes
- separación estricta `Render ≠ Spatial`;
- `Adaptive Spatial Proxy` para representación espacial robusta;
- `Spatial Contracts` como requisitos de interacción;
- traits y matriz de compatibilidad;
- `Claims / Spatial Leases` para exclusividad espacial temporal;
- `Spatial Episodes` como ciclo de una interacción espacial;
- `Work Edges`, `Ports` y `Seat Bays` como interfaces de uso;
- `Dynamic Sweep` para volumen requerido durante movimiento/interacción;
- `Mobility Envelope` y `Carry Envelope`;
- `Spatial Gates`, `Critical Routes` y `Flow Quality`;
- LOD lógico y perfiles de tuning data-driven.

## Reglas
- Interaction & Reservation no replica Claims/Leases.
- Navigation puede consultar viabilidad pero conserva autoridad de trayectoria.
- Animation representa la resolución espacial sin modificarla.
- Modo Edición y BBPLFS deben validar contra contratos espaciales comunes.
- Puertas y sillas pueden cambiar su ocupación espacial durante episodios relevantes; no se simulan mediante física por bisagra como autoridad de gameplay.

## Evolución
Cambios futuros deben tratarse como hardening/V2 y demostrar que respetan contratos públicos y compatibilidad con sistemas ya integrados.