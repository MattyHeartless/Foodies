# Símbolo de Foodies

La identidad usa una f minúscula con tenedor, basada en la referencia aprobada [foodies-fork-reference.png](design/foodies-fork-reference.png). El símbolo se reconstruyó como vector para conservar nitidez en favicon y en la animación.

`frontend/src/lib/brand-mark.json` es la fuente única de geometría y color (#ee6c3d). El componente `BrandMark` la utiliza en cabecera, pie, portada, títulos del catálogo, tiendas, placeholders, plataforma y overlay. El overlay conserva caída, avance lateral y revelado del nombre; respeta movimiento reducido y permite omitirlo.

Al cambiar el símbolo ejecutar `node frontend/scripts/generate-icons.mjs`. Genera icon.svg, favicon.ico (16/32/48 px) y apple-icon.png (180 px) desde la misma geometría. Next.js publica los enlaces de metadata con identificadores de versión.
