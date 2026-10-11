#!/usr/bin/env python3
"""Extract the approved fixed catalog, never stock counts. Offline only; emits SQL for separately approved provisioning."""
import argparse
import json
import posixpath
import re
import zipfile
from decimal import Decimal
from pathlib import Path
from xml.etree import ElementTree as ET

S = '{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'
R = '{http://schemas.openxmlformats.org/officeDocument/2006/relationships}'
D = '{http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing}'
A = '{http://schemas.openxmlformats.org/drawingml/2006/main}'

def relationships(z, path):
    rel = posixpath.join(posixpath.dirname(path), '_rels', posixpath.basename(path) + '.rels')
    if rel not in z.namelist():
        return {}
    result = {}
    for node in ET.fromstring(z.read(rel)):
        if node.get('TargetMode') == 'External':
            continue
        target = node.get('Target', '')
        resolved = target.lstrip('/') if target.startswith('/') else posixpath.normpath(posixpath.join(posixpath.dirname(path), target))
        if resolved.startswith('xl/') and '..' not in resolved.split('/'):
            result[node.get('Id')] = resolved
    return result

def extract(source, sheet_name):
    with zipfile.ZipFile(source) as z:
        if len(z.infolist()) > 2000 or sum(i.file_size for i in z.infolist()) > 80 * 1024 * 1024:
            raise ValueError('Workbook exceeds supported size')
        shared = []
        if 'xl/sharedStrings.xml' in z.namelist():
            shared = [''.join(n.itertext()) for n in ET.fromstring(z.read('xl/sharedStrings.xml'))]
        sheets = ET.fromstring(z.read('xl/workbook.xml')).find(S + 'sheets')
        match = next((n for n in sheets if n.get('name') == sheet_name), None)
        if match is None:
            raise ValueError('Requested sheet not found')
        path = relationships(z, 'xl/workbook.xml')[match.get(R + 'id')]
        root = ET.fromstring(z.read(path))
        cells = {n.get('r'): n for n in root.iter(S + 'c')}
        def value(address):
            node = cells.get(address)
            if node is None:
                return ''
            if node.find(S + 'f') is not None:
                raise ValueError('Catalog metadata must not contain formulas: ' + address)
            raw = node.findtext(S + 'v', '')
            if node.get('t') == 's':
                return shared[int(raw)].strip()
            if node.get('t') == 'inlineStr':
                return ''.join(node.find(S + 'is').itertext()).strip()
            return raw.strip()
        expected = {'N3':'제작중','O3':'키팅대차','P3':'적재 랙','T3':'자재실 합계','U3':'ERP','V3':'이동중'}
        for cell, text in expected.items():
            if re.sub(r'\s+', '', value(cell)) != re.sub(r'\s+', '', text):
                raise ValueError('Unsupported header: ' + cell)
        styles = ET.fromstring(z.read('xl/styles.xml'))
        fills = list(styles.find(S + 'fills'))
        xfs = list(styles.find(S + 'cellXfs'))
        def color(address):
            node = cells.get(address)
            if node is None:
                return '#FFFFFF'
            fill = fills[int(xfs[int(node.get('s', '0'))].get('fillId', '0'))]
            fg = fill.find('.//' + S + 'fgColor')
            if fg is None:
                return '#FFFFFF'
            if fg.get('rgb'):
                return '#' + fg.get('rgb')[-6:]
            return '#D9D9D9' if fg.get('theme') == '0' and fg.get('tint') else '#FFFFFF'
        images = {}
        image_keys = {}
        for draw in root.findall(S + 'drawing'):
            dp = relationships(z, path)[draw.get(R + 'id')]
            rels = relationships(z, dp)
            for anchor in ET.fromstring(z.read(dp)):
                row_node = anchor.find(D + 'from/' + D + 'row')
                blip = anchor.find('.//' + A + 'blip')
                if row_node is None or blip is None:
                    continue
                row = int(row_node.text) + 1
                if not 4 <= row <= 95:
                    continue
                ip = rels.get(blip.get(R + 'embed'))
                if not ip or not ip.lower().endswith(('.png', '.jpg', '.jpeg')):
                    raise ValueError('Unsupported item image')
                key = f'item-{row-3:02d}-{len(image_keys.get(row, []))+1}' + Path(ip).suffix.lower()
                images[key] = z.read(ip)
                image_keys.setdefault(row, []).append(key)
        result = []
        category = ''
        for row in range(4, 96):
            category = value(f'A{row}') or category
            if category not in ('사급', '도급', '부자재'):
                raise ValueError('Unexpected category')
            per_set = Decimal(value(f'H{row}'))
            if not per_set.is_finite() or per_set <= 0 or not value(f'E{row}'):
                raise ValueError('Missing item code or invalid SET quantity')
            result.append(dict(id=row-3, sourceRow=row, category=category,
                **{key:value(f'{col}{row}') for key,col in {'vendor':'C','name':'D','code':'E','spec':'F','unit':'G','packBox':'I','packPalletEa':'J','packPalletBox':'K','packNote':'L'}.items()},
                setQuantity=float(per_set), imageKeys=image_keys.get(row, []),
                cellFills={col:color(f'{col}{row}') for col in ['A','B','C','D','E','F','G','H','N','O','P','Q','R','T','V','W','Y','Z','AA']}))
        if any(value(f'E{row}') for row in range(96, max(int(re.search(r'\d+', key).group()) for key in cells) + 1)):
            raise ValueError('Extra item rows are not supported')
        return result, images

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('workbook', type=Path)
    parser.add_argument('--sheet', required=True)
    parser.add_argument('--output-dir', required=True, type=Path)
    args = parser.parse_args()
    catalog, images = extract(args.workbook, args.sheet)
    # Private source-derived outputs must remain outside the checkout.
    output = args.output_dir.resolve()
    repository = Path(__file__).resolve().parents[1]
    if repository == output or repository in output.parents:
        raise ValueError('Output must be outside the repository')
    output.mkdir(mode=0o700, parents=True, exist_ok=False)
    (output / 'images').mkdir(mode=0o700)
    text = json.dumps(catalog, ensure_ascii=False, indent=2)
    (output / 'catalog.json').write_text(text, encoding='utf-8')
    for key, data in images.items():
        (output / 'images' / key).write_bytes(data)
    # This file is never executed by this helper. Identity and empty-catalog guards fail closed.
    sql = """begin;
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM qms_database_identity WHERE singleton AND database_kind='business' AND business_unit_code='CHEONGJU') THEN
  RAISE EXCEPTION 'Cheongju business DB identity required';
 END IF;
END $$;
LOCK TABLE g2_material_items IN EXCLUSIVE MODE;
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM g2_material_items) THEN RAISE EXCEPTION 'Catalog already provisioned'; END IF;
END $$;
"""
    for item in catalog:
        metadata = json.dumps(item, ensure_ascii=False).replace("'", "''")
        sql += f"INSERT INTO g2_material_items(id, source_row, metadata) VALUES ({item['id']},{item['sourceRow']},'{metadata}'::jsonb);\n"
    (output / 'catalog.sql').write_text(sql + 'commit;\n', encoding='utf-8')
    print(f'Prepared {len(catalog)} catalog items and {len(images)} images. No stock counts, DB connection or DB changes.')

if __name__ == '__main__':
    main()
