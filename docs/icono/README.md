# Icono de VideoSeeker

Prismáticos de tubos rectos vistos desde delante y un poco desde arriba, con perspectiva: las lentes, que están cerca, se ven grandes y los tubos se juntan un poco hacia los oculares. Mario lo eligió el 2 de noviembre de 2026 (propuesta «14a, perspectiva baja»). Sustituye al escudo con el explorador en la montaña, que tenía demasiado detalle para un icono. Mario quería algo sobrio y profesional, a juego con la interfaz oscura.

- `VideoSeeker.svg`: dibujo principal (256 × 256). Se usa en 20 px y desde 48 px.
- `VideoSeeker-barra.svg`: versión para la barra de tareas (propuesta «16a»). Es el mismo dibujo con los tubos algo más largos y juntos, para que sea casi cuadrado y llene el icono de arriba abajo. Mario la eligió solo para la barra porque allí, a 24 px, el principal se veía demasiado pequeño al lado de los demás iconos. Se usa en 24, 32 y 40 px, que son los tamaños que toma la barra (24 px al 100 %, y 32 o 40 px reducidos con más escala). Alt+Tab también puede usarlos.
- `VideoSeeker-16.svg`: versión de 16 px dibujada píxel a píxel. Lleva la rueda de enfoque arriba, el cuerpo oscuro que se ensancha y dos lentes redondas separadas.
- `VideoSeeker-256.png`: vista previa.
- `fuente/`: el modelo 3D sencillo que genera el dibujo grande. Es un render plano con perspectiva hecho en Python, sin dependencias. El principal se genera con `python3 roof3d.py salida 14a-perspectiva-baja:0:35:0:320` (los parámetros son giro, inclinación, alabeo y distancia de cámara). El de la barra sale de `make16.py` (`16a-grande-actual`).
- El icono de la app es `src/VideoSeeker.App/Assets/VideoSeeker.ico`, con 16, 20, 24, 32, 40, 48, 64, 96, 128 y 256 px. Las ventanas lo toman solas del exe (`ApplicationIcon` en el csproj).

Colores (planos, sombreado en tres tonos): goma grafito `#2A2F37` / `#3A414C` / `#4D5562`, metal `#5F656E` / `#8A9099` / `#B7BDC6`, cristal `#14243A` con reflejo `#2F5E9E` y brillo `#E6E8EB`, contorno `#16181C` (el fondo de la app). En el 16 px, el centro de la lente usa el azul de la app, `#4C9BFF`.
