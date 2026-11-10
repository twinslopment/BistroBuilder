# Bistro Builder — Bloque 03 · Inventario · V7 responsive (aprobada)

**Fecha de aprobación visual:** 10 de noviembre de 2026.
**Decisión:** el usuario aprueba la versión responsive V7 para conservar como referencia reutilizable; podrán hacerse cambios posteriores.
**Fase:** previsualización en HTML/CSS/JS. **No integrada en Unity**.

## Entregable congelado
- `BistroBuilder_Inventario_Responsive_V7.html` — previsualización autocontenida con datos simulados.
- `BistroBuilder_Inventario_Responsive_V7_Paquete.zip` — HTML, CSS, pruebas, capturas e informe de prueba.
- `INFORME_DE_PRUEBAS.md` — comprobación de 9 resoluciones y limitaciones.
- `Comparativa_4_resoluciones.png` — muestra visual de adaptación a distintas relaciones de aspecto.

## Funciones incluidas
- Existencias: búsqueda, filtros, estado y detalle de ingredientes.
- Alertas de stock: productos bajos, críticos, agotados y caducidad próxima.
- Movimientos: entradas, consumos, ajustes, caducidad.
- Recepciones: entradas de mercancía y detalle.
- Ajustes de stock **simulados** con movimientos de ejemplo, reinicio de prueba.

## Identidad vinculante
- Sistema visual común de Personal y `docs/30_UI_UX/BISTRO_BUILDER_DESIGN_SYSTEM_GLOBAL_V1.md`.
- Pantalla completa entre barras, navegación lateral, filas crema, grafito cálido, bronce y brillo discreto.
- Movimiento reducido y luz al 50 % como decisiones artísticas globales.
- **Barras originales del juego**: superior V21/V30 e inferior E5.5, sin rediseñar ni modificar. Las barras dentro de la preview son capturas de referencia, no las originales animadas.

## Evidencia y estado de pruebas
- Verificadas nueve resoluciones: 1024×768, 1280×720, 1366×768, 1440×900, 1600×900, 1904×910, 1920×1080, 2560×1440 y 3440×1440.
- Pruebas de navegación, filtros, detalle, ajustes y cambios dinámicos de resolución; sin excepciones JS observadas.
- Las fuentes exactas de Personal no han sido verificadas; Segoe Script e Inter con fallback forman parte del prototipo. No distribuir archivos de fuentes.
- **La aprobación es visual del prototipo**. No afirma pruebas de Unity, integración con el inventario canónico ni gameplay real.

## Control de cambios
- Mantener V7 original **sin sobrescribir** como versión de referencia.
- Las modificaciones futuras se realizarán como nuevas versiones V8+ o en ramas de diseño nuevas, sobre esta base.
- No fusionar en la rama principal de integración ni modificar escenas, scripts, assets o prefabs de Unity sin autorización posterior.

## Integridad del entregable original
- SHA-256 HTML: `87687eb4056ae56e9e5d2e83b5aab678425d69c802d0b03049b8507b337a7a0d`
- SHA-256 ZIP: `d8d01c979d915df7d2f940cecea7db419f76bab76cf8e9a68214d516d0b2fc3f`