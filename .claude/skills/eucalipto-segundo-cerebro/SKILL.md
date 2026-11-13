---
name: eucalipto-segundo-cerebro
description: Consultar y actualizar el segundo cerebro de LECG / Casa Eucalipto (registro maestro, aprendizajes operativos, KnowledgeExplorer). Usar antes de repetir una investigación ("¿ya probamos esto?", "qué decidimos de X", "antecedentes de"), al cerrar un trabajo para dejar constancia, o cuando el usuario dice "anota", "registra", "recuerda en el proyecto".
---

# Segundo cerebro

Es el único sistema de memoria del proyecto. No crees otro, ni un `CLAUDE.md` paralelo, ni copies estas entradas a la memoria privada del agente.

## Archivos
- `docs/review/second-brain/registro-maestro.md`: decisiones y estados por tema, con fecha. Las entradas recientes van al final.
- `docs/review/second-brain/aprendizajes-operativos.md`: métodos que funcionaron y fallos que no hay que repetir.
- `docs/review/second-brain/lecciones-por-tema.md`: esas lecciones destiladas por tema (puente, ensayo, familias, cota real, PBR, Enscape, multicapa, uniones, recintos, Blender e IES), con su coste. Léelo primero.
- `docs/review/second-brain/criterio-del-arquitecto.md`: sus preferencias, con cita y fecha, separando lo firme de lo inferido.
- `docs/review/claude-code-casa-eucalipto-continuidad-20261107.md`: traspaso a Claude Code.
- Informes por tema: `docs/review/<tema>-<fecha>/`. Recibos JSON: `.tmp/<tema>/`.

## Consultar (antes de investigar o proponer)
```bash
rg -n -i "<término>" docs/review/second-brain/{lecciones-por-tema,criterio-del-arquitecto,registro-maestro,aprendizajes-operativos}.md | head -20
dotnet run --project tools/KnowledgeExplorer -- query "<pregunta>" project   # all | repo | project | sessions
dotnet run --project tools/KnowledgeExplorer -- timeline "<tema>"          # qué pasó y en qué orden, con conversaciones
dotnet run --project tools/KnowledgeExplorer -- index                      # vectores bge-m3 que falten (GPU, ~1 min)
dotnet run --project tools/KnowledgeExplorer -- graph-find "<término>"
```
Busca IDs (`1745596`), nombres de familia (`LECG_179`) y temas (`barandal`, `Jardín Zen`). Revisa primero los intentos fallidos registrados.

El hook `.claude/hooks/recuerdo.mjs` ya añade a cada mensaje del usuario los fragmentos más pertinentes, con el aviso «Segundo cerebro LECG». Son pistas con fecha, no el estado actual: contrástalas antes de actuar y profundiza con los comandos de arriba cuando no basten. En pedidos de diseño, el bloque «CRITERIO DEL ARQUITECTO» va primero: aplícalo antes de proponer (`query "<tema>" criterio` para ver más).

El hook Stop `.claude/hooks/cierre.mjs` no te deja cerrar un turno con cambios confirmados en Revit, o con un estudio en Blender, sin escribir aquí. Si lo que hiciste fue un ensayo revertido, dilo en una línea.

## Registrar (al terminar algo con resultado)
Añade un párrafo **detrás de la última entrada** de `registro-maestro.md` con este formato. No lo insertes antes de la última: comprueba con `tail` que quedó al final.

`DD/MM (hh:mm) — <Tema> <estado: aplicado | verificado | propuesta | aceptado por el usuario | pausado>. <qué y dónde: IDs, UID, tipo>. <evidencia: recibo/ruta>. <qué se conservó: avisos, recintos, ejes>. <pendientes>. Sin guardar/sincronizar.`

- Distingue la propuesta de la aplicación, y la verificación por API de la revisión visual o en Enscape.
- Si es un aprendizaje reutilizable (método o fallo), añádelo también, al final, a `aprendizajes-operativos.md`. Antes busca si ya existe (`rg`) para ampliarlo en vez de duplicarlo. Si cambia una lección destilada, actualiza su tema en `lecciones-por-tema.md`.
- Si el usuario corrige algo visual o de criterio («no me gusta», «se sale», «chueco»), añádelo a `criterio-del-arquitecto.md` con su frase y su fecha.
- Si corriges una entrada anterior, no la borres: añade una corrección con fecha que diga qué estaba mal y por qué.
- **Decisión de diseño del usuario** (elige una opción, fija un criterio, dice «no quiero…»): regístrala con su frase entre comillas. Añádela también a `.claude/skills/eucalipto-criterio/references/decisiones.md`, así las próximas sesiones proponen lo que él ya eligió.
- No escribas hechos sin comprobarlos en ese momento: avisos, áreas, sincronización (journal) o IDs.
