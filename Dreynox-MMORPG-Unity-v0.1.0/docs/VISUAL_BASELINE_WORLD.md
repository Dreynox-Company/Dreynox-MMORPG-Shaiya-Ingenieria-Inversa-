# Baseline visual histórico — world / HUD

## Propósito

Este documento conserva la referencia visual alcanzada antes de migrar el cliente
de Flutter a Unity. No es una definición de assets ni una medición de porcentaje:
es un registro de diferencias observables que Unity debe cerrar contra `game.exe`.

Fuente de trabajo histórica: `side-by-side-world.jpg` (2048×742), generada durante
la fase de QA visual del cliente Flutter. En esa composición el lado que contiene
el overlay de laboratorio (`[Backend] QA visual`) corresponde al cliente
reconstruido; el otro lado se usa como referencia del cliente clásico.

## Lo que ya estaba razonablemente alineado

- Encuadre general de cámara tercera persona y escala del personaje.
- Posición relativa de la ventana de quest/tutorial.
- Botones Accept / Cancel y estructura general del panel.
- Vitals en esquina superior izquierda.
- Barra numérica superior.
- Minimap en cuadrante superior derecho.
- Personaje/NPCs colocados en el mismo tipo de zona jugable.

## Diferencias que Unity debe cerrar

1. **Iluminación y atmósfera**
   - La reconstrucción histórica tenía cielo y luz más planos.
   - Deben calibrarse exposición, ambient light, fog y sombras contra la captura
     exacta de `game.exe`, no contra valores arbitrarios de URP.

2. **Terreno**
   - Color, mezcla de capas, detalle y transición camino/césped todavía diferían.
   - El gate será una captura con la misma cámara y posición, seguida de
     MAE/RMSE/PSNR/SSIM y revisión del diff.

3. **Vegetación y props**
   - Árboles, follaje y algunas siluetas no coincidían todavía con la referencia.
   - Deben preservarse posición, escala, billboard/LOD y alpha-cutout.

4. **UI final**
   - Deben eliminarse overlays de laboratorio del Client Release.
   - La legibilidad/tipografía de algunos textos y reward labels difería.
   - El sandbox conserva diagnóstico, pero el release real nunca lo muestra.

5. **Minimap**
   - El contenido, escala y framing del minimap deben salir del mismo mapa/posición
     de mundo que la escena 3D.

## Gate Unity

Para cada escenario visual estable:

1. fijar variante de `game.exe` por SHA-256;
2. capturar `game.exe` y Unity en la misma resolución;
3. usar `Dreynox MMORPG > Client Parity > Visual Screenshot Comparator`;
4. guardar MAE, RMSE, PSNR, SSIM y diff PNG;
5. corregir cámara/materiales/lighting/UI;
6. repetir hasta que la diferencia restante sea explicable y documentada.

La métrica no reemplaza la inspección visual: un SSIM alto no certifica por sí
solo paridad de animación, input, timing, gameplay o protocolo.
