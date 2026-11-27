# GestionCarp

Aplicación de escritorio para Windows que sirve para **explorar carpetas, compartir su estructura y renombrar
archivos por lotes**, con soporte especial para series: los subtítulos se renombran a la vez que los capítulos
para que el reproductor los cargue solo.

## Funciones (fase 1)

### 📁 Explorador y exportar
- Árbol completo de cualquier carpeta, con tamaño, número de archivos y subcarpetas.
- Filtros: profundidad, mostrar u ocultar archivos, ocultos/sistema, carpetas a excluir (`.git; node_modules`)
  y tipos de archivo (`*.mkv; *.mp4`).
- Exportación en **texto (árbol)**, **Markdown**, **JSON**, **HTML** (página con carpetas plegables),
  **Excel** (.xlsx, una fila por elemento con filtros) y **Word** (.docx), con tamaños, fechas y totales opcionales.
  Botón **Copiar al portapapeles** y **Guardar como…**. Si seleccionas una subcarpeta, se exporta solo esa.
- **Usar como plantilla**: lleva las carpetas a la pestaña *Crear carpetas* para repetir la estructura en otro sitio.

  ```
  Mi Serie/
  ├── Temporada 1/
  │   ├── Mi Serie - Cap01.mkv
  │   └── Mi Serie - Cap01.es.srt
  └── Temporada 2/
  ```

### ✏️ Renombrar (estilo ReNamer)
- Reglas encadenables con **vista previa en tiempo real**:
  - **Numerar**: `Mi Serie - Cap` + `01, 02, 03…` (inicio, incremento, cifras, al principio/final o
    sustituyendo el nombre, reinicio por carpeta para temporadas).
  - **Plantilla de series**: `{serie} - T{temp:00}E{ep:00} - {titulo}` → `Mi Serie - T01E05 - Piloto`.
    Detecta temporada y episodio en el nombre (o la temporada en carpetas como `Temporada 2`).
  - **Limpiar nombres**: `[Grupo] Mi.Serie.S01E02.1080p.WEB-DL.x264-XYZ` → `Mi Serie S01E02`.
  - **Metadatos**: `{fecha:yyyy-MM-dd HH.mm.ss}` o `{pista:00} - {titulo}` con datos internos de fotos (EXIF de
    JPEG, PNG), música (MP3, FLAC, M4A) y vídeo (MP4, MOV): fecha, cámara, resolución, artista, álbum, duración…
  - **Reemplazar** (texto o expresión regular), **Insertar**, **Eliminar** (primeros/últimos caracteres,
    un tramo o todo lo que hay entre `[` `]`), **Mayúsculas/minúsculas**.
- **Presets**: guarda combinaciones de reglas con un nombre, cárgalas con un clic y expórtalas a un archivo
  para compartirlas. Incluye presets de ejemplo.
  - Cada regla puede aplicarse al nombre, a la extensión o a ambos.
- **Orden natural** (`cap2` antes que `cap10`) y reordenación manual.
- Detección de errores antes de tocar nada: nombres duplicados, caracteres no válidos en Windows, nombres
  reservados (`CON`, `NUL`…), archivos que ya existen, rutas demasiado largas.
- Renombrado seguro (intercambios `A↔B` incluidos): si algo falla, se revierte todo.
- **Deshacer**, también después de cerrar la aplicación (historial en `%LocalAppData%\GestionCarp`).
- **Subtítulos acompañantes**: al renombrar `serie.e1.mkv`, sus subtítulos `serie.e1.srt`,
  `serie.e1.es.srt` o `serie.e1.en.forced.ass` pasan a llamarse como el vídeo nuevo, conservando el idioma.

### 💬 Subtítulos
Para cuando los subtítulos tienen nombres distintos a los vídeos (por ejemplo, descargados aparte):
- Empareja **por número de episodio** (`S01E05`, `1x05`, `Cap 5`, `Capítulo 05`, `Episodio 5`, `Serie - 05`…)
  o **por orden** dentro de cada carpeta (cada idioma por separado).
- Renombra cada subtítulo como su vídeo: `Mi Serie - Cap05.mkv` → `Mi Serie - Cap05.es.srt`.
- Conserva o añade la etiqueta de idioma, y saca los subtítulos de carpetas `Subs` para dejarlos junto al vídeo.
- Puedes desmarcar los que no quieras tocar; también se puede deshacer.

### 🧱 Crear carpetas
- Pega un árbol (el que exporta GestionCarp), una lista con sangría, Markdown o JSON y se crea esa estructura
  dentro de la carpeta que elijas. Vista previa con lo que se creará, lo que ya existe y los nombres no válidos.
- Opcionalmente crea también los archivos (vacíos). Se puede **deshacer**: borra solo lo creado que siga vacío.

### ⚖️ Comparar carpetas
- Muestra qué está solo en A, solo en B y qué es distinto (por nombre y tamaño, también fecha, o contenido exacto).
- **Copia lo que falta** de una carpeta a otra sin sobrescribir nunca nada (se puede deshacer) y copia un informe.

### 👯 Duplicados
- Encuentra archivos idénticos por contenido en una o varias carpetas, aunque tengan otro nombre.
- Marca automáticamente las copias sobrantes (conservando la más antigua, la más reciente o la de ruta más corta)
  y las envía a la **Papelera de reciclaje**.

### General
- **Menú contextual del Explorador** (opcional, *Archivo*): clic derecho en una carpeta → *GestionCarp* (explorar,
  renombrar, subtítulos, comparar, duplicados, crear carpetas) y en archivos → *Renombrar con GestionCarp*.
- Interfaz en **español, inglés y portugués** (menú *Idioma*); el cambio es inmediato y se recuerda.
- **Buscar actualizaciones** (menú *Ayuda*): compara tu versión con la última publicada en GitHub y, si hay una
  nueva, la descarga, sustituye el programa y lo reinicia. Puede comprobarse automáticamente al iniciar.
- Ventana **Acerca de** con la versión instalada y el autor.
- **Guía de uso** integrada (menú *Ayuda* o tecla <kbd>F1</kbd>): se abre en el navegador sin conexión, en el
  apartado de la pestaña en la que estés y en el idioma elegido.
- La ventana se adapta al tamaño de la pantalla la primera vez y recuerda su tamaño y posición.

## Descargar

Descarga `GestionCarp.exe` de la última versión en
**[Releases](https://github.com/ArslanMe/GestionCarp/releases/latest)** y ábrelo; no hace falta instalar nada.
Cada actualización de `main` publica una versión nueva automáticamente.

Si Windows muestra «Windows protegió su PC» (el programa no está firmado), pulsa
**Más información → Ejecutar de todas formas**.

## Compilar desde el código

Requiere el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet test
dotnet run --project src/GestionCarp.App
```

## Estructura

| Proyecto | Contenido |
|---|---|
| `src/GestionCarp.Core` | Lógica sin interfaz: análisis y exportación del árbol, reglas, planificador, ejecutor con deshacer, emparejado de subtítulos. |
| `src/GestionCarp.App` | Interfaz WPF (MVVM). |
| `tests/GestionCarp.Core.Tests` | Pruebas automáticas de la lógica. |


## Autor y licencia

Creado por **Raudel Camacho** · © 2026 Raudel Camacho

Publicado con la [licencia MIT](LICENSE): puedes usar, copiar, modificar y distribuir el programa libremente,
siempre que se conserve el aviso de copyright y de licencia.
