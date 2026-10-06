#!/usr/bin/env python3
r"""
Carga única de colaboradores (RNF-006): lee la hoja EMPLEADO de Codigo.xlsm (columnas cod_empl y Nom_empl;
las demás se ignoran) y genera datos-locales/colaboradores_inserts.sql con INSERTs para rrhh.tb_Colaborador.

- datos-locales/ está en .gitignore: el SQL contiene nombres reales y NO se commitea.
- No imprime nombres: solo cantidades y los códigos de las filas sin nombre.
- Solo usa la biblioteca estándar de Python (el .xlsm es un zip de XML).

Uso:  python tools/cargar_colaboradores.py [ruta\Codigo.xlsm]
"""
import re
import sys
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

RUTA_EXCEL = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(r"C:\SAPS-datos\Codigo.xlsm")
SALIDA = Path(__file__).resolve().parent.parent / "datos-locales" / "colaboradores_inserts.sql"
HOJA, COL_CODIGO, COL_NOMBRE = "EMPLEADO", "cod_empl", "Nom_empl"
LONGITUD_CODIGO, MAX_NOMBRE = 10, 100

NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
      "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships"}


def leer_filas(ruta):
    with zipfile.ZipFile(ruta) as z:
        textos = []
        if "xl/sharedStrings.xml" in z.namelist():
            for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall("m:si", NS):
                textos.append("".join(t.text or "" for t in si.iter(f"{{{NS['m']}}}t")))
        libro = ET.fromstring(z.read("xl/workbook.xml"))
        rid = next(s.get(f"{{{NS['r']}}}id") for s in libro.find("m:sheets", NS) if s.get("name") == HOJA)
        rels = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
        destino = next(r.get("Target") for r in rels if r.get("Id") == rid).lstrip("/")
        hoja = ET.fromstring(z.read(destino if destino.startswith("xl/") else "xl/" + destino))
        for fila in hoja.iter(f"{{{NS['m']}}}row"):
            celdas = {}
            for c in fila.findall("m:c", NS):
                col = re.match(r"[A-Z]+", c.get("r")).group()
                v = c.find("m:v", NS)
                if c.get("t") == "s" and v is not None:
                    celdas[col] = textos[int(v.text)]
                elif c.get("t") == "inlineStr":
                    celdas[col] = "".join(t.text or "" for t in c.iter(f"{{{NS['m']}}}t"))
                elif v is not None:
                    celdas[col] = v.text
            yield celdas


def limpiar_nombre(nombre):
    n = (nombre or "").replace("¥", "Ñ")
    n = re.sub(r"\s+", " ", n).strip()      # espacios al inicio/final y dobles
    n = n.rstrip(".").strip()               # punto final
    return n                                # el orden de nombres y apellidos NO se toca


def normalizar_codigo(valor):
    c = (valor or "").strip()
    if c.endswith(".0"):
        c = c[:-2]
    return c.zfill(LONGITUD_CODIGO) if c.isdigit() else c


def main():
    filas = list(leer_filas(RUTA_EXCEL))
    encabezado = next(f for f in filas if COL_CODIGO in f.values() and COL_NOMBRE in f.values())
    col_cod = next(k for k, v in encabezado.items() if v == COL_CODIGO)
    col_nom = next(k for k, v in encabezado.items() if v == COL_NOMBRE)
    datos = filas[filas.index(encabezado) + 1:]

    cargar, sin_nombre, vistos = [], [], set()
    for f in datos:
        codigo = normalizar_codigo(f.get(col_cod))
        if not codigo:
            continue
        nombre = limpiar_nombre(f.get(col_nom))
        if not nombre:
            sin_nombre.append(codigo)
            continue
        if codigo in vistos:
            sys.exit(f"Código repetido en el Excel: {codigo}")
        if len(codigo) > LONGITUD_CODIGO or len(nombre) > MAX_NOMBRE:
            sys.exit(f"Código {codigo}: excede la longitud de la columna")
        vistos.add(codigo)
        cargar.append((codigo, nombre))

    SALIDA.parent.mkdir(exist_ok=True)
    with open(SALIDA, "w", encoding="utf-8-sig", newline="\n") as s:
        s.write("-- Carga única de colaboradores (generado por tools/cargar_colaboradores.py). NO commitear.\n")
        s.write("-- Ejecutar con sqlcmd -I -f 65001 sobre SAPS_DB. Es repetible: omite los códigos que ya existen.\n")
        s.write("SET NOCOUNT ON;\nBEGIN TRANSACTION;\n")
        for codigo, nombre in cargar:
            nom = nombre.replace("'", "''")
            s.write(f"INSERT INTO [rrhh].[tb_Colaborador] ([Codigo], [NombreCompleto], [EstaActivo]) "
                    f"SELECT N'{codigo}', N'{nom}', 1 "
                    f"WHERE NOT EXISTS (SELECT 1 FROM [rrhh].[tb_Colaborador] WHERE [Codigo] = N'{codigo}');\n")
        s.write("COMMIT TRANSACTION;\n")

    print(f"Filas leídas con código: {len(cargar) + len(sin_nombre)}")
    print(f"Colaboradores a cargar: {len(cargar)}")
    print(f"Archivo generado: {SALIDA}")
    print("Códigos sin nombre (no se cargan): " + ", ".join(sin_nombre))


if __name__ == "__main__":
    main()
