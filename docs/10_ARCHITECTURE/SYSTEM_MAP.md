# Bistro Builder — mapa canónico de sistemas

## Principio central
Cada sistema tiene una autoridad explícita. Integrar significa consumir contratos públicos, no copiar estado ni crear una segunda fuente de verdad.

## Cadena runtime
`Gameplay/IA → Interaction & Reservation → BBSIS → Navigation → Animation → sistema de dominio`.

## Capas principales
| Capa | Responsabilidad |
|---|---|
| Gameplay / IA | intención, prioridades y resultado de gameplay |
| Interaction & Reservation | asignaciones, permisos lógicos, custody y colas semánticas reales |
| BBSIS | viabilidad espacial, contratos, claims/leases, puertos, sweeps y gates |
| Navigation & Crowd Flow | rutas, velocidad lógica, heading, llegada, circulación y tráfico humano |
| Character Animation | representación visual, motion recipes, blending, IK, gaze y recovery |
| Sistemas de dominio | clientes, comandas, cocina, personal, inventario, economía, reservas, etc. |
| Presentation/UI | lectura y comandos; nunca autoridad del estado de dominio |
| Persistence | snapshots versionados y orden de rehidratación entre autoridades |

## Sistemas transversales adicionales
- **BBPLFS:** generación y distribución procedural de layouts y furnishing; consume contratos canónicos.
- **Modo Edición/Construcción:** edición estructural del restaurante fuera de servicio.
- **Climate & Weather:** condiciones globales simplificadas; interior confortable.
- **UI Design System:** lenguaje visual común sin redefinir reglas de negocio.

## Regla de integración
Un sistema puede proyectar datos derivados de otro, pero no poseer la fuente original. Finanzas recibe nómina calculada por Personal; Horarios filtra elegibilidad pero no crea empleados; Animation representa custody pero no la decide.

## Persistencia
Los proveedores de Save/Load deben conservar identidad estable, ser versionados y respetar dependencias de Apply/Finalize. No se permiten serializadores paralelos para el mismo estado canónico.