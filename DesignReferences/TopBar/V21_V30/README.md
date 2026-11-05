# Prototipo visual aprobado: Bistro Builder V21 + V30 rápida

Abrir `index.html` con navegador web. Mantener `styles.css`, `app.js` y `assets/` en sus rutas actuales.

Esta carpeta es la **referencia visual vinculante** para la nueva barra superior del juego. No es una escena de Unity ni introduce lógica jugable.

Consulta [el documento de integración](../../../docs/30_UI_UX/BARRA_SUPERIOR_V21_V30_APROBADA.md) (enlace relativo desde el repositorio) para criterios, estados y pruebas.

**Identidad:** logo original, fondo negro satinado, 10 iconos SVG blancos, selección luminosa.

**Movimiento elegido:** V30 rápida — 280 ms; el modo «Lenta» es exclusivamente demostrativo.

**Wheel/Servicio:** concepto separado; no se accede desde el logo.

## Comprobación de identidad (SHA-256)

- `index.html`: `E8ACC676E0EDB2789CB54844B324571B461C42A6E6843D7FB9E9D4B6171F2517`
- `styles.css`: `EBC5401D9225856EA6B5CB8E7A6C711EEC10052A9CDDEC920B4F31A125AFDCAA`
- `app.js`: `8EE36736957FC2DA4A319ED6DF9392FCA27F0BB8FA28CEC5164256CE27F019CA`
- `assets/Logotipo_Bistro_Builder_Original.png`: `3FD6C2E45A5E2E3E030F52E45452EAD1029068B2BE02028AA14DD7F89C165D56`

Fuente exacta conservada. El código C# del movimiento V30 se encuentra en `Assets/Scripts/Presentation/UI/` en esta misma rama.
